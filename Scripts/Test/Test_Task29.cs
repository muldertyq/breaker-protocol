using Godot;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Boss;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Environment.Asteroids;
using BreakerProtocol.Ship;
using BreakerProtocol.UI.Boss;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-29 交互式验证场景：多阶段解体战列舰 Boss「泰坦熔炉」决战演练场
	/// </summary>
	public partial class Test_Task29 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private ShipEntity _bossShip = null!;
		private TitanForgeBossController _bossController = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private BulletManager _bulletManager = null!;
		private AsteroidFieldManager _asteroidField = null!;
		private BossHealthBarUI _bossHealthUI = null!;
		private RichTextLabel _hudLabel = null!;

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			// 1. 初始化小行星掩体
			_asteroidField = new AsteroidFieldManager
			{
				Name = "AsteroidFieldManager",
				TargetAsteroidCount = 32,
				FieldArea = new Rect2(-1800, -1800, 3600, 3600)
			};
			AddChild(_asteroidField);

			// 2. 生成玩家战舰 (0, 520)
			_playerShip = new ShipEntity
			{
				Name = "PlayerCruiser",
				Position = new Vector2(0, 520)
			};
			_playerShip.AddToGroup("Player");
			_playerShip.AddToGroup("Ship");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}

			// 3. 生成 Boss 要塞 (0, -420)
			SpawnBossFortress();

			// 4. 生成小行星掩体
			_asteroidField.GenerateAsteroidField(_playerShip.GlobalPosition);

			// 5. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateUI();
		}

		private void SpawnBossFortress()
		{
			if (GodotObject.IsInstanceValid(_bossShip)) _bossShip.QueueFree();
			if (GodotObject.IsInstanceValid(_bossController)) _bossController.QueueFree();

			_bossShip = new ShipEntity
			{
				Name = "Boss_TitanForge",
				Position = new Vector2(0, -420),
				Rotation = Mathf.Pi
			};
			_bossShip.AddToGroup("Ship");
			_bossShip.CurrentPalette = FactionPalettes.HeavyFoundry;
			AddChild(_bossShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_l_ironclad", out var ironBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_bossShip, ironBp!);
			}

			_bossController = new TitanForgeBossController { Name = "TitanForgeBossController" };
			AddChild(_bossController);
			_bossController.Initialize(_bossShip, _playerShip);

			if (_bossHealthUI != null)
			{
				_bossHealthUI.BindBoss(_bossController);
			}
		}

		private void CreateUI()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_bossHealthUI = new BossHealthBarUI();
			_bossHealthUI.BindBoss(_bossController);
			canvasLayer.AddChild(_bossHealthUI);

			_hudLabel = new RichTextLabel
			{
				Position = new Vector2(25, 110),
				Size = new Vector2(650, 600),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_hudLabel.AddThemeFontSizeOverride("normal_font_size", 15);
			canvasLayer.AddChild(_hudLabel);
		}

		public override void _Process(double delta)
		{
			// 阶段强切测试热键
			if (Input.IsKeyPressed(Key.Key1))
			{
				// Phase 1
			}
			else if (Input.IsKeyPressed(Key.Key2))
			{
				_bossController.EnterPhase2();
			}
			else if (Input.IsKeyPressed(Key.Key3))
			{
				_bossController.EnterPhase3();
			}

			// 玩家开火
			if (Input.IsMouseButtonPressed(MouseButton.Left) && !_playerShip.Hotwire.IsInHotwireMode)
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			// [按 R 键]: 满血重置战场
			if (Input.IsKeyPressed(Key.R))
			{
				if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
				{
					ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
					_playerShip.Position = new Vector2(0, 520);
					_playerShip.LinearVelocity = Vector2.Zero;
				}
				SpawnBossFortress();
				_asteroidField.GenerateAsteroidField(_playerShip.GlobalPosition);
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			double fps = Engine.GetFramesPerSecond();
			float bossHp = _bossController.CalculateCurrentTotalHp();
			string phaseName = _bossController.CurrentPhase.ToString();

			DisplayServer.WindowSetTitle($"《断路协议》| 帧率: {fps:F0} FPS | Boss阶段: {phaseName} | Boss HP: {bossHp:F0}");

			_hudLabel.Text = $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"[b][color=yellow]【TASK-29 战列舰 Boss「泰坦熔炉」三阶段决战演练场】[/color][/b]\n" +
							 $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"• Boss 状态阶段:  [b][color=cyan]{phaseName}[/color][/b]\n" +
							 $"• Boss 剩余耐久:  [b][color=white]{bossHp:F0}[/color][/b] HP (占比: {_bossController.GetHpRatio() * 100:F0}%)\n" +
							 $"• 浮游子舰存活:  [b][color=white]{_bossController.SpawnedEscorts.Count}[/color][/b] 艘 | 实时帧率: [color=green]{fps:F0} FPS[/color]\n" +
							 $"--------------------------------------------------\n" +
							 $"[三大战斗阶段机制说明]\n" +
							 $"1. 【PHASE 1 - 防御要塞】: Boss 外覆重型斜面装甲，双舷加特林火力压制；\n" +
							 $"2. 【PHASE 2 - 浮游子舰分离】: HP<60% 爆甲脱离生成 2 艘护航子舰，核心暴露并释放 360° 旋转弹幕；\n" +
							 $"3. 【PHASE 3 - 狂暴熔毁冲撞】: HP<30% 开启 30 秒自毁倒计时，Boss 航速暴涨 +150% 全速冲撞玩家！\n" +
							 $"--------------------------------------------------\n" +
							 $"[调试热键]\n" +
							 $"[按 2 键]: 强制进入 Phase 2 (触发两翼爆甲与子舰分离)\n" +
							 $"[按 3 键]: 强制进入 Phase 3 (触发 30s 自毁狂暴冲撞)\n" +
							 $"[按 R 键]: 满血重置战场与 Boss";
		}
	}
}
