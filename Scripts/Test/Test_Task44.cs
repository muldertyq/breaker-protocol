using System.Linq;
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
using BreakerProtocol.UI.SectorMap;
using BreakerProtocol.World.Director;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Events;
using BreakerProtocol.World.Sector;
using BreakerProtocol.World.Session;
using BreakerProtocol.World.Settlement;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-44 演练场：战局上下文实体与全域导航返回总线验证中枢
	/// </summary>
	public partial class Test_Task44 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private BulletManager _bulletManager = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private SceneTransitionManager _transitionManager = null!;

		// 全景 UI
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

		private string _sessionLog = "🚀 战局会话与全域返回总线就绪。所有子界面均支持 [◀ 返回按钮] 与 [ESC 键] 畅通导航！";

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			// 1. 初始化转场
			_transitionManager = new SceneTransitionManager();
			AddChild(_transitionManager);

			// 2. 初始化战舰与摄像机
			_playerShip = new ShipEntity
			{
				Name = "PlayerShip_T44",
				Position = Vector2.Zero
			};
			_playerShip.AddToGroup("Player");
			_playerShip.AddToGroup("Ship");
			AddChild(_playerShip);

			_camera = new CombatCameraController { TargetShip = _playerShip };
			AddChild(_camera);
			_juice.BindCamera(_camera);

			// 3. 构建全景 UI 并注入中枢
			CreateAllUIs();
			BindManagers();

			// 4. 默认启动主菜单
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
			_eventUI.Visible = false;
			canvas.AddChild(_eventUI);

			_metaTechUI = new MetaTechTreeUI();
			_metaTechUI.Visible = false;
			canvas.AddChild(_metaTechUI);

			_sandboxUI = new SandboxBayUI();
			_sandboxUI.Visible = false;
			canvas.AddChild(_sandboxUI);

			_summaryUI = new RunSummaryUI();
			_summaryUI.Visible = false;
			canvas.AddChild(_summaryUI);

			_fleetHangarUI = new FleetHangarUI();
			_fleetHangarUI.Visible = false;
			canvas.AddChild(_fleetHangarUI);

			_mainMenuUI = new MainMenuUI();
			canvas.AddChild(_mainMenuUI);

			_topBannerLabel = new RichTextLabel
			{
				Position = new Vector2(20, 10),
				Size = new Vector2(1240, 75),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_topBannerLabel.AddThemeFontSizeOverride("normal_font_size", 13);
			canvas.AddChild(_topBannerLabel);
		}

		private void BindManagers()
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

			// 自动挂载所有 UI 的返回与路由逻辑
			gsm.BindAllUIEvents();
			GameRunSession.Instance.BindPlayerShip(_playerShip);
		}

		private void HandleNodeSelectedOnMap(SectorNode node)
		{
			GameRunSession.Instance.AdvanceToNode(node);

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
					GameStateManager.Instance.SwitchState(GameState.Combat, true, "✦ 遭遇敌方战术舰队 ✦");
					break;
			}
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (@event is InputEventKey ek && ek.Pressed && !ek.Echo)
			{
				var gsm = GameStateManager.Instance;
				var session = GameRunSession.Instance;

				// [按 1 键]: 制造战损并记录
				if (ek.Keycode == Key.Key1)
				{
					InflictTraumaAndRecord();
				}
				// [按 2 键]: 击坠常规机
				else if (ek.Keycode == Key.Key2)
				{
					session.RecordEnemyKilled("Scout");
					PlayerEconomyManager.Instance.AddScraps(65);
					_sessionLog = $"[color=lime]⚔️ 击坠先锋机 (+65 ⚙)，已歼敌 {session.CurrentStats.StandardEnemiesKilled} 架！[/color]";
				}
				// [按 3 键]: 击杀精英
				else if (ek.Keycode == Key.Key3)
				{
					session.RecordEnemyKilled("Elite");
					PlayerEconomyManager.Instance.AddComputeCores(1);
					_sessionLog = $"[color=gold]👑 猎杀精英 (+1 💠)，已击溃精英 {session.CurrentStats.ElitesKilled} 艘！[/color]";
				}
				// [按 V 键]: 胜利结算
				else if (ek.Keycode == Key.V)
				{
					gsm.TriggerGameOver(RunEndingType.Victory);
				}
			}
		}

		private void InflictTraumaAndRecord()
		{
			var session = GameRunSession.Instance;
			var modules = _playerShip.Grid.Modules.ToList();
			if (modules.Count > 0)
			{
				var target = modules[(int)GD.RandRange(0, modules.Count - 1)];
				float dmg = 70.0f;
				target.CurrentHp = Mathf.Max(5.0f, target.CurrentHp - dmg);
				session.RecordDamageTaken(dmg);
				session.NotifyShipStateChanged();

				_sessionLog = $"[color=orange]💥 构件 [{target.Definition.Name}] 受到创伤 (-{dmg} HP)！[/color]";
			}
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;
			GameRunSession.Instance.UpdateSessionTimer(dt);

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
			var session = GameRunSession.Instance;
			var eco = PlayerEconomyManager.Instance;

			float curHp = 0, maxHp = 0;
			foreach (var m in _playerShip.Grid.Modules)
			{
				if (!m.IsDestroyed) curHp += m.CurrentHp;
				maxHp += m.MaxHp;
			}

			string stateName = gsm.CurrentState switch
			{
				GameState.MainMenu        => "[color=cyan]主菜单 (MainMenu)[/color]",
				GameState.FleetHangar     => "[color=gold]选船机库 (FleetHangar)[/color]",
				GameState.HangarMetaTech  => "[color=yellow]母港科研 (HangarMetaTech)[/color]",
				GameState.SandboxBay      => "[color=magenta]风洞测试 (SandboxBay)[/color]",
				GameState.SectorMap       => "[color=lime]星区星图 (SectorMap)[/color]",
				GameState.Combat          => "[color=crimson]战术空战 (Combat)[/color]",
				GameState.BlackMarket     => "[color=yellow]废土黑市 (BlackMarket)[/color]",
				GameState.AnomalyDialogue => "[color=magenta]深空异象 (AnomalyDialogue)[/color]",
				GameState.RunSettlement   => "[color=green]战役结算 (RunSettlement)[/color]",
				_                         => "[color=white]未知[/color]"
			};

			_topBannerLabel.Text =
				$"[b][color=yellow]【TASK-44 战局上下文与返回总线】[/color][/b] 状态: {stateName} | " +
				$"耐久: [color=lightgreen]{curHp:F0}/{maxHp:F0} HP[/color] | " +
				$"资产: [color=yellow]{eco.Scraps} ⚙[/color] [color=cyan]{eco.ComputeCores} 💠[/color] | " +
				$"星区深度: [color=gold]第 {session.CurrentStats.SectorsCleared} 列[/color] | " +
				$"时长: [color=white]{session.SessionElapsedSeconds:F0}s[/color]\n" +
				$"• {_sessionLog}\n" +
				$"[color=gray][快捷验证]: 任意子面板均有 [◀ 返回按钮] 或按 [ESC 键] | [1] 制造战损 | [2] 歼敌 | [V] 触发结算[/color]";
		}
	}
}
