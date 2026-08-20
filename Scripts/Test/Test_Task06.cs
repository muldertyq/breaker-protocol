using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-06 交互式验证场景：动态质心偏移、转动惯量实时计算与飞行力学验证
	/// </summary>
	public partial class Test_Task06 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private Label _hudLabel = null!;

		public override void _Ready()
		{
			// 1. 创建飞船实体
			_playerShip = new ShipEntity
			{
				Name = "TestShip",
				Position = new Vector2(600, 400)
			};
			_playerShip.AddToGroup("Player");
			AddChild(_playerShip);

			// 2. 组装初始基础飞船 (居中对称)
			BuildBaseShip();

			// 3. 创建跟随摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);

			// 4. 创建 HUD 遥测面板
			CreateHUD();
		}

		private void BuildBaseShip()
		{
			_playerShip.Grid.Clear();

			// 动力源 (2x2, 坐标 -1, 0)
			var coreDef = DataManager.Instance.Modules.Get("hf_source_core_2x2");
			_playerShip.Grid.TryPlaceModule(coreDef, new Vector2I(-1, 0), rotation: 0, out _);

			// 冷凝舱 (2x2, 坐标 -1, -2)
			var cryoDef = DataManager.Instance.Modules.Get("hf_mod_cryo_chamber");
			_playerShip.Grid.TryPlaceModule(cryoDef, new Vector2I(-1, -2), rotation: 0, out _);

			// 主炮 (3x1, 坐标 -1, -3)
			var gunDef = DataManager.Instance.Modules.Get("hf_wep_railgun_h");
			_playerShip.Grid.TryPlaceModule(gunDef, new Vector2I(-1, -3), rotation: 0, out _);

			// 物理重构
			_playerShip.RebuildPhysics();
		}

		public override void _Process(double delta)
		{
			// 响应按键动态改装测试
			if (Input.IsActionJustPressed("ui_select") == false) // 排除空格键
			{
				if (Input.IsKeyPressed(Key.Key1))
				{
					// 按 1：左翼挂载 35t 重装甲板 (非对称偏心测试)
					var armorDef = DataManager.Instance.Modules.Get("hf_arm_plate_2x2");
					_playerShip.Grid.TryPlaceModule(armorDef, new Vector2I(-3, -1), rotation: 0, out _);
					_playerShip.RebuildPhysics();
				}
				else if (Input.IsKeyPressed(Key.Key2))
				{
					// 按 2：右翼挂载 35t 重装甲板 (恢复对称测试)
					var armorDef = DataManager.Instance.Modules.Get("hf_arm_plate_2x2");
					_playerShip.Grid.TryPlaceModule(armorDef, new Vector2I(1, -1), rotation: 0, out _);
					_playerShip.RebuildPhysics();
				}
				else if (Input.IsKeyPressed(Key.Key3))
				{
					// 按 3：重置回基础构型
					BuildBaseShip();
				}
			}

			UpdateHUD();
		}

		private void CreateHUD()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_hudLabel = new Label
			{
				Position = new Vector2(20, 20)
			};
			_hudLabel.AddThemeFontSizeOverride("font_size", 16);
			_hudLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			canvasLayer.AddChild(_hudLabel);
		}

		private void UpdateHUD()
		{
			var p = _playerShip.PhysicsData;
			float speedMeters = GlobalMetrics.PixelsToMeters(_playerShip.LinearVelocity.Length());

			_hudLabel.Text = $"【《断路协议》TASK-06 动态力学与质心遥测】\n" +
							 $"==================================================\n" +
							 $"全舰总质量:     {p.TotalMass:F1} 吨 (t)\n" +
							 $"物理质心 (CoM):  X={p.CenterOfMassGrid.X:F2}, Y={p.CenterOfMassGrid.Y:F2} GU\n" +
							 $"质心偏航角偏差: {p.CenterOfMassYawOffsetDegrees:F1}° ({(Mathf.Abs(p.CenterOfMassYawOffsetDegrees) < 0.1f ? "完美对称" : "质心偏左/右")})\n" +
							 $"总转动惯量 (I):  {p.MomentOfInertia:F1} t·m²\n" +
							 $"当前航速:       {speedMeters:F1} m/s\n" +
							 $"--------------------------------------------------\n" +
							 $"[改装测试热键]\n" +
							 $"[按 1 键]: 左翼加装 35t 重装甲 (质心左偏，甩尾明显)\n" +
							 $"[按 2 键]: 右翼加装 35t 重装甲 (恢复对称，惯量大幅增加)\n" +
							 $"[按 3 键]: 重置回轻型对称初始舰体\n" +
							 $"--------------------------------------------------\n" +
							 $"[飞行操控] WASD: 推进 | 鼠标: 瞄准旋转 | Space: 纯牛顿漂移";
		}
	}
}
