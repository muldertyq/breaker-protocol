using System;
using Godot;
using BreakerProtocol.Data.Persistence;
using BreakerProtocol.Ship;
using BreakerProtocol.UI.CombatHUD;
using BreakerProtocol.UI.Events;
using BreakerProtocol.UI.Market;
using BreakerProtocol.UI.Menu;
using BreakerProtocol.UI.Meta;
using BreakerProtocol.UI.Settlement;
using BreakerProtocol.World.Director;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Events;
using BreakerProtocol.World.Meta;
using BreakerProtocol.World.Pacts;
using BreakerProtocol.World.Sector;
using BreakerProtocol.World.Settlement;

namespace BreakerProtocol.Core
{
	/// <summary>
	/// 全局游戏生命周期核心状态枚举
	/// </summary>
	public enum GameState
	{
		MainMenu,         // 游戏主标题菜单
		Hangar,           // 母港科研局与出击整备
		SectorMap,        // DAG 航路分支跃迁星图
		Combat,           // 战术实时空战
		BlackMarket,      // 废土黑市交易终端
		AnomalyDialogue,  // 深空随机叙事异象
		RefitBay,         // 战场临时紧急改装
		RunSettlement     // 战役胜败综合评分结算
	}

	/// <summary>
	/// 全局游戏状态机中枢 (管理状态切换、UI 互斥、自动存盘与场景生命周期)
	/// </summary>
	public class GameStateManager
	{
		private static GameStateManager? _instance;
		public static GameStateManager Instance => _instance ??= new GameStateManager();

		public GameState CurrentState { get; private set; } = GameState.MainMenu;
		public event Action<GameState, GameState>? OnStateChanged;

		// 核心实体与系统引用
		public ShipEntity? PlayerShip { get; set; }
		public SectorGraph? CurrentSectorGraph { get; set; }

		// UI 视图句柄容器
		public MainMenuUI? MainMenuUI { get; set; }
		public SectorMapUI? MapUI { get; set; }
		public BlackMarketShopUI? MarketUI { get; set; }
		public SpaceEventDialogueUI? EventUI { get; set; }
		public MetaTechTreeUI? MetaTechUI { get; set; }
		public RunSummaryUI? SummaryUI { get; set; }
		public CombatHUD? CombatHUD { get; set; }

		private GameStateManager() { }

		/// <summary>
		/// 安全切换全局游戏状态（带全屏无缝过渡动画）
		/// </summary>
		public void SwitchState(GameState newState, bool useTransition = true, string? transitionHint = null)
		{
			if (CurrentState == newState && newState != GameState.MainMenu) return;

			GameState previousState = CurrentState;

			if (useTransition && SceneTransitionManager.Instance != null)
			{
				SceneTransitionManager.Instance.Transition(() =>
				{
					ApplyStateSwitch(previousState, newState);
				}, 0.25f, transitionHint);
			}
			else
			{
				ApplyStateSwitch(previousState, newState);
			}
		}

		private void ApplyStateSwitch(GameState previousState, GameState newState)
		{
			ExitState(previousState);
			CurrentState = newState;
			EnterState(newState);
			OnStateChanged?.Invoke(previousState, newState);
		}

		private void ExitState(GameState state)
		{
			switch (state)
			{
				case GameState.Combat:
					// 离开战斗态时自动向磁盘备份战损现场
					if (PlayerShip != null && CurrentSectorGraph != null)
					{
						SaveManager.Instance.SaveCurrentRun(PlayerShip, CurrentSectorGraph);
					}
					break;
			}
		}

