using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-14 交互式验证场景：多层复合装甲、大倾角跳弹偏转与爆甲重构
	/// </summary>
	public partial class Test_Task14 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private ShipEntity _targetArmoredShip = null!;
		private CombatCameraController _camera = null!;
		private Label _hudLabel = null!;

		public override void _Ready()
		{
			// 1. 创建玩家战舰 (搭载重型高初速穿甲磁轨炮)
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

			// 2. 创建用于受击测试的重型装甲靶舰 (位于前方 600, 200)
			_targetArmoredShip = new ShipEntity
			{
				Name = "TargetArmoredShip",
				Position = new Vector2(600, 200),
				Rotation = Mathf.Pi // 舰首朝下，正对玩家
			};
			AddChild(_targetArmoredShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var targetBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_targetArmoredShip, targetBp!);
			}

			// 3. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);

			// 4. UI 遥测面板
			CreateHUD();
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
			// 鼠标左键开火
			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			// 按 R 键重置/复原靶舰
			if (Input.IsKeyPressed(Key.R))
			{
				if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var targetBp))
				{
					ShipBlueprintLoader.ApplyBlueprint(_targetArmoredShip, targetBp!);
				}
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			var tGrid = _targetArmoredShip.Grid;
			var tPhysics = _targetArmoredShip.PhysicsData;

			string moduleStatus = string.Empty;
			foreach (var m in tGrid.Modules)
			{
				string hpColor = m.CurrentHp > (m.MaxHp * 0.5f) ? "green" : (m.CurrentHp > 0 ? "yellow" : "red");
				moduleStatus += $"• [{m.Definition.Name}] HP: [color={hpColor}]{m.CurrentHp:F0}/{m.MaxHp:F0}[/color]\n";
			}

			_hudLabel.Text = $"【《断路协议》TASK-14 装甲力学与跳弹测试靶场】\n" +
							 $"==================================================\n" +
							 $"目标靶舰构件数: {tGrid.ModuleCount} 个 | 质量: {tPhysics.TotalMass:F1} 吨\n" +
							 $"生化自愈状态:   {(_targetArmoredShip.BioRegen.IsRegenerating ? "[color=green]自愈激活中 (+15HP/s)[/color]" : "[color=orange]处于交火受击状态[/color]")}\n" +
							 $"--------------------------------------------------\n" +
							 $"[靶舰实时构件耐久状态]\n" +
							 $"{moduleStatus}" +
							 $"--------------------------------------------------\n" +
							 $"[深度测试指南]\n" +
							 $"1. 【斜向射击首装甲】: 移动至侧方大角度射击首装甲，观察子弹以物理反射角高亮弹开！\n" +
							 $"2. 【正面垂直贯穿】: 垂直对准舰体中心连续射击，打穿外层首装甲并逐层贯穿！\n" +
							 $"3. 【爆甲物理重构】: 将某块装甲血量打空，观察构件瞬间解体爆碎，靶舰质量与碰撞箱即时更新！\n" +
							 $"4. 【按 R 键】: 一键满血修复并复原靶舰构型";
		}
	}
}
