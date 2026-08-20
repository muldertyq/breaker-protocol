using Godot;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
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
using BreakerProtocol.World.Sector;
using BreakerProtocol.World.Settlement;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-42 演练场：全局游戏状态机与场景无缝过渡系统验证中枢
	/// </summary>
	public partial class Test_Task42 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private BulletManager _bulletManager = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private SceneTransitionManager _transitionManager = null!;

		// 全景 UI
		private MainMenuUI _mainMenuUI = null!;
		private SectorMapUI _mapUI = null!;
		private BlackMarketShopUI _marketUI = null!;
		private SpaceEventDialogueUI _eventUI = null!;
		private MetaTechTreeUI _metaTechUI = null!;
		private RunSummaryUI _summaryUI = null!;
		private CombatHUD _combatHUD = null!;
		private RichTextLabel _topBannerLabel = null!;

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			// 1. 初始化转场管理器
			_transitionManager = new SceneTransitionManager();
			AddChild(_transitionManager);

			// 2. 初始化战舰与摄像机
			PlayerEconomyManager.Instance.Reset(initialScraps: 200, initialCores: 1);
			_playerShip = new ShipEntity
			{
				Name = "PlayerShip_T42",
				Position = Vector2.Zero
			};
			_playerShip.AddToGroup("Player");
			_playerShip.AddToGroup("Ship");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var bp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, bp!);
			}

			_camera = new CombatCameraController { TargetShip = _playerShip };
			AddChild(_camera);
			_juice.BindCamera(_camera);

			// 3. 构建 UI 并绑定状态机
			CreateAllUIs();
			BindGameStateManager();

			// 4. 初始直接切入主菜单 (关闭多余 HUD)
			GameStateManager.Instance.SwitchState(GameState.MainMenu, false);
		}

		private void CreateAllUIs()
		{
			var canvas = new CanvasLayer { Layer = 10 };
			AddChild(canvas);

			_combatHUD = new CombatHUD { TargetShip = _playerShip };
			_combatHUD.Visible = false;
			canvas.AddChild(_combatHUD);

			_mapUI = new SectorMapUI();
			_mapUI.OnNodeSelected += HandleNodeSelectedOnMap;
			_mapUI.Visible = false;
			canvas.AddChild(_mapUI);

			_marketUI = new BlackMarketShopUI();
			_marketUI.Visible = false;
			canvas.AddChild(_marketUI);

			_eventUI = new SpaceEventDialogueUI();
			_eventUI.OnEventResolved += (outcome) => GameStateManager.Instance.SwitchState(GameState.SectorMap);
			_eventUI.Visible = false;
			canvas.AddChild(_eventUI);

			_metaTechUI = new MetaTechTreeUI();
			_metaTechUI.Visible = false;
			canvas.AddChild(_metaTechUI);

			_summaryUI = new RunSummaryUI();
			_summaryUI.OnNavigateToMetaTech += () => GameStateManager.Instance.SwitchState(GameState.Hangar);
			_summaryUI.OnStartNewRun += () => GameStateManager.Instance.StartNewRun();
			_summaryUI.Visible = false;
			canvas.AddChild(_summaryUI);

			// 顶部全息测试快捷键提示栏
			_topBannerLabel = new RichTextLabel
			{
				Position = new Vector2(20, 10),
				Size = new Vector2(1240, 60),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_topBannerLabel.AddThemeFontSizeOverride("normal_font_size", 13);
			canvas.AddChild(_topBannerLabel);

			// 主菜单置于最顶层
			_mainMenuUI = new MainMenuUI();
			_mainMenuUI.OnNewRunRequested += () => GameStateManager.Instance.StartNewRun();
			_mainMenuUI.OnContinueRunRequested += () => GameStateManager.Instance.ContinueSavedRun();
			_mainMenuUI.OnHangarRequested += () => GameStateManager.Instance.SwitchState(GameState.Hangar);
			canvas.AddChild(_mainMenuUI);
		}

		private void BindGameStateManager()
		{
			var gsm = GameStateManager.Instance;
			gsm.PlayerShip = _playerShip;
			gsm.MainMenuUI = _mainMenuUI;
			gsm.MapUI = _mapUI;
			gsm.MarketUI = _marketUI;
			gsm.EventUI = _eventUI;
			gsm.MetaTechUI = _metaTechUI;
			gsm.SummaryUI = _summaryUI;
			gsm.CombatHUD = _combatHUD;
		}

		private void HandleNodeSelectedOnMap(SectorNode node)
		{
			switch (node.Type)
			{
				case SectorNodeType.Market:
					GameStateManager.Instance.SwitchState(GameState.BlackMarket, true, "✦ 对接废土黑市空间站 ✦");
					break;
				case SectorNodeType.Event:
					_eventUI.OpenEvent(SpaceEventDatabase.GetRandomEvent(), _playerShip);
					GameStateManager.Instance.SwitchState(GameState.AnomalyDialogue, true, "✦ 扫描到深空异象信标 ✦");
					break;
				default:
					GameStateManager.Instance.SwitchState(GameState.Combat, true, "✦ 遭遇敌方巡逻舰队 ✦");
					break;
			}
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (@event is InputEventKey ek && ek.Pressed && !ek.Echo)
			{
				var gsm = GameStateManager.Instance;

				if (ek.Keycode == Key.Escape)
				{
					if (gsm.CurrentState == GameState.MainMenu)
						gsm.SwitchState(GameState.SectorMap);
					else
						gsm.SwitchState(GameState.MainMenu);
				}
				else if (ek.Keycode == Key.M)
				{
					gsm.SwitchState(GameState.SectorMap);
				}
				else if (ek.Keycode == Key.B)
				{
					gsm.SwitchState(GameState.BlackMarket);
				}
				else if (ek.Keycode == Key.H)
				{
					gsm.SwitchState(GameState.Hangar);
				}
				else if (ek.Keycode == Key.V)
				{
					gsm.TriggerGameOver(RunEndingType.Victory);
				}
				else if (ek.Keycode == Key.K)
				{
					gsm.TriggerGameOver(RunEndingType.Defeat_Destroyed);
				}
			}
		}

		public override void _Process(double delta)
		{
			if (Godot.Input.IsMouseButtonPressed(MouseButton.Left) && GameStateManager.Instance.CurrentState == GameState.Combat)
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			UpdateBanner();
		}

		private void UpdateBanner()
		{
			var gsm = GameStateManager.Instance;
			var eco = PlayerEconomyManager.Instance;

			string stateName = gsm.CurrentState switch
			{
				GameState.MainMenu        => "[color=cyan]主标题菜单 (MainMenu)[/color]",
				GameState.Hangar          => "[color=gold]母港科研局 (Hangar)[/color]",
				GameState.SectorMap       => "[color=lime]星区跃迁星图 (SectorMap)[/color]",
				GameState.Combat          => "[color=crimson]战术空战模式 (Combat)[/color]",
				GameState.BlackMarket     => "[color=yellow]废土黑市终端 (BlackMarket)[/color]",
				GameState.AnomalyDialogue => "[color=magenta]深空异象日志 (AnomalyDialogue)[/color]",
				GameState.RunSettlement   => "[color=green]战役综合结算 (RunSettlement)[/color]",
				_                         => "[color=white]未知[/color]"
			};

			_topBannerLabel.Text =
				$"[b][color=yellow]【TASK-42 状态机测试】[/color][/b] 状态: {stateName} | " +
				$"资产: [color=yellow]{eco.Scraps} ⚙[/color] [color=cyan]{eco.ComputeCores} 💠[/color] | " +
				$"局内存档: {(SaveManager.Instance.HasActiveRunSave() ? "[color=lime]已保存[/color]" : "[color=gray]无[/color]")}\n" +
				$"[color=gray][快捷键]: ESC 主菜单 | M 星图 | B 黑市 | H 母港科研 | V 胜利结算 | K 阵亡结算[/color]";
		}
	}
}
