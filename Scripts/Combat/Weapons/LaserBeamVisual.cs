using Godot;
using BreakerProtocol.Combat.Effects;

namespace BreakerProtocol.Combat.Weapons
{
	/// <summary>
	/// 瞬发电能/高维激光光束实体
	/// </summary>
	public partial class LaserBeamVisual : Node2D
	{
		public Vector2 StartPoint { get; set; }
		public Vector2 EndPoint { get; set; }
		public Color BeamColor { get; set; } = new Color(0.3f, 0.9f, 1.0f, 1.0f);
		public float Duration { get; set; } = 0.12f;
		public float BeamWidth { get; set; } = 6.0f;

		private float _timer = 0.0f;

		public override void _Ready()
		{
			ZIndex = 11;
		}

		public override void _Process(double delta)
		{
			_timer += (float)delta;
			if (_timer >= Duration)
			{
				QueueFree();
				return;
			}
			QueueRedraw();
		}

		public override void _Draw()
		{
			Vector2 localStart = ToLocal(StartPoint);
			Vector2 localEnd = ToLocal(EndPoint);

			float fade = 1.0f - (_timer / Duration);
			float currentWidth = BeamWidth * fade;

			// 1. 外层光晕
			Color outerColor = new(BeamColor.R, BeamColor.G, BeamColor.B, fade * 0.8f);
			DrawLine(localStart, localEnd, outerColor, currentWidth * 2.0f);

			// 2. 内层白炽激光核心
			Color innerCore = new(1.0f, 1.0f, 1.0f, fade);
			DrawLine(localStart, localEnd, innerCore, currentWidth * 0.6f);

			// 3. 终点撞击火花光斑
			DrawCircle(localEnd, currentWidth * 1.5f, outerColor);
			DrawCircle(localEnd, currentWidth * 0.8f, Colors.White);
		}
	}
}
