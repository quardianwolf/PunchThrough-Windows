using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
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
        // General
        ChkLaunchAtStartup.IsChecked = StartupService.IsEnabled();
        ChkAutoConnect.IsChecked = _appState.AutoConnect;

        // Language
        CmbLanguage.Items.Clear();
        foreach (var lang in Enum.GetValues<AppLanguage>())
            CmbLanguage.Items.Add(lang.GetDisplayName());
        CmbLanguage.SelectedIndex = (int)_appState.AppLanguage;

        // Bypass mode
        CmbProxyMode.Items.Clear();
        foreach (var mode in Enum.GetValues<ProxyMode>())
            CmbProxyMode.Items.Add(mode.GetDisplayName());
        CmbProxyMode.SelectedIndex = (int)_appState.ProxyMode;
        UpdateProxyModeUI();

        // DNS
        CmbDnsServer.Items.Clear();
        foreach (var dns in Enum.GetValues<DnsServer>())
            CmbDnsServer.Items.Add(dns.GetDisplayName());
        CmbDnsServer.SelectedIndex = (int)_appState.DnsServer;
        TxtCustomDns.Text = _appState.CustomDns;
        CustomDnsPanel.Visibility = _appState.DnsServer == DnsServer.Custom
            ? Visibility.Visible : Visibility.Collapsed;
        ChkDoH.IsChecked = _appState.EnableDoH;

        // Logs
        LogList.ItemsSource = _appState.Logs;
        UpdateLogView();

        // Localize all text
        ApplyStrings();
        UpdateConnectionStatus();
    }

    private void ApplyStrings()
    {
        // Tabs
        TabGeneral.Header = Strings.Get("General");
        TabBypass.Header = Strings.Get("Bypass");
        TabLogs.Header = Strings.Get("Logs");

        // General tab
        LblStartup.Text = Strings.Get("LaunchAtStartup").Split(' ')[0]; // "Startup" / "Baslangic"
        ChkLaunchAtStartup.Content = Strings.Get("LaunchAtStartup");
        ChkAutoConnect.Content = Strings.Get("AutoConnect");
        LblLanguage.Text = Strings.Get("Language");
        LblConnection.Text = Strings.Get("Connection");
        LblStatus.Text = Strings.Get("Status");
        LblMethod.Text = Strings.Get("Method");
        LblAbout.Text = Strings.Get("About");

        // Bypass tab
        LblBypassMode.Text = Strings.Get("Bypass") + " Mode";
        ChkDoH.Content = Strings.Get("UseDoH");

        // Logs tab
        TxtNoLogs.Text = Strings.Get("NoLogs");
        BtnClearLogs.Content = Strings.Get("ClearLogs");
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
                UpdateConnectionStatus();
        });
    }

    private void UpdateConnectionStatus()
    {
        TxtStatus.Text = _appState.StatusText;
        StatusDot.Fill = _appState.ConnectionStatus.State switch
        {
            ConnectionState.Connected => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50)),
            ConnectionState.Connecting or ConnectionState.Disconnecting => new SolidColorBrush(Colors.Orange),
            ConnectionState.Error => new SolidColorBrush(Colors.Red),
            _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0xCC, 0xCC))
        };

        if (_appState.ConnectionStatus.State == ConnectionState.Connected)
        {
            ConnectedInfo.Visibility = Visibility.Visible;
            TxtMethod.Text = "Zapret";
            TxtDns.Text = _appState.EffectiveDns;
        }
        else
        {
            ConnectedInfo.Visibility = Visibility.Collapsed;
        }
    }

    // --- Event handlers ---

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e) => DragMove();
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

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

        // Re-apply strings immediately
        _isInitializing = true;
        CmbProxyMode.Items.Clear();
        foreach (var mode in Enum.GetValues<ProxyMode>())
            CmbProxyMode.Items.Add(mode.GetDisplayName());
        CmbProxyMode.SelectedIndex = (int)_appState.ProxyMode;
        _isInitializing = false;

        ApplyStrings();
        UpdateProxyModeUI();
    }

    private async void OnProxyModeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        _appState.ProxyMode = (ProxyMode)CmbProxyMode.SelectedIndex;
        UpdateProxyModeUI();
        _appState.SaveSettings();

        if (_appState.ConnectionStatus.State == ConnectionState.Connected)
        {
            await BypassService.Instance.DisconnectAsync(_appState);
            await BypassService.Instance.ConnectAsync(_appState);
        }
    }

    private void UpdateProxyModeUI()
    {
        if (CmbProxyMode.SelectedIndex < 0) return;
        var mode = (ProxyMode)CmbProxyMode.SelectedIndex;
        TxtProxyModeDesc.Text = mode switch
        {
            ProxyMode.Full => Strings.Get("ProxyModeFullDesc"),
            ProxyMode.DiscordOnly => Strings.Get("ProxyModeDiscordDesc"),
            ProxyMode.Custom => Strings.Get("ProxyModeCustomDesc"),
            _ => ""
        };
        CustomDomainsPanel.Visibility = mode == ProxyMode.Custom
            ? Visibility.Visible : Visibility.Collapsed;
        if (mode == ProxyMode.Custom)
        {
            RefreshDomainList();
            BuildPresetButtons();
        }
    }

    private void RefreshDomainList()
    {
        DomainList.Items.Clear();
        foreach (var d in _appState.CustomProxyDomains)
            DomainList.Items.Add(d);
    }

    private void BuildPresetButtons()
    {
        PresetPanel.Children.Clear();
        foreach (var (category, domains) in KnownDomains.Presets)
        {
            var btn = new System.Windows.Controls.Button
            {
                Content = $"+ {category}",
                FontSize = 11,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 4, 4),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xF5, 0xE9)),
                BorderThickness = new Thickness(0)
            };
            var captured = domains;
            btn.Click += async (_, _) =>
            {
                foreach (var d in captured)
                    if (!_appState.CustomProxyDomains.Contains(d))
                        _appState.CustomProxyDomains.Add(d);
                RefreshDomainList();
                _appState.SaveSettings();
                await ReconnectIfNeeded();
            };
            PresetPanel.Children.Add(btn);
        }
    }

    private async void OnAddDomain(object sender, RoutedEventArgs e)
    {
        var domain = TxtNewDomain.Text.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(domain)) return;
        if (!_appState.CustomProxyDomains.Contains(domain))
        {
            _appState.CustomProxyDomains.Add(domain);
            RefreshDomainList();
            _appState.SaveSettings();
            await ReconnectIfNeeded();
        }
        TxtNewDomain.Text = "";
    }

    private async void OnRemoveDomain(object sender, RoutedEventArgs e)
    {
        if (DomainList.SelectedItem is string domain)
        {
            _appState.CustomProxyDomains.Remove(domain);
            RefreshDomainList();
            _appState.SaveSettings();
            await ReconnectIfNeeded();
        }
    }

    private async Task ReconnectIfNeeded()
    {
        if (_appState.ConnectionStatus.State == ConnectionState.Connected)
        {
            await BypassService.Instance.DisconnectAsync(_appState);
            await BypassService.Instance.ConnectAsync(_appState);
        }
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

    private void OnGitHubLinkClick(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.Invoke(UpdateLogView);

    private void UpdateLogView()
    {
        var hasLogs = _appState.Logs.Count > 0;
        TxtNoLogs.Visibility = hasLogs ? Visibility.Collapsed : Visibility.Visible;
        LogList.Visibility = hasLogs ? Visibility.Visible : Visibility.Collapsed;
        TxtLogCount.Text = $"{_appState.Logs.Count} {Strings.Get("Entries")}";
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
