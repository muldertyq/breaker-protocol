using Godot;

namespace BreakerProtocol.World.Economy
{
	public enum SalvageType
	{
		Scraps,      // 基础金属废料
		ComputeCore, // 军用算力核心
		WeaponCrate  // 武器装备补给箱
	}

	/// <summary>
	/// 战场物理漂浮战利品残骸实体 (支持惯性漂移与牵引光束捕获)
	/// </summary>
	public partial class SalvageDropEntity : Node2D
	{
		public SalvageType Type { get; set; } = SalvageType.Scraps;
		public int Amount { get; set; } = 40;
		public Vector2 Velocity { get; set; } = Vector2.Zero;
		public bool IsBeingPulled { get; set; } = false;

		private float _animTime = 0.0f;
		private float _pulseSpeed = 4.0f;

		public override void _Ready()
		{
			AddToGroup("Salvage");
			_animTime = (float)GD.RandRange(0.0, 5.0);
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;
			_animTime += dt * _pulseSpeed;

			// 物理移动与真空微阻尼
			Position += Velocity * dt;
			if (!IsBeingPulled)
			{
				Velocity = Velocity.MoveToward(Vector2.Zero, 25.0f * dt);
			}

			IsBeingPulled = false; // 每帧由牵引光束重置状态
			QueueRedraw();
		}

		public override void _Draw()
		{
			Color mainColor = Type switch
			{
				SalvageType.ComputeCore => Colors.Cyan,
				SalvageType.WeaponCrate => Colors.Gold,
				_                       => Colors.LimeGreen
			};

			float pulse = (Mathf.Sin(_animTime) + 1.0f) * 0.5f;
			float radius = 9.0f + pulse * 2.5f;

			// 1. 绘制外发光晕
			DrawCircle(Vector2.Zero, radius + 4.0f, new Color(mainColor.R, mainColor.G, mainColor.B, 0.25f));

			// 2. 绘制像素菱形战利品货箱
			Vector2[] diamond = new Vector2[]
			{
				new(0, -radius),
				new(radius, 0),
				new(0, radius),
				new(-radius, 0)
			};
			DrawColoredPolygon(diamond, new Color(0.05f, 0.12f, 0.18f, 0.95f));
			DrawPolyline(diamond, mainColor, 1.8f);
			DrawLine(diamond[3], diamond[0], mainColor, 1.8f);

			// 3. 内部核心小点
			DrawCircle(Vector2.Zero, 3.0f, mainColor);
		}
	}
}
