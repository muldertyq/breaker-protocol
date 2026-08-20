using System.Collections.Generic;
using Godot;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.Ship.Pipeline;

namespace BreakerProtocol.Combat.Trauma
{
	/// <summary>
	/// 射线穿透创伤步进结算报告
	/// </summary>
	public struct RaymarchStepResult
	{
		public int TotalCellsTraversed;       // 穿透穿过的总网格格数
		public int DamagedModuleCount;        // 沿途受到创伤的机组构件数
		public int SeveredWireCount;          // 沿途被斩断的 PCB 导线数
		public bool FullyPenetrated;          // 是否贯穿出飞船另一侧
		public List<string> SeveredWireIds;   // 被切断的导线 ID 列表
	}

	/// <summary>
	/// 舰内二维网格射线步进创伤引擎 (基于 2D DDA 快速体素遍历算法，规范 09)
	/// </summary>
	public static class InternalTraumaRaymarcher
	{
		private const int MaxRaySteps = 64; // 最大舰内步进格数

		/// <summary>
		/// 执行舰内 2D DDA 射线穿透步进
		/// </summary>
		/// <param name="ship">受击战舰目标</param>
		/// <param name="localStartGrid">局部网格入射起点 (GU)</param>
		/// <param name="localDir">子弹在局部网格空间中的行进方向</param>
		/// <param name="bulletDamage">子弹实时剩余威力 (传递引用)</param>
		/// <param name="remainingPierce">子弹剩余穿透层数 (传递引用)</param>
		/// <param name="elements">附带元素属性</param>
		/// <param name="firstHitModuleId">首个被击中已扣血的构件 ID (防止重复扣血)</param>
		public static RaymarchStepResult MarchThroughShip(
			ShipEntity ship,
			Vector2 localStartGrid,
			Vector2 localDir,
			ref float bulletDamage,
			ref int remainingPierce,
			ElementFlags elements,
			string? firstHitModuleId = null)
		{
			var result = new RaymarchStepResult
			{
				TotalCellsTraversed = 0,
				DamagedModuleCount = 0,
				SeveredWireCount = 0,
				FullyPenetrated = false,
				SeveredWireIds = new List<string>()
			};

			if (remainingPierce < 0 || bulletDamage <= 1.0f) return result;

			Vector2 dir = localDir.Normalized();
			if (dir.LengthSquared() < 0.001f) return result;

			// ============================================================
			// 阶段 1：初始化 2D DDA 算法参数
			// ============================================================
			Vector2I currentCell = new(Mathf.FloorToInt(localStartGrid.X), Mathf.FloorToInt(localStartGrid.Y));

			int stepX = dir.X >= 0 ? 1 : -1;
			int stepY = dir.Y >= 0 ? 1 : -1;

			float deltaDistX = Mathf.Abs(dir.X) > 0.0001f ? Mathf.Abs(1.0f / dir.X) : 10000.0f;
			float deltaDistY = Mathf.Abs(dir.Y) > 0.0001f ? Mathf.Abs(1.0f / dir.Y) : 10000.0f;

			float sideDistX = (dir.X >= 0)
				? (currentCell.X + 1.0f - localStartGrid.X) * deltaDistX
				: (localStartGrid.X - currentCell.X) * deltaDistX;

			float sideDistY = (dir.Y >= 0)
				? (currentCell.Y + 1.0f - localStartGrid.Y) * deltaDistY
				: (localStartGrid.Y - currentCell.Y) * deltaDistY;

			var damagedModulesInThisRay = new HashSet<string>();
			if (!string.IsNullOrEmpty(firstHitModuleId))
			{
				damagedModulesInThisRay.Add(firstHitModuleId); // 记录首层已扣血构件
			}

			// ============================================================
			// 阶段 2：逐格步进结算
			// ============================================================
			for (int step = 0; step < MaxRaySteps; step++)
			{
				result.TotalCellsTraversed++;

				// 1. 调用原版 SeverWiresAt (基于 _gridWireMap 空间索引 O(1) 命中)
				var cutWires = ship.Pipeline.SeverWiresAt(currentCell);
				if (cutWires.Count > 0)
				{
					result.SeveredWireCount += cutWires.Count;
					foreach (var wire in cutWires)
					{
						result.SeveredWireIds.Add(wire.WireId);
					}

					Vector2 cellPixelWorld = ship.GlobalTransform * GlobalMetrics.MetersToPixels(new Vector2(currentCell.X + 0.5f, currentCell.Y + 0.5f));
					
					VfxManager.Instance?.SpawnElectricArc(cellPixelWorld, cellPixelWorld + new Vector2(16, -16), Colors.Yellow);
					VfxManager.Instance?.SpawnFloatingText(cellPixelWorld, "⚡ PCB 供电线路被斩断!", Colors.Crimson);
					GD.PrintRich($"[color=red][Trauma] ✂️ 穿甲弹在网格 ({currentCell.X}, {currentCell.Y}) 切断了 {cutWires.Count} 条 PCB 供电导线！[/color]");
				}

				// 2. 检查当前网格是否存在构件机组
				var module = ship.Grid.GetModuleAt(currentCell);
				if (module != null && !module.IsDestroyed && !damagedModulesInThisRay.Contains(module.InstanceId))
				{
					damagedModulesInThisRay.Add(module.InstanceId);
					result.DamagedModuleCount++;

					float res = module.Definition.ArmorResistance;
					float damageDealt = Mathf.Max(5.0f, bulletDamage - res);
					
					module.CurrentHp = Mathf.Max(0.0f, module.CurrentHp - damageDealt);
					ship.OnModuleDamaged(module, damageDealt);

					remainingPierce--;
					bulletDamage *= 0.70f;

					string tierName = GetDamageTierName(module);
					GD.PrintRich($"[color=orange][Trauma] 🎯 击中内部机组 [{module.Definition.Name}] -{damageDealt:F0} HP | 状态: {tierName} | 剩余穿深: {remainingPierce}[/color]");

					if (remainingPierce < 0 || bulletDamage < 10.0f)
					{
						break;
					}
				}

				// 3. 推进 DDA 步进
				if (sideDistX < sideDistY)
				{
					sideDistX += deltaDistX;
					currentCell.X += stepX;
				}
				else
				{
					sideDistY += deltaDistY;
					currentCell.Y += stepY;
				}

				// 4. 超出战舰网格范围判定贯穿
				if (Mathf.Abs(currentCell.X) > 32 || Mathf.Abs(currentCell.Y) > 32)
				{
					result.FullyPenetrated = true;
					break;
				}
			}

			return result;
		}

		private static string GetDamageTierName(ModuleInstance module)
		{
			float ratio = module.CurrentHp / module.MaxHp;
			if (ratio >= 0.60f) return "[color=green]完好 (100%~60%)[/color]";
			if (ratio >= 0.25f) return "[color=yellow]轻损 (60%~25%)[/color]";
			if (ratio > 0.0f)   return "[color=orange]重瘫 (25%~1%)[/color]";
			return "[color=red]彻底摧毁 (0%)[/color]";
		}
	}
}
