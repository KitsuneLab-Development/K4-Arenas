using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace K4Arenas.Models;

public class Arena
{
	//** ? Main */
	private readonly Plugin Plugin;
	private readonly IStringLocalizer Localizer;

	//** ? Arena Main Details */
	public int ArenaID;
	private int ArenaScore;
	private RoundType RoundType;
	public readonly Tuple<List<SpawnPoint>, List<SpawnPoint>> Spawns;
	public ArenaResult Result = new ArenaResult(ArenaResultType.Empty, null, null);

	//** ? Arena Players */
	public List<ArenaPlayer>? Team1;
	public List<ArenaPlayer>? Team2;

	public Arena(Plugin plugin, Tuple<List<SpawnPoint>, List<SpawnPoint>> spawns)
	{
		Spawns = spawns;
		Plugin = plugin;
		Localizer = Plugin.Localizer;
	}

	public bool IsActive
	{
		get
		{
			return Team1?.Count(p => p.IsValid && p.Controller.PlayerPawn?.Value?.Health > 0) > 0 && Team2?.Count(p => p.IsValid && p.Controller.PlayerPawn?.Value?.Health > 0) > 0;
		}
	}

	public bool HasRealPlayers
	{
		get
		{
			return Team1?.Count(p => p.IsValid && !p.Controller.IsBot) > 0 || Team2?.Count(p => p.IsValid && !p.Controller.IsBot) > 0;
		}
	}

	public void AddChallengePlayers(List<ArenaPlayer> team1, List<ArenaPlayer> team2)
	{
		ArenaID = -2;
		Team1 = team1;
		Team2 = team2;
		RoundType = Plugin.GetCommonRoundType(team1.First().RoundPreferences, team2.First().RoundPreferences, false);
		ArenaScore = 0;

		var (t1Spawns, t2Spawns) = Plugin.rng.Next(0, 2) == 1 ? (Spawns.Item1, Spawns.Item2) : (Spawns.Item2, Spawns.Item1);

		SetPlayerDetails(Team1, t1Spawns, CsTeam.Terrorist, Team2);
		SetPlayerDetails(Team2, t2Spawns, CsTeam.CounterTerrorist, Team1);
	}

	public void AddPlayers(List<ArenaPlayer>? team1, List<ArenaPlayer>? team2, RoundType? roundType, int arenaID = -1, int arenaScore = 0)
	{
		ArenaID = arenaID;
		Team1 = team1;
		Team2 = team2;
		RoundType = roundType ?? RoundType.RoundTypes[0];
		Result = new ArenaResult(ArenaResultType.Empty, null, null);
		ArenaScore = arenaScore;

		if (Team1 is null && Team2 is null)
			return;

		var (t1Spawns, t2Spawns) = Plugin.rng.Next(0, 2) == 1 ? (Spawns.Item1, Spawns.Item2) : (Spawns.Item2, Spawns.Item1);

		SetPlayerDetails(Team1, t1Spawns, CsTeam.Terrorist, Team2);
		SetPlayerDetails(Team2, t2Spawns, CsTeam.CounterTerrorist, Team1);
	}

	public void WarmupPopulate()
	{
		ClearInvalidTeamPlayers();

		Queue<ArenaPlayer> availablePlayers = new Queue<ArenaPlayer>(Plugin.WaitingArenaPlayers.Where(p => p.IsValid && !p.AFK && p.PlayerIsSafe));

		if (availablePlayers.Count == 0)
			return;

		ArenaID = -1;
		RoundType = new RoundType("none", 1, null, null, false, WeaponType.Unknown, false);
		ArenaScore = 0;

		var (t1Spawns, t2Spawns) = Plugin.rng.Next(0, 2) == 1 ? (Spawns.Item1, Spawns.Item2) : (Spawns.Item2, Spawns.Item1);

		bool arenaUpdated = false;

		arenaUpdated |= TryPopulateTeam(ref Team1, availablePlayers);
		arenaUpdated |= TryPopulateTeam(ref Team2, availablePlayers);

		if (arenaUpdated)
		{
			SetPlayerDetails(Team1, t1Spawns, CsTeam.Terrorist, Team2);
			SetPlayerDetails(Team2, t2Spawns, CsTeam.CounterTerrorist, Team1);

			Result = new ArenaResult(ArenaResultType.Tie, Team1, Team2);
		}

		bool TryPopulateTeam(ref List<ArenaPlayer>? team, Queue<ArenaPlayer> availablePlayers)
		{
			if (team == null && availablePlayers.TryDequeue(out ArenaPlayer? player))
			{
				team = new List<ArenaPlayer> { player };
				Plugin.WaitingArenaPlayers = new Queue<ArenaPlayer>(Plugin.WaitingArenaPlayers.Where(p => p != player));
				return true;
			}

			return false;
		}
	}

	public void ClearInvalidTeamPlayers()
	{
		Team1?.RemoveAll(p => !p.IsValid);
		Team1 = Team1?.Count > 0 ? Team1 : null;

		Team2?.RemoveAll(p => !p.IsValid);
		Team2 = Team2?.Count > 0 ? Team2 : null;
	}

	public void RemovePlayer(CCSPlayerController player)
	{
		Team1?.RemoveAll(p => p.Controller == player);
		Team1 = Team1?.Count > 0 ? Team1 : null;

		Team2?.RemoveAll(p => p.Controller == player);
		Team2 = Team2?.Count > 0 ? Team2 : null;
	}

