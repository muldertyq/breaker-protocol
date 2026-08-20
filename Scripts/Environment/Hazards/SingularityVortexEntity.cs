using Godot;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.Environment.Asteroids;

namespace BreakerProtocol.Environment.Hazards
{
	/// <summary>
	/// 空间高引力黑洞奇点漩涡实体 (规范 07 / TASK-26)
	/// </summary>
	public partial class SingularityVortexEntity : Node2D
	{
		[Export] public float GravityRadius { get; set; } = 650.0f; // 引力影响范围 (像素)
		[Export] public float EventHorizonRadius { get; set; } = 60.0f; // 绝对事件视界半径 (进入必死撕裂)
		[Export] public float GravitationalStrength { get; set; } = 3200.0f; // 引力强度常数

		private float _rotationAngle = 0.0f;
		private float _damageTickTimer = 0.0f;

		public override void _Ready()
		{
			ZIndex = 8;
			AddToGroup("SingularityHazard");
		}

		public override void _PhysicsProcess(double delta)
		{
			float dt = (float)delta;
			_rotationAngle += dt * 3.5f;

			// 1. 引力拉拽所有物理刚体 (战舰 / 小行星 / 残骸)
			ApplyGravitationalField(dt);

			// 2. 引潮力剪切撕裂伤害 (靠近视界的战舰遭受结构撕裂)
			_damageTickTimer += dt;
			if (_damageTickTimer >= 0.20f)
			{
				_damageTickTimer = 0.0f;
				ApplyTidalDamage();
			}

			QueueRedraw();
		}

		private void ApplyGravitationalField(float dt)
		{
			// 拉拽战舰
			var ships = GetTree().GetNodesInGroup("Ship");
			foreach (var node in ships)
			{
				if (node is RigidBody2D rb)
				{
					float dist = GlobalPosition.DistanceTo(rb.GlobalPosition);
					if (dist < GravityRadius && dist > EventHorizonRadius * 0.4f)
					{
						Vector2 pullDir = (GlobalPosition - rb.GlobalPosition).Normalized();
						// 引力与距离平方成反比 (加底保底防奇点无穷大)
						float forceFactor = 1.0f - (dist / GravityRadius);
						float force = GravitationalStrength * (forceFactor * forceFactor) * rb.Mass;
						rb.ApplyCentralForce(pullDir * force);
					}
				}
			}

			// 拉拽小行星与残骸
			var asteroids = GetTree().GetNodesInGroup("Asteroid");
			foreach (var node in asteroids)
			{
				if (node is RigidBody2D rb)
				{
					float dist = GlobalPosition.DistanceTo(rb.GlobalPosition);
					if (dist < GravityRadius && dist > EventHorizonRadius * 0.5f)
					{
						Vector2 pullDir = (GlobalPosition - rb.GlobalPosition).Normalized();
						float forceFactor = 1.0f - (dist / GravityRadius);
						rb.ApplyCentralForce(pullDir * (GravitationalStrength * 0.6f * forceFactor * rb.Mass));
					}
				}
			}
		}

		private void ApplyTidalDamage()
		{
			var ships = GetTree().GetNodesInGroup("Ship");
			foreach (var node in ships)
			{
				if (node is ShipEntity ship)
				{
					float dist = GlobalPosition.DistanceTo(ship.GlobalPosition);

					// 战舰深入引潮力危险区 (< 160px)
					if (dist < EventHorizonRadius * 2.8f)
					{
						float intensity = 1.0f - (dist / (EventHorizonRadius * 2.8f));
						float damage = intensity * 45.0f;

						var modules = ship.Grid.Modules;
						foreach (var m in modules)
						{
							if (!m.IsDestroyed && GD.Randf() > 0.45f)
							{
								m.CurrentHp = Mathf.Max(0.0f, m.CurrentHp - damage);
								ship.OnModuleDamaged(m, damage);

								Vector2 modWorld = ship.GlobalTransform * GlobalMetrics.MetersToPixels((Vector2)m.GridPosition + new Vector2(0.5f, 0.5f));
								VfxManager.Instance?.SpawnElectricArc(modWorld, GlobalPosition, Colors.Purple);
								break;
							}
						}

						if (dist < EventHorizonRadius * 1.2f)
						{
							VfxManager.Instance?.SpawnFloatingText(ship.GlobalPosition, "⚠️ 引潮力机械过载撕裂!", Colors.OrangeRed);
							JuiceManager.Instance?.AddCameraTrauma(0.35f);
						}
					}
				}
			}

			// 吞噬过于靠近视界的小行星
			var asteroids = GetTree().GetNodesInGroup("Asteroid");
			foreach (var node in asteroids)
			{
				if (node is AsteroidEntity asteroid)
				{
					if (GlobalPosition.DistanceTo(asteroid.GlobalPosition) < EventHorizonRadius * 1.1f)
					{
						VfxManager.Instance?.SpawnModuleExplosion(asteroid.GlobalPosition, new Vector2(40, 40), Colors.DarkViolet, shardCount: 12);
						asteroid.QueueFree();
					}
				}
			}
		}

		/// <summary>
		/// 计算指定点在当前物理帧受到的引力加速度矢量 (供弹道引力透镜弯折使用)
		/// </summary>
		public Vector2 GetGravitationalAcceleration(Vector2 targetPos)
		{
			float dist = GlobalPosition.DistanceTo(targetPos);
			if (dist >= GravityRadius || dist <= 10.0f) return Vector2.Zero;

			Vector2 dir = (GlobalPosition - targetPos).Normalized();
			float factor = 1.0f - (dist / GravityRadius);
			float accel = (GravitationalStrength * 2.2f) * (factor * factor);
			return dir * accel;
		}

		public override void _Draw()
		{
			// 1. 引力透镜吸积盘 (Accretion Disk)
			for (int i = 0; i < 4; i++)
			{
				float r = EventHorizonRadius + (i * 24.0f);
				float offset = _rotationAngle * (i % 2 == 0 ? 1.0f : -1.2f) + (i * 1.2f);
				Color ringColor = new(0.6f + (i * 0.1f), 0.2f, 0.9f - (i * 0.1f), 0.65f - (i * 0.12f));
				DrawArc(Vector2.Zero, r, offset, offset + 3.8f, 32, ringColor, 3.0f - (i * 0.4f));
			}

			// 2. 引力边界光晕圈
			DrawArc(Vector2.Zero, GravityRadius, 0, Mathf.Tau, 64, new Color(0.4f, 0.1f, 0.8f, 0.10f), 1.5f);

			// 3. 绝对事件视界黑色核心 (Event Horizon)
			DrawCircle(Vector2.Zero, EventHorizonRadius, Colors.Black);
			DrawArc(Vector2.Zero, EventHorizonRadius, 0, Mathf.Tau, 32, new Color(0.85f, 0.35f, 1.0f, 0.90f), 3.5f);
		}
	}
}
