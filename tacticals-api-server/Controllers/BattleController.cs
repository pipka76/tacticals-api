using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using tacticals_api_server.Domain;
using tacticals_api_server.Domain.GameServers;

namespace tacticals_api_server.Controllers;

[ApiController]
public class BattleController : Controller
{
    private ILogger<BattleController> _logger;
    private GameServerManager _gameServers;
    
    public BattleController(ILogger<BattleController> logger, GameServerManager gameServers)
    {
        _logger = logger;
        _gameServers = gameServers;
    }
    
    [HttpPut]
    [Route("battle/register")]
    public IActionResult Register(string name)
    {
        _logger.LogDebug($"RegisterNewBattle: {name}");

        var battleId = BattleRegistry.RegisterNewBattle(name, string.Empty);
        if (battleId == Guid.Empty)
            return new ConflictResult();

        return new ContentResult()
        {
            StatusCode = (int)HttpStatusCode.OK,
            ContentType = "application/json",
            Content = "{" + $"\"id\": \"{battleId}\"" + "}"
        };
    }

    [HttpPut]
    [Route("battle/authorize")]
    public IActionResult Authorize(Guid id, int ownerPeer)
    {
        _logger.LogDebug($"AuthorizeBattle: {id} {ownerPeer}");

        if (!BattleRegistry.AuthorizeBattle(id, ownerPeer))
            return StatusCode((int)HttpStatusCode.Forbidden);

        return Ok();
    }
    
    [HttpPut]
    [Route("battle/join")]
    public IActionResult Join(Guid id, int peerId)
    {
        _logger.LogDebug($"JoinBattle: {id} {peerId}");

        if(!BattleRegistry.JoinBattle(id, peerId))
            return StatusCode((int)HttpStatusCode.Forbidden);

        return Ok();
    }

    [HttpPut]
    [Route("battle/playerready")]
    public IActionResult PlayerReady(Guid id, int peerId, string name, bool isReady)
    {
        _logger.LogDebug($"PlayerReady: {id} {peerId} {name} {isReady}");

        if(isReady)
            BattleRegistry.PlayerReady(id, peerId, name);
        else
            BattleRegistry.PlayerNotReady(id, peerId, name);

        return Ok();
    }
    
    /// <summary>
    /// Launches the battle's dedicated game server and returns the endpoint players connect to.
    /// Calling it again for a running battle returns the same endpoint.
    /// </summary>
    [HttpPut]
    [Route("battle/start")]
    public async Task<IActionResult> StartBattle(Guid id, int peerId)
    {
        _logger.LogDebug($"StartBattle: {id} {peerId}");

        if(!BattleRegistry.CanStartBattle(id, peerId))
            return StatusCode((int)HttpStatusCode.Forbidden);

        var instance = await _gameServers.StartAsync(id);
        if (instance == null)
            return StatusCode((int)HttpStatusCode.ServiceUnavailable);

        if (!BattleRegistry.MarkRunning(id, instance.PublicHost, instance.Port))
        {
            // Battle was finished/removed while its server was starting.
            _gameServers.Stop(id);
            return NotFound();
        }

        return Ok(new BattleEndpoint(id, instance.PublicHost, instance.Port));
    }

    /// <summary>
    /// Where to connect for a battle: 404 if unknown, 409 while it hasn't been started yet.
    /// </summary>
    [HttpGet]
    [Route("battle/endpoint")]
    public IActionResult GetEndpoint(Guid id)
    {
        var b = BattleRegistry.GetBattle(id);
        if (b == null)
            return NotFound();

        if (!b.IsRunning)
            return Conflict();

        return Ok(new BattleEndpoint(b.ID, b.Host, b.Port));
    }

    /// <summary>Running game server processes and the ports mapped to them.</summary>
    [HttpGet]
    [Route("battle/instances")]
    public IActionResult ListInstances()
    {
        return Ok(_gameServers.ListInstances());
    }

    [HttpPut]
    [Route("battle/finish")]
    public IActionResult FinishBattle([FromQuery] Guid id, [FromQuery] int peerId, [FromBody] string statistics)
    {
        _logger.LogDebug($"FinishBattle: {id} {peerId}");

        if(!BattleRegistry.FinishBattle(id, peerId))
            return StatusCode((int)HttpStatusCode.Forbidden);

        _gameServers.Stop(id);
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
}

public record BattleEndpoint(Guid Id, string Host, int Port);
