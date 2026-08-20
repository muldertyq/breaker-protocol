using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.Ship
{
	/// <summary>
	/// 构件在战舰网格上的运行时实例
	/// </summary>
	public class ModuleInstance
	{
		/// <summary>
		/// 运行时全局唯一实例 ID（如 "inst_hf_railgun_001"）
		/// </summary>
		public string InstanceId { get; }

		/// <summary>
		/// 对应的数据定义模板
		/// </summary>
		public ModuleDataDefinition Definition { get; }

		/// <summary>
		/// 构件左上角锚定在飞船网格上的基准坐标
		/// </summary>
		public Vector2I GridPosition { get; set; }

		/// <summary>
		/// 旋转朝向：0 = 0度, 1 = 90度顺时针, 2 = 180度, 3 = 270度顺时针
		/// </summary>
		public int Rotation { get; set; }

		/// <summary>
		/// 当前剩余结构耐久 (HP)
		/// </summary>
		public float CurrentHp { get; set; }

		/// <summary>
		/// 最大结构耐久
		/// </summary>
		public float MaxHp => Definition.BaseHp;

		/// <summary>
		/// 构件是否已被完全摧毁
		/// </summary>
		public bool IsDestroyed => CurrentHp <= 0.0f;

		public ModuleInstance(string instanceId, ModuleDataDefinition definition, Vector2I gridPosition, int rotation = 0)
		{
			InstanceId = instanceId;
			Definition = definition;
			GridPosition = gridPosition;
			Rotation = Mathf.PosMod(rotation, 4);
			CurrentHp = definition.BaseHp;
		}

		/// <summary>
		/// 获取旋转后的有效宽高尺寸 (GU)
		/// </summary>
		public Vector2I GetRotatedSize()
		{
			// 90度或270度时，宽高对调
			return (Rotation % 2 == 1) 
				? new Vector2I(Definition.Height, Definition.Width) 
				: new Vector2I(Definition.Width, Definition.Height);
		}

		/// <summary>
		/// 计算该构件实例在战舰网格上实际占用的所有绝对网格坐标集合
		/// </summary>
		public IEnumerable<Vector2I> GetOccupiedGridCells()
		{
			Vector2I size = GetRotatedSize();
			for (int x = 0; x < size.X; x++)
			{
				for (int y = 0; y < size.Y; y++)
				{
					yield return new Vector2I(GridPosition.X + x, GridPosition.Y + y);
				}
			}
		}

		/// <summary>
		/// 计算经过旋转变换后，所有引脚在战舰网格上的绝对坐标
		/// </summary>
		public IEnumerable<(PinDefinition PinDef, Vector2I TransformedGridPos)> GetTransformedPins()
		{
			if (Definition.Pins == null) yield break;

			int origW = Definition.Width;
			int origH = Definition.Height;

			foreach (var pin in Definition.Pins)
			{
				int localX = pin.LocalGridX;
				int localY = pin.LocalGridY;

				// 根据四向旋转计算引脚在构件包围盒内部的旋转后坐标
				int rotX, rotY;
				switch (Rotation)
				{
					case 1: // 90度顺时针
						rotX = origH - 1 - localY;
						rotY = localX;
						break;
					case 2: // 180度
						rotX = origW - 1 - localX;
						rotY = origH - 1 - localY;
						break;
					case 3: // 270度顺时针
						rotX = localY;
						rotY = origW - 1 - localX;
						break;
					default: // 0度
						rotX = localX;
						rotY = localY;
						break;
				}

				Vector2I absolutePos = new(GridPosition.X + rotX, GridPosition.Y + rotY);
				yield return (pin, absolutePos);
			}
		}
	}
}
