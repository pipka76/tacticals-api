namespace tacticals_api_server.Domain.GameServers;

/// <summary>
/// "GameServers" section of appsettings. Describes how to launch one dedicated Godot
/// process per battle and which ports it may use.
/// </summary>
public class GameServerOptions
{
    public const string Section = "GameServers";

    /// <summary>Godot editor binary (dev) or exported headless server binary (prod).</summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>Command line; {port} and {battleId} are substituted per instance.</summary>
    public string Arguments { get; set; } = "--headless -- --port={port} --battle={battleId}";

    /// <summary>Working directory of the game process; relative paths resolve against the API content root.</summary>
    public string WorkingDirectory { get; set; } = ".";

    /// <summary>Host name / IP clients use to reach game instances.</summary>
    public string PublicHost { get; set; } = "127.0.0.1";

    /// <summary>Inclusive UDP port range handed out to game instances.</summary>
    public int MinPort { get; set; } = 20000;
    public int MaxPort { get; set; } = 20099;

    /// <summary>How long to wait for <see cref="ReadyMarker"/> before the start is considered failed.</summary>
    public int StartupTimeoutSeconds { get; set; } = 30;

    /// <summary>Line the game prints to stdout once its ENet server listens.</summary>
    public string ReadyMarker { get; set; } = "Server Started!";

    /// <summary>Base URL the game process uses to call back into this API (results, finish).</summary>
    public string ApiCallbackUrl { get; set; } = "http://127.0.0.1:5000/";

    /// <summary>Hard cap on an instance's lifetime; a hung or forgotten server is killed after this.</summary>
    public int MaxLifetimeMinutes { get; set; } = 120;

    /// <summary>How long a join ticket stays valid after the API hands it to a player.</summary>
    public int TicketLifetimeSeconds { get; set; } = 60;

    /// <summary>How often the lifetime watchdog runs.</summary>
    public int WatchdogIntervalSeconds { get; set; } = 30;
}
