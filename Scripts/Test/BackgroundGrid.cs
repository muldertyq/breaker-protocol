using Godot;
using BreakerProtocol.Core;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// 无限动态视口背景网格 (随摄像机视野无限延伸，支持缩放自适应与防摩尔纹)
	/// </summary>
	public partial class BackgroundGrid : Node2D
	{
		// 基础主次网格间隔 (像素)
		private const float MinorGridStep = 40.0f; // 次要小网格 (5 GU = 40px = 5m)
		private const float MajorGridStep = 200.0f; // 主要大网格 (25 GU = 200px = 25m)

		// 颜色定义 (深空工业调色)
		private readonly Color _minorGridColor = new(0.10f, 0.12f, 0.16f, 1.0f);
		private readonly Color _majorGridColor = new(0.18f, 0.22f, 0.28f, 1.0f);
		private readonly Color _axisColor = new(0.35f, 0.45f, 0.60f, 1.0f);

		public override void _Process(double delta)
		{
			// 每一帧通知重绘，跟随摄像机视野
			QueueRedraw();
		}

		public override void _Draw()
		{
			var viewport = GetViewport();
			if (viewport == null) return;

			var camera = viewport.GetCamera2D();
			if (camera == null) return;

			// 1. 计算当前摄像机在世界空间中的可视矩形 (Visible World Rect)
			Vector2 viewSize = viewport.GetVisibleRect().Size / camera.Zoom;
			Vector2 camPos = camera.GlobalPosition;

			// 视野边界（向外多扩出 200px 缓冲，防止边缘线闪烁）
			float left = camPos.X - (viewSize.X * 0.5f) - 200.0f;
			float right = camPos.X + (viewSize.X * 0.5f) + 200.0f;
			float top = camPos.Y - (viewSize.Y * 0.5f) - 200.0f;
			float bottom = camPos.Y + (viewSize.Y * 0.5f) + 200.0f;

			// 2. 对齐到网格整数倍起点
			float startX = Mathf.Floor(left / MinorGridStep) * MinorGridStep;
			float startY = Mathf.Floor(top / MinorGridStep) * MinorGridStep;

			// 3. 动态绘制纵向垂直网格线 (Vertical Lines)
			for (float x = startX; x <= right; x += MinorGridStep)
			{
				bool isAxis = Mathf.IsZeroApprox(x);
				bool isMajor = Mathf.IsZeroApprox(Mathf.PosMod(x, MajorGridStep));

				Color lineColor = isAxis ? _axisColor : (isMajor ? _majorGridColor : _minorGridColor);
				float lineWidth = isAxis ? 2.0f : (isMajor ? 1.5f : 1.0f);

				DrawLine(new Vector2(x, top), new Vector2(x, bottom), lineColor, lineWidth);
			}

			// 4. 动态绘制横向水平网格线 (Horizontal Lines)
			for (float y = startY; y <= bottom; y += MinorGridStep)
			{
				bool isAxis = Mathf.IsZeroApprox(y);
				bool isMajor = Mathf.IsZeroApprox(Mathf.PosMod(y, MajorGridStep));

				Color lineColor = isAxis ? _axisColor : (isMajor ? _majorGridColor : _minorGridColor);
				float lineWidth = isAxis ? 2.0f : (isMajor ? 1.5f : 1.0f);

				DrawLine(new Vector2(left, y), new Vector2(right, y), lineColor, lineWidth);
			}
		}
	}
}
