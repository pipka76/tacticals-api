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
    [Route("battle/register")]
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
    [Route("battle/authorize")]
    public IActionResult Authorize(Guid id, int ownerPeer)
    {
        _logger.LogDebug($"AuthorizeBattle: {id} {ownerPeer}");

        if (!BattleRegistry.AuthorizeBattle(id, ownerPeer))
            return new ForbidResult();

        return Ok();
    }
    
    [HttpPut]
    [Route("battle/join")]
    public IActionResult Join(Guid id, int peerId)
    {
        _logger.LogDebug($"JoinBattle: {id} {peerId}");

        if(!BattleRegistry.JoinBattle(id, peerId))
            return new ForbidResult();

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
    
    [HttpPut]
    [Route("battle/start")]
    public IActionResult StartBattle(Guid id, int peerId)
    {
        _logger.LogDebug($"StartBattle: {id} {peerId}");

        if(!BattleRegistry.StartBattle(id, peerId))
            return new ForbidResult();

        return Ok();
    }

    [HttpPut]
    [Route("battle/finish")]
    public IActionResult FinishBattle([FromQuery] Guid id, [FromQuery] int peerId, [FromBody] string statistics)
    {
        _logger.LogDebug($"FinishBattle: {id} {peerId}");

        if(!BattleRegistry.FinishBattle(id, peerId))
            return new ForbidResult();

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