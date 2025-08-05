using CounterStrikeSharp.API.Modules.Entities.Constants;
using K4ArenaSharedApi;

namespace K4Arenas.Models;

public struct WeaponModel
{
	public static List<CsItem> rifleItems =
	[
		CsItem.AK47,
		CsItem.M4A1S,
		CsItem.M4A1,
		CsItem.GalilAR,
		CsItem.Famas,
		CsItem.SG556,
		CsItem.AUG,
	];

	public static List<CsItem> sniperItems =
	[
		CsItem.AWP,
		CsItem.SSG08,
		CsItem.SCAR20,
		CsItem.G3SG1,
	];

	public static List<CsItem> shotgunItems =
	[
		CsItem.XM1014,
		CsItem.Nova,
		CsItem.MAG7,
		CsItem.SawedOff,
	];

	public static List<CsItem> smgItems =
	[
		CsItem.Mac10,
		CsItem.MP9,
		CsItem.MP7,
		CsItem.P90,
		CsItem.MP5SD,
		CsItem.Bizon,
		CsItem.UMP45,
	];

	public static List<CsItem> lmgItems =
	[
		CsItem.M249,
		CsItem.Negev,
	];

	public static List<CsItem> pistolItems =
	[
		CsItem.Deagle,
		CsItem.Glock,
		CsItem.USPS,
		CsItem.HKP2000,
		CsItem.Elite,
		CsItem.Tec9,
		CsItem.P250,
		CsItem.CZ,
		CsItem.FiveSeven,
		CsItem.Revolver
	];

	public static List<CsItem> GetWeaponList(WeaponType type)
	{
		List<CsItem> allWeapons = GetAllPrimaryWeapons();

		switch (type)
		{
			case WeaponType.Rifle:
				return rifleItems;
			case WeaponType.Sniper:
				return sniperItems;
			case WeaponType.Shotgun:
				return shotgunItems;
			case WeaponType.SMG:
				return smgItems;
			case WeaponType.LMG:
				return lmgItems;
			case WeaponType.Pistol:
				return pistolItems;
			default:
				return allWeapons;
		}
	}

	public static List<CsItem> GetAllPrimaryWeapons()
	{
		List<CsItem> allPrimaryWeapons =
		[
			.. rifleItems,
			.. sniperItems,
			.. shotgunItems,
			.. smgItems,
			.. lmgItems,
		];
		return allPrimaryWeapons;
	}

	public static CsItem GetRandomWeapon(WeaponType type)
	{
		List<CsItem> possibleItems = GetWeaponList(type);
		return possibleItems[Random.Shared.Next(0, possibleItems.Count)];
	}

	public static WeaponType GetWeaponType(CsItem? weapon)
	{
		if (weapon is null)
			return WeaponType.Unknown;

		if (rifleItems.Contains(weapon.Value))
			return WeaponType.Rifle;
		if (sniperItems.Contains(weapon.Value))
			return WeaponType.Sniper;
		if (shotgunItems.Contains(weapon.Value))
			return WeaponType.Shotgun;
		if (smgItems.Contains(weapon.Value))
			return WeaponType.SMG;
		if (lmgItems.Contains(weapon.Value))
			return WeaponType.LMG;
		if (pistolItems.Contains(weapon.Value))
			return WeaponType.Pistol;

		return WeaponType.Unknown;
	}
}