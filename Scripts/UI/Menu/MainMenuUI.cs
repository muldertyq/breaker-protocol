using System;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Persistence;

namespace BreakerProtocol.UI.Menu
{
	/// <summary>
	/// 游戏主标题菜单全息界面 (支持视口自适应与双通道点击捕获)
	/// </summary>
	public partial class MainMenuUI : Control
	{
		public event Action? OnNewRunRequested;
		public event Action? OnContinueRunRequested;
		public event Action? OnHangarRequested;

		private int _hoveredButtonIndex = -1;
		private Vector2 _currentMousePos = Vector2.Zero;

		public override void _Ready()
		{
			SetAnchorsPreset(LayoutPreset.FullRect);
			GrowHorizontal = GrowDirection.Both;
			GrowVertical = GrowDirection.Both;
			MouseFilter = MouseFilterEnum.Stop;

			// 强制初始化视口全屏尺寸 (解决 CanvasLayer 下 Size 为 0 的问题)
			Vector2 vpSize = GetViewportRect().Size;
			CustomMinimumSize = vpSize;
			Size = vpSize;
		}

		public override void _Process(double delta)
		{
			if (!Visible) return;

			// 动态同步视口尺寸
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
			_hoveredButtonIndex = -1;
			for (int i = 0; i < 4; i++)
			{
				if (GetMenuButtonRect(i).HasPoint(mousePos))
				{
					_hoveredButtonIndex = i;
					break;
				}
			}

			MouseDefaultCursorShape = (_hoveredButtonIndex != -1) ? CursorShape.PointingHand : CursorShape.Arrow;
		}

		// -------------------------------------------------------------
		// 双通道鼠标输入监听 (确保 100% 捕获点击事件)
		// -------------------------------------------------------------
		public override void _GuiInput(InputEvent @event)
		{
			if (!Visible) return;
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				HandleMenuClick(_currentMousePos);
				AcceptEvent();
			}
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (!Visible) return;
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				HandleMenuClick(_currentMousePos);
			}
		}

		private void HandleMenuClick(Vector2 clickPos)
		{
			bool hasSave = SaveManager.Instance.HasActiveRunSave();

			for (int i = 0; i < 4; i++)
			{
				if (GetMenuButtonRect(i).HasPoint(clickPos))
				{
					switch (i)
					{
						case 0: // 继续战役
							if (hasSave)
							{
								OnContinueRunRequested?.Invoke();
							}
							break;
						case 1: // 全新启航
							OnNewRunRequested?.Invoke();
							break;
						case 2: // 母港科研局
							OnHangarRequested?.Invoke();
							break;
						case 3: // 退出程序
							GetTree().Quit();
							break;
					}
					return;
				}
			}
		}

		private Rect2 GetMenuButtonRect(int index)
		{
			Vector2 viewSize = GetViewportRect().Size;
			float btnW = 380.0f;
			float btnH = 52.0f;
			float startY = viewSize.Y * 0.44f;
			float x = (viewSize.X - btnW) * 0.5f;
			return new Rect2(x, startY + (index * 68.0f), btnW, btnH);
		}

		public override void _Draw()
		{
			if (!Visible) return;

			var font = ThemeDB.FallbackFont;
			Vector2 viewSize = GetViewportRect().Size;
			Vector2 center = viewSize * 0.5f;

			// 1. 全屏半透明科幻暗化底板
			DrawRect(new Rect2(Vector2.Zero, viewSize), new Color(0.02f, 0.04f, 0.08f, 0.94f));

			// 2. 主副标题居中绘制
			string title = "✦  断  路  协  议  ✦";
			string subTitle = "BREAKER PROTOCOL : VOID ASCENSION";

			DrawString(font, new Vector2(center.X - 300, viewSize.Y * 0.20f), title, HorizontalAlignment.Center, 600, 38, Colors.Cyan);
			DrawString(font, new Vector2(center.X - 300, viewSize.Y * 0.26f), subTitle, HorizontalAlignment.Center, 600, 14, Colors.Gold);

			// 3. 绘制 4 大居中菜单按钮
			bool hasSave = SaveManager.Instance.HasActiveRunSave();
			string[] btnTexts = {
				hasSave ? "▶  继续战役 (恢复现场)" : "▷  暂无进行中的战役",
				"🚀  全新启航 (开启新星区)",
				"🔬  母港科研总局 (Meta 科技)",
				"✕  退出战备终端"
			};

			for (int i = 0; i < 4; i++)
			{
				Rect2 rect = GetMenuButtonRect(i);
				bool isHover = i == _hoveredButtonIndex;
				bool isEnabled = (i != 0 || hasSave);

				Color bgColor = isHover && isEnabled ? new Color(0.12f, 0.38f, 0.60f, 0.95f) : new Color(0.05f, 0.10f, 0.18f, 0.85f);
				Color borderColor = isEnabled ? (isHover ? Colors.White : Colors.Cyan) : Colors.DimGray;

				DrawRect(rect, bgColor);
				DrawRect(rect, borderColor, false, isHover && isEnabled ? 2.0f : 1.2f);

				Color textColor = isEnabled ? (isHover ? Colors.Gold : Colors.White) : Colors.Gray;
				DrawString(font, rect.Position + new Vector2(30, 33), btnTexts[i], HorizontalAlignment.Left, -1, 14, textColor);
			}

			// 4. 底部状态与版本信息
			DrawString(font, new Vector2(30, viewSize.Y - 25), "Breaker Protocol Core Engine v0.8.2 | Task-42 State Machine Active", HorizontalAlignment.Left, -1, 12, Colors.DarkGray);
		}
	}
}
