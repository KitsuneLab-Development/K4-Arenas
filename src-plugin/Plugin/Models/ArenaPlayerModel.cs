using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;
using Menu;
using Menu.Enums;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace K4Arenas.Models;

public class ArenaPlayer
{
	//** ? Main */
	private readonly Plugin Plugin;
	public readonly IStringLocalizer Localizer;
	public readonly PluginConfig Config;

	//** ? Player */
	public readonly CCSPlayerController Controller;
	public readonly ulong SteamID;
	public SpawnPoint? SpawnPoint;
	public bool PlayerIsSafe;
	public ushort MVPs = 0;
	public bool Loaded = false;
	public string ArenaTag = string.Empty;
	public string CenterMessage = string.Empty;

	//** ? Settings */
	public bool AFK = false;
	public Dictionary<WeaponType, CsItem?> WeaponPreferences = new Dictionary<WeaponType, CsItem?>
	{
		{ WeaponType.Rifle, null },
		{ WeaponType.Sniper, null },
		{ WeaponType.SMG, null },
		{ WeaponType.LMG, null },
		{ WeaponType.Shotgun, null },
		{ WeaponType.Pistol, null }
	};

	public List<RoundType> RoundPreferences = [.. RoundType.RoundTypes];

	public ArenaPlayer(Plugin plugin, CCSPlayerController playerController)
	{
		Plugin = plugin;
		Localizer = Plugin.Localizer;
		Config = Plugin.Config;

		Controller = playerController;
		SteamID = playerController.SteamID;
		PlayerIsSafe = playerController.IsBot;
	}

	public bool IsValid
		=> Controller?.IsValid == true && Controller.PlayerPawn?.IsValid == true;

	public bool IsAlive
		=> Controller.PlayerPawn.Value?.Health > 0;

	public void SetupWeapons(RoundType roundType)
	{
		if (!this.IsValid || Controller.PlayerPawn.Value == null)
		{
			Plugin.Logger.LogWarning($"Cannot setup weapons for invalid player or null pawn: {Controller.PlayerName}");
			return;
		}

		Controller.RemoveWeapons();

		if (Config.CompatibilitySettings.GiveKnifeByDefault)
			Controller.GiveNamedItem(CsItem.Knife);

		if (roundType.PrimaryPreference == WeaponType.Unknown) // Warmup or Random round types
		{
			Controller.GiveNamedItem(WeaponModel.GetRandomWeapon(WeaponType.Unknown));
			Controller.GiveNamedItem(WeaponModel.GetRandomWeapon(WeaponType.Pistol));
		}
		else
		{
			if (roundType.PrimaryWeapon != null)
			{
				Controller.GiveNamedItem((CsItem)roundType.PrimaryWeapon);
			}
			else if (roundType.UsePreferredPrimary && roundType.PrimaryPreference != null && WeaponPreferences != null)
			{
				WeaponType primaryPreferenceType = (WeaponType)roundType.PrimaryPreference;
				CsItem? primaryPreference = WeaponPreferences.GetValueOrDefault(primaryPreferenceType) ?? WeaponModel.GetRandomWeapon(primaryPreferenceType);
				Controller.GiveNamedItem((CsItem)primaryPreference);
			}

			if (roundType.SecondaryWeapon != null)
			{
				Controller.GiveNamedItem((CsItem)roundType.SecondaryWeapon);
			}
			else if (roundType.UsePreferredSecondary && WeaponPreferences != null)
			{
				CsItem? secondaryPreference = WeaponPreferences.GetValueOrDefault(WeaponType.Pistol) ?? WeaponModel.GetRandomWeapon(WeaponType.Pistol);
				Controller.GiveNamedItem((CsItem)secondaryPreference);
			}
		}

		Server.NextWorldUpdate(() =>
		{
			if (!this.IsValid)
				return;

			if (Controller.PlayerPawn.Value != null)
			{
				CCSPlayerPawn playerPawn = Controller.PlayerPawn.Value;

				playerPawn.ArmorValue = roundType.Armor ? 100 : 0;
				Utilities.SetStateChanged(playerPawn, "CCSPlayerPawn", "m_ArmorValue");

				if (playerPawn.ItemServices != null)
				{
					CCSPlayer_ItemServices itemService = new CCSPlayer_ItemServices(playerPawn.ItemServices.Handle)
					{
						HasHelmet = roundType.Helmet
					};

					Utilities.SetStateChanged(playerPawn, "CBasePlayerPawn", "m_pItemServices");
				}
				else
				{
					Plugin.Logger.LogWarning($"ItemServices is null for player: {Controller.PlayerName}");
				}
			}
			else
			{
				Plugin.Logger.LogWarning($"PlayerPawn is null for player: {Controller.PlayerName}");
			}
		});
	}

