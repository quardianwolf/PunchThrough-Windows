using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PunchThrough.Models;

public class AppState : INotifyPropertyChanged
{
    private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;
    private DnsServer _dnsServer = DnsServer.Cloudflare;
    private string _customDns = "";
    private bool _enableDoH = true;
    private bool _enableQuic;
    private bool _launchAtStartup;
    private bool _autoConnect;
    private ProxyMode _proxyMode = ProxyMode.Full;
    private List<string> _customProxyDomains = new();
    private string _zapretStrategy = "";
    private AppLanguage _appLanguage = AppLanguage.System;
    private string _tunnelStatusText = "";
    private bool _tunnelActive;

    public ConnectionStatus ConnectionStatus
    {
        get => _connectionStatus;
        set => SetField(ref _connectionStatus, value);
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

    public bool EnableDoH
    {
        get => _enableDoH;
        set => SetField(ref _enableDoH, value);
    }

    public bool EnableQuic
    {
        get => _enableQuic;
        set => SetField(ref _enableQuic, value);
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

    public string ZapretStrategy
    {
        get => _zapretStrategy;
        set => SetField(ref _zapretStrategy, value);
    }

    public AppLanguage AppLanguage
    {
        get => _appLanguage;
        set => SetField(ref _appLanguage, value);
    }

    // SSH tunnel configuration (changed together from the settings window)
    public TunnelMode TunnelMode { get; set; } = TunnelMode.Off;
    public string TunnelHost { get; set; } = "";
    public int TunnelPort { get; set; } = 22;
    public string TunnelUser { get; set; } = "";
    public string TunnelKeyPath { get; set; } = "";
    public List<string> TunnelDomains { get; set; } = new();

    /// <summary>Human readable tunnel state; empty when the tunnel is off.</summary>
    public string TunnelStatusText
    {
        get => _tunnelStatusText;
        set => SetField(ref _tunnelStatusText, value);
    }

    public bool TunnelActive
    {
        get => _tunnelActive;
        set => SetField(ref _tunnelActive, value);
    }

    public DnsProfile DnsProfile => DnsServer.GetProfile(CustomDns);

    /// <summary>The DNS address that is actually written to the adapters.</summary>
    public string EffectiveDns => DnsProfile.Primary;

    public string StatusIcon => ConnectionStatus.State switch
    {
        ConnectionState.Connected => "icon-connected",
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
        var dispatcher = System.Windows.Application.Current?.Dispatcher;

        if (dispatcher != null && !dispatcher.CheckAccess())
            dispatcher.BeginInvoke(() => AddLogInternal(entry));
        else
            AddLogInternal(entry);
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
        _enableDoH = settings.EnableDoH;
        _enableQuic = settings.EnableQuic;
        _launchAtStartup = settings.LaunchAtStartup;
        _autoConnect = settings.AutoConnect;
        _proxyMode = settings.ProxyMode;
        _customProxyDomains = settings.CustomProxyDomains;
        _zapretStrategy = settings.ZapretStrategy;
        _appLanguage = settings.AppLanguage;
        TunnelMode = settings.TunnelMode;
        TunnelHost = settings.TunnelHost;
        TunnelPort = settings.TunnelPort;
        TunnelUser = settings.TunnelUser;
        TunnelKeyPath = settings.TunnelKeyPath;
        TunnelDomains = settings.TunnelDomains;
    }

    public void SaveSettings()
    {
        var settings = new Settings
        {
            DnsServer = DnsServer,
            CustomDns = CustomDns,
            EnableDoH = EnableDoH,
            EnableQuic = EnableQuic,
            LaunchAtStartup = LaunchAtStartup,
            AutoConnect = AutoConnect,
            ProxyMode = ProxyMode,
            CustomProxyDomains = CustomProxyDomains,
            ZapretStrategy = ZapretStrategy,
            AppLanguage = AppLanguage,
            TunnelMode = TunnelMode,
            TunnelHost = TunnelHost,
            TunnelPort = TunnelPort,
            TunnelUser = TunnelUser,
            TunnelKeyPath = TunnelKeyPath,
            TunnelDomains = TunnelDomains
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
