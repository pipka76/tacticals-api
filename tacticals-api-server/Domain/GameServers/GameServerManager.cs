using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace tacticals_api_server.Domain.GameServers;

/// <summary>
/// Launches and tracks one dedicated Godot server process per battle. Owns port allocation:
/// a port is taken when the process starts and released when it exits.
/// </summary>
public class GameServerManager : IHostedService
{
    private readonly ILogger<GameServerManager> _logger;
    private readonly GameServerOptions _options;
    private readonly string _workingDirectory;
    private readonly object _lock = new();
    private readonly Dictionary<Guid, GameInstance> _instances = new();

    public GameServerManager(ILogger<GameServerManager> logger, IOptions<GameServerOptions> options, IHostEnvironment env)
    {
        _logger = logger;
        _options = options.Value;
        _workingDirectory = Path.GetFullPath(Path.Combine(env.ContentRootPath, _options.WorkingDirectory));
    }

    /// <summary>
    /// Starts the game server for a battle (or returns the already running one) and waits until
    /// it accepts connections. Returns null when no port is free or the process failed to come up.
    /// </summary>
    public async Task<GameInstance?> StartAsync(Guid battleId)
    {
        GameInstance? instance;
        lock (_lock)
        {
            if (!_instances.TryGetValue(battleId, out instance))
            {
                var port = AllocatePort();
                if (port < 0)
                {
                    _logger.LogWarning("No free port in {Min}-{Max} for battle {BattleId}", _options.MinPort, _options.MaxPort, battleId);
                    return null;
                }

                instance = Launch(battleId, port);
                if (instance == null)
                    return null;

                _instances[battleId] = instance;
            }
        }

        var ready = instance.ReadySignal.Task;
        var completed = await Task.WhenAny(ready, Task.Delay(TimeSpan.FromSeconds(_options.StartupTimeoutSeconds)));
        if (completed == ready && ready.Result)
            return instance;

        _logger.LogWarning("Game server for battle {BattleId} did not become ready", battleId);
        Stop(battleId);
        return null;
    }

    /// <summary>Kills the battle's game server, if any, and releases its port.</summary>
    public void Stop(Guid battleId)
    {
        GameInstance? instance;
        lock (_lock)
        {
            if (!_instances.Remove(battleId, out instance))
                return;
        }

        Kill(instance);
    }

    public IList<GameInstance> ListInstances()
    {
        lock (_lock)
        {
            return _instances.Values.ToList();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Don't leave orphaned game servers behind when the API shuts down.
        List<GameInstance> all;
        lock (_lock)
        {
            all = _instances.Values.ToList();
            _instances.Clear();
        }

        foreach (var instance in all)
            Kill(instance);

        return Task.CompletedTask;
    }

    private GameInstance? Launch(Guid battleId, int port)
    {
        var instance = new GameInstance()
        {
            BattleId = battleId,
            Port = port,
            PublicHost = _options.PublicHost,
            StartedUtc = DateTime.UtcNow
        };

        var args = _options.Arguments
            .Replace("{port}", port.ToString())
            .Replace("{battleId}", battleId.ToString());

        var process = new Process()
        {
            StartInfo = new ProcessStartInfo(_options.ExecutablePath, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = _workingDirectory
            },
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null)
                return;

            _logger.LogInformation("[game:{Port}] {Line}", port, e.Data);
            if (e.Data.Contains(_options.ReadyMarker))
            {
                instance.IsReady = true;
                instance.ReadySignal.TrySetResult(true);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                _logger.LogWarning("[game:{Port}] {Line}", port, e.Data);
        };
        process.Exited += (_, _) => OnExited(instance);

        try
        {
            _logger.LogInformation("Starting game server for battle {BattleId} on port {Port}: {Exe} {Args}", battleId, port, _options.ExecutablePath, args);
            process.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start game server process {Exe}", _options.ExecutablePath);
            process.Dispose();
            return null;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        instance.Process = process;
        instance.ProcessId = process.Id;
        return instance;
    }

    private void OnExited(GameInstance instance)
    {
        bool wasTracked;
        lock (_lock)
        {
            wasTracked = _instances.TryGetValue(instance.BattleId, out var current) && current == instance;
            if (wasTracked)
                _instances.Remove(instance.BattleId);
        }

        instance.ReadySignal.TrySetResult(false);
        _logger.LogInformation("Game server for battle {BattleId} on port {Port} exited with code {Code}",
            instance.BattleId, instance.Port, instance.Process?.ExitCode);

        // A server that was running and exited on its own means the battle is over (or crashed).
        // A server that never got ready leaves the battle in the lobby so the owner can retry.
        if (wasTracked && instance.IsReady)
            BattleRegistry.RemoveBattle(instance.BattleId);

        instance.Process?.Dispose();
    }

    private void Kill(GameInstance instance)
    {
        try
        {
            if (instance.Process is { HasExited: false })
            {
                _logger.LogInformation("Stopping game server for battle {BattleId} on port {Port}", instance.BattleId, instance.Port);
                instance.Process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited and disposed.
        }
    }

    // Caller holds _lock.
    private int AllocatePort()
    {
        var used = _instances.Values.Select(i => i.Port).ToHashSet();
        for (int port = _options.MinPort; port <= _options.MaxPort; port++)
        {
            if (!used.Contains(port) && IsUdpPortFree(port))
                return port;
        }

        return -1;
    }

    private static bool IsUdpPortFree(int port)
    {
        // ENet is UDP; also catches ports held by processes we don't track (e.g. a leftover server).
        try
        {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
