namespace tacticals_api_server.Domain;

public record Battle
{
    public Guid ID { set; get; }
    public string Name { set; get; }
    public int Owner { set; get; }
    public List<Peer> Peers { set; get; }
    public bool IsRunning { set; get; }
    public string MapName { set; get; }
    // Public endpoint of the battle's game server; set once the battle is running.
    public string Host { set; get; }
    public int Port { set; get; }
}

public record Peer
{
    public int Id { set; get; }
    public string Name { set; get; }
    public bool IsReady { set; get; }
}