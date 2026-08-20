using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-11 交互式验证场景：PCB 逻辑元件 (分流器交替供能 + 电容容量扩充)
	/// </summary>
	public partial class Test_Task11 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private Label _telemetryLabel = null!;

		private string _leftGunId = string.Empty;
		private string _rightGunId = string.Empty;

		public override void _Ready()
		{
			// 1. 创建飞船实体
			_playerShip = new ShipEntity
			{
				Name = "LogicTestShip",
				Position = new Vector2(600, 400)
			};
			_playerShip.AddToGroup("Player");
			AddChild(_playerShip);

			// 2. 组装具备分流器与电容的战舰
			BuildLogicDemoShip();

			// 3. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);

			// 4. UI 面板
			CreateTelemetryUI();

			_playerShip.Pulses.OnWeaponFired += (wId, pulse) =>
			{
				// 开火反冲微震
				Vector2 recoilDir = _playerShip.Transform.Y;
				_playerShip.ApplyCentralImpulse(recoilDir * 800.0f);
			};
		}

		private void BuildLogicDemoShip()
		{
			_playerShip.Grid.Clear();
			_playerShip.Pipeline.Clear();

			// 1. 动力反应堆 (2x2, 底部 -1, 1) -> pulseOutput: 4.0发/秒
			var coreDef = DataManager.Instance.Modules.Get("hf_source_core_2x2");
			_playerShip.Grid.TryPlaceModule(coreDef, new Vector2I(-1, 1), rotation: 0, out var coreInst);

			// 2. 总线分流器 (1x1, 位于反应堆正上方 -1, 0)
			var splitterDef = DataManager.Instance.Modules.Get("hf_log_splitter");
			_playerShip.Grid.TryPlaceModule(splitterDef, new Vector2I(-1, 0), rotation: 0, out var splitterInst);

			// 3. 左舷战术储能电容 (1x1, -2, -1) -> 额外 +8 容量
			var capDef = DataManager.Instance.Modules.Get("hf_log_capacitor");
			_playerShip.Grid.TryPlaceModule(capDef, new Vector2I(-2, -1), rotation: 0, out var capInst);

			// 4. 左舷副炮 (2x1, -2, -2) 与 右舷副炮 (2x1, 0, -2)
			var gunDef = DataManager.Instance.Modules.Get("mod_custom_gauss_s");
			_playerShip.Grid.TryPlaceModule(gunDef, new Vector2I(-2, -2), rotation: 0, out var leftGunInst);
			_playerShip.Grid.TryPlaceModule(gunDef, new Vector2I(0, -2), rotation: 0, out var rightGunInst);

			_leftGunId = leftGunInst!.InstanceId;
			_rightGunId = rightGunInst!.InstanceId;

			// 5. 泰坦主推 (3x2, -1, 3)
			var engineDef = DataManager.Instance.Modules.Get("hf_eng_titan_main");
			_playerShip.Grid.TryPlaceModule(engineDef, new Vector2I(-1, 3), rotation: 0, out _);

			// 6. 铺设逻辑回路:
			var pins = new List<Ship.Pipeline.PinInstance>(_playerShip.GetAllPins());

			// A: 反应堆 OUT -> 分流器 IN
			var coreOut = pins.Find(p => p.OwnerModuleInstanceId == coreInst!.InstanceId && p.Type == Data.Models.PinType.OUT);
			var splitterIn = pins.Find(p => p.OwnerModuleInstanceId == splitterInst!.InstanceId && p.Type == Data.Models.PinType.IN);
			_playerShip.Pipeline.TryAddWire(coreOut!, splitterIn!, _playerShip.Grid, out _);

			// B (左支路): 分流器 OUT -> 电容 IN -> 左副炮 IN
			var splitterOut = pins.Find(p => p.OwnerModuleInstanceId == splitterInst!.InstanceId && p.Type == Data.Models.PinType.OUT);
			var capIn = pins.Find(p => p.OwnerModuleInstanceId == capInst!.InstanceId && p.Type == Data.Models.PinType.IN);
			_playerShip.Pipeline.TryAddWire(splitterOut!, capIn!, _playerShip.Grid, out _);

			var capOut = pins.Find(p => p.OwnerModuleInstanceId == capInst!.InstanceId && p.Type == Data.Models.PinType.OUT);
			var leftGunIn = pins.Find(p => p.OwnerModuleInstanceId == _leftGunId && p.Type == Data.Models.PinType.IN);
			_playerShip.Pipeline.TryAddWire(capOut!, leftGunIn!, _playerShip.Grid, out _);

			// C (右支路): 分流器 OUT -> 右副炮 IN
			var rightGunIn = pins.Find(p => p.OwnerModuleInstanceId == _rightGunId && p.Type == Data.Models.PinType.IN);
			_playerShip.Pipeline.TryAddWire(splitterOut!, rightGunIn!, _playerShip.Grid, out _);

			_playerShip.RebuildPhysics();
		}

		private void CreateTelemetryUI()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_telemetryLabel = new Label
			{
				Position = new Vector2(25, 25),
				Size = new Vector2(480, 650)
			};
			_telemetryLabel.AddThemeFontSizeOverride("font_size", 15);
			_telemetryLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			canvasLayer.AddChild(_telemetryLabel);
		}

		public override void _Process(double delta)
		{
			// 鼠标左键：开火左副炮 (含电容扩充 16发)
			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				_playerShip.Pulses.TriggerWeaponFire(_leftGunId, out _);
			}

			// 鼠标右键：开火右副炮 (常规 8发)
			if (Input.IsMouseButtonPressed(MouseButton.Right))
			{
				_playerShip.Pulses.TriggerWeaponFire(_rightGunId, out _);
			}

			UpdateTelemetryHUD();
		}

		private void UpdateTelemetryHUD()
		{
			_playerShip.Pulses.WeaponBuffers.TryGetValue(_leftGunId, out var leftBuffer);
			_playerShip.Pulses.WeaponBuffers.TryGetValue(_rightGunId, out var rightBuffer);

			int lCount = leftBuffer != null ? leftBuffer.BufferedCount : 0;
			int lMax = leftBuffer != null ? leftBuffer.MaxCapacity : 16;

			int rCount = rightBuffer != null ? rightBuffer.BufferedCount : 0;
			int rMax = rightBuffer != null ? rightBuffer.MaxCapacity : 8;

			string leftBar = BuildBar(lCount, lMax);
			string rightBar = BuildBar(rCount, rMax);

			_telemetryLabel.Text = $"【《断路协议》TASK-11 PCB 逻辑元件与分流遥测】\n" +
								   $"==================================================\n" +
								   $"在途流动脉冲数:   {_playerShip.Pulses.InFlightPulses.Count} 个\n" +
								   $"--------------------------------------------------\n" +
								   $"【左副炮 (串联电容扩容)】\n" +
								   $"能量缓冲池: {leftBar} ({lCount}/{lMax} 发 - 超大容量)\n" +
								   $"状态:       {(lCount > 0 ? "[color=green]蓄能充沛[/color]" : "[color=red]缓冲见底[/color]")}\n\n" +
								   $"【右副炮 (常规直连)】\n" +
								   $"能量缓冲池: {rightBar} ({rCount}/{rMax} 发 - 标准容量)\n" +
								   $"状态:       {(rCount > 0 ? "[color=green]蓄能充沛[/color]" : "[color=red]缓冲见底[/color]")}\n" +
								   $"--------------------------------------------------\n" +
								   $"[逻辑元件运行监控]\n" +
								   $"■ 总线分流器:    1入2出，交替向左右两路输送脉冲\n" +
								   $"■ 储能电容舱:    为左副炮提供 +8 额外爆发电容\n" +
								   $"--------------------------------------------------\n" +
								   $"[操控指南]\n" +
								   $"[按住鼠标左键]: 开火左副炮 (16发长爆发)\n" +
								   $"[按住鼠标右键]: 开火右副炮 (8发常规爆发)\n" +
								   $"[左右键同时按]: 双舷副炮同步齐射\n" +
								   $"[WASD / Shift]: 推进飞行与加力";
		}

		private string BuildBar(int count, int max)
		{
			string bar = "[";
			for (int i = 0; i < max; i++)
			{
				bar += (i < count) ? "■" : "□";
			}
			return bar + "]";
		}
	}
}