	public void SetPlayerDetails(List<ArenaPlayer>? team, List<SpawnPoint> spawns, CsTeam switchTo, List<ArenaPlayer>? opponents)
	{
		if (team is null)
			return;

		List<SpawnPoint> spawnsCopy = new List<SpawnPoint>(spawns);

		foreach (ArenaPlayer player in team)
		{
			if (player?.IsValid == true)
			{
				int randomSpawnIndex = Plugin.rng.Next(0, spawnsCopy.Count);
				player.SpawnPoint = spawnsCopy[randomSpawnIndex];
				spawnsCopy.RemoveAt(randomSpawnIndex);

				player.Controller.Score = ArenaScore;

				player.Controller.Clan = Plugin.GetRequiredTag(ArenaID);
				Utilities.SetStateChanged(player.Controller, "CCSPlayerController", "m_szClan");

				string arenaName = ArenaID == -1 ? Localizer["k4.general.warmup"] : Localizer["k4.general.arena", ArenaID];

				player.Controller.SwitchTeam(switchTo);

				if (ArenaID != -1)
				{
					player.Controller.PrintToChat($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.arena_roundstart", Plugin.GetRequiredArenaName(ArenaID), Plugin.GetOpponentNames(opponents) ?? "Unknown", Localizer[RoundType.Name ?? "Missing"]]}");
				}

				if (Plugin.gameRules?.WarmupPeriod == true)
				{
					SetupArenaPlayer(player.Controller);
				}
			}
		}
	}

	public void SetupArenaPlayer(CCSPlayerController playerController)
	{
		ArenaPlayer? player = Team1?.FirstOrDefault(p => p.IsValid && p.Controller == playerController) ?? Team2?.FirstOrDefault(p => p.IsValid && p.Controller == playerController);

		if (player?.IsValid == true && player.Controller.PlayerPawn.Value != null)
		{
			SpawnPoint? playerSpawn = player.SpawnPoint;

			if (playerSpawn == null)
			{
				Plugin.Logger.LogError("Cannot spawn {0} because the spawn point is null", player.Controller.PlayerName);
				return;
			}

			Vector? pos = playerSpawn.AbsOrigin;
			QAngle? angle = playerSpawn.AbsRotation;

			if (pos == null || angle == null)
				return;

			Vector velocity = new Vector(0, 0, 0);

			player.Controller.PlayerPawn.Value.Teleport(pos, angle, velocity);
			player.Controller.PlayerPawn.Value.Health = 100;

			if (RoundType.StartFunction != null)
			{
				List<CCSPlayerController>? team1 = Team1?.Select(p => p.Controller).ToList();
				List<CCSPlayerController>? team2 = Team2?.Select(p => p.Controller).ToList();

				if (team1 is null && team2 is null)
					return;

				Server.NextFrame(() => RoundType.StartFunction(team1, team2));
			}
			else
			{
				player.SetupWeapons(RoundType);
			}
		}
	}

	public void OnRoundEnd()
	{
		if (ArenaID == -1)
		{
			Team1?.ForEach(Plugin.WaitingArenaPlayers.Enqueue);
			Team2?.ForEach(Plugin.WaitingArenaPlayers.Enqueue);
			return;
		}

		if (Team1 == null && Team2 == null)
		{
			Result = new ArenaResult(ArenaResultType.Empty, null, null);
		}
		else
		{
			if (Team1 == null || Team2 == null)
			{
				List<ArenaPlayer> winners = (Team1 == null ? Team2 : Team1)!;
				Result = new ArenaResult(ArenaResultType.NoOpponent, winners, null);
			}
			else
			{
				int team1Alive = Team1.Count(p => p.IsValid && p.Controller.PlayerPawn?.Value?.Health > 0);
				int team2Alive = Team2.Count(p => p.IsValid && p.Controller.PlayerPawn?.Value?.Health > 0);

				if (team1Alive == team2Alive)
				{
					if (ArenaID == -2)
					{
						Server.PrintToChatAll($"{Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.tie", Team1.First().Controller.PlayerName, Team2.First().Controller.PlayerName]}");
						Result = new ArenaResult(ArenaResultType.Tie, null, null);

						Team1.Concat(Team2)
							.Where(p => p.Challenge is not null)
							.ToList()
							.ForEach(p => p.Challenge!.IsEnded = true);
					}
					else
						Result = new ArenaResult(ArenaResultType.Tie, Team1, Team2);
				}
				else
				{
					if (ArenaID == -2)
					{
						ArenaPlayer winner = (team1Alive > team2Alive ? Team1.First() : Team2.First())!;
						ArenaPlayer loser = (team1Alive > team2Alive ? Team2.First() : Team1.First())!;

						Server.PrintToChatAll($"{Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.winner", winner.Controller.PlayerName, loser.Controller.PlayerName]}");
						Result = new ArenaResult(ArenaResultType.Win, null, null);

						Team1.Concat(Team2)
							.Where(p => p.Challenge is not null)
							.ToList()
							.ForEach(p => p.Challenge!.IsEnded = true);
					}
					else
					{
						List<ArenaPlayer> winners = (team1Alive > team2Alive ? Team1 : Team2)!;
						List<ArenaPlayer> losers = (team1Alive > team2Alive ? Team2 : Team1)!;

						Result = new ArenaResult(ArenaResultType.Win, winners, losers);
					}
				}
			}

			if (RoundType.EndFunction is not null)
			{
				List<CCSPlayerController>? team1 = Team1?.Select(p => p.Controller).ToList();
				List<CCSPlayerController>? team2 = Team2?.Select(p => p.Controller).ToList();

				if (team1 is null && team2 is null)
					return;

				RoundType.EndFunction(team1, team2);
			}
		}
	}
}