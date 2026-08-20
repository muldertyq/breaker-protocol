using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-10 交互式验证场景：脉冲时空行进流动、中继冷凝编译与爆发连射仿真
	/// </summary>
	public partial class Test_Task10 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private Label _telemetryLabel = null!;

		// 记录主炮实例 ID
		private string _mainGunInstanceId = string.Empty;

		// 统计射击发数与即时射速
		private int _totalShotsFired = 0;
		private double _lastShotTime = 0.0;
		private float _instantDps = 0.0f;
		private readonly List<string> _combatLog = new();

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

			// 2. 组装具备全套供电与武器管线的战舰
			BuildPoweredCombatShip();

			// 3. 创建跟随摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);

			// 4. 创建 UI 遥测面板
			CreateTelemetryUI();

			// 5. 监听开火事件
			_playerShip.Pulses.OnWeaponFired += OnWeaponFiredCallback;
		}

		private void BuildPoweredCombatShip()
		{
			_playerShip.Grid.Clear();
			_playerShip.Pipeline.Clear();

			// 1. 动力反应堆 (2x2, 位于底部 -1, 0)
			var coreDef = DataManager.Instance.Modules.Get("hf_source_core_2x2");
			_playerShip.Grid.TryPlaceModule(coreDef, new Vector2I(-1, 0), rotation: 0, out var coreInst);

			// 2. 极寒冷凝修饰舱 (2x2, 位于中部 -1, -2)
			var cryoDef = DataManager.Instance.Modules.Get("hf_mod_cryo_chamber");
			_playerShip.Grid.TryPlaceModule(cryoDef, new Vector2I(-1, -2), rotation: 0, out var cryoInst);

			// 3. 重型磁轨主炮 (3x1, 位于顶部 -1, -3)
			var gunDef = DataManager.Instance.Modules.Get("hf_wep_railgun_h");
			_playerShip.Grid.TryPlaceModule(gunDef, new Vector2I(-1, -3), rotation: 0, out var gunInst);
			_mainGunInstanceId = gunInst!.InstanceId;

			// 4. 泰坦主推 (3x2, -1, 2)
			var engineDef = DataManager.Instance.Modules.Get("hf_eng_titan_main");
			_playerShip.Grid.TryPlaceModule(engineDef, new Vector2I(-1, 2), rotation: 0, out _);

			// 5. 自动铺设两级 PCB 能量铜排回路
			// 阶段 1: 动力堆 OUT -> 冷凝舱 IN
			var pins = new List<Ship.Pipeline.PinInstance>(_playerShip.GetAllPins());
			var coreOut = pins.Find(p => p.OwnerModuleInstanceId == coreInst!.InstanceId && p.Type == Data.Models.PinType.OUT);
			var cryoIn = pins.Find(p => p.OwnerModuleInstanceId == cryoInst!.InstanceId && p.Type == Data.Models.PinType.IN);
			_playerShip.Pipeline.TryAddWire(coreOut!, cryoIn!, _playerShip.Grid, out _);

			// 阶段 2: 冷凝舱 OUT -> 主炮 IN
			var cryoOut = pins.Find(p => p.OwnerModuleInstanceId == cryoInst!.InstanceId && p.Type == Data.Models.PinType.OUT);
			var gunIn = pins.Find(p => p.OwnerModuleInstanceId == gunInst!.InstanceId && p.Type == Data.Models.PinType.IN);
			_playerShip.Pipeline.TryAddWire(cryoOut!, gunIn!, _playerShip.Grid, out _);

			_playerShip.RebuildPhysics();
		}

		private void CreateTelemetryUI()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_telemetryLabel = new Label
			{
				Position = new Vector2(25, 25),
				Size = new Vector2(450, 650)
			};
			_telemetryLabel.AddThemeFontSizeOverride("font_size", 15);
			_telemetryLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			canvasLayer.AddChild(_telemetryLabel);
		}

		public override void _Process(double delta)
		{
			// 检测鼠标左键开火输入
			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				// 尝试消耗缓冲脉冲触发磁轨炮开火
				_playerShip.Pulses.TriggerWeaponFire(_mainGunInstanceId, out _);
			}

			UpdateTelemetryHUD();
		}

		private void OnWeaponFiredCallback(string weaponId, PulsePacket firedPulse)
		{
			_totalShotsFired++;
			double now = Time.GetTicksMsec() / 1000.0;
			double interval = now - _lastShotTime;
			_lastShotTime = now;

			float currentFireRate = interval > 0.001 ? (float)(1.0 / interval) : 0.0f;
			_instantDps = firedPulse.Power * 320.0f;

			string logEntry = $"[{now:F2}s] 💥 磁轨主炮发射！瞬时射速:{currentFireRate:F1}发/s | 伤害:{_instantDps:F0} | 穿透:+{firedPulse.BonusPierce} | 元素:{firedPulse.Elements}";
			_combatLog.Add(logEntry);
			if (_combatLog.Count > 6) _combatLog.RemoveAt(0);

			// 触发开火反冲 (TASK-04 算子联动)
			Vector2 recoilDir = _playerShip.Transform.Y; // 舰尾方向
			_playerShip.ApplyCentralImpulse(recoilDir * 1500.0f);
		}

		private void UpdateTelemetryHUD()
		{
			_playerShip.Pulses.WeaponBuffers.TryGetValue(_mainGunInstanceId, out var buffer);

			int bufferedCount = buffer != null ? buffer.BufferedCount : 0;
			int maxCap = buffer != null ? buffer.MaxCapacity : 8;

			string bufferBar = "[";
			for (int i = 0; i < maxCap; i++)
			{
				bufferBar += (i < bufferedCount) ? "■" : "□";
			}
			bufferBar += "]";

			string logText = string.Join("\n", _combatLog);

			_telemetryLabel.Text = $"【《断路协议》TASK-10 PCB 脉冲流动与爆发仿真】\n" +
								   $"==================================================\n" +
								   $"在途流动脉冲数:   {_playerShip.Pulses.InFlightPulses.Count} 个 (在铜排中飞速流动)\n" +
								   $"磁轨炮缓冲能量池: {bufferBar} ({bufferedCount}/{maxCap} 发)\n" +
								   $"主炮状态:         {(bufferedCount > 0 ? "[color=green]蓄能就绪 (Ready)[/color]" : "[color=red]缓冲耗尽 (等待供能)[/color]")}\n" +
								   $"累计发射子弹:     {_totalShotsFired} 发\n" +
								   $"--------------------------------------------------\n" +
								   $"[供能网络参数]\n" +
								   $"■ 聚变反应堆:    恒定产出 3.0 发/秒 (金白光斑)\n" +
								   $"■ 极寒冷凝舱:    动态注入 Cryo 极寒与 +2 穿透 (青蓝光斑)\n" +
								   $"■ 磁轨主炮:      机械极限射速 6.0 发/秒 | 8发储能池\n" +
								   $"--------------------------------------------------\n" +
								   $"[实时战斗射击日志]\n" +
								   $"{logText}\n" +
								   $"--------------------------------------------------\n" +
								   $"[操作] 按住鼠标左键: 倾泻爆发开火 | WASD: 推进飞行";
		}
	}
}
