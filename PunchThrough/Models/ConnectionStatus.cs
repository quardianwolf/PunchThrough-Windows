namespace PunchThrough.Models;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting,
    Error
}

public class ConnectionStatus
{
    public ConnectionState State { get; }
    public string? ErrorMessage { get; }

    private ConnectionStatus(ConnectionState state, string? errorMessage = null)
    {
        State = state;
        ErrorMessage = errorMessage;
    }

    public static ConnectionStatus Disconnected => new(ConnectionState.Disconnected);
    public static ConnectionStatus Connecting => new(ConnectionState.Connecting);
    public static ConnectionStatus Connected => new(ConnectionState.Connected);
    public static ConnectionStatus Disconnecting => new(ConnectionState.Disconnecting);
    public static ConnectionStatus CreateError(string message) => new(ConnectionState.Error, message);

    public bool IsActive => State is ConnectionState.Connected or ConnectionState.Connecting;

    public bool CanToggle => State is ConnectionState.Connected
        or ConnectionState.Disconnected
        or ConnectionState.Error;
}
