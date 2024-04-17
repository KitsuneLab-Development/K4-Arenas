
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

public class ArenaFinder
{
	private readonly List<SpawnPoint> ctSpawns;
	private readonly List<SpawnPoint> tSpawns;
	private readonly float minTeamDistance;
	private readonly float minEnemyDistance;
	private readonly float maxTeamDistance;

	public ArenaFinder()
	{
		ctSpawns = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist").ToList();
		tSpawns = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist").ToList();

		List<float> spawnTeamDistances = new List<float>();
		List<float> spawnEnemyDistances = new List<float>();

		CalculateDistances(ctSpawns, ctSpawns, spawnTeamDistances);
		CalculateDistances(tSpawns, tSpawns, spawnTeamDistances);

		CalculateDistances(ctSpawns, tSpawns, spawnEnemyDistances);

		minTeamDistance = spawnTeamDistances.Min();
		maxTeamDistance = spawnTeamDistances.Max();
		minEnemyDistance = spawnEnemyDistances.Min();
	}

	public List<Tuple<List<SpawnPoint>, List<SpawnPoint>>> GetSpawnPairs()
	{
		var spawnPairs = new List<Tuple<List<SpawnPoint>, List<SpawnPoint>>>();
		var spawnToRemove = new List<SpawnPoint>();

		foreach (var ctSpawn in ctSpawns.Where(ct => ct.AbsOrigin != null).ToList())
		{
			var closestTSpawn = tSpawns
				.Where(t => t.AbsOrigin != null)
				.Select(t => new { TSpawn = t, Distance = DistanceTo(ctSpawn.AbsOrigin!, t.AbsOrigin!) })
				.Where(t => t.Distance < minEnemyDistance * 1.3)
				.OrderBy(t => t.Distance)
				.FirstOrDefault();

			if (closestTSpawn != null)
			{
				Tuple<List<SpawnPoint>, List<SpawnPoint>>? existingPair = null;

				if (minTeamDistance < minEnemyDistance && minEnemyDistance < maxTeamDistance)
				{
					existingPair = FindExistingPairForSpawn(spawnPairs, ctSpawn, closestTSpawn.TSpawn);
				}

				if (existingPair != null)
				{
					existingPair.Item1.Add(ctSpawn);
					existingPair.Item2.Add(closestTSpawn.TSpawn);
				}
				else
				{
					spawnPairs.Add(Tuple.Create(new List<SpawnPoint> { ctSpawn }, new List<SpawnPoint> { closestTSpawn.TSpawn }));
				}

				tSpawns.Remove(closestTSpawn.TSpawn);
				spawnToRemove.Add(ctSpawn);
			}
		}

		foreach (var spawn in spawnToRemove)
		{
			ctSpawns.Remove(spawn);
		}

		spawnToRemove.Clear();
		var spawnLeftovers = new List<SpawnPoint>(tSpawns.Concat(ctSpawns));

		foreach (var spawn in spawnLeftovers)
		{
			var existingPair = FindExistingPairSoloForSpawn(spawnPairs, spawn);

			if (existingPair != null)
			{
				if (existingPair.Value.ct)
				{
					existingPair.Value.Item1.Add(spawn);
				}
				else
				{
					existingPair.Value.Item2.Add(spawn);
				}

				spawnToRemove.Add(spawn);
			}
		}

		foreach (var spawn in spawnToRemove)
		{
			spawnLeftovers.Remove(spawn);
		}

		return spawnPairs;
	}

	private (List<SpawnPoint>, List<SpawnPoint>, bool ct)? FindExistingPairSoloForSpawn(List<Tuple<List<SpawnPoint>, List<SpawnPoint>>> pairs, SpawnPoint spawnToCheck)
	{
		foreach (var pair in pairs)
		{
			var ctDistance = DistanceTo(pair.Item1[0].AbsOrigin!, spawnToCheck.AbsOrigin!);

			if (ctDistance < minEnemyDistance)
			{
				return (pair.Item1, pair.Item2, true);
			}

			var tDistance = DistanceTo(pair.Item2[0].AbsOrigin!, spawnToCheck.AbsOrigin!);

			if (tDistance < minEnemyDistance)
			{
				return (pair.Item1, pair.Item2, false);
			}
		}

		return null;
	}

	private Tuple<List<SpawnPoint>, List<SpawnPoint>>? FindExistingPairForSpawn(List<Tuple<List<SpawnPoint>, List<SpawnPoint>>> pairs, SpawnPoint ctSpawn, SpawnPoint tSpawn)
	{
		foreach (var pair in pairs)
		{
			var ctDistance = DistanceTo(pair.Item1[0].AbsOrigin!, ctSpawn.AbsOrigin!);
			var tDistance = DistanceTo(pair.Item2[0].AbsOrigin!, tSpawn.AbsOrigin!);

			if (ctDistance < minEnemyDistance && tDistance < minEnemyDistance)
			{
				return pair;
			}
		}

		return null;
	}

	private void CalculateDistances(List<SpawnPoint> sourceSpawns, List<SpawnPoint> targetSpawns, List<float> distances)
	{
		foreach (SpawnPoint sourceSpawn in sourceSpawns)
		{
			if (sourceSpawn.AbsOrigin == null) continue;

			foreach (SpawnPoint targetSpawn in targetSpawns)
			{
				if (targetSpawn.AbsOrigin == null || (sourceSpawns == targetSpawns && sourceSpawn == targetSpawn))
					continue;

				float distance = DistanceTo(sourceSpawn.AbsOrigin, targetSpawn.AbsOrigin);
				distances.Add(distance);
			}
		}
	}

	private float DistanceTo(Vector a, Vector b)
	{
		return (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
	}
}
