using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.Ship.Pipeline
{
	/// <summary>
	/// 全舰内部 PCB 电路管线网络拓扑容器
	/// </summary>
	public class PipelineNetwork
	{
		// 存储所有导线：Key 为 WireId
		private readonly Dictionary<string, PipelineWire> _wires = new();

		// 网格占用加速索引：Key 为 GridPos，Value 为流经该网格的所有 WireId 列表
		private readonly Dictionary<Vector2I, List<string>> _gridWireMap = new();

		private int _wireCounter = 0;

		public IReadOnlyCollection<PipelineWire> Wires => _wires.Values;
		public int WireCount => _wires.Count;

		/// <summary>
		/// 尝试创建并接入一条新导线
		/// </summary>
		public bool TryAddWire(PinInstance srcPin, PinInstance dstPin, ShipGrid grid, out PipelineWire? outWire)
		{
			outWire = null;

			// 1. 兼容性验证
			if (!PinCompatibilityValidator.CanConnect(srcPin, dstPin, out string error))
			{
				GD.PrintErr($"[PipelineNetwork] 连线失败: {error}");
				return false;
			}

			// 2. 曼哈顿 A* 寻路解算路径
			var path = ManhattanRouter.FindPath(srcPin.AbsoluteGridPos, dstPin.AbsoluteGridPos, grid);
			if (path.Count < 2)
			{
				GD.PrintErr("[PipelineNetwork] 连线失败：路径被实体障碍阻断，无法找到正交通路！");
				return false;
			}

			// 3. 生成导线实例
			string wireId = $"wire_{++_wireCounter:D3}";
			var wire = new PipelineWire(
				wireId,
				srcPin.OwnerModuleInstanceId, srcPin.Definition.PinId, srcPin.AbsoluteGridPos,
				dstPin.OwnerModuleInstanceId, dstPin.Definition.PinId, dstPin.AbsoluteGridPos,
				path
			);

			_wires[wireId] = wire;

			// 4. 更新网格索引
			foreach (var pos in path)
			{
				if (!_gridWireMap.ContainsKey(pos))
				{
					_gridWireMap[pos] = new List<string>();
				}
				_gridWireMap[pos].Add(wireId);
			}

			outWire = wire;
			GD.PrintRich($"[color=cyan][PipelineNetwork] PCB 导线已连通: [{wireId}] 从 ({srcPin.AbsoluteGridPos.X},{srcPin.AbsoluteGridPos.Y}) -> ({dstPin.AbsoluteGridPos.X},{dstPin.AbsoluteGridPos.Y})，跨越 {path.Count} 格[/color]");
			return true;
		}

		/// <summary>
		/// 移除指定导线
		/// </summary>
		public bool RemoveWire(string wireId)
		{
			if (!_wires.TryGetValue(wireId, out var wire)) return false;

			foreach (var pos in wire.GridPath)
			{
				if (_gridWireMap.TryGetValue(pos, out var list))
				{
					list.Remove(wireId);
					if (list.Count == 0) _gridWireMap.Remove(pos);
				}
			}

			_wires.Remove(wireId);
			GD.Print($"[PipelineNetwork] 导线已移除: [{wireId}]");
			return true;
		}

		/// <summary>
		/// 当某个构件被摧毁或脱落时，彻底删除所有与该构件相连的导线 (防止幽灵线悬空漂浮)
		/// </summary>
		public void RemoveWiresConnectedTo(string moduleInstanceId)
		{
			var toRemove = new List<string>();
			foreach (var wire in _wires.Values)
			{
				if (wire.SourceModuleId == moduleInstanceId || wire.TargetModuleId == moduleInstanceId)
				{
					toRemove.Add(wire.WireId);
				}
			}

			foreach (var wireId in toRemove)
			{
				RemoveWire(wireId);
			}
		}

		/// <summary>
		/// 当某个网格受到穿甲破坏时，切断流经该网格的所有导线
		/// </summary>
		public List<PipelineWire> SeverWiresAt(Vector2I gridPos)
		{
			var severed = new List<PipelineWire>();
			if (_gridWireMap.TryGetValue(gridPos, out var wireIds))
			{
				foreach (var id in wireIds)
				{
					if (_wires.TryGetValue(id, out var wire))
					{
						wire.DurabilityHp = 0.0f; // 标记断线
						severed.Add(wire);
					}
				}
			}
			return severed;
		}

		/// <summary>
		/// 获取流经指定网格的所有导线
		/// </summary>
		public IEnumerable<PipelineWire> GetWiresAt(Vector2I gridPos)
		{
			if (_gridWireMap.TryGetValue(gridPos, out var list))
			{
				foreach (var id in list)
				{
					if (_wires.TryGetValue(id, out var w)) yield return w;
				}
			}
		}

		/// <summary>
		/// 清空全舰导线
		/// </summary>
		public void Clear()
		{
			_wires.Clear();
			_gridWireMap.Clear();
			_wireCounter = 0;
		}
	}
}
