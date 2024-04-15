using CounterStrikeSharp.API.Core;

namespace K4ArenaSharedApi
{
	public interface IK4ArenaSharedApi
	{
		public int AddSpecialRound(string name, int teamSize, bool enabledByDefault, Action<List<CCSPlayerController>?, List<CCSPlayerController>?> startFunction, Action<List<CCSPlayerController>?, List<CCSPlayerController>?> endFunction);
		public void RemoveSpecialRound(int id);
	}
}
