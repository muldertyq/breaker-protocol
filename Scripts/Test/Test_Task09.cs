using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models; // <--- 关键修复：补齐 PinType 所在的命名空间
using BreakerProtocol.Ship;
using BreakerProtocol.Ship.Pipeline;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-09 交互式验证场景：PCB 引脚对齐、曼哈顿 A* 自动寻路与流向箭头渲染
	/// </summary>
	public partial class Test_Task09 : Node2D
	{
		private ShipGrid _grid = null!;
		private PipelineNetwork _pipeline = null!;

		private readonly Vector2 _canvasOrigin = new(500, 360);
		private const float GridPixelSize = 36.0f; // 缩放为 36px 方便观察与点击

		// 交互连线状态
		private PinInstance? _draggedSourcePin;
		private List<Vector2I> _currentPreviewPath = new();
		private bool _isPreviewValid = false;

		private Label _infoLabel = null!;

		public override void _Ready()
		{
			_grid = new ShipGrid();
			_pipeline = new PipelineNetwork();

			CreateUI();
			BuildTestShip();
		}

		private void CreateUI()
		{
			_infoLabel = new Label
			{
				Position = new Vector2(25, 25),
				Size = new Vector2(400, 600)
			};
			_infoLabel.AddThemeFontSizeOverride("font_size", 15);
			_infoLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			AddChild(_infoLabel);
		}

		private void BuildTestShip()
		{
			_grid.Clear();
			_pipeline.Clear();

			// 1. 动力反应堆 (2x2, 位于底部 -1, 1) -> 带有 2 个 OUT 引脚 (蓝点)
			var coreDef = DataManager.Instance.Modules.Get("hf_source_core_2x2");
			_grid.TryPlaceModule(coreDef, new Vector2I(-1, 1), rotation: 0, out _);

			// 2. 极寒冷凝修饰舱 (2x2, 位于中部 -1, -1) -> 带有 1 个 IN 引脚 (绿点) 和 1 个 OUT 引脚 (蓝点)
			var cryoDef = DataManager.Instance.Modules.Get("hf_mod_cryo_chamber");
			_grid.TryPlaceModule(cryoDef, new Vector2I(-1, -1), rotation: 0, out _);

			// 3. 重型磁轨主炮 (3x1, 位于顶部 -1, -3) -> 带有 1 个 IN 引脚 (绿点)
			var gunDef = DataManager.Instance.Modules.Get("hf_wep_railgun_h");
			_grid.TryPlaceModule(gunDef, new Vector2I(-1, -3), rotation: 0, out _);

			// 4. 左侧放置一块实心重装甲板 (-3, -1) -> 演示曼哈顿 A* 自动绕开装甲障碍
			var armorDef = DataManager.Instance.Modules.Get("hf_arm_plate_2x2");
			_grid.TryPlaceModule(armorDef, new Vector2I(-3, -1), rotation: 0, out _);

			UpdateInfoHUD();
		}

		private void UpdateInfoHUD()
		{
			_infoLabel.Text = $"【《断路协议》TASK-09 PCB 走线与曼哈顿寻路】\n" +
							  $"================================================\n" +
							  $"已建立 PCB 导线:  {_pipeline.WireCount} 条\n" +
							  $"当前拖拽起点:     {(_draggedSourcePin != null ? $"{_draggedSourcePin.Definition.PinId} ({_draggedSourcePin.AbsoluteGridPos})" : "无 (等待点击蓝点)")}\n" +
							  $"预览路径步数:     {_currentPreviewPath.Count} 格 (合规: {_isPreviewValid})\n" +
							  $"------------------------------------------------\n" +
							  $"[PCB 走线操作指南]\n" +
							  $"1. 鼠标左键点击 [● 蓝点 OUT] 作为起点；\n" +
							  $"2. 移动鼠标，观察曼哈顿 A* 自动以最少直角拐弯绕行；\n" +
							  $"3. 点击目标 [● 绿点 IN] 完成 PCB 导线铺设；\n" +
							  $"4. 鼠标右键点击任意导线可将其拆除。\n" +
							  $"------------------------------------------------\n" +
							  $"[图例说明]\n" +
							  $"● 蓝点: 能量输出 (OUT) | ● 绿点: 能量输入 (IN)\n" +
							  $"═ 金黄双线: 已建成的 PCB 能量铜排\n" +
							  $"► 绿色箭头: 曼哈顿正交脉冲流动方向\n" +
							  $"■ 灰色块: 实心阻挡装甲 (A* 自动绕行)";
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed)
			{
				Vector2I mouseGrid = ScreenToGrid(GetLocalMousePosition());
				var clickedPin = FindPinAtGrid(mouseGrid);

				// 鼠标左键
				if (mouseBtn.ButtonIndex == MouseButton.Left)
				{
					if (_draggedSourcePin == null)
					{
						// 阶段 1：选择 OUT 引脚作为起点
						if (clickedPin != null && clickedPin.Type == PinType.OUT)
						{
							_draggedSourcePin = clickedPin;
							GD.Print($"[Wiring] 选中起点引脚: {clickedPin.Definition.PinId} 位于 {clickedPin.AbsoluteGridPos}");
						}
					}
					else
					{
						// 阶段 2：点击目标 IN 引脚完成连线
						if (clickedPin != null && clickedPin.Type == PinType.IN)
						{
							if (_pipeline.TryAddWire(_draggedSourcePin, clickedPin, _grid, out _))
							{
								_draggedSourcePin = null;
								_currentPreviewPath.Clear();
								UpdateInfoHUD();
							}
						}
					}
				}
				// 鼠标右键：取消拖拽或删除光标处的导线
				else if (mouseBtn.ButtonIndex == MouseButton.Right)
				{
					if (_draggedSourcePin != null)
					{
						_draggedSourcePin = null;
						_currentPreviewPath.Clear();
					}
					else
					{
						var wiresAtPos = new List<PipelineWire>(_pipeline.GetWiresAt(mouseGrid));
						foreach (var w in wiresAtPos)
						{
							_pipeline.RemoveWire(w.WireId);
						}
					}
					UpdateInfoHUD();
				}

				QueueRedraw();
			}
		}

		public override void _Process(double delta)
		{
			// 鼠标移动时实时解算 A* 预览路径
			if (_draggedSourcePin != null)
			{
				Vector2I mouseGrid = ScreenToGrid(GetLocalMousePosition());
				_currentPreviewPath = ManhattanRouter.FindPath(_draggedSourcePin.AbsoluteGridPos, mouseGrid, _grid);
				
				var targetPin = FindPinAtGrid(mouseGrid);
				_isPreviewValid = targetPin != null && targetPin.Type == PinType.IN && 
								  PinCompatibilityValidator.CanConnect(_draggedSourcePin, targetPin, out _);

				UpdateInfoHUD();
				QueueRedraw();
			}
		}

		public override void _Draw()
		{
			// 1. 绘制底层坐标网格 (-6 到 +6)
			for (int x = -6; x <= 6; x++)
			{
				for (int y = -6; y <= 6; y++)
				{
					Vector2 pos = GridToScreen(new Vector2I(x, y));
					DrawRect(new Rect2(pos, new Vector2(GridPixelSize, GridPixelSize)), new Color(0.12f, 0.15f, 0.20f), filled: false, width: 1.0f);
				}
			}

			// 2. 绘制构件色块
			foreach (var module in _grid.Modules)
			{
				Color moduleColor = module.Definition.Category switch
				{
					"PowerSource" => new Color(0.15f, 0.45f, 0.95f, 0.70f),
					"Modifier"    => new Color(0.15f, 0.85f, 0.75f, 0.70f),
					"Weapon"      => new Color(0.95f, 0.25f, 0.25f, 0.70f),
					"Armor"       => new Color(0.45f, 0.45f, 0.50f, 0.90f),
					_             => new Color(0.70f, 0.70f, 0.70f, 0.70f)
				};

				foreach (var cellPos in module.GetOccupiedGridCells())
				{
					Vector2 screenPos = GridToScreen(cellPos);
					DrawRect(new Rect2(screenPos + Vector2.One * 2, new Vector2(GridPixelSize - 4, GridPixelSize - 4)), moduleColor, filled: true);
				}
			}

			// 3. 绘制所有已建成的 PCB 导线 (金黄铜排 + 脉冲方向箭头)
			foreach (var wire in _pipeline.Wires)
			{
				DrawPipelineWire(wire.GridPath, new Color(1.0f, 0.75f, 0.15f, 0.95f), isPreview: false);
			}

			// 4. 绘制当前拖拽中的实时 A* 预览折线
			if (_draggedSourcePin != null && _currentPreviewPath.Count > 1)
			{
				Color previewColor = _isPreviewValid ? new Color(0.2f, 1.0f, 0.4f, 0.85f) : new Color(1.0f, 0.3f, 0.3f, 0.85f);
				DrawPipelineWire(_currentPreviewPath, previewColor, isPreview: true);
			}

			// 5. 绘制所有构件引脚端口 (绿点 IN / 蓝点 OUT)
			foreach (var pin in GetAllPins())
			{
				Vector2 pinCenter = GridToScreenCenter(pin.AbsoluteGridPos);
				Color pinColor = pin.Type == PinType.IN ? new Color(0.2f, 1.0f, 0.3f) : new Color(0.2f, 0.6f, 1.0f);

				DrawCircle(pinCenter, 7.0f, pinColor);
				DrawCircle(pinCenter, 3.5f, Colors.White);

				// 若是被选中的起点，绘制闪烁光环
				if (_draggedSourcePin != null && _draggedSourcePin.AbsoluteGridPos == pin.AbsoluteGridPos)
				{
					DrawArc(pinCenter, 12.0f, 0, Mathf.Tau, 16, Colors.Yellow, 2.0f);
				}
			}
		}

		private void DrawPipelineWire(List<Vector2I> path, Color wireColor, bool isPreview)
		{
			if (path.Count < 2) return;

			// 绘制主干线段
			for (int i = 0; i < path.Count - 1; i++)
			{
				Vector2 p1 = GridToScreenCenter(path[i]);
				Vector2 p2 = GridToScreenCenter(path[i + 1]);

				DrawLine(p1, p2, wireColor, isPreview ? 2.5f : 4.0f);

				// 在每两个相邻网格中点绘制单向流动三角箭头 ►
				Vector2 mid = (p1 + p2) * 0.5f;
				Vector2 dir = (p2 - p1).Normalized();
				DrawFlowArrow(mid, dir, wireColor);
			}
		}

		private void DrawFlowArrow(Vector2 center, Vector2 dir, Color color)
		{
			Vector2 normal = new(-dir.Y, dir.X);
			float arrowSize = 6.0f;

			Vector2 tip = center + dir * arrowSize;
			Vector2 left = center - dir * arrowSize + normal * (arrowSize * 0.7f);
			Vector2 right = center - dir * arrowSize - normal * (arrowSize * 0.7f);

			Vector2[] points = { left, right, tip };
			DrawColoredPolygon(points, color);
		}

		private IEnumerable<PinInstance> GetAllPins()
		{
			foreach (var module in _grid.Modules)
			{
				foreach (var (pinDef, pinGridPos) in module.GetTransformedPins())
				{
					yield return new PinInstance(module.InstanceId, pinDef, pinGridPos);
				}
			}
		}

		private PinInstance? FindPinAtGrid(Vector2I gridPos)
		{
			foreach (var pin in GetAllPins())
			{
				if (pin.AbsoluteGridPos == gridPos) return pin;
			}
			return null;
		}

		private Vector2 GridToScreen(Vector2I gridPos)
		{
			return _canvasOrigin + new Vector2(gridPos.X * GridPixelSize, gridPos.Y * GridPixelSize);
		}

		private Vector2 GridToScreenCenter(Vector2I gridPos)
		{
			return GridToScreen(gridPos) + new Vector2(GridPixelSize * 0.5f, GridPixelSize * 0.5f);
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
