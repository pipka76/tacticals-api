namespace tacticals_api_server.Domain;

public record Battle
{
    public Guid ID { set; get; }
    public string Name { set; get; }
    // Profile id of the player who registered the battle.
    public Guid Owner { set; get; }
    public List<Peer> Peers { set; get; }
    public bool IsRunning { set; get; }
    public string MapName { set; get; }
    // Public endpoint of the battle's game server; set once the battle is running.
    public string Host { set; get; }
    public int Port { set; get; }
}

public record Peer
{
    // Profile id; Name is the profile name.
    public Guid Id { set; get; }
    public string Name { set; get; }
    public bool IsReady { set; get; }
}

/// <summary>
/// Where a player connects for a running battle. Ticket is one-time and short-lived: the client
/// sends it in the ENet auth handshake and the game server redeems it with the API.
/// </summary>
public record BattleEndpoint(Guid Id, string Host, int Port, string Ticket);

/// <summary>Who a redeemed ticket belongs to.</summary>
public record TicketIdentity(Guid ProfileId, string Name);
