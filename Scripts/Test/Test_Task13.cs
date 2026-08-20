using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-13 交互式验证场景：全武器实弹开火、激光束渲染与靶场打靶
	/// </summary>
	public partial class Test_Task13 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private Label _hudLabel = null!;

		public override void _Ready()
		{
			// 1. 创建飞船实体
			_playerShip = new ShipEntity
			{
				Name = "CombatPlayerShip",
				Position = new Vector2(600, 500)
			};
			_playerShip.AddToGroup("Player");
			AddChild(_playerShip);

			// 2. 默认加载“铁砧”级中型重装护卫舰
			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}

			// 3. 摄像机跟随
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);

			// 4. 生成 3 个受击测试靶标 (分布在前方)
			SpawnDummies();

			// 5. 创建 HUD 面板
			CreateHUD();
		}

		private void SpawnDummies()
		{
			Vector2[] positions =
			{
				new Vector2(600, 150),
				new Vector2(400, 100),
				new Vector2(800, 100)
			};

			foreach (var pos in positions)
			{
				var dummy = new TargetDummy
				{
					GlobalPosition = pos
				};
				AddChild(dummy);
			}
		}

		private void CreateHUD()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_hudLabel = new Label
			{
				Position = new Vector2(25, 25),
				Size = new Vector2(480, 550)
			};
			_hudLabel.AddThemeFontSizeOverride("font_size", 15);
			_hudLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			canvasLayer.AddChild(_hudLabel);
		}

		public override void _Process(double delta)
		{
			// 武器切换热键
			if (Input.IsKeyPressed(Key.Key1))
			{
				if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var bp))
					ShipBlueprintLoader.ApplyBlueprint(_playerShip, bp!);
			}
			else if (Input.IsKeyPressed(Key.Key2))
			{
				if (DataManager.Instance.Blueprints.TryGet("bp_vs_m_prism", out var bp))
					ShipBlueprintLoader.ApplyBlueprint(_playerShip, bp!);
			}
			else if (Input.IsKeyPressed(Key.Key3))
			{
				if (DataManager.Instance.Blueprints.TryGet("bp_bc_m_carapace", out var bp))
					ShipBlueprintLoader.ApplyBlueprint(_playerShip, bp!);
			}

			// 鼠标左键：开火射击
			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			// 将节点统计提取为局部变量，避免在字符串插值中出现转义引号导致的语法解析错误
			int activeProjectiles = GetTree().GetNodesInGroup("Projectile").Count;

			_hudLabel.Text = $"【《断路协议》TASK-13 武器发射与实体弹道靶场】\n" +
							 $"==================================================\n" +
							 $"当前战舰构型:   {_playerShip.Name}\n" +
							 $"主武器总数:     {_playerShip.Pulses.WeaponBuffers.Count} 门\n" +
							 $"在途物理子弹:   {activeProjectiles} 发\n" +
							 $"--------------------------------------------------\n" +
							 $"[武器实弹测试热键]\n" +
							 $"[按 1 键]: 重工“铁砧”级 (极寒磁轨重炮 + 穿甲青蓝拖尾)\n" +
							 $"[按 2 键]: 虚空“棱镜”级 (多棱镜 3 向分裂相位死光激光)\n" +
							 $"[按 3 键]: 生化“甲壳”级 (强酸热核燃烧自爆虫巢)\n" +
							 $"--------------------------------------------------\n" +
							 $"[操控] 鼠标瞄准 | 左键开火 | WASD 推进 | Space 漂移";
		}
	}
}
