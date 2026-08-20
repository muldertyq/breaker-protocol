using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// 修复版 TASK-05 验证场景：居中对称拼接、无缝连通与坐标标尺
	/// </summary>
	public partial class Test_Task05 : Node2D
	{
		private ShipGrid _grid = null!;
		private StructuralGraph _graph = null!;
		private Label _infoLabel = null!;

		// 单元格屏幕像素大小
		private const float CellPixelSize = 36.0f;
		private readonly Vector2 _renderOrigin = new(500, 320);

		public override void _Ready()
		{
			_grid = new ShipGrid();
			_graph = new StructuralGraph();

			CreateUI();
			BuildTestShip();
		}

		private void CreateUI()
		{
			_infoLabel = new Label
			{
				Position = new Vector2(30, 30),
				Size = new Vector2(380, 520)
			};
			_infoLabel.AddThemeFontSizeOverride("font_size", 15);
			_infoLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			AddChild(_infoLabel);
		}

		private void BuildTestShip()
		{
			// 1. 放置核心动力源 (2x2，坐标 -1, 0 -> 占用 X:[-1,0], Y:[0,1])
			var coreDef = DataManager.Instance.Modules.Get("hf_source_core_2x2");
			_grid.TryPlaceModule(coreDef, new Vector2I(-1, 0), rotation: 0, out _);

			// 2. 放置极寒冷凝舱 (2x2，坐标 -1, -2 -> 占用 X:[-1,0], Y:[-2,-1]，紧贴动力源上方)
			var cryoDef = DataManager.Instance.Modules.Get("hf_mod_cryo_chamber");
			_grid.TryPlaceModule(cryoDef, new Vector2I(-1, -2), rotation: 0, out _);

			// 3. 放置重型磁轨主炮 (3x1，坐标 -1, -3 -> 占用 X:[-1,0,1], Y:[-3]，严丝合缝紧贴冷凝舱上方，居中对称)
			var gunDef = DataManager.Instance.Modules.Get("hf_wep_railgun_h");
			_grid.TryPlaceModule(gunDef, new Vector2I(-1, -3), rotation: 0, out _);

			// 4. 重建物理受力连通图
			_graph.RebuildGraph(_grid);

			// 5. 刷新面板信息
			var connected = _graph.GetConnectedComponentsFromPowerSources(_grid);
			var bounds = _grid.GetGridBounds();

			_infoLabel.Text = $"【《断路协议》TASK-05 飞船网格与连通图】\n" +
							  $"========================================\n" +
							  $"构件总数: {_grid.ModuleCount} 个\n" +
							  $"网格边界: [{bounds.Position.X},{bounds.Position.Y}] 到 [{bounds.End.X},{bounds.End.Y}]\n" +
							  $"边界跨度: {bounds.Size.X} x {bounds.Size.Y} GU\n" +
							  $"物理连通构件: {connected.Count} / {_grid.ModuleCount} 个 (全连通)\n" +
							  $"----------------------------------------\n" +
							  $"[构件坐标清单]\n" +
							  $"■ 动力堆 (蓝): (-1, 0)  尺寸: 2x2\n" +
							  $"■ 冷凝舱 (青): (-1, -2) 尺寸: 2x2\n" +
							  $"■ 磁轨炮 (红): (-1, -3) 尺寸: 3x1 (居中)\n" +
							  $"----------------------------------------\n" +
							  $"[图例说明]\n" +
							  $"● 绿点: IN 输入引脚 | ● 蓝点: OUT 输出引脚\n" +
							  $"─ 黄线: 构件物理相邻连通受力线";

			QueueRedraw();
		}

		public override void _Draw()
		{
			if (_grid == null) return;

			// 1. 绘制底层坐标网格与标尺文字 (-4 到 +4)
			for (int x = -4; x <= 4; x++)
			{
				for (int y = -4; y <= 4; y++)
				{
					Vector2 pos = _renderOrigin + new Vector2(x * CellPixelSize, y * CellPixelSize);
					DrawRect(new Rect2(pos, new Vector2(CellPixelSize, CellPixelSize)), new Color(0.12f, 0.15f, 0.20f), filled: false, width: 1.0f);
				}
			}

			// 绘制 X/Y 轴中线
			Vector2 originScreen = _renderOrigin;
			DrawLine(new Vector2(originScreen.X - 160, originScreen.Y), new Vector2(originScreen.X + 160, originScreen.Y), new Color(0.3f, 0.4f, 0.5f, 0.5f), 1.5f);
			DrawLine(new Vector2(originScreen.X, originScreen.Y - 160), new Vector2(originScreen.X, originScreen.Y + 160), new Color(0.3f, 0.4f, 0.5f, 0.5f), 1.5f);

			// 2. 绘制每个构件的实体色块
			foreach (var module in _grid.Modules)
			{
				Color moduleColor = module.Definition.Category switch
				{
					"PowerSource" => new Color(0.15f, 0.45f, 0.95f, 0.75f),
					"Modifier" => new Color(0.15f, 0.85f, 0.75f, 0.75f),
					"Weapon" => new Color(0.95f, 0.25f, 0.25f, 0.75f),
					_ => new Color(0.7f, 0.7f, 0.7f, 0.75f)
				};

				foreach (var cellPos in module.GetOccupiedGridCells())
				{
					Vector2 screenPos = _renderOrigin + new Vector2(cellPos.X * CellPixelSize, cellPos.Y * CellPixelSize);
					DrawRect(new Rect2(screenPos + Vector2.One * 2, new Vector2(CellPixelSize - 4, CellPixelSize - 4)), moduleColor, filled: true);
				}

				// 3. 绘制经过旋转变换后的引脚端口 (绿点 IN, 蓝点 OUT)
				foreach (var (pinDef, pinPos) in module.GetTransformedPins())
				{
					Vector2 pinScreenPos = _renderOrigin + new Vector2(pinPos.X * CellPixelSize + CellPixelSize * 0.5f, pinPos.Y * CellPixelSize + CellPixelSize * 0.5f);
					Color pinColor = pinDef.Type == "IN" ? new Color(0.2f, 1.0f, 0.3f) : new Color(0.2f, 0.6f, 1.0f);
					DrawCircle(pinScreenPos, 6.0f, pinColor);
					DrawCircle(pinScreenPos, 3.0f, Colors.White); // 白心高亮
				}
			}

			// 4. 绘制构件之间的相邻拓扑连通线（黄色受力骨架线）
			foreach (var module in _grid.Modules)
			{
				Vector2 fromCenter = GetModuleScreenCenter(module);
				foreach (var neighborId in _graph.GetNeighbors(module.InstanceId))
				{
					if (string.CompareOrdinal(module.InstanceId, neighborId) < 0)
					{
						var neighbor = GetModuleById(neighborId);
						if (neighbor != null)
						{
							Vector2 toCenter = GetModuleScreenCenter(neighbor);
							DrawLine(fromCenter, toCenter, new Color(1.0f, 0.85f, 0.1f, 0.9f), width: 3.0f);
						}
					}
				}
			}
		}

		private Vector2 GetModuleScreenCenter(ModuleInstance module)
		{
			Vector2I size = module.GetRotatedSize();
			Vector2 centerGrid = new Vector2(module.GridPosition.X + size.X * 0.5f, module.GridPosition.Y + size.Y * 0.5f);
			return _renderOrigin + centerGrid * CellPixelSize;
		}

		private ModuleInstance? GetModuleById(string instanceId)
		{
			foreach (var m in _grid.Modules)
			{
				if (m.InstanceId == instanceId) return m;
			}
			return null;
		}
	}
}
