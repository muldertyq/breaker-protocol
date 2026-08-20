using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.World.Sandbox;

namespace BreakerProtocol.UI.Sandbox
{
	/// <summary>
	/// 母港虚拟风洞测试靶场全息控制面板 UI
	/// </summary>
	public partial class SandboxBayUI : Control
	{
		private Rect2 _panelArea;
		private float _animTime = 0.0f;
		private Vector2 _currentMousePos = Vector2.Zero;

		public override void _Ready()
		{
			SetAnchorsPreset(LayoutPreset.FullRect);
			GrowHorizontal = GrowDirection.Both;
			GrowVertical = GrowDirection.Both;
			MouseFilter = MouseFilterEnum.Ignore;
		}

		public override void _Process(double delta)
		{
			_animTime += (float)delta * 3.0f;
			_currentMousePos = GetLocalMousePosition();
			QueueRedraw();
		}

		public override void _Draw()
		{
			Vector2 vpSize = GetViewportRect().Size;
			float w = vpSize.X > 100 ? vpSize.X : 1280.0f;
			_panelArea = new Rect2(w - 360, 70, 330, 310);

			var font = ThemeDB.FallbackFont;
			var mgr = SandboxBayManager.Instance;
			if (mgr == null) return;

			// 1. 绘制全息测试仪背板
			DrawRect(_panelArea, new Color(0.02f, 0.06f, 0.10f, 0.92f));
			DrawRect(_panelArea, Colors.Cyan, false, 2.0f);

			// 2. 标头
			DrawString(font, _panelArea.Position + new Vector2(15, 24), "【 虚拟风洞实弹打靶遥测仪 】 SANDBOX BAY", HorizontalAlignment.Left, -1, 12, Colors.Gold);
			DrawLine(_panelArea.Position + new Vector2(10, 36), _panelArea.Position + new Vector2(_panelArea.Size.X - 10, 36), new Color(0.3f, 0.5f, 0.7f, 0.4f), 1.0f);

			// 3. 实时 DPS 仪表核心数值
			DrawString(font, _panelArea.Position + new Vector2(15, 62), "实时秒伤 (DPS 3.0s 滑窗):", HorizontalAlignment.Left, -1, 11, Colors.White);
			DrawString(font, _panelArea.Position + new Vector2(15, 96), $"{mgr.CurrentDPS:F0} HP/s", HorizontalAlignment.Left, -1, 26, Colors.LimeGreen);

			DrawString(font, _panelArea.Position + new Vector2(180, 62), "峰值秒伤 (Peak):", HorizontalAlignment.Left, -1, 11, Colors.LightGray);
			DrawString(font, _panelArea.Position + new Vector2(180, 92), $"{mgr.PeakDPS:F0} HP/s", HorizontalAlignment.Left, -1, 18, Colors.Gold);

			DrawLine(_panelArea.Position + new Vector2(10, 110), _panelArea.Position + new Vector2(_panelArea.Size.X - 10, 110), new Color(0.3f, 0.5f, 0.7f, 0.4f), 1.0f);

			// 4. 创伤与跳弹率
			DrawString(font, _panelArea.Position + new Vector2(15, 132), $"• 累计造成创伤: {mgr.TotalDamageDealt:F0} HP", HorizontalAlignment.Left, -1, 11, Colors.White);
			DrawString(font, _panelArea.Position + new Vector2(15, 154), $"• 装甲跳弹偏折率: {mgr.GetRicochetRate():F1}% ({mgr.TotalRicochets}/{mgr.TotalHits})", HorizontalAlignment.Left, -1, 11, Colors.Cyan);

			DrawLine(_panelArea.Position + new Vector2(10, 172), _panelArea.Position + new Vector2(_panelArea.Size.X - 10, 172), new Color(0.3f, 0.5f, 0.7f, 0.4f), 1.0f);

			// 5. 调试环境开关状态
			string pwrStr = mgr.InfinitePower ? "[color=green]🟢开启[/color]" : "[color=gray]关闭[/color]";
			string coolStr = mgr.ZeroThermal ? "[color=green]🟢开启[/color]" : "[color=gray]关闭[/color]";
			DrawString(font, _panelArea.Position + new Vector2(15, 196), $"• 无限能量开火: {pwrStr}  (按 [P] 切换)", HorizontalAlignment.Left, -1, 11, Colors.White);
			DrawString(font, _panelArea.Position + new Vector2(15, 218), $"• 零发热超导态: {coolStr}  (按 [O] 切换)", HorizontalAlignment.Left, -1, 11, Colors.White);

			// 6. 靶标生成热键指南
			DrawLine(_panelArea.Position + new Vector2(10, 235), _panelArea.Position + new Vector2(_panelArea.Size.X - 10, 235), new Color(0.3f, 0.5f, 0.7f, 0.4f), 1.0f);
			DrawString(font, _panelArea.Position + new Vector2(15, 256), "[靶标生成]: [1] 静止轻靶 | [2] 机动风筝靶", HorizontalAlignment.Left, -1, 11, Colors.Gold);
			DrawString(font, _panelArea.Position + new Vector2(15, 276), "            [3] 巡洋战舰靶 | [K] 清空靶舰", HorizontalAlignment.Left, -1, 11, Colors.Gold);
			DrawString(font, _panelArea.Position + new Vector2(15, 296), "            [U] 一键清空重置秒伤统计", HorizontalAlignment.Left, -1, 11, Colors.Yellow);
		}
	}
}
