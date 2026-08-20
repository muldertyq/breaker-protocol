using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.Ship.Physics;
using BreakerProtocol.Ship.Validation;

namespace BreakerProtocol.UI.Refit
{
	/// <summary>
	/// 全屏蓝图装配与改装工作台 UI 控制器
	/// </summary>
	public partial class RefitCanvasUI : Control
	{
		// 目标战舰实体引用
		private ShipEntity _targetShip = null!;

		// 当前选中的待放置构件定义（若为 null 表示选择/拆卸模式）
		private ModuleDataDefinition? _selectedModuleDef;
		private int _currentRotation = 0; // 0, 1, 2, 3

		// 画布中心在屏幕上的像素偏移点
		private Vector2 _canvasOrigin = new(640, 360);
		private const float GridPixelSize = 32.0f; // 装配视图下 1 GU 放大为 32px

		// UI 控件节点
		private PanelContainer _leftPanel = null!;
		private VBoxContainer _moduleListContainer = null!;
		private PanelContainer _rightPanel = null!;
		private Label _telemetryLabel = null!;
		private Button _confirmButton = null!;

		// 实时校验状态
		private ShipValidationReport _currentReport = new();

		public void Initialize(ShipEntity targetShip)
		{
			_targetShip = targetShip;
			BuildUIHierarchy();
			PopulateModuleCatalog("All");
			UpdateTelemetryAndValidation();
		}

		private void BuildUIHierarchy()
		{
			// 铺满全屏
			SetAnchorsPreset(LayoutPreset.FullRect);
			MouseFilter = MouseFilterEnum.Ignore;

			// 1. 左侧构件工具箱面板
			_leftPanel = new PanelContainer
			{
				Position = new Vector2(20, 20),
				Size = new Vector2(240, 680)
			};
			AddChild(_leftPanel);

			var leftVBox = new VBoxContainer();
			_leftPanel.AddChild(leftVBox);

			var leftTitle = new Label { Text = "【 构件军械库 】" };
			leftTitle.AddThemeFontSizeOverride("font_size", 18);
			leftTitle.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			leftVBox.AddChild(leftTitle);

			// 分类过滤按钮行
			var filterHBox = new HBoxContainer();
			leftVBox.AddChild(filterHBox);

			string[] categories = { "全部", "动力", "武器", "修饰", "装甲", "推进" };
			string[] catKeys = { "All", "PowerSource", "Weapon", "Modifier", "Armor", "Thruster" };

			for (int i = 0; i < categories.Length; i++)
			{
				string key = catKeys[i];
				var btn = new Button { Text = categories[i] };
				btn.Pressed += () => PopulateModuleCatalog(key);
				filterHBox.AddChild(btn);
			}

			// 可滚动的构件列表
			var scroll = new ScrollContainer
			{
				CustomMinimumSize = new Vector2(220, 580)
			};
			leftVBox.AddChild(scroll);

			_moduleListContainer = new VBoxContainer();
			scroll.AddChild(_moduleListContainer);

			// 2. 右侧遥测与安全合规面板
			_rightPanel = new PanelContainer
			{
				Position = new Vector2(1020, 20),
				Size = new Vector2(240, 680)
			};
			AddChild(_rightPanel);

			var rightVBox = new VBoxContainer();
			_rightPanel.AddChild(rightVBox);

			var rightTitle = new Label { Text = "【 飞船力学与安全校验 】" };
			rightTitle.AddThemeFontSizeOverride("font_size", 18);
			rightTitle.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			rightVBox.AddChild(rightTitle);

			_telemetryLabel = new Label
			{
				CustomMinimumSize = new Vector2(220, 480)
			};
			_telemetryLabel.AddThemeFontSizeOverride("font_size", 14);
			rightVBox.AddChild(_telemetryLabel);

			// 确认实装按钮
			_confirmButton = new Button
			{
				Text = "确认实装并启航 (Tab)",
				CustomMinimumSize = new Vector2(220, 45)
			};
			_confirmButton.Pressed += OnConfirmPressed;
			rightVBox.AddChild(_confirmButton);
		}

