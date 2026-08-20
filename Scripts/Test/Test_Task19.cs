using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-19 交互式验证场景：双路混流元素合成矩阵与四重化学/物理反应状态机
	/// </summary>
	public partial class Test_Task19 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private ShipEntity _targetShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private Label _hudLabel = null!;

		private ElementFlags _currentCustomElement = ElementFlags.Thermal | ElementFlags.Cryo; // 默认热冲击
		private string _elementModeName = "💥 热冲击 (Fire + Cryo)";

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
				Name = "SynthesizerPlayerShip",
				Position = new Vector2(600, 560)
			};
			_playerShip.AddToGroup("Player");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}

			// 2. 创建靶舰 (600, 220, 舰首朝下)
			_targetShip = new ShipEntity
			{
				Name = "TargetCruiser",
				Position = new Vector2(600, 220),
				Rotation = Mathf.Pi
			};
			AddChild(_targetShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_l_ironclad", out var targetBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_targetShip, targetBp!);
			}

			// 3. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateHUD();
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

			// 切换混流合成配方
			if (Input.IsKeyPressed(Key.Key1))
			{
				_currentCustomElement = ElementFlags.Thermal | ElementFlags.Cryo;
				_elementModeName = "💥 热冲击 (Thermal Shock = Fire + Cryo)";
			}
			else if (Input.IsKeyPressed(Key.Key2))
			{
				_currentCustomElement = ElementFlags.Thermal | ElementFlags.Acid;
				_elementModeName = "☣️ 爆燃毒爆 (Acid Combustion = Fire + Acid)";
			}
			else if (Input.IsKeyPressed(Key.Key3))
			{
				_currentCustomElement = ElementFlags.Cryo | ElementFlags.Void;
				_elementModeName = "❄️ 绝对零度 (Absolute Zero = Cryo + Void)";
			}
			else if (Input.IsKeyPressed(Key.Key4))
			{
				_currentCustomElement = ElementFlags.Acid | ElementFlags.Void;
				_elementModeName = "🌌 熵增噬灭 (Entropy Collapse = Acid + Void)";
			}

			// 玩家开火：传入当前选中的合成元素
			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _, _currentCustomElement);
				}
			}

			// 靶舰自动射击逻辑 (每 1.2 秒开火一次，用于演示定身与炸膛反噬)
			_targetShootTimer += dt;
			if (_targetShootTimer >= 1.2f)
			{
				_targetShootTimer = 0.0f;
				foreach (var weaponId in _targetShip.Pulses.WeaponBuffers.Keys)
				{
					_targetShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			// 按 R 键复原靶舰
			if (Input.IsKeyPressed(Key.R))
			{
				if (DataManager.Instance.Blueprints.TryGet("bp_hf_l_ironclad", out var targetBp))
				{
					ShipBlueprintLoader.ApplyBlueprint(_targetShip, targetBp!);
				}
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			var targetStatus = ElementalSynthesisMatrix.GetOrCreateStatus(_targetShip);

			_hudLabel.Text = $"【《断路协议》TASK-19 双路混流元素合成矩阵】\n" +
							 $"==================================================\n" +
							 $"当前混流弹药:   {_elementModeName}\n" +
							 $"--------------------------------------------------\n" +
							 $"【靶舰实时受击异常监控】\n" +
							 $"■ 冰冻定身状态: {(targetStatus.IsFrozen ? $"[color=cyan]❄️ 绝对零度冻结中 ({targetStatus.FreezeLockTimer:F1}s)[/color]" : "正常开火航行")}\n" +
							 $"■ 熵增炸膛印记: {(targetStatus.HasEntropyCurse ? $"[color=purple]🌌 噬灭炸膛印记 ({targetStatus.EntropyCurseTimer:F1}s)[/color]" : "无")}\n" +
							 $"■ 生化火海残留: {(targetStatus.AcidPoolTimer > 0 ? $"[color=green]☣️ 毒火燃烧中 ({targetStatus.AcidPoolTimer:F1}s)[/color]" : "无")}\n" +
							 $"--------------------------------------------------\n" +
							 $"[四大合成反应测试指南]\n" +
							 $"[按 1 键]: 💥 热冲击 (Fire+Cryo) -> 击中爆发紫粉光斑，头顶跳字【热冲击 碎甲 -150】！\n" +
							 $"[按 2 键]: ☣️ 爆燃毒爆 (Fire+Acid) -> 击中在靶舰下方生成【4秒绿色沸腾生化火海】！\n" +
							 $"[按 3 键]: ❄️ 绝对零度 (Cryo+Void) -> 击中靶舰出现【青蓝冰晶光环】，靶舰强制熄火停止开火！\n" +
							 $"[按 4 键]: 🌌 熵增噬灭 (Acid+Void) -> 击中出现【紫色光环】，靶舰开火时内部爆出 EMP 紫电自扣 100 HP！\n" +
							 $"--------------------------------------------------\n" +
							 $"[操控] 鼠标瞄准 | 左键开火 | [按 R 键]: 满血重置铁甲级靶舰";
		}
	}
}
