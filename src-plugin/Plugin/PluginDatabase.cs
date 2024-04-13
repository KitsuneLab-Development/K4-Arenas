
using System.Data;
using CounterStrikeSharp.API.Core;
using K4Arenas.Models;
using MySqlConnector;
using Dapper;
using CounterStrikeSharp.API.Modules.Entities.Constants;

namespace K4Arenas;

public sealed partial class Plugin : BasePlugin
{
	public MySqlConnection CreateConnection(PluginConfig config)
	{
		DatabaseSettings _settings = config.DatabaseSettings;

		MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
		{
			Server = _settings.Host,
			UserID = _settings.Username,
			Password = _settings.Password,
			Database = _settings.Database,
			Port = (uint)_settings.Port,
			SslMode = Enum.Parse<MySqlSslMode>(_settings.Sslmode, true),
		};

		return new MySqlConnection(builder.ToString());
	}

	public async Task CreateTableAsync()
	{
		string tablePrefix = Config.DatabaseSettings.TablePrefix;
		string tableQuery = @$"CREATE TABLE IF NOT EXISTS `{tablePrefix}k4-arenas` (
			`steamid64` BIGINT UNIQUE,
			`rifle` INT,
			`sniper` INT,
			`shotgun` INT,
			`smg` INT,
			`lmg` INT,
			`pistol` INT,
			`rounds` VARCHAR(256) NOT NULL,
			`lastseen` TIMESTAMP NOT NULL
		);";

		using MySqlConnection connection = CreateConnection(Config);
		await connection.OpenAsync();

