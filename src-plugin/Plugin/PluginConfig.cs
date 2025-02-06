namespace K4Arenas
{
	using CounterStrikeSharp.API.Core;
	using K4Arenas.Models;
	using System.Text.Json.Serialization;

	public sealed class PluginConfig : BasePluginConfig
	{
		[JsonPropertyName("use-predefined-config")]
		public bool UsePredefinedConfig { get; set; } = true;

		[JsonPropertyName("database-settings")]
		public DatabaseSettings DatabaseSettings { get; set; } = new DatabaseSettings();

		[JsonPropertyName("command-settings")]
		public CommandSettings CommandSettings { get; set; } = new CommandSettings();

		[JsonPropertyName("round-settings")]
		public List<RoundTypeReader> RoundSettings { get; set; } =
		[
			new() {
				TranslationName = "k4.rounds.rifle",
				TeamSize = 1,
				UsePreferredPrimary = true,
				UsePreferredSecondary = true,
				PrimaryPreference = WeaponType.Rifle,
				Armor = true,
				Helmet = true
			},
			new() {
				TranslationName = "k4.rounds.sniper",
				TeamSize = 1,
				UsePreferredPrimary = true,
				UsePreferredSecondary = true,
				PrimaryPreference = WeaponType.Sniper,
				Armor = true,
				Helmet = true
			},
			new() {
				TranslationName = "k4.rounds.shotgun",
				TeamSize = 1,
				UsePreferredPrimary = true,
				UsePreferredSecondary = true,
				PrimaryPreference = WeaponType.Shotgun,
				Armor = true,
				Helmet = true
			},
			new() {
				TranslationName = "k4.rounds.pistol",
				TeamSize = 1,
				UsePreferredSecondary = true,
				Armor = true,
				Helmet = true
			},
			new() {
				TranslationName = "k4.rounds.scout",
				TeamSize = 1,
				PrimaryWeapon = "weapon_ssg08",
				UsePreferredSecondary = true,
				Armor = true,
				Helmet = true
			},
			new() {
				TranslationName = "k4.rounds.awp",
				TeamSize = 1,
				PrimaryWeapon = "weapon_awp",
				UsePreferredSecondary = true,
				Armor = true,
				Helmet = true
			},
			new() {
				TranslationName = "k4.rounds.deagle",
				TeamSize = 1,
				SecondaryWeapon = "weapon_deagle",
				Armor = false,
				Helmet = false
			},
			new() {
				TranslationName = "k4.rounds.smg",
				TeamSize = 1,
				UsePreferredPrimary = true,
				UsePreferredSecondary = true,
				PrimaryPreference = WeaponType.SMG,
				Armor = true,
				Helmet = true
			},
			new() {
				TranslationName = "k4.rounds.lmg",
				TeamSize = 1,
				UsePreferredPrimary = true,
				UsePreferredSecondary = true,
				PrimaryPreference = WeaponType.LMG,
				Armor = true,
				Helmet = true
			},
			new() {
				TranslationName = "k4.rounds.2vs2",
				TeamSize = 2,
				UsePreferredPrimary = true,
				UsePreferredSecondary = true,
				PrimaryPreference = WeaponType.Unknown,
				Armor = true,
				Helmet = true,
				EnabledByDefault = false
			},
			new() {
				TranslationName = "k4.rounds.3vs3",
				TeamSize = 3,
				UsePreferredPrimary = true,
				UsePreferredSecondary = true,
				PrimaryPreference = WeaponType.Unknown,
				Armor = true,
				Helmet = true,
				EnabledByDefault = false
			},
			new() {
				TranslationName = "k4.rounds.knife",
				TeamSize = 1,
				Armor = false,
				Helmet = false
			}
		];

		[JsonPropertyName("compatibility-settings")]
		public CompatibilitySettings CompatibilitySettings { get; set; } = new CompatibilitySettings();

		[JsonPropertyName("default-weapon-settings")]
		public DefaultWeaponSettings DefaultWeaponSettings { get; set; } = new DefaultWeaponSettings();

		[JsonPropertyName("ConfigVersion")]
		public override int Version { get; set; } = 8;
	}

	public sealed class CompatibilitySettings
	{
		[JsonPropertyName("force-arena-clantags")]
		public bool ForceArenaClantags { get; set; } = false;

		[JsonPropertyName("block-flash-of-not-opponent")]
		public bool BlockFlashOfNotOpponent { get; set; } = false;

		[JsonPropertyName("block-damage-of-not-opponent")]
		public bool BlockDamageOfNotOpponent { get; set; } = false;

		[JsonPropertyName("give-knife-by-default")]
		public bool GiveKnifeByDefault { get; set; } = true;

		[JsonPropertyName("disable-clantags")]
		public bool DisableClantags { get; set; } = false;
	}

	public sealed class CommandSettings
	{
		[JsonPropertyName("gun-pref-commands")]
		public List<string> GunsCommands { get; set; } =
		[
			"guns",
			"gunpref",
			"weaponpref",
		];

		[JsonPropertyName("round-pref-commands")]
		public List<string> RoundsCommands { get; set; } =
		[
			"rounds",
			"roundpref",
		];

		[JsonPropertyName("queue-commands")]
		public List<string> QueueCommands { get; set; } =
		[
			"queue"
		];

		[JsonPropertyName("afk-commands")]
		public List<string> AFKCommands { get; set; } =
		[
			"afk"
		];

		[JsonPropertyName("challenge-commands")]
		public List<string> ChallengeCommands { get; set; } =
		[
			"challenge",
			"duel"
		];

		[JsonPropertyName("challenge-accept-commands")]
		public List<string> ChallengeAcceptCommands { get; set; } =
		[
			"caccept",
			"capprove"
		];

		[JsonPropertyName("challenge-decline-commands")]
		public List<string> ChallengeDeclineCommands { get; set; } =
		[
			"cdecline",
			"cdeny"
		];

		[JsonPropertyName("center-menu-mode")]
		public bool CenterMenuMode { get; set; } = true;

		[JsonPropertyName("center-announce-mode")]
		public bool CenterAnnounceMode { get; set; } = true;

		[JsonPropertyName("freeze-in-center-menu")]
		public bool FreezeInMenu { get; set; } = true;

		[JsonPropertyName("show-menu-credits")]
		public bool ShowMenuCredits { get; set; } = true;
	}

	public sealed class DefaultWeaponSettings
	{
		[JsonPropertyName("default-rifle")]
		public string? DefaultRifle { get; set; } = null;

		[JsonPropertyName("default-sniper")]
		public string? DefaultSniper { get; set; } = null;

		[JsonPropertyName("default-smg")]
		public string? DefaultSMG { get; set; } = null;

		[JsonPropertyName("default-lmg")]
		public string? DefaultLMG { get; set; } = null;

		[JsonPropertyName("default-shotgun")]
		public string? DefaultShotgun { get; set; } = null;

		[JsonPropertyName("default-pistol")]
		public string? DefaultPistol { get; set; } = null;

		[JsonPropertyName("default-round")]
		public string? DefaultRound { get; set; } = "k4.rounds.rifle";
	}

	public sealed class DatabaseSettings
	{
		[JsonPropertyName("host")]
		public string Host { get; set; } = "localhost";

		[JsonPropertyName("username")]
		public string Username { get; set; } = "root";

		[JsonPropertyName("database")]
		public string Database { get; set; } = "database";

		[JsonPropertyName("password")]
		public string Password { get; set; } = "password";

		[JsonPropertyName("port")]
		public int Port { get; set; } = 3306;

		[JsonPropertyName("sslmode")]
		public string Sslmode { get; set; } = "preferred";

		[JsonPropertyName("table-prefix")]
		public string TablePrefix { get; set; } = "";

		[JsonPropertyName("table-purge-days")]
		public int TablePurgeDays { get; set; } = 30;
	}
}