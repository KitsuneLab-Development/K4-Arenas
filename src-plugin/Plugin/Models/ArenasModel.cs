
using CounterStrikeSharp.API.Core;
using K4Arenas;
using K4Arenas.Models;
using Microsoft.Extensions.Logging;

public class Arenas
{
	//** ? Main */
	private readonly Plugin Plugin;

	//** ? Arenas */
	public List<Arena> ArenaList { get; set; } = new List<Arena>();

	public Arenas(Plugin plugin)
	{
		Plugin = plugin;

		Plugin.CheckCommonProblems();

		List<Tuple<List<SpawnPoint>, List<SpawnPoint>>> spawnPairs = new ArenaFinder(this.Plugin).GetSpawnPairs();

		foreach (Tuple<List<SpawnPoint>, List<SpawnPoint>> spawnPair in spawnPairs)
		{
			Arena arena = new Arena(Plugin, spawnPair);
			ArenaList.Add(arena);
		}

		Plugin.Logger.LogInformation("Successfully setup {0} arenas!", ArenaList.Count);
	}

	public int Count => ArenaList.Count;

	public ArenaPlayer? FindPlayer(CCSPlayerController? player)
	{
		IEnumerable<ArenaPlayer> allPlayers = Plugin.WaitingArenaPlayers
			.Concat(ArenaList.SelectMany(x => x.Team1 ?? Enumerable.Empty<ArenaPlayer>()))
			.Concat(ArenaList.SelectMany(x => x.Team2 ?? Enumerable.Empty<ArenaPlayer>()));

		return allPlayers.FirstOrDefault(p => p.Controller == player);
	}

	public List<CCSPlayerController> FindOpponents(CCSPlayerController? player)
	{
		var arenaPlayer = FindPlayer(player);

		if (arenaPlayer is null)
			return new List<CCSPlayerController>();

		var arenaID = Plugin.GetPlayerArenaID(arenaPlayer);

		if(arenaID < 0)
			return new List<CCSPlayerController>();

		var arena = ArenaList.FirstOrDefault(a => a.ArenaID == arenaID);
		if (arena == null)
			return new List<CCSPlayerController>();

		var opponents = arena.Team1?.Any(p => p.Controller == player) == true ? arena.Team2 : arena.Team1;
		return opponents?.Select(p => p.Controller).ToList() ?? new List<CCSPlayerController>();
	}

	public ArenaPlayer? FindPlayer(ulong steamId)
	{
		IEnumerable<ArenaPlayer> allPlayers = Plugin.WaitingArenaPlayers
			.Concat(ArenaList.SelectMany(x => x.Team1 ?? Enumerable.Empty<ArenaPlayer>()))
			.Concat(ArenaList.SelectMany(x => x.Team2 ?? Enumerable.Empty<ArenaPlayer>()));

		return allPlayers.FirstOrDefault(p => p.SteamID == steamId);
	}

	public bool IsPlayerInArena(CCSPlayerController player)
	{
		return ArenaList.Any(a => a.Team1?.Any(p => p.Controller == player) == true || a.Team2?.Any(p => p.Controller == player) == true);
	}

	public bool AddTeamsToArena(int arenaID, int displayID, int teamSize, Queue<ArenaPlayer> notAFKrankedPlayers, RoundType checkType)
	{
		if (notAFKrankedPlayers.Count < teamSize * 2)
			return false;

		Arena arena = ArenaList[arenaID];
		if (arena.Spawns.Item1.Count < teamSize || arena.Spawns.Item2.Count < teamSize)
			return false;

		List<ArenaPlayer> team1Preview = notAFKrankedPlayers.Take(teamSize).ToList();
		List<ArenaPlayer> team2Preview = notAFKrankedPlayers.Skip(teamSize).Take(teamSize).ToList();

		List<RoundType> preferencesTeam1 = team1Preview.SelectMany(player => player.RoundPreferences).Distinct().ToList();
		List<RoundType> preferencesTeam2 = team2Preview.SelectMany(player => player.RoundPreferences).Distinct().ToList();

		RoundType roundType = Plugin.GetCommonRoundType(preferencesTeam1, preferencesTeam2, true);

		if (roundType.ID != checkType.ID)
			return false;

		for (int i = 0; i < teamSize * 2; i++)
		{
			notAFKrankedPlayers.Dequeue();
		}

		arena.AddPlayers(team1Preview, team2Preview, roundType, displayID);
		return true;
	}

	public void Shuffle()
	{
		int n = ArenaList.Count;
		while (n > 1)
		{
			n--;
			int k = Plugin.rng.Next(n + 1);
			Arena value = ArenaList[k];
			ArenaList[k] = ArenaList[n];
			ArenaList[n] = value;
		}
	}

	public void Clear() => ArenaList.Clear();
}
