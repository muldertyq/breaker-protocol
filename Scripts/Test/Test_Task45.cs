using System.Collections.Generic;
using Godot;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Persistence;
using BreakerProtocol.Ship;
using BreakerProtocol.UI.CombatHUD;
using BreakerProtocol.World.Director;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Sector;
using BreakerProtocol.World.Session;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-45 演练场：程序化战术遭遇战导演系统与动态波次生成验证中枢
	/// </summary>
	public partial class Test_Task45 : Node2D
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
		private RichTextLabel _topBannerLabel = null!;

		private string _directorLog = "🚀 遭遇战导演总控已就绪。正在调度第一波敌军进场！";

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
				Name = "PlayerShip_T45",
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

			GameRunSession.Instance.BindPlayerShip(_playerShip);
			GameRunSession.Instance.InitializeNewRun("bp_hf_m_anvil");

			// 2. 初始化 UI 与波次横幅
			CreateUI();

			// 3. 构建并启动遭遇战导演
			_encounterDirector = new CombatEncounterDirector();
			AddChild(_encounterDirector);

			_encounterDirector.OnWaveStarted += (waveIdx, totalWaves, title) =>
			{
				_waveBannerUI.ShowWaveBanner(waveIdx, totalWaves, title);
				_directorLog = $"[color=gold]⚠️ {title}！敌方舰队完成超空间折跃！[/color]";
			};

			_encounterDirector.OnWaveCleared += (waveIdx) =>
			{
				_directorLog = $"[color=lime]✔ 第 {waveIdx} 波敌军已肃清！增援雷达解算中...[/color]";
			};

			_encounterDirector.OnEncounterCompleted += () =>
			{
				_waveBannerUI.ShowVictoryBanner();
				_directorLog = "[color=green]👑 本区域全部波次敌机已全歼！超空间跳跃通道已解锁！[/color]";
			};

			// 启动第 1 列常规遭遇战 (3 波次)
			_encounterDirector.StartEncounter(SectorNodeType.Combat, sectorColumn: 1, _playerShip);
		}

		private void CreateUI()
		{
			var canvas = new CanvasLayer { Layer = 10 };
			AddChild(canvas);

			_combatHUD = new CombatHUD { TargetShip = _playerShip };
			canvas.AddChild(_combatHUD);

			_waveBannerUI = new EncounterWaveBannerUI();
			canvas.AddChild(_waveBannerUI);

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

		public override void _UnhandledInput(InputEvent @event)
		{
			if (@event is InputEventKey ek && ek.Pressed && !ek.Echo)
			{
				// [按 K 键]: 彻底秒杀当前波次所有敌机，清理敌方弹幕并触发波次结算
				if (ek.Keycode == Key.K)
				{
					ClearCurrentWaveInstantly();
				}
				// [按 H 键]: 玩家战舰一键无敌大修满血
				else if (ek.Keycode == Key.H)
				{
					foreach (var m in _playerShip.Grid.Modules)
					{
						m.CurrentHp = m.MaxHp;
					}
					_directorLog = "[color=cyan]🔧 玩家战舰已执行战地紧急大修，耐久全满！[/color]";
				}
				// [按 R 键]: 重新开始一轮遭遇战
				else if (ek.Keycode == Key.R)
				{
					_encounterDirector.StartEncounter(SectorNodeType.Combat, sectorColumn: 2, _playerShip);
				}
			}
		}

		private void ClearCurrentWaveInstantly()
		{
			// 1. 彻底销毁所有存活敌舰
			var enemies = new List<ShipEntity>(_encounterDirector.ActiveEnemies);
			foreach (var e in enemies)
			{
				if (GodotObject.IsInstanceValid(e))
				{
					e.QueueFree();
				}
			}
			_encounterDirector.ActiveEnemies.Clear();

			// 2. 清理屏幕上所有属于敌方的滞空子弹，防止秒杀后玩家依然被流弹打死
			var projectiles = GetTree().GetNodesInGroup("Projectile");
			foreach (var p in projectiles)
			{
				if (p is ProjectileEntity proj && proj.AttackerShip != _playerShip)
				{
					proj.QueueFree();
				}
			}

			_directorLog = "[color=yellow]⚡ 已执行 [K 键] 一键清屏指令，敌舰与敌方弹幕已湮灭！[/color]";
		}

		public override void _Process(double delta)
		{
			// 玩家主炮射击
			if (Godot.Input.IsMouseButtonPressed(MouseButton.Left))
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

			float curHp = 0, maxHp = 0;
			foreach (var m in _playerShip.Grid.Modules)
			{
				if (!m.IsDestroyed) curHp += m.CurrentHp;
				maxHp += m.MaxHp;
			}

			_topBannerLabel.Text =
				$"[b][color=yellow]【TASK-45 遭遇战演练场】[/color][/b] " +
				$"玩家耐久: [color={(curHp < maxHp * 0.4f ? "red" : "lightgreen")}]{curHp:F0}/{maxHp:F0} HP[/color] | " +
				$"歼敌: [color=lime]{session.CurrentStats.StandardEnemiesKilled} 架[/color] | " +
				$"废料: [color=yellow]{eco.Scraps} ⚙[/color] | " +
				$"日志: {_directorLog}\n" +
				$"[color=gray][快捷调试]: [K 键] 一键清屏跳波 | [H 键] 一键满血修复 | [R 键] 重新生成遭遇战[/color]";
		}
	}
}
