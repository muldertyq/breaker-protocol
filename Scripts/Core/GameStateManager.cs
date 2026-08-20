using System;
using Godot;
using BreakerProtocol.Data.Persistence;
using BreakerProtocol.Ship;
using BreakerProtocol.UI.CombatHUD;
using BreakerProtocol.UI.Events;
using BreakerProtocol.UI.Hangar;
using BreakerProtocol.UI.Market;
using BreakerProtocol.UI.Menu;
using BreakerProtocol.UI.Meta;
using BreakerProtocol.UI.Sandbox;
using BreakerProtocol.UI.SectorMap;
using BreakerProtocol.UI.Settlement;
using BreakerProtocol.World.Director;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Events;
using BreakerProtocol.World.Meta;
using BreakerProtocol.World.Pacts;
using BreakerProtocol.World.Sector;
using BreakerProtocol.World.Session;
using BreakerProtocol.World.Settlement;

namespace BreakerProtocol.Core
{
	public enum GameState
	{
		MainMenu,         // 游戏主标题菜单
		FleetHangar,      // 母港机库选船出征
		HangarMetaTech,   // 母港科研局 (Meta 科技树)
		SandboxBay,       // 虚拟风洞测试靶场
		SectorMap,        // DAG 航路分支跃迁星图
		Combat,           // 战术实时空战
		BlackMarket,      // 废土黑市交易终端
		AnomalyDialogue,  // 深空随机叙事异象
		RunSettlement     // 战役胜败综合评分结算
	}

	/// <summary>
	/// 全局游戏状态机总控中枢
	/// </summary>
	public class GameStateManager
	{
		private static GameStateManager? _instance;
		public static GameStateManager Instance => _instance ??= new GameStateManager();

		public GameState CurrentState { get; private set; } = GameState.MainMenu;
		public event Action<GameState, GameState>? OnStateChanged;

		public ShipEntity? PlayerShip { get; set; }

		// UI 视图句柄容器
		public MainMenuUI? MainMenuUI { get; set; }
		public FleetHangarUI? FleetHangarUI { get; set; }
		public SectorMapUI? MapUI { get; set; }
		public BlackMarketShopUI? MarketUI { get; set; }
		public SpaceEventDialogueUI? EventUI { get; set; }
		public MetaTechTreeUI? MetaTechUI { get; set; }
		public SandboxBayUI? SandboxUI { get; set; }
		public RunSummaryUI? SummaryUI { get; set; }
		public CombatHUD? CombatHUD { get; set; }

		private GameStateManager() { }

