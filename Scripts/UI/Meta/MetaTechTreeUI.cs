using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.World.Meta;

namespace BreakerProtocol.UI.Meta
{
	/// <summary>
	/// 全息母港科研局 Meta 科技树操作界面 (支持全景 3 列分支、前置连线、即时反馈与返回导航)
	/// </summary>
	public partial class MetaTechTreeUI : Control
	{
		public event Action? OnBackRequested;

		private Rect2 _panelArea;
		private float _animTime = 0.0f;
		private Vector2 _currentMousePos = Vector2.Zero;
		private MetaTechNode? _hoveredNode = null;
		private string _feedbackLog = "请选择科技节点进行研发升级，增益将永久作用于后续所有战局！";
		private Color _feedbackColor = Colors.Gold;

		public override void _Ready()
		{
			SetAnchorsPreset(LayoutPreset.FullRect);
			GrowHorizontal = GrowDirection.Both;
			GrowVertical = GrowDirection.Both;
			MouseFilter = MouseFilterEnum.Stop;

			Vector2 vpSize = GetViewportRect().Size;
			CustomMinimumSize = vpSize;
			Size = vpSize;
		}

		public override void _Process(double delta)
		{
			if (!Visible) return;

			_animTime += (float)delta * 3.0f;

			Vector2 vpSize = GetViewportRect().Size;
			if (vpSize.X > 100 && vpSize.Y > 100 && Size != vpSize)
			{
				Size = vpSize;
				CustomMinimumSize = vpSize;
			}

			_currentMousePos = GetLocalMousePosition();
			UpdateHoverState(_currentMousePos);

			QueueRedraw();
		}

		private void UpdateHoverState(Vector2 mousePos)
		{
			_hoveredNode = null;

			foreach (var tech in MetaProgressionManager.Instance.AllTechs.Values)
			{
				if (GetNodeScreenRect(tech).HasPoint(mousePos))
				{
					_hoveredNode = tech;
					break;
				}
			}

			bool isHoverBtn = GetBackButtonRect().HasPoint(mousePos) ||
							  GetResetButtonRect().HasPoint(mousePos) ||
							  GetAddPointsButtonRect().HasPoint(mousePos);

			MouseDefaultCursorShape = (_hoveredNode != null || isHoverBtn) ? CursorShape.PointingHand : CursorShape.Arrow;
		}

