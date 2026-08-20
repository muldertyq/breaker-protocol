using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Environment.Asteroids;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-25 交互式验证场景：空间小行星带程序化生成、分形破碎切割与战术掩体力学
	/// </summary>
	public partial class Test_Task25 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private ShipEntity _enemyTurretShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private BulletManager _bulletManager = null!;
		private AsteroidFieldManager _asteroidField = null!;
		private RichTextLabel _hudLabel = null!;

		private float _enemyShootTimer = 0.0f;

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			// 1. 初始化小行星带管理器
			_asteroidField = new AsteroidFieldManager
			{
				Name = "AsteroidFieldManager",
				TargetAsteroidCount = 42,
				FieldArea = new Rect2(-1400, -1400, 2800, 2800)
			};
			AddChild(_asteroidField);

			// 2. 创建玩家战舰 (0, 350)
			_playerShip = new ShipEntity
			{
				Name = "AsteroidExplorerShip",
				Position = new Vector2(0, 350)
			};
			_playerShip.AddToGroup("Player");
			_playerShip.AddToGroup("Ship");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}

			// 3. 创建敌方重火力要塞 (0, -450, 隔着小行星带对峙)
			_enemyTurretShip = new ShipEntity
			{
				Name = "EnemyFortressShip",
				Position = new Vector2(0, -450),
				Rotation = Mathf.Pi
			};
			_enemyTurretShip.AddToGroup("Ship");
			AddChild(_enemyTurretShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_l_ironclad", out var ironcladBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_enemyTurretShip, ironcladBp!);
			}

			// 4. 生成小行星带 (避开玩家出生点)
			_asteroidField.GenerateAsteroidField(_playerShip.GlobalPosition);

			// 5. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateHUD();
		}

		private void CreateHUD()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_hudLabel = new RichTextLabel
			{
				Position = new Vector2(25, 25),
				Size = new Vector2(650, 650),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_hudLabel.AddThemeFontSizeOverride("normal_font_size", 15);
			canvasLayer.AddChild(_hudLabel);
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;

			// 玩家开火 (受到过热与飞线模式约束)
			if (Input.IsMouseButtonPressed(MouseButton.Left) && !_playerShip.Hotwire.IsInHotwireMode)
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			// 敌方要塞持续向下扫射弹幕 (用于掩体阻挡实测)
			_enemyShootTimer += dt;
			if (_enemyShootTimer >= 0.35f)
			{
				_enemyShootTimer = 0.0f;
				Vector2 shotDir = Vector2.Down.Rotated((float)GD.RandRange(-0.25, 0.25));
				_bulletManager.SpawnBullet(
					_enemyTurretShip.GlobalPosition + (shotDir * 40.0f),
					shotDir * 520.0f,
					damage: 20.0f,
					pierce: 0,
					elements: ElementFlags.Kinetic,
					attackerShip: _enemyTurretShip,
					lifeTime: 3.5f
				);
			}

			// [按 R 键]: 重新生成小行星带与复原敌舰
			if (Input.IsKeyPressed(Key.R))
			{
				_asteroidField.GenerateAsteroidField(_playerShip.GlobalPosition);
				if (DataManager.Instance.Blueprints.TryGet("bp_hf_l_ironclad", out var ironcladBp))
				{
					ShipBlueprintLoader.ApplyBlueprint(_enemyTurretShip, ironcladBp!);
				}
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			double fps = Engine.GetFramesPerSecond();
			double frameTimeMs = 1000.0 / Mathf.Max(1.0, fps);
			int asteroidCount = GetTree().GetNodesInGroup("Asteroid").Count;
			int activeBullets = _bulletManager.ActiveBulletCount;

			DisplayServer.WindowSetTitle($"《断路协议》| 帧率: {fps:F0} FPS | 存活小行星: {asteroidCount} 块 | 同屏弹幕: {activeBullets}");

			_hudLabel.Text = $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"[b][color=yellow]【TASK-25 空间小行星带与战术掩体展厅】[/color][/b]\n" +
							 $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"• 存活小行星:     [b][color=white]{asteroidCount}[/color][/b] 块 (铁矿 / 水晶 / 易爆矿)\n" +
							 $"• 同屏弹幕负荷:   [b][color=cyan]{activeBullets}[/color][/b] 颗 (MultiMesh GPU 批处理)\n" +
							 $"• 实时渲染性能:   [color=green]{fps:F0} FPS[/color] (单帧: {frameTimeMs:F1} ms)\n" +
							 $"--------------------------------------------------\n" +
							 $"[color=yellow][三大核心战术机制操作指南][/color]\n" +
							 $"1. 【战术掩体防御】: 躲在小行星后方，观察敌机密集的红色弹幕全部被小行星物理吸收阻挡；\n" +
							 $"2. 【分形物理破碎】: 射击大型小行星，承受重创后解体分裂为【2~3 块物理子碎石】并扩散；\n" +
							 $"3. 【易爆小行星殉爆】: 瞄准带有橙红光晕的易爆小行星，打爆引发【350HP 范围殉爆】炸爆敌舰！\n" +
							 $"4. 【物理撞击推挤】: 驾驶战舰加速撞击小行星，感受动量传递推开掩体的牛顿力学体验；\n" +
							 $"--------------------------------------------------\n" +
							 $"[操控] WASD 飞行 | 鼠标瞄准/开火 | [按 R 键]: 重新随机生成小行星带";
		}
	}
}
