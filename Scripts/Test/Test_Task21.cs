using System.Collections.Generic;
using System.Linq;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-21 交互式验证场景：全舰热力学发热累积、次生殉爆蔓延与防殉爆保险阀实装
	/// </summary>
	public partial class Test_Task21 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private ShipEntity _targetNoFuseShip = null!;
		private ShipEntity _targetWithFuseShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private Label _hudLabel = null!;

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			// 1. 创建玩家战舰 (用于测试连射发热与过热熔断)
			_playerShip = new ShipEntity
			{
				Name = "ThermalPlayerShip",
				Position = new Vector2(600, 580)
			};
			_playerShip.AddToGroup("Player");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}

			// 2. 创建无保险阀靶舰 (350, 220) -> 用于测试殉爆连锁炸毁核心
			_targetNoFuseShip = new ShipEntity
			{
				Name = "TargetNoFuse",
				Position = new Vector2(350, 220),
				Rotation = Mathf.Pi
			};
			AddChild(_targetNoFuseShip);
			BuildNoFuseTarget(_targetNoFuseShip);

			// 3. 创建安装保险阀靶舰 (850, 220) -> 用于测试保险阀 0.1s 自熔断保护核心
			_targetWithFuseShip = new ShipEntity
			{
				Name = "TargetWithFuse",
				Position = new Vector2(850, 220),
				Rotation = Mathf.Pi
			};
			AddChild(_targetWithFuseShip);
			BuildWithFuseTarget(_targetWithFuseShip);

			// 4. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateHUD();
		}

		private void BuildNoFuseTarget(ShipEntity ship)
		{
			ship.Grid.Clear();
			ship.Pipeline.Clear();

			// 核心在后 (-1, 1)
			var coreDef = DataManager.Instance.Modules.Get("hf_source_core_2x2");
			ship.Grid.TryPlaceModule(coreDef, new Vector2I(-1, 1), rotation: 0, out var core);

			// 外挂危险易爆大电容在前 (-1, -1) -> 紧挨着核心，无保护！
			var capDef = DataManager.Instance.Modules.Get("hf_log_capacitor");
			ship.Grid.TryPlaceModule(capDef, new Vector2I(-1, -1), rotation: 0, out var cap);
			if (cap != null) cap.CurrentHp = 80.0f; // 脆弱易爆

			var pins = ship.GetAllPins().ToList();
			var outPin = pins.FirstOrDefault(p => p.OwnerModuleInstanceId == core!.InstanceId && p.Type == PinType.OUT);
			var inPin = pins.FirstOrDefault(p => p.OwnerModuleInstanceId == cap!.InstanceId && p.Type == PinType.IN);
			if (outPin != null && inPin != null) ship.Pipeline.TryAddWire(outPin, inPin, ship.Grid, out _);

			ship.RebuildPhysics();
		}

		private void BuildWithFuseTarget(ShipEntity ship)
		{
			ship.Grid.Clear();
			ship.Pipeline.Clear();

			// 核心在后 (-1, 2)
			var coreDef = DataManager.Instance.Modules.Get("hf_source_core_2x2");
			ship.Grid.TryPlaceModule(coreDef, new Vector2I(-1, 2), rotation: 0, out var core);

			// 中间安装防殉爆保险阀 (0, 0)
			var fuseDef = DataManager.Instance.Modules.Get("hf_log_fuse");
			ship.Grid.TryPlaceModule(fuseDef, new Vector2I(0, 0), rotation: 0, out var fuse);

			// 外挂危险易爆大电容在前 (0, -2)
			var capDef = DataManager.Instance.Modules.Get("hf_log_capacitor");
			ship.Grid.TryPlaceModule(capDef, new Vector2I(0, -2), rotation: 0, out var cap);
			if (cap != null) cap.CurrentHp = 80.0f;

			var pins = ship.GetAllPins().ToList();
			var coreOut = pins.FirstOrDefault(p => p.OwnerModuleInstanceId == core!.InstanceId && p.Type == PinType.OUT);
			var fuseIn = pins.FirstOrDefault(p => p.OwnerModuleInstanceId == fuse!.InstanceId && p.Type == PinType.IN);
			var fuseOut = pins.FirstOrDefault(p => p.OwnerModuleInstanceId == fuse!.InstanceId && p.Type == PinType.OUT);
			var capIn = pins.FirstOrDefault(p => p.OwnerModuleInstanceId == cap!.InstanceId && p.Type == PinType.IN);

			if (coreOut != null && fuseIn != null) ship.Pipeline.TryAddWire(coreOut, fuseIn, ship.Grid, out _);
			if (fuseOut != null && capIn != null) ship.Pipeline.TryAddWire(fuseOut, capIn, ship.Grid, out _);

			ship.RebuildPhysics();
		}

		private void CreateHUD()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_hudLabel = new Label
			{
				Position = new Vector2(25, 25),
				Size = new Vector2(580, 650)
			};
			_hudLabel.AddThemeFontSizeOverride("font_size", 15);
			_hudLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			canvasLayer.AddChild(_hudLabel);
		}

		public override void _Process(double delta)
		{
			// 玩家开火 (受到过热熔断保护约束)
			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				if (!_playerShip.Thermal.IsOverheated)
				{
					foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
					{
						_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
					}
				}
			}

			// 按 R 键复原所有靶舰与热量
			if (Input.IsKeyPressed(Key.R))
			{
				_playerShip.Thermal.Reset();
				BuildNoFuseTarget(_targetNoFuseShip);
				BuildWithFuseTarget(_targetWithFuseShip);
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			float heatPercent = _playerShip.Thermal.OverheatRatio * 100.0f;
			string heatStatus = _playerShip.Thermal.IsOverheated 
				? $"[color=red]🔥 强制过热熔断停火中 ({_playerShip.Thermal.LockoutTimer:F1}s)[/color]" 
				: (_playerShip.Thermal.IsWarning ? $"[color=yellow]⚠️ 高温预警 ({heatPercent:F0}%)[/color]" : $"[color=green]正常 ({heatPercent:F0}%)[/color]");

			_hudLabel.Text = $"【《断路协议》TASK-21 全舰热力学与防殉爆安全展厅】\n" +
							 $"==================================================\n" +
							 $"全舰发热负荷:   [{heatPercent:F0}% / 100%] | 状态: {heatStatus}\n" +
							 $"散热速率:       {_playerShip.Thermal.CoolingRate:F1} Q/s\n" +
							 $"--------------------------------------------------\n" +
							 $"[三大核心机制验证指南]\n" +
							 $"1. 【开火发热与过热熔断】: 鼠标左键高频连射，热量达 90% 预警，达 100% 全舰【橙红过热泛光并强制停火 3 秒】！\n" +
							 $"2. 【无保险阀殉爆测试】: 射击左侧靶舰外挂电容，电容引爆 400HP 殉爆【连锁炸毁后方核心】！\n" +
							 $"3. 【保险阀防护实测】: 射击右侧靶舰外挂电容，【保险阀 0.1s 极速自熔断，后方核心毫发无损】！\n" +
							 $"--------------------------------------------------\n" +
							 $"[操控] 鼠标瞄准 | 左键开火 | [按 R 键]: 重置全部靶舰与热量";
		}
	}
}
