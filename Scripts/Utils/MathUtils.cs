using Godot;

namespace BreakerProtocol.Utils
{
	/// <summary>
	/// 通用平滑数学工具类
	/// 提供无顿挫、帧率无关的平滑阻尼算法 (SmoothDamp)
	/// </summary>
	public static class MathUtils
	{
		/// <summary>
		/// 标量浮点数平滑阻尼 (Float SmoothDamp)
		/// 模拟临界阻尼弹簧震荡系统，平滑追踪目标值
		/// </summary>
		/// <param name="current">当前值</param>
		/// <param name="target">目标值</param>
		/// <param name="currentVelocity">当前变化速度（引用传递）</param>
		/// <param name="smoothTime">达到目标的大致耗时（秒）</param>
		/// <param name="deltaTime">帧间隔时间</param>
		/// <param name="maxSpeed">最大变化速率上限</param>
		/// <returns>计算后的新当前值</returns>
		public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float deltaTime, float maxSpeed = Mathf.Inf)
		{
			// 避免除以零
			smoothTime = Mathf.Max(0.0001f, smoothTime);
			float omega = 2.0f / smoothTime;

			float x = omega * deltaTime;
			float exp = 1.0f / (1.0f + x + (0.48f * x * x) + (0.235f * x * x * x));
			float change = current - target;
			float originalTo = target;

			// 限制最大移动速度
			float maxChange = maxSpeed * smoothTime;
			change = Mathf.Clamp(change, -maxChange, maxChange);
			target = current - change;

			float temp = (currentVelocity + (omega * change)) * deltaTime;
			currentVelocity = (currentVelocity - (omega * temp)) * exp;
			float output = target + ((change + temp) * exp);

			// 防止过冲（Overshoot）
			if (((originalTo - current > 0.0f) && (output > originalTo)) ||
				((originalTo - current < 0.0f) && (output < originalTo)))
			{
				output = originalTo;
				currentVelocity = (output - originalTo) / deltaTime;
			}

			return output;
		}

		/// <summary>
		/// 2D 向量平滑阻尼 (Vector2 SmoothDamp)
		/// 用于摄像机位置追踪
		/// </summary>
		public static Vector2 SmoothDampVec2(Vector2 current, Vector2 target, ref Vector2 currentVelocity, float smoothTime, float deltaTime, float maxSpeed = Mathf.Inf)
		{
			float vx = currentVelocity.X;
			float vy = currentVelocity.Y;

			float x = SmoothDamp(current.X, target.X, ref vx, smoothTime, deltaTime, maxSpeed);
			float y = SmoothDamp(current.Y, target.Y, ref vy, smoothTime, deltaTime, maxSpeed);

			currentVelocity = new Vector2(vx, vy);
			return new Vector2(x, y);
		}
	}
}
