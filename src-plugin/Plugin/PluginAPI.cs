namespace K4Arenas
{
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Capabilities;
	using K4Arenas.Models;
	using K4ArenaSharedApi;

	public sealed partial class Plugin : BasePlugin
	{
		public static PluginCapability<IK4ArenaSharedApi> Capability_SharedAPI { get; } = new("k4-arenas:sharedapi");

		public void Initialize_API()
		{
			Capabilities.RegisterPluginCapability(Capability_SharedAPI, () => new ArenaAPIHandler());
		}
	}

	public class ArenaAPIHandler : IK4ArenaSharedApi
	{
		public int AddSpecialRound(string name, int teamSize, bool enabledByDefault, Action<List<CCSPlayerController>?, List<CCSPlayerController>?> startFunction, Action<List<CCSPlayerController>?, List<CCSPlayerController>?> endFunction)
		{
			return RoundType.AddSpecialRoundType(name, teamSize, enabledByDefault, startFunction, endFunction);
		}

		public void RemoveSpecialRound(int id)
		{
			RoundType.RemoveSpecialRoundType(id);
		}
	}
}