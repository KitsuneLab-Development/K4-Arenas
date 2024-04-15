
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using K4ArenaSharedApi;
using Microsoft.Extensions.Logging;

namespace K4ArenaRoundExample;

[MinimumApiVersion(205)]
public class PluginK4ArenaRoundExample : BasePlugin
{
	public static int RoundTypeID { get; private set; } = -1;
	public override string ModuleName => "K4-Arenas Addon - NameOfRound";
	public override string ModuleAuthor => "YourNameHere";
	public override string ModuleVersion => "1.0.0";

	public static PluginCapability<IK4ArenaSharedApi> Capability_SharedAPI { get; } = new("k4-arenas:sharedapi");
	public override void OnAllPluginsLoaded(bool hotReload)
	{
		IK4ArenaSharedApi? checkAPI = Capability_SharedAPI.Get();

		if (checkAPI != null)
		{
			// This registers the name, team size, start function, and end function for the special round.
			// False is the state of being enabled in the player's round preferences by default
			RoundTypeID = checkAPI.AddSpecialRound("NameOfRound", 1, false, RoundStart, RoundEnd);
		}
		else
			Logger.LogError("Failed to get shared API capability for K4-Arenas.");
	}

	public override void Unload(bool hotReload)
	{
		IK4ArenaSharedApi? checkAPI = Capability_SharedAPI.Get();

		if (checkAPI != null)
		{
			// Remove the round, because we dont wanna see it multiple times in the list if reloaded or so
			checkAPI.RemoveSpecialRound(RoundTypeID);
		}
		else
			Logger.LogError("Failed to get shared API capability for K4-Arenas.");
	}

	public void RoundStart(List<CCSPlayerController>? team1, List<CCSPlayerController>? team2)
	{
		// What to happen with the teams when spawned
	}

	public void RoundEnd(List<CCSPlayerController>? team1, List<CCSPlayerController>? team2)
	{
		// What to happen with the teams when the round is over, and the result is known
		// Usefull for cleaning up shits
	}
}