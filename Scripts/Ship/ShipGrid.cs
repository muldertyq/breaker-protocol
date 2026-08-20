using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.Ship
{
	/// <summary>
	/// 飞船网格管理容器
	/// 支持构件放置、重叠碰撞检测、网格查询与包围盒解算
	/// </summary>
	public class ShipGrid
	{
		// 存储网格单元数据：Key 为战舰局部网格坐标 (X, Y)
		private readonly Dictionary<Vector2I, GridCell> _cells = new();

		// 存储所有已放置的构件实例：Key 为 InstanceId
		private readonly Dictionary<string, ModuleInstance> _modules = new();

		// 实例自增计数器
		private int _instanceCounter = 0;

		/// <summary>
		/// 当前飞船内包含的所有构件实例只读列表
		/// </summary>
		public IReadOnlyCollection<ModuleInstance> Modules => _modules.Values;

		/// <summary>
		/// 当前构件总数量
		/// </summary>
		public int ModuleCount => _modules.Count;

		/// <summary>
		/// 尝试在指定网格位置放置一个构件
		/// </summary>
		/// <param name="definition">构件数据定义</param>
		/// <param name="gridPos">网格左上角坐标</param>
		/// <param name="rotation">旋转方向 (0/1/2/3)</param>
		/// <param name="outInstance">输出成功创建的构件实例</param>
		/// <returns>若无空间重叠则放置成功并返回 true</returns>
		public bool TryPlaceModule(ModuleDataDefinition definition, Vector2I gridPos, int rotation, out ModuleInstance? outInstance)
		{
			outInstance = null;

			// 1. 创建临时实例计算占用网格
			string instanceId = $"inst_{definition.Id}_{++_instanceCounter:D3}";
			var newInstance = new ModuleInstance(instanceId, definition, gridPos, rotation);

			// 2. 检查空间是否已被占用 (防重叠)
			foreach (var cellPos in newInstance.GetOccupiedGridCells())
			{
				if (_cells.TryGetValue(cellPos, out var cell) && cell.IsOccupied)
				{
					GD.PrintErr($"[ShipGrid] 放置失败：网格坐标 ({cellPos.X}, {cellPos.Y}) 已被构件 [{cell.ModuleInstanceId}] 占用！");
					return false;
				}
			}

			// 3. 写入网格数据
			foreach (var cellPos in newInstance.GetOccupiedGridCells())
			{
				_cells[cellPos] = new GridCell { ModuleInstanceId = instanceId };
			}

			_modules[instanceId] = newInstance;
			outInstance = newInstance;

			GD.PrintRich($"[color=green][ShipGrid] 成功放置构件: [{definition.Name}] (ID:{instanceId}) 位于 ({gridPos.X}, {gridPos.Y}), 旋转: {rotation * 90}°[/color]");
			return true;
		}

		/// <summary>
		/// 根据实例 ID 移除构件
		/// </summary>
		public bool RemoveModule(string instanceId)
		{
			if (!_modules.TryGetValue(instanceId, out var instance))
			{
				return false;
			}

			// 清理网格占用
			foreach (var cellPos in instance.GetOccupiedGridCells())
			{
				_cells.Remove(cellPos);
			}

			_modules.Remove(instanceId);
			GD.Print($"[ShipGrid] 已移除构件: ID [{instanceId}]");
			return true;
		}

		/// <summary>
		/// 获取指定网格坐标上的构件实例
		/// </summary>
		public ModuleInstance? GetModuleAt(Vector2I gridPos)
		{
			if (_cells.TryGetValue(gridPos, out var cell) && cell.IsOccupied)
			{
				if (_modules.TryGetValue(cell.ModuleInstanceId, out var instance))
				{
					return instance;
				}
			}
			return null;
		}

		/// <summary>
		/// 获取整艘飞船当前占用的全局网格包围盒 (MinGrid, MaxGrid)
		/// </summary>
		public Rect2I GetGridBounds()
		{
			if (_cells.Count == 0) return new Rect2I(0, 0, 0, 0);

			int minX = int.MaxValue, minY = int.MaxValue;
			int maxX = int.MinValue, maxY = int.MinValue;

			foreach (var pos in _cells.Keys)
			{
				if (pos.X < minX) minX = pos.X;
				if (pos.Y < minY) minY = pos.Y;
				if (pos.X > maxX) maxX = pos.X;
				if (pos.Y > maxY) maxY = pos.Y;
			}

			return new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
		}

		/// <summary>
		/// 清空整艘飞船网格
		/// </summary>
		public void Clear()
		{
			_cells.Clear();
			_modules.Clear();
			_instanceCounter = 0;
		}
	}
}
