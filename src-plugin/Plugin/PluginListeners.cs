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
			if (player?.IsValid == true && player.PlayerPawn?.IsValid == true)
			{
				ArenaPlayer? arenaPlayer = Arenas?.FindPlayer(player);
				if (arenaPlayer != null)
					arenaPlayer.PlayerIsSafe = true;

				if (player.Team != CsTeam.None)
				{
					if (arenaPlayer?.AFK == false && player.Team != CsTeam.Spectator && info.ArgByIndex(1) == "1")
					{
						return HookResult.Continue;
					}
					else if (arenaPlayer?.AFK == true && player.Team == CsTeam.Spectator && (info.ArgByIndex(1) == "2" || info.ArgByIndex(1) == "3"))
					{
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