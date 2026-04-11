using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PunchThrough.Models;

public class AppState : INotifyPropertyChanged
{
    private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;
    private BypassMethod _selectedMethod = BypassMethod.SpoofDPI;
    private DnsServer _dnsServer = DnsServer.Cloudflare;
    private string _customDns = "";
    private int _spoofDpiPort = 8080;
    private bool _enableDoH = true;
    private bool _enableSystemProxy = true;
    private bool _launchAtStartup;
    private bool _autoConnect;
    private ProxyMode _proxyMode = ProxyMode.Full;
    private List<string> _customProxyDomains = new();
    private AppLanguage _appLanguage = AppLanguage.System;

    public ConnectionStatus ConnectionStatus
    {
        get => _connectionStatus;
        set => SetField(ref _connectionStatus, value);
    }

    public BypassMethod SelectedMethod
    {
        get => _selectedMethod;
        set => SetField(ref _selectedMethod, value);
    }

    public DnsServer DnsServer
    {
        get => _dnsServer;
        set { SetField(ref _dnsServer, value); OnPropertyChanged(nameof(EffectiveDns)); }
    }

    public string CustomDns
    {
        get => _customDns;
        set { SetField(ref _customDns, value); OnPropertyChanged(nameof(EffectiveDns)); }
    }

    public int SpoofDpiPort
    {
        get => _spoofDpiPort;
        set => SetField(ref _spoofDpiPort, value);
    }

    public bool EnableDoH
    {
        get => _enableDoH;
        set => SetField(ref _enableDoH, value);
    }

    public bool EnableSystemProxy
    {
        get => _enableSystemProxy;
        set => SetField(ref _enableSystemProxy, value);
    }

    public bool LaunchAtStartup
    {
        get => _launchAtStartup;
        set => SetField(ref _launchAtStartup, value);
    }

    public bool AutoConnect
    {
        get => _autoConnect;
        set => SetField(ref _autoConnect, value);
    }

    public ProxyMode ProxyMode
    {
        get => _proxyMode;
        set => SetField(ref _proxyMode, value);
    }

    public List<string> CustomProxyDomains
    {
        get => _customProxyDomains;
        set { _customProxyDomains = value; OnPropertyChanged(); }
    }

    public AppLanguage AppLanguage
    {
        get => _appLanguage;
        set => SetField(ref _appLanguage, value);
    }

    public string EffectiveDns => DnsServer switch
    {
        DnsServer.Google => "8.8.8.8",
        DnsServer.Cloudflare => "1.1.1.1",
        DnsServer.Quad9 => "9.9.9.9",
        DnsServer.Custom => string.IsNullOrWhiteSpace(CustomDns) ? "8.8.8.8" : CustomDns,
        _ => "8.8.8.8"
    };

    public string StatusIcon => ConnectionStatus.State switch
    {
        ConnectionState.Connected => "icon-connected",
        ConnectionState.Connecting or ConnectionState.Disconnecting => "icon-disconnected",
        ConnectionState.Disconnected => "icon-disconnected",
        ConnectionState.Error => "icon-error",
        _ => "icon-disconnected"
    };

    public string StatusText => ConnectionStatus.State switch
    {
        ConnectionState.Connected => Strings.Get("Connected"),
        ConnectionState.Connecting => Strings.Get("Connecting"),
        ConnectionState.Disconnecting => Strings.Get("Disconnecting"),
        ConnectionState.Disconnected => Strings.Get("Disconnected"),
        ConnectionState.Error => $"{Strings.Get("Error")}: {ConnectionStatus.ErrorMessage}",
        _ => Strings.Get("Disconnected")
    };

    // Logs
    public ObservableCollection<LogEntry> Logs { get; } = new();
    private const int MaxLogs = 1000;

    public void AddLog(string message, LogLevel level = LogLevel.Info)
    {
        var entry = new LogEntry(message, level);

        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => AddLogInternal(entry));
        }
        else
        {
            AddLogInternal(entry);
        }
    }

    private void AddLogInternal(LogEntry entry)
    {
        Logs.Add(entry);
        while (Logs.Count > MaxLogs)
            Logs.RemoveAt(0);
    }

    public void ClearLogs() => Logs.Clear();

    // Settings persistence
    public void LoadSettings()
    {
        var settings = Settings.Load();
        _dnsServer = settings.DnsServer;
        _customDns = settings.CustomDns;
        _spoofDpiPort = settings.SpoofDpiPort;
        _enableDoH = settings.EnableDoH;
        _enableSystemProxy = settings.EnableSystemProxy;
        _launchAtStartup = settings.LaunchAtStartup;
        _autoConnect = settings.AutoConnect;
        _proxyMode = settings.ProxyMode;
        _customProxyDomains = settings.CustomProxyDomains;
        _appLanguage = settings.AppLanguage;
    }

    public void SaveSettings()
    {
        var settings = new Settings
        {
            DnsServer = DnsServer,
            CustomDns = CustomDns,
            SpoofDpiPort = SpoofDpiPort,
            EnableDoH = EnableDoH,
            EnableSystemProxy = EnableSystemProxy,
            LaunchAtStartup = LaunchAtStartup,
            AutoConnect = AutoConnect,
            ProxyMode = ProxyMode,
            CustomProxyDomains = CustomProxyDomains,
            AppLanguage = AppLanguage
        };
        settings.Save();
    }

    // INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Auto-notify dependent properties
        if (name == nameof(ConnectionStatus))
        {
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
