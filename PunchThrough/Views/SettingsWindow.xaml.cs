using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using PunchThrough.Models;
using PunchThrough.Services;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;

namespace PunchThrough.Views;

public partial class SettingsWindow : Window
{
    private static readonly Brush OkBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
    private static readonly Brush FailBrush = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
    private static readonly Brush PendingBrush = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));

    private readonly AppState _appState;
    private readonly List<string> _tunnelDomains;
    private bool _isInitializing = true;

    public SettingsWindow(AppState appState)
    {
        InitializeComponent();
        _appState = appState;
        _tunnelDomains = new List<string>(appState.TunnelDomains);

        InitializeControls();
        BindState();

        _isInitializing = false;
    }

    private void InitializeControls()
    {
        // General
        ChkLaunchAtStartup.IsChecked = StartupService.IsEnabled();
        ChkAutoConnect.IsChecked = _appState.AutoConnect;

        // Bypass
        TxtCustomDns.Text = _appState.CustomDns;
        ChkDoH.IsChecked = _appState.EnableDoH;
        ChkQuic.IsChecked = _appState.EnableQuic;

        // Tunnel
        TxtTunnelHost.Text = _appState.TunnelHost;
        TxtTunnelPort.Text = _appState.TunnelPort.ToString();
        TxtTunnelUser.Text = _appState.TunnelUser;
        TxtTunnelKey.Text = _appState.TunnelKeyPath;
        RefreshTunnelDomainList();

        // Logs
        LogList.ItemsSource = _appState.Logs;
        UpdateLogView();

        PopulateCombos();
        ApplyStrings();
        ResetTunnelChecks();
        UpdateConnectionStatus();
    }

    /// <summary>Fill every combo box with display names in the current language.</summary>
    private void PopulateCombos()
    {
        var tunnelMode = CmbTunnelMode.SelectedIndex >= 0 ? CmbTunnelMode.SelectedIndex : (int)_appState.TunnelMode;

        Fill(CmbLanguage, Enum.GetValues<AppLanguage>().Select(l => l.GetDisplayName()), (int)_appState.AppLanguage);
        Fill(CmbProxyMode, Enum.GetValues<ProxyMode>().Select(m => m.GetDisplayName()), (int)_appState.ProxyMode);
        Fill(CmbDnsServer, Enum.GetValues<DnsServer>().Select(d => d.GetDisplayName()), (int)_appState.DnsServer);
        Fill(CmbTunnelMode, Enum.GetValues<TunnelMode>().Select(m => m.GetDisplayName()), tunnelMode);

        CustomDnsPanel.Visibility = _appState.DnsServer == DnsServer.Custom
            ? Visibility.Visible : Visibility.Collapsed;
        UpdateProxyModeUI();
        UpdateTunnelModeUI();
    }

    private static void Fill(ComboBox combo, IEnumerable<string> items, int selected)
    {
        combo.Items.Clear();
        foreach (var item in items)
            combo.Items.Add(item);
        combo.SelectedIndex = selected;
    }

    private void ApplyStrings()
    {
        // Tabs
        TabGeneral.Header = Strings.Get("General");
        TabBypass.Header = Strings.Get("Bypass");
        TabTunnel.Header = Strings.Get("Tunnel");
        TabLogs.Header = Strings.Get("Logs");

        // General tab
        LblStartup.Text = Strings.Get("Startup");
        ChkLaunchAtStartup.Content = Strings.Get("LaunchAtStartup");
        ChkAutoConnect.Content = Strings.Get("AutoConnect");
        LblLanguage.Text = Strings.Get("Language");
        LblConnection.Text = Strings.Get("Connection");
        LblStatus.Text = Strings.Get("Status");
        LblMethod.Text = Strings.Get("Method");
        LblTunnelInfo.Text = Strings.Get("TunnelStatusLabel");
        LblAbout.Text = Strings.Get("About");

        // Bypass tab
        LblBypassMode.Text = Strings.Get("BypassMode");
        LblCustomDomains.Text = Strings.Get("CustomDomains");
        LblQuickAdd.Text = Strings.Get("QuickAdd");
        ChkDoH.Content = Strings.Get("UseDoH");
        ChkQuic.Content = Strings.Get("UseQuic");
        LblStrategy.Text = Strings.Get("BypassStrategy");
        BtnAutoDetect.Content = Strings.Get("AutoDetectStrategy");
        BtnDiagnostics.Content = Strings.Get("RunDiagnostics");
        UpdateCurrentStrategy();

        // Tunnel tab
        TxtTunnelIntro.Text = Strings.Get("TunnelIntro");
        RunTunnelGuide.Text = Strings.Get("TunnelGuide");
        LblTunnelStep1.Text = Strings.Get("TunnelStep1");
        LblTunnelStep2.Text = Strings.Get("TunnelStep2");
        LblTunnelStep3.Text = Strings.Get("TunnelStep3");
        LblTunnelStep4.Text = Strings.Get("TunnelStep4");
        BtnInstallSsh.Content = Strings.Get("TunnelInstallSsh");
        LblTunnelHost.Text = Strings.Get("TunnelHostLabel");
        LblTunnelPort.Text = Strings.Get("TunnelPortLabel");
        LblTunnelUser.Text = Strings.Get("TunnelUserLabel");
        LblTunnelKey.Text = Strings.Get("TunnelKeyLabel");
        BtnTunnelBrowse.Content = Strings.Get("TunnelBrowse");
        BtnTunnelTest.Content = Strings.Get("TunnelTest");
        TxtTunnelSitesHint.Text = Strings.Get("TunnelSitesHint");
        BtnTunnelApply.Content = Strings.Get("TunnelApply");

        // Logs tab
        TxtNoLogs.Text = Strings.Get("NoLogs");
        BtnClearLogs.Content = Strings.Get("ClearLogs");
        UpdateLogView();
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
            if (e.PropertyName is nameof(AppState.ConnectionStatus) or nameof(AppState.StatusText)
                or nameof(AppState.TunnelStatusText) or nameof(AppState.TunnelActive))
                UpdateConnectionStatus();
        });
    }

    private void UpdateConnectionStatus()
    {
        TxtStatus.Text = _appState.StatusText;
        StatusDot.Fill = _appState.ConnectionStatus.State switch
        {
            ConnectionState.Connected => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
            ConnectionState.Connecting or ConnectionState.Disconnecting => new SolidColorBrush(Colors.Orange),
            ConnectionState.Error => new SolidColorBrush(Colors.Red),
            _ => new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC))
        };

        if (_appState.ConnectionStatus.State == ConnectionState.Connected)
        {
            ConnectedInfo.Visibility = Visibility.Visible;
            TxtMethod.Text = "Zapret";
            TxtDns.Text = _appState.EffectiveDns;

            var hasTunnel = !string.IsNullOrEmpty(_appState.TunnelStatusText);
            TunnelInfoRow.Visibility = hasTunnel ? Visibility.Visible : Visibility.Collapsed;
            TxtTunnelInfo.Text = _appState.TunnelStatusText;
            TxtTunnelInfo.Foreground = _appState.TunnelActive ? OkBrush : FailBrush;
        }
        else
        {
            ConnectedInfo.Visibility = Visibility.Collapsed;
        }
    }

    // --- Window chrome ---

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e) => DragMove();
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnGitHubLinkClick(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    // --- General tab ---

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

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || CmbLanguage.SelectedIndex < 0) return;
        var lang = (AppLanguage)CmbLanguage.SelectedIndex;
        _appState.AppLanguage = lang;
        lang.Apply();
        _appState.SaveSettings();

        // Re-apply every text immediately
        _isInitializing = true;
        PopulateCombos();
        _isInitializing = false;

        ApplyStrings();
        ResetTunnelChecks();
        UpdateConnectionStatus();
    }

    // --- Bypass tab ---

    private async void OnProxyModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || CmbProxyMode.SelectedIndex < 0) return;
        _appState.ProxyMode = (ProxyMode)CmbProxyMode.SelectedIndex;
        UpdateProxyModeUI();
        _appState.SaveSettings();
        await BypassService.Instance.ReconnectAsync(_appState);
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
            var btn = new Button
            {
                Content = $"+ {category}",
                FontSize = 11,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 4, 4),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xE9)),
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
                await BypassService.Instance.ReconnectAsync(_appState);
            };
            PresetPanel.Children.Add(btn);
        }
    }

    private void OnNewDomainKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OnAddDomain(sender, e);
    }

    private async void OnAddDomain(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNewDomain.Text)) return;

        var domain = DomainUtil.Normalize(TxtNewDomain.Text);
        if (domain == null)
        {
            MessageBox.Show(this, Strings.Get("InvalidDomain"), "PunchThrough", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TxtNewDomain.Text = "";
        if (_appState.CustomProxyDomains.Contains(domain)) return;

        _appState.CustomProxyDomains.Add(domain);
        RefreshDomainList();
        _appState.SaveSettings();
        await BypassService.Instance.ReconnectAsync(_appState);
    }

    private async void OnRemoveDomain(object sender, RoutedEventArgs e)
    {
        if (DomainList.SelectedItem is not string domain) return;

        _appState.CustomProxyDomains.Remove(domain);
        RefreshDomainList();
        _appState.SaveSettings();
        await BypassService.Instance.ReconnectAsync(_appState);
    }

    private async void OnDnsServerChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || CmbDnsServer.SelectedIndex < 0) return;
        _appState.DnsServer = (DnsServer)CmbDnsServer.SelectedIndex;
        CustomDnsPanel.Visibility = _appState.DnsServer == DnsServer.Custom
            ? Visibility.Visible : Visibility.Collapsed;
        _appState.SaveSettings();

        // A custom server is applied once its address has been typed (see OnCustomDnsLostFocus)
        if (_appState.DnsServer != DnsServer.Custom || DnsServerExtensions.IsValidCustom(_appState.CustomDns))
            await BypassService.Instance.ReconnectAsync(_appState);
    }

    private void OnCustomDnsChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing) return;
        _appState.CustomDns = TxtCustomDns.Text.Trim();
        _appState.SaveSettings();

        TxtCustomDns.Foreground = TxtCustomDns.Text.Length == 0 || DnsServerExtensions.IsValidCustom(TxtCustomDns.Text)
            ? Brushes.Black : FailBrush;
    }

    private string _appliedCustomDns = "";

    private async void OnCustomDnsLostFocus(object sender, RoutedEventArgs e)
    {
        // Reconnecting on every keystroke would restart the bypass for each typed digit
        var value = _appState.CustomDns;
        if (value == _appliedCustomDns || !DnsServerExtensions.IsValidCustom(value)) return;
        _appliedCustomDns = value;
        await BypassService.Instance.ReconnectAsync(_appState);
    }

    private async void OnDoHChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _appState.EnableDoH = ChkDoH.IsChecked == true;
        _appState.SaveSettings();
        await BypassService.Instance.ReconnectAsync(_appState);
    }

    private async void OnQuicChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _appState.EnableQuic = ChkQuic.IsChecked == true;
        _appState.SaveSettings();
        await BypassService.Instance.ReconnectAsync(_appState);
    }

    private void UpdateCurrentStrategy()
    {
        TxtCurrentStrategy.Text = string.IsNullOrEmpty(_appState.ZapretStrategy)
            ? Strings.Get("DefaultStrategy")
            : _appState.ZapretStrategy;
    }

    private async void OnDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        BtnDiagnostics.IsEnabled = false;
        TxtAutoDetectStatus.Text = Strings.Get("DiagRunning");
        try
        {
            var path = await DiagnosticsService.RunDiagnosticsAsync();
            TxtAutoDetectStatus.Text = Strings.Format("DiagSaved", Path.GetFileName(path));
            _appState.AddLog(TxtAutoDetectStatus.Text);
        }
        catch (Exception ex)
        {
            TxtAutoDetectStatus.Text = $"{Strings.Get("Error")}: {ex.Message}";
        }
        finally
        {
            BtnDiagnostics.IsEnabled = true;
        }
    }

    private async void OnAutoDetectClick(object sender, RoutedEventArgs e)
    {
        BtnAutoDetect.IsEnabled = false;
        TxtAutoDetectStatus.Text = Strings.Get("Detecting");

        try
        {
            var wasConnected = _appState.ConnectionStatus.State == ConnectionState.Connected;
            if (wasConnected)
                await BypassService.Instance.DisconnectAsync(_appState);

            var progress = new Progress<(int current, int total, string strategy)>(p =>
                TxtAutoDetectStatus.Text = Strings.Format("DetectTesting", p.current, p.total, p.strategy));

            var hosts = StrategyDetector.TestHostsFor(_appState.ProxyMode, _appState.CustomProxyDomains);
            var (args, name) = await StrategyDetector.DetectAsync(hosts, progress);
            if (args != null)
            {
                _appState.ZapretStrategy = args;
                _appState.SaveSettings();
                TxtAutoDetectStatus.Text = Strings.Format("DetectFound", name);
                UpdateCurrentStrategy();
            }
            else
            {
                TxtAutoDetectStatus.Text = Strings.Get("DetectNone");
            }

            if (wasConnected)
                await BypassService.Instance.ConnectAsync(_appState);
        }
        catch (Exception ex)
        {
            TxtAutoDetectStatus.Text = $"{Strings.Get("Error")}: {ex.Message}";
        }
        finally
        {
            BtnAutoDetect.IsEnabled = true;
        }
    }

    // --- Tunnel tab ---

    private static void SetCheck(TextBlock target, bool? ok, string text)
    {
        target.Text = (ok switch { true => "✔  ", false => "✖  ", _ => "•  " }) + text;
        target.Foreground = ok switch { true => OkBrush, false => FailBrush, _ => PendingBrush };
    }

    private void ResetTunnelChecks()
    {
        UpdateSshClientCheck();
        SetCheck(ChkReach, null, Strings.Get("TunnelChkReach"));
        SetCheck(ChkLogin, null, Strings.Get("TunnelChkLogin"));
        SetCheck(ChkExit, null, Strings.Get("TunnelChkExit"));
    }

    private bool UpdateSshClientCheck()
    {
        var found = TunnelService.FindSsh() != null;
        SetCheck(ChkSshClient, found, Strings.Get(found ? "TunnelSshFound" : "TunnelSshMissing"));
        BtnInstallSsh.Visibility = found ? Visibility.Collapsed : Visibility.Visible;
        return found;
    }

    private async void OnInstallSshClick(object sender, RoutedEventArgs e)
    {
        BtnInstallSsh.IsEnabled = false;
        SetCheck(ChkSshClient, null, Strings.Get("TunnelInstallingSsh"));
        try
        {
            await TunnelService.InstallSshAsync();
        }
        catch { }

        if (!UpdateSshClientCheck())
            SetCheck(ChkSshClient, false, Strings.Get("TunnelInstallFailed"));
        BtnInstallSsh.IsEnabled = true;
    }

    private void OnTunnelBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.Get("TunnelKeyLabel"),
            Filter = "SSH private key|*.key;*.pem;id_*;*|All files|*.*",
            CheckFileExists = true
        };

        var sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        if (Directory.Exists(sshDir)) dialog.InitialDirectory = sshDir;

        if (dialog.ShowDialog(this) == true)
            TxtTunnelKey.Text = dialog.FileName;
    }

    /// <summary>The server details as currently typed (not necessarily saved yet).</summary>
    private TunnelConfig ReadTunnelForm()
    {
        int.TryParse(TxtTunnelPort.Text.Trim(), out var port);
        var mode = CmbTunnelMode.SelectedIndex >= 0 ? (TunnelMode)CmbTunnelMode.SelectedIndex : TunnelMode.Off;
        return new TunnelConfig(mode, TxtTunnelHost.Text.Trim(), port, TxtTunnelUser.Text.Trim(),
            TxtTunnelKey.Text.Trim().Trim('"'), _tunnelDomains.ToArray());
    }

    private async void OnTunnelTestClick(object sender, RoutedEventArgs e)
    {
        BtnTunnelTest.IsEnabled = false;
        BtnTunnelTest.Content = Strings.Get("TunnelTesting");
        ResetTunnelChecks();

        try
        {
            await TunnelService.TestAsync(ReadTunnelForm(), (check, ok, text) =>
            {
                var target = check switch
                {
                    TunnelCheck.Client => ChkSshClient,
                    TunnelCheck.Reach => ChkReach,
                    TunnelCheck.Login => ChkLogin,
                    _ => ChkExit
                };
                SetCheck(target, ok, text);
            });
        }
        catch (Exception ex)
        {
            SetCheck(ChkExit, false, ex.Message);
        }
        finally
        {
            BtnTunnelTest.Content = Strings.Get("TunnelTest");
            BtnTunnelTest.IsEnabled = true;
        }
    }

    private void OnTunnelModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        UpdateTunnelModeUI();
    }

    private void UpdateTunnelModeUI()
    {
        if (CmbTunnelMode.SelectedIndex < 0) return;
        var mode = (TunnelMode)CmbTunnelMode.SelectedIndex;
        TxtTunnelModeDesc.Text = mode.GetDescription();
        TunnelSitesPanel.Visibility = mode == TunnelMode.SelectedSites
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshTunnelDomainList()
    {
        TunnelDomainList.Items.Clear();
        foreach (var d in _tunnelDomains)
            TunnelDomainList.Items.Add(d);
    }

    private void OnTunnelDomainKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OnTunnelAddDomain(sender, e);
    }

    private void OnTunnelAddDomain(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtTunnelDomain.Text)) return;

        var domain = DomainUtil.Normalize(TxtTunnelDomain.Text);
        if (domain == null)
        {
            SetTunnelStatus(Strings.Get("InvalidDomain"), ok: false);
            return;
        }

        TxtTunnelDomain.Text = "";
        SetTunnelStatus("", ok: true);
        if (_tunnelDomains.Contains(domain)) return;

        _tunnelDomains.Add(domain);
        RefreshTunnelDomainList();
    }

    private void OnTunnelRemoveDomain(object sender, RoutedEventArgs e)
    {
        if (TunnelDomainList.SelectedItem is not string domain) return;
        _tunnelDomains.Remove(domain);
        RefreshTunnelDomainList();
    }

    private void SetTunnelStatus(string text, bool ok)
    {
        TxtTunnelStatus.Text = text;
        TxtTunnelStatus.Foreground = ok ? PendingBrush : FailBrush;
    }

    private async void OnTunnelApplyClick(object sender, RoutedEventArgs e)
    {
        // A domain still sitting in the input box is clearly meant to be included
        OnTunnelAddDomain(sender, e);

        var config = ReadTunnelForm();
        if (config.Mode != TunnelMode.Off)
        {
            if (TunnelService.FindSsh() == null)
            {
                SetTunnelStatus(Strings.Get("TunnelSshMissing"), ok: false);
                return;
            }

            var problem = config.Validate();
            if (problem != null)
            {
                SetTunnelStatus(problem, ok: false);
                return;
            }

            if (config.Mode == TunnelMode.SelectedSites && _tunnelDomains.Count == 0)
            {
                SetTunnelStatus(Strings.Get("TunnelNeedSites"), ok: false);
                return;
            }
        }

        _appState.TunnelMode = config.Mode;
        _appState.TunnelHost = config.Host;
        _appState.TunnelPort = config.Port is >= 1 and <= 65535 ? config.Port : 22;
        _appState.TunnelUser = config.User;
        _appState.TunnelKeyPath = config.KeyPath;
        _appState.TunnelDomains = new List<string>(_tunnelDomains);
        _appState.SaveSettings();

        if (_appState.ConnectionStatus.State != ConnectionState.Connected)
        {
            SetTunnelStatus(Strings.Get("TunnelSavedInactive"), ok: true);
            return;
        }

        BtnTunnelApply.IsEnabled = false;
        SetTunnelStatus(Strings.Get("TunnelSaved"), ok: true);
        try
        {
            await BypassService.Instance.RestartTunnelAsync(_appState);
            SetTunnelStatus(_appState.TunnelStatusText, ok: _appState.TunnelActive || config.Mode == TunnelMode.Off);
        }
        finally
        {
            BtnTunnelApply.IsEnabled = true;
        }
    }

    // --- Logs tab ---

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
