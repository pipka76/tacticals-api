using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tacticals_api_server.Domain;
using tacticals_api_server.Domain.Auth;
using tacticals_api_server.Domain.Database;
using tacticals_api_server.Domain.GameServers;

namespace tacticals_api_server.Controllers;

/// <summary>
/// Player endpoints authenticate with the login token ("Authorization: Bearer"); the caller's
/// profile is the identity for ownership and membership. battle/server/* endpoints are for the
/// dedicated game server and authenticate with its per-instance token instead.
/// </summary>
[ApiController]
public class BattleController : Controller
{
    private ILogger<BattleController> _logger;
    private GameServerManager _gameServers;
    private ApiDatabaseContext _db;

    public BattleController(ILogger<BattleController> logger, GameServerManager gameServers, ApiDatabaseContext db)
    {
        _logger = logger;
        _gameServers = gameServers;
        _db = db;
    }

    /// <summary>Creates a battle owned by the caller, who is joined to it as the first player.</summary>
    [HttpPut]
    [Route("battle/register")]
    public async Task<IActionResult> Register(string name)
    {
        var caller = await GetCallerAsync();
        if (caller == null)
            return Unauthorized();

        _logger.LogDebug($"RegisterNewBattle: {name} by {caller.Value.Id}");

        var battle = BattleRegistry.RegisterNewBattle(name, string.Empty, caller.Value.Id, caller.Value.Name);
        if (battle == null)
            return new ConflictResult();

        return new ContentResult()
        {
            StatusCode = (int)HttpStatusCode.OK,
            ContentType = "application/json",
            Content = "{" + $"\"id\": \"{battle.ID}\"" + "}"
        };
    }

    [HttpPut]
    [Route("battle/join")]
    public async Task<IActionResult> Join(Guid id)
    {
        var caller = await GetCallerAsync();
        if (caller == null)
            return Unauthorized();

        _logger.LogDebug($"JoinBattle: {id} {caller.Value.Id}");

        return ToResponse(BattleRegistry.JoinBattle(id, caller.Value.Id, caller.Value.Name));
    }

    /// <summary>Leaves a battle that hasn't started; the owner leaving disbands it.</summary>
    [HttpPut]
    [Route("battle/leave")]
    public IActionResult Leave(Guid id)
    {
        var profileId = AuthTokens.GetProfileId(Request);
        if (profileId == null)
            return Unauthorized();

        _logger.LogDebug($"LeaveBattle: {id} {profileId}");

        return ToResponse(BattleRegistry.LeaveBattle(id, profileId.Value));
    }

    [HttpPut]
    [Route("battle/playerready")]
    public IActionResult PlayerReady(Guid id, bool isReady)
    {
        var profileId = AuthTokens.GetProfileId(Request);
        if (profileId == null)
            return Unauthorized();

        _logger.LogDebug($"PlayerReady: {id} {profileId} {isReady}");

        return ToResponse(BattleRegistry.SetReady(id, profileId.Value, isReady));
    }

    /// <summary>
    /// Owner only, once every player is ready. Launches the battle's dedicated game server and
    /// returns the endpoint plus the owner's join ticket. Calling it again for a running battle
    /// returns the same endpoint with a fresh ticket.
    /// </summary>
    [HttpPut]
    [Route("battle/start")]
    public async Task<IActionResult> StartBattle(Guid id)
    {
        var caller = await GetCallerAsync();
        if (caller == null)
            return Unauthorized();

        _logger.LogDebug($"StartBattle: {id} {caller.Value.Id}");

        var canStart = BattleRegistry.CanStartBattle(id, caller.Value.Id);
        if (canStart != BattleResult.Ok)
            return ToResponse(canStart);

        var instance = await _gameServers.StartAsync(id);
        if (instance == null)
            return StatusCode((int)HttpStatusCode.ServiceUnavailable);

        if (!BattleRegistry.MarkRunning(id, instance.PublicHost, instance.Port))
        {
            // Battle was finished/removed while its server was starting.
            _gameServers.Stop(id);
            return NotFound();
        }

        var ticket = _gameServers.IssueTicket(id, caller.Value.Id, caller.Value.Name);
        if (ticket == null)
            return StatusCode((int)HttpStatusCode.ServiceUnavailable);

        return Ok(new BattleEndpoint(id, instance.PublicHost, instance.Port, ticket));
    }

