namespace K4Arenas.Models
{
	public enum ArenaResultType
	{
		Win,         // Both players were present in the arena and a winner was determined
		Tie,         // Both players were present in the arena and neither won
		NoOpponent,  // Only one player was present in the arena and they are the "winner"
		Empty        // No players were in the arena
	}

	public readonly struct ArenaResult(ArenaResultType resultType, List<ArenaPlayer>? winners, List<ArenaPlayer>? losers)
	{
		public readonly ArenaResultType ResultType = resultType;
		public readonly List<ArenaPlayer>? Winners = winners;
		public readonly List<ArenaPlayer>? Losers = losers;
	}
}