using System;
using Godot;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.UI.CombatHUD;
using BreakerProtocol.UI.Events;
using BreakerProtocol.UI.Market;
using BreakerProtocol.UI.Meta;
using BreakerProtocol.UI.SectorMap;
using BreakerProtocol.UI.Settlement;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Events;
using BreakerProtocol.World.Meta;
using BreakerProtocol.World.Sector;
using BreakerProtocol.World.Session;
using BreakerProtocol.World.Settlement;

namespace BreakerProtocol.World.Director
{
	public enum GameFlowState
	{
		Hangar,           // 母港选船与科研科技树
		SectorNavigation, // DAG 分支星图跃迁选择
		CombatBattle,     // 实战遭遇战 (常规/精英/小行星)
		BlackMarketShop,  // 深空黑市改装终端
		AnomalyDialogue,  // 叙事异象交互
		BossEncounter,    // 泰坦熔炉 Boss 战
		RunSettlement     // 战役结算与数据入账
	}

	/// <summary>
	/// 《断路协议》全流程宏观游戏循环调度总控
	/// </summary>
	public partial class GameDirector : Node
	{
		public static GameDirector Instance { get; private set; } = null!;

		public GameFlowState CurrentState { get; private set; } = GameFlowState.SectorNavigation;
		public ShipEntity PlayerShip { get; set; } = null!;

		// UI 引用
		public SectorMapUI MapUI { get; set; } = null!;
		public BlackMarketShopUI MarketUI { get; set; } = null!;
		public SpaceEventDialogueUI EventUI { get; set; } = null!;
		public MetaTechTreeUI MetaTechUI { get; set; } = null!;
		public RunSummaryUI SummaryUI { get; set; } = null!;
		public CombatHUD CombatHUD { get; set; } = null!;

		public event Action<GameFlowState>? OnStateChanged;

		public override void _Ready()
		{
			Instance = this;
		}

		public void SwitchState(GameFlowState newState)
		{
			CurrentState = newState;

			// 统一管理所有界面的显隐
			if (MapUI != null) MapUI.Visible = (newState == GameFlowState.SectorNavigation);
			if (MarketUI != null) MarketUI.Visible = (newState == GameFlowState.BlackMarketShop);
			if (EventUI != null) EventUI.Visible = (newState == GameFlowState.AnomalyDialogue);
			if (MetaTechUI != null) MetaTechUI.Visible = (newState == GameFlowState.Hangar);
			if (SummaryUI != null) SummaryUI.Visible = (newState == GameFlowState.RunSettlement);
			if (CombatHUD != null) CombatHUD.Visible = (newState == GameFlowState.CombatBattle || newState == GameFlowState.BossEncounter);

			OnStateChanged?.Invoke(newState);
		}

		/// <summary>
		/// 从星图节点进入对应战术副本
		/// </summary>
		public void EnterEncounterFromSector(SectorNodeType nodeType)
		{
			switch (nodeType)
			{
				case SectorNodeType.Market:
					MarketUI?.Initialize(PlayerShip);
					SwitchState(GameFlowState.BlackMarketShop);
					break;

				case SectorNodeType.Event:
					EventUI?.OpenEvent(SpaceEventDatabase.GetRandomEvent(), PlayerShip);
					SwitchState(GameFlowState.AnomalyDialogue);
					break;

				case SectorNodeType.Boss:
					SwitchState(GameFlowState.BossEncounter);
					break;

				default: // Combat / Elite / Repair
					SwitchState(GameFlowState.CombatBattle);
					break;
			}
		}

		/// <summary>
		/// 战斗或异象完成，返回星图继续跃迁
		/// </summary>
		public void ReturnToSectorMap()
		{
			SwitchState(GameFlowState.SectorNavigation);
		}

		/// <summary>
		/// 战役通关或战败，触发结算总控
		/// </summary>
		public void TriggerGameOver(RunEndingType ending)
		{
			int clearedCols = (MapUI != null && MapUI.Graph != null)
				? Mathf.Max(1, (int)MapUI.Graph.PursuitWavefrontColumn + 1)
				: (GameRunSession.Instance.IsSessionActive ? GameRunSession.Instance.CurrentStats.SectorsCleared : 4);

			var stats = new RunStatistics
			{
				Ending = ending,
				SectorsCleared = clearedCols,
				StandardEnemiesKilled = 14,
				ElitesKilled = 2,
				BossesKilled = ending == RunEndingType.Victory ? 1 : 0,
				TotalScrapsEarned = PlayerEconomyManager.Instance.Scraps,
				ComputeCoresEarned = PlayerEconomyManager.Instance.ComputeCores,
				FinalHullIntegrityPercent = 85.0f
			};

			SummaryUI?.OpenSummary(stats);
			SwitchState(GameFlowState.RunSettlement);
		}
	}
}