		private void PopulateModuleCatalog(string categoryFilter)
		{
			foreach (var child in _moduleListContainer.GetChildren())
			{
				child.QueueFree();
			}

			foreach (var moduleDef in DataManager.Instance.Modules.GetAll())
			{
				if (categoryFilter != "All" && moduleDef.Category != categoryFilter) continue;

				var cardBtn = new Button
				{
					Text = $"{moduleDef.Name}\n[{moduleDef.Width}x{moduleDef.Height} GU | {moduleDef.Mass:F1}t | {moduleDef.BaseHp:F0}HP]",
					Alignment = HorizontalAlignment.Left
				};

				var capturedDef = moduleDef;
				cardBtn.Pressed += () =>
				{
					_selectedModuleDef = capturedDef;
					_currentRotation = 0;
					GD.Print($"[RefitCanvas] 已选中待装配构件: [{capturedDef.Name}]");
				};

				_moduleListContainer.AddChild(cardBtn);
			}
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (!Visible) return;

			if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
			{
				// 按 R 键顺时针旋转 90度
				if (keyEvent.Keycode == Key.R)
				{
					_currentRotation = (_currentRotation + 1) % 4;
					QueueRedraw();
				}
			}

			if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed)
			{
				Vector2 localMouse = GetLocalMousePosition();
				Vector2I gridHoverPos = ScreenToGrid(localMouse);

				// 鼠标左键：放置当前选中的构件
				if (mouseBtn.ButtonIndex == MouseButton.Left && _selectedModuleDef != null)
				{
					if (_targetShip.Grid.TryPlaceModule(_selectedModuleDef, gridHoverPos, _currentRotation, out _))
					{
						UpdateTelemetryAndValidation();
						QueueRedraw();
					}
				}
				// 鼠标右键：拆除/删除光标处的构件
				else if (mouseBtn.ButtonIndex == MouseButton.Right)
				{
					var existingModule = _targetShip.Grid.GetModuleAt(gridHoverPos);
					if (existingModule != null)
					{
						_targetShip.Grid.RemoveModule(existingModule.InstanceId);
						UpdateTelemetryAndValidation();
						QueueRedraw();
					}
					else
					{
						// 若空地右键，取消当前选中的放置构件
						_selectedModuleDef = null;
						QueueRedraw();
					}
				}
			}
		}

		public override void _Process(double delta)
		{
			if (Visible)
			{
				// 鼠标在画布上移动时持续重绘幽灵预览
				QueueRedraw();
			}
		}

		public override void _Draw()
		{
			if (!Visible || _targetShip == null) return;

			// ==========================================
			// 1. 绘制底层坐标网格 (-10 到 +10 GU)
			// ==========================================
			for (int x = -10; x <= 10; x++)
			{
				for (int y = -10; y <= 10; y++)
				{
					Vector2 pos = GridToScreen(new Vector2I(x, y));
					DrawRect(new Rect2(pos, new Vector2(GridPixelSize, GridPixelSize)), new Color(0.12f, 0.15f, 0.20f, 0.7f), filled: false, width: 1.0f);
				}
			}

			// 绘制 X/Y 轴中线
			DrawLine(new Vector2(_canvasOrigin.X - 350, _canvasOrigin.Y), new Vector2(_canvasOrigin.X + 350, _canvasOrigin.Y), new Color(0.3f, 0.4f, 0.5f, 0.5f), 1.5f);
			DrawLine(new Vector2(_canvasOrigin.X, _canvasOrigin.Y - 350), new Vector2(_canvasOrigin.X, _canvasOrigin.Y + 350), new Color(0.3f, 0.4f, 0.5f, 0.5f), 1.5f);

			// ==========================================
			// 2. 绘制所有已实装的构件实体
			// ==========================================
			foreach (var module in _targetShip.Grid.Modules)
			{
				Color moduleColor = module.Definition.Category switch
				{
					"PowerSource" => new Color(0.15f, 0.45f, 0.95f, 0.85f),
					"Modifier"    => new Color(0.15f, 0.85f, 0.75f, 0.85f),
					"Weapon"      => new Color(0.95f, 0.25f, 0.25f, 0.85f),
					"Armor"       => new Color(0.50f, 0.50f, 0.55f, 0.90f),
					"Thruster"    => new Color(1.00f, 0.55f, 0.10f, 0.85f),
					_             => new Color(0.70f, 0.70f, 0.70f, 0.85f)
				};

				foreach (var cellPos in module.GetOccupiedGridCells())
				{
					Vector2 screenPos = GridToScreen(cellPos);
					DrawRect(new Rect2(screenPos + Vector2.One * 2, new Vector2(GridPixelSize - 4, GridPixelSize - 4)), moduleColor, filled: true);
				}

				// 绘制引脚端口 (绿点 IN, 蓝点 OUT)
				foreach (var (pinDef, pinPos) in module.GetTransformedPins())
				{
					Vector2 pinCenter = GridToScreen(pinPos) + new Vector2(GridPixelSize * 0.5f, GridPixelSize * 0.5f);
					Color pinColor = pinDef.Type == "IN" ? new Color(0.2f, 1.0f, 0.3f) : new Color(0.2f, 0.6f, 1.0f);
					DrawCircle(pinCenter, 5.0f, pinColor);
					DrawCircle(pinCenter, 2.5f, Colors.White);
				}
			}

			// ==========================================
			// 3. 绘制鼠标悬停处的半透明“幽灵构件预览”
			// ==========================================
			if (_selectedModuleDef != null)
			{
				Vector2I hoverGrid = ScreenToGrid(GetLocalMousePosition());
				var tempInstance = new ModuleInstance("ghost", _selectedModuleDef, hoverGrid, _currentRotation);

				// 检查是否允许放置 (无重叠)
				bool canPlace = true;
				foreach (var cellPos in tempInstance.GetOccupiedGridCells())
				{
					var existing = _targetShip.Grid.GetModuleAt(cellPos);
					if (existing != null)
					{
						canPlace = false;
						break;
					}
				}

				Color ghostColor = canPlace 
					? new Color(0.2f, 1.0f, 0.4f, 0.50f)  // 绿色：允许放置
					: new Color(1.0f, 0.2f, 0.2f, 0.50f); // 红色：空间重叠被阻挡

				foreach (var cellPos in tempInstance.GetOccupiedGridCells())
				{
					Vector2 screenPos = GridToScreen(cellPos);
					DrawRect(new Rect2(screenPos + Vector2.One * 2, new Vector2(GridPixelSize - 4, GridPixelSize - 4)), ghostColor, filled: true);
				}

				// 绘制旋转后的幽灵引脚预览
				foreach (var (pinDef, pinPos) in tempInstance.GetTransformedPins())
				{
					Vector2 pinCenter = GridToScreen(pinPos) + new Vector2(GridPixelSize * 0.5f, GridPixelSize * 0.5f);
					DrawCircle(pinCenter, 5.0f, canPlace ? Colors.Yellow : Colors.Red);
				}
			}
		}

