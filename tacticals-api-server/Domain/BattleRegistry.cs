using System.Collections.Concurrent;

namespace tacticals_api_server.Domain;

public class BattleRegistry
{
	private static ConcurrentDictionary<Guid, Battle> _battles = new();
	private const int MIN_PORT = 20000;
	
	public static Guid RegisterNewBattle(string name, string mapName, out int port)
	{
		port = -1;
		if (_battles.Any(b => b.Value.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)))
			return Guid.Empty;

		var freePort = MIN_PORT;
		if (_battles.Count > 0)
			freePort = _battles.Max(b => b.Value.Port) + 1;

		var b = new Battle()
		{
			ID = Guid.NewGuid(),
			Name = name,
			Owner = -1,
			IsRunning = false,
			MapName = mapName,
			Peers = new List<Peer>(),
			Port = freePort
		};

		if (!_battles.TryAdd(b.ID, b))
			return Guid.Empty;

		port = freePort;
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

		if(!battle.Peers.Any(p => p.Id == peerId))
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

	public static bool StartBattle(Guid battleId, int ownerId)
	{
		if (_battles.TryGetValue(battleId, out Battle battle))
		{
			if (battle.Owner != ownerId)
				return false;

			battle.IsRunning = true;

			return true;
		}

		return false;
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