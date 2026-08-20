using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.Data.Validation
{
	/// <summary>
	/// 校验严重等级
	/// </summary>
	public enum ValidationSeverity
	{
		Info,
		Warning,
		Error
	}

	/// <summary>
	/// 单条校验结果条目
	/// </summary>
	public class ValidationEntry
	{
		public ValidationSeverity Severity { get; set; }
		public string Message { get; set; } = string.Empty;

		public ValidationEntry(ValidationSeverity severity, string message)
		{
			Severity = severity;
			Message = message;
		}
	}

	/// <summary>
	/// 强类型全域数据合规校验器
	/// 负责在构件/蓝图/外循环规则注册前拦截非法配置
	/// </summary>
	public static class DataValidator
	{
		/// <summary>
		/// 全局全量校验入口（在 DataManager 加载完毕后统一调用）
		/// </summary>
		public static bool ValidateAll(DataManager dm)
		{
			bool allValid = true;

			// 1. 校验深空异象事件合法性
			foreach (var ev in dm.Events.GetAll())
			{
				if (string.IsNullOrWhiteSpace(ev.Title))
				{
					GD.PrintRich($"[color=red][DataValidator:Error] 异象事件 [{ev.Id}] 缺少标题 (Title)！[/color]");
					allValid = false;
				}

				if (ev.Choices == null || ev.Choices.Count == 0)
				{
					GD.PrintRich($"[color=red][DataValidator:Error] 异象事件 [{ev.Id}] 没有任何可选分支选项 (Choices)！[/color]");
					allValid = false;
				}
				else
				{
					foreach (var choice in ev.Choices)
					{
						if (choice.SuccessRate < 0.0f || choice.SuccessRate > 1.0f)
						{
							GD.PrintRich($"[color=red][DataValidator:Error] 异象事件 [{ev.Id}] 选项 '{choice.ChoiceText}' 的成功率超出 [0.0, 1.0] 范围: {choice.SuccessRate}[/color]");
							allValid = false;
						}
					}
				}
			}

			// 2. 校验科技树节点与前置依赖
			foreach (var tech in dm.Techs.GetAll())
			{
				if (tech.CoreCost < 0)
				{
					GD.PrintRich($"[color=red][DataValidator:Error] 科技节点 [{tech.Id}] 的算力核心消耗为负数: {tech.CoreCost}[/color]");
					allValid = false;
				}

				if (tech.Prerequisites != null)
				{
					foreach (var req in tech.Prerequisites)
					{
						if (!dm.Techs.Contains(req))
						{
							GD.PrintRich($"[color=red][DataValidator:Error] 科技节点 [{tech.Id}] 依赖了不存在的前置科技 ID: [{req}][/color]");
							allValid = false;
						}
					}
				}
			}

			// 3. 校验灾厄契约
			foreach (var pact in dm.Pacts.GetAll())
			{
				if (pact.ScrapBonusMultiplier < 0.0f)
				{
					GD.PrintRich($"[color=yellow][DataValidator:Warning] 灾厄契约 [{pact.Id}] 废料倍率加成为负数: {pact.ScrapBonusMultiplier}[/color]");
				}
			}

			// 4. 校验黑市经济规则
			var cfg = dm.MarketConfig;
			if (cfg.DefaultStockCount <= 0)
			{
				GD.PrintRich("[color=yellow][DataValidator:Warning] 黑市默认货架数量 DefaultStockCount <= 0，已自动重置为默认 5 格。[/color]");
				cfg.DefaultStockCount = 5;
			}

			if (cfg.ScrapResellRate <= 0.0f || cfg.ScrapResellRate > 1.0f)
			{
				GD.PrintRich($"[color=yellow][DataValidator:Warning] 黑市折旧回购率 ScrapResellRate ({cfg.ScrapResellRate}) 异常，建议在 (0.0, 1.0] 区间。[/color]");
			}

			// 5. 校验掉落表合法性 (TASK-40 新增)
			foreach (var table in dm.DropTables.GetAll())
			{
				if (table.MinScraps < 0 || table.MaxScraps < table.MinScraps)
				{
					GD.PrintRich($"[color=red][DataValidator:Error] 掉落表 [{table.TableId}] 废料区间非法: [{table.MinScraps}, {table.MaxScraps}][/color]");
					allValid = false;
				}

				if (table.Entries != null)
				{
					foreach (var entry in table.Entries)
					{
						if (entry.Weight < 0)
						{
							GD.PrintRich($"[color=red][DataValidator:Error] 掉落表 [{table.TableId}] 存在负数权重: {entry.Weight}[/color]");
							allValid = false;
						}
						if (entry.DropType == DropItemType.Module && !string.IsNullOrEmpty(entry.ModuleId))
						{
							if (!dm.Modules.Contains(entry.ModuleId))
							{
								GD.PrintRich($"[color=yellow][DataValidator:Warning] 掉落表 [{table.TableId}] 引用了不存在的构件 ID: [{entry.ModuleId}][/color]");
							}
						}
					}
				}
			}

			// 6. 校验遭遇战合法性 (TASK-40 新增)
			foreach (var enc in dm.Encounters.GetAll())
			{
				if (enc.Ships == null || enc.Ships.Count == 0)
				{
					GD.PrintRich($"[color=red][DataValidator:Error] 遭遇战 [{enc.EncounterId}] 未配置任何战舰！[/color]");
					allValid = false;
				}
				else
				{
					foreach (var ship in enc.Ships)
					{
						if (!dm.Blueprints.Contains(ship.BlueprintId))
						{
							GD.PrintRich($"[color=red][DataValidator:Error] 遭遇战 [{enc.EncounterId}] 引用了不存在的战舰蓝图: [{ship.BlueprintId}][/color]");
							allValid = false;
						}
					}
				}
			}

			if (allValid)
			{
				GD.PrintRich("[color=green][DataValidator:Info] ✔ 全域数据（构件、蓝图、异象、科技树、契约、黑市、掉落池、遭遇池）规则校验全部通过！[/color]");
			}

			return allValid;
		}

		/// <summary>
		/// 校验单个构件定义的合法性 (保持原版几何与引脚检查)
		/// </summary>
		/// <param name="module">待校验的构件数据</param>
		/// <param name="sourceFilePath">来源文件路径（用于报错追踪）</param>
		/// <param name="entries">输出的错误/警告列表</param>
		/// <returns>若无 Error 级别错误则返回 true</returns>
		public static bool ValidateModule(ModuleDataDefinition module, string sourceFilePath, out List<ValidationEntry> entries)
		{
			entries = new List<ValidationEntry>();
			bool hasFatalError = false;

			// 1. 基础字段非空检查
			if (string.IsNullOrWhiteSpace(module.Id))
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Error, "构件 ID 不能为空！"));
				hasFatalError = true;
			}

			if (string.IsNullOrWhiteSpace(module.Name))
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Warning, $"构件 [{module.Id}] 未填写显示名称 (Name)。"));
			}

			// 2. 几何网格模数检查 (规范 02)
			if (module.Width <= 0 || module.Height <= 0)
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Error, $"构件 [{module.Id}] 的尺寸非法：Width={module.Width}, Height={module.Height}（宽高必须大于等于 1）！"));
				hasFatalError = true;
			}
			else if (module.Width > 8 || module.Height > 8)
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Warning, $"构件 [{module.Id}] 尺寸过大 ({module.Width}x{module.Height})，超过了 8x8 推荐上限。"));
			}

			// 3. 物理属性合理性检查
			if (module.Mass <= 0.0f)
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Warning, $"构件 [{module.Id}] 质量 <= 0，已自动修正为默认质量 1.0t。"));
				module.Mass = 1.0f;
			}

			if (module.BaseHp <= 0.0f)
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Error, $"构件 [{module.Id}] 基础耐久 BaseHp 必须大于 0！"));
				hasFatalError = true;
			}

			// 4. 引脚对齐与越界检查 (规范 03)
			if (module.Pins != null && module.Pins.Length > 0)
			{
				HashSet<string> pinIdSet = new();
				HashSet<Vector2I> pinCoordSet = new();

				foreach (var pin in module.Pins)
				{
					// 4.1 引脚 ID 唯一性检查
					if (string.IsNullOrWhiteSpace(pin.PinId))
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error, $"构件 [{module.Id}] 存在未命名的引脚！"));
						hasFatalError = true;
					}
					else if (!pinIdSet.Add(pin.PinId))
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error, $"构件 [{module.Id}] 存在重复的 PinId: [{pin.PinId}]！"));
						hasFatalError = true;
					}

					// 4.2 引脚坐标是否落在构件网格边界内 [0, Width-1] x [0, Height-1]
					if (pin.LocalGridX < 0 || pin.LocalGridX >= module.Width ||
						pin.LocalGridY < 0 || pin.LocalGridY >= module.Height)
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error, 
							$"构件 [{module.Id}] 的引脚 [{pin.PinId}] 坐标越界！坐标: ({pin.LocalGridX}, {pin.LocalGridY})，构件尺寸: {module.Width}x{module.Height}。"));
						hasFatalError = true;
					}

					// 4.3 同一格不能有重叠引脚
					Vector2I coord = new(pin.LocalGridX, pin.LocalGridY);
					if (!pinCoordSet.Add(coord))
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Warning, 
							$"构件 [{module.Id}] 在局部坐标 ({pin.LocalGridX}, {pin.LocalGridY}) 存在多个重叠引脚。"));
					}

					// 4.4 引脚方向有效性检查
					if (pin.Type != "IN" && pin.Type != "OUT")
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error, 
							$"构件 [{module.Id}] 引脚 [{pin.PinId}] 的 Type [{pin.Type}] 非法！必须是 'IN' 或 'OUT'。"));
						hasFatalError = true;
					}
				}
			}

			// 5. 打印汇总日志
			if (entries.Count > 0)
			{
				foreach (var entry in entries)
				{
					string colorTag = entry.Severity switch
					{
						ValidationSeverity.Error => "red",
						ValidationSeverity.Warning => "yellow",
						_ => "white"
					};

					GD.PrintRich($"[color={colorTag}][DataValidator:{entry.Severity}] 文件 [{sourceFilePath}] -> {entry.Message}[/color]");
				}
			}

			return !hasFatalError;
		}
	}
}
