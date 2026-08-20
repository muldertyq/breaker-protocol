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
	/// TASK-22 交互式验证场景：战地应急飞线系统 (Hotwiring) 与 0.2x 战术子弹时间绝地反杀
	/// </summary>
	public partial class Test_Task22 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private ShipEntity _bossTargetShip = null!;
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

			// 1. 创建玩家战舰 (位于 600, 600)
			_playerShip = new ShipEntity
			{
				Name = "HotwirePlayerShip",
				Position = new Vector2(600, 600)
			};
			_playerShip.AddToGroup("Player");
			AddChild(_playerShip);
			BuildSeveredPlayerShip(_playerShip);

			// 2. 创建前方重型装甲靶舰 (位于 600, 220, 舰首朝下)
			_bossTargetShip = new ShipEntity
			{
				Name = "BossTargetShip",
				Position = new Vector2(600, 220),
				Rotation = Mathf.Pi
			};
			AddChild(_bossTargetShip);
			BuildBossTargetShip(_bossTargetShip);

			// 3. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateHUD();
		}

		private void BuildSeveredPlayerShip(ShipEntity ship)
		{
			ship.Grid.Clear();
			ship.Pipeline.Clear();

			// 1. 舰尾：聚变核心 (2x2, -1, 1)
			var coreDef = DataManager.Instance.Modules.Get("hf_source_core_2x2");
			ship.Grid.TryPlaceModule(coreDef, new Vector2I(-1, 1), rotation: 0, out var core);

			// 2. 舰首：重型磁轨主炮 (2x2, -1, -3)
			var gunDef = DataManager.Instance.Modules.Get("hf_wep_railgun_h");
			ship.Grid.TryPlaceModule(gunDef, new Vector2I(-1, -3), rotation: 0, out var mainGun);

			// 3. 两翼装甲
			var armorDef = DataManager.Instance.Modules.Get("hf_arm_plate_2x2");
			ship.Grid.TryPlaceModule(armorDef, new Vector2I(-3, -1), rotation: 0, out _);
			ship.Grid.TryPlaceModule(armorDef, new Vector2I(1, -1), rotation: 0, out _);

			// 4. 推进器
			var engDef = DataManager.Instance.Modules.Get("hf_eng_titan_main");
			ship.Grid.TryPlaceModule(engDef, new Vector2I(-1, 3), rotation: 0, out _);

			// 5. 初始状态：故意建立一条【已被击断】的主炮导线 (DurabilityHp = 0, 划红叉 ❌)
			var pins = ship.GetAllPins().ToList();
			var outPin = pins.FirstOrDefault(p => p.OwnerModuleInstanceId == core!.InstanceId && p.Type == PinType.OUT);
			var inPin = pins.FirstOrDefault(p => p.OwnerModuleInstanceId == mainGun!.InstanceId && p.Type == PinType.IN);

			if (outPin != null && inPin != null)
			{
				if (ship.Pipeline.TryAddWire(outPin, inPin, ship.Grid, out var wire))
				{
					if (wire != null) wire.DurabilityHp = 0.0f; // 标记断线！
				}
			}

			ship.Hotwire.Reset();
			ship.RebuildPhysics();
		}

		private void BuildBossTargetShip(ShipEntity ship)
		{
			ship.Grid.Clear();
			ship.Pipeline.Clear();

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_l_ironclad", out var ironcladBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(ship, ironcladBp!);
			}
			else
			{
				var coreDef = DataManager.Instance.Modules.Get("hf_source_core_2x2");
				ship.Grid.TryPlaceModule(coreDef, new Vector2I(-1, 0), rotation: 0, out _);
				ship.RebuildPhysics();
			}
		}

		private void CreateHUD()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_hudLabel = new Label
			{
				Position = new Vector2(25, 25),
				Size = new Vector2(600, 650)
			};
			_hudLabel.AddThemeFontSizeOverride("font_size", 15);
			_hudLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			canvasLayer.AddChild(_hudLabel);
		}

		public override void _Process(double delta)
		{
			// 玩家开火
			if (Input.IsMouseButtonPressed(MouseButton.Left) && !_playerShip.Hotwire.IsInHotwireMode)
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			// 按 R 键复原
			if (Input.IsKeyPressed(Key.R))
			{
				BuildSeveredPlayerShip(_playerShip);
				BuildBossTargetShip(_bossTargetShip);
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			int hotwireCount = _playerShip.Pipeline.Wires.Count(w => w.IsHotwire && !w.IsSevered);
			int severedCount = _playerShip.Pipeline.Wires.Count(w => w.IsSevered);
			int totalBuffered = _playerShip.Pulses.WeaponBuffers.Values.Sum(b => b.BufferedCount);

			string hotwireState = _playerShip.Hotwire.IsInHotwireMode 
				? $"[color=yellow]⏱️ 战术子弹时间激活 (0.2x) | 剩余时间: {_playerShip.Hotwire.CurrentBulletTimeTimer:F1}s[/color]" 
				: "[color=green]常速战斗态[/color]";

			_hudLabel.Text = $"【《断路协议》TASK-22 战地应急飞线与子弹时间展厅】\n" +
							 $"==================================================\n" +
							 $"模式状态:       {hotwireState}\n" +
							 $"飞线补丁库存:   [{_playerShip.Hotwire.HotwirePatchesRemaining} / 5] 个\n" +
							 $"全舰导线状态:   已断线={severedCount} 条 | 应急飞线={hotwireCount} 条\n" +
							 $"主炮就绪弹量:   {totalBuffered} 发 ({(totalBuffered > 0 ? "[color=green]⚡ 主炮已通电就绪！[/color]" : "[color=red]❌ 供电断开，主炮哑火！[/color]")})\n" +
							 $"--------------------------------------------------\n" +
							 $"[战地飞线绝地反杀操作指南]\n" +
							 $"1. 【开局哑火】: 尝试鼠标左键开火，主炮由于初始断线（红色 ❌）无法发射；\n" +
							 $"2. 【按住 F 键】: 进入【0.2x 战术子弹时间】，全舰引脚亮起绿色 (OUT) 与橙色 (IN) 光环；\n" +
							 $"3. 【划线搭桥】: 鼠标左键点击核心绿色 OUT 引脚，按住拖拽至主炮橙色 IN 引脚松开；\n" +
							 $"4. 【虚线飞线接通】: 瞬间消耗 1 个补丁生成【黄色虚线飞线】，主炮瞬间开始充电！\n" +
							 $"5. 【绝地反杀】: 松开 F 键恢复常速，鼠标左键一枪将正前方巨舰轰爆！\n" +
							 $"--------------------------------------------------\n" +
							 $"[操控] 按住 [F 键] 飞线 | 鼠标瞄准/拖拽 | 左键开火 | [按 R 键]: 重置战场";
		}
	}
}
