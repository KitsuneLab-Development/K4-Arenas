using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;

namespace K4ArenaSharedApi
{
	public enum WeaponType
	{
		Rifle,
		Sniper,
		SMG,
		LMG,
		Shotgun,
		Pistol,
		Unknown
	}

	public interface IK4ArenaSharedApi
	{
		public int AddSpecialRound(string name, int teamSize, bool enabledByDefault, Action<List<CCSPlayerController>?, List<CCSPlayerController>?> startFunction, Action<List<CCSPlayerController>?, List<CCSPlayerController>?> endFunction);
		public void RemoveSpecialRound(int id);
		public int GetArenaPlacement(CCSPlayerController player);
		public string GetArenaName(CCSPlayerController player);
		public bool IsAFK(CCSPlayerController player);
		public List<CCSPlayerController> FindOpponents(CCSPlayerController player);
		public void TerminateRoundIfPossible();
		public void PerformAFKAction(CCSPlayerController player, bool afk);
		public CsItem? GetPlayerWeaponPreference(CCSPlayerController player, WeaponType weaponType);
	}
}