		public override void _GuiInput(InputEvent @event)
		{
			if (!Visible) return;
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				HandleClick(_currentMousePos);
				AcceptEvent();
			}
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (!Visible) return;
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				HandleClick(_currentMousePos);
			}
			else if (@event is InputEventKey ek && ek.Pressed && !ek.Echo)
			{
				if (ek.Keycode == Key.Escape || ek.Keycode == Key.Backspace)
				{
					OnBackRequested?.Invoke();
				}
			}
		}

		private void HandleClick(Vector2 clickPos)
		{
			// 1. 返回主菜单按钮
			if (GetBackButtonRect().HasPoint(clickPos))
			{
				OnBackRequested?.Invoke();
				return;
			}

			// 2. 点击重置洗点
			if (GetResetButtonRect().HasPoint(clickPos))
			{
				MetaProgressionManager.Instance.ResetAllTechs();
				_feedbackLog = "🔄 所有科技已重置，研发数据碎片已 100% 全额返还！";
				_feedbackColor = Colors.Yellow;
				QueueRedraw();
				return;
			}

			// 3. 点击注资调试
			if (GetAddPointsButtonRect().HasPoint(clickPos))
			{
				MetaProgressionManager.Instance.AddDataFragments(100);
				_feedbackLog = "💾 母港科研赞助到账！获得 +100 研发数据碎片。";
				_feedbackColor = Colors.LimeGreen;
				QueueRedraw();
				return;
			}

			// 4. 点击科技节点进行解锁
			foreach (var tech in MetaProgressionManager.Instance.AllTechs.Values)
			{
				if (GetNodeScreenRect(tech).HasPoint(clickPos))
				{
					TryUnlockTech(tech);
					return;
				}
			}
		}

		private void TryUnlockTech(MetaTechNode tech)
		{
			if (tech.IsUnlocked)
			{
				_feedbackLog = $"✔ 科技【{tech.Name}】已处于激活状态，永久生效中！";
				_feedbackColor = Colors.Cyan;
				return;
			}

			if (!string.IsNullOrEmpty(tech.PrerequisiteId))
			{
				var pre = MetaProgressionManager.Instance.AllTechs[tech.PrerequisiteId];
				if (!pre.IsUnlocked)
				{
					_feedbackLog = $"❌ 无法解锁！需要先研发前置科技【{pre.Name}】。";
					_feedbackColor = Colors.OrangeRed;
					return;
				}
			}

			if (MetaProgressionManager.Instance.DataFragments < tech.Cost)
			{
				_feedbackLog = $"❌ 研发碎片不足！解锁【{tech.Name}】需要 {tech.Cost} 💾，当前仅有 {MetaProgressionManager.Instance.DataFragments} 💾。";
				_feedbackColor = Colors.OrangeRed;
				return;
			}

			if (MetaProgressionManager.Instance.UnlockTech(tech.Id))
			{
				_feedbackLog = $"🎉 成功研发核心科技【{tech.Name}】！(-{tech.Cost} 💾 研发碎片)";
				_feedbackColor = Colors.LimeGreen;
			}
		}

		private Rect2 GetPanelArea()
		{
			Vector2 vpSize = GetViewportRect().Size;
			float w = vpSize.X > 100 ? vpSize.X : 1280.0f;
			float h = vpSize.Y > 100 ? vpSize.Y : 720.0f;
			return new Rect2(80, 60, w - 160, h - 120);
		}

		private Rect2 GetNodeScreenRect(MetaTechNode node)
		{
			var panel = GetPanelArea();
			Vector2 pos = panel.Position + node.DisplayPosition;
			return new Rect2(pos.X - 100, pos.Y - 32, 200, 64);
		}

		private Rect2 GetBackButtonRect()
		{
			var panel = GetPanelArea();
			return new Rect2(panel.Position.X + 25, panel.End.Y - 60, 180, 38);
		}

		private Rect2 GetResetButtonRect()
		{
			var panel = GetPanelArea();
			return new Rect2(panel.Position.X + 220, panel.End.Y - 60, 180, 38);
		}

		private Rect2 GetAddPointsButtonRect()
		{
			var panel = GetPanelArea();
			return new Rect2(panel.Position.X + 415, panel.End.Y - 60, 180, 38);
		}

		public override void _Draw()
		{
			if (!Visible) return;

			_panelArea = GetPanelArea();
			var font = ThemeDB.FallbackFont;

			// 1. 绘制科幻科研局背板
			DrawRect(_panelArea, new Color(0.02f, 0.04f, 0.08f, 0.96f));
			DrawRect(_panelArea, new Color(0.2f, 0.5f, 0.85f, 0.75f), false, 2.5f);

			// 2. 标头与研发数据资产
			DrawString(font, _panelArea.Position + new Vector2(30, 38), "【 母港工程科研总局 · 全局永久科技树 】 FLEET R&D NEXUS", HorizontalAlignment.Left, -1, 16, Colors.Gold);
			string dataTag = $"• 研发数据碎片: {MetaProgressionManager.Instance.DataFragments} 💾";
			DrawString(font, _panelArea.Position + new Vector2(_panelArea.Size.X - 260, 38), dataTag, HorizontalAlignment.Right, -1, 15, Colors.Cyan);
			DrawLine(_panelArea.Position + new Vector2(25, 52), _panelArea.Position + new Vector2(_panelArea.Size.X - 25, 52), new Color(0.3f, 0.5f, 0.7f, 0.4f), 1.5f);

			// 3. 绘制 3 大派系分支列标头 (全景 3 列并排)
			DrawString(font, _panelArea.Position + new Vector2(140, 90), "🛡️ [ 重工冶金派系 ]", HorizontalAlignment.Center, -1, 14, new Color(0.95f, 0.5f, 0.3f));
			DrawString(font, _panelArea.Position + new Vector2(500, 90), "⚡ [ 超频电容派系 ]", HorizontalAlignment.Center, -1, 14, new Color(0.35f, 0.85f, 0.95f));
			DrawString(font, _panelArea.Position + new Vector2(860, 90), "🚀 [ 矢量推进派系 ]", HorizontalAlignment.Center, -1, 14, new Color(0.45f, 0.95f, 0.45f));

			// 4. 绘制前置依赖连线
			foreach (var tech in MetaProgressionManager.Instance.AllTechs.Values)
			{
				if (!string.IsNullOrEmpty(tech.PrerequisiteId))
				{
					var pre = MetaProgressionManager.Instance.AllTechs[tech.PrerequisiteId];
					Vector2 fromPos = _panelArea.Position + pre.DisplayPosition;
					Vector2 toPos = _panelArea.Position + tech.DisplayPosition;

					Color lineColor = (pre.IsUnlocked && tech.IsUnlocked) ? Colors.Gold : (pre.IsUnlocked ? Colors.Cyan : new Color(0.3f, 0.35f, 0.4f, 0.5f));
					float lineWidth = (pre.IsUnlocked && tech.IsUnlocked) ? 3.0f : 1.5f;

					DrawLine(fromPos, toPos, lineColor, lineWidth);
				}
			}

			// 5. 绘制所有科技节点
			foreach (var tech in MetaProgressionManager.Instance.AllTechs.Values)
			{
				DrawTechNode(tech);
			}

			// 6. 绘制底部控制条
			DrawLine(_panelArea.Position + new Vector2(25, _panelArea.Size.Y - 75), _panelArea.Position + new Vector2(_panelArea.Size.X - 25, _panelArea.Size.Y - 75), new Color(0.3f, 0.5f, 0.7f, 0.4f), 1.5f);

			// 按钮 1: 返回主菜单
			Rect2 backBtn = GetBackButtonRect();
			bool isHoverBack = backBtn.HasPoint(_currentMousePos);
			DrawRect(backBtn, isHoverBack ? new Color(0.45f, 0.15f, 0.15f) : new Color(0.22f, 0.08f, 0.08f));
			DrawRect(backBtn, isHoverBack ? Colors.White : Colors.OrangeRed, false, 1.2f);
			DrawString(font, backBtn.Position + new Vector2(20, 24), "◀ 返回主菜单 (ESC)", HorizontalAlignment.Center, -1, 12, Colors.White);

			// 按钮 2: 重置洗点
			Rect2 resetBtn = GetResetButtonRect();
			bool isHoverReset = resetBtn.HasPoint(_currentMousePos);
			DrawRect(resetBtn, isHoverReset ? new Color(0.55f, 0.2f, 0.2f) : new Color(0.35f, 0.15f, 0.15f));
			DrawRect(resetBtn, isHoverReset ? Colors.White : Colors.OrangeRed, false, 1.2f);
			DrawString(font, resetBtn.Position + new Vector2(20, 24), "🔄 100% 全额洗点", HorizontalAlignment.Center, -1, 12, Colors.White);

			// 按钮 3: 注资调试
			Rect2 addBtn = GetAddPointsButtonRect();
			bool isHoverAdd = addBtn.HasPoint(_currentMousePos);
			DrawRect(addBtn, isHoverAdd ? new Color(0.2f, 0.55f, 0.35f) : new Color(0.15f, 0.35f, 0.25f));
			DrawRect(addBtn, isHoverAdd ? Colors.White : Colors.LimeGreen, false, 1.2f);
			DrawString(font, addBtn.Position + new Vector2(20, 24), "💾 赞助 +100 碎片", HorizontalAlignment.Center, -1, 12, Colors.White);

			// 状态反馈文本
			DrawString(font, _panelArea.Position + new Vector2(615, _panelArea.Size.Y - 35), _feedbackLog, HorizontalAlignment.Left, -1, 12, _feedbackColor);

			// 7. 顶层绘制悬停战术卡片 (Tooltip)
			if (_hoveredNode != null)
			{
				DrawTechTooltip(_hoveredNode, _currentMousePos);
			}
		}

		private void DrawTechNode(MetaTechNode node)
		{
			Rect2 rect = GetNodeScreenRect(node);
			var font = ThemeDB.FallbackFont;
			bool isHover = node == _hoveredNode;

			bool hasPrereq = string.IsNullOrEmpty(node.PrerequisiteId) || MetaProgressionManager.Instance.AllTechs[node.PrerequisiteId].IsUnlocked;
			bool canAfford = MetaProgressionManager.Instance.DataFragments >= node.Cost;

			Color bgColor;
			Color borderColor;

			if (node.IsUnlocked)
			{
				bgColor = new Color(0.10f, 0.25f, 0.18f, 0.95f);
				borderColor = Colors.Gold;
			}
			else if (hasPrereq)
			{
				bgColor = isHover ? new Color(0.12f, 0.22f, 0.32f, 0.95f) : new Color(0.06f, 0.12f, 0.18f, 0.85f);
				borderColor = canAfford ? Colors.Cyan : Colors.OrangeRed;
			}
			else
			{
				bgColor = new Color(0.04f, 0.06f, 0.08f, 0.80f);
				borderColor = new Color(0.3f, 0.35f, 0.4f, 0.5f);
			}

			DrawRect(rect, bgColor);
			DrawRect(rect, borderColor, false, isHover ? 2.5f : (node.IsUnlocked ? 2.0f : 1.2f));

			string statusTag = node.IsUnlocked ? "[已激活]" : (hasPrereq ? $"{node.Cost} 💾" : "[未解锁前置]");
			Color tagColor = node.IsUnlocked ? Colors.LimeGreen : (hasPrereq ? (canAfford ? Colors.Gold : Colors.OrangeRed) : Colors.Gray);

			DrawString(font, rect.Position + new Vector2(10, 26), node.Name, HorizontalAlignment.Left, -1, 12, node.IsUnlocked ? Colors.Gold : (hasPrereq ? Colors.White : Colors.Gray));
			DrawString(font, rect.Position + new Vector2(10, 48), $"T{node.Tier} 科技 | {statusTag}", HorizontalAlignment.Left, -1, 11, tagColor);
		}

		private void DrawTechTooltip(MetaTechNode node, Vector2 mousePos)
		{
			Vector2 cardSize = new(300, 115);
			Vector2 cardPos = mousePos + new Vector2(18, 18);

			Vector2 vpSize = GetViewportRect().Size;
			if (cardPos.X + cardSize.X > vpSize.X - 20) cardPos.X = mousePos.X - cardSize.X - 10;
			if (cardPos.Y + cardSize.Y > vpSize.Y - 20) cardPos.Y = mousePos.Y - cardSize.Y - 10;

			DrawRect(new Rect2(cardPos, cardSize), new Color(0.03f, 0.07f, 0.12f, 0.98f));
			DrawRect(new Rect2(cardPos, cardSize), node.IsUnlocked ? Colors.Gold : Colors.Cyan, false, 2.0f);

			var font = ThemeDB.FallbackFont;
			DrawString(font, cardPos + new Vector2(12, 22), $"【T{node.Tier} {node.Name}】", HorizontalAlignment.Left, -1, 13, node.IsUnlocked ? Colors.Gold : Colors.Cyan);
			DrawString(font, cardPos + new Vector2(12, 44), $"研发成本: {node.Cost} 💾 数据碎片", HorizontalAlignment.Left, -1, 11, Colors.LightGray);
			DrawString(font, cardPos + new Vector2(12, 66), node.Description, HorizontalAlignment.Left, 276, 11, Colors.White);
		}
	}
}
