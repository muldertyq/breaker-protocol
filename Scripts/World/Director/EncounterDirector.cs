using System.Collections.Generic;
using System.Linq;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.Ship.AI;

namespace BreakerProtocol.World.Director
{
	/// <summary>
	/// 战役遭遇生成导演 (完全由 encounter_pools.json 驱动)
	/// </summary>
	public static class EncounterDirector
	{
		/// <summary>
		/// 根据星区类别与难度随机挑选遭遇配置
		/// </summary>
		public static EncounterDef? PickEncounter(string category = "Combat", int maxDifficulty = 3)
		{
			var candidates = DataManager.Instance.Encounters.GetAll()
				.Where(e => e.Category == category && e.DifficultyRating <= maxDifficulty)
				.ToList();

			if (candidates.Count == 0)
			{
				return DataManager.Instance.Encounters.GetAll().FirstOrDefault();
			}

			return candidates[(int)GD.RandRange(0, candidates.Count - 1)];
		}

		/// <summary>
		/// 在指定战场生成敌方战队 (支持指定目标并挂载 AI 控制器)
		/// </summary>
		public static List<ShipEntity> SpawnEncounter(Node worldContext, EncounterDef encounter, Vector2 centerPos, Node2D? target = null)
		{
			var spawnedShips = new List<ShipEntity>();

			foreach (var shipDef in encounter.Ships)
			{
				if (!DataManager.Instance.Blueprints.TryGet(shipDef.BlueprintId, out var blueprint) || blueprint == null)
				{
					GD.PrintErr($"[EncounterDirector] 未找到蓝图: {shipDef.BlueprintId}");
					continue;
				}

				Vector2 spawnPos = centerPos + new Vector2(shipDef.SpawnOffset.X, shipDef.SpawnOffset.Y);

				var enemyShip = new ShipEntity
				{
					Name = $"Enemy_{shipDef.Role}_{spawnedShips.Count + 1}",
					Position = spawnPos,
					Rotation = Mathf.Pi // 敌舰默认朝向下方
				};
				enemyShip.AddToGroup("Enemies");
				enemyShip.AddToGroup("Ship");

				worldContext.AddChild(enemyShip);
				ShipBlueprintLoader.ApplyBlueprint(enemyShip, blueprint);

				// 挂载 AI 控制原型 (对齐 AiArchetype 标准定义)
				AiArchetype archetype = shipDef.Role switch
				{
					"Scout"  => AiArchetype.Swarm,
					"Elite"  => AiArchetype.KiteSniper, // 修复此处枚举名称
					"Boss"   => AiArchetype.Brawler,
					_        => AiArchetype.Brawler
				};

				enemyShip.AttachAI(archetype, target);
				spawnedShips.Add(enemyShip);
			}

			GD.Print($"[EncounterDirector] 已生成遭遇战: 【{encounter.DisplayName}】，共 {spawnedShips.Count} 艘战舰。");
			return spawnedShips;
		}
	}
}
