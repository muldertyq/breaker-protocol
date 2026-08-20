using System;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Persistence;

namespace BreakerProtocol.UI.Menu
{
	/// <summary>
	/// 游戏主标题菜单全息界面 (支持视口自适应与全功能流转)
	/// </summary>
	public partial class MainMenuUI : Control
	{
		public event Action? OnNewRunRequested;
		public event Action? OnContinueRunRequested;
		public event Action? OnHangarRequested;
		public event Action? OnSandboxRequested;

		private int _hoveredButtonIndex = -1;
		private Vector2 _currentMousePos = Vector2.Zero;

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
			for (int i = 0; i < 5; i++)
			{
				if (GetMenuButtonRect(i).HasPoint(mousePos))
				{
					_hoveredButtonIndex = i;
					break;
				}
			}

			MouseDefaultCursorShape = (_hoveredButtonIndex != -1) ? CursorShape.PointingHand : CursorShape.Arrow;
		}

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

			for (int i = 0; i < 5; i++)
			{
				if (GetMenuButtonRect(i).HasPoint(clickPos))
				{
					switch (i)
					{
						case 0: // 继续战役
							if (hasSave) OnContinueRunRequested?.Invoke();
							break;
						case 1: // 开始新征程 (进入机库选船)
							OnNewRunRequested?.Invoke();
							break;
						case 2: // 母港科研总局
							OnHangarRequested?.Invoke();
							break;
						case 3: // 虚拟风洞测试靶场
							OnSandboxRequested?.Invoke();
							break;
						case 4: // 退出程序
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
			float btnH = 50.0f;
			float startY = viewSize.Y * 0.38f;
			float x = (viewSize.X - btnW) * 0.5f;
			return new Rect2(x, startY + (index * 64.0f), btnW, btnH);
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

			DrawString(font, new Vector2(center.X - 300, viewSize.Y * 0.16f), title, HorizontalAlignment.Center, 600, 38, Colors.Cyan);
			DrawString(font, new Vector2(center.X - 300, viewSize.Y * 0.22f), subTitle, HorizontalAlignment.Center, 600, 14, Colors.Gold);

			// 3. 绘制 5 大居中菜单按钮
			bool hasSave = SaveManager.Instance.HasActiveRunSave();
			string[] btnTexts = {
				hasSave ? "▶  继续战役 (恢复现场)" : "▷  暂无进行中的战役",
				"🚀  开始新征程 (机库选船)",
				"🔬  母港科研总局 (Meta 科技)",
				"🛠️  虚拟风洞靶场 (自由改装)",
				"✕  退出战备终端"
			};

			for (int i = 0; i < 5; i++)
			{
				Rect2 rect = GetMenuButtonRect(i);
				bool isHover = i == _hoveredButtonIndex;
				bool isEnabled = (i != 0 || hasSave);

				Color bgColor = isHover && isEnabled ? new Color(0.12f, 0.38f, 0.60f, 0.95f) : new Color(0.05f, 0.10f, 0.18f, 0.85f);
				Color borderColor = isEnabled ? (isHover ? Colors.White : Colors.Cyan) : Colors.DimGray;

				DrawRect(rect, bgColor);
				DrawRect(rect, borderColor, false, isHover && isEnabled ? 2.0f : 1.2f);

				Color textColor = isEnabled ? (isHover ? Colors.Gold : Colors.White) : Colors.Gray;
				DrawString(font, rect.Position + new Vector2(30, 32), btnTexts[i], HorizontalAlignment.Left, -1, 14, textColor);
			}

			// 4. 底部状态与版本信息
			DrawString(font, new Vector2(30, viewSize.Y - 25), "Breaker Protocol Core Engine v0.8.3 | Task-43 Fleet Hangar Active", HorizontalAlignment.Left, -1, 12, Colors.DarkGray);
		}
	}
}
