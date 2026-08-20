using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-18 交互式验证场景：打击感微顿帧、创伤值震颤与方向性受击反馈
	/// </summary>
	public partial class Test_Task18 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private ShipEntity _targetShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private Label _hudLabel = null!;

		public override void _Ready()
		{
			// 1. 挂载打击感中枢与特效
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			// 2. 玩家战舰 (重型穿甲磁轨炮)
			_playerShip = new ShipEntity
			{
				Name = "JuicePlayerShip",
				Position = new Vector2(600, 560)
			};
			_playerShip.AddToGroup("Player");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}

			// 3. 靶舰 (600, 220)
			_targetShip = new ShipEntity
			{
				Name = "JuiceTargetShip",
				Position = new Vector2(600, 220),
				Rotation = Mathf.Pi
			};
			AddChild(_targetShip);
			BuildTargetCruiser(_targetShip);

			// 4. 跟随摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateHUD();
		}

		private void BuildTargetCruiser(ShipEntity ship)
		{
			ship.Grid.Clear();
			ship.Pipeline.Clear();

			var coreDef = DataManager.Instance.Modules.Get("hf_source_core_2x2");
			ship.Grid.TryPlaceModule(coreDef, new Vector2I(-1, 0), rotation: 0, out _);

			var engDef = DataManager.Instance.Modules.Get("hf_eng_titan_main");
			ship.Grid.TryPlaceModule(engDef, new Vector2I(-1, 2), rotation: 0, out _);

			// 脆弱连接点 (HP 120)
			var jumperDef = DataManager.Instance.Modules.Get("hf_log_jumper");
			ship.Grid.TryPlaceModule(jumperDef, new Vector2I(-2, 0), rotation: 0, out var lJoint);
			if (lJoint != null) lJoint.CurrentHp = 120.0f;

			ship.Grid.TryPlaceModule(jumperDef, new Vector2I(1, 0), rotation: 0, out var rJoint);
			if (rJoint != null) rJoint.CurrentHp = 120.0f;

			// 首部斜装甲 (用于跳弹测试)
			var prowDef = DataManager.Instance.Modules.Get("hf_arm_prow_4x1");
			ship.Grid.TryPlaceModule(prowDef, new Vector2I(-2, -1), rotation: 0, out _);

			var armorDef = DataManager.Instance.Modules.Get("hf_arm_plate_2x2");
			ship.Grid.TryPlaceModule(armorDef, new Vector2I(-4, -1), rotation: 0, out _);
			ship.Grid.TryPlaceModule(armorDef, new Vector2I(2, -1), rotation: 0, out _);

			var gunDef = DataManager.Instance.Modules.Get("mod_custom_gauss_s");
			ship.Grid.TryPlaceModule(gunDef, new Vector2I(-4, -2), rotation: 0, out _);
			ship.Grid.TryPlaceModule(gunDef, new Vector2I(2, -2), rotation: 0, out _);

			ship.RebuildPhysics();
		}

		private void CreateHUD()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_hudLabel = new Label
			{
				Position = new Vector2(25, 25),
				Size = new Vector2(500, 600)
			};
			_hudLabel.AddThemeFontSizeOverride("font_size", 15);
			_hudLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			canvasLayer.AddChild(_hudLabel);
		}

		public override void _Process(double delta)
		{
			// 开火射击
			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			// 按 R 键复原靶舰
			if (Input.IsKeyPressed(Key.R))
			{
				BuildTargetCruiser(_targetShip);
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			_hudLabel.Text = $"【《断路协议》TASK-18 打击感 (Hitstop & Shake) 靶场】\n" +
							 $"==================================================\n" +
							 $"全局流速 (TimeScale): {Engine.TimeScale:F2}x ({(Engine.TimeScale < 0.9f ? "[color=orange]⚡ 微顿帧生效中[/color]" : "[color=green]正常流速[/color]")})\n" +
							 $"创伤震颤 (Trauma):    {_camera.CurrentTrauma * 100:F0}%\n" +
							 $"震屏位移 (Offset):    ({_camera.Offset.X:F1}px, {_camera.Offset.Y:F1}px)\n" +
							 $"--------------------------------------------------\n" +
							 $"[三阶打击感验证指南]\n" +
							 $"1. 【大倾角跳弹】: 斜射首装甲，体验【35ms 轻微顿 + 清脆短震】！\n" +
							 $"2. 【正面击穿】: 垂直对准舰身射击，体验【45ms 扎实顿挫 + 穿甲震屏】！\n" +
							 $"3. 【爆甲大爆炸】: 打断连接龙骨瞬间，体验【80ms 重度顿帧 + 70% 巨震】！\n" +
							 $"--------------------------------------------------\n" +
							 $"[按 R 键]: 满血修复重构靶舰";
		}
	}
}
