using System.Collections.Generic;
using Godot;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Core;

namespace BreakerProtocol.Ship.Thermal
{
	/// <summary>
	/// 舰内高爆殉爆与防殉爆保险阀自熔断引擎 (规范 09《次生火灾与殉爆》)
	/// </summary>
	public static class SympatheticDetonationEngine
	{
		/// <summary>
		/// 检查构件被摧毁时是否满足高爆殉爆条件
		/// </summary>
		public static void CheckAndTrigger(ShipEntity ship, ModuleInstance destroyedModule)
		{
			string category = destroyedModule.Definition.Category;
			string modId = destroyedModule.Definition.Id; // 修正：使用 Id

			// 仅限高危蓄能/储能/反应堆机组触发内部高爆殉爆
			bool isVolatile = category == "PowerSource" || 
							  modId.Contains("capacitor") || 
							  modId.Contains("ammo") ||
							  modId.Contains("mortar");

			if (!isVolatile) return;

			Vector2I centerGrid = destroyedModule.GridPosition;
			Vector2 centerWorld = ship.GlobalTransform * GlobalMetrics.MetersToPixels(
				new Vector2(centerGrid.X + 0.5f, centerGrid.Y + 0.5f)
			);

			GD.PrintRich($"[color=red][SympatheticDetonation] 💥 高危机组 [{destroyedModule.Definition.Name}] 发生内部连锁高爆殉爆！[/color]");

			// 1. 触发大爆炸破片与冲击波视效
			VfxManager.Instance?.SpawnModuleExplosion(centerWorld, new Vector2(64, 64), Colors.OrangeRed, shardCount: 24);
			VfxManager.Instance?.SpawnFloatingText(centerWorld, "💥 内部高爆殉爆 400HP!", Colors.Crimson);
			JuiceManager.Instance?.TriggerHitstop(0.08f, 0.03f);
			JuiceManager.Instance?.AddCameraTrauma(0.75f);

			// 2. 向周围相邻网格释放 400 HP 爆轰波
			Vector2I[] adjacentOffsets = {
				Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right,
				new(1, 1), new(-1, 1), new(1, -1), new(-1, -1),
				new(0, 2), new(0, -2), new(2, 0), new(-2, 0)
			};

			var damagedModules = new HashSet<string> { destroyedModule.InstanceId };

			foreach (var offset in adjacentOffsets)
			{
				Vector2I targetCell = centerGrid + offset;
				var targetMod = ship.Grid.GetModuleAt(targetCell);

				if (targetMod == null || targetMod.IsDestroyed || damagedModules.Contains(targetMod.InstanceId))
				{
					continue;
				}

				// ============================================================
				// 核心防护：防殉爆保险阀 (Fuse) 检测！
				// ============================================================
				string targetLogicType = targetMod.Definition.Properties.TryGetProperty("logicType", out var lt)
					? lt.GetString() ?? string.Empty
					: string.Empty;

				if (targetMod.Definition.Category == "Logic" && (targetLogicType == "Fuse" || targetMod.Definition.Id.Contains("fuse")))
				{
					// 保险阀在 0.1s 极速自熔断，彻底吸收爆轰波，隔绝后方核心！
					targetMod.CurrentHp = 0.0f;
					ship.OnModuleDamaged(targetMod, 400.0f);

					Vector2 fusePosWorld = ship.GlobalTransform * GlobalMetrics.MetersToPixels(
						new Vector2(targetMod.GridPosition.X + 0.5f, targetMod.GridPosition.Y + 0.5f)
					);
					VfxManager.Instance?.SpawnElectricArc(fusePosWorld, fusePosWorld + new Vector2(15, -15), Colors.Yellow);
					VfxManager.Instance?.SpawnFloatingText(fusePosWorld, "🛡️ 保险阀自熔断！阻断殉爆！", Colors.Cyan);
					GD.PrintRich("[color=cyan][SympatheticDetonation] 🛡️ [防殉爆保险阀] 监测到逆向超载爆轰波，0.1s 内瞬间自熔断，成功保全后方核心机组！[/color]");
					
					// 阻断该方向后续蔓延
					break;
				}

				// 无保险阀保护：机组承受 400 HP 殉爆撕裂伤害
				damagedModules.Add(targetMod.InstanceId);
				float shockwaveDamage = 400.0f;
				targetMod.CurrentHp = Mathf.Max(0.0f, targetMod.CurrentHp - shockwaveDamage);
				ship.OnModuleDamaged(targetMod, shockwaveDamage);
				GD.PrintRich($"[color=orange][SympatheticDetonation] ⚠️ 殉爆波波及机组 [{targetMod.Definition.Name}]，造成 -{shockwaveDamage:F0} 巨额内部伤害！[/color]");
			}
		}
	}
}
