
namespace K4Arenas
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;
	using CounterStrikeSharp.API.Modules.Commands;
	using K4Arenas.Models;
	using CounterStrikeSharp.API.Modules.Admin;
	using System.Data;
	using CounterStrikeSharp.API.Modules.Entities.Constants;
	using System.Runtime.Serialization;
	using CounterStrikeSharp.API.Modules.Cvars;
	using Microsoft.Extensions.Logging;

	public sealed partial class Plugin : BasePlugin
	{
		public void TerminateRoundIfPossible()
		{
			if (IsBetweenRounds || gameRules == null)
				return;

			if (gameRules.WarmupPeriodEnd <= Server.CurrentTime && gameRules.WarmupPeriod == true)
			{
				IsBetweenRounds = true;

				Arenas?.Clear();
				Arenas = new Arenas(this);

				Server.NextWorldUpdate(() => Server.ExecuteCommand("mp_warmup_end; mp_restartgame 1"));
				return;
			}

			if (gameRules.WarmupPeriod == true || Arenas == null)
				return;

			List<CCSPlayerController> players = Utilities.GetPlayers()
					.Where(x => x?.IsValid == true && x.PlayerPawn?.IsValid == true && !x.IsHLTV && x.Connected == PlayerConnectedState.PlayerConnected)
					.ToList();

			if (!players.Any(p => !p.IsBot))
				return;

			if (Arenas.ArenaList.All(a => !a.HasRealPlayers || a.HasFinished))
			{
				IsBetweenRounds = true;

				List<CCSPlayerController> alivePlayers = players.Where(x => x.PlayerPawn.Value?.Health > 0).ToList();

				int tCount = alivePlayers.Count(x => x.Team == CsTeam.Terrorist);
				int ctCount = alivePlayers.Count(x => x.Team == CsTeam.CounterTerrorist);

				Server.NextWorldUpdate(() =>
				{
					try
					{
						var mpRoundRestartDelay = ConVar.Find("mp_round_restart_delay");
						float delay = mpRoundRestartDelay != null ? mpRoundRestartDelay.GetPrimitiveValue<float>() : 3f;
						gameRules.TerminateRound(delay, tCount > ctCount ? RoundEndReason.TerroristsWin : ctCount > tCount ? RoundEndReason.CTsWin : RoundEndReason.RoundDraw);
					}
					catch (Exception ex)
					{
						Logger.LogError($"Error in TerminateRound: {ex.Message}");
					}
				});
			}
		}

		public ArenaPlayer? SetupPlayer(CCSPlayerController playerController)
		{
			if (playerController == null || !playerController.IsValid)
			{
				Logger.LogWarning("Attempted to setup null or invalid player");
				return null;
			}

			try
			{
				ArenaPlayer arenaPlayer = new ArenaPlayer(this, playerController);
				WaitingArenaPlayers.Enqueue(arenaPlayer);

				arenaPlayer.ArenaTag = $"{Localizer[gameRules?.WarmupPeriod == true ? "k4.general.warmup" : "k4.general.waiting"]} |";

				if (!Config.CompatibilitySettings.DisableClantags)
				{
					playerController.Clan = arenaPlayer.ArenaTag;
					Utilities.SetStateChanged(playerController, "CCSPlayerController", "m_szClan");
				}

				if (!arenaPlayer.Controller.IsBot)
				{
					arenaPlayer.Controller.PrintToChat($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.queue_added", WaitingArenaPlayers.Count]}");
					arenaPlayer.Controller.PrintToChat($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.arena_afk", Config.CommandSettings.AFKCommands.FirstOrDefault() ?? "Missing"]}");

					if (HasDatabase)
					{
						arenaPlayer.Controller.PrintToChat($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.arena_commands", Config.CommandSettings.GunsCommands.FirstOrDefault() ?? "Missing", Config.CommandSettings.RoundsCommands.FirstOrDefault() ?? "Missing"]}");

						ulong steamID = playerController.SteamID;
						Task.Run(async () =>
						{
							try
							{
								await LoadPlayerAsync(steamID);
							}
							catch (Exception ex)
							{
								Logger.LogError($"Error loading player data: {ex.Message}");
							}
						});
					}
				}

				return arenaPlayer;
			}
			catch (Exception ex)
			{
				Logger.LogError($"Error in SetupPlayer: {ex.Message}");
				return null;
			}
		}

		public int GetPlayerArenaID(ArenaPlayer player)
		{
			int playerArenaID = Arenas!.ArenaList
				.Where(a => a.Team1?.Any(p => p == player) == true || a.Team2?.Any(p => p == player) == true)
				.Select(a => a.ArenaID)
				.FirstOrDefault(-1);

			return playerArenaID;
		}

		public bool CommandHelper(CCSPlayerController? player, CommandInfo info, CommandUsage usage, int argCount = 0, string? help = null, string? permission = null)
		{
			switch (usage)
			{
				case CommandUsage.CLIENT_ONLY:
					if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
					{
						info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.commandclientonly"]}");
						return false;
					}
					break;
				case CommandUsage.SERVER_ONLY:
					if (player != null)
					{
						info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.commandserveronly"]}");
						return false;
					}
					break;
				case CommandUsage.CLIENT_AND_SERVER:
					if (!(player == null || (player != null && player.IsValid && player.PlayerPawn.Value != null)))
						return false;
					break;
			}

			if (permission != null && permission.Length > 0)
			{
				if (player != null && !AdminManager.PlayerHasPermissions(player, permission))
				{
					info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.commandnoperm"]}");
					return false;
				}
			}

			if (argCount > 0 && help != null)
			{
				int checkArgCount = argCount + 1;
				if (info.ArgCount < checkArgCount)
				{
					info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.commandhelp", info.ArgByIndex(0).Replace("css_", string.Empty), help]}");
					return false;
				}
			}

			return true;
		}

		public void CheckCommonProblems()
		{
			// Common things that fuck up the gameplay
			Server.ExecuteCommand("mp_join_grace_time 0");

			// This could cause problems with the items
			Server.ExecuteCommand("mp_t_default_secondary \"\"");
			Server.ExecuteCommand("mp_ct_default_secondary \"\"");
			Server.ExecuteCommand("mp_t_default_primary \"\"");
			Server.ExecuteCommand("mp_ct_default_primary \"\"");
			Server.ExecuteCommand("mp_equipment_reset_rounds 0");
			Server.ExecuteCommand("mp_free_armor 0");
		}

		public RoundType GetCommonRoundType(List<RoundType>? roundPreferences1, List<RoundType>? roundPreferences2, bool multi)
		{
			List<RoundType> commonRounds = roundPreferences1?.Intersect(roundPreferences2 ?? roundPreferences1)?.ToList() ?? new List<RoundType>();
			List<RoundType> commonUsableRounds = multi ? commonRounds : commonRounds.Where(rt => rt.TeamSize < 2).ToList();

			if (commonUsableRounds.Any())
			{
				return commonUsableRounds[Random.Shared.Next(0, commonUsableRounds.Count)];
			}

			List<RoundType> availableRoundTypes = RoundType.RoundTypes.Where(rt => rt.TeamSize < 2).ToList();
			return availableRoundTypes[Random.Shared.Next(0, availableRoundTypes.Count)];
		}

		public static void MoveQueue<T>(Queue<T> from, Queue<T> to)
		{
			while (from.Count > 0)
			{
				T item = from.Dequeue();
				to.Enqueue(item);
			}
		}

		public void EnqueueTeamPlayers(List<ArenaPlayer>? team, Queue<ArenaPlayer> queue)
		{
			if (team is null)
				return;

			foreach (ArenaPlayer player in team)
			{
				if (player?.IsValid == true)
				{
					queue.Enqueue(player);
				}
			}
		}

		public string GetOpponentNames(List<ArenaPlayer>? opponents)
		{
			if (opponents is null || opponents.Count == 0)
				return Localizer["k4.general.no_opponent"];


			return string.Join(", ", opponents.Where(p => p.IsValid).Select(p => p.Controller.IsBot && !string.IsNullOrEmpty(GetArenaName(p.Controller)) ? $"{Localizer["k4.general.bot"]} " + p.Controller.PlayerName.Replace(GetArenaName(p.Controller), "").Replace("|", "") : p.Controller.PlayerName));
		}

		public string GetArenaName(CCSPlayerController player)
		{
			var arenaPlayer = Arenas?.FindPlayer(player);
			if (arenaPlayer is not null)
			{
				string arenaTag = arenaPlayer.ArenaTag;
				if (arenaTag.EndsWith(" |"))
					arenaTag = arenaTag.Substring(0, arenaTag.Length - 2);

				return arenaTag;
			}
			return string.Empty;
		}

		public static CsItem? FindEnumValueByEnumMemberValue(string? search)
		{
			if (search is null)
				return null;

			var type = typeof(CsItem);
			foreach (var field in type.GetFields())
			{
				var attribute = field.GetCustomAttributes(typeof(EnumMemberAttribute), false).Cast<EnumMemberAttribute>().FirstOrDefault();
				if (attribute?.Value == search)
				{
					return (CsItem?)field.GetValue(null);
				}
			}
			return null;
		}

		public string? GetRequiredTag(CCSPlayerController player)
		{
			var arena = Arenas?.ArenaList.FirstOrDefault(a =>
				(a.Team1?.Any(p => p.Controller == player) ?? false) ||
				(a.Team2?.Any(p => p.Controller == player) ?? false));

			if (arena != null)
				return GetRequiredTag(arena.ArenaID);

			if (WaitingArenaPlayers.Any(p => p.Controller == player))
				return Localizer["k4.general.waiting"];

			return null;
		}

		public string GetRequiredTag(int arenaID) =>
			arenaID switch
			{
				-2 => $"{Localizer["k4.general.challenge"]} |",
				-1 => $"{Localizer["k4.general.warmup"]} |",
				_ => $"{Localizer["k4.general.arena"]} {arenaID} |",
			};

		public string GetRequiredArenaName(int arenaID) =>
		arenaID switch
		{
			-2 => Localizer["k4.general.challenge"],
			-1 => Localizer["k4.general.warmup"],
			_ => $"{arenaID}"
		};
	}
}