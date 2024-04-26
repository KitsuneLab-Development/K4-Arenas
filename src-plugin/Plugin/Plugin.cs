namespace K4Arenas
{
    using Microsoft.Extensions.Logging;

    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Core.Attributes;

    using K4Arenas.Models;
    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Modules.Timers;
    using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
    using System.Runtime.InteropServices;

    [MinimumApiVersion(200)]
    public sealed partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
    {
        //** ? PLUGIN GLOBALS */
        public required PluginConfig Config { get; set; } = new PluginConfig();
        public GameConfig? GameConfig { get; set; }
        public static readonly Random rng = new();
        public static MemoryFunctionVoid<IntPtr, string, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr>? GiveNamedItem2;

        public bool IsBetweenRounds = false;

        public void OnConfigParsed(PluginConfig config)
        {
            CheckCommonProblems();

            if (config.Version < Config.Version)
            {
                base.Logger.LogWarning("Configuration version mismatch (Expected: {0} | Current: {1})", this.Config.Version, config.Version);
            }

            //** ? Signature Check */

            GiveNamedItem2 = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? new(@"\x55\x48\x89\xE5\x41\x57\x41\x56\x41\x55\x41\x54\x53\x48\x83\xEC\x18\x48\x89\x7D\xC8\x48\x85\xF6\x74")
                : RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? new(@"\x48\x83\xEC\x38\x48\xC7\x44\x24\x28\x00\x00\x00\x00\x45\x33\xC9\x45\x33\xC0\xC6\x44\x24\x20\x00\xE8\x2A\x2A\x2A\x2A\x48\x85")
                    : null;

            //** ? Load Round Types */

            if (config.RoundSettings.Count > 0)
            {
                RoundType.ClearRoundTypes();
                foreach (RoundTypeReader round in config.RoundSettings)
                {
                    RoundType.AddRoundType(round);
                }
            }
            else
                RoundType.ResetRoundTypes();

            this.Config = config;
        }

        public Queue<ArenaPlayer> WaitingArenaPlayers { get; set; } = new Queue<ArenaPlayer>();
        public Arenas? Arenas { get; set; } = null;

        public CCSGameRules? gameRules = null;
        public Timer? WarmupTimer { get; set; } = null;
        public Timer? ForceClanTimer { get; set; } = null;

        public override void Load(bool hotReload)
        {
            if (Config.UsePredefinedConfig)
            {
                GameConfig = new GameConfig(this);
                GameConfig?.Apply();
            }

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(ModulePath);

            if (!IsDatabaseConfigDefault(Config))
            {
                Task.Run(CreateTableAsync).Wait();
                Task.Run(PurgeDatabaseAsync);
            }
            else
            {
                base.Logger.LogError("Please setup your MySQL database settings in the configuration file!");
                Server.ExecuteCommand($"css_plugins unload {fileNameWithoutExtension}");
                return;
            }

            //** ? Core */

            Initialize_API();
            Initialize_Events();
            Initialize_Commands();
            Initialize_Listeners();

            //** ? Setup */

            if (hotReload)
            {
                if (Arenas is null)
                    Arenas = new Arenas(this);

                gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;

                Utilities.GetPlayers()
                    .Where(p => p.IsValid == true && p.PlayerPawn?.IsValid == true && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected)
                    .ToList()
                    .ForEach(p =>
                    {
                        SetupPlayer(p);
                    });

                Server.ExecuteCommand("mp_restartgame 1");
            }

            //** ? Force Clantags */

            if (Config.CompatibilitySettings.ForceArenaClantags)
            {
                ForceClanTimer = AddTimer(1, () =>
                {
                    if (Arenas is null) return;

                    var validPlayers = Utilities.GetPlayers()
                        .Where(p => p?.IsValid == true && p.PlayerPawn?.IsValid == true && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected);

                    foreach (CCSPlayerController player in validPlayers)
                    {
                        string? requiredTag = GetRequiredTag(player);
                        if (requiredTag != null && player.Clan != requiredTag)
                        {
                            player.Clan = requiredTag;
                            Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
                        }
                    }
                }, TimerFlags.REPEAT);
            }
        }

        public override void Unload(bool hotReload)
        {
            List<ArenaPlayer> players = Utilities.GetPlayers()
                 .Where(p => p.IsValid && p.PlayerPawn?.IsValid == true && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected)
                 .Select(p => Arenas?.FindPlayer(p)!)
                 .Where(p => p != null)
                 .ToList();

            Task.Run(() => SavePlayerPreferencesAsync(players));
        }
    }
}