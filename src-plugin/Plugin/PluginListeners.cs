namespace K4Arenas
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Utils;
	using K4Arenas.Models;
	using Microsoft.Extensions.Logging;

	public sealed partial class Plugin : BasePlugin
	{
		public void Initialize_Listeners()
		{
			AddCommandListener("jointeam", ListenerJoinTeam);
		}

		public HookResult ListenerJoinTeam(CCSPlayerController? player, CommandInfo info)
		{
			if (player?.IsValid == true && player.PlayerPawn?.IsValid == true && info.ArgByIndex(1) != "0")
			{
				ArenaPlayer? arenaPlayer = Arenas?.FindPlayer(player);
				if (arenaPlayer != null)
					arenaPlayer.PlayerIsSafe = true;

				if (player.Team != CsTeam.None)
				{
					if (arenaPlayer?.AFK == false && player.Team != CsTeam.Spectator && info.ArgByIndex(1) == "1")
					{
						arenaPlayer!.AFK = true;

						player.Clan = $"{Localizer["k4.general.afk"]} |";
						Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");

						player!.ChangeTeam(CsTeam.Spectator);

						player.PrintToChat($" {Localizer["k4.general.prefix"]} {string.Format(Localizer["k4.chat.afk_enabled"], Config.CommandSettings.AFKCommands.FirstOrDefault("Missing"))}");
						return HookResult.Stop;
					}
					else if (arenaPlayer?.AFK == true && player.Team == CsTeam.Spectator && (info.ArgByIndex(1) == "2" || info.ArgByIndex(1) == "3"))
					{
						arenaPlayer!.AFK = false;

						arenaPlayer.Controller.Clan = $"{Localizer["k4.general.waiting"]} |";
						Utilities.SetStateChanged(arenaPlayer.Controller, "CCSPlayerController", "m_szClan");

						player.PrintToChat($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.afk_disabled"]}");
						return HookResult.Continue;
					}

					player.ExecuteClientCommand("play sounds/ui/weapon_cant_buy.vsnd_c");
					return HookResult.Stop;
				}
			}

			return HookResult.Continue;
		}
	}
}