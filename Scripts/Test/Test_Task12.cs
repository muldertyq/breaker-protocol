using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-12 交互式验证场景：全势力 28 构件与 9 艘战舰蓝图一键切换展厅
	/// </summary>
	public partial class Test_Task12 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private Label _telemetryLabel = null!;
		private VBoxContainer _blueprintListContainer = null!;

		private BlueprintDataDefinition? _currentBlueprint;

		public override void _Ready()
		{
			// 1. 创建飞船实体
			_playerShip = new ShipEntity
			{
				Name = "ShowroomShip",
				Position = new Vector2(600, 360)
			};
			_playerShip.AddToGroup("Player");
			AddChild(_playerShip);

			// 2. 创建摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);

			// 3. 构建 UI 面板
			CreateShowroomUI();

			// 4. 默认加载第一艘战舰 (铁砧级)
			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var defaultBp))
			{
				LoadBlueprint(defaultBp!);
			}
		}

		private void CreateShowroomUI()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			// 左侧蓝图选择列表
			var leftPanel = new PanelContainer
			{
				Position = new Vector2(20, 20),
				Size = new Vector2(260, 680)
			};
			canvasLayer.AddChild(leftPanel);

			var leftVBox = new VBoxContainer();
			leftPanel.AddChild(leftVBox);

			var title = new Label { Text = "【 9 大预设战舰档案 】" };
			title.AddThemeFontSizeOverride("font_size", 18);
			title.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			leftVBox.AddChild(title);

			var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(240, 620) };
			leftVBox.AddChild(scroll);

			_blueprintListContainer = new VBoxContainer();
			scroll.AddChild(_blueprintListContainer);

			// 填充 9 艘预设战舰按钮
			foreach (var bp in DataManager.Instance.Blueprints.GetAll())
			{
				var capturedBp = bp;
				string factionTag = bp.Faction switch
				{
					"HeavyFoundry" => "[重工]",
					"VoidSyndicate" => "[虚空]",
					"BioChitin"     => "[生化]",
					_ => "[通用]"
				};

				var btn = new Button
				{
					Text = $"{factionTag} {bp.Name}\n(阶梯: {bp.HullClass} 级 | 构件: {bp.Modules.Count} 个)",
					Alignment = HorizontalAlignment.Left
				};

				btn.Pressed += () => LoadBlueprint(capturedBp);
				_blueprintListContainer.AddChild(btn);
			}

			// 右侧遥测监控面板
			_telemetryLabel = new Label
			{
				Position = new Vector2(980, 20),
				Size = new Vector2(280, 680)
			};
			_telemetryLabel.AddThemeFontSizeOverride("font_size", 15);
			_telemetryLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			canvasLayer.AddChild(_telemetryLabel);
		}

		private void LoadBlueprint(BlueprintDataDefinition blueprint)
		{
			_currentBlueprint = blueprint;
			ShipBlueprintLoader.ApplyBlueprint(_playerShip, blueprint);
			UpdateTelemetry();
		}

		public override void _Process(double delta)
		{
			// 鼠标左键触发主炮开火
			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			UpdateTelemetry();
		}

		private void UpdateTelemetry()
		{
			if (_currentBlueprint == null) return;

			var p = _playerShip.PhysicsData;
			var f = _playerShip.Flight;
			float speedMeters = GlobalMetrics.PixelsToMeters(_playerShip.LinearVelocity.Length());

			_telemetryLabel.Text = $"【《断路协议》TASK-12 战舰档案遥测】\n" +
								   $"====================================\n" +
								   $"当前战舰:   {_currentBlueprint.Name}\n" +
								   $"所属阵营:   {_currentBlueprint.Faction}\n" +
								   $"舰体吨位:   {_currentBlueprint.HullClass} 级舰体\n" +
								   $"------------------------------------\n" +
								   $"全舰总质量: {p.TotalMass:F1} 吨 (t)\n" +
								   $"转动惯量:   {p.MomentOfInertia:F1} t·m²\n" +
								   $"构件总数:   {_playerShip.Grid.ModuleCount} 个\n" +
								   $"PCB 导线条: {_playerShip.Pipeline.WireCount} 条\n" +
								   $"主推前推力: {f.ThrustCapability.MaxForwardThrust:F0} N\n" +
								   $"当前航速:   {speedMeters:F1} m/s\n" +
								   $"------------------------------------\n" +
								   $"[蓝图描述]\n" +
								   $"{_currentBlueprint.Description}\n" +
								   $"------------------------------------\n" +
								   $"[全数据库统计]\n" +
								   $"■ 构件库注册总数: {DataManager.Instance.Modules.Count} 个\n" +
								   $"■ 预设蓝图注册数: {DataManager.Instance.Blueprints.Count} 艘\n" +
								   $"------------------------------------\n" +
								   $"[操作] 点击左侧切换战舰 | WASD 试驾 | 左键开火";
		}
	}
}