		private void EnterState(GameState state)
		{
			// 1. UI 互斥显隐同步
			if (MainMenuUI != null) MainMenuUI.Visible = (state == GameState.MainMenu);
			if (MapUI != null) MapUI.Visible = (state == GameState.SectorMap);
			if (MarketUI != null) MarketUI.Visible = (state == GameState.BlackMarket);
			if (EventUI != null) EventUI.Visible = (state == GameState.AnomalyDialogue);
			if (MetaTechUI != null) MetaTechUI.Visible = (state == GameState.Hangar);
			if (SummaryUI != null) SummaryUI.Visible = (state == GameState.RunSettlement);
			if (CombatHUD != null) CombatHUD.Visible = (state == GameState.Combat);

			// 2. 状态进入特化逻辑
			switch (state)
			{
				case GameState.SectorMap:
					// 跃迁到星图时触发自动存盘
					if (PlayerShip != null && CurrentSectorGraph != null)
					{
						SaveManager.Instance.SaveCurrentRun(PlayerShip, CurrentSectorGraph);
					}
					break;

				case GameState.BlackMarket:
					if (PlayerShip != null) MarketUI?.Initialize(PlayerShip);
					break;

				case GameState.Hangar:
					SaveManager.Instance.LoadMeta();
					break;
			}

			GD.PrintRich($"[color=cyan][GameStateManager] 状态流转成功: [{CurrentState}][/color]");
		}

		/// <summary>
		/// 开启一轮全新星区战役
		/// </summary>
		public void StartNewRun(string initialBlueprintId = "bp_hf_m_anvil")
		{
			SaveManager.Instance.DeleteRunSave();
			PlayerEconomyManager.Instance.Reset(initialScraps: 200, initialCores: 1);
			CalamityPactManager.Instance.Reset();

			// 1. 重构玩家飞船并注入 Meta 增益
			if (PlayerShip != null)
			{
				if (DataManager.Instance.Blueprints.TryGet(initialBlueprintId, out var bp) && bp != null)
				{
					ShipBlueprintLoader.ApplyBlueprint(PlayerShip, bp);
				}
				MetaProgressionManager.Instance.ApplyMetaBuffsToNewRun(PlayerShip);
			}

			// 2. 生成全新星图
			CurrentSectorGraph = SectorMapGenerator.GenerateSector(totalColumns: 8);
			MapUI?.SetGraph(CurrentSectorGraph);

			SwitchState(GameState.SectorMap, true, "✦ 正在生成未知深空星区拓扑 ✦");
		}

		/// <summary>
		/// 从本地存档无损恢复战役进度
		/// </summary>
		public bool ContinueSavedRun()
		{
			if (!SaveManager.Instance.HasActiveRunSave() || PlayerShip == null) return false;

			if (SaveManager.Instance.RestoreCurrentRun(PlayerShip, out var restoredGraph))
			{
				CurrentSectorGraph = restoredGraph;
				if (CurrentSectorGraph != null && MapUI != null)
				{
					MapUI.SetGraph(CurrentSectorGraph);
				}
				SwitchState(GameState.SectorMap, true, "✦ 正在重构舰载战损与航路数据 ✦");
				return true;
			}

			return false;
		}

		/// <summary>
		/// 战役胜利或阵亡结算
		/// </summary>
		public void TriggerGameOver(RunEndingType ending)
		{
			var stats = new RunStatistics
			{
				Ending = ending,
				SectorsCleared = CurrentSectorGraph?.NodesByColumn.FindIndex(col => col.Exists(n => n.Id == CurrentSectorGraph.CurrentNodeId)) + 1 ?? 4,
				StandardEnemiesKilled = 12,
				ElitesKilled = 2,
				BossesKilled = ending == RunEndingType.Victory ? 1 : 0,
				TotalScrapsEarned = PlayerEconomyManager.Instance.Scraps,
				ComputeCoresEarned = PlayerEconomyManager.Instance.ComputeCores,
				FinalHullIntegrityPercent = 80.0f
			};

			SaveManager.Instance.DeleteRunSave();
			SummaryUI?.OpenSummary(stats);
			SwitchState(GameState.RunSettlement, true, "✦ 战役信号归档结算中 ✦");
		}
	}
}
