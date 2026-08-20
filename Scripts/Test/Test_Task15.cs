using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-15 交互式验证场景：船体物理断裂、孤立断肢脱落与残骸太空漂流
	/// </summary>
	public partial class Test_Task15 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private ShipEntity _severableTargetShip = null!;
		private CombatCameraController _camera = null!;
		private Label _hudLabel = null!;

		public override void _Ready()
		{
			// 1. 创建玩家战舰
			_playerShip = new ShipEntity
			{
				Name = "PlayerAttacker",
				Position = new Vector2(600, 560)
			};
			_playerShip.AddToGroup("Player");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}

			// 2. 创建可切断的三段式测试靶舰 (位于前方 600, 220)
			_severableTargetShip = new ShipEntity
			{
				Name = "SeverableTargetShip",
				Position = new Vector2(600, 220),
				Rotation = Mathf.Pi
			};
			AddChild(_severableTargetShip);
			BuildSeverableCruiser(_severableTargetShip);

			// 3. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);

			// 4. UI 遥测面板
			CreateHUD();
		}

		private void BuildSeverableCruiser(ShipEntity ship)
		{
			ship.Grid.Clear();
			ship.Pipeline.Clear();

			// 中央核心段：聚变动力堆 (2x2, 0, 0) + 泰坦主推 (3x2, -1, 2)
			var coreDef = DataManager.Instance.Modules.Get("hf_source_core_2x2");
			ship.Grid.TryPlaceModule(coreDef, new Vector2I(-1, 0), rotation: 0, out _);

			var engDef = DataManager.Instance.Modules.Get("hf_eng_titan_main");
			ship.Grid.TryPlaceModule(engDef, new Vector2I(-1, 2), rotation: 0, out _);

			// 关键连接点：左机翼连接龙骨 (1x1, -2, 0, 脆弱易断点 HP 120)
			var jumperDef = DataManager.Instance.Modules.Get("hf_log_jumper");
			ship.Grid.TryPlaceModule(jumperDef, new Vector2I(-2, 0), rotation: 0, out var leftJoint);
			if (leftJoint != null) leftJoint.CurrentHp = 120.0f; // 设为低血量，方便打断测试

			// 关键连接点：右机翼连接龙骨 (1x1, 1, 0, 脆弱易断点 HP 120)
			ship.Grid.TryPlaceModule(jumperDef, new Vector2I(1, 0), rotation: 0, out var rightJoint);
			if (rightJoint != null) rightJoint.CurrentHp = 120.0f;

			// 左翼挂载群：重型装甲板 (2x2, -4, -1) + 高斯副炮 (2x1, -4, -2)
			var armorDef = DataManager.Instance.Modules.Get("hf_arm_plate_2x2");
			ship.Grid.TryPlaceModule(armorDef, new Vector2I(-4, -1), rotation: 0, out _);
			var gunDef = DataManager.Instance.Modules.Get("mod_custom_gauss_s");
			ship.Grid.TryPlaceModule(gunDef, new Vector2I(-4, -2), rotation: 0, out _);

			// 右翼挂载群：重型装甲板 (2x2, 2, -1) + 高斯副炮 (2x1, 2, -2)
			ship.Grid.TryPlaceModule(armorDef, new Vector2I(2, -1), rotation: 0, out _);
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
			// 鼠标左键开火射击
			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			// 按 R 键一键重组复原靶舰
			if (Input.IsKeyPressed(Key.R))
			{
				BuildSeverableCruiser(_severableTargetShip);
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			int debrisCount = GetTree().GetNodesInGroup("Debris").Count;
			var tGrid = _severableTargetShip.Grid;
			var tPhysics = _severableTargetShip.PhysicsData;

			_hudLabel.Text = $"【《断路协议》TASK-15 船体物理断裂与残骸漂流】\n" +
							 $"==================================================\n" +
							 $"靶舰剩余构件数: {tGrid.ModuleCount} 个 | 质量: {tPhysics.TotalMass:F1} 吨\n" +
							 $"质心偏航偏移:   {tPhysics.CenterOfMassYawOffsetDegrees:F1}° ({(Mathf.Abs(tPhysics.CenterOfMassYawOffsetDegrees) < 0.2f ? "对称" : "严重偏心！")})\n" +
							 $"空间残骸实体数: {debrisCount} 块 (正在真空中翻滚漂流)\n" +
							 $"--------------------------------------------------\n" +
							 $"[战术断舰试验指南]\n" +
							 $"1. 【瞄准左机翼连接点 (紫色小块)】: 连续射击打穿连接龙骨！\n" +
							 $"2. 【肉眼见证整翼断裂脱落】: 龙骨破碎瞬间，整只左翼瞬间断开，受反作用力向侧后方翻滚飞出！\n" +
							 $"3. 【质心重构与偏心】: 靶舰质心瞬间大角度右偏，质量即时扣减！\n" +
							 $"4. 【按 R 键】: 一键满血重置三段式巡洋舰";
		}
	}
}
