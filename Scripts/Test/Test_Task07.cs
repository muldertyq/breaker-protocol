using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-07 飞控系统与牛顿漂移验证场景
	/// </summary>
	public partial class Test_Task07 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private Label _hudLabel = null!;

		public override void _Ready()
		{
			// 1. 创建飞船实体
			_playerShip = new ShipEntity
			{
				Name = "PlayerShip",
				Position = new Vector2(600, 400)
			};
			_playerShip.AddToGroup("Player");
			AddChild(_playerShip);

			// 2. 组装具备推进系统的完整战舰
			BuildFullPoweredShip();

			// 3. 创建跟随摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);

			// 4. 创建 UI 遥测面板
			CreateTelemetryHUD();
		}

		private void BuildFullPoweredShip()
		{
			_playerShip.Grid.Clear();

			// 动力堆 (2x2, 中心 -1, 0)
			var coreDef = DataManager.Instance.Modules.Get("hf_source_core_2x2");
			_playerShip.Grid.TryPlaceModule(coreDef, new Vector2I(-1, 0), rotation: 0, out _);

			// 冷凝舱 (2x2, -1, -2)
			var cryoDef = DataManager.Instance.Modules.Get("hf_mod_cryo_chamber");
			_playerShip.Grid.TryPlaceModule(cryoDef, new Vector2I(-1, -2), rotation: 0, out _);

			// 磁轨主炮 (3x1, -1, -3)
			var gunDef = DataManager.Instance.Modules.Get("hf_wep_railgun_h");
			_playerShip.Grid.TryPlaceModule(gunDef, new Vector2I(-1, -3), rotation: 0, out _);

			// 泰坦主推进器 (3x2, -1, +2，安装在动力源下方)
			var engineDef = DataManager.Instance.Modules.Get("hf_eng_titan_main");
			_playerShip.Grid.TryPlaceModule(engineDef, new Vector2I(-1, 2), rotation: 0, out _);

			// 两侧 RCS 姿态微喷 (1x1, 左右各一个)
			var rcsDef = DataManager.Instance.Modules.Get("hf_eng_rcs_heavy");
			_playerShip.Grid.TryPlaceModule(rcsDef, new Vector2I(-2, 0), rotation: 0, out _);
			_playerShip.Grid.TryPlaceModule(rcsDef, new Vector2I(1, 0), rotation: 0, out _);

			_playerShip.RebuildPhysics();
		}

		private void CreateTelemetryHUD()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_hudLabel = new Label
			{
				Position = new Vector2(25, 25)
			};
			_hudLabel.AddThemeFontSizeOverride("font_size", 16);
			_hudLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			canvasLayer.AddChild(_hudLabel);
		}

		public override void _Process(double delta)
		{
			var p = _playerShip.PhysicsData;
			var f = _playerShip.Flight;
			float speedMeters = GlobalMetrics.PixelsToMeters(_playerShip.LinearVelocity.Length());

			string modeText = f.AssistMode == BreakerProtocol.Ship.Physics.FlightAssistMode.NewtonianDrift
				? "[color=red]【纯牛顿惯性漂移中 (DRIFT ON)】[/color]"
				: "[color=green]【巡航辅助自动抓地 (CRUISE ASSIST)】[/color]";

			_hudLabel.Text = $"【《断路协议》TASK-07 飞控与漂移动力学遥测】\n" +
							 $"==================================================\n" +
							 $"飞控状态:        {modeText}\n" +
							 $"航向速度:        {speedMeters:F1} m/s\n" +
							 $"漂移滑移角 (β):  {f.SlipAngleDegrees:F1}° ({(f.SlipAngleDegrees > 45 ? "大角度侧滑！" : "航向对齐")})\n" +
							 $"全舰总质量:      {p.TotalMass:F1} 吨 (t)\n" +
							 $"主推前推力:      {f.ThrustCapability.MaxForwardThrust:F0} N (加力: {f.IsBoosting})\n" +
							 $"姿态转向力矩:    {f.ThrustCapability.MaxAngularTorque:F0} N·m\n" +
							 $"推进器总数:      {f.ThrustCapability.Thrusters.Count} 个\n" +
							 $"--------------------------------------------------\n" +
							 $"[深度操控指南]\n" +
							 $"[W/S/A/D]       : 空间推力 (主推前向 + RCS四向平移)\n" +
							 $"[Shift]         : 激活泰坦主推 2.0x 氮气加力\n" +
							 $"[按住 Space]    : 开启纯牛顿漂移 (关闭阻尼，滑行中360°甩头瞄准)\n" +
							 $"[松开 Space]    : 瞬间恢复巡航抓地制动";
		}
	}
}