    /// <summary>
    /// Where a player of this battle connects, with a fresh one-time join ticket.
    /// 404 if unknown, 403 if the caller isn't in the battle, 409 while it hasn't been started yet.
    /// </summary>
    [HttpGet]
    [Route("battle/endpoint")]
    public async Task<IActionResult> GetEndpoint(Guid id)
    {
        var caller = await GetCallerAsync();
        if (caller == null)
            return Unauthorized();

        var b = BattleRegistry.GetBattle(id);
        if (b == null)
            return NotFound();

        if (!BattleRegistry.IsMember(id, caller.Value.Id))
            return StatusCode((int)HttpStatusCode.Forbidden);

        if (!b.IsRunning)
            return Conflict();

        var ticket = _gameServers.IssueTicket(id, caller.Value.Id, caller.Value.Name);
        if (ticket == null)
            return Conflict();

        return Ok(new BattleEndpoint(b.ID, b.Host, b.Port, ticket));
    }

    /// <summary>Running game server processes and the ports mapped to them.</summary>
    [HttpGet]
    [Route("battle/instances")]
    public IActionResult ListInstances()
    {
        return Ok(_gameServers.ListInstances());
    }

    /// <summary>Owner aborts the battle: removes it and stops its game server.</summary>
    [HttpPut]
    [Route("battle/finish")]
    public IActionResult FinishBattle([FromQuery] Guid id)
    {
        var profileId = AuthTokens.GetProfileId(Request);
        if (profileId == null)
            return Unauthorized();

        _logger.LogDebug($"FinishBattle: {id} {profileId}");

        var result = BattleRegistry.FinishBattle(id, profileId.Value);
        if (result == BattleResult.Ok)
            _gameServers.Stop(id);

        return ToResponse(result);
    }

    /// <summary>
    /// Called by the dedicated game server when a player connects: redeems the join ticket the
    /// player sent in the ENet auth handshake. 404 if unknown, already used, or expired.
    /// </summary>
    [HttpPost]
    [Route("battle/server/ticket")]
    public IActionResult ServerRedeemTicket([FromQuery] Guid id, [FromBody] string ticket)
    {
        if (!IsGameServer(id))
            return Unauthorized();

        var identity = _gameServers.RedeemTicket(id, ticket);
        _logger.LogInformation($"ServerRedeemTicket: {id} {(identity == null ? "rejected" : identity.ProfileId)}");

        if (identity == null)
            return NotFound();

        return Ok(identity);
    }

    /// <summary>
    /// Called by the dedicated game server (not by players) when the battle is decided, right
    /// before it quits. Authenticated with the per-instance token from its environment.
    /// </summary>
    [HttpPut]
    [Route("battle/server/finish")]
    public IActionResult ServerFinishBattle([FromQuery] Guid id, [FromBody] string statistics)
    {
        if (!IsGameServer(id))
            return Unauthorized();

        _logger.LogInformation($"ServerFinishBattle: {id} {statistics}");

        // The process exits on its own right after this; its Exited handler releases the port.
        BattleRegistry.RemoveBattle(id);
        return Ok();
    }

    [HttpGet]
    [Route("battle/get")]
    public IActionResult GetBattle(Guid id)
    {
        _logger.LogDebug($"GetBattle");

        var b = BattleRegistry.GetBattle(id);

        return new ContentResult()
        {
            StatusCode = (int)HttpStatusCode.OK,
            ContentType = "application/json",
            Content = System.Text.Json.JsonSerializer.Serialize(b, new JsonSerializerOptions() { IncludeFields = true })
        };
    }

    [HttpGet]
    [Route("battle/list")]
    public IActionResult ListBattles()
    {
        _logger.LogDebug($"ListBattles");

        var list = BattleRegistry.GetRegisteredBattles(false);

        return new ContentResult()
        {
            StatusCode = (int)HttpStatusCode.OK,
            ContentType = "application/json",
            Content = System.Text.Json.JsonSerializer.Serialize(list, new JsonSerializerOptions() { IncludeFields = true })
        };
    }

    /// <summary>The logged-in caller's profile id and name, or null if the token is missing/invalid.</summary>
    private async Task<(Guid Id, string Name)?> GetCallerAsync()
    {
        var profileId = AuthTokens.GetProfileId(Request);
        if (profileId == null)
            return null;

        var name = await _db.Profiles.Where(p => p.Id == profileId.Value).Select(p => p.Name).FirstOrDefaultAsync();
        if (name == null)
            return null;

        return (profileId.Value, name);
    }

    private bool IsGameServer(Guid battleId)
    {
        var token = AuthTokens.GetBearer(Request);
        return token != null && _gameServers.IsServerToken(battleId, token);
    }

    private IActionResult ToResponse(BattleResult result) => result switch
    {
        BattleResult.Ok => Ok(),
        BattleResult.NotFound => NotFound(),
        BattleResult.Forbidden => StatusCode((int)HttpStatusCode.Forbidden),
        _ => Conflict()
    };
}
