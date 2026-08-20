using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.Ship
{
	/// <summary>
	/// 飞船结构拓扑与物理连通图
	/// 负责解算构件间的相邻拓扑关系，并执行 BFS 动力源可达性检查
	/// </summary>
	public class StructuralGraph
	{
		// 邻接表：Key 为 InstanceId，Value 为所有与其物理相邻的构件 InstanceId 集合
		private readonly Dictionary<string, HashSet<string>> _adjacencyList = new();

		/// <summary>
		/// 根据飞船当前网格全量重建结构拓扑连通图
		/// </summary>
		public void RebuildGraph(ShipGrid grid)
		{
			_adjacencyList.Clear();

			// 1. 为所有构件初始化邻接节点
			foreach (var module in grid.Modules)
			{
				_adjacencyList[module.InstanceId] = new HashSet<string>();
			}

			// 四向相邻偏移向量
			Vector2I[] neighborOffsets = { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right };

			// 2. 遍历每个构件占用的所有网格，探测其邻居
			foreach (var module in grid.Modules)
			{
				foreach (var cellPos in module.GetOccupiedGridCells())
				{
					foreach (var offset in neighborOffsets)
					{
						Vector2I neighborPos = cellPos + offset;
						var neighborModule = grid.GetModuleAt(neighborPos);

						// 如果邻居存在且不是自身，建立双向连接
						if (neighborModule != null && neighborModule.InstanceId != module.InstanceId)
						{
							_adjacencyList[module.InstanceId].Add(neighborModule.InstanceId);
							_adjacencyList[neighborModule.InstanceId].Add(module.InstanceId);
						}
					}
				}
			}
		}

		/// <summary>
		/// 获取与指定构件物理相邻的所有构件实例 ID
		/// </summary>
		public IReadOnlyCollection<string> GetNeighbors(string instanceId)
		{
			if (_adjacencyList.TryGetValue(instanceId, out var neighbors))
			{
				return neighbors;
			}
			return System.Array.Empty<string>();
		}

		/// <summary>
		/// 执行 BFS 广度优先搜索，获取所有与存活核心动力源物理连通的构件 ID 集合
		/// </summary>
		/// <param name="grid">飞船网格对象</param>
		/// <returns>所有保持物理连通的构件 InstanceId 集合</returns>
		public HashSet<string> GetConnectedComponentsFromPowerSources(ShipGrid grid)
		{
			HashSet<string> connectedSet = new();
			Queue<string> queue = new();

			// 1. 寻找所有存活的动力核心作为 BFS 起点
			foreach (var module in grid.Modules)
			{
				if (module.Definition.Category == "PowerSource" && !module.IsDestroyed)
				{
					queue.Enqueue(module.InstanceId);
					connectedSet.Add(module.InstanceId);
				}
			}

			// 2. BFS 遍历扩散
			while (queue.Count > 0)
			{
				string currentId = queue.Dequeue();

				if (_adjacencyList.TryGetValue(currentId, out var neighbors))
				{
					foreach (var neighborId in neighbors)
					{
						var neighborModule = grid.Modules;
						if (!connectedSet.Contains(neighborId))
						{
							connectedSet.Add(neighborId);
							queue.Enqueue(neighborId);
						}
					}
				}
			}

			return connectedSet;
		}
	}
}
