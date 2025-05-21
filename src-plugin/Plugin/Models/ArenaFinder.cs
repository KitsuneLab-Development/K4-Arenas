using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using K4Arenas;
using Microsoft.Extensions.Logging;

public class ArenaFinder
{
	public Plugin Plugin;

	private readonly List<SpawnPoint> ctSpawns;
	private readonly List<SpawnPoint> tSpawns;

	private readonly List<CInfoTeleportDestination> teleportDestinations;

	// ** FINE TUNE LOGIC ** //
	private static readonly float MERGE_THRESHOLD = 1.5f;
	private static readonly float FACTOR = 1.1f;

	public ArenaFinder(Plugin plugin)
	{
		Plugin = plugin;
		ctSpawns = [.. Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist")];
		tSpawns = [.. Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist")];
		teleportDestinations = [.. Utilities.FindAllEntitiesByDesignerName<CInfoTeleportDestination>("info_teleport_destination")];

		if (teleportDestinations.Count > 0)
		{
			Plugin.Logger.LogInformation("Detected {0} teleport destination(s) on map {1}. Switching to CYBERSHOKE compatibility.", teleportDestinations.Count, Server.MapName);
		}
		else
		{
			if (ctSpawns.Count == 0 || tSpawns.Count == 0)
			{
				Plugin.Logger.LogCritical("No spawn points detected on map: " + Server.MapName);
			}
			else
			{
				Plugin.Logger.LogDebug($"Detected {ctSpawns.Count} CT spawns and {tSpawns.Count} T spawns on map {Server.MapName}");
			}
		}
	}

	public List<Tuple<List<SpawnPoint>, List<SpawnPoint>>> GetArenaPairs()
	{
		List<Tuple<List<SpawnPoint>, List<SpawnPoint>>> spawns;
		if (teleportDestinations.Count > 0)
		{
			spawns = ReplaceTeleportSpawns();
		}
		else
		{
			spawns = GetSpawnPairsUsingEnemyPairing();
		}

		Plugin.Logger.LogInformation("Successfully setup {0} arena(s) on map {1}!", spawns.Count, Server.MapName);

		if (spawns.Count > 0)
		{
			var maxPairSize = spawns
				.Select(pair => Math.Min(pair.Item1.Count, pair.Item2.Count))
				.Max();

			Plugin.Logger.LogInformation("Supported arena modes: {0}", maxPairSize > 1 ? $"1v1-{maxPairSize}v{maxPairSize}" : "1v1");
		}
		else
		{
			Plugin.Logger.LogWarning("No arenas were created. Players will not be able to spawn.");
		}
		return spawns;
	}

	private List<Tuple<List<CInfoTeleportDestination>, List<CInfoTeleportDestination>>> GetTeleportArenaPairs()
	{
		if (teleportDestinations.Count == 0)
		{
			Plugin.Logger.LogWarning("No teleport destinations found.");
			return [];
		}

		var unpaired = new HashSet<CInfoTeleportDestination>(teleportDestinations);
		var pairs = new List<Tuple<List<CInfoTeleportDestination>, List<CInfoTeleportDestination>>>();

		while (unpaired.Count >= 2)
		{
			var t1 = unpaired.First();
			unpaired.Remove(t1);

			var closest = unpaired.OrderBy(t => DistanceTo(t1.AbsOrigin!, t.AbsOrigin!)).FirstOrDefault();
			if (closest != null)
			{
				unpaired.Remove(closest);
				pairs.Add(Tuple.Create(new List<CInfoTeleportDestination> { t1 }, new List<CInfoTeleportDestination> { closest }));
			}
		}

		return pairs;
	}

	public List<Tuple<List<SpawnPoint>, List<SpawnPoint>>> ReplaceTeleportSpawns()
	{
		ctSpawns.ForEach(s => s.AcceptInput("SetDisabled"));
		tSpawns.ForEach(s => s.AcceptInput("SetDisabled"));

		ctSpawns.Clear();
		tSpawns.Clear();

		var teleportPairs = GetTeleportArenaPairs();
		var spawnPairs = new List<Tuple<List<SpawnPoint>, List<SpawnPoint>>>();

		foreach (var pair in teleportPairs)
		{
			var ctSpawnList = new List<SpawnPoint>();
			var tSpawnList = new List<SpawnPoint>();

			foreach (var ctTeleport in pair.Item1)
			{
				ctTeleport.Remove();

				SpawnPoint? entity = Utilities.CreateEntityByName<SpawnPoint>("info_player_counterterrorist");
				if (entity is null)
					continue;

				entity.Teleport(ctTeleport.AbsOrigin, ctTeleport.AbsRotation);
				entity.DispatchSpawn();
				ctSpawnList.Add(entity);
			}

			foreach (var tTeleport in pair.Item2)
			{
				tTeleport.Remove();

				SpawnPoint? entity = Utilities.CreateEntityByName<SpawnPoint>("info_player_terrorist");
				if (entity is null)
					continue;

				entity.Teleport(tTeleport.AbsOrigin, tTeleport.AbsRotation);
				entity.DispatchSpawn();
				tSpawnList.Add(entity);
			}

			spawnPairs.Add(Tuple.Create(ctSpawnList, tSpawnList));
		}

		return spawnPairs;
	}

	public List<Tuple<List<SpawnPoint>, List<SpawnPoint>>> GetSpawnPairsUsingEnemyPairing()
	{
		var allSpawns = new List<SpawnPoint>();
		foreach (var spawn in ctSpawns)
		{
			if (spawn.AbsOrigin != null)
				allSpawns.Add(spawn);
		}

		foreach (var spawn in tSpawns)
		{
			if (spawn.AbsOrigin != null)
				allSpawns.Add(spawn);
		}

		if (allSpawns.Count == 0)
		{
			Plugin.Logger.LogError("No valid spawn points found for grouping.");
			return [];
		}

		var enemyDistances = new List<float>();
		foreach (var spawn in allSpawns)
		{
			var enemySpawns = allSpawns.Where(s => s.DesignerName != spawn.DesignerName && s.AbsOrigin != null).ToList();
			if (enemySpawns.Count == 0)
				continue;

			float minDist = enemySpawns.Min(s => DistanceTo(spawn.AbsOrigin!, s.AbsOrigin!));
			enemyDistances.Add(minDist);
		}

		if (enemyDistances.Count == 0)
		{
			Plugin.Logger.LogWarning("Failed to compute enemy distances.");
			return [];
		}

		enemyDistances.Sort();
		float medianEnemyDistance = enemyDistances.Count % 2 == 1 ?
			enemyDistances[enemyDistances.Count / 2] :
			(enemyDistances[(enemyDistances.Count / 2) - 1] + enemyDistances[enemyDistances.Count / 2]) / 2;

		Plugin.Logger.LogDebug($"Median enemy distance: {medianEnemyDistance:F2}");

		float threshold = medianEnemyDistance * FACTOR;

		Plugin.Logger.LogDebug($"Computed enemy threshold (factor {FACTOR}): {threshold:F2}");

		var uf = new UnionFind<SpawnPoint>(allSpawns);
		var ctList = allSpawns.Where(s => s.DesignerName == "info_player_counterterrorist").ToList();
		var tList = allSpawns.Where(s => s.DesignerName == "info_player_terrorist").ToList();

		foreach (var ct in ctList)
		{
			foreach (var t in tList)
			{
				float d = DistanceTo(ct.AbsOrigin!, t.AbsOrigin!);
				if (d <= threshold)
				{
					uf.Union(ct, t);
				}
			}
		}

		var rawClusters = new List<List<SpawnPoint>>();
		var clustersDict = new Dictionary<SpawnPoint, List<SpawnPoint>>();
		foreach (var spawn in allSpawns)
		{
			SpawnPoint root = uf.Find(spawn);
			if (!clustersDict.ContainsKey(root))
				clustersDict[root] = new List<SpawnPoint>();
			clustersDict[root].Add(spawn);
		}
		rawClusters.AddRange(clustersDict.Values);
		Plugin.Logger.LogDebug($"Found {rawClusters.Count} raw cluster(s).");

		float mergeThreshold = threshold * MERGE_THRESHOLD;
		var mergedClusters = MergeClusters(rawClusters, mergeThreshold);
		Plugin.Logger.LogDebug($"After merging, {mergedClusters.Count} cluster(s) remain.");

		var arenaPairs = new List<Tuple<List<SpawnPoint>, List<SpawnPoint>>>();
		int arenaIndex = 0;
		foreach (var cluster in mergedClusters)
		{
			var clusterCT = cluster.Where(s => s.DesignerName == "info_player_counterterrorist").ToList();
			var clusterT = cluster.Where(s => s.DesignerName == "info_player_terrorist").ToList();
			if (clusterCT.Count > 0 && clusterT.Count > 0)
			{
				arenaPairs.Add(Tuple.Create(clusterCT, clusterT));
				Plugin.Logger.LogDebug($"Arena {++arenaIndex}: CT Spawns: {clusterCT.Count}, T Spawns: {clusterT.Count}.");
			}
			else
			{
				Plugin.Logger.LogDebug($"Cluster discarded (only {(clusterCT.Count > 0 ? "CT" : "T")} spawns present).");
			}
		}

		// Fallback: if no valid arenas were found, create a single arena with all spawns
		if (arenaPairs.Count == 0 && ctList.Count > 0 && tList.Count > 0)
		{
			Plugin.Logger.LogWarning("No suitable arenas found with standard clustering. Using fallback mode with all spawns.");
			arenaPairs.Add(Tuple.Create(ctList, tList));
			Plugin.Logger.LogDebug($"Fallback Arena: CT Spawns: {ctList.Count}, T Spawns: {tList.Count}.");
		}

		return arenaPairs;
	}

	private static float DistanceTo(Vector a, Vector b)
	{
		return (float)Math.Sqrt(Math.Pow(a.X - b.X, 2) +
								Math.Pow(a.Y - b.Y, 2) +
								Math.Pow(a.Z - b.Z, 2));
	}

	private static Vector ComputeCentroid(List<SpawnPoint> cluster)
	{
		float sumX = 0, sumY = 0, sumZ = 0;
		int count = cluster.Count;
		foreach (var s in cluster)
		{
			sumX += s.AbsOrigin!.X;
			sumY += s.AbsOrigin!.Y;
			sumZ += s.AbsOrigin!.Z;
		}
		return new Vector(sumX / count, sumY / count, sumZ / count);
	}

	private static List<List<SpawnPoint>> MergeClusters(List<List<SpawnPoint>> clusters, float mergeThreshold)
	{
		bool merged;
		do
		{
			merged = false;
			for (int i = 0; i < clusters.Count; i++)
			{
				for (int j = i + 1; j < clusters.Count; j++)
				{
					Vector centroid1 = ComputeCentroid(clusters[i]);
					Vector centroid2 = ComputeCentroid(clusters[j]);
					if (DistanceTo(centroid1, centroid2) < mergeThreshold)
					{
						clusters[i].AddRange(clusters[j]);
						clusters.RemoveAt(j);
						merged = true;
						break;
					}
				}
				if (merged)
					break;
			}
		} while (merged);
		return clusters;
	}

	private class UnionFind<T> where T : notnull
	{
		private readonly Dictionary<T, T> parent;

		public UnionFind(IEnumerable<T> items)
		{
			parent = [];
			foreach (T item in items)
			{
				parent[item] = item;
			}
		}

		public T Find(T item)
		{
			if (!parent.TryGetValue(item, out T? value))
				throw new ArgumentException("Item not found in union-find structure.");

			if (!item.Equals(value))
			{
				parent[item] = Find(value);
			}
			return parent[item];
		}

		public void Union(T item1, T item2)
		{
			T root1 = Find(item1);
			T root2 = Find(item2);

			if (!root1.Equals(root2))
			{
				parent[root2] = root1;
			}
		}
	}
}
