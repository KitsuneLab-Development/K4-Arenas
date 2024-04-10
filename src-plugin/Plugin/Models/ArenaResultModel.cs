namespace K4Arenas.Models;

public enum ArenaResultType
{
	Win,         // Both players were present in the arena and a winner was determined
	Tie,         // Both players were present in the arena and neither won
	NoOpponent,  // Only one player was present in the arena and they are the "winner"
	Empty        // No players were in the arena
}

public struct ArenaResult
{
	public readonly ArenaResultType ResultType;
	public readonly List<ArenaPlayer>? Winners;
	public readonly List<ArenaPlayer>? Losers;

	public ArenaResult(ArenaResultType resultType, List<ArenaPlayer>? winners, List<ArenaPlayer>? losers)
	{
		ResultType = resultType;
		Winners = winners;
		Losers = losers;
	}
}