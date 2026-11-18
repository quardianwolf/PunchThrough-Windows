using System.ComponentModel;
using System.Drawing;
using System.Reflection;
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

    private static readonly Icon ConnectedIcon = LoadEmbeddedIcon("PunchThrough.Assets.connected.ico");
    private static readonly Icon DisconnectedIcon = LoadEmbeddedIcon("PunchThrough.Assets.disconnected.ico");

    // Menu items that need updating
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _tunnelItem;
    private readonly ToolStripMenuItem _toggleItem;

    public TrayIcon(AppState appState)
    {
        _appState = appState;

        // Status item (disabled, just for display)
        _statusItem = new ToolStripMenuItem(appState.StatusText) { Enabled = false };
        _tunnelItem = new ToolStripMenuItem("") { Enabled = false, Visible = false };

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
        _contextMenu.Items.Add(_tunnelItem);
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
        if (e.PropertyName is not (nameof(AppState.ConnectionStatus) or nameof(AppState.StatusText)
            or nameof(AppState.StatusIcon) or nameof(AppState.TunnelStatusText)))
            return;

        // State can change from background work (watchdog, tunnel monitor); the menu belongs to the UI thread
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => { UpdateIcon(); UpdateMenu(); });
            return;
        }

        UpdateIcon();
        UpdateMenu();
    }

    private void UpdateIcon()
    {
        _notifyIcon.Icon = _appState.ConnectionStatus.State switch
        {
            ConnectionState.Connected => ConnectedIcon,
            _ => DisconnectedIcon
        };

        // The tooltip has a hard length limit and error messages can be long
        var text = $"PunchThrough - {_appState.StatusText}";
        _notifyIcon.Text = text.Length > 120 ? text[..120] : text;
    }

    private void UpdateMenu()
    {
        _statusItem.Text = _appState.StatusText;

        if (_appState.ConnectionStatus.State == ConnectionState.Connected)
        {
            _statusItem.Text += $" \u2022 {_appState.EffectiveDns}";
        }

        var showTunnel = _appState.ConnectionStatus.State == ConnectionState.Connected
            && !string.IsNullOrEmpty(_appState.TunnelStatusText);
        _tunnelItem.Visible = showTunnel;
        if (showTunnel)
        {
            var tunnel = Strings.Format("TunnelMenu", _appState.TunnelStatusText);
            _tunnelItem.Text = tunnel.Length > 90 ? tunnel[..90] + "..." : tunnel;
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
        var answer = System.Windows.MessageBox.Show(
            Strings.Get("ResetConfirm"), Strings.Get("ResetTitle"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        await BypassService.Instance.DisconnectAsync(_appState);
        BypassService.Instance.ShutdownSync(); // also covers a state where Disconnect was not allowed
        InstallService.Uninstall();
        Application.Current.Shutdown();
    }

    private static void OnQuitClick(object? sender, EventArgs e)
    {
        Application.Current.Shutdown();
    }

    private static Icon LoadEmbeddedIcon(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        return stream != null ? new Icon(stream, 16, 16) : SystemIcons.Application;
    }

    public void Dispose()
    {
        _appState.PropertyChanged -= OnAppStateChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
    }
}
