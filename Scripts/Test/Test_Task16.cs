using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-16 交互式验证场景：全套像素损毁特效、撞击火花、电弧与爆甲大爆炸
	/// </summary>
	public partial class Test_Task16 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private ShipEntity _targetCruiser = null!;
		private CombatCameraController _camera = null!;
		private VfxManager _vfx = null!;
		private Label _hudLabel = null!;

		public override void _Ready()
		{
			// 1. 创建全局 VFX 特效节点
			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			// 2. 创建玩家战舰 (极寒穿甲磁轨主炮)
			_playerShip = new ShipEntity
			{
				Name = "VfxPlayerShip",
				Position = new Vector2(600, 560)
			};
			_playerShip.AddToGroup("Player");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}

			// 3. 创建靶舰 (600, 220)
			_targetCruiser = new ShipEntity
			{
				Name = "VfxTargetCruiser",
				Position = new Vector2(600, 220),
				Rotation = Mathf.Pi
			};
			AddChild(_targetCruiser);
			BuildTargetCruiser(_targetCruiser);

			// 4. 跟随摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);

			// 5. UI 遥测
			CreateHUD();
		}

		private void BuildTargetCruiser(ShipEntity ship)
		{
			ship.Grid.Clear();
			ship.Pipeline.Clear();

			// 中央核心：聚变反应堆 + 推进器
			var coreDef = DataManager.Instance.Modules.Get("hf_source_core_2x2");
			ship.Grid.TryPlaceModule(coreDef, new Vector2I(-1, 0), rotation: 0, out _);

			var engDef = DataManager.Instance.Modules.Get("hf_eng_titan_main");
			ship.Grid.TryPlaceModule(engDef, new Vector2I(-1, 2), rotation: 0, out _);

			// 左连接龙骨 (弱点，HP 120)
			var jumperDef = DataManager.Instance.Modules.Get("hf_log_jumper");
			ship.Grid.TryPlaceModule(jumperDef, new Vector2I(-2, 0), rotation: 0, out var lJoint);
			if (lJoint != null) lJoint.CurrentHp = 120.0f;

			// 右连接龙骨 (弱点，HP 120)
			ship.Grid.TryPlaceModule(jumperDef, new Vector2I(1, 0), rotation: 0, out var rJoint);
			if (rJoint != null) rJoint.CurrentHp = 120.0f;

			// 舰首斜装甲 (用于跳弹测试)
			var prowDef = DataManager.Instance.Modules.Get("hf_arm_prow_4x1");
			ship.Grid.TryPlaceModule(prowDef, new Vector2I(-2, -1), rotation: 0, out _);

			// 左右舷重装甲与副炮
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
			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			if (Input.IsKeyPressed(Key.R))
			{
				BuildTargetCruiser(_targetCruiser);
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			_hudLabel.Text = $"【《断路协议》TASK-16 像素打击损毁视效展厅】\n" +
							 $"==================================================\n" +
							 $"FPS: {Engine.GetFramesPerSecond()} | 渲染特效活跃中\n" +
							 $"--------------------------------------------------\n" +
							 $"[视效验证指南]\n" +
							 $"1. 【大倾角跳弹火花】: 掠射首部斜装甲，爆发【金黄色密集火花束】！\n" +
							 $"2. 【击穿元素火花】: 垂直击中装甲，爆出【青蓝色极寒光晕与冲击波】！\n" +
							 $"3. 【低血冒烟与电弧】: 构件血量低于 35% 时，局部【冒黑烟与蓝色电弧】！\n" +
							 $"4. 【爆甲大爆炸与破片】: 击碎构件瞬间，爆出【18 块带物理旋转的像素破片】！\n" +
							 $"5. 【残骸燃烧尾烟】: 打断机翼后，断翼在翻滚中【持续拖拽燃烧火星与浓烟】！\n" +
							 $"--------------------------------------------------\n" +
							 $"[按 R 键]: 满血修复重构靶舰";
		}
	}
}
