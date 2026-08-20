using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Ship;
using BreakerProtocol.World.Economy;

namespace BreakerProtocol.World.Meta
{
	public enum TechCategory
	{
		Metallurgy,  // 重工冶金 (防护、装甲、爆甲)
		Electronics, // 超频电容 (配线、经济、黑客)
		Propulsion   // 矢量推进 (机动、刹车、航道)
	}

	/// <summary>
	/// 单个 Meta 科技节点数据模型
	/// </summary>
	public class MetaTechNode
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public TechCategory Category { get; set; }
		public int Tier { get; set; } = 1;
		public int Cost { get; set; } = 50;
		public string Description { get; set; } = string.Empty;
		public string? PrerequisiteId { get; set; }
		public bool IsUnlocked { get; set; } = false;
		public Vector2 DisplayPosition { get; set; }
	}

	/// <summary>
	/// 局外永久 Meta 科技树总控中枢 (单例)
	/// </summary>
	public class MetaProgressionManager
	{
		private static MetaProgressionManager? _instance;
		public static MetaProgressionManager Instance => _instance ??= new MetaProgressionManager();

		public int DataFragments { get; private set; } = 280;

		public Dictionary<string, MetaTechNode> AllTechs { get; } = new();
		public event Action<int>? OnDataFragmentsChanged;
		public event Action<MetaTechNode>? OnTechUnlocked;

		public MetaProgressionManager()
		{
			InitializeDatabase();
		}

		private void InitializeDatabase()
		{
			AllTechs.Clear();

			// 1. 重工冶金分支 (Metallurgy)
			AddTech(new MetaTechNode
			{
				Id = "tech_meta_armor_1",
				Name = "复合重装甲渗碳",
				Category = TechCategory.Metallurgy,
				Tier = 1,
				Cost = 60,
				Description = "全舰所有装甲与结构构件的基础耐久度永久提升 +15%。",
				DisplayPosition = new Vector2(220, 200)
			});
			AddTech(new MetaTechNode
			{
				Id = "tech_meta_ramming_2",
				Name = "高频冲压撞角",
				Category = TechCategory.Metallurgy,
				Tier = 2,
				Cost = 120,
				PrerequisiteId = "tech_meta_armor_1",
				Description = "舰体高速撞击敌舰时动能伤害提升 +35%，自身受到的撞击反作用力降低 25%。",
				DisplayPosition = new Vector2(220, 320)
			});
			AddTech(new MetaTechNode
			{
				Id = "tech_meta_ablative_3",
				Name = "预应力战术爆甲",
				Category = TechCategory.Metallurgy,
				Tier = 3,
				Cost = 200,
				PrerequisiteId = "tech_meta_ramming_2",
				Description = "绝境过载爆甲脱困机构无需改装，开局初始自带完整装药。",
				DisplayPosition = new Vector2(220, 440)
			});

			// 2. 超频电容分支 (Electronics)
			AddTech(new MetaTechNode
			{
				Id = "tech_elec_bus_1",
				Name = "超导低温配线母线",
				Category = TechCategory.Electronics,
				Tier = 1,
				Cost = 60,
				Description = "脉冲电路的信号衰减与发热耗损降低 20%，供电效率显著提高。",
				DisplayPosition = new Vector2(580, 200)
			});
			AddTech(new MetaTechNode
			{
				Id = "tech_elec_economy_2",
				Name = "废土走私暗格",
				Category = TechCategory.Electronics,
				Tier = 2,
				Cost = 120,
				PrerequisiteId = "tech_elec_bus_1",
				Description = "每次开启新星区航行时，开局初始废料额外获赠 +150 ⚙。",
				DisplayPosition = new Vector2(580, 320)
			});
			AddTech(new MetaTechNode
			{
				Id = "tech_elec_hacker_3",
				Name = "深空断路解算矩阵",
				Category = TechCategory.Electronics,
				Tier = 3,
				Cost = 200,
				PrerequisiteId = "tech_elec_economy_2",
				Description = "遭遇深空异象与未知事件时，所有高风险分支的检定成功率常驻 +15%。",
				DisplayPosition = new Vector2(580, 440)
			});

			// 3. 矢量推进分支 (Propulsion)
			AddTech(new MetaTechNode
			{
				Id = "tech_prop_rcs_1",
				Name = "矢量姿态高压喷口",
				Category = TechCategory.Propulsion,
				Tier = 1,
				Cost = 60,
				Description = "RCS 姿态调整回转扭矩提升 +25%，舰首瞄准指向更加迅捷精准。",
				DisplayPosition = new Vector2(940, 200)
			});
			AddTech(new MetaTechNode
			{
				Id = "tech_prop_brake_2",
				Name = "电磁反喷阻尼过载",
				Category = TechCategory.Propulsion,
				Tier = 2,
				Cost = 120,
				PrerequisiteId = "tech_prop_rcs_1",
				Description = "巡航阻尼开启状态下的急停刹车反推力提升 +30%，摆脱惯性漂移更快。",
				DisplayPosition = new Vector2(940, 320)
			});
			AddTech(new MetaTechNode
			{
				Id = "tech_prop_jump_3",
				Name = "跃迁引擎超频预热",
				Category = TechCategory.Propulsion,
				Tier = 3,
				Cost = 200,
				PrerequisiteId = "tech_prop_brake_2",
				Description = "星区生成时，敌方追击舰队前线起始落后距离额外增加 +1.0 星区列！",
				DisplayPosition = new Vector2(940, 440)
			});
		}

		private void AddTech(MetaTechNode tech)
		{
			AllTechs[tech.Id] = tech;
		}

		public void AddDataFragments(int amount)
		{
			if (amount <= 0) return;
			DataFragments += amount;
			OnDataFragmentsChanged?.Invoke(DataFragments);
		}

		public void SetDataFragments(int amount)
		{
			DataFragments = Mathf.Max(0, amount);
			OnDataFragmentsChanged?.Invoke(DataFragments);
		}

		public bool UnlockTech(string techId)
		{
			if (!AllTechs.TryGetValue(techId, out var tech)) return false;
			if (tech.IsUnlocked) return false;

			if (!string.IsNullOrEmpty(tech.PrerequisiteId))
			{
				if (!AllTechs.TryGetValue(tech.PrerequisiteId, out var pre) || !pre.IsUnlocked)
				{
					return false;
				}
			}

			if (DataFragments >= tech.Cost)
			{
				DataFragments -= tech.Cost;
				tech.IsUnlocked = true;
				OnDataFragmentsChanged?.Invoke(DataFragments);
				OnTechUnlocked?.Invoke(tech);
				return true;
			}

			return false;
		}

		public void ResetAllTechs()
		{
			int refund = 0;
			foreach (var tech in AllTechs.Values)
			{
				if (tech.IsUnlocked)
				{
					refund += tech.Cost;
					tech.IsUnlocked = false;
				}
			}
			DataFragments += refund;
			OnDataFragmentsChanged?.Invoke(DataFragments);
		}

		/// <summary>
		/// 从存档中原子化装载母港科技与碎片状态 (专供 SaveManager.LoadMeta 调用)
		/// </summary>
		public void LoadState(int fragments, List<string>? unlockedTechIds)
		{
			// 1. 重置全部科技解锁标记 (不退款)
			foreach (var tech in AllTechs.Values)
			{
				tech.IsUnlocked = false;
			}

			// 2. 精确覆盖碎片资产
			DataFragments = Mathf.Max(0, fragments);

			// 3. 标记已解锁科技
			if (unlockedTechIds != null)
			{
				foreach (var techId in unlockedTechIds)
				{
					if (AllTechs.TryGetValue(techId, out var tech))
					{
						tech.IsUnlocked = true;
					}
				}
			}

			OnDataFragmentsChanged?.Invoke(DataFragments);
		}

		public void ApplyMetaBuffsToNewRun(ShipEntity ship)
		{
			if (IsUnlocked("tech_elec_economy_2"))
			{
				PlayerEconomyManager.Instance.AddScraps(150);
			}

			if (IsUnlocked("tech_meta_armor_1") && ship.Grid != null)
			{
				foreach (var m in ship.Grid.Modules)
				{
					if (m.Definition.Category == "Armor" || m.Definition.Category == "Hull")
					{
						m.CurrentHp = m.MaxHp * 1.15f;
					}
				}
			}
		}

		public bool IsUnlocked(string techId)
		{
			return AllTechs.TryGetValue(techId, out var t) && t.IsUnlocked;
		}
	}
}
