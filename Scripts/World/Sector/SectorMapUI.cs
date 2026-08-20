using System;
using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.World.Sector
{
	/// <summary>
	/// 交互式全息星区作战地图 (支持视口自适应、双通道鼠标交互、侦察卡片与追击波前)
	/// </summary>
	public partial class SectorMapUI : Control
	{
		public SectorGraph Graph { get; private set; } = null!;
		public event Action<SectorNode>? OnNodeSelected;

		private float _animTime = 0.0f;
		private Rect2 _mapArea;
		private SectorNode? _hoveredNode = null;
		private Vector2 _currentMousePos = Vector2.Zero;

		public override void _Ready()
		{
			SetAnchorsPreset(LayoutPreset.FullRect);
			GrowHorizontal = GrowDirection.Both;
			GrowVertical = GrowDirection.Both;
			MouseFilter = MouseFilterEnum.Stop;

			// 强制初始化视口尺寸
			Vector2 vpSize = GetViewportRect().Size;
			CustomMinimumSize = vpSize;
			Size = vpSize;
		}

		public void SetGraph(SectorGraph graph)
		{
			Graph = graph;
			QueueRedraw();
		}

		public override void _Process(double delta)
		{
			_animTime += (float)delta * 3.5f;

			// 视口尺寸动态同步 (彻底根除 CanvasLayer 下尺寸为 0 的 Bug)
			Vector2 vpSize = GetViewportRect().Size;
			if (vpSize.X > 100 && vpSize.Y > 100 && Size != vpSize)
			{
				Size = vpSize;
				CustomMinimumSize = vpSize;
			}

			// 每帧主动解算鼠标位置与悬停节点
			_currentMousePos = GetLocalMousePosition();
			UpdateHoveredNode(_currentMousePos);

			QueueRedraw();
		}

		private void UpdateHoveredNode(Vector2 mousePos)
		{
			if (Graph == null) return;

			SectorNode? prevHover = _hoveredNode;
			_hoveredNode = null;

			foreach (var node in Graph.AllNodes.Values)
			{
				Vector2 nodePos = GetNodeScreenPosition(node);
				if (mousePos.DistanceTo(nodePos) <= 28.0f)
				{
					_hoveredNode = node;
					break;
				}
			}

			if (_hoveredNode != prevHover)
			{
				MouseDefaultCursorShape = (_hoveredNode != null && _hoveredNode.State == NodeExplorationState.Reachable)
					? CursorShape.PointingHand
					: CursorShape.Arrow;
			}
		}

		// -------------------------------------------------------------
		// 双通道鼠标输入监听 (确保 100% 捕获点击事件)
		// -------------------------------------------------------------
		public override void _GuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				HandleNodeClick(_currentMousePos);
				AcceptEvent();
			}
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				HandleNodeClick(_currentMousePos);
			}
		}

		private void HandleNodeClick(Vector2 clickPos)
		{
			if (Graph == null) return;

			foreach (var node in Graph.AllNodes.Values)
			{
				Vector2 nodeScreenPos = GetNodeScreenPosition(node);
				if (clickPos.DistanceTo(nodeScreenPos) <= 32.0f)
				{
					if (node.State == NodeExplorationState.Reachable)
					{
						ExecuteJumpToNode(node);
					}
					break;
				}
			}
		}

		/// <summary>
		/// 执行超空间跳跃到目标节点，并推进追击舰队前线
		/// </summary>
		public void ExecuteJumpToNode(SectorNode targetNode)
		{
			// 1. 将上一个驻泊节点设为 Visited (已探索)
			if (Graph.CurrentNodeId != null && Graph.AllNodes.TryGetValue(Graph.CurrentNodeId, out var prevNode))
			{
				prevNode.State = NodeExplorationState.Visited;
			}

			// 2. 将当前选中的目标节点设为 Current (舰队驻泊中)
			Graph.CurrentNodeId = targetNode.Id;
			targetNode.State = NodeExplorationState.Current;

			// 3. 推进追击前线 (+1.0 列)
			Graph.PursuitWavefrontColumn += 1.0f;

			// 4. 重置旧的可达节点，并将处于追击波前左侧的未访问节点设为 Overrun (沦陷)
			foreach (var node in Graph.AllNodes.Values)
			{
				if (node.State == NodeExplorationState.Reachable)
				{
					node.State = NodeExplorationState.Unreachable;
				}

				if (node.Column <= Graph.PursuitWavefrontColumn && node != targetNode && node.State != NodeExplorationState.Visited)
				{
					node.State = NodeExplorationState.Overrun;
				}
			}

			// 5. 激活当前节点所有正向连线的下一列节点为 Reachable (可跃迁)
			foreach (var nextId in targetNode.OutgoingConnections)
			{
				if (Graph.AllNodes.TryGetValue(nextId, out var nextNode))
				{
					if (nextNode.State != NodeExplorationState.Overrun)
					{
						nextNode.State = NodeExplorationState.Reachable;
					}
				}
			}

			OnNodeSelected?.Invoke(targetNode);
			QueueRedraw();
		}

		public override void _Draw()
		{
			if (Graph == null) return;

			Vector2 vpSize = GetViewportRect().Size;
			float w = vpSize.X > 100 ? vpSize.X : 1280.0f;
			float h = vpSize.Y > 100 ? vpSize.Y : 720.0f;

			_mapArea = new Rect2(80, 95, w - 160, h - 180);

			// 1. 绘制科幻深空网格背板
			DrawRect(_mapArea, new Color(0.02f, 0.04f, 0.07f, 0.95f));
			DrawGridLines(_mapArea);
			DrawRect(_mapArea, new Color(0.15f, 0.35f, 0.65f, 0.7f), false, 2.0f);

			// 2. 绘制追击前线危险区域
			float pursuitX = _mapArea.Position.X + ((Graph.PursuitWavefrontColumn + 0.5f) / Graph.TotalColumns) * _mapArea.Size.X;
			if (pursuitX > _mapArea.Position.X)
			{
				float overrunWidth = Mathf.Min(pursuitX - _mapArea.Position.X, _mapArea.Size.X);
				var overrunRect = new Rect2(_mapArea.Position.X, _mapArea.Position.Y, overrunWidth, _mapArea.Size.Y);
				DrawRect(overrunRect, new Color(0.85f, 0.12f, 0.12f, 0.22f));
				DrawLine(new Vector2(pursuitX, _mapArea.Position.Y), new Vector2(pursuitX, _mapArea.End.Y), Colors.OrangeRed, 3.0f);

				// 追击线标头
				DrawString(ThemeDB.FallbackFont, new Vector2(pursuitX - 60, _mapArea.Position.Y + 20), "[ ⚠️ 追击前线 ]", HorizontalAlignment.Center, -1, 11, Colors.OrangeRed);
			}

			// 3. 绘制航路连线
			foreach (var node in Graph.AllNodes.Values)
			{
				Vector2 startPos = GetNodeScreenPosition(node);
				foreach (var nextId in node.OutgoingConnections)
				{
					if (Graph.AllNodes.TryGetValue(nextId, out var nextNode))
					{
						Vector2 endPos = GetNodeScreenPosition(nextNode);
						DrawRouteLine(startPos, endPos, node.State, nextNode.State);
					}
				}
			}

			// 4. 绘制所有战术徽章节点
			foreach (var node in Graph.AllNodes.Values)
			{
				Vector2 pos = GetNodeScreenPosition(node);
				DrawTacticalBadgeNode(node, pos);
			}

			// 5. 最顶层绘制鼠标悬停战术侦察卡片 (Tooltip)
			if (_hoveredNode != null)
			{
				DrawTacticalTooltip(_hoveredNode, _currentMousePos);
			}
		}

		private void DrawGridLines(Rect2 area)
		{
			Color gridColor = new(0.1f, 0.2f, 0.35f, 0.15f);
			for (float x = area.Position.X; x <= area.End.X; x += 60.0f)
			{
				DrawLine(new Vector2(x, area.Position.Y), new Vector2(x, area.End.Y), gridColor, 1.0f);
			}
			for (float y = area.Position.Y; y <= area.End.Y; y += 60.0f)
			{
				DrawLine(new Vector2(area.Position.X, y), new Vector2(area.End.X, y), gridColor, 1.0f);
			}
		}

		private void DrawRouteLine(Vector2 from, Vector2 to, NodeExplorationState fromState, NodeExplorationState toState)
		{
			Color lineColor = new(0.2f, 0.35f, 0.5f, 0.40f);
			float width = 1.5f;

			if ((fromState == NodeExplorationState.Visited || fromState == NodeExplorationState.Current) && toState == NodeExplorationState.Reachable)
			{
				float pulse = (Mathf.Sin(_animTime * 2.0f) + 1.0f) * 0.5f;
				lineColor = new Color(0.25f, 0.95f, 0.60f, 0.7f + pulse * 0.3f);
				width = 2.5f;
			}
			else if (fromState == NodeExplorationState.Overrun || toState == NodeExplorationState.Overrun)
			{
				lineColor = new Color(0.75f, 0.2f, 0.2f, 0.30f);
			}

			DrawLine(from, to, lineColor, width);
		}

		private void DrawTacticalBadgeNode(SectorNode node, Vector2 pos)
		{
			Color typeColor = node.GetTypeColor();
			float radius = 17.0f;
			bool isHovered = node == _hoveredNode;

			switch (node.State)
			{
				case NodeExplorationState.Current:
					float currentPulse = Mathf.Sin(_animTime * 2.0f) * 2.5f;
					DrawHexagon(pos, radius + 8.0f + currentPulse, new Color(1.0f, 0.85f, 0.2f, 0.35f), true);
					DrawHexagon(pos, radius, Colors.Gold, true);
					DrawHexagon(pos, radius + 3.0f, Colors.Gold, false, 2.5f);
					DrawString(ThemeDB.FallbackFont, pos + new Vector2(-35, -radius - 8.0f), "[ 🚩 驻泊中 ]", HorizontalAlignment.Center, -1, 11, Colors.Gold);
					break;

				case NodeExplorationState.Reachable:
					float pulse = Mathf.Sin(_animTime) * 3.5f;
					float hoverAdd = isHovered ? 4.0f : 0.0f;
					DrawHexagon(pos, radius + 6.0f + pulse + hoverAdd, new Color(0.2f, 0.95f, 0.55f, 0.40f), true);
					DrawHexagon(pos, radius, typeColor, true);
					DrawHexagon(pos, radius + 2.0f, Colors.LimeGreen, false, 2.0f);
					break;

				case NodeExplorationState.Visited:
					DrawHexagon(pos, radius * 0.75f, new Color(0.3f, 0.35f, 0.4f), true);
					DrawHexagon(pos, radius * 0.75f, Colors.White, false, 1.2f);
					break;

				case NodeExplorationState.Overrun:
					DrawHexagon(pos, radius * 0.85f, new Color(0.4f, 0.1f, 0.1f), true);
					DrawHexagon(pos, radius * 0.85f, Colors.Red, false, 1.5f);
					break;

				default:
					DrawHexagon(pos, radius * 0.85f, typeColor.Darkened(0.6f), true);
					DrawHexagon(pos, radius * 0.85f, typeColor.Darkened(0.3f), false, 1.2f);
					break;
			}

			// 绘制中心矢量图形
			DrawNodeVectorIcon(node.Type, pos, (node.State == NodeExplorationState.Visited) ? Colors.Gray : Colors.Black);

			// 绘制底部全称
			string name = node.GetDisplayName();
			var font = ThemeDB.FallbackFont;
			Vector2 strSize = font.GetStringSize(name, HorizontalAlignment.Center, -1, 10);
			Color nameColor = (node.State == NodeExplorationState.Reachable || node.State == NodeExplorationState.Current) ? Colors.White : new Color(0.65f, 0.65f, 0.65f, 0.55f);
			DrawString(font, pos + new Vector2(-strSize.X * 0.5f, radius + 15.0f), name, HorizontalAlignment.Center, -1, 10, nameColor);
		}

		private void DrawHexagon(Vector2 center, float radius, Color color, bool filled, float width = 1.0f)
		{
			Vector2[] points = new Vector2[6];
			for (int i = 0; i < 6; i++)
			{
				float angle = (i * 60.0f) * Mathf.Pi / 180.0f;
				points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
			}

			if (filled)
			{
				DrawColoredPolygon(points, color);
			}
			else
			{
				for (int i = 0; i < 6; i++)
				{
					DrawLine(points[i], points[(i + 1) % 6], color, width);
				}
			}
		}

		private void DrawNodeVectorIcon(SectorNodeType type, Vector2 pos, Color color)
		{
			switch (type)
			{
				case SectorNodeType.Combat:
					DrawLine(pos + new Vector2(-6, -6), pos + new Vector2(6, 6), color, 2.0f);
					DrawLine(pos + new Vector2(6, -6), pos + new Vector2(-6, 6), color, 2.0f);
					break;
				case SectorNodeType.Elite:
					DrawCircle(pos + new Vector2(0, -2), 4.5f, color);
					DrawRect(new Rect2(pos.X - 3, pos.Y + 2, 6, 4), color);
					break;
				case SectorNodeType.Market:
					DrawRect(new Rect2(pos.X - 5, pos.Y - 5, 10, 10), color, false, 1.8f);
					DrawLine(pos + new Vector2(-5, 0), pos + new Vector2(5, 0), color, 1.2f);
					break;
				case SectorNodeType.Repair:
					DrawLine(pos + new Vector2(-6, 0), pos + new Vector2(6, 0), color, 2.5f);
					DrawLine(pos + new Vector2(0, -6), pos + new Vector2(0, 6), color, 2.5f);
					break;
				case SectorNodeType.Event:
					DrawLine(pos + new Vector2(0, -6), pos + new Vector2(5, 0), color, 1.8f);
					DrawLine(pos + new Vector2(5, 0), pos + new Vector2(0, 6), color, 1.8f);
					DrawLine(pos + new Vector2(0, 6), pos + new Vector2(-5, 0), color, 1.8f);
					DrawLine(pos + new Vector2(-5, 0), pos + new Vector2(0, -6), color, 1.8f);
					break;
				case SectorNodeType.Boss:
					DrawLine(pos + new Vector2(-7, 5), pos + new Vector2(0, -7), color, 2.2f);
					DrawLine(pos + new Vector2(0, -7), pos + new Vector2(7, 5), color, 2.2f);
					DrawLine(pos + new Vector2(7, 5), pos + new Vector2(-7, 5), color, 2.2f);
					break;
			}
		}

		private void DrawTacticalTooltip(SectorNode node, Vector2 mousePos)
		{
			Vector2 cardSize = new(280, 110);
			Vector2 cardPos = mousePos + new Vector2(18, 18);

			Vector2 vpSize = GetViewportRect().Size;
			if (cardPos.X + cardSize.X > vpSize.X - 20) cardPos.X = mousePos.X - cardSize.X - 10;
			if (cardPos.Y + cardSize.Y > vpSize.Y - 20) cardPos.Y = mousePos.Y - cardSize.Y - 10;

			DrawRect(new Rect2(cardPos, cardSize), new Color(0.03f, 0.07f, 0.12f, 0.98f));
			DrawRect(new Rect2(cardPos, cardSize), node.GetTypeColor(), false, 2.0f);

			var font = ThemeDB.FallbackFont;
			DrawString(font, cardPos + new Vector2(12, 22), $"【{node.GetDisplayName()}】", HorizontalAlignment.Left, -1, 13, node.GetTypeColor());
			DrawString(font, cardPos + new Vector2(12, 45), $"• 驻留: {node.FactionAffiliation}", HorizontalAlignment.Left, -1, 11, Colors.LightGray);
			DrawString(font, cardPos + new Vector2(12, 66), $"• 收益: {node.PotentialLoot}", HorizontalAlignment.Left, -1, 11, Colors.Gold);
			DrawString(font, cardPos + new Vector2(12, 87), $"• 危险: {node.DangerLevel}", HorizontalAlignment.Left, -1, 11, Colors.OrangeRed);
		}

		private Vector2 GetNodeScreenPosition(SectorNode node)
		{
			return new Vector2(
				_mapArea.Position.X + node.NormalizedPosition.X * _mapArea.Size.X,
				_mapArea.Position.Y + node.NormalizedPosition.Y * _mapArea.Size.Y
			);
		}
	}
}