	public void ShowRoundPreferenceMenu()
	{
		if (Plugin.Config.CommandSettings.CenterMenuMode)
		{
			ShowCenterRoundPreferenceMenu();
		}
		else
		{
			ShowChatRoundPreferenceMenu();
		}
	}

	private void ShowChatRoundPreferenceMenu()
	{
		ChatMenu roundPreferenceMenu = new ChatMenu(Localizer.ForPlayer(Controller, "k4.menu.roundpref.title"));
		foreach (RoundType roundType in RoundType.RoundTypes)
		{
			bool isRoundTypeEnabled = RoundPreferences.Contains(roundType);
			roundPreferenceMenu.AddMenuOption(isRoundTypeEnabled ? Localizer.ForPlayer(Controller, "k4.menu.roundpref.item_enabled", Localizer.ForPlayer(Controller, roundType.Name)) : Localizer.ForPlayer(Controller, "k4.menu.roundpref.item_disabled", Localizer.ForPlayer(Controller, roundType.Name)),
				(player, option) =>
				{
					ToggleRoundPreference(roundType);
					Task.Run(SavePlayerPreferencesAsync);
				}
			);
		}

		MenuManager.OpenChatMenu(Controller, roundPreferenceMenu);
	}

	private void ShowCenterRoundPreferenceMenu()
	{
		var items = new List<MenuItem>();
		var defaultValues = new Dictionary<int, object>();

		for (int i = 0; i < RoundType.RoundTypes.Count; i++)
		{
			RoundType roundType = RoundType.RoundTypes[i];
			bool isRoundTypeEnabled = RoundPreferences.Contains(roundType);
			items.Add(new MenuItem(MenuItemType.Bool, new MenuValue($"{Localizer.ForPlayer(Controller, roundType.Name)}: ")));
			defaultValues[i] = isRoundTypeEnabled;
		}

		Plugin.Menu?.ShowScrollableMenu(Controller, Localizer.ForPlayer(Controller, "k4.menu.roundpref.title"), items, (buttons, menu, selected) =>
		{
			menu.RepeatedButtons = false;

			if (buttons == MenuButtons.Back || buttons == MenuButtons.Exit)
			{
				Task.Run(SavePlayerPreferencesAsync);
				return;
			}

			if (selected == null) return;
			if (buttons == MenuButtons.Select)
			{
				RoundType roundType = RoundType.RoundTypes[menu.Option];
				bool newValue = selected.Data[0] == 1;

				if (newValue != RoundPreferences.Contains(roundType))
				{
					ToggleRoundPreference(roundType);
				}
			}
		}, false, Config.CommandSettings.FreezeInMenu, 5, defaultValues, Config.CommandSettings.ShowMenuCredits);
	}

	private void ToggleRoundPreference(RoundType roundType)
	{
		bool isRoundTypeEnabled = RoundPreferences.Contains(roundType);
		if (isRoundTypeEnabled)
		{
			if (RoundPreferences.Count == 1)
			{
				Controller.PrintToChat($" {Localizer.ForPlayer(Controller, "k4.general.prefix")} {Localizer.ForPlayer(Controller, "k4.chat.round_preferences_atleastone")}");
			}
			else
			{
				RoundPreferences.Remove(roundType);
				Controller.PrintToChat($" {Localizer.ForPlayer(Controller, "k4.general.prefix")} {Localizer.ForPlayer(Controller, "k4.chat.round_preferences_removed", Localizer.ForPlayer(Controller, roundType.Name))}");
			}
		}
		else
		{
			RoundPreferences.Add(roundType);
			Controller.PrintToChat($" {Localizer.ForPlayer(Controller, "k4.general.prefix")} {Localizer.ForPlayer(Controller, "k4.chat.round_preferences_added", Localizer.ForPlayer(Controller, roundType.Name))}");
		}
	}

	public void ShowWeaponPreferenceMenu()
	{
		if (Plugin.Config.CommandSettings.CenterMenuMode)
		{
			ShowCenterWeaponPreferenceMenu();
		}
		else
		{
			ShowChatWeaponPreferenceMenu();
		}
	}

