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
using BreakerProtocol.Environment.Hazards;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-26 交互式验证场景：高引力黑洞奇点、弹道引力透镜偏转与 EMP 电磁星云风暴
	/// </summary>
	public partial class Test_Task26 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private ShipEntity _enemyDriftingShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private BulletManager _bulletManager = null!;
		private AsteroidFieldManager _asteroidField = null!;
		private SpaceEnvironmentManager _envManager = null!;
		private RichTextLabel _hudLabel = null!;

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			// 1. 初始化空间极端环境管理器
			_envManager = new SpaceEnvironmentManager { Name = "SpaceEnvironmentManager" };
			AddChild(_envManager);

			// 2. 初始化小行星带
			_asteroidField = new AsteroidFieldManager
			{
				Name = "AsteroidFieldManager",
				TargetAsteroidCount = 36,
				FieldArea = new Rect2(-1500, -1500, 3000, 3000)
			};
			AddChild(_asteroidField);

			// 3. 创建玩家战舰 (0, 380)
			_playerShip = new ShipEntity
			{
				Name = "ExtremeExplorerShip",
				Position = new Vector2(0, 380)
			};
			_playerShip.AddToGroup("Player");
			_playerShip.AddToGroup("Ship");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}

			// 4. 创建在黑洞边缘受困漂流的敌方重巡 (-450, -200)
			_enemyDriftingShip = new ShipEntity
			{
				Name = "DriftingEnemyShip",
				Position = new Vector2(-450, -200),
				Rotation = Mathf.Pi * 0.4f
			};
			_enemyDriftingShip.AddToGroup("Ship");
			AddChild(_enemyDriftingShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_l_ironclad", out var ironcladBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_enemyDriftingShip, ironcladBp!);
			}

			// 5. 布设极端环境：左侧黑洞奇点 + 右侧 EMP 电磁星云
			SetupExtremeHazards();

			// 6. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateHUD();
		}

		private void SetupExtremeHazards()
		{
			_envManager.ClearAll();

			// 左侧：大型高引力黑洞奇点 (-500, -200)
			_envManager.SpawnSingularity(new Vector2(-500, -200), gravityRadius: 750.0f, eventHorizon: 65.0f);

			// 右侧：大范围 EMP 电磁星云风暴 (550, -100)
			_envManager.SpawnNebulaStorm(new Vector2(550, -100), radius: 360.0f);

			// 生成小行星群
			_asteroidField.GenerateAsteroidField(_playerShip.GlobalPosition);
		}

		private void CreateHUD()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_hudLabel = new RichTextLabel
			{
				Position = new Vector2(25, 25),
				Size = new Vector2(660, 660),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_hudLabel.AddThemeFontSizeOverride("normal_font_size", 15);
			canvasLayer.AddChild(_hudLabel);
		}

		public override void _Process(double delta)
		{
			// 玩家开火 (受到过热与飞线模式约束)
			if (Input.IsMouseButtonPressed(MouseButton.Left) && !_playerShip.Hotwire.IsInHotwireMode)
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			// [按 1 键]: 沿朝向发射扇形弹幕 (用于直观观察引力透镜弯曲)
			if (Input.IsKeyPressed(Key.Key1))
			{
				for (int i = 0; i < 24; i++)
				{
					Vector2 shotDir = (-_playerShip.GlobalTransform.Y).Rotated((float)GD.RandRange(-0.35, 0.35));
					_bulletManager.SpawnBullet(
						_playerShip.GlobalPosition,
						shotDir * (float)GD.RandRange(450.0, 650.0),
						damage: 20.0f,
						pierce: 0,
						elements: ElementFlags.Kinetic,
						attackerShip: _playerShip,
						lifeTime: 4.5f
					);
				}
			}

			// [按 R 键]: 满血重置战场与极端环境
			if (Input.IsKeyPressed(Key.R))
			{
				SetupExtremeHazards();
				if (DataManager.Instance.Blueprints.TryGet("bp_hf_l_ironclad", out var ironcladBp))
				{
					ShipBlueprintLoader.ApplyBlueprint(_enemyDriftingShip, ironcladBp!);
					_enemyDriftingShip.Position = new Vector2(-450, -200);
					_enemyDriftingShip.LinearVelocity = Vector2.Zero;
				}
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			double fps = Engine.GetFramesPerSecond();
			double frameTimeMs = 1000.0 / Mathf.Max(1.0, fps);
			float heat = _playerShip.Thermal.OverheatRatio * 100.0f;
			int activeBullets = _bulletManager.ActiveBulletCount;

			DisplayServer.WindowSetTitle($"《断路协议》| 帧率: {fps:F0} FPS | 全舰发热: {heat:F0}% | 弹幕: {activeBullets}");

			_hudLabel.Text = $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"[b][color=yellow]【TASK-26 空间极端物理环境与引力透镜展厅】[/color][/b]\n" +
							 $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"• 全舰热力负荷:   [{heat:F0}% / 100%] {(_playerShip.Thermal.IsOverheated ? "[color=red]🔥熔断[/color]" : "[color=green]受控[/color]")}\n" +
							 $"• 活跃弹道实例:   [b][color=cyan]{activeBullets}[/color][/b] 颗 (支持引力透镜弯折)\n" +
							 $"• 实时渲染性能:   [color=green]{fps:F0} FPS[/color] (单帧: {frameTimeMs:F1} ms)\n" +
							 $"--------------------------------------------------\n" +
							 $"[color=yellow][两大空间极端天体操作指南][/color]\n" +
							 $"1. 【左侧黑洞奇点 (X: -500)】:\n" +
							 $"   ■ [引力阱拉拽]: 观察受困敌舰与小行星被无情吸向黑洞中心；\n" +
							 $"   ■ [引力透镜弹道弯折]: 朝黑洞边缘开火 (或按 1 键)，[color=cyan]数千发实弹弹道发生壮观弧形偏转与弹弓加速[/color]！\n" +
							 $"   ■ [视界引潮力撕裂]: 过于靠近中心 (<80px) 触发结构过载，机组构件瞬间撕裂解体；\n" +
							 $"2. 【右侧 EMP 电磁星云 (X: +550)】:\n" +
							 $"   ■ [漏电与热量渗透]: 驾驶战舰驶入蓝色星云，全舰爆出短路电弧，发热负荷持续飙升；\n" +
							 $"   ■ [武器总线跳闸]: 星云强电离引发武器电容间歇性断电卡壳！\n" +
							 $"--------------------------------------------------\n" +
							 $"[操控] WASD 飞行 | 鼠标开火 | [按 1 键]: 扇形引力弹幕 | [按 R 键]: 满血重置";
		}
	}
}
