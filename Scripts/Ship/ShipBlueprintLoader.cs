using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship.Pipeline;

namespace BreakerProtocol.Ship
{
	/// <summary>
	/// 战舰蓝图解析与生成引擎
	/// </summary>
	public static class ShipBlueprintLoader
	{
		/// <summary>
		/// 将蓝图数据完整实装应用到指定 ShipEntity 战舰上
		/// </summary>
		public static bool ApplyBlueprint(ShipEntity ship, BlueprintDataDefinition blueprint)
		{
			if (blueprint == null)
			{
				GD.PrintErr("[ShipBlueprintLoader] 蓝图对象为 null！");
				return false;
			}

			// 1. 清空当前战舰网格与管线
			ship.Grid.Clear();
			ship.Pipeline.Clear();

			// 2. 依次放置构件
			foreach (var record in blueprint.Modules)
			{
				if (DataManager.Instance.Modules.TryGet(record.ModuleId, out var def))
				{
					ship.Grid.TryPlaceModule(def!, new Vector2I(record.GridX, record.GridY), record.Rotation, out _);
				}
				else
				{
					GD.PrintErr($"[ShipBlueprintLoader] 缺失构件定义: [{record.ModuleId}]，蓝图可能损坏！");
				}
			}

			// 3. 收集全舰所有引脚
			var pins = new List<PinInstance>(ship.GetAllPins());

			// 4. 自动铺设蓝图预设导线
			foreach (var wireRec in blueprint.Wires)
			{
				Vector2I srcPos = new(wireRec.SourceGridX, wireRec.SourceGridY);
				Vector2I dstPos = new(wireRec.TargetGridX, wireRec.TargetGridY);

				var srcPin = pins.Find(p => p.AbsoluteGridPos == srcPos && p.Type == PinType.OUT);
				var dstPin = pins.Find(p => p.AbsoluteGridPos == dstPos && p.Type == PinType.IN);

				if (srcPin != null && dstPin != null)
				{
					ship.Pipeline.TryAddWire(srcPin, dstPin, ship.Grid, out _);
				}
				else
				{
					GD.PrintErr($"[ShipBlueprintLoader] 无法在 ({srcPos}) -> ({dstPos}) 之间建立导线：端口未找到！");
				}
			}

			// 5. 重构战舰物理刚体与受力拓扑
			ship.RebuildPhysics();

			GD.PrintRich($"[color=green][ShipBlueprintLoader] 成功应用蓝图 [{blueprint.Name}] (ID:{blueprint.Id}): 构件数={ship.Grid.ModuleCount}, 导线数={ship.Pipeline.WireCount}[/color]");
			return true;
		}
	}
}
