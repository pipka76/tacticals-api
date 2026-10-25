namespace tacticals_api_server.Domain;

/// <summary>
/// Contract between the API and a dedicated game server process: how the process is told
/// where to report back, and what its exit code means.
/// </summary>
public static class GameServerExit
{
    /// <summary>Battle was played to the end; results were (or should have been) reported first.</summary>
    public const int Finished = 0;

    /// <summary>All players disconnected mid-battle.</summary>
    public const int Abandoned = 2;

    /// <summary>No player connected within the join timeout.</summary>
    public const int NoPlayers = 3;

    // Environment variables the API sets on the game server process.
    public const string ApiUrlEnv = "TACTICALS_API_URL";
    public const string ServerTokenEnv = "TACTICALS_SERVER_TOKEN";

    public static string Describe(int exitCode) => exitCode switch
    {
        Finished => "finished",
        Abandoned => "abandoned",
        NoPlayers => "no players",
        _ => "crashed"
    };
}
