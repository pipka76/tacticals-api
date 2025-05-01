using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using tacticals_api_server.Domain;

namespace tacticals_api_server.Controllers;

[ApiController]
public class BattleController : Controller
{
    private ILogger<BattleController> _logger;
    
    public BattleController(ILogger<BattleController> logger)
    {
        _logger = logger;
    }
    
    [HttpPut]
    [Route("register")]
    public IActionResult Register(string name)
    {
        _logger.LogDebug($"RegisterNewBattle: {name}");

        var battleId = BattleRegistry.RegisterNewBattle(name, string.Empty, out int port);
        if (battleId == Guid.Empty)
            return new ConflictResult();

        return new ContentResult()
        {
            StatusCode = (int)HttpStatusCode.OK,
            ContentType = "application/json",
            Content = "{" + $"\"id\": \"{battleId}\", \"port\": {port}" + "}"
        };
    }

    [HttpPut]
    [Route("authorize")]
    public IActionResult Authorize(Guid id, int ownerPeer)
    {
        _logger.LogDebug($"AuthorizeBattle: {id} {ownerPeer}");

        if (!BattleRegistry.AuthorizeBattle(id, ownerPeer))
            return new ForbidResult();

        return Ok();
    }
    
    [HttpPut]
    [Route("join")]
    public IActionResult Join(Guid id, int peerId)
    {
        _logger.LogDebug($"JoinBattle: {id} {peerId}");

        if(!BattleRegistry.JoinBattle(id, peerId))
            return new ForbidResult();

        return Ok();
    }

    [HttpPut]
    [Route("playerready")]
    public IActionResult PlayerReady(Guid id, int peerId, string name, bool isReady)
    {
        _logger.LogDebug($"PlayerReady: {id} {peerId} {name} {isReady}");

        if(isReady)
            BattleRegistry.PlayerReady(id, peerId, name);
        else
            BattleRegistry.PlayerNotReady(id, peerId, name);

        return Ok();
    }
    
    [HttpPut]
    [Route("start")]
    public IActionResult StartBattle(Guid id, int peerId)
    {
        _logger.LogDebug($"StartBattle: {id} {peerId}");

        if(!BattleRegistry.StartBattle(id, peerId))
            return new ForbidResult();

        return Ok();
    }

    [HttpPut]
    [Route("finish")]
    public IActionResult FinishBattle(Guid id, int peerId)
    {
        _logger.LogDebug($"FinishBattle: {id} {peerId}");

        if(!BattleRegistry.FinishBattle(id, peerId))
            return new ForbidResult();

        return Ok();
    }
    
    [HttpGet]
    [Route("get")]
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
    [Route("list")]
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