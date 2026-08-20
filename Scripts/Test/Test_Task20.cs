using System.Collections.Generic;
using System.Linq;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-20 交互式验证场景：舰内 2D DDA 射线步进创伤、手术刀式截断铜排与哑火实测
	/// </summary>
	public partial class Test_Task20 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private ShipEntity _targetCorridorShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private Label _hudLabel = null!;

		private float _targetShootTimer = 0.0f;

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			// 1. 创建玩家战舰
			_playerShip = new ShipEntity
			{
				Name = "SniperPlayerShip",
				Position = new Vector2(600, 580)
			};
			_playerShip.AddToGroup("Player");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}

			// 2. 创建走廊穿透测试靶舰
			_targetCorridorShip = new ShipEntity
			{
				Name = "TargetCorridorShip",
				Position = new Vector2(600, 200),
				Rotation = Mathf.Pi
			};
			AddChild(_targetCorridorShip);
			BuildCorridorShip(_targetCorridorShip);

			// 3. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateHUD();
		}

		private void BuildCorridorShip(ShipEntity ship)
		{
			ship.Grid.Clear();
			ship.Pipeline.Clear();

			// 1. 舰尾：聚变核心 (2x2, -1, 2)
			var coreDef = DataManager.Instance.Modules.Get("hf_source_core_2x2");
			ship.Grid.TryPlaceModule(coreDef, new Vector2I(-1, 2), rotation: 0, out var core);

			// 2. 舰首两侧：高斯副炮 (左翼 -3, -2, 右翼 1, -2)
			var gunDef = DataManager.Instance.Modules.Get("mod_custom_gauss_s");
			ship.Grid.TryPlaceModule(gunDef, new Vector2I(-3, -2), rotation: 0, out var leftGun);
			ship.Grid.TryPlaceModule(gunDef, new Vector2I(1, -2), rotation: 0, out var rightGun);

			// 3. 舰首中央：前装甲板 (2x1, -1, -2)
			var armorDef = DataManager.Instance.Modules.Get("vs_arm_flux_shield_2x1");
			ship.Grid.TryPlaceModule(armorDef, new Vector2I(-1, -2), rotation: 0, out _);

			// 4. 使用原版 TryAddWire 与曼哈顿 A* 自动寻路布线
			var allPins = ship.GetAllPins().ToList();
			var outPins = allPins.Where(p => p.OwnerModuleInstanceId == core!.InstanceId && p.Definition.Type == "OUT").ToList();
			var inPinLeft = allPins.FirstOrDefault(p => p.OwnerModuleInstanceId == leftGun!.InstanceId && p.Definition.Type == "IN");
			var inPinRight = allPins.FirstOrDefault(p => p.OwnerModuleInstanceId == rightGun!.InstanceId && p.Definition.Type == "IN");

			if (outPins.Count > 0 && inPinLeft != null)
			{
				ship.Pipeline.TryAddWire(outPins[0], inPinLeft, ship.Grid, out _);
			}
			if (outPins.Count > 1 && inPinRight != null)
			{
				ship.Pipeline.TryAddWire(outPins[1], inPinRight, ship.Grid, out _);
			}

			ship.RebuildPhysics();
		}

		private void CreateHUD()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_hudLabel = new Label
			{
				Position = new Vector2(25, 25),
				Size = new Vector2(560, 650)
			};
			_hudLabel.AddThemeFontSizeOverride("font_size", 15);
			_hudLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			canvasLayer.AddChild(_hudLabel);
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;

			// 玩家开火
			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			// 靶舰自动开火 (通电状态下每 0.8s 射击)
			_targetShootTimer += dt;
			if (_targetShootTimer >= 0.8f)
			{
				_targetShootTimer = 0.0f;
				foreach (var weaponId in _targetCorridorShip.Pulses.WeaponBuffers.Keys)
				{
					_targetCorridorShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			// 按 R 键复原靶舰
			if (Input.IsKeyPressed(Key.R))
			{
				BuildCorridorShip(_targetCorridorShip);
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			int severedCount = 0;
			foreach (var w in _targetCorridorShip.Pipeline.Wires)
			{
				if (w.IsSevered) severedCount++;
			}

			int totalTargetBufferedPulses = 0;
			foreach (var b in _targetCorridorShip.Pulses.WeaponBuffers.Values)
			{
				totalTargetBufferedPulses += b.BufferedCount;
			}

			_hudLabel.Text = $"【《断路协议》TASK-20 舰内 2D DDA 射线步进创伤展厅】\n" +
							 $"==================================================\n" +
							 $"靶舰供电导线总数: {_targetCorridorShip.Pipeline.WireCount} 条 | 已被切断: {severedCount} 条\n" +
							 $"靶舰副炮暂存电量: {totalTargetBufferedPulses} 发 ({(severedCount >= 2 ? "[color=red]❌ 全舰供电回路阻断，副炮已彻底断电哑火！[/color]" : "[color=green]⚡ 供电回路通畅，副炮持续开火中[/color]")})\n" +
							 $"--------------------------------------------------\n" +
							 $"[内构手术刀式破坏验证指南]\n" +
							 $"1. 【靶舰初始状态】: 两侧副炮持续自动射击，中央走廊清晰可见金黄色的供电铜排；\n" +
							 $"2. 【瞄准中央走廊射击】: 对准靶舰正面中央前甲射击，重穿甲弹击穿前甲进入内部；\n" +
							 $"3. 【2D DDA 网格射线步进】: 射线在走廊内扫过，【精准切断中央走廊铜排】！\n" +
							 $"4. 【断线红叉与瞬间哑火】: 走廊铜排爆出短路电弧并挂上【红色断线叉号 ❌】，副炮耗尽电容后瞬间哑火！\n" +
							 $"--------------------------------------------------\n" +
							 $"[操控] 鼠标瞄准 | 左键开火 | [按 R 键]: 满血重置走廊靶舰";
		}
	}
}
