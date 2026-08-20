using Godot;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Persistence;
using BreakerProtocol.Ship;
using BreakerProtocol.UI.CombatHUD;
using BreakerProtocol.UI.Events;
using BreakerProtocol.UI.Hangar;
using BreakerProtocol.UI.Market;
using BreakerProtocol.UI.Menu;
using BreakerProtocol.UI.Meta;
using BreakerProtocol.UI.Sandbox;
using BreakerProtocol.UI.Settlement;
using BreakerProtocol.World.Director;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Events;
using BreakerProtocol.World.Sector;
using BreakerProtocol.World.Settlement;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-43 演练场：母港主菜单、选船出征与科研总装界面验证中枢
	/// </summary>
	public partial class Test_Task43 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private BulletManager _bulletManager = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private SceneTransitionManager _transitionManager = null!;

		private MainMenuUI _mainMenuUI = null!;
		private FleetHangarUI _fleetHangarUI = null!;
		private SectorMapUI _mapUI = null!;
		private BlackMarketShopUI _marketUI = null!;
		private SpaceEventDialogueUI _eventUI = null!;
		private MetaTechTreeUI _metaTechUI = null!;
		private SandboxBayUI _sandboxUI = null!;
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
				Name = "PlayerShip_T43",
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

			// 3. 构建全景 UI 并注入 GameStateManager
			CreateAllUIs();
			BindGameStateManager();

			// 4. 初始切入主菜单
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

			_sandboxUI = new SandboxBayUI();
			_sandboxUI.Visible = false;
			canvas.AddChild(_sandboxUI);

			_summaryUI = new RunSummaryUI();
			_summaryUI.OnNavigateToMetaTech += () => GameStateManager.Instance.SwitchState(GameState.HangarMetaTech);
			_summaryUI.OnStartNewRun += () => GameStateManager.Instance.SwitchState(GameState.FleetHangar);
			_summaryUI.Visible = false;
			canvas.AddChild(_summaryUI);

			_fleetHangarUI = new FleetHangarUI();
			_fleetHangarUI.OnShipSelectedAndEngage += (blueprintId) => GameStateManager.Instance.StartNewRun(blueprintId);
			_fleetHangarUI.OnBackToMainMenu += () => GameStateManager.Instance.SwitchState(GameState.MainMenu);
			_fleetHangarUI.Visible = false;
			canvas.AddChild(_fleetHangarUI);

			_mainMenuUI = new MainMenuUI();
			_mainMenuUI.OnNewRunRequested += () => GameStateManager.Instance.SwitchState(GameState.FleetHangar, true, "✦ 正在对接母港出征战备机库 ✦");
			_mainMenuUI.OnContinueRunRequested += () => GameStateManager.Instance.ContinueSavedRun();
			_mainMenuUI.OnHangarRequested += () => GameStateManager.Instance.SwitchState(GameState.HangarMetaTech, true, "✦ 正在连线母港科研总局 ✦");
			_mainMenuUI.OnSandboxRequested += () => GameStateManager.Instance.SwitchState(GameState.SandboxBay, true, "✦ 正在启动虚拟风洞靶场 ✦");
			canvas.AddChild(_mainMenuUI);

			_topBannerLabel = new RichTextLabel
			{
				Position = new Vector2(20, 10),
				Size = new Vector2(1240, 60),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_topBannerLabel.AddThemeFontSizeOverride("normal_font_size", 13);
			canvas.AddChild(_topBannerLabel);
		}

		private void BindGameStateManager()
		{
			var gsm = GameStateManager.Instance;
			gsm.PlayerShip = _playerShip;
			gsm.MainMenuUI = _mainMenuUI;
			gsm.FleetHangarUI = _fleetHangarUI;
			gsm.MapUI = _mapUI;
			gsm.MarketUI = _marketUI;
			gsm.EventUI = _eventUI;
			gsm.MetaTechUI = _metaTechUI;
			gsm.SandboxUI = _sandboxUI;
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
				else if (ek.Keycode == Key.Key1)
				{
					gsm.SwitchState(GameState.FleetHangar);
				}
				else if (ek.Keycode == Key.Key2)
				{
					gsm.SwitchState(GameState.HangarMetaTech);
				}
				else if (ek.Keycode == Key.Key3)
				{
					gsm.SwitchState(GameState.SandboxBay);
				}
				else if (ek.Keycode == Key.M)
				{
					gsm.SwitchState(GameState.SectorMap);
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
				GameState.FleetHangar     => "[color=gold]选船机库 (FleetHangar)[/color]",
				GameState.HangarMetaTech  => "[color=yellow]母港科研局 (MetaTech)[/color]",
				GameState.SandboxBay      => "[color=magenta]风洞测试场 (SandboxBay)[/color]",
				GameState.SectorMap       => "[color=lime]星区跃迁星图 (SectorMap)[/color]",
				GameState.Combat          => "[color=crimson]战术空战模式 (Combat)[/color]",
				_                         => "[color=white]其他[/color]"
			};

			_topBannerLabel.Text =
				$"[b][color=yellow]【TASK-43 选船出征与母港机库演练场】[/color][/b] 当前状态: {stateName} | " +
				$"当前战舰构件数: [color=cyan]{_playerShip.Grid.Modules.Count}[/color] | " +
				$"资产: [color=yellow]{eco.Scraps} ⚙[/color] [color=cyan]{eco.ComputeCores} 💠[/color]\n" +
				$"[color=gray][快捷流转]: [1] 选船机库 | [2] 母港科研 | [3] 虚拟风洞 | [M] 跃迁星图 | [ESC] 主菜单[/color]";
		}
	}
}