	private void ShowChatWeaponPreferenceMenu()
	{
		ChatMenu weaponPreferenceMenu = new ChatMenu(Localizer.ForPlayer(Controller, "k4.menu.weaponpref.title"));
		foreach (WeaponType weaponType in Enum.GetValues(typeof(WeaponType)))
		{
			if (weaponType == WeaponType.Unknown || !IsAllowedWeaponType(weaponType))
				continue;
			weaponPreferenceMenu.AddMenuOption(Localizer.ForPlayer(Controller, $"k4.rounds.{weaponType.ToString().ToLower()}"),
				(player, option) =>
				{
					ShowWeaponSubPreferenceMenu(weaponType);
				}
			);
		}
		MenuManager.OpenChatMenu(Controller, weaponPreferenceMenu);
	}

	private void ShowCenterWeaponPreferenceMenu()
	{
		var items = new List<MenuItem>();
		var values = new Dictionary<int, WeaponType>();
		int count = 0;
		foreach (WeaponType weaponType in Enum.GetValues(typeof(WeaponType)))
		{
			if (weaponType == WeaponType.Unknown || !IsAllowedWeaponType(weaponType))
				continue;
			items.Add(new MenuItem(MenuItemType.Button, [new MenuValue($"{Localizer.ForPlayer(Controller, $"k4.rounds.{weaponType.ToString().ToLower()}")}")]));
			values.Add(count++, weaponType);
		}

		Plugin.Menu?.ShowScrollableMenu(Controller, Localizer.ForPlayer(Controller, "k4.menu.weaponpref.title"), items, (buttons, menu, selected) =>
		{
			menu.RepeatedButtons = false;

			if (buttons == MenuButtons.Back || buttons == MenuButtons.Exit)
			{
				Task.Run(SavePlayerPreferencesAsync);
				return;
			}

			if (selected == null) return;
			if (buttons == MenuButtons.Select)
			{
				WeaponType selectedWeaponType = values[menu.Option];
				ShowWeaponSubPreferenceMenu(selectedWeaponType);
			}
		}, false, Config.CommandSettings.FreezeInMenu, disableDeveloper: Config.CommandSettings.ShowMenuCredits);
	}

	private bool IsAllowedWeaponType(WeaponType weaponType)
	{
		return weaponType switch
		{
			WeaponType.Rifle => Config.AllowedWeaponPreferences.Rifle,
			WeaponType.Sniper => Config.AllowedWeaponPreferences.Sniper,
			WeaponType.SMG => Config.AllowedWeaponPreferences.SMG,
			WeaponType.LMG => Config.AllowedWeaponPreferences.LMG,
			WeaponType.Shotgun => Config.AllowedWeaponPreferences.Shotgun,
			WeaponType.Pistol => Config.AllowedWeaponPreferences.Pistol,
			_ => false
		};
	}

	public void ShowWeaponSubPreferenceMenu(WeaponType weaponType)
	{
		if (Plugin.Config.CommandSettings.CenterMenuMode)
		{
			ShowCenterWeaponSubPreferenceMenu(weaponType);
		}
		else
		{
			ShowChatWeaponSubPreferenceMenu(weaponType);
		}
	}

	private void ShowChatWeaponSubPreferenceMenu(WeaponType weaponType)
	{
		ChatMenu primaryPreferenceMenu = new ChatMenu(Localizer.ForPlayer(Controller, "k4.menu.weaponpref.title"));
		AddWeaponOptions(primaryPreferenceMenu, weaponType);
		MenuManager.OpenChatMenu(Controller, primaryPreferenceMenu);
	}

