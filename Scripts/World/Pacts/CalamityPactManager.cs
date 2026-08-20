using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.World.Pacts
{
	/// <summary>
	/// 极限挑战灾厄契约热度总控中枢 (单例逻辑管理器，完全由 DataManager 驱动)
	/// </summary>
	public class CalamityPactManager
	{
		private static CalamityPactManager? _instance;
		public static CalamityPactManager Instance => _instance ??= new CalamityPactManager();

		// 记录当前局内已勾选/激活的契约 ID
		private readonly HashSet<string> _activePactIds = new();

		public event Action? OnPactsChanged;

		/// <summary>
		/// 切换契约勾选状态
		/// </summary>
		public void TogglePact(string pactId)
		{
			if (_activePactIds.Contains(pactId))
			{
				_activePactIds.Remove(pactId);
			}
			else
			{
				_activePactIds.Add(pactId);
			}
			OnPactsChanged?.Invoke();
		}

		/// <summary>
		/// 判断指定契约当前是否已激活
		/// </summary>
		public bool IsActive(string pactId)
		{
			return _activePactIds.Contains(pactId);
		}

		/// <summary>
		/// 获取当前已激活的所有契约数量（热度总等级）
		/// </summary>
		public int GetTotalHeatLevel()
		{
			return _activePactIds.Count;
		}

		/// <summary>
		/// 结算收益加成倍率 (基于 JSON 中定义的 scrapBonusMultiplier 动态求和)
		/// </summary>
		public float GetScoreRewardMultiplier()
		{
			float totalBonus = 0.0f;
			foreach (var id in _activePactIds)
			{
				if (DataManager.Instance.Pacts.TryGet(id, out var pact))
				{
					totalBonus += pact!.ScrapBonusMultiplier;
				}
			}
			return 1.0f + totalBonus;
		}

		/// <summary>
		/// 获取当前已激活的所有契约数据列表
		/// </summary>
		public List<CalamityPactDef> GetActivePacts()
		{
			var result = new List<CalamityPactDef>();
			foreach (var id in _activePactIds)
			{
				if (DataManager.Instance.Pacts.TryGet(id, out var pact))
				{
					result.Add(pact!);
				}
			}
			return result;
		}

		/// <summary>
		/// 清空所有激活状态（新一轮出击或战役重置时调用）
		/// </summary>
		public void Reset()
		{
			_activePactIds.Clear();
			OnPactsChanged?.Invoke();
		}
	}
}
