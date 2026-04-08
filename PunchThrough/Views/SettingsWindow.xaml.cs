using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using PunchThrough.Models;
using PunchThrough.Services;

namespace PunchThrough.Views;

public partial class SettingsWindow : Window
{
    private readonly AppState _appState;
    private bool _isInitializing = true;

    public SettingsWindow(AppState appState)
    {
        InitializeComponent();
        _appState = appState;

        InitializeControls();
        BindState();

        _isInitializing = false;
    }

    private void InitializeControls()
    {
        // General tab
        ChkLaunchAtStartup.IsChecked = StartupService.IsEnabled();
        ChkAutoConnect.IsChecked = _appState.AutoConnect;

        // Language combo
        CmbLanguage.Items.Clear();
        foreach (var lang in Enum.GetValues<AppLanguage>())
            CmbLanguage.Items.Add(lang.GetDisplayName());
        CmbLanguage.SelectedIndex = (int)_appState.AppLanguage;

        // Bypass tab
        CmbDnsServer.Items.Clear();
        foreach (var dns in Enum.GetValues<DnsServer>())
            CmbDnsServer.Items.Add(dns.GetDisplayName());
        CmbDnsServer.SelectedIndex = (int)_appState.DnsServer;

        TxtCustomDns.Text = _appState.CustomDns;
        CustomDnsPanel.Visibility = _appState.DnsServer == DnsServer.Custom
            ? Visibility.Visible : Visibility.Collapsed;

        ChkDoH.IsChecked = _appState.EnableDoH;
        TxtPort.Text = _appState.SpoofDpiPort.ToString();
        ChkSystemProxy.IsChecked = _appState.EnableSystemProxy;

        // Check SpoofDPI installation
        UpdateInstallationStatus();

        // Logs
        LogList.ItemsSource = _appState.Logs;
        UpdateLogView();

        // Apply localized strings
        ApplyStrings();
    }

    private void ApplyStrings()
    {
        TabGeneral.Header = Strings.Get("General");
        TabBypass.Header = Strings.Get("Bypass");
        TabLogs.Header = Strings.Get("Logs");
        TxtRestartHint.Text = Strings.Get("RestartLanguage");
        BtnClearLogs.Content = Strings.Get("ClearLogs");
        TxtNoLogs.Text = Strings.Get("NoLogs");
    }

    private void BindState()
    {
        _appState.PropertyChanged += OnAppStateChanged;
        ((INotifyCollectionChanged)_appState.Logs).CollectionChanged += OnLogsChanged;
    }

    private void OnAppStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.PropertyName is nameof(AppState.ConnectionStatus) or nameof(AppState.StatusText))
            {
                UpdateConnectionStatus();
            }
        });
    }

    private void UpdateConnectionStatus()
    {
        TxtStatus.Text = _appState.StatusText;
        StatusDot.Fill = _appState.ConnectionStatus.State switch
        {
            ConnectionState.Connected => new SolidColorBrush(Colors.LimeGreen),
            ConnectionState.Connecting or ConnectionState.Disconnecting => new SolidColorBrush(Colors.Orange),
            ConnectionState.Error => new SolidColorBrush(Colors.Red),
            _ => new SolidColorBrush(Colors.Gray)
        };

        if (_appState.ConnectionStatus.State == ConnectionState.Connected)
        {
            ConnectedInfo.Visibility = Visibility.Visible;
            TxtMethod.Text = "SpoofDPI";
            TxtDns.Text = _appState.EffectiveDns;
        }
        else
        {
            ConnectedInfo.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateInstallationStatus()
    {
        var installed = SpoofDPIService.IsSpoofDPIInstalled();
        InstallDot.Fill = installed
            ? new SolidColorBrush(Colors.LimeGreen)
            : new SolidColorBrush(Colors.Red);
        TxtInstallStatus.Text = installed ? Strings.Get("Installed") : Strings.Get("NotInstalled");
        InstallHintPanel.Visibility = installed ? Visibility.Collapsed : Visibility.Visible;
    }

    // Event handlers
    private void OnLaunchAtStartupChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var enabled = ChkLaunchAtStartup.IsChecked == true;
        StartupService.SetEnabled(enabled);
        _appState.LaunchAtStartup = enabled;
        _appState.SaveSettings();
    }

    private void OnAutoConnectChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _appState.AutoConnect = ChkAutoConnect.IsChecked == true;
        _appState.SaveSettings();
    }

    private void OnLanguageChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        var lang = (AppLanguage)CmbLanguage.SelectedIndex;
        _appState.AppLanguage = lang;
        lang.Apply();
        _appState.SaveSettings();
        _appState.AddLog("Language changed. Restart app to apply.");
    }

    private void OnDnsServerChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        _appState.DnsServer = (DnsServer)CmbDnsServer.SelectedIndex;
        CustomDnsPanel.Visibility = _appState.DnsServer == DnsServer.Custom
            ? Visibility.Visible : Visibility.Collapsed;
        _appState.SaveSettings();
    }

    private void OnCustomDnsChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isInitializing) return;
        _appState.CustomDns = TxtCustomDns.Text;
        _appState.SaveSettings();
    }

    private void OnDoHChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _appState.EnableDoH = ChkDoH.IsChecked == true;
        _appState.SaveSettings();
    }

    private void OnPortChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (int.TryParse(TxtPort.Text, out var port) && port is > 0 and < 65536)
        {
            _appState.SpoofDpiPort = port;
            _appState.SaveSettings();
        }
    }

    private void OnSystemProxyChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _appState.EnableSystemProxy = ChkSystemProxy.IsChecked == true;
        _appState.SaveSettings();
    }

    private void OnCopyInstallCommand(object sender, RoutedEventArgs e)
    {
        System.Windows.Clipboard.SetText("scoop install spoofdpi");
    }

    private void OnGitHubLinkClick(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    // Logs
    private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.Invoke(UpdateLogView);
    }

    private void UpdateLogView()
    {
        var hasLogs = _appState.Logs.Count > 0;
        TxtNoLogs.Visibility = hasLogs ? Visibility.Collapsed : Visibility.Visible;
        LogList.Visibility = hasLogs ? Visibility.Visible : Visibility.Collapsed;
        TxtLogCount.Text = $"{_appState.Logs.Count} {Strings.Get("Entries")}";

        // Auto-scroll to bottom
        if (hasLogs && LogList.Items.Count > 0)
            LogList.ScrollIntoView(LogList.Items[^1]);
    }

    private void OnClearLogs(object sender, RoutedEventArgs e)
    {
        _appState.ClearLogs();
        UpdateLogView();
    }

    protected override void OnClosed(EventArgs e)
    {
        _appState.PropertyChanged -= OnAppStateChanged;
        ((INotifyCollectionChanged)_appState.Logs).CollectionChanged -= OnLogsChanged;
        base.OnClosed(e);
    }
}
