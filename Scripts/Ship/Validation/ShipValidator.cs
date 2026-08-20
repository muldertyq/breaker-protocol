using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.Ship.Validation
{
	/// <summary>
	/// 单项安全校验条目结果
	/// </summary>
	public class ValidationCheckItem
	{
		public string Name { get; set; } = string.Empty;
		public bool IsPassed { get; set; }
		public string DetailMessage { get; set; } = string.Empty;

		public ValidationCheckItem(string name, bool isPassed, string detail)
		{
			Name = name;
			IsPassed = isPassed;
			DetailMessage = detail;
		}
	}

	/// <summary>
	/// 全舰装配安全与合法性校验汇总报告
	/// </summary>
	public class ShipValidationReport
	{
		public bool IsAllPassed { get; set; } = true;
		public List<ValidationCheckItem> Items { get; } = new();

		public void AddCheck(string name, bool isPassed, string detail)
		{
			Items.Add(new ValidationCheckItem(name, isPassed, detail));
			if (!isPassed) IsAllPassed = false;
		}
	}

	/// <summary>
	/// 飞船装配合法性与物理安全校验器
	/// 严格遵循规范 02 与规范 08 中的工程装配铁律
	/// </summary>
	public static class ShipValidator
	{
		public static ShipValidationReport Validate(ShipGrid grid, StructuralGraph graph)
		{
			var report = new ShipValidationReport();

			// ============================================================
			// 检查项 1：动力核心完整性 (Power Core Check)
			// 铁律：全船必须存在至少 1 个 PowerSource 构件
			// ============================================================
			int powerCoreCount = 0;
			foreach (var module in grid.Modules)
			{
				if (module.Definition.Category == "PowerSource" && !module.IsDestroyed)
				{
					powerCoreCount++;
				}
			}

			bool powerPassed = powerCoreCount >= 1;
			report.AddCheck(
				"动力回路完整性",
				powerPassed,
				powerPassed ? $"已装配 {powerCoreCount} 台动力反应堆" : "缺失动力源！战舰必须至少装配 1 台反应堆"
			);

			// ============================================================
			// 检查项 2：推进器喷口无遮挡 (Thruster Clearance Check)
			// 铁律：依据规范 02，后向推进器喷口下方 2 格内严禁有实体装甲或构件阻挡
			// ============================================================
			bool thrusterBlocked = false;
			string thrusterError = string.Empty;

			foreach (var module in grid.Modules)
			{
				if (module.Definition.Category == "Thruster")
				{
					string dirType = module.Definition.Properties.TryGetProperty("thrustDirection", out var dt)
						? dt.GetString() ?? "Backward"
						: "Backward";

					// 仅对后向主推进器检测后方遮挡
					if (dirType == "Backward" && module.Rotation == 0) // 0度朝向（向后喷）
					{
						Vector2I size = module.GetRotatedSize();
						// 检查推进器底部下方 1~2 格区域
						for (int x = 0; x < size.X; x++)
						{
							for (int yOffset = 1; yOffset <= 2; yOffset++)
							{
								Vector2I checkPos = new(module.GridPosition.X + x, module.GridPosition.Y + size.Y - 1 + yOffset);
								var blocker = grid.GetModuleAt(checkPos);
								if (blocker != null && blocker.InstanceId != module.InstanceId)
								{
									thrusterBlocked = true;
									thrusterError = $"主推进器 [{module.Definition.Name}] 喷口后方被 [{blocker.Definition.Name}] 遮挡！";
									break;
								}
							}
							if (thrusterBlocked) break;
						}
					}
				}
				if (thrusterBlocked) break;
			}

			report.AddCheck(
				"推进器喷口通畅",
				!thrusterBlocked,
				!thrusterBlocked ? "所有推进器喷口无物理遮挡" : thrusterError
			);

			// ============================================================
			// 检查项 3：结构物理连通性 (Structural Connectivity Check)
			// 铁律：所有构件必须与动力核心保持物理连通，禁止悬空浮游零件
			// ============================================================
			graph.RebuildGraph(grid);
			var connectedIds = graph.GetConnectedComponentsFromPowerSources(grid);
			bool allConnected = connectedIds.Count == grid.ModuleCount;

			report.AddCheck(
				"物理结构连通性",
				allConnected,
				allConnected ? $"全部 {grid.ModuleCount} 个构件结构连通" : $"存在 {grid.ModuleCount - connectedIds.Count} 个未与核心相连的悬空构件！"
			);

			return report;
		}
	}
}
