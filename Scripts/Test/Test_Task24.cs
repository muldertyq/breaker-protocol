using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-24 交互式验证场景：MultiMeshInstance2D 弹幕合批与万弹齐发极限性能压测
	/// </summary>
	public partial class Test_Task24 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private ShipEntity _targetShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private BulletManager _bulletManager = null!;
		private RichTextLabel _hudLabel = null!;

		private ElementFlags _currentElement = ElementFlags.Kinetic | ElementFlags.Thermal;

		public override void _Ready()
		{
			// 1. 初始化特效与打击感中枢
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			// 2. 初始化 BulletManager GPU 批处理器
			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			// 3. 创建玩家战舰 (中心 600, 580)
			_playerShip = new ShipEntity
			{
				Name = "GatlingPlayerShip",
				Position = new Vector2(600, 580)
			};
			_playerShip.AddToGroup("Player");
			_playerShip.AddToGroup("Ship");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}

			// 4. 创建正前方重型铁甲靶舰 (600, 180, 舰首朝下)
			_targetShip = new ShipEntity
			{
				Name = "StressTargetShip",
				Position = new Vector2(600, 180),
				Rotation = Mathf.Pi
			};
			_targetShip.AddToGroup("Ship");
			AddChild(_targetShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_l_ironclad", out var ironcladBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_targetShip, ironcladBp!);
			}

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
			// 玩家按住鼠标左键：极速加特林喷射
			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				for (int i = 0; i < 4; i++)
				{
					Vector2 spreadDir = (-_playerShip.GlobalTransform.Y).Rotated((float)GD.RandRange(-0.25, 0.25));
					_bulletManager.SpawnBullet(
						_playerShip.GlobalPosition + (spreadDir * 30.0f),
						spreadDir * (float)GD.RandRange(550.0, 850.0),
						damage: 25.0f,
						pierce: 1,
						elements: _currentElement,
						attackerShip: _playerShip,
						lifeTime: 3.0f,
						size: 1.0f
					);
				}
			}

			// [按 1 键]: 瞬间爆发 600 发 360° 环形全向弹幕
			if (Input.IsKeyPressed(Key.Key1))
			{
				SpawnCircularBarrage(600);
			}

			// [按 2 键]: 极限压力测试 -> 瞬间注入 3,000 发漫天弹幕
			if (Input.IsKeyPressed(Key.Key2))
			{
				SpawnCircularBarrage(3000);
			}

			// [按 C 键]: 清空当前所有活跃弹幕
			if (Input.IsKeyPressed(Key.C))
			{
				_bulletManager.ClearAll();
			}

			// [按 R 键]: 满血重置靶舰
			if (Input.IsKeyPressed(Key.R))
			{
				if (DataManager.Instance.Blueprints.TryGet("bp_hf_l_ironclad", out var ironcladBp))
				{
					ShipBlueprintLoader.ApplyBlueprint(_targetShip, ironcladBp!);
				}
			}

			UpdateHUD();
		}

		private void SpawnCircularBarrage(int count)
		{
			float angleStep = Mathf.Tau / count;
			for (int i = 0; i < count; i++)
			{
				float angle = i * angleStep;
				Vector2 dir = Vector2.FromAngle(angle);
				float speed = (float)GD.RandRange(250.0, 650.0);

				_bulletManager.SpawnBullet(
					_playerShip.GlobalPosition,
					dir * speed,
					damage: 15.0f,
					pierce: 0,
					elements: _currentElement,
					attackerShip: _playerShip,
					lifeTime: 4.0f,
					size: 0.9f
				);
			}
		}

		private void UpdateHUD()
		{
			double fps = Engine.GetFramesPerSecond();
			double frameTimeMs = 1000.0 / Mathf.Max(1.0, fps);
			int activeBullets = _bulletManager.ActiveBulletCount;

			string perfColor = fps >= 55.0 ? "green" : (fps >= 30.0 ? "yellow" : "red");

			// 关键修复：使用 Godot 4 标准 API WindowSetTitle
			DisplayServer.WindowSetTitle($"《断路协议》| 帧率: {fps:F0} FPS ({frameTimeMs:F1}ms) | 同屏弹幕: {activeBullets} 颗");

			// HUD 富文本面板
			_hudLabel.Text = $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"[b][color=yellow]【TASK-24 MultiMesh 弹幕合批与性能压测】[/color][/b]\n" +
							 $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"• 渲染管线:       MultiMeshInstance2D ([color=cyan]单 DrawCall GPU 合批[/color])\n" +
							 $"• 空间粗筛:       Broadphase Filter ([color=green]空旷深空跳过物理射线[/color])\n" +
							 $"• 同屏活跃弹幕:   [b][color=white]{activeBullets}[/color][/b] / {BulletManager.MaxBulletCapacity} 颗\n" +
							 $"• 实时渲染帧率:   [b][color={perfColor}]{fps:F0} FPS[/color][/b] (单帧耗时: [color={perfColor}]{frameTimeMs:F1} ms[/color])\n" +
							 $"--------------------------------------------------\n" +
							 $"[color=yellow][快捷性能压测指令][/color]\n" +
							 $"■ [鼠标左键] -> 按住持续超高速喷射加特林弹幕流\n" +
							 $"■ [按 1 键]  -> 瞬间爆发 360° 环形扩散 600 发弹幕\n" +
							 $"■ [按 2 键]  -> 极限注入 3,000 发全屏狂暴漫天弹幕\n" +
							 $"■ [按 C 键]  -> 一键清空全场所有弹幕\n" +
							 $"■ [按 R 键]  -> 满血重置重装铁甲靶舰\n" +
							 $"--------------------------------------------------\n" +
							 $"[操控] WASD 飞行 | 鼠标瞄准 | 左键开火";
		}
	}
}