	private void ShowCenterWeaponSubPreferenceMenu(WeaponType weaponType)
	{
		var items = new List<MenuItem>();
		var defaultValues = new Dictionary<int, object>();

		items.Add(new MenuItem(MenuItemType.Bool, new MenuValue($"{Localizer.ForPlayer(Controller, "k4.general.random")}: ")));
		defaultValues[0] = WeaponPreferences[weaponType] == null;

		List<CsItem> possibleItems = WeaponModel.GetWeaponList(weaponType);
		for (int i = 0; i < possibleItems.Count; i++)
		{
			CsItem item = possibleItems[i];
			if (WeaponModel.GetWeaponType(item) != weaponType)
				continue;
			items.Add(new MenuItem(MenuItemType.Bool, new MenuValue($"{Localizer.ForPlayer(Controller, item.ToString())}: ")));
			defaultValues[i + 1] = WeaponPreferences[weaponType] == item;
		}

		Plugin.Menu?.ShowScrollableMenu(Controller, Localizer.ForPlayer(Controller, "k4.menu.weaponpref.title"), items, (buttons, menu, selected) =>
		{
			switch (buttons)
			{
				case MenuButtons.Select:
					if (selected == null) return;

					SetWeaponPreference(weaponType, menu.Option == 0 ? null : possibleItems[menu.Option - 1]);
					ShowCenterWeaponSubPreferenceMenu(weaponType);
					Task.Run(SavePlayerPreferencesAsync);
					break;
			}
		}, true, Config.CommandSettings.FreezeInMenu, 5, defaultValues, Config.CommandSettings.ShowMenuCredits);
	}

	private void AddWeaponOptions(ChatMenu menu, WeaponType weaponType)
	{
		menu.AddMenuOption(WeaponPreferences[weaponType] is null ? Localizer.ForPlayer(Controller, "k4.menu.weaponpref.item_enabled", Localizer.ForPlayer(Controller, "k4.general.random")) : Localizer.ForPlayer(Controller, "k4.menu.weaponpref.item_disabled", Localizer.ForPlayer(Controller, "k4.general.random")),
			(player, option) =>
			{
				SetWeaponPreference(weaponType, null);
				Task.Run(SavePlayerPreferencesAsync);
			}
		);

		List<CsItem> possibleItems = WeaponModel.GetWeaponList(weaponType);
		foreach (CsItem item in possibleItems)
		{
			if (WeaponModel.GetWeaponType(item) != weaponType)
				continue;

			bool isItemEnabled = WeaponPreferences[weaponType] == item;
			menu.AddMenuOption(isItemEnabled ? Localizer.ForPlayer(Controller, "k4.menu.weaponpref.item_enabled", Localizer.ForPlayer(Controller, item.ToString())) : Localizer.ForPlayer(Controller, "k4.menu.weaponpref.item_disabled", Localizer.ForPlayer(Controller, item.ToString())),
				(player, option) =>
				{
					SetWeaponPreference(weaponType, item);
					Task.Run(SavePlayerPreferencesAsync);
				}
			);
		}
	}

	private void SetWeaponPreference(WeaponType weaponType, CsItem? item)
	{
		WeaponPreferences[weaponType] = item;
		Controller.PrintToChat($" {Localizer.ForPlayer(Controller, "k4.general.prefix")} {Localizer.ForPlayer(Controller, "k4.chat.weapon_preferences_added", Localizer.ForPlayer(Controller, item?.ToString() ?? "k4.general.random"))}");
	}

	public async Task SavePlayerPreferencesAsync()
	{
		if (!Loaded)
			return;

		using MySqlConnection connection = Plugin.CreateConnection(Config);
		await connection.OpenAsync();

		try
		{
			string sqlUpdate = $@"
				UPDATE `{Config.DatabaseSettings.TablePrefix}k4-arenas`
				SET `rifle` = @Rifle, `sniper` = @Sniper, `shotgun` = @Shotgun, `smg` = @SMG, `lmg` = @LMG, `pistol` = @Pistol, `rounds` = @Rounds
				WHERE `steamid64` = @SteamId;";

			var weaponParameters = new
			{
				SteamId = SteamID,
				Rifle = WeaponPreferences.TryGetValue(WeaponType.Rifle, out CsItem? rifle) ? rifle : null,
				Sniper = WeaponPreferences.TryGetValue(WeaponType.Sniper, out CsItem? sniper) ? sniper : null,
				Shotgun = WeaponPreferences.TryGetValue(WeaponType.Shotgun, out CsItem? shotgun) ? shotgun : null,
				SMG = WeaponPreferences.TryGetValue(WeaponType.SMG, out CsItem? smg) ? smg : null,
				LMG = WeaponPreferences.TryGetValue(WeaponType.LMG, out CsItem? lmg) ? lmg : null,
				Pistol = WeaponPreferences.TryGetValue(WeaponType.Pistol, out CsItem? pistol) ? pistol : null,
				Rounds = string.Join(",", RoundPreferences.Select(r => r.ID))
			};

			await connection.ExecuteAsync(sqlUpdate, weaponParameters);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError("Failed to save player preferences: {0}", ex.Message);
			throw;
		}
	}
}