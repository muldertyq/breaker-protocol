using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.World.Economy
{
	/// <summary>
	/// 结算掉落结果 DTO
	/// </summary>
	public class LootResult
	{
		public int Scraps { get; set; } = 0;
		public int ComputeCores { get; set; } = 0;
		public List<string> DroppedModuleIds { get; } = new();
	}

	/// <summary>
	/// 全局战利品加权轮盘掉落服务 (完全由 drop_tables.json 驱动)
	/// </summary>
	public static class LootDropService
	{
		/// <summary>
		/// 执行加权掉落结算
		/// </summary>
		/// <param name="tableId">掉落表 ID</param>
		/// <param name="luckMultiplier">幸运倍率 (受契约与科技加成)</param>
		public static LootResult RollLoot(string tableId, float luckMultiplier = 1.0f)
		{
			var result = new LootResult();

			if (!DataManager.Instance.DropTables.TryGet(tableId, out var table) || table == null)
			{
				GD.PrintErr($"[LootDropService] 未找到掉落表: {tableId}");
				return result;
			}

			// 1. 金属废料区间抽取
			int baseScraps = (int)GD.RandRange(table.MinScraps, table.MaxScraps);
			result.Scraps = Mathf.Max(0, (int)(baseScraps * luckMultiplier));

			// 2. 算力核心几率判定
			if (table.CoreDropChance > 0.0f)
			{
				float finalChance = Mathf.Clamp(table.CoreDropChance * luckMultiplier, 0.0f, 1.0f);
				if (GD.Randf() <= finalChance)
				{
					result.ComputeCores = 1;
				}
			}

			// 3. 构件物品加权轮盘抽取 (Roulette Wheel Selection)
			if (table.Entries != null && table.Entries.Count > 0)
			{
				int totalWeight = 0;
				foreach (var entry in table.Entries) totalWeight += entry.Weight;

				if (totalWeight > 0)
				{
					int roll = (int)GD.RandRange(0, totalWeight);
					int currentSum = 0;

					foreach (var entry in table.Entries)
					{
						currentSum += entry.Weight;
						if (roll <= currentSum)
						{
							if (entry.DropType == DropItemType.Module && !string.IsNullOrEmpty(entry.ModuleId))
							{
								result.DroppedModuleIds.Add(entry.ModuleId);
							}
							break;
						}
					}
				}
			}

			return result;
		}

		/// <summary>
		/// 在战场坐标生成物理掉落物实体 (对齐 TASK-37 SalvageDropEntity 规范)
		/// </summary>
		public static void SpawnLootAt(Node worldContext, Vector2 position, string tableId, float luckMultiplier = 1.0f)
		{
			var loot = RollLoot(tableId, luckMultiplier);

			// 产出金属废料实体
			if (loot.Scraps > 0)
			{
				var scrapDrop = new SalvageDropEntity
				{
					Position = position + new Vector2((float)GD.RandRange(-25, 25), (float)GD.RandRange(-25, 25)),
					Velocity = new Vector2((float)GD.RandRange(-20, 20), (float)GD.RandRange(-20, 20)),
					Type = SalvageType.Scraps,
					Amount = loot.Scraps
				};
				worldContext.AddChild(scrapDrop);
			}

			// 产出算力核心实体
			if (loot.ComputeCores > 0)
			{
				var coreDrop = new SalvageDropEntity
				{
					Position = position + new Vector2((float)GD.RandRange(-25, 25), (float)GD.RandRange(-25, 25)),
					Velocity = new Vector2((float)GD.RandRange(-15, 15), (float)GD.RandRange(-15, 15)),
					Type = SalvageType.ComputeCore,
					Amount = loot.ComputeCores
				};
				worldContext.AddChild(coreDrop);
			}
		}
	}
}
