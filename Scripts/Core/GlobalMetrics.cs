using Godot;

namespace BreakerProtocol.Core
{
	/// <summary>
	/// 全局度量衡与基础物理换算类
	/// 依据规范 01：1 逻辑网格 (GU) = 8x8 物理像素 (px) = 1.0 逻辑米 (m)
	/// </summary>
	public static class GlobalMetrics
	{
		// ==========================================
		// 1. 基础度量衡常量定义
		// ==========================================

		/// <summary>
		/// 每逻辑米对应的物理像素数 (Pixels Per Meter)
		/// </summary>
		public const float PixelsPerMeter = 8.0f;

		/// <summary>
		/// 单个网格单元的物理像素宽高 (Grid Unit Size in Pixels)
		/// </summary>
		public const int GridUnitPixels = 8;

		/// <summary>
		/// 单个网格单元对应的物理逻辑米长度 (1 GU = 1.0 m)
		/// </summary>
		public const float GridUnitMeters = 1.0f;

		/// <summary>
		/// 基准设计分辨率宽度 (1080p)
		/// </summary>
		public const int TargetScreenWidth = 1920;

		/// <summary>
		/// 基准设计分辨率高度 (1080p)
		/// </summary>
		public const int TargetScreenHeight = 1080;

		/// <summary>
		/// Zoom=1.0 时，屏幕可视宽度（物理米）：1920 / 8 = 240m
		/// </summary>
		public const float ViewportWidthMeters = TargetScreenWidth / PixelsPerMeter;

		/// <summary>
		/// Zoom=1.0 时，屏幕可视高度（物理米）：1080 / 8 = 135m
		/// </summary>
		public const float ViewportHeightMeters = TargetScreenHeight / PixelsPerMeter;

		// ==========================================
		// 2. 坐标与尺度换算工具函数
		// ==========================================

		/// <summary>
		/// 物理米转换为像素长度 (Meters -> Pixels)
		/// </summary>
		public static float MetersToPixels(float meters) => meters * PixelsPerMeter;

		/// <summary>
		/// 物理米向量转换为像素向量
		/// </summary>
		public static Vector2 MetersToPixels(Vector2 metersVector) => metersVector * PixelsPerMeter;

		/// <summary>
		/// 像素长度转换为物理米 (Pixels -> Meters)
		/// </summary>
		public static float PixelsToMeters(float pixels) => pixels / PixelsPerMeter;

		/// <summary>
		/// 像素向量转换为物理米向量
		/// </summary>
		public static Vector2 PixelsToMeters(Vector2 pixelVector) => pixelVector / PixelsPerMeter;

		/// <summary>
		/// 世界坐标（像素）转换为离散蓝图网格坐标 (GU)
		/// </summary>
		/// <param name="worldPixelPos">世界像素坐标</param>
		/// <returns>离散整数网格坐标</returns>
		public static Vector2I WorldPixelsToGrid(Vector2 worldPixelPos)
		{
			return new Vector2I(
				Mathf.FloorToInt(worldPixelPos.X / GridUnitPixels),
				Mathf.FloorToInt(worldPixelPos.Y / GridUnitPixels)
			);
		}

		/// <summary>
		/// 离散蓝图网格坐标转换为世界像素中心点坐标
		/// </summary>
		/// <param name="gridCoord">网格坐标</param>
		/// <returns>该网格中心的世界像素坐标</returns>
		public static Vector2 GridToWorldCenterPixels(Vector2I gridCoord)
		{
			return new Vector2(
				(gridCoord.X * GridUnitPixels) + (GridUnitPixels * 0.5f),
				(gridCoord.Y * GridUnitPixels) + (GridUnitPixels * 0.5f)
			);
		}
	}
}
