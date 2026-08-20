using System;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;

namespace BreakerProtocol.UI.Sandbox
{
	/// <summary>
	/// 虚拟风洞自由改装靶场 UI
	/// </summary>
	public partial class SandboxBayUI : Control
	{
		public event Action? OnBackToMainMenu;

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
			QueueRedraw();
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
					OnBackToMainMenu?.Invoke();
				}
			}
		}

		private void HandleClick(Vector2 clickPos)
		{
			if (GetExitButtonRect().HasPoint(clickPos))
			{
				OnBackToMainMenu?.Invoke();
			}
		}

		private Rect2 GetExitButtonRect()
		{
			Vector2 vpSize = GetViewportRect().Size;
			return new Rect2(vpSize.X - 220, 20, 180, 42);
		}

		public override void _Draw()
		{
			if (!Visible) return;

			var font = ThemeDB.FallbackFont;
			Vector2 vpSize = GetViewportRect().Size;

			// 1. 顶部标题
			DrawString(font, new Vector2(60, 45), "✦ 虚拟风洞测试靶场 · 自由改装测试 ✦ SANDBOX LAB", HorizontalAlignment.Left, -1, 16, Colors.Cyan);

			// 2. 右上角退出按钮
			Rect2 exitBtn = GetExitButtonRect();
			bool isHover = exitBtn.HasPoint(_currentMousePos);
			DrawRect(exitBtn, isHover ? new Color(0.4f, 0.15f, 0.15f) : new Color(0.18f, 0.08f, 0.08f));
			DrawRect(exitBtn, isHover ? Colors.White : Colors.OrangeRed, false, 1.5f);
			DrawString(font, exitBtn.Position + new Vector2(25, 26), "◀ 退出靶场 (ESC)", HorizontalAlignment.Center, -1, 12, Colors.White);
		}
	}
}
