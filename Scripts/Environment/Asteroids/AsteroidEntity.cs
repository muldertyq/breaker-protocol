using System.Collections.Generic;
using Godot;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;

namespace BreakerProtocol.Environment.Asteroids
{
	public enum AsteroidType
	{
		Iron,      // 重金属铁矿：高装甲抗性 (8.0)，质重耐打
		Crystal,   // 虚空水晶：中等抗性 (4.0)，破碎产生晶莹爆光
		Volatile   // 易爆挥发矿：低抗性 (2.0)，被摧毁时引发 350HP 范围殉爆冲击波
	}

	/// <summary>
	/// 可破坏空间物理小行星实体 (支持装甲跳弹、分形破碎与战术掩体碰撞)
	/// </summary>
	public partial class AsteroidEntity : RigidBody2D
	{
		public AsteroidType Type { get; set; } = AsteroidType.Iron;
		public float RadiusPixels { get; set; } = 48.0f;
		public float MaxHp { get; set; } = 120.0f;
		public float CurrentHp { get; set; } = 120.0f;
		public float ArmorResistance { get; set; } = 6.0f;

		private CollisionPolygon2D _collisionPolygon = null!;
		private Vector2[] _polygonPoints = System.Array.Empty<Vector2>();
		private Color _fillColor;
		private Color _outlineColor;

		public override void _Ready()
		{
			GravityScale = 0.0f;
			LinearDamp = 0.35f;
			AngularDamp = 0.50f;
			AddToGroup("Asteroid");
			AddToGroup("Obstacle");

			SetupProperties();
			GenerateProceduralMesh();
		}

		private void SetupProperties()
		{
			switch (Type)
			{
				case AsteroidType.Iron:
					ArmorResistance = 8.0f;
					MaxHp = RadiusPixels * 2.8f;
					Mass = Mathf.Max(5.0f, RadiusPixels * RadiusPixels * 0.08f);
					_fillColor = new Color(0.25f, 0.23f, 0.22f);
					_outlineColor = new Color(0.45f, 0.40f, 0.38f);
					break;

				case AsteroidType.Crystal:
					ArmorResistance = 4.0f;
					MaxHp = RadiusPixels * 2.0f;
					Mass = Mathf.Max(4.0f, RadiusPixels * RadiusPixels * 0.06f);
					_fillColor = new Color(0.15f, 0.25f, 0.35f);
					_outlineColor = new Color(0.35f, 0.70f, 0.95f);
					break;

				case AsteroidType.Volatile:
					ArmorResistance = 2.0f;
					MaxHp = RadiusPixels * 1.5f;
					Mass = Mathf.Max(4.0f, RadiusPixels * RadiusPixels * 0.05f);
					_fillColor = new Color(0.35f, 0.15f, 0.10f);
					_outlineColor = new Color(0.95f, 0.45f, 0.15f);
					break;
			}

			CurrentHp = MaxHp;
		}

		private void GenerateProceduralMesh()
		{
			int segments = Mathf.Clamp(Mathf.RoundToInt(RadiusPixels * 0.30f), 8, 20);
			_polygonPoints = new Vector2[segments];
			float angleStep = Mathf.Tau / segments;

			for (int i = 0; i < segments; i++)
			{
				float angle = i * angleStep;
				float radiusJitter = RadiusPixels * (float)GD.RandRange(0.80, 1.20);
				_polygonPoints[i] = new Vector2(Mathf.Cos(angle) * radiusJitter, Mathf.Sin(angle) * radiusJitter);
			}

			_collisionPolygon = new CollisionPolygon2D
			{
				Polygon = _polygonPoints
			};
			AddChild(_collisionPolygon);
		}

		/// <summary>
		/// 受到武器或碰撞创伤
		/// </summary>
		public void TakeDamage(float incomingDamage, ElementFlags elements, Vector2 hitDir, Vector2 hitWorldPos)
		{
			float actualDamage = Mathf.Max(5.0f, incomingDamage - ArmorResistance);

			// 热冲击与强酸对小行星增伤
			if ((elements & (ElementFlags.Thermal | ElementFlags.Acid)) != 0)
			{
				actualDamage *= 1.4f;
			}

			CurrentHp -= actualDamage;

			// 承受物理冲击力
			ApplyImpulse(hitDir * (incomingDamage * 8.0f), hitWorldPos - GlobalPosition);

			if (CurrentHp <= 0.0f)
			{
				Disintegrate(hitDir);
			}
			else
			{
				VfxManager.Instance?.SpawnImpactSparks(hitWorldPos, -hitDir, _outlineColor, isRicochet: false);
			}
		}

