
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Cvars;
using K4ArenaSharedApi;
using Microsoft.Extensions.Logging;

namespace K4ArenaBots;

[MinimumApiVersion(284)]
public class Plugin : BasePlugin
{
	public override string ModuleName => "K4-Arenas Addon - Bots Support";
	public override string ModuleDescription => "Adds a bot in empty arena if there is no opponent.";
	public override string ModuleAuthor => "Cruze";
	public override string ModuleVersion => "1.0.0";

	public static PluginCapability<IK4ArenaSharedApi> Capability_SharedAPI { get; } = new("k4-arenas:sharedapi");
	public static IK4ArenaSharedApi? SharedAPI_Arena { get; private set; } = null;

	private CCSGameRules? gameRules = null;
	private string botQuotaMode = "normal";

    public override void OnAllPluginsLoaded(bool hotReload)
    {
		SharedAPI_Arena = Capability_SharedAPI.Get();
    }

    public override void Load(bool hotReload)
	{
		if(hotReload)
		{
			gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;

			SharedAPI_Arena = Capability_SharedAPI.Get();

			var quota = ConVar.Find("bot_quota_mode");
			botQuotaMode = quota?.StringValue ?? "normal";
		}

		RegisterListener<Listeners.OnMapStart>((mapName) =>
		{
			DeleteOldGameConfig();
			AddTimer(0.1f, () =>
			{
				gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;

				var quota = ConVar.Find("bot_quota_mode");
				botQuotaMode = quota?.StringValue ?? "normal";
			});
		});

		RegisterListener<Listeners.OnMapEnd>(() =>
		{
			gameRules = null;
		});

		RegisterEventHandler((EventPlayerSpawn @event, GameEventInfo info) =>
		{
			var player = @event.Userid;

			if(player == null || !player.IsValid || player.IsBot || player.IsHLTV || SharedAPI_Arena == null || SharedAPI_Arena.IsAFK(player))
				return HookResult.Continue;

			SpawnBotInEmptyArena(player);

			return HookResult.Continue;
		});

		RegisterEventHandler((EventPlayerDisconnect @event, GameEventInfo info) =>
		{
			var player = @event.Userid;

			if(player == null || !player.IsValid || !player.IsBot)
				return HookResult.Continue;

			info.DontBroadcast = true;
			return HookResult.Continue;
		}, HookMode.Pre);

		RegisterEventHandler((EventRoundPrestart @event, GameEventInfo info) =>
		{
			if(gameRules == null || gameRules.WarmupPeriod || SharedAPI_Arena == null)
			{
				Server.ExecuteCommand($"bot_prefix \"WARMUP\"");
				return HookResult.Continue;
			}

			(var players, var bots) = GetPlayers();

			if(!bots.Any() || bots.First() == null)
			{
				Server.ExecuteCommand($"bot_prefix \"\"");
				return HookResult.Continue;
			}

			var bot = bots.First();
			var arenaS = SharedAPI_Arena?.GetArenaName(bot) + " |" ?? "";

			Server.ExecuteCommand($"bot_prefix {arenaS}");
			return HookResult.Continue;
		}, HookMode.Post);

		RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
		{
			SpawnBotInEmptyArena(null, true);
			return HookResult.Continue;
		});
	}

	private void SpawnBotInEmptyArena(CCSPlayerController? play, bool roundEnd = false)
	{
		if(gameRules == null || gameRules.WarmupPeriod || SharedAPI_Arena == null)
			return;

		(var players, var bots) = GetPlayers();

		Logger.LogInformation($"Players: {players.Count()} | Bots: {bots.Count()}");

		if(players.Count() % 2 == 0)
		{
			Logger.LogInformation($"Even players, no need to spawn bot.");
			if(bots.Count() > 0)
				Server.ExecuteCommand("bot_quota 0");
			return;
		}

		if(play != null && SharedAPI_Arena.FindOpponents(play).Count() > 0)
			return;

		if(!bots.Any() || bots.Count() > 1)
		{
			if(botQuotaMode == "fill")
				Server.ExecuteCommand("bot_quota 2");
			else
				Server.ExecuteCommand("bot_quota 1");

			Logger.LogInformation($"Spawning bot in empty arena.");
		}

		if(roundEnd)
			return;

		AddTimer(0.1f, () =>
		{
			players = new();
			bots = new();
			(players, bots) = GetPlayers();
			if(!bots.Any() || bots.First() == null)
				return;

			SharedAPI_Arena.TerminateRoundIfPossible();
		});
	}

	private void DeleteOldGameConfig()
	{
		// If bot_quota 0 exists in gameconfig.cfg, unlimited round restart will be there so we need to delete it & create a fresh config without it.
		// Creating of new file is handled by main plugin with the update.

		string filePath = Path.Combine(Server.GameDirectory, "csgo/addons/counterstrikesharp/plugins", "K4-Arenas", "gameconfig.cfg");

		if(File.Exists(filePath))
		{
			if(File.ReadAllText(filePath).Contains("bot_quota "))
			{
				Logger.LogWarning($"Old gameconfig file found, deleting it.");
				File.Delete(filePath);
			}
		}
	}

	private (List<CCSPlayerController>, List<CCSPlayerController>) GetPlayers()
	{
		var players = new List<CCSPlayerController>();
		var bots = new List<CCSPlayerController>();

		for (int i = 0; i < Server.MaxPlayers; i++)
		{
			var controller = Utilities.GetPlayerFromSlot(i);

			if (controller == null || !controller.IsValid || controller.IsHLTV)
				continue;

			if(controller.IsBot)
			{
				bots.Add(controller);
				continue;
			}

			if(controller.Connected == PlayerConnectedState.PlayerConnected)
				players.Add(controller);
		}
		return (players, bots);
	}
}