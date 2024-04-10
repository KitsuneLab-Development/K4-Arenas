using K4Arenas.Models;

public class ChallengeModel
{
	//** Player1 */
	public readonly ArenaPlayer Player1;
	public readonly int Player1Placement;

	//** Player2 */
	public readonly ArenaPlayer Player2;
	public readonly int Player2Placement;

	//** State */
	public bool IsAccepted = false;
	public bool IsEnded = false;

	public ChallengeModel(ArenaPlayer player1, ArenaPlayer player2, int p1Placement, int p2Placement)
	{
		Player1 = player1;
		Player1Placement = p1Placement;

		Player2 = player2;
		Player2Placement = p2Placement;
	}
}
