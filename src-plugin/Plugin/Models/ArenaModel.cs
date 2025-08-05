using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Utils;
using K4ArenaSharedApi;
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
		=> Team1?.Any(p => p.IsValid && Plugin.Arenas?.FindPlayer(p.Controller)?.AFK == false) == true && Team2?.Any(p => p.IsValid && Plugin.Arenas?.FindPlayer(p.Controller)?.AFK == false) == true;

	public bool HasFinished
		=> !IsActive || Team1?.All(p => p.IsValid && !p.IsAlive) == true || Team2?.All(p => p.IsValid && !p.IsAlive) == true;

	public bool HasRealPlayers
		=> Team1?.Any(p => p.IsValid && !p.Controller.IsBot) == true || Team2?.Any(p => p.IsValid && !p.Controller.IsBot) == true;

	public void AddChallengePlayers(List<ArenaPlayer> team1, List<ArenaPlayer> team2)
	{
		ArenaID = -2;
		Team1 = team1;
		Team2 = team2;
		RoundType = Plugin.GetCommonRoundType(team1.First().RoundPreferences, team2.First().RoundPreferences, false);
		ArenaScore = 0;

		var (t1Spawns, t2Spawns) = Random.Shared.Next(0, 2) == 1 ? (Spawns.Item1, Spawns.Item2) : (Spawns.Item2, Spawns.Item1);

		string team1Names = Team1?.Select(p => p.Controller.PlayerName).DefaultIfEmpty("none").Aggregate((a, b) => $"{a}, {b}") ?? "null";
		string team2Names = Team2?.Select(p => p.Controller.PlayerName).DefaultIfEmpty("none").Aggregate((a, b) => $"{a}, {b}") ?? "null";

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

		var (t1Spawns, t2Spawns) = Random.Shared.Next(0, 2) == 1 ? (Spawns.Item1, Spawns.Item2) : (Spawns.Item2, Spawns.Item1);

		string team1Names = Team1?.Select(p => p.Controller.PlayerName).DefaultIfEmpty("none").Aggregate((a, b) => $"{a}, {b}") ?? "null";
		string team2Names = Team2?.Select(p => p.Controller.PlayerName).DefaultIfEmpty("none").Aggregate((a, b) => $"{a}, {b}") ?? "null";

		SetPlayerDetails(Team1, t1Spawns, CsTeam.Terrorist, Team2);
		SetPlayerDetails(Team2, t2Spawns, CsTeam.CounterTerrorist, Team1);
	}

	public void WarmupPopulate()
	{
		ClearInvalidTeamPlayers();

		Queue<ArenaPlayer> availablePlayers = new Queue<ArenaPlayer>(Plugin.WaitingArenaPlayers.Where(p => p.IsValid && !p.AFK && p.PlayerIsSafe));

		if (availablePlayers.Count == 0 || (Team1?.Count > 0 && Team2?.Count > 0))
			return;

		ArenaID = -1;
		RoundType = new RoundType("none", 1, null, null, false, WeaponType.Unknown, false);
		ArenaScore = 0;

		var (t1Spawns, t2Spawns) = Random.Shared.Next(0, 2) == 1 ? (Spawns.Item1, Spawns.Item2) : (Spawns.Item2, Spawns.Item1);

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
				team = [player];
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

		List<SpawnPoint> spawnsCopy = [.. spawns];

		foreach (ArenaPlayer player in team)
		{
			if (player?.IsValid == true)
			{
				int randomSpawnIndex = Random.Shared.Next(0, spawnsCopy.Count);
				player.SpawnPoint = spawnsCopy[randomSpawnIndex];
				spawnsCopy.RemoveAt(randomSpawnIndex);

				player.Controller.MVPs = player.MVPs;
				Utilities.SetStateChanged(player.Controller, "CCSPlayerController", "m_iMVPs");

				player.Controller.Score = ArenaScore;
				Utilities.SetStateChanged(player.Controller, "CCSPlayerController", "m_iScore");

				if (player.Controller.ActionTrackingServices != null)
				{
					player.Controller.ActionTrackingServices.MatchStats.Damage = ArenaScore;
					Utilities.SetStateChanged(player.Controller, "CCSPlayerController", "m_pActionTrackingServices");
				}

				player.ArenaTag = Plugin.GetRequiredTag(ArenaID);

				if (!Plugin.Config.CompatibilitySettings.DisableClantags)
				{
					player.Controller.Clan = player.ArenaTag;
					Utilities.SetStateChanged(player.Controller, "CCSPlayerController", "m_szClan");
				}

				if (player.Controller.Team > CsTeam.Spectator)
					player.Controller.SwitchTeam(switchTo);
				else
					player.Controller.ChangeTeam(switchTo);

				Plugin.AddTimer(1.0f, () =>
				{
					if (player.IsValid && !player.IsAlive && player.Controller.Team > CsTeam.Spectator && !player.AFK)
						player.Controller.Respawn();
				});

				if (!player.Controller.IsBot)
				{
					// Bots plugin sets bot_prefix at EventRoundPreStart hence some delay to print opponent names. (Frame not enough sometimes)
					Plugin.AddTimer(0.001f, () =>
					{
						if (player.Controller.IsValid)
							if (Plugin.Config.CommandSettings.CenterAnnounceMode)
							{
								var arenaName = Plugin.GetRequiredArenaName(ArenaID);
								var opponentNames = Plugin.GetOpponentNames(player.Controller, opponents) ?? "Unknown";
								var roundName = ArenaID == -1 ? Localizer.ForPlayer(player.Controller, "k4.general.random") : Localizer.ForPlayer(player.Controller, RoundType.Name ?? "Missing");

								player.CenterMessage = Localizer.ForPlayer(player.Controller, "k4.chat.arena_roundstart_html", arenaName, roundName, opponentNames);
								player.Controller.PrintToChat($" {Localizer.ForPlayer(player.Controller, "k4.general.prefix")} {Localizer.ForPlayer(player.Controller, "k4.chat.arena_roundstart", arenaName, opponentNames, roundName, opponentNames)}");
							}
					});
				}

				if (Plugin.gameRules?.WarmupPeriod == true)
				{
					SetupArenaPlayer(player.Controller);
				}
			}
		}
	}

	public void SetupArenaPlayer(CCSPlayerController? playerController)
	{
		if (playerController == null || !playerController.IsValid)
			return;

		ArenaPlayer? player = (Team1 ?? Enumerable.Empty<ArenaPlayer>())
			.Concat(Team2 ?? Enumerable.Empty<ArenaPlayer>())
			.FirstOrDefault(p => p.IsValid && p.Controller == playerController);

		if (player?.IsValid != true || player.Controller.PlayerPawn.Value == null)
			return;

		SpawnPoint? playerSpawn = player.SpawnPoint;

		if (playerSpawn == null)
		{
			Plugin.Logger.LogError($"Cannot spawn {player.Controller.PlayerName} because the spawn point is null");
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
			List<CCSPlayerController>? team1 = Team1?.Select(p => p.Controller).Where(c => c != null).ToList();
			List<CCSPlayerController>? team2 = Team2?.Select(p => p.Controller).Where(c => c != null).ToList();

			if (team1 == null && team2 == null)
			{
				Plugin.Logger.LogWarning("Both teams are null in SetupArenaPlayer");
				return;
			}

			Server.NextWorldUpdate(() => RoundType.StartFunction(team1, team2));
		}
		else
		{
			player.SetupWeapons(RoundType);
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
				List<ArenaPlayer> winners = (Team1 ?? Team2)!;
				Result = new ArenaResult(ArenaResultType.NoOpponent, winners, null);
			}
			else
			{
				int team1Alive = Team1.Count(p => p.IsValid && p.IsAlive);
				int team2Alive = Team2.Count(p => p.IsValid && p.IsAlive);

				if (team1Alive == team2Alive)
				{
					if (ArenaID == -2)
					{
						Plugin.PrintToChatAll("k4.general.challenge.tie", Team1.First().Controller.PlayerName, Team2.First().Controller.PlayerName);

						Team1.Concat(Team2).ToList().ForEach(p =>
						{
							var challenge = Plugin.FindChallengeForPlayer(p.Controller);
							if (challenge != null)
								challenge.IsEnded = true;
						});
					}

					Result = new ArenaResult(ArenaResultType.Tie, Team1, Team2);
				}
				else
				{
					List<ArenaPlayer> winners = (team1Alive > team2Alive ? Team1 : Team2)!;
					List<ArenaPlayer> losers = (team1Alive > team2Alive ? Team2 : Team1)!;

					winners.ForEach(p =>
					{
						p.MVPs++;
						p.Controller.MVPs = p.MVPs;
						Utilities.SetStateChanged(p.Controller, "CCSPlayerController", "m_iMVPs");
					});

					if (ArenaID == -2)
					{
						Plugin.PrintToChatAll("k4.general.challenge.winner", winners[0].Controller.PlayerName, losers[0].Controller.PlayerName);

						Team1.Concat(Team2).ToList().ForEach(p =>
						{
							var challenge = Plugin.FindChallengeForPlayer(p.Controller);
							if (challenge != null)
								challenge.IsEnded = true;
						});
					}

					Result = new ArenaResult(ArenaResultType.Win, winners, losers);
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