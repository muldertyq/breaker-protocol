using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.Environment.Asteroids
{
	/// <summary>
	/// 程序化小行星带生成与空间流管理器 (规范 07 / TASK-25)
	/// </summary>
	public partial class AsteroidFieldManager : Node2D
	{
		public static AsteroidFieldManager Instance { get; private set; } = null!;

		[Export] public Rect2 FieldArea { get; set; } = new(-1500, -1500, 3000, 3000);
		[Export] public int TargetAsteroidCount { get; set; } = 48;
		[Export] public float SafeSpawnRadius { get; set; } = 280.0f; // 战舰出生点安全空腔

		public override void _Ready()
		{
			Instance = this;
			ZIndex = -1; // 位于战舰与弹幕下方
		}

		/// <summary>
		/// 在指定区域内程序化布设动态漂流小行星群
		/// </summary>
		public void GenerateAsteroidField(Vector2 safeCenter)
		{
			// 清理现有小行星
			var existing = GetTree().GetNodesInGroup("Asteroid");
			foreach (var node in existing)
			{
				if (node is Node2D n2d) n2d.QueueFree();
			}

			int created = 0;
			int maxAttempts = TargetAsteroidCount * 4;

			for (int attempt = 0; attempt < maxAttempts && created < TargetAsteroidCount; attempt++)
			{
				Vector2 candidatePos = new(
					(float)GD.RandRange(FieldArea.Position.X, FieldArea.End.X),
					(float)GD.RandRange(FieldArea.Position.Y, FieldArea.End.Y)
				);

				// 避开玩家出生安全空腔
				if (candidatePos.DistanceTo(safeCenter) < SafeSpawnRadius)
				{
					continue;
				}

				// 随机小行星类型 (70% 铁矿, 20% 水晶, 10% 易爆矿)
				float typeRoll = (float)GD.RandRange(0.0, 1.0);
				AsteroidType type = AsteroidType.Iron;
				if (typeRoll > 0.90f) type = AsteroidType.Volatile;
				else if (typeRoll > 0.70f) type = AsteroidType.Crystal;

				float radius = (float)GD.RandRange(22.0, 58.0);

				var asteroid = new AsteroidEntity
				{
					Type = type,
					RadiusPixels = radius,
					GlobalPosition = candidatePos,
					LinearVelocity = new Vector2((float)GD.RandRange(-15.0, 15.0), (float)GD.RandRange(-15.0, 15.0)),
					AngularVelocity = (float)GD.RandRange(-0.6, 0.6)
				};

				AddChild(asteroid);
				created++;
			}

			GD.PrintRich($"[color=green][AsteroidField] 成功程序化生成 {created} 块动态漂流物理小行星！[/color]");
		}
	}
}
