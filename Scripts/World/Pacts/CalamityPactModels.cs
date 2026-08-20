using System;
using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.World.Pacts
{
	public enum PactId
	{
		SolarStorm,       // 强电磁风暴 (散热-35%)
		DepletedUranium,  // 贫铀穿甲装药 (敌方穿透+30%)
		PursuitTighten,   // 追击严阵以待 (追击速度+50%)
		WeaknessScan,     // 弱点针对扫描 (AI集火裸线)
		VolatileSurge,    // 高爆殉爆链 (次生电涌)
		GravityAnomaly    // 深空引力紊乱 (漂移惯性极大)
	}

	public class CalamityPact
	{
		public PactId Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public int HeatLevel { get; set; } = 1;
		public string Description { get; set; } = string.Empty;
		public string PenaltyTag { get; set; } = string.Empty;
		public bool IsActive { get; set; } = false;
	}

	/// <summary>
	/// 极限挑战灾厄契约热度总控中枢 (单例)
	/// </summary>
	public class CalamityPactManager
	{
		private static CalamityPactManager? _instance;
		public static CalamityPactManager Instance => _instance ??= new CalamityPactManager();

		public Dictionary<PactId, CalamityPact> Pacts { get; } = new();
		public event Action? OnPactsChanged;

		public CalamityPactManager()
		{
			InitializePacts();
		}

		private void InitializePacts()
		{
			Pacts.Clear();

			AddPact(new CalamityPact
			{
				Id = PactId.SolarStorm,
				Name = "⚡ 强电磁耀斑风暴",
				HeatLevel = 1,
				Description = "恒星风暴产生严重电磁干扰，全舰所有导线与武器的自然散热速率降低 35%。",
				PenaltyTag = "散热效率: -35%"
			});

			AddPact(new CalamityPact
			{
				Id = PactId.DepletedUranium,
				Name = "💥 敌方贫铀穿甲军备",
				HeatLevel = 1,
				Description = "敌方舰队加装了贫铀穿甲弹头，对玩家外覆装甲的跳弹倾角要求大幅提高，穿透力增加 30%。",
				PenaltyTag = "敌方穿透: +30%"
			});

			AddPact(new CalamityPact
			{
				Id = PactId.PursuitTighten,
				Name = "⏱️ 追击舰队严阵以待",
				HeatLevel = 2,
				Description = "敌方追击先锋部队引擎过载，星区每次跳跃后追击波前推进速度提升 50%。",
				PenaltyTag = "追击航速: +50%"
			});

			AddPact(new CalamityPact
			{
				Id = PactId.WeaknessScan,
				Name = "🧠 启发式弱点定点打击",
				HeatLevel = 2,
				Description = "所有敌机 AI 深度解锁启发式扫描，优先集火玩家暴露的供电铜排裸线与受损侧舷。",
				PenaltyTag = "AI集火弱点"
			});

			AddPact(new CalamityPact
			{
				Id = PactId.VolatileSurge,
				Name = "💣 高敏反应堆次生电涌",
				HeatLevel = 2,
				Description = "动力回路极为敏感，任何构件受创时均有 20% 几率向邻近导线释放 40 点次生电涌伤害。",
				PenaltyTag = "次生电涌几率: 20%"
			});

			AddPact(new CalamityPact
			{
				Id = PactId.GravityAnomaly,
				Name = "🌌 深空超重引力紊乱",
				HeatLevel = 3,
				Description = "时空曲率不稳定，巡航阻尼制动反推力削弱 40%，机动漂移失控风险大幅攀升。",
				PenaltyTag = "阻尼制动: -40%"
			});
		}

		private void AddPact(CalamityPact pact)
		{
			Pacts[pact.Id] = pact;
		}

		public void TogglePact(PactId id)
		{
			if (Pacts.TryGetValue(id, out var pact))
			{
				pact.IsActive = !pact.IsActive;
				OnPactsChanged?.Invoke();
			}
		}

		public int GetTotalHeatLevel()
		{
			int total = 0;
			foreach (var p in Pacts.Values)
			{
				if (p.IsActive) total += p.HeatLevel;
			}
			return total;
		}

		/// <summary>
		/// 结算收益加成倍率 (每个热度等级额外加成 +20% 研发数据与积分)
		/// </summary>
		public float GetScoreRewardMultiplier()
		{
			return 1.0f + (GetTotalHeatLevel() * 0.20f);
		}

		public bool IsActive(PactId id)
		{
			return Pacts.TryGetValue(id, out var p) && p.IsActive;
		}
	}
}