		await connection.ExecuteAsync(tableQuery);
	}

	public async Task LoadPlayerAsync(ulong SteamID)
	{
		string tablePrefix = Config.DatabaseSettings.TablePrefix;

		DefaultWeaponSettings dws = Config.DefaultWeaponSettings;

		string sqlInsertOrUpdate = $@"
				INSERT INTO `{tablePrefix}k4-arenas` (`steamid64`, `lastseen`, `rifle`, `sniper`, `shotgun`, `smg`, `lmg`, `pistol`, `rounds`)
				VALUES (@SteamID, CURRENT_TIMESTAMP, {dws.DefaultRifle}, {dws.DefaultRifle}, {dws.DefaultShotgun}, {dws.DefaultSMG}, {dws.DefaultLMG}, {dws.DefaultPistol}, @Rounds)
				ON DUPLICATE KEY UPDATE `lastseen` = CURRENT_TIMESTAMP;";

		string sqlSelect = $@"
				SELECT `rifle`, `sniper`, `shotgun`, `smg`, `lmg`, `pistol`, `rounds`
				FROM `{tablePrefix}k4-arenas` WHERE `steamid64` = @SteamID;";

		using MySqlConnection connection = CreateConnection(Config);
		await connection.OpenAsync();

		string rounds = string.Join(",", RoundType.RoundTypes.Select(x => x.ID.ToString()));
		await connection.ExecuteAsync(sqlInsertOrUpdate, new { SteamID, Rounds = rounds });

		dynamic? result = await connection.QuerySingleOrDefaultAsync<dynamic>(sqlSelect, new { SteamID });
		if (result != null)
		{
			ArenaPlayer? arenaPlayer = Arenas?.FindPlayer(SteamID);

			if (arenaPlayer == null)
				return;

			arenaPlayer.WeaponPreferences = new Dictionary<WeaponType, CsItem?>
			{
				{ WeaponType.Rifle, (CsItem?)result.rifle },
				{ WeaponType.Sniper, (CsItem?)result.sniper },
				{ WeaponType.Shotgun, (CsItem?)result.shotgun },
				{ WeaponType.SMG, (CsItem?)result.smg },
				{ WeaponType.LMG, (CsItem?)result.lmg },
				{ WeaponType.Pistol, (CsItem?)result.pistol }
			};

			if (!string.IsNullOrEmpty(result.rounds))
			{
				List<int> validRoundIds = new List<int>();
				string[] roundIds = result.rounds.Split(',');
				List<RoundType> roundPreferences = new List<RoundType>();

				foreach (string roundId in roundIds)
				{
					if (int.TryParse(roundId, out int id))
					{
						RoundType? roundType = RoundType.RoundTypes.FirstOrDefault(x => x.ID == id);
						if (roundType != null)
						{
							roundPreferences.Add((RoundType)roundType);
							validRoundIds.Add(id);
						}
					}
				}

				if (validRoundIds.Count != roundIds.Length)
				{
					string validRounds = string.Join(",", validRoundIds);
					string sqlUpdateRounds = $@"
					UPDATE `{tablePrefix}k4-arenas`
					SET `rounds` = @ValidRounds
					WHERE `steamid64` = @SteamID;";

					await connection.ExecuteAsync(sqlUpdateRounds, new { SteamID, ValidRounds = validRounds });
				}

				arenaPlayer.RoundPreferences = roundPreferences;
			}
		}
	}

	public async Task SelectWeaponDatabaseAsync(ulong SteamId, CsItem? Weapon, WeaponType WeaponType)
	{
		string columnName = GetColumnName(WeaponType);

		string query = $@"
        UPDATE `{Config.DatabaseSettings.TablePrefix}k4-arenas`
        SET `{columnName}` = @Weapon
        WHERE `steamid64` = @SteamId;";

		using MySqlConnection connection = CreateConnection(Config);
		await connection.OpenAsync();

		await connection.ExecuteAsync(query, new { Weapon, SteamId });
	}

	public async Task UpdateRoundDatabaseAsync(ulong steamid, int round, bool remove)
	{
		string selectQuery = $@"
        SELECT `rounds`
        FROM `{Config.DatabaseSettings.TablePrefix}k4-arenas`
        WHERE `steamid64` = @SteamId;";

		using (MySqlConnection connection = CreateConnection(Config))
		{
			await connection.OpenAsync();

			string? rounds = await connection.QueryFirstOrDefaultAsync<string>(selectQuery, new { SteamId = steamid });

			if (rounds != null)
			{
				List<int> roundList = !string.IsNullOrEmpty(rounds) ? rounds.Split(',').Select(int.Parse).ToList() : new List<int>();

				if (remove)
				{
					roundList.Remove(round);
				}
				else
				{
					roundList.Add(round);
				}

				string newRounds = string.Join(",", roundList);

				string updateQuery = $@"
                UPDATE `{Config.DatabaseSettings.TablePrefix}k4-arenas`
                SET `rounds` = @NewRounds
                WHERE `steamid64` = @SteamId;";

				await connection.ExecuteAsync(updateQuery, new { NewRounds = newRounds, SteamId = steamid });
			}
		}
	}

	public async Task PurgeDatabaseAsync()
	{
		if (Config.DatabaseSettings.TablePurgeDays <= 0)
			return;

		string query = $@"
        DELETE FROM `{Config.DatabaseSettings.TablePrefix}k4-arenas`
        WHERE `lastseen` < DATE_SUB(NOW(), INTERVAL @PurgeDays DAY);";

		using MySqlConnection connection = CreateConnection(Config);
		await connection.OpenAsync();

		await connection.ExecuteAsync(query, new { PurgeDays = Config.DatabaseSettings.TablePurgeDays });
	}

	public bool IsDatabaseConfigDefault(PluginConfig config)
	{
		DatabaseSettings _settings = config.DatabaseSettings;
		return _settings.Host == "localhost" &&
			_settings.Username == "root" &&
			_settings.Database == "database" &&
			_settings.Password == "password" &&
			_settings.Port == 3306 &&
			_settings.Sslmode == "none" &&
			_settings.TablePrefix == "" &&
			_settings.TablePurgeDays == 30;
	}

	private string GetColumnName(WeaponType weaponType)
	{
		switch (weaponType)
		{
			case WeaponType.Rifle:
				return "rifle";
			case WeaponType.Sniper:
				return "sniper";
			case WeaponType.Shotgun:
				return "shotgun";
			case WeaponType.SMG:
				return "smg";
			case WeaponType.LMG:
				return "lmg";
			case WeaponType.Pistol:
				return "pistol";
			default:
				throw new ArgumentException("Invalid weapon type");
		}
	}
}