using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.Ship
{
	/// <summary>
	/// 战舰船体物理断裂引擎 (全面支持多孤岛独立切分与脱落导线彻底清理)
	/// </summary>
	public static class HullSeveranceEngine
	{
		public static void CheckAndSeverDisconnectedClusters(ShipEntity ship)
		{
			if (ship.Grid.ModuleCount == 0) return;

			// 1. 刷新受力拓扑图
			ship.Graph.RebuildGraph(ship.Grid);

			// 2. 获取所有仍与存活动力核心保持物理连通的构件 ID
			var connectedToCore = ship.Graph.GetConnectedComponentsFromPowerSources(ship.Grid);

			// 3. 收集所有与动力核心断开的孤立构件
			var orphanModules = new List<ModuleInstance>();
			foreach (var module in ship.Grid.Modules)
			{
				if (!connectedToCore.Contains(module.InstanceId))
				{
					orphanModules.Add(module);
				}
			}

			if (orphanModules.Count == 0) return;

			// ============================================================
			// 核心算法：对孤立构件进行空间连通域 BFS 聚类 (拆分为多个独立岛屿)
			// ============================================================
			var debrisClusters = ClusterOrphanModules(orphanModules);

			GD.PrintRich($"[color=orange][HullSeverance] ⚠️ 触发结构断裂！共 {orphanModules.Count} 个构件解体，物理切分为 {debrisClusters.Count} 块完全独立的残骸！[/color]");

			// 4. 将所有孤立构件从母舰网格与管线中剥离 (彻底注销关联导线，杜绝幽灵线残留)
			foreach (var orphan in orphanModules)
			{
				ship.Pipeline.RemoveWiresConnectedTo(orphan.InstanceId);
				ship.Grid.RemoveModule(orphan.InstanceId);
			}

			Vector2 shipComWorld = ship.GlobalTransform * ship.PhysicsData.CenterOfMassPixels;

			// 5. 为每一个相互独立的残骸岛屿分别实例化专属的 DebrisEntity
			foreach (var cluster in debrisClusters)
			{
				if (cluster.Count == 0) continue;

				// 计算当前这块残骸独立的质心与质量
				float clusterTotalMass = 0.0f;
				Vector2 clusterMassSum = Vector2.Zero;
				foreach (var m in cluster)
				{
					float mass = m.Definition.Mass;
					Vector2I size = m.GetRotatedSize();
					Vector2 center = (Vector2)m.GridPosition + ((Vector2)size * 0.5f);
					clusterTotalMass += mass;
					clusterMassSum += center * mass;
				}
				Vector2 clusterComGrid = clusterMassSum / Mathf.Max(1.0f, clusterTotalMass);

				// 残骸世界坐标精确对齐自身质心
				Vector2 clusterComPixels = Core.GlobalMetrics.MetersToPixels(clusterComGrid);
				Vector2 debrisWorldPos = ship.GlobalTransform * clusterComPixels;

				// 计算该残骸独立的外向弹射冲量
				Vector2 separationDir = (debrisWorldPos - shipComWorld).Normalized();
				if (separationDir == Vector2.Zero) separationDir = Vector2.Right;

				Vector2 separationImpulse = (separationDir * (float)GD.RandRange(180.0, 260.0)) + 
										   (new Vector2(-separationDir.Y, separationDir.X) * (float)GD.RandRange(-80.0, 80.0));

				var debris = new DebrisEntity
				{
					GlobalPosition = debrisWorldPos,
					Rotation = ship.Rotation
				};

				ship.GetTree().CurrentScene.AddChild(debris);
				debris.Initialize(cluster, clusterComGrid, ship.LinearVelocity, ship.AngularVelocity, separationImpulse, ship);
			}

			// 6. 母舰即时重构质量与质心
			ship.RebuildPhysics();
		}

		/// <summary>
		/// 使用 BFS 空间拓扑扩散算法，将脱落构件按四向相邻关系划分为若干个互不相连的独立集群
		/// </summary>
		private static List<List<ModuleInstance>> ClusterOrphanModules(List<ModuleInstance> orphans)
		{
			var clusters = new List<List<ModuleInstance>>();
			var visited = new HashSet<string>();
			var orphanMap = new Dictionary<string, ModuleInstance>();

			foreach (var m in orphans)
			{
				orphanMap[m.InstanceId] = m;
			}

			// 构建空间网格坐标 -> 构件 ID 的快速检索表
			var cellToOrphanId = new Dictionary<Vector2I, string>();
			foreach (var m in orphans)
			{
				foreach (var cell in m.GetOccupiedGridCells())
				{
					cellToOrphanId[cell] = m.InstanceId;
				}
			}

			foreach (var orphan in orphans)
			{
				if (visited.Contains(orphan.InstanceId)) continue;

				// 发现新岛屿，开始 BFS 扩散
				var currentCluster = new List<ModuleInstance>();
				var queue = new Queue<ModuleInstance>();

				queue.Enqueue(orphan);
				visited.Add(orphan.InstanceId);

				while (queue.Count > 0)
				{
					var curr = queue.Dequeue();
					currentCluster.Add(curr);

					// 探索当前构件所有四向相邻的格子
					foreach (var cell in curr.GetOccupiedGridCells())
					{
						Vector2I[] neighbors = {
							cell + Vector2I.Up,
							cell + Vector2I.Down,
							cell + Vector2I.Left,
							cell + Vector2I.Right
						};

						foreach (var n in neighbors)
						{
							if (cellToOrphanId.TryGetValue(n, out var neighborId))
							{
								if (!visited.Contains(neighborId) && orphanMap.TryGetValue(neighborId, out var neighborMod))
								{
									visited.Add(neighborId);
									queue.Enqueue(neighborMod);
								}
							}
						}
					}
				}

				clusters.Add(currentCluster);
			}

			return clusters;
		}
	}
}
