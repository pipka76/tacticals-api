using System.Diagnostics;

namespace tacticals_api_server.Domain.GameServers;

/// <summary>
/// One running dedicated game server process and the port it owns.
/// </summary>
public class GameInstance
{
    public Guid BattleId { get; init; }
    public int Port { get; init; }
    public string PublicHost { get; init; } = string.Empty;
    public int ProcessId { get; set; }
    public DateTime StartedUtc { get; init; }
    public bool IsReady { get; set; }

    internal Process? Process;

    /// <summary>Secret handed to the game process; it presents it when reporting back to the API.</summary>
    internal string Token = string.Empty;

    /// <summary>Outstanding join tickets: ticket -> who it was issued to. Guarded by the manager's lock.</summary>
    internal readonly Dictionary<string, (TicketIdentity Identity, DateTime ExpiresUtc)> Tickets = new();

    /// <summary>Completes true when the ready marker was printed, false if the process died first.</summary>
    internal readonly TaskCompletionSource<bool> ReadySignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
