
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

	/***************************
	 *  Example of AFK action  *
	 ***************************/

	// This is the method that will be called when the player is AFK
	// This section can be copied 1:1 to any Anti-AFK plugin as it is due to it has all required methods with the most stable ways
	// Compile this plugin with Arenas API, its not required to have it in the server, but it will be used if available

	public static IK4ArenaSharedApi? SharedAPI_Arena { get; private set; }
	public (bool ArenaFound, bool Checked) ArenaSupport = (false, false);
	public void PerformAFKAction(CCSPlayerController player, bool afk)
	{
		if (!ArenaSupport.Checked) // We do check only once, so we basically cache the result
		{
			string arenaPath = Path.GetFullPath(Path.Combine(ModuleDirectory, "..", "K4-Arenas"));
			ArenaSupport.ArenaFound = Directory.Exists(arenaPath);
			ArenaSupport.Checked = true;
		}

		if (!ArenaSupport.ArenaFound)
		{
			// Arena not found, run your own logics here
			return;
		}

		if (SharedAPI_Arena is null)
		{
			// This section won't be executed if Arena is not found, so wont cause issues from capability not found
			PluginCapability<IK4ArenaSharedApi> Capability_SharedAPI = new("k4-arenas:sharedapi");
			SharedAPI_Arena = Capability_SharedAPI.Get();
		}

		// Arena found, perform the action
		SharedAPI_Arena?.PerformAFKAction(player, afk);
	}
}