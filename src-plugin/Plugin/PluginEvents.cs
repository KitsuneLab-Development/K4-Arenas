namespace K4Arenas
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Timers;
	using CounterStrikeSharp.API.Modules.Utils;
	using K4Arenas.Models;
	using Microsoft.Extensions.Logging;

	public sealed partial class Plugin : BasePlugin
	{
		public void Initialize_Events()
		{
			RegisterListener<Listeners.OnMapStart>((mapName) =>
			{
				Task.Run(PurgeDatabaseAsync);

				AddTimer(0.1f, () =>
				{
					GameConfig?.Apply();
					CheckCommonProblems();

					if (Arenas is null)
						Arenas = new Arenas(this);

					gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;

					if (gameRules?.WarmupPeriod == true)
					{
						WarmupTimer = AddTimer(1.0f, () =>
						{
							if (gameRules?.WarmupPeriod == true)
							{
								foreach (Arena arena in Arenas.ArenaList)
									arena.WarmupPopulate();
							}
							else
							{
								GameConfig?.Apply();
								WarmupTimer?.Kill();
							}
						}, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
					}
				});
			});

			RegisterListener<Listeners.OnMapEnd>(() =>
			{
				Arenas?.Clear();
				Arenas = null;

				WaitingArenaPlayers.Clear();
				IsBetweenRounds = false;
			});

			RegisterEventHandler((EventPlayerActivate @event, GameEventInfo info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (!playerController.IsValid)
					return HookResult.Continue;

				if (playerController.IsHLTV)
					return HookResult.Continue;

				if (Arenas?.FindPlayer(playerController) != null)
					return HookResult.Continue;

				SetupPlayer(playerController);

				if (gameRules?.WarmupPeriod == false)
				{
					if (playerController.IsBot)
						return HookResult.Continue;

					TerminateRoundIfPossible();
				}

				return HookResult.Continue;
			});

			RegisterEventHandler((EventPlayerDisconnect @event, GameEventInfo info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (!playerController.IsValid)
					return HookResult.Continue;

				if (!playerController.IsBot && !playerController.IsHLTV)
				{
					ArenaPlayer? arenaPlayer = Arenas?.FindPlayer(playerController);

					if (arenaPlayer is null)
						return HookResult.Continue;

					Task.Run(() => SavePlayerPreferencesAsync(new List<ArenaPlayer> { arenaPlayer }));
				}

				WaitingArenaPlayers = new Queue<ArenaPlayer>(WaitingArenaPlayers.Where(p => p.Controller != playerController));
				Arenas?.ArenaList.ForEach(arena => arena.RemovePlayer(playerController));
				return HookResult.Continue;
			});

			RegisterEventHandler((EventRoundPrestart @event, GameEventInfo info) =>
			{
				if (gameRules == null || gameRules.WarmupPeriod)
					return HookResult.Continue;

				if (Arenas is null)
					Arenas = new Arenas(this);

				GameConfig?.Apply();
				CheckCommonProblems();

				Utilities.GetPlayers()
					.Where(x => x?.IsValid == true && x.PlayerPawn?.IsValid == true && !x.IsHLTV && x.Connected == PlayerConnectedState.PlayerConnected)
					.ToList()
					.ForEach(x =>
					{
						if (WaitingArenaPlayers.Any(p => p.Controller == x) || Arenas.IsPlayerInArena(x))
							return;

						WaitingArenaPlayers.Enqueue(new ArenaPlayer(this, x));
					});

				Queue<ArenaPlayer> arenaWinners = new Queue<ArenaPlayer>();
				Queue<ArenaPlayer> arenaLosers = new Queue<ArenaPlayer>();

				foreach (Arena arena in Arenas.ArenaList)
				{
					ArenaResult arenaResult = arena.Result;

					switch (arenaResult.ResultType)
					{
						case ArenaResultType.Win:
							EnqueueTeamPlayers(arenaResult.Winners, arenaWinners);
							EnqueueTeamPlayers(arenaResult.Losers, arenaLosers);
							break;
						case ArenaResultType.NoOpponent:
							EnqueueTeamPlayers(arenaResult.Winners, arenaWinners);
							break;
						case ArenaResultType.Tie:
							EnqueueTeamPlayers(arenaResult.Winners, arenaLosers);
							EnqueueTeamPlayers(arenaResult.Losers, arenaLosers);
							break;
					}
				}

				Queue<ArenaPlayer> rankedPlayers = new Queue<ArenaPlayer>();

				if (arenaWinners.Count > 1)
				{
					ArenaPlayer p1 = arenaWinners.Dequeue();
					ArenaPlayer p2 = arenaWinners.Dequeue();

					rankedPlayers.Enqueue(p1);
					rankedPlayers.Enqueue(p2);
				}

				while (arenaWinners.Count > 0)
				{
					ArenaPlayer player = arenaWinners.Dequeue();
					rankedPlayers.Enqueue(player);

					if (arenaLosers.Count > 0)
					{
						player = arenaLosers.Dequeue();
						rankedPlayers.Enqueue(player);
					}
				}

				MoveQueue(arenaLosers, rankedPlayers);
				MoveQueue(WaitingArenaPlayers, rankedPlayers);
				MoveEndedChallengesToQueue(rankedPlayers);

				if (rankedPlayers.GroupBy(p => p.Controller).Any(g => g.Count() > 1))
				{
					Logger.LogCritical("There is a player twice in the rankedPlayers queue. Please notify the developer about this!");

					var distinctPlayers = new Queue<ArenaPlayer>();
					var seenControllers = new HashSet<CCSPlayerController>();
					foreach (var player in rankedPlayers)
					{
						if (!seenControllers.Contains(player.Controller))
						{
							distinctPlayers.Enqueue(player);
							seenControllers.Add(player.Controller);
						}
					}
					rankedPlayers = distinctPlayers;
				}

				Arenas.Shuffle();

				Queue<ArenaPlayer> notAFKrankedPlayers = new Queue<ArenaPlayer>();

				IEnumerable<ArenaPlayer> validPlayers = rankedPlayers.Where(p => p.IsValid);
				foreach (ArenaPlayer player in validPlayers)
				{
					if (player.AFK)
					{
						player.Controller.PrintToChat($"{Localizer["k4.general.prefix"]} {Localizer["k4.chat.afk_reminder", Config.CommandSettings.AFKCommands.FirstOrDefault("Missing")]}");

						player.Controller.Clan = $"{Localizer["k4.general.afk"]} |";
						Utilities.SetStateChanged(player.Controller, "CCSPlayerController", "m_szClan");

						WaitingArenaPlayers.Enqueue(player);
					}
					else
						notAFKrankedPlayers.Enqueue(player);
				}

				bool anyTeamRoundTypes = RoundType.RoundTypes.Any(roundType => roundType.TeamSize > 1);

				foreach (ArenaPlayer player in notAFKrankedPlayers)
				{
					if (player.Challenge?.IsAccepted == false)
					{
						player.Challenge = null;
					}
				}

				Queue<ChallengeModel> challengeList = new Queue<ChallengeModel>(notAFKrankedPlayers
					.Where(p => p.Challenge != null)
					.Select(p => p.Challenge!)
					.Distinct());

				int displayIndex = 1;
				for (int arenaID = 0; arenaID < Arenas.Count; arenaID++)
				{
					if (challengeList.Count > 0)
					{
						ChallengeModel challenge = challengeList.Dequeue()!;

						List<ArenaPlayer> team1 = new List<ArenaPlayer> { challenge.Player1 };
						List<ArenaPlayer> team2 = new List<ArenaPlayer> { challenge.Player2 };

						if (team1.Count(p => p.IsValid) == 0 || team2.Count(p => p.IsValid) == 0)
							continue;

						notAFKrankedPlayers = new Queue<ArenaPlayer>(notAFKrankedPlayers.Except(team1.Concat(team2)));

						Arenas.ArenaList[arenaID].AddChallengePlayers(team1, team2);
						continue;
					}

					if (anyTeamRoundTypes && RoundType.RoundTypes.Where(roundType => roundType.TeamSize > 1).Any(roundType => Arenas.AddTeamsToArena(arenaID, displayIndex, roundType.TeamSize, notAFKrankedPlayers, roundType)))
					{
						displayIndex++;
						continue;
					}

					if (notAFKrankedPlayers.Count >= 1)
					{
						ArenaPlayer player1 = notAFKrankedPlayers.Dequeue();
						ArenaPlayer? player2 = notAFKrankedPlayers.Count >= 1 ? notAFKrankedPlayers.Dequeue() : null;

						RoundType roundType = GetCommonRoundType(player1.RoundPreferences, player2?.RoundPreferences, false);

						Arenas.ArenaList[arenaID].AddPlayers(new List<ArenaPlayer> { player1 }, player2 != null ? new List<ArenaPlayer> { player2 } : null, roundType, displayIndex, (Arenas.Count - displayIndex) * 50);
						displayIndex++;
					}
					else
					{
						Arenas.ArenaList[arenaID].AddPlayers(null, null, null, displayIndex, (Arenas.Count - displayIndex) * 50);
						displayIndex++;
					}
				}

				while (notAFKrankedPlayers.Count > 0)
				{
					ArenaPlayer arenaPlayer = notAFKrankedPlayers.Dequeue();

					arenaPlayer.Controller.Clan = $"{Localizer["k4.general.waiting"]} |";
					Utilities.SetStateChanged(arenaPlayer.Controller, "CCSPlayerController", "m_szClan");

					arenaPlayer.Controller.ChangeTeam(CsTeam.Spectator);

					WaitingArenaPlayers.Enqueue(arenaPlayer);
				}

				return HookResult.Continue;
			});

			RegisterEventHandler((EventRoundStart @event, GameEventInfo info) =>
			{
				IsBetweenRounds = false;
				return HookResult.Continue;
			});

			RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
			{
				IsBetweenRounds = true;

				if (Arenas is null)
					return HookResult.Continue;

				foreach (Arena arena in Arenas.ArenaList)
					arena.OnRoundEnd();

				return HookResult.Continue;
			});

			RegisterEventHandler((EventPlayerSpawn @event, GameEventInfo info) =>
			{
				if (Arenas is null)
					return HookResult.Continue;

				foreach (Arena arena in Arenas.ArenaList)
					arena.SetupArenaPlayer(@event.Userid);

				return HookResult.Continue;
			});

			RegisterEventHandler((EventPlayerDeath @event, GameEventInfo info) =>
			{
				TerminateRoundIfPossible(@event.Userid.Team);
				return HookResult.Continue;
			});

			RegisterEventHandler((EventPlayerTeam @event, GameEventInfo info) =>
			{
				info.DontBroadcast = true;
				return HookResult.Changed;
			}, HookMode.Pre);

			RegisterEventHandler((EventSwitchTeam @event, GameEventInfo info) =>
			{
				info.DontBroadcast = true;
				return HookResult.Changed;
			}, HookMode.Pre);
		}
	}
}