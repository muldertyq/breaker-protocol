using System.Collections.Generic;
using Godot;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Persistence;
using BreakerProtocol.Ship;
using BreakerProtocol.UI.CombatHUD;
using BreakerProtocol.UI.SectorMap;
using BreakerProtocol.World.Director;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Sector;
using BreakerProtocol.World.Session;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-46 演练场：超空间跃迁撤离门与战场胜利闭环验证中枢
	/// </summary>
	public partial class Test_Task46 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private BulletManager _bulletManager = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private SceneTransitionManager _transitionManager = null!;

		private CombatEncounterDirector _encounterDirector = null!;
		private EncounterWaveBannerUI _waveBannerUI = null!;
		private CombatHUD _combatHUD = null!;
		private SectorMapUI _mapUI = null!;
		private RichTextLabel _topBannerLabel = null!;

		private string _sessionLog = "🚀 遭遇战进行中，全歼 3 波敌舰后超空间撤离门将自动展开！";

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			_transitionManager = new SceneTransitionManager();
			AddChild(_transitionManager);

			// 1. 初始化玩家战舰与会话
			PlayerEconomyManager.Instance.Reset(initialScraps: 200, initialCores: 1);
			_playerShip = new ShipEntity
			{
				Name = "PlayerShip_T46",
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

			// 建立单局会话
			GameRunSession.Instance.BindPlayerShip(_playerShip);
			GameRunSession.Instance.InitializeNewRun("bp_hf_m_anvil");

			// 2. 初始化 UI 与状态机
			CreateAllUIs();
			BindGameStateManager();

			// 3. 构建并启动遭遇战导演
			_encounterDirector = new CombatEncounterDirector();
			AddChild(_encounterDirector);

			_encounterDirector.OnWaveStarted += (waveIdx, totalWaves, title) =>
			{
				_waveBannerUI.ShowWaveBanner(waveIdx, totalWaves, title);
				_sessionLog = $"[color=gold]⚠️ {title}！敌机已折跃切入战场！[/color]";
			};

			_encounterDirector.OnEncounterCompleted += () =>
			{
				_waveBannerUI.ShowVictoryBanner();
				_sessionLog = "[color=lime]👑 敌军已全歼！超空间跃迁信标门已在战区前方展开，请驶入撤离！[/color]";
			};

			_encounterDirector.OnJumpGateSpawned += (gate) =>
			{
				gate.OnJumpSequenceInitiated += () =>
				{
					_sessionLog = "[color=yellow]⚡ 跃迁引擎共振充能完毕！超空间通道贯通！[/color]";
				};

				gate.OnGateJumpCompleted += () =>
				{
					ExecuteHyperspaceExtraction();
				};
			};

			// 默认切入 Combat 模式并启动遭遇战
			GameStateManager.Instance.SwitchState(GameState.Combat, false);
			_encounterDirector.StartEncounter(SectorNodeType.Combat, sectorColumn: 2, _playerShip);
		}

		private void CreateAllUIs()
		{
			var canvas = new CanvasLayer { Layer = 10 };
			AddChild(canvas);

			_combatHUD = new CombatHUD { TargetShip = _playerShip };
			canvas.AddChild(_combatHUD);

			_waveBannerUI = new EncounterWaveBannerUI();
			canvas.AddChild(_waveBannerUI);

			_mapUI = new SectorMapUI();
			_mapUI.Visible = false;
			canvas.AddChild(_mapUI);

			_topBannerLabel = new RichTextLabel
			{
				Position = new Vector2(20, 65),
				Size = new Vector2(1240, 50),
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
			gsm.CombatHUD = _combatHUD;
			gsm.MapUI = _mapUI;
			gsm.BindAllUIEvents();
		}

		private void ExecuteHyperspaceExtraction()
		{
			// 1. 战损现场自动存盘
			GameRunSession.Instance.SaveCurrentRun();

			// 2. 超空间无缝转场切回星图
			GameStateManager.Instance.SwitchState(GameState.SectorMap, true, "✦ 正在完成超空间跳跃并重构星区拓扑 ✦");
			_sessionLog = "[color=green]✔ 超空间跃迁成功！已安全撤回星区星图！[/color]";
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (@event is InputEventKey ek && ek.Pressed && !ek.Echo)
			{
				// [按 K 键]: 一键秒杀当前波次
				if (ek.Keycode == Key.K)
				{
					ClearCurrentWaveInstantly();
				}
				// [按 G 键]: 直接在玩家前方空降跃迁门 (调试用)
				else if (ek.Keycode == Key.G)
				{
					_encounterDirector.SpawnExtractionGate();
					_sessionLog = "[color=cyan]🚪 调试指令：超空间撤离信标门已强制展开！[/color]";
				}
				// [按 H 键]: 一键战地大修满血
				else if (ek.Keycode == Key.H)
				{
					foreach (var m in _playerShip.Grid.Modules)
					{
						m.CurrentHp = m.MaxHp;
					}
					_sessionLog = "[color=cyan]🔧 玩家战舰已执行战地紧急大修，耐久全满！[/color]";
				}
				// [按 M 键]: 星图 ⇄ 空战 切换
				else if (ek.Keycode == Key.M)
				{
					var gsm = GameStateManager.Instance;
					gsm.SwitchState(gsm.CurrentState == GameState.Combat ? GameState.SectorMap : GameState.Combat);
				}
			}
		}

		private void ClearCurrentWaveInstantly()
		{
			var enemies = new List<ShipEntity>(_encounterDirector.ActiveEnemies);
			foreach (var e in enemies)
			{
				if (GodotObject.IsInstanceValid(e))
				{
					e.QueueFree();
				}
			}
			_encounterDirector.ActiveEnemies.Clear();

			var projectiles = GetTree().GetNodesInGroup("Projectile");
			foreach (var p in projectiles)
			{
				if (p is ProjectileEntity proj && proj.AttackerShip != _playerShip)
				{
					proj.QueueFree();
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
			var session = GameRunSession.Instance;
			var eco = PlayerEconomyManager.Instance;
			var gsm = GameStateManager.Instance;

			float curHp = 0, maxHp = 0;
			foreach (var m in _playerShip.Grid.Modules)
			{
				if (!m.IsDestroyed) curHp += m.CurrentHp;
				maxHp += m.MaxHp;
			}

			_topBannerLabel.Text =
				$"[b][color=yellow]【TASK-46 超空间跃迁门演练场】[/color][/b] 状态: [color=cyan]{gsm.CurrentState}[/color] | " +
				$"耐久: [color=lightgreen]{curHp:F0}/{maxHp:F0} HP[/color] | " +
				$"歼敌: [color=lime]{session.CurrentStats.StandardEnemiesKilled}[/color] | " +
				$"资产: [color=yellow]{eco.Scraps} ⚙[/color] | " +
				$"日志: {_sessionLog}\n" +
				$"[color=gray][快捷调试]: [K 键] 一键清怪 | [G 键] 强开跃迁门 | [H 键] 回满血 | 驶入跃迁门光圈自动撤离[/color]";
		}
	}
}
