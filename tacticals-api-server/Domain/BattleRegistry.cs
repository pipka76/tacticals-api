using System.Collections.Concurrent;

namespace tacticals_api_server.Domain;

public class BattleRegistry
{
	private static ConcurrentDictionary<Guid, Battle> _battles = new();
	public const int MAX_PLAYERS = 4;
	
	public static Guid RegisterNewBattle(string name, string mapName)
	{
		if (_battles.Any(b => b.Value.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)))
			return Guid.Empty;

		// Host/Port stay empty until the game server is started (see MarkRunning).
		var b = new Battle()
		{
			ID = Guid.NewGuid(),
			Name = name,
			Owner = -1,
			IsRunning = false,
			MapName = mapName,
			Peers = new List<Peer>(),
			Port = 0
		};

		if (!_battles.TryAdd(b.ID, b))
			return Guid.Empty;

		return b.ID;
	}

	public static bool AuthorizeBattle(Guid id, int ownerId)
	{
		if (!_battles.TryGetValue(id, out Battle battle))
			return false;

		if (battle.Owner != -1)
			return false;

		battle.Owner = ownerId;
		battle.Peers.Add(new Peer() { Id = ownerId, IsReady = false });

		return true;
	}

	public static bool JoinBattle(Guid id, int peerId)
	{
		if (!_battles.TryGetValue(id, out Battle battle))
			return false;
		
		if(battle.IsRunning)
			return false;

		if (battle.Peers.Any(p => p.Id == peerId))
			return true;

		if (battle.Peers.Count >= MAX_PLAYERS)
			return false;

		battle.Peers.Add(new Peer() { Id = peerId, IsReady = false });

		return true;
	}

	public static void PlayerReady(Guid battleID, int peerId, string name)
	{
		if (_battles.TryGetValue(battleID, out Battle battle))
		{
			foreach (var p in battle.Peers)
			{
				if (p.Id == peerId)
				{
					p.IsReady = true;
					p.Name = name;
					return;
				}
			}
		}
	}

	public static void PlayerNotReady(Guid battleID, int peerId, string name)
	{
		if (_battles.TryGetValue(battleID, out Battle battle))
		{
			foreach (var p in battle.Peers)
			{
				if (p.Id == peerId)
				{
					p.IsReady = false;
					p.Name = name;
					return;
				}
			}
		}
	}

	public static IList<Battle> GetRegisteredBattles(bool? running = null)
	{
		if (running.HasValue)
			return _battles.Values.Where(b => b.IsRunning == running).ToList();

		return _battles.Values.ToList();
	}

	public static bool CanStartBattle(Guid battleId, int ownerId)
	{
		return _battles.TryGetValue(battleId, out Battle battle) && battle.Owner == ownerId;
	}

	/// <summary>
	/// Records the public endpoint of the battle's game server. Returns false if the battle
	/// disappeared while its server was starting.
	/// </summary>
	public static bool MarkRunning(Guid battleId, string host, int port)
	{
		if (!_battles.TryGetValue(battleId, out Battle battle))
			return false;

		battle.Host = host;
		battle.Port = port;
		battle.IsRunning = true;
		return true;
	}

	public static void RemoveBattle(Guid battleId)
	{
		_battles.TryRemove(battleId, out _);
	}

	public static bool FinishBattle(Guid battleId, int ownerId)
	{
		if (_battles.TryGetValue(battleId, out Battle battle))
		{
			if (battle.Owner != ownerId)
				return false;

			return _battles.TryRemove(battleId, out _);
		}

		return false;
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