		private void Disintegrate(Vector2 impactDir)
		{
			// 1. 若为易爆型小行星，引爆 350HP 殉爆冲击波
			if (Type == AsteroidType.Volatile)
			{
				TriggerVolatileExplosion();
			}
			else
			{
				VfxManager.Instance?.SpawnModuleExplosion(GlobalPosition, new Vector2(RadiusPixels * 1.5f, RadiusPixels * 1.5f), _outlineColor, shardCount: 16);
				JuiceManager.Instance?.TriggerExplosionJuice(GlobalPosition, intensity: 0.6f);
			}

			// 2. 分形物理破碎：若小行星体积较大 (半径 > 26px)，分裂为 2~3 块更小的子小行星
			if (RadiusPixels >= 26.0f)
			{
				int splitCount = GD.RandRange(2, 3);
				float childRadius = RadiusPixels * (float)GD.RandRange(0.45, 0.60);

				for (int i = 0; i < splitCount; i++)
				{
					Vector2 splitDir = impactDir.Rotated((float)GD.RandRange(-1.2, 1.2)).Normalized();
					var childAsteroid = new AsteroidEntity
					{
						Type = this.Type,
						RadiusPixels = childRadius,
						GlobalPosition = GlobalPosition + (splitDir * (childRadius * 1.1f)),
						LinearVelocity = LinearVelocity * 0.5f + (splitDir * (float)GD.RandRange(80.0, 180.0)),
						AngularVelocity = (float)GD.RandRange(-4.0, 4.0)
					};

					GetTree().CurrentScene.AddChild(childAsteroid);
				}
			}

			QueueFree();
		}

		private void TriggerVolatileExplosion()
		{
			float explosionRadius = RadiusPixels * 4.5f;
			VfxManager.Instance?.SpawnModuleExplosion(GlobalPosition, new Vector2(explosionRadius, explosionRadius), Colors.OrangeRed, shardCount: 28);
			VfxManager.Instance?.SpawnFloatingText(GlobalPosition, "💥 易爆矿层殉爆 350HP!", Colors.Crimson);
			JuiceManager.Instance?.TriggerExplosionJuice(GlobalPosition, intensity: 1.3f);
			JuiceManager.Instance?.AddCameraTrauma(0.65f);

			// 对范围内战舰和小行星造成爆轰
			var bodies = GetTree().GetNodesInGroup("Ship");
			foreach (var node in bodies)
			{
				if (node is Ship.ShipEntity targetShip)
				{
					float dist = GlobalPosition.DistanceTo(targetShip.GlobalPosition);
					if (dist < explosionRadius)
					{
						float dmg = (1.0f - (dist / explosionRadius)) * 350.0f;
						var randomMod = targetShip.Grid.Modules;
						foreach (var m in randomMod)
						{
							if (!m.IsDestroyed)
							{
								m.CurrentHp = Mathf.Max(0.0f, m.CurrentHp - dmg);
								targetShip.OnModuleDamaged(m, dmg);
								break;
							}
						}
					}
				}
			}
		}

		public override void _Draw()
		{
			if (_polygonPoints.Length < 3) return;

			// 绘制小行星多边形实体
			DrawColoredPolygon(_polygonPoints, _fillColor);

			// 绘制粗糙外轮廓线
			for (int i = 0; i < _polygonPoints.Length; i++)
			{
				Vector2 p1 = _polygonPoints[i];
				Vector2 p2 = _polygonPoints[(i + 1) % _polygonPoints.Length];
				DrawLine(p1, p2, _outlineColor, 2.0f);
			}

			// 若受损严重，绘制龟裂纹路
			if (CurrentHp < MaxHp * 0.5f)
			{
				DrawLine(Vector2.Zero, _polygonPoints[0] * 0.7f, Colors.Black, 1.5f);
				DrawLine(Vector2.Zero, _polygonPoints[_polygonPoints.Length / 2] * 0.6f, Colors.Black, 1.5f);
			}
		}
	}
}
