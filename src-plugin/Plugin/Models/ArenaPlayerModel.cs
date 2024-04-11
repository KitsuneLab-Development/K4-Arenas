using System.Data;
using System.Runtime.InteropServices;
using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace K4Arenas.Models;

public class ArenaPlayer
{
	//** ? Main */
	private readonly Plugin Plugin;
	public readonly IStringLocalizer Localizer;
	public readonly PluginConfig Config;

	//** ? Player */
	public readonly CCSPlayerController Controller;
	public ChallengeModel? Challenge = null;
	public readonly ulong SteamID;
	public SpawnPoint? SpawnPoint;
	public bool PlayerIsSafe;

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

	public List<RoundType> RoundPreferences = RoundType.RoundTypes.ToList();

	public ArenaPlayer(Plugin plugin, CCSPlayerController playerController)
	{
		Plugin = plugin;
		Localizer = Plugin.Localizer;
		Config = Plugin.Config;

		Controller = playerController;
		SteamID = playerController.SteamID;
		PlayerIsSafe = false;
	}

	public bool IsValid
	{
		get
		{
			return Controller?.IsValid == true && Controller.PlayerPawn?.IsValid == true && Controller.Connected == PlayerConnectedState.PlayerConnected;
		}
	}

	public void SetupWeapons(RoundType roundType)
	{
		if (this.IsValid)
		{
			Controller.RemoveWeapons();

			if (!Config.CompatibilitySettings.CSSSkinchangerKnifeCompatibility)
			{
				PlayerGiveNamedItem(Controller, CsItem.Knife);
			}

			if (roundType.PrimaryWeapon != null)
			{
				PlayerGiveNamedItem(Controller, (CsItem)roundType.PrimaryWeapon);
			}
			else if (roundType.UsePreferredPrimary && roundType.PrimaryPreference != null && WeaponPreferences != null)
			{
				WeaponType primaryPreferenceType = (WeaponType)roundType.PrimaryPreference;
				CsItem? primaryPreference = primaryPreferenceType != WeaponType.Unknown ? WeaponPreferences.GetValueOrDefault(primaryPreferenceType) ?? null : null;

				if (primaryPreference == null)
				{
					primaryPreference = WeaponModel.GetRandomWeapon(primaryPreferenceType);
				}

				PlayerGiveNamedItem(Controller, (CsItem)primaryPreference);
			}

			if (roundType.SecondaryWeapon != null)
			{
				PlayerGiveNamedItem(Controller, (CsItem)roundType.SecondaryWeapon);
			}
			else if (roundType.UsePreferredSecondary && WeaponPreferences != null)
			{
				CsItem? secondaryPreference = WeaponPreferences.GetValueOrDefault(WeaponType.Pistol) ?? null;

				if (secondaryPreference == null)
				{
					secondaryPreference = WeaponModel.GetRandomWeapon(WeaponType.Pistol);
				}

				PlayerGiveNamedItem(Controller, (CsItem)secondaryPreference);
			}

			if (Controller.PlayerPawn.Value != null)
			{
				CCSPlayerPawn playerPawn = Controller.PlayerPawn.Value;

				playerPawn.ArmorValue = roundType.Armor ? 100 : 0;
				Utilities.SetStateChanged(playerPawn, "CCSPlayerPawnBase", "m_ArmorValue");


				new CCSPlayer_ItemServices(playerPawn.ItemServices!.Handle)
				{
					HasHelmet = roundType.Helmet,

				};

				Utilities.SetStateChanged(playerPawn, "CCSPlayer_ItemServices", "m_bHasHelmet");
				Utilities.SetStateChanged(playerPawn, "CBasePlayerPawn", "m_pItemServices");
			}
		}
	}

	public void ShowRoundPreferenceMenu()
	{
		ChatMenu roundPreferenceMenu = new ChatMenu(Localizer["k4.menu.roundpref.title"]);

		foreach (RoundType roundType in RoundType.RoundTypes)
		{
			bool isRoundTypeEnabled = RoundPreferences.Contains(roundType);
			roundPreferenceMenu.AddMenuOption(isRoundTypeEnabled ? Localizer["k4.menu.roundpref.item_enabled", Localizer[roundType.Name]] : Localizer["k4.menu.roundpref.item_disabled", Localizer[roundType.Name]],
				(player, option) =>
				{
					ulong steamID = Controller.SteamID;

					if (isRoundTypeEnabled)
					{
						RoundPreferences.Remove(roundType);
						Controller.PrintToChat($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.round_preferences_removed", Localizer[roundType.Name]]}");
					}
					else
					{
						RoundPreferences.Add(roundType);
						Controller.PrintToChat($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.round_preferences_added", Localizer[roundType.Name]]}");
					}

					Task.Run(() => Plugin.UpdateRoundDatabaseAsync(steamID, roundType.ID, isRoundTypeEnabled));

					ShowRoundPreferenceMenu();
				}
			);
		}

		MenuManager.OpenChatMenu(Controller, roundPreferenceMenu);
	}

