namespace K4Arenas
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Commands.Targeting;
	using CounterStrikeSharp.API.Modules.Entities.Constants;
	using CounterStrikeSharp.API.Modules.Utils;
	using K4Arenas.Models;
	using Microsoft.Extensions.Logging;

	public sealed partial class Plugin : BasePlugin
	{
		public void Initialize_Commands()
		{
			Config.CommandSettings.QueueCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Checks the queue position for the 1v1 arena", Command_Queue);
			});

			Config.CommandSettings.RoundsCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Opens the round preference menu", Command_RoundPref);
			});

			Config.CommandSettings.GunsCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Opens the weapon preference menu", Command_WeaponPref);
			});

			Config.CommandSettings.AFKCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Toggles the AFK status", Command_AFK);
			});

			Config.CommandSettings.ChallengeCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Challenges a player to a 1v1", Command_Challenge);
			});

			Config.CommandSettings.ChallengeAcceptCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Accepts a challenge", Command_Accept);
			});

			Config.CommandSettings.ChallengeDeclineCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Declines a challenge", Command_Decline);
			});
		}

		public void Command_Accept(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			ArenaPlayer? p1 = Arenas?.FindPlayer(player!);

			if (p1 is null)
				return;

			if (p1.Challenge is null)
			{
				info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.notchallenged"]}");
				return;
			}

			ArenaPlayer p2 = p1.Challenge.Player1;

			if (!p2.IsValid)
			{
				info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.notavailable"]}");
				p1.Challenge = null;
				return;
			}

			p1.Challenge!.IsAccepted = true;
			p2.Challenge!.IsAccepted = true;

			info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.accepted", p2.Controller.PlayerName]}");
			p2.Controller.PrintToChat($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.acceptedby", player!.PlayerName]}");
		}

		public void Command_Decline(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			ArenaPlayer? p1 = Arenas?.FindPlayer(player!);

			if (p1 is null)
				return;

			if (p1.Challenge is null)
			{
				info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.notchallenged"]}");
				return;
			}

			ArenaPlayer p2 = p1.Challenge.Player1;

			if (!p2.IsValid)
			{
				info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.notavailable"]}");
				p1.Challenge = null;
				return;
			}

			p1.Challenge = null;
			p2.Challenge = null;

			info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.declined", p2.Controller.PlayerName]}");
			p2.Controller.PrintToChat($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.declinedby", player!.PlayerName]}");
		}

		public void Command_Challenge(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY, 1, "[name]"))
				return;

			ArenaPlayer? p1 = Arenas?.FindPlayer(player!);

			if (p1 is null)
				return;

			if (p1.Challenge != null)
			{
				info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.inchallenge"]}");
				return;
			}

			TargetResult targetResult = info.GetArgTargetResult(1);
			if (targetResult.Count() != 1)
			{
				info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.invalidtarget"]}");
				return;
			}

			CCSPlayerController challengedPlayer = targetResult.First();
			ArenaPlayer? p2 = Arenas?.FindPlayer(challengedPlayer);

			if (p2 is null || p2 == p1)
			{
				info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.invalidtarget"]}");
				return;
			}

			if (p2.Challenge != null)
			{
				info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.inchallenge"]}");
				return;
			}

			int p1ArenaID = GetPlayerInArena(p1);
			int p2ArenaID = GetPlayerInArena(p2);

			if (p1ArenaID == -1 || p2ArenaID == -1)
			{
				info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.notinarena"]}");
				return;
			}

			ChallengeModel challenge = new ChallengeModel(p1!, p2, p1ArenaID, p2ArenaID);
			p1.Challenge = challenge;
			p2.Challenge = challenge;

			info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.waiting", challengedPlayer.PlayerName]}");
			challengedPlayer.PrintToChat($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.challenge.request", player!.PlayerName, Config.CommandSettings.ChallengeAcceptCommands.FirstOrDefault("Missing"), Config.CommandSettings.ChallengeDeclineCommands.FirstOrDefault("Missing")]}");

			int GetPlayerInArena(ArenaPlayer player)
			{
				int playerArenaID = Arenas!.ArenaList
					.Where(a => a.Team1?.Any(p => p == player) == true || a.Team2?.Any(p => p == player) == true)
					.Select(a => a.ArenaID)
					.FirstOrDefault(-1);

				return playerArenaID;
			}
		}

		public void Command_AFK(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			ArenaPlayer? arenaPlayer = Arenas?.FindPlayer(player!);

			if (arenaPlayer is null)
				return;

			arenaPlayer.AFK = !arenaPlayer.AFK;

			if (arenaPlayer.AFK)
			{
				player!.ChangeTeam(CsTeam.Spectator);
				player.Clan = $"{Localizer["k4.general.afk"]} |";
				Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
			}
			else
			{
				arenaPlayer.Controller.Clan = $"{Localizer["k4.general.waiting"]} |";
				Utilities.SetStateChanged(arenaPlayer.Controller, "CCSPlayerController", "m_szClan");
			}

			info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {(arenaPlayer.AFK ? string.Format(Localizer["k4.chat.afk_enabled"], Config.CommandSettings.AFKCommands.FirstOrDefault("Missing")) : Localizer["k4.chat.afk_disabled"])}");
		}

		public void Command_Queue(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			int queuePlace = WaitingArenaPlayers.ToList().FindIndex(p => p.Controller == player);

			if (queuePlace == -1)
			{
				info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.queue_not_in_queue"]}");
				return;
			}

			info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.queue_position", queuePlace + 1]}");
		}

		public void Command_RoundPref(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			Arenas?.FindPlayer(player!)?.ShowRoundPreferenceMenu();
		}

		public void Command_WeaponPref(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			Arenas?.FindPlayer(player!)?.ShowWeaponPreferenceMenu();
		}
	}
}