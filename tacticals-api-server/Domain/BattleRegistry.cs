using System.Collections.Concurrent;

namespace tacticals_api_server.Domain;

public enum BattleResult
{
	Ok,
	NotFound,
	Forbidden,
	Conflict
}

/// <summary>
/// In-memory lobby state. Players and the owner are identified by profile id (from the login
/// token). Mutations of a battle's peer list happen under lock(battle).
/// </summary>
public class BattleRegistry
{
	private static ConcurrentDictionary<Guid, Battle> _battles = new();
	private static readonly object _registerLock = new();
	public const int MAX_PLAYERS = 4;

	/// <summary>Creates a battle owned by (and already joined by) the caller; null if the name is taken.</summary>
	public static Battle RegisterNewBattle(string name, string mapName, Guid ownerId, string ownerName)
	{
		lock (_registerLock)
		{
			if (_battles.Values.Any(b => b.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)))
				return null;

			// Host/Port stay empty until the game server is started (see MarkRunning).
			var b = new Battle()
			{
				ID = Guid.NewGuid(),
				Name = name,
				Owner = ownerId,
				IsRunning = false,
				MapName = mapName,
				Peers = new List<Peer>() { new Peer() { Id = ownerId, Name = ownerName, IsReady = false } },
				Port = 0
			};

			_battles[b.ID] = b;
			return b;
		}
	}

	public static BattleResult JoinBattle(Guid id, Guid profileId, string name)
	{
		if (!_battles.TryGetValue(id, out Battle battle))
			return BattleResult.NotFound;

		lock (battle)
		{
			if (battle.Peers.Any(p => p.Id == profileId))
				return BattleResult.Ok;

			if (battle.IsRunning || battle.Peers.Count >= MAX_PLAYERS)
				return BattleResult.Conflict;

			battle.Peers.Add(new Peer() { Id = profileId, Name = name, IsReady = false });
			return BattleResult.Ok;
		}
	}

	/// <summary>Leaves a battle that hasn't started; the owner leaving disbands it.</summary>
	public static BattleResult LeaveBattle(Guid id, Guid profileId)
	{
		if (!_battles.TryGetValue(id, out Battle battle))
			return BattleResult.NotFound;

		lock (battle)
		{
			if (battle.IsRunning)
				return BattleResult.Conflict;

			if (battle.Owner == profileId)
			{
				_battles.TryRemove(id, out _);
				return BattleResult.Ok;
			}

			battle.Peers.RemoveAll(p => p.Id == profileId);
			return BattleResult.Ok;
		}
	}

	public static BattleResult SetReady(Guid id, Guid profileId, bool isReady)
	{
		if (!_battles.TryGetValue(id, out Battle battle))
			return BattleResult.NotFound;

		lock (battle)
		{
			var peer = battle.Peers.FirstOrDefault(p => p.Id == profileId);
			if (peer == null)
				return BattleResult.Forbidden;

			peer.IsReady = isReady;
			return BattleResult.Ok;
		}
	}

	public static bool IsMember(Guid id, Guid profileId)
	{
		if (!_battles.TryGetValue(id, out Battle battle))
			return false;

		lock (battle)
		{
			return battle.Peers.Any(p => p.Id == profileId);
		}
	}

	public static IList<Battle> GetRegisteredBattles(bool? running = null)
	{
		if (running.HasValue)
			return _battles.Values.Where(b => b.IsRunning == running).ToList();

		return _battles.Values.ToList();
	}

	/// <summary>Owner only, and every joined player (owner included) must be ready. Ok again once running.</summary>
	public static BattleResult CanStartBattle(Guid battleId, Guid profileId)
	{
		if (!_battles.TryGetValue(battleId, out Battle battle))
			return BattleResult.NotFound;

		lock (battle)
		{
			if (battle.Owner != profileId)
				return BattleResult.Forbidden;

			if (!battle.IsRunning && !battle.Peers.All(p => p.IsReady))
				return BattleResult.Conflict;

			return BattleResult.Ok;
		}
	}

	/// <summary>
	/// Records the public endpoint of the battle's game server. Returns false if the battle
	/// disappeared while its server was starting.
	/// </summary>
	public static bool MarkRunning(Guid battleId, string host, int port)
	{
		if (!_battles.TryGetValue(battleId, out Battle battle))
			return false;

		lock (battle)
		{
			battle.Host = host;
			battle.Port = port;
			battle.IsRunning = true;
			return true;
		}
	}

	public static void RemoveBattle(Guid battleId)
	{
		_battles.TryRemove(battleId, out _);
	}

	public static BattleResult FinishBattle(Guid battleId, Guid profileId)
	{
		if (!_battles.TryGetValue(battleId, out Battle battle))
			return BattleResult.NotFound;

		if (battle.Owner != profileId)
			return BattleResult.Forbidden;

		_battles.TryRemove(battleId, out _);
		return BattleResult.Ok;
	}

	public static Battle GetBattle(Guid id)
	{
		if (_battles.TryGetValue(id, out Battle battle))
		{
			return battle;
		}

		return null;
	}
}