		/// <summary>
		/// 一键自动绑定全域 UI 的导航返回与跳转事件
		/// </summary>
		public void BindAllUIEvents()
		{
			// 1. 主菜单 ➔ 4 大子功能
			if (MainMenuUI != null)
			{
				MainMenuUI.OnNewRunRequested += () => SwitchState(GameState.FleetHangar, true, "✦ 正在对接母港出征战备机库 ✦");
				MainMenuUI.OnContinueRunRequested += () => ContinueSavedRun();
				MainMenuUI.OnHangarRequested += () => SwitchState(GameState.HangarMetaTech, true, "✦ 正在连线母港科研总局 ✦");
				MainMenuUI.OnSandboxRequested += () => SwitchState(GameState.SandboxBay, true, "✦ 正在启动虚拟风洞靶场 ✦");
			}

			// 2. 选船机库 ➔ 启航或退回主菜单
			if (FleetHangarUI != null)
			{
				FleetHangarUI.OnShipSelectedAndEngage += (blueprintId) => StartNewRun(blueprintId);
				FleetHangarUI.OnBackToMainMenu += () => SwitchState(GameState.MainMenu);
			}

			// 3. 星图 ➔ 返回空战 / 关闭星图
			if (MapUI != null)
			{
				MapUI.OnBackToGameRequested += () => SwitchState(GameState.Combat, true, "✦ 退出星图，校准战术交火空域 ✦");
			}

			// 4. 母港科研局 ➔ 退回主菜单
			if (MetaTechUI != null)
			{
				MetaTechUI.OnBackRequested += () => SwitchState(GameState.MainMenu);
			}

			// 5. 虚拟风洞 ➔ 退回主菜单
			if (SandboxUI != null)
			{
				SandboxUI.OnBackToMainMenu += () => SwitchState(GameState.MainMenu);
			}

			// 6. 废土黑市 ➔ 离港退回星图
			if (MarketUI != null)
			{
				MarketUI.OnCloseRequested += () => SwitchState(GameState.SectorMap, true, "✦ 正在离开空间站并校准航向 ✦");
			}

			// 7. 异象事件 ➔ 结算后退回星图
			if (EventUI != null)
			{
				EventUI.OnEventResolved += (outcome) => SwitchState(GameState.SectorMap);
			}

			// 8. 结算面板 ➔ 3 大跳转
			if (SummaryUI != null)
			{
				SummaryUI.OnNavigateToMetaTech += () => SwitchState(GameState.HangarMetaTech);
				SummaryUI.OnStartNewRun += () => SwitchState(GameState.FleetHangar);
				SummaryUI.OnReturnToMainMenu += () => SwitchState(GameState.MainMenu);
			}
		}

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
				case GameState.BlackMarket:
				case GameState.AnomalyDialogue:
					GameRunSession.Instance.SaveCurrentRun();
					break;
			}
		}

		private void EnterState(GameState state)
		{
			if (MainMenuUI != null) MainMenuUI.Visible = (state == GameState.MainMenu);
			if (FleetHangarUI != null) FleetHangarUI.Visible = (state == GameState.FleetHangar);
			if (MapUI != null) MapUI.Visible = (state == GameState.SectorMap);
			if (MarketUI != null) MarketUI.Visible = (state == GameState.BlackMarket);
			if (EventUI != null) EventUI.Visible = (state == GameState.AnomalyDialogue);
			if (MetaTechUI != null) MetaTechUI.Visible = (state == GameState.HangarMetaTech);
			if (SandboxUI != null) SandboxUI.Visible = (state == GameState.SandboxBay);
			if (SummaryUI != null) SummaryUI.Visible = (state == GameState.RunSettlement);
			if (CombatHUD != null) CombatHUD.Visible = (state == GameState.Combat);

			switch (state)
			{
				case GameState.SectorMap:
					if (GameRunSession.Instance.CurrentSectorGraph != null && MapUI != null)
					{
						MapUI.SetGraph(GameRunSession.Instance.CurrentSectorGraph);
					}
					GameRunSession.Instance.SaveCurrentRun();
					break;

				case GameState.BlackMarket:
					if (PlayerShip != null) MarketUI?.Initialize(PlayerShip);
					break;

				case GameState.HangarMetaTech:
					SaveManager.Instance.LoadMeta();
					break;
			}

			GD.PrintRich($"[color=cyan][GameStateManager] 状态流转完成: [{CurrentState}][/color]");
		}

		public void StartNewRun(string initialBlueprintId = "bp_hf_m_anvil")
		{
			if (PlayerShip != null)
			{
				GameRunSession.Instance.BindPlayerShip(PlayerShip);
			}
			GameRunSession.Instance.InitializeNewRun(initialBlueprintId);

			if (MapUI != null && GameRunSession.Instance.CurrentSectorGraph != null)
			{
				MapUI.SetGraph(GameRunSession.Instance.CurrentSectorGraph);
			}

			SwitchState(GameState.SectorMap, true, "✦ 正在生成未知深空星区拓扑 ✦");
		}

		public bool ContinueSavedRun()
		{
			if (PlayerShip == null) return false;

			if (GameRunSession.Instance.RestoreSavedRun(PlayerShip))
			{
				if (MapUI != null && GameRunSession.Instance.CurrentSectorGraph != null)
				{
					MapUI.SetGraph(GameRunSession.Instance.CurrentSectorGraph);
				}
				SwitchState(GameState.SectorMap, true, "✦ 正在重构舰载战损与航路数据 ✦");
				return true;
			}

			return false;
		}

		public void TriggerGameOver(RunEndingType ending)
		{
			var finalStats = GameRunSession.Instance.TerminateRun(ending);
			SummaryUI?.OpenSummary(finalStats);
			SwitchState(GameState.RunSettlement, true, "✦ 战役信号归档结算中 ✦");
		}
	}
}
