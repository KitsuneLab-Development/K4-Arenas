
using CounterStrikeSharp.API;
using Microsoft.Extensions.Logging;

namespace K4Arenas.Models;

public class GameConfig
{
	//** ? Main */
	private readonly Plugin Plugin;

	//** ? Variables */
	private Dictionary<string, string>? ConfigSettings;

	//** ? States */
	public bool IsLoaded => ConfigSettings != null;

	public GameConfig(Plugin plugin)
	{
		Plugin = plugin;

		this.Load();
	}

	public void Apply()
	{
		if (ConfigSettings is null)
			return;

		foreach (var (key, value) in ConfigSettings)
		{
			Server.ExecuteCommand($"{key} {value}");
		}
	}

	private void Load()
	{
		string filePath = Path.Combine(Plugin.ModuleDirectory, "gameconfig.cfg");

		if (!File.Exists(filePath))
		{
			this.Create(filePath);
		}

		try
		{
			ConfigSettings = File.ReadLines(filePath)
				.Select(ParseConfigLine)
				.Where(pair => pair != null)
				.ToDictionary(pair => pair!.Value.Key, pair => pair!.Value.Value);

			Plugin.Logger.LogInformation("Loaded {ConfigCount} configuration settings from '{FilePath}'. You can disable this by setting 'use-predefined-config' to false.",
				ConfigSettings.Count, filePath);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError(ex, "Failed to load configuration settings from '{FilePath}'.", filePath);
		}
	}

	private void Create(string path)
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

		try
		{
			File.WriteAllLines(path, defaultConfigLines);
			Plugin.Logger.LogInformation("Created a default gameconfig.cfg file. Please configure it to your needs at '{0}'", path);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError(ex, "Failed to create default configuration file at {FilePath}.", path);
		}
	}

	private static (string Key, string Value)? ParseConfigLine(string line)
	{
		string trimmedLine = line.Trim();
		if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("//"))
			return null;

		string[] parts = trimmedLine.Split(' ', 2);
		if (parts.Length != 2)
			return null;

		return (Key: parts[0], Value: parts[1]);
	}
}
