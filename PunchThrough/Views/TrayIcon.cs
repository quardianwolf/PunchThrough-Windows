using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using PunchThrough.Models;
using PunchThrough.Services;
namespace PunchThrough.Views;

public class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly AppState _appState;
    private readonly ContextMenuStrip _contextMenu;
    private SettingsWindow? _settingsWindow;

    // Menu items that need updating
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _toggleItem;

    public TrayIcon(AppState appState)
    {
        _appState = appState;

        // Status item (disabled, just for display)
        _statusItem = new ToolStripMenuItem(appState.StatusText) { Enabled = false };

        // Connect/Disconnect toggle
        _toggleItem = new ToolStripMenuItem(Strings.Get("Connect"));
        _toggleItem.Click += OnToggleClick;

        // Settings
        var settingsItem = new ToolStripMenuItem(Strings.Get("ShowSettings"));
        settingsItem.Click += OnSettingsClick;

        // Reset
        var resetItem = new ToolStripMenuItem(Strings.Get("ResetAndQuit"));
        resetItem.Click += OnResetClick;

        // Quit
        var quitItem = new ToolStripMenuItem(Strings.Get("Quit"));
        quitItem.Click += OnQuitClick;

        // Build context menu
        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add(_statusItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_toggleItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(settingsItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(resetItem);
        _contextMenu.Items.Add(quitItem);

        // Create NotifyIcon
        _notifyIcon = new NotifyIcon
        {
            Text = "PunchThrough",
            ContextMenuStrip = _contextMenu,
            Visible = false
        };

        _notifyIcon.DoubleClick += OnSettingsClick;

        // Listen for state changes
        _appState.PropertyChanged += OnAppStateChanged;

        UpdateIcon();
        UpdateMenu();
    }

    public void Show() => _notifyIcon.Visible = true;

    private void OnAppStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.ConnectionStatus) or nameof(AppState.StatusText) or nameof(AppState.StatusIcon))
        {
            UpdateIcon();
            UpdateMenu();
        }
    }

    private void UpdateIcon()
    {
        // Use a colored icon based on connection state
        _notifyIcon.Icon = _appState.ConnectionStatus.State switch
        {
            ConnectionState.Connected => CreateColorIcon(Color.LimeGreen),
            ConnectionState.Connecting or ConnectionState.Disconnecting => CreateColorIcon(Color.Orange),
            ConnectionState.Error => CreateColorIcon(Color.Red),
            _ => CreateColorIcon(Color.Gray)
        };

        _notifyIcon.Text = $"PunchThrough - {_appState.StatusText}";
    }

    private void UpdateMenu()
    {
        _statusItem.Text = _appState.StatusText;

        if (_appState.ConnectionStatus.State == ConnectionState.Connected)
        {
            _statusItem.Text += $" \u2022 {_appState.EffectiveDns}";
        }

        _toggleItem.Text = _appState.ConnectionStatus.IsActive
            ? Strings.Get("Disconnect")
            : Strings.Get("Connect");

        _toggleItem.Enabled = _appState.ConnectionStatus.CanToggle;
    }

    private async void OnToggleClick(object? sender, EventArgs e)
    {
        await BypassService.Instance.ToggleAsync(_appState);
    }

    private void OnSettingsClick(object? sender, EventArgs e)
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_appState);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private async void OnResetClick(object? sender, EventArgs e)
    {
        // Stop SpoofDPI and disable proxy
        await BypassService.Instance.DisconnectAsync(_appState);

        // Remove startup registry entry
        StartupService.SetEnabled(false);

        // Disable system proxy (safety net)
        ProxyService.SetProxy(false, 0);

        _appState.AddLog("All settings reset. You can safely delete PunchThrough.");
        Application.Current.Shutdown();
    }

    private static void OnQuitClick(object? sender, EventArgs e)
    {
        Application.Current.Shutdown();
    }

    // Generate a simple colored circle icon programmatically
    private static Icon CreateColorIcon(Color color)
    {
        using var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, 1, 1, 14, 14);

        // Add a subtle border
        using var pen = new Pen(Color.FromArgb(80, 0, 0, 0), 1);
        graphics.DrawEllipse(pen, 1, 1, 14, 14);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _appState.PropertyChanged -= OnAppStateChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
    }
}