		private void UpdateTelemetryAndValidation()
		{
			// 1. 执行即时安全校验流水线
			_currentReport = ShipValidator.Validate(_targetShip.Grid, _targetShip.Graph);

			// 2. 试算物理参数
			var physics = CenterOfMassSolver.Solve(_targetShip.Grid);
			var thrust = ThrusterSolver.Solve(_targetShip.Grid);

			// 3. 组装右侧状态面板
			string validationText = "【安全合规校验流水线】\n";
			foreach (var item in _currentReport.Items)
			{
				string statusIcon = item.IsPassed ? "[color=green][✔][/color]" : "[color=red][✘][/color]";
				validationText += $"{statusIcon} {item.Name}\n  └ {item.DetailMessage}\n";
			}

			_telemetryLabel.Text = $"【实时物理力学状态】\n" +
								  $"------------------------------------\n" +
								  $"构件总数:       {_targetShip.Grid.ModuleCount} 个\n" +
								  $"全舰总质量:     {physics.TotalMass:F1} 吨\n" +
								  $"物理质心 (CoM):  ({physics.CenterOfMassGrid.X:F2}, {physics.CenterOfMassGrid.Y:F2})\n" +
								  $"转动惯量 (MoI):  {physics.MomentOfInertia:F1} t·m²\n" +
								  $"前向最大推力:   {thrust.MaxForwardThrust:F0} N\n" +
								  $"推重比 (T/W):   {(thrust.MaxForwardThrust / (physics.TotalMass * 9.8f)):F2}\n" +
								  $"------------------------------------\n" +
								  $"{validationText}\n" +
								  $"------------------------------------\n" +
								  $"[操作提示]\n" +
								  $"[左键]: 放置构件 | [右键]: 拆除构件\n" +
								  $"[R 键]: 顺时针 90° 旋转待放构件";

			// 4. 若校验未通过，禁用实装确认按钮
			_confirmButton.Disabled = !_currentReport.IsAllPassed;
			_confirmButton.Text = _currentReport.IsAllPassed ? "确认实装并启航 (Tab)" : "安全校验未通过 (禁止启航)";
		}

		private void OnConfirmPressed()
		{
			if (!_currentReport.IsAllPassed)
			{
				GD.PrintErr("[RefitCanvas] 安全校验未通过，禁止实装！");
				return;
			}

			// 校验通过，重构刚体物理并关闭装配界面
			_targetShip.RebuildPhysics();
			Hide();
			GD.PrintRich("[color=green][RefitCanvas] 装配确认完成，战舰物理与受力拓扑已重构更新！[/color]");
		}

		private Vector2 GridToScreen(Vector2I gridPos)
		{
			return _canvasOrigin + new Vector2(gridPos.X * GridPixelSize, gridPos.Y * GridPixelSize);
		}

		private Vector2I ScreenToGrid(Vector2 screenPos)
		{
			Vector2 offset = screenPos - _canvasOrigin;
			return new Vector2I(
				Mathf.FloorToInt(offset.X / GridPixelSize),
				Mathf.FloorToInt(offset.Y / GridPixelSize)
			);
		}
	}
}
