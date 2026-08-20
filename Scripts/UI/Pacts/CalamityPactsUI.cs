using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.World.Pacts;

namespace BreakerProtocol.UI.Pacts
{
	/// <summary>
	/// 灾厄契约高阶热度选择界面
	/// </summary>
	public partial class CalamityPactsUI : Control
	{
		private Rect2 _panelArea;
		private float _animTime = 0.0f;
		private Vector2 _currentMousePos = Vector2.Zero;
		private PactId? _hoveredPact = null;

		public event Action? OnStartWithPacts;

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
			_hoveredPact = null;
			int index = 0;
			foreach (var pact in CalamityPactManager.Instance.Pacts.Values)
			{
				if (GetPactCardRect(index).HasPoint(mousePos))
				{
					_hoveredPact = pact.Id;
					break;
				}
				index++;
			}

			bool isHoverBtn = GetStartButtonRect().HasPoint(mousePos);
			MouseDefaultCursorShape = (_hoveredPact != null || isHoverBtn) ? CursorShape.PointingHand : CursorShape.Arrow;
		}

		public override void _GuiInput(InputEvent @event)
		{
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
		}

		private void HandleClick(Vector2 clickPos)
		{
			int index = 0;
			foreach (var pact in CalamityPactManager.Instance.Pacts.Values)
			{
				if (GetPactCardRect(index).HasPoint(clickPos))
				{
					CalamityPactManager.Instance.TogglePact(pact.Id);
					return;
				}
				index++;
			}

			if (GetStartButtonRect().HasPoint(clickPos))
			{
				Visible = false;
				OnStartWithPacts?.Invoke();
			}
		}

		private Rect2 GetPanelArea()
		{
			Vector2 vpSize = GetViewportRect().Size;
			float w = vpSize.X > 100 ? vpSize.X : 1280.0f;
			float h = vpSize.Y > 100 ? vpSize.Y : 720.0f;
			return new Rect2(120, 65, w - 240, h - 130);
		}

		private Rect2 GetPactCardRect(int index)
		{
			var panel = GetPanelArea();
			int col = index % 2;
			int row = index / 2;
			float cardW = (panel.Size.X - 90) * 0.5f;
			float cardH = 105.0f;

			float x = panel.Position.X + 30 + (col * (cardW + 30));
			float y = panel.Position.Y + 80 + (row * (cardH + 20));
			return new Rect2(x, y, cardW, cardH);
		}

		private Rect2 GetStartButtonRect()
		{
			var panel = GetPanelArea();
			return new Rect2(panel.Position.X + (panel.Size.X * 0.5f) - 130, panel.End.Y - 60, 260, 42);
		}

		public override void _Draw()
		{
			_panelArea = GetPanelArea();
			var font = ThemeDB.FallbackFont;

			int totalHeat = CalamityPactManager.Instance.GetTotalHeatLevel();
			float mult = CalamityPactManager.Instance.GetScoreRewardMultiplier();

			// 1. 绘制科幻深渊契约背板
			DrawRect(_panelArea, new Color(0.04f, 0.02f, 0.05f, 0.96f));
			DrawRect(_panelArea, totalHeat > 0 ? Colors.OrangeRed : Colors.Cyan, false, 2.5f);

			// 2. 标头与当前热度总览
			DrawString(font, _panelArea.Position + new Vector2(30, 36), "【 极限挑战 · 灾厄契约热度系统 】 CALAMITY HEAT PACTS", HorizontalAlignment.Left, -1, 16, Colors.Gold);
			string heatTag = $"• 当前热度等级: 🔥 {totalHeat} 级  (结算收益: +{(mult - 1.0f) * 100:F0}%)";
			DrawString(font, _panelArea.Position + new Vector2(_panelArea.Size.X - 360, 36), heatTag, HorizontalAlignment.Right, -1, 13, Colors.OrangeRed);
			DrawLine(_panelArea.Position + new Vector2(25, 50), _panelArea.Position + new Vector2(_panelArea.Size.X - 25, 50), new Color(0.4f, 0.5f, 0.6f, 0.4f), 1.5f);

			// 3. 绘制 6 大灾厄契约卡片
			int index = 0;
			foreach (var pact in CalamityPactManager.Instance.Pacts.Values)
			{
				Rect2 card = GetPactCardRect(index);
				bool isHover = pact.Id == _hoveredPact;

				Color cardBg = pact.IsActive ? new Color(0.20f, 0.05f, 0.05f, 0.95f) : new Color(0.06f, 0.08f, 0.12f, 0.85f);
				Color cardBorder = pact.IsActive ? Colors.OrangeRed : (isHover ? Colors.White : new Color(0.3f, 0.4f, 0.5f, 0.6f));

				DrawRect(card, cardBg);
				DrawRect(card, cardBorder, false, pact.IsActive ? 2.2f : 1.2f);

				// 勾选状态框
				Rect2 checkRect = new(card.Position.X + 15, card.Position.Y + 16, 20, 20);
				DrawRect(checkRect, pact.IsActive ? Colors.OrangeRed : new Color(0.1f, 0.1f, 0.1f));
				DrawRect(checkRect, Colors.White, false, 1.0f);
				if (pact.IsActive)
				{
					DrawString(font, checkRect.Position + new Vector2(3, 16), "✔", HorizontalAlignment.Center, -1, 14, Colors.White);
				}

				// 标题与热度等级
				DrawString(font, card.Position + new Vector2(45, 30), $"{pact.Name} (🔥 +{pact.HeatLevel} 级)", HorizontalAlignment.Left, -1, 13, pact.IsActive ? Colors.Gold : Colors.White);
				DrawString(font, card.Position + new Vector2(15, 60), pact.Description, HorizontalAlignment.Left, (int)card.Size.X - 30, 11, Colors.LightGray);
				DrawString(font, card.Position + new Vector2(15, 92), $"• 惩罚: {pact.PenaltyTag}", HorizontalAlignment.Left, -1, 11, Colors.OrangeRed);

				index++;
			}

			// 4. 底部确认按钮
			Rect2 startBtn = GetStartButtonRect();
			bool isHoverBtn = startBtn.HasPoint(_currentMousePos);
			DrawRect(startBtn, isHoverBtn ? new Color(0.75f, 0.35f, 0.15f) : new Color(0.55f, 0.25f, 0.10f));
			DrawRect(startBtn, isHoverBtn ? Colors.White : Colors.Gold, false, isHoverBtn ? 2.0f : 1.2f);
			DrawString(font, startBtn.Position + new Vector2(30, 26), "🔥 签署契约 · 开启极限战役", HorizontalAlignment.Center, -1, 13, Colors.White);
		}
	}
}
