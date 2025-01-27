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
        public Menu.KitsuneMenu Menu { get; private set; } = null!;
        public bool IsBetweenRounds = false;
        public bool HasDatabase = false;

        public void OnConfigParsed(PluginConfig config)
        {
            CheckCommonProblems();

            if (config.Version < Config.Version)
            {
                base.Logger.LogWarning("Configuration version mismatch (Expected: {0} | Current: {1})", this.Config.Version, config.Version);
            }

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

        public override void Load(bool hotReload)
        {
            if (Config.UsePredefinedConfig)
                GameConfig = new GameConfig(this);

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(ModulePath);

            if (!IsDatabaseConfigDefault(Config))
            {
                HasDatabase = true;

                Task.Run(CreateTableAsync).Wait();
                Task.Run(PurgeDatabaseAsync);
            }
            else
            {
                base.Logger.LogError("Please setup your MySQL database settings in the configuration file in order to use the preferences system.");
            }

            Menu = new Menu.KitsuneMenu(this);

            //** ? Core */

            Initialize_API();
            Initialize_Events();
            Initialize_Commands();
            Initialize_Listeners();

            //** ? Setup */

            if (hotReload)
            {
                Arenas ??= new Arenas(this);

                gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;

                Utilities.GetPlayers()
                    .Where(p => p.IsValid == true && p.PlayerPawn?.IsValid == true && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected)
                    .ToList()
                    .ForEach(p =>
                    {
                        SetupPlayer(p);
                    });

                GameConfig?.Apply();

                Server.ExecuteCommand("mp_restartgame 1");
            }

            //** ? Force Clantags */

            if (Config.CompatibilitySettings.ForceArenaClantags)
            {
                AddTimer(1, () =>
                {
                    if (Arenas is null) return;

                    var validPlayers = Utilities.GetPlayers()
                        .Where(p => p?.IsValid == true && p.PlayerPawn?.IsValid == true && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected);

                    foreach (CCSPlayerController player in validPlayers)
                    {
                        string? requiredTag = GetRequiredTag(player);
                        if (requiredTag != null && player.Clan != requiredTag)
                        {
                            var arenaPlayer = Arenas.FindPlayer(player);
                            if (arenaPlayer is null) continue;

                            arenaPlayer.ArenaTag = requiredTag;

                            if (!Config.CompatibilitySettings.DisableClantags)
                            {
                                player.Clan = arenaPlayer.ArenaTag;
                                Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
                            }
                        }
                    }
                }, TimerFlags.REPEAT);
            }
        }
    }
}