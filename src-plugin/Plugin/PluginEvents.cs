namespace K4Arenas
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Translations;
	using CounterStrikeSharp.API.Modules.Cvars;
	using CounterStrikeSharp.API.Modules.Timers;
	using CounterStrikeSharp.API.Modules.Utils;
	using K4Arenas.Models;

	public sealed partial class Plugin : BasePlugin
	{
		private int lastRealPlayers = 0;
		public void Initialize_Events()
		{
			RegisterListener<Listeners.OnMapStart>((mapName) =>
			{
				Task.Run(PurgeDatabaseAsync);

				AddTimer(0.1f, () =>
				{
					Arenas ??= new Arenas(this);
					lastRealPlayers = 0;

					GameConfig?.Apply();
					CheckCommonProblems();

					gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;

					foreach (CCSPlayerController player in Utilities.GetPlayers().Where(x => x?.IsValid == true && !x.IsHLTV && x.Connected == PlayerConnectedState.PlayerConnected && !x.IsBot))
					{
						if (Arenas.FindPlayer(player) == null)
							SetupPlayer(player);
					}

					AddTimer(3, () => // ! Fixes issues with the 3 sec warmup countdown when warmuptime is 0
					{
						if (gameRules?.WarmupPeriod == true && ConVar.Find("mp_warmuptime")?.GetPrimitiveValue<float>() > 0.0f)
						{
							WarmupTimer = AddTimer(2.0f, () => // ! Populate warmup slots every 2 seconds
							{
								if (lastRealPlayers == 0)
								{
									lastRealPlayers = Utilities.GetPlayers().Count(x => x?.IsValid == true && !x.IsHLTV && x.Connected == PlayerConnectedState.PlayerConnected && !x.IsBot);
									return;
								}

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
			});

			RegisterListener<Listeners.OnMapEnd>(() =>
			{
				Arenas?.Clear();
				Arenas = null;

				WaitingArenaPlayers.Clear();
				IsBetweenRounds = false;
			});

			RegisterEventHandler((EventRoundFreezeEnd @event, GameEventInfo info) =>
			{
				var players = Utilities.GetPlayers().Where(x => x?.IsValid == true && x.PlayerPawn?.IsValid == true && !x.IsBot && !x.IsHLTV);

				if (players.Any())
				{
					foreach (var player in players)
					{
						ArenaPlayer? arenaPlayer = Arenas?.FindPlayer(player);
						if (arenaPlayer != null)
						{
							arenaPlayer.CenterMessage = string.Empty;
						}
					}
				}
				return HookResult.Continue;
			});

			RegisterEventHandler((EventPlayerActivate @event, GameEventInfo info) =>
			{
				CCSPlayerController? playerController = @event.Userid;

				if (playerController is null || !playerController.IsValid)
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
				CCSPlayerController? playerController = @event.Userid;
				if (playerController is null || !playerController.IsValid)
					return HookResult.Continue;

				WaitingArenaPlayers = new Queue<ArenaPlayer>(WaitingArenaPlayers.Where(p => p.Controller != playerController));
				Arenas?.ArenaList.ForEach(arena => arena.RemovePlayer(playerController));
				return HookResult.Continue;
			});

			RegisterEventHandler<EventPlayerBlind>((@event, info) =>
			{
				if (!Config.CompatibilitySettings.BlockFlashOfNotOpponent)
					return HookResult.Continue;

				ArenaPlayer? attacker = Arenas?.FindPlayer(@event.Attacker);
				ArenaPlayer? target = Arenas?.FindPlayer(@event.Userid);

				if (Arenas is null || attacker is null || target is null)
					return HookResult.Continue;

				if (!attacker.IsValid || !target.IsValid)
					return HookResult.Continue;

				int attackerArenaNumber = Arenas.ArenaList.FindIndex(a => a.Team1?.Any(p => p == attacker) == true || a.Team2?.Any(p => p == attacker) == true);
				int targetArenaNumber = Arenas.ArenaList.FindIndex(a => a.Team1?.Any(p => p == target) == true || a.Team2?.Any(p => p == target) == true);

				if (attackerArenaNumber != targetArenaNumber)
				{
					if (target.Controller.PlayerPawn.Value != null)
						target.Controller.PlayerPawn.Value.BlindUntilTime = Server.CurrentTime;
				}

				return HookResult.Continue;
			});

			RegisterEventHandler((EventPlayerHurt @event, GameEventInfo info) =>
			{
				if (!Config.CompatibilitySettings.BlockDamageOfNotOpponent)
					return HookResult.Continue;

				ArenaPlayer? attacker = Arenas?.FindPlayer(@event.Attacker);
				ArenaPlayer? target = Arenas?.FindPlayer(@event.Userid);

				if (Arenas is null || attacker is null || target is null)
					return HookResult.Continue;

				if (!attacker.IsValid || !target.IsValid)
					return HookResult.Continue;

				int attackerArenaNumber = Arenas.ArenaList.FindIndex(a => a.Team1?.Any(p => p == attacker) == true || a.Team2?.Any(p => p == attacker) == true);
				int targetArenaNumber = Arenas.ArenaList.FindIndex(a => a.Team1?.Any(p => p == target) == true || a.Team2?.Any(p => p == target) == true);

				if (attackerArenaNumber != targetArenaNumber)
				{
					if (target.Controller.PlayerPawn.Value != null)
					{
						target.Controller.PlayerPawn.Value.Health += @event.DmgHealth;
						target.Controller.PlayerPawn.Value.ArmorValue += @event.DmgArmor;
					}
				}

				return HookResult.Continue;
			}, HookMode.Pre);

			RegisterEventHandler((EventRoundPrestart @event, GameEventInfo info) =>
			{
				if (gameRules == null || gameRules.WarmupPeriod || Arenas == null)
					return HookResult.Continue;

				Queue<ArenaPlayer> arenaWinners = new();
				Queue<ArenaPlayer> arenaLosers = new();

				foreach (Arena arena in Arenas.ArenaList.OrderBy(a => a.ArenaID < 0).ThenBy(a => Math.Abs(a.ArenaID)))
				{
					if (arena.ArenaID == -2)
					{
						var arenaPlayers = arena.Team1?.Concat(arena.Team2 ?? []);
						if (arenaPlayers == null || !arenaPlayers.Any())
							continue;

						foreach (var player in arenaPlayers)
						{
							ChallengeModel? challenge = FindChallengeForPlayer(player.Controller);
							if (challenge is null)
							{
								arenaLosers.Enqueue(player);
								continue;
							}

							ArenaResult result = arena.Result;

							var player1 = challenge.Player1;
							var player2 = challenge.Player2;

							MoveBackChallengePlayer(player1, challenge.Player1Placement, ref result.Winners?.Contains(player1) == true ? ref arenaWinners : ref arenaLosers);
							MoveBackChallengePlayer(player2, challenge.Player2Placement, ref result.Winners?.Contains(player2) == true ? ref arenaWinners : ref arenaLosers);

							Challenges.Remove(challenge);
						}
					}
					else
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
								if (arena.Team1?.All(p => p.Controller.IsBot) == true && arena.Team2?.All(p => p.Controller.IsBot) == true)
								{
									var (winners, losers) = Random.Shared.Next(2) == 0
										? (arena.Team1, arena.Team2)
										: (arena.Team2, arena.Team1);

									EnqueueTeamPlayers(winners, arenaWinners);
									EnqueueTeamPlayers(losers, arenaLosers);
								}
								else
								{
									EnqueueTeamPlayers(arena.Team1, arenaLosers);
									EnqueueTeamPlayers(arena.Team2, arenaLosers);
								}
								break;
						}
					}
				}

				Challenges.RemoveAll(c => c.IsEnded || !c.IsAccepted);

				Queue<ArenaPlayer> rankedPlayers = new Queue<ArenaPlayer>();

				if (arenaWinners.Count > 1)
				{
					rankedPlayers.Enqueue(arenaWinners.Dequeue());
					rankedPlayers.Enqueue(arenaWinners.Dequeue());
				}

				while (arenaWinners.Count > 0)
				{
					rankedPlayers.Enqueue(arenaWinners.Dequeue());

					if (arenaLosers.Count > 0)
					{
						rankedPlayers.Enqueue(arenaLosers.Dequeue());
					}
				}

				MoveQueue(arenaLosers, rankedPlayers);
				MoveQueue(WaitingArenaPlayers, rankedPlayers);

				Arenas.Shuffle();

				Queue<ArenaPlayer> notAFKrankedPlayers = new Queue<ArenaPlayer>();

				IEnumerable<ArenaPlayer> validPlayers = rankedPlayers.Where(p => p.IsValid);
				foreach (ArenaPlayer player in validPlayers)
				{
					if (player.AFK)
					{
						player.Controller.PrintToChat($"{Localizer.ForPlayer(player.Controller, "k4.general.prefix")} {Localizer.ForPlayer(player.Controller, "k4.chat.afk_reminder", Config.CommandSettings.AFKCommands.FirstOrDefault("Missing"))}");

						player.ArenaTag = $"{Localizer["k4.general.afk"]} |";

						if (!Config.CompatibilitySettings.DisableClantags)
						{
							player.Controller.Clan = player.ArenaTag;
							Utilities.SetStateChanged(player.Controller, "CCSPlayerController", "m_szClan");
						}

						WaitingArenaPlayers.Enqueue(player);
					}
					else
						notAFKrankedPlayers.Enqueue(player);
				}

				bool anyTeamRoundTypes = RoundType.RoundTypes.Any(roundType => roundType.TeamSize > 1);

				// ? Prioritize real players over bots
				notAFKrankedPlayers = new Queue<ArenaPlayer>(notAFKrankedPlayers.OrderBy(p => p.Controller.IsBot));

				Challenges.RemoveAll(c => !c.Player1.IsValid || !c.Player2.IsValid);

				int displayIndex = 1;
				int handledChallanges = 0;
				for (int arenaID = 0; arenaID < Arenas.Count; arenaID++)
				{
					if (Challenges.Count > handledChallanges)
					{
						ChallengeModel challenge = Challenges[handledChallanges];

						List<ArenaPlayer> team1 = [challenge.Player1];
						List<ArenaPlayer> team2 = [challenge.Player2];

						notAFKrankedPlayers = new Queue<ArenaPlayer>(notAFKrankedPlayers.Except(team1.Concat(team2)));

						handledChallanges++;
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
						notAFKrankedPlayers.TryDequeue(out ArenaPlayer? player2);

						RoundType roundType = GetCommonRoundType(player1.RoundPreferences, player2?.RoundPreferences, false);

						Arenas.ArenaList[arenaID].AddPlayers([player1], player2 != null ? [player2] : null, roundType, displayIndex, (Arenas.Count - displayIndex) * 50);
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

					arenaPlayer.ArenaTag = $"{Localizer["k4.general.waiting"]} |";

					if (!Config.CompatibilitySettings.DisableClantags)
					{
						arenaPlayer.Controller.Clan = arenaPlayer.ArenaTag;
						Utilities.SetStateChanged(arenaPlayer.Controller, "CCSPlayerController", "m_szClan");
					}

					if (arenaPlayer.PlayerIsSafe)
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

			RegisterEventHandler((EventRoundMvp @event, GameEventInfo info) =>
			{
				return HookResult.Handled;
			}, HookMode.Pre);

			RegisterEventHandler((EventPlayerDeath @event, GameEventInfo info) =>
			{
				AddTimer(1.0f, TerminateRoundIfPossible);
				return HookResult.Continue;
			});

			RegisterEventHandler((EventPlayerTeam @event, GameEventInfo info) =>
			{
				info.DontBroadcast = true;
				var player = @event.Userid;

				if (player is null || !player.IsValid)
					return HookResult.Continue;

				var oldTeam = (CsTeam)@event.Oldteam;
				var newTeam = (CsTeam)@event.Team;

				if (oldTeam == CsTeam.None || (oldTeam > CsTeam.Spectator && newTeam > CsTeam.Spectator))
					return HookResult.Continue;

				if (!player.IsBot)
					TerminateRoundIfPossible();

				ArenaPlayer? arenaPlayer = Arenas?.FindPlayer(player);

				if (arenaPlayer?.AFK == false && player.Team != CsTeam.Spectator && newTeam == CsTeam.Spectator)
				{
					arenaPlayer!.AFK = true;

					arenaPlayer.ArenaTag = $"{Localizer["k4.general.afk"]} |";

					if (!Config.CompatibilitySettings.DisableClantags)
					{
						player.Clan = arenaPlayer.ArenaTag;
						Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
					}

					player!.ChangeTeam(CsTeam.Spectator);

					player.PrintToChat($" {Localizer.ForPlayer(player, "k4.general.prefix")} {string.Format(Localizer.ForPlayer(player, "k4.chat.afk_enabled"), Config.CommandSettings.AFKCommands.FirstOrDefault("Missing"))}");
					return HookResult.Stop;
				}
				else if (arenaPlayer?.AFK == true && player.Team == CsTeam.Spectator && newTeam > CsTeam.Spectator)
				{
					arenaPlayer!.AFK = false;

					arenaPlayer.ArenaTag = $"{Localizer["k4.general.waiting"]} |";

					if (!Config.CompatibilitySettings.DisableClantags)
					{
						arenaPlayer.Controller.Clan = arenaPlayer.ArenaTag;
						Utilities.SetStateChanged(arenaPlayer.Controller, "CCSPlayerController", "m_szClan");
					}

					player.PrintToChat($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "k4.chat.afk_disabled")}");
					return HookResult.Continue;
				}

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