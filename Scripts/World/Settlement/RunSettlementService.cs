using System;
using Godot;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Meta;

namespace BreakerProtocol.World.Settlement
{
	public enum RunEndingType
	{
		Victory,          // 斩杀星区 Boss 取得辉煌胜利
		Defeat_Destroyed, // 舰体结构彻底解体殉爆
		Defeat_Overrun,   // 被追击舰队浪潮彻底吞没
		Abandoned         // 战术撤退 / 主动放弃
	}

	public enum EvaluationRank
	{
		S, // 传奇旗舰 (Score >= 5000)
		A, // 精英领航 (Score >= 3500)
		B, // 坚韧老兵 (Score >= 2000)
		C, // 幸存残骸 (Score >= 1000)
		D  // 彻底折戟 (Score < 1000)
	}

	/// <summary>
	/// 单局战役详细统计与战果数据包
	/// </summary>
	public class RunStatistics
	{
		public RunEndingType Ending { get; set; } = RunEndingType.Victory;
		public int SectorsCleared { get; set; } = 8;
		public int StandardEnemiesKilled { get; set; } = 14;
		public int ElitesKilled { get; set; } = 3;
		public int BossesKilled { get; set; } = 1;
		public int TotalScrapsEarned { get; set; } = 680;
		public int ComputeCoresEarned { get; set; } = 3;
		public float DamageTakenTotal { get; set; } = 420.0f;
		public float FinalHullIntegrityPercent { get; set; } = 85.0f;
		public float DurationSeconds { get; set; } = 485.0f;

		// 结算产出
		public int CalculatedScore { get; set; }
		public EvaluationRank Rank { get; set; }
		public int DataFragmentsEarned { get; set; }
	}

	/// <summary>
	/// 战役结算与战损残局折算中枢
	/// </summary>
	public static class RunSettlementService
	{
		public static RunStatistics CalculateSettlement(RunStatistics stats)
		{
			// 1. 基础得分计算
			int sectorScore = stats.SectorsCleared * 300;
			int killScore = (stats.StandardEnemiesKilled * 80) + (stats.ElitesKilled * 250) + (stats.BossesKilled * 1000);
			int economyScore = (stats.TotalScrapsEarned / 2) + (stats.ComputeCoresEarned * 150);
			int survivalBonus = stats.Ending == RunEndingType.Victory ? 1500 : 0;
			int integrityBonus = (int)(stats.FinalHullIntegrityPercent * 10);

			int totalScore = Mathf.Max(100, sectorScore + killScore + economyScore + survivalBonus + integrityBonus);
			stats.CalculatedScore = totalScore;

			// 2. 评级判定 (S / A / B / C / D)
			stats.Rank = totalScore switch
			{
				>= 5000 => EvaluationRank.S,
				>= 3500 => EvaluationRank.A,
				>= 2200 => EvaluationRank.B,
				>= 1200 => EvaluationRank.C,
				_       => EvaluationRank.D
			};

			// 3. 研发数据碎片转化计算 (Data Fragments 💾)
			// 公式: (总得分 / 40) + (Boss击杀 * 100) + (精英击杀 * 30) + 胜利保底
			int fragmentReward = (totalScore / 40) + (stats.BossesKilled * 100) + (stats.ElitesKilled * 30);
			if (stats.Ending == RunEndingType.Victory)
			{
				fragmentReward += 150;
			}
			stats.DataFragmentsEarned = fragmentReward;

			// 4. 将收益注入局外 Meta 科技总控中枢
			MetaProgressionManager.Instance.AddDataFragments(fragmentReward);

			return stats;
		}
	}
}
