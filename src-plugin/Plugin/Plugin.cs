namespace K4Arenas
{
    using Microsoft.Extensions.Logging;

    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Core.Attributes;

    using K4Arenas.Models;
    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Modules.Timers;

    [MinimumApiVersion(200)]
    public sealed partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
    {
        //** ? PLUGIN GLOBALS */
        public required PluginConfig Config { get; set; } = new PluginConfig();
        public static readonly Random rng = new Random();

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
        }
    }
}