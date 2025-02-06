using System.Runtime.Serialization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using K4Arenas.Models;

namespace K4Arenas.Models
{
	public struct RoundType(string name, int teamSize, CsItem? primary, CsItem? secondary, bool usePreferredPrimary = false, WeaponType? primaryPreference = null, bool usePreferredSecondary = false, bool armor = true, bool helmet = true, bool enabledByDefaultAction = true, Action<List<CCSPlayerController>?, List<CCSPlayerController>?>? startFunction = null, Action<List<CCSPlayerController>?, List<CCSPlayerController>?>? endFunction = null)
	{
		private static int nextID = 0;

		public readonly int ID = nextID++;
		public readonly string Name = name;
		public readonly CsItem? PrimaryWeapon = primary;
		public readonly WeaponType? PrimaryPreference = primaryPreference;
		public readonly CsItem? SecondaryWeapon = secondary;
		public readonly bool UsePreferredPrimary = usePreferredPrimary;
		public readonly bool UsePreferredSecondary = usePreferredSecondary;
		public readonly bool Armor = armor;
		public readonly bool Helmet = helmet;
		public readonly int TeamSize = teamSize;
		public readonly bool EnabledByDefault = enabledByDefaultAction;
		public readonly Action<List<CCSPlayerController>?, List<CCSPlayerController>?>? StartFunction = startFunction;
		public readonly Action<List<CCSPlayerController>?, List<CCSPlayerController>?>? EndFunction = endFunction;
		public static readonly RoundType Rifle = new("k4.rounds.rifle", 1, null, null, true, WeaponType.Rifle, true);
		public static readonly RoundType Sniper = new("k4.rounds.sniper", 1, null, null, true, WeaponType.Sniper, true);
		public static readonly RoundType Shotgun = new("k4.rounds.shotgun", 1, null, null, true, WeaponType.Shotgun, true);
		public static readonly RoundType Pistol = new("k4.rounds.pistol", 1, null, null, false, null, true);
		public static readonly RoundType Scout = new("k4.rounds.scout", 1, CsItem.Scout, null, false, null, true);
		public static readonly RoundType AWP = new("k4.rounds.awp", 1, CsItem.AWP, null, false, null, true);
		public static readonly RoundType Deagle = new("k4.rounds.deagle", 1, null, CsItem.Deagle, false, null, false);
		public static readonly RoundType SMG = new("k4.rounds.smg", 1, null, null, true, WeaponType.SMG, true);
		public static readonly RoundType LMG = new("k4.rounds.lmg", 1, null, null, true, WeaponType.LMG, true);
		public static readonly RoundType TwoVSTwo = new("k4.rounds.2vs2", 2, null, null, true, WeaponType.Unknown, true);
		public static readonly RoundType ThreeVSThree = new("k4.rounds.3vs3", 3, null, null, true, WeaponType.Unknown, true);
		public static readonly RoundType Knife = new("k4.rounds.knife", 1, null, null, false, null, false, false, false);

		public static List<RoundType> RoundTypes { get; } = [];

		public static void AddRoundType(RoundTypeReader roundType)
		{
			CsItem? PrimaryPreference = FindEnumValueByEnumMemberValue(roundType.PrimaryWeapon);
			CsItem? SecondaryPreference = FindEnumValueByEnumMemberValue(roundType.SecondaryWeapon);

			RoundTypes.Add(new RoundType(roundType.TranslationName, roundType.TeamSize, PrimaryPreference, SecondaryPreference, roundType.UsePreferredPrimary, roundType.PrimaryPreference, roundType.UsePreferredSecondary, roundType.Armor, roundType.Helmet, roundType.EnabledByDefault));
		}

		public static int AddSpecialRoundType(string name, int teamSize, bool enabledByDefault, Action<List<CCSPlayerController>?, List<CCSPlayerController>?> startFunction, Action<List<CCSPlayerController>?, List<CCSPlayerController>?> endFunction)
		{
			RoundType specialRound = new(name, teamSize, null, null, false, null, false, false, false, enabledByDefault, startFunction, endFunction);
			RoundTypes.Add(specialRound);
			return specialRound.ID;
		}

		public static void RemoveSpecialRoundType(int id)
		{
			RoundTypes.RemoveAll(x => x.ID == id);
		}

		public static void ClearRoundTypes()
		{
			RoundTypes.Clear();
			nextID = 0;
		}

		public static void ResetRoundTypes()
		{
			RoundTypes.Clear();
			RoundTypes.AddRange(
			[
				Rifle,
				Sniper,
				Shotgun,
				Pistol,
				Scout,
				AWP,
				Deagle,
				SMG,
				LMG,
				Knife,
				TwoVSTwo,
				ThreeVSThree
			]);
		}

		public static CsItem? FindEnumValueByEnumMemberValue(string? search)
		{
			if (search is null)
				return null;

			var type = typeof(CsItem);
			foreach (var field in type.GetFields())
			{
				var attribute = field.GetCustomAttributes(typeof(EnumMemberAttribute), false).Cast<EnumMemberAttribute>().FirstOrDefault();
				if (attribute?.Value == search)
				{
					return (CsItem?)field.GetValue(null);
				}
			}
			return null;
		}
	}
}

public class RoundTypeReader
{
	public required string TranslationName { get; set; }
	public int TeamSize { get; set; } = 1;
	public string? PrimaryWeapon { get; set; } = null;
	public string? SecondaryWeapon { get; set; } = null;
	public bool UsePreferredPrimary { get; set; } = false;
	public WeaponType? PrimaryPreference { get; set; } = null;
	public bool UsePreferredSecondary { get; set; } = false;
	public bool Armor { get; set; } = true;
	public bool Helmet { get; set; } = true;
	public bool EnabledByDefault { get; set; } = true;
}