	public void ShowWeaponPreferenceMenu()
	{
		ChatMenu weaponPreferenceMenu = new ChatMenu(Localizer["k4.menu.weaponpref.title"]);

		foreach (WeaponType weaponType in Enum.GetValues(typeof(WeaponType)))
		{
			weaponPreferenceMenu.AddMenuOption(Localizer[$"k4.rounds.{weaponType.ToString().ToLower()}"],
				(player, option) =>
				{
					ShowWeaponSubPreferenceMenu(weaponType);
				}
			);
		}

		MenuManager.OpenChatMenu(Controller, weaponPreferenceMenu);
	}

	public void ShowWeaponSubPreferenceMenu(WeaponType weaponType)
	{
		ChatMenu primaryPreferenceMenu = new ChatMenu(Localizer["k4.menu.weaponpref.title"]);

		primaryPreferenceMenu.AddMenuOption(WeaponPreferences[weaponType] is null ? Localizer["k4.menu.weaponpref.item_enabled", Localizer["k4.general.random"]] : Localizer["k4.menu.weaponpref.item_disabled", Localizer["k4.general.random"]],
			(player, option) =>
			{
				WeaponPreferences[weaponType] = CsItem.Snowball;
				Controller.PrintToChat($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.weapon_preferences_added", Localizer["k4.general.random"]]}");

				ulong steamID = Controller.SteamID;
				Task.Run(() => Plugin.SelectWeaponDatabaseAsync(steamID, null, weaponType));

				ShowWeaponSubPreferenceMenu(weaponType);
			}
		);

		List<CsItem> possibleItems = WeaponModel.GetWeaponList(weaponType);
		foreach (CsItem item in possibleItems)
		{
			if (WeaponModel.GetWeaponType(item) != weaponType)
				continue;

			bool isItemEnabled = WeaponPreferences[weaponType] == item;
			primaryPreferenceMenu.AddMenuOption(isItemEnabled ? Localizer["k4.menu.weaponpref.item_enabled", Localizer[item.ToString()]] : Localizer["k4.menu.weaponpref.item_disabled", Localizer[item.ToString()]],
				(player, option) =>
				{
					WeaponPreferences[weaponType] = item;
					Controller.PrintToChat($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.weapon_preferences_added", Localizer[item.ToString()]]}");

					ulong steamID = Controller.SteamID;
					Task.Run(() => Plugin.SelectWeaponDatabaseAsync(steamID, item, weaponType));

					ShowWeaponSubPreferenceMenu(weaponType);
				}
			);
		}

		MenuManager.OpenChatMenu(Controller, primaryPreferenceMenu);
	}

	public static MemoryFunctionVoid<IntPtr, string, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr> GiveNamedItem2Linux = new(@"\x55\x48\x89\xE5\x41\x57\x41\x56\x41\x55\x41\x54\x53\x48\x83\xEC\x18\x48\x89\x7D\xC8\x48\x85\xF6\x74");
	public static MemoryFunctionVoid<IntPtr, string, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr> GiveNamedItem2Windows = new(@"\x48\x83\xEC\x38\x48\xC7\x44\x24\x28\x00\x00\x00\x00\x45\x33\xC9\x45\x33\xC0\xC6\x44\x24\x20\x00\xE8\x2A\x2A\x2A\x2A\x48\x85");
	public void PlayerGiveNamedItem(CCSPlayerController player, CsItem item)
	{
		if (!player.PlayerPawn.IsValid || player.PlayerPawn.Value == null || !player.PlayerPawn.Value.IsValid || player.PlayerPawn.Value.ItemServices == null)
			return;

		var GiveNamedItem2 = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? GiveNamedItem2Linux : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? GiveNamedItem2Windows : null;

		if (!Plugin.Config.CompatibilitySettings.MetamodSkinchanger || GiveNamedItem2 is null)
		{
			player.GiveNamedItem(item);
			return;
		}

		string? itemName = EnumUtils.GetEnumMemberAttributeValue(item);

		if (itemName is null)
			return;

		try
		{
			GiveNamedItem2.Invoke(player.PlayerPawn.Value.ItemServices.Handle, itemName, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
		}
		catch (Exception e)
		{
			Plugin.Logger.LogError("Failed to give named item. It is recommended to disable 'metamod-skinchanger-compatibility' on this server. Error: " + e.Message);
			player.GiveNamedItem(item);
		}
	}
}