namespace K4Arenas;

public class GameConfigLoader
{
    public Dictionary<string, string>? GameConfigSettings { get; private set; } = null;
    public bool ConfigsLoaded { get; private set; } = false;

    public event EventHandler<Dictionary<string, string>>? EventConfigsLoaded;

    private Plugin? _plugin;

    public GameConfigLoader()
    {
    }

    public void Clear()
    {
        ConfigsLoaded = false;
        GameConfigSettings = null;
    }

    void LoadConfigs()
    {
        Clear();
        string configFile = Path.Combine(_plugin!.ModulePath, "../gameconfig.txt");
        Console.WriteLine($"Looking for config file at: {configFile}");
        if (!File.Exists(configFile))
        {
            Console.WriteLine("Config file not found, creating a new one with default values.");
            CreateDefaultConfigFile(configFile);
        }

        GameConfigSettings = File.ReadLines(configFile)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("//"))
            .Select(line =>
            {
                string[] parts = line.Split(' ', 2);
                return (Key: parts[0], Value: parts.Length == 2 ? parts[1] : string.Empty);
            })
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        Console.WriteLine($"Loaded {GameConfigSettings.Count} configuration settings.");
        ConfigsLoaded = true;
        EventConfigsLoaded?.Invoke(this, GameConfigSettings!);
    }

    private static void CreateDefaultConfigFile(string path)
    {
        var defaultConfigLines = new List<string>
        {
            "// Changing these might break the gamemode",
            "bot_quota 0",
            "mp_autoteambalance 0",
            "mp_ct_default_primary \"\"",
            "mp_ct_default_secondary \"\"",
            "mp_t_default_primary \"\"",
            "mp_t_default_secondary \"\"",
            "mp_halftime 0",
            "mp_join_grace_time 0",
            "mp_match_can_clinch 0",
            "mp_respawn_immunitytime 0",
            "",
            "// Essential for better player experience",
            "mp_autokick 0",
            "mp_warmuptime 0",
            "mp_maxmoney 0",
            "mp_teamcashawards 0",
            "mp_playercashawards 0",
            "sv_disable_radar 1",
            "sv_ignoregrenaderadio 1",
            "",
            "// You can change whatever you want here, up to your preferences",
            "mp_freezetime 3",
            "mp_forcecamera 0",
            "mp_endmatch_votenextmap 0",
            "mp_match_end_changelevel 1",
            "mp_match_end_restart 0",
            "mp_maxrounds 0",
            "mp_round_restart_delay 2",
            "sv_allow_votes 0",
            "mp_timelimit 15",
            "mp_win_panel_display_time 5",
            "sv_talk_enemy_dead 1",
            "sv_talk_enemy_living 1",
            "sv_deadtalk 1"
        };

        File.WriteAllLines(path, defaultConfigLines);
        Console.WriteLine("Default config file created with predefined values.");
    }

    public void OnLoad(Plugin plugin)
    {
        _plugin = plugin;
        LoadConfigs();
    }
}