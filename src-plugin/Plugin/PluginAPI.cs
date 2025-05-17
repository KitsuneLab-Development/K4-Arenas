namespace K4Arenas
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Capabilities;
	using CounterStrikeSharp.API.Modules.Utils;
	using K4Arenas.Models;
	using K4ArenaSharedApi;

	public sealed partial class Plugin : BasePlugin
	{
		public static PluginCapability<IK4ArenaSharedApi> Capability_SharedAPI { get; } = new("k4-arenas:sharedapi");

		public void Initialize_API()
		{
			Capabilities.RegisterPluginCapability(Capability_SharedAPI, () => new ArenaAPIHandler(this));
		}
	}

	public class ArenaAPIHandler : IK4ArenaSharedApi
	{
		public Plugin plugin { get; set; }
		public ArenaAPIHandler(Plugin plugin)
		{
			this.plugin = plugin;
		}

		public int AddSpecialRound(string name, int teamSize, bool enabledByDefault, Action<List<CCSPlayerController>?, List<CCSPlayerController>?> startFunction, Action<List<CCSPlayerController>?, List<CCSPlayerController>?> endFunction)
		{
			return RoundType.AddSpecialRoundType(name, teamSize, enabledByDefault, startFunction, endFunction);
		}

		public void RemoveSpecialRound(int id)
		{
			RoundType.RemoveSpecialRoundType(id);
		}

		public int GetArenaPlacement(CCSPlayerController player)
		{
			var arenaPlayer = plugin.Arenas?.FindPlayer(player);
			return arenaPlayer is not null ? plugin.GetPlayerArenaID(arenaPlayer) : -1;
		}

		public string GetArenaName(CCSPlayerController player)
		{
			var arenaPlayer = plugin.Arenas?.FindPlayer(player);
			if (arenaPlayer is not null)
			{
				string arenaTag = arenaPlayer.ArenaTag;
				if (arenaTag.EndsWith(" |"))
					arenaTag = arenaTag.Substring(0, arenaTag.Length - 2);

				return arenaTag;
			}
			return string.Empty;
		}

		public bool IsAFK(CCSPlayerController player)
		{
			var arenaPlayer = plugin.Arenas?.FindPlayer(player);
			if (arenaPlayer is not null)
			{
				return arenaPlayer.AFK;
			}
			return false;
		}

		public List<CCSPlayerController> FindOpponents(CCSPlayerController player)
		{
			var arenaOpponents = plugin.Arenas?.FindOpponents(player);
			if (arenaOpponents is not null)
			{
				return arenaOpponents;
			}
			return new List<CCSPlayerController>();
		}

		public void TerminateRoundIfPossible()
		{
			plugin.TerminateRoundIfPossible();
		}

		public void PerformAFKAction(CCSPlayerController player, bool afk)
		{
			ArenaPlayer? arenaPlayer = plugin.Arenas?.FindPlayer(player!);

			if (arenaPlayer is null)
				return;

			arenaPlayer!.AFK = afk;

			if (afk)
			{
				arenaPlayer!.AFK = true;
				player!.ChangeTeam(CsTeam.Spectator);
				arenaPlayer.ArenaTag = $"{plugin.Localizer["k4.general.afk"]} |";

				if (!plugin.Config.CompatibilitySettings.DisableClantags)
				{
					player.Clan = arenaPlayer.ArenaTag;
					Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
				}
			}
			else
			{
				arenaPlayer.ArenaTag = $"{plugin.Localizer["k4.general.waiting"]} |";

				if (!plugin.Config.CompatibilitySettings.DisableClantags)
				{
					arenaPlayer.Controller.Clan = arenaPlayer.ArenaTag;
					Utilities.SetStateChanged(arenaPlayer.Controller, "CCSPlayerController", "m_szClan");
				}
			}
		}
	}
}