using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.Combat.Effects
{
	/// <summary>
	/// 全局轻量化像素战斗特效管理器 (支持火花、破片、浓烟、电弧、浮空跳字与生化火海)
	/// </summary>
	[GlobalClass]
	public partial class VfxManager : Node2D
	{
		public static VfxManager? Instance { get; private set; }

		private class SparkParticle
		{
			public Vector2 Position;
			public Vector2 Velocity;
			public Color Color;
			public float Lifetime;
			public float MaxLifetime;
			public float Length;
		}

		private class ShrapnelParticle
		{
			public Vector2 Position;
			public Vector2 Velocity;
			public float Rotation;
			public float AngularVelocity;
			public Vector2 Size;
			public Color Color;
			public float Lifetime;
			public float MaxLifetime;
		}

		private class SmokeParticle
		{
			public Vector2 Position;
			public Vector2 Velocity;
			public float Radius;
			public float MaxRadius;
			public Color Color;
			public float Lifetime;
			public float MaxLifetime;
		}

		private class ElectricArc
		{
			public List<Vector2> Points = new();
			public Color Color;
			public float Lifetime;
			public float MaxLifetime;
		}

		private class FloatingText
		{
			public Vector2 Position;
			public string Text = string.Empty;
			public Color Color;
			public float Lifetime;
			public float MaxLifetime;
		}

		private class AcidPool
		{
			public Vector2 Position;
			public float Radius;
			public float Lifetime;
			public float MaxLifetime;
		}

		private readonly List<SparkParticle> _sparks = new();
		private readonly List<ShrapnelParticle> _shrapnels = new();
		private readonly List<SmokeParticle> _smokes = new();
		private readonly List<ElectricArc> _arcs = new();
		private readonly List<FloatingText> _floatingTexts = new();
		private readonly List<AcidPool> _acidPools = new();

		public override void _EnterTree()
		{
			Instance = this;
			ZIndex = 20; // 置顶显示
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;

			// 1. 火星
			for (int i = _sparks.Count - 1; i >= 0; i--)
			{
				var s = _sparks[i];
				s.Lifetime -= dt;
				if (s.Lifetime <= 0.0f) { _sparks.RemoveAt(i); continue; }
				s.Position += s.Velocity * dt;
				s.Velocity *= Mathf.Exp(-4.0f * dt);
			}

			// 2. 破片
			for (int i = _shrapnels.Count - 1; i >= 0; i--)
			{
				var sh = _shrapnels[i];
				sh.Lifetime -= dt;
				if (sh.Lifetime <= 0.0f) { _shrapnels.RemoveAt(i); continue; }
				sh.Position += sh.Velocity * dt;
				sh.Rotation += sh.AngularVelocity * dt;
				sh.Velocity *= Mathf.Exp(-2.5f * dt);
			}

			// 3. 浓烟
			for (int i = _smokes.Count - 1; i >= 0; i--)
			{
				var sm = _smokes[i];
				sm.Lifetime -= dt;
				if (sm.Lifetime <= 0.0f) { _smokes.RemoveAt(i); continue; }
				sm.Position += sm.Velocity * dt;
				sm.Velocity *= Mathf.Exp(-3.0f * dt);
				float p = 1.0f - (sm.Lifetime / sm.MaxLifetime);
				sm.Radius = Mathf.Lerp(3.0f, sm.MaxRadius, p);
			}

			// 4. 电弧
			for (int i = _arcs.Count - 1; i >= 0; i--)
			{
				var a = _arcs[i];
				a.Lifetime -= dt;
				if (a.Lifetime <= 0.0f) _arcs.RemoveAt(i);
			}

			// 5. 浮空跳字 (向上飘动渐隐)
			for (int i = _floatingTexts.Count - 1; i >= 0; i--)
			{
				var ft = _floatingTexts[i];
				ft.Lifetime -= dt;
				if (ft.Lifetime <= 0.0f) { _floatingTexts.RemoveAt(i); continue; }
				ft.Position += new Vector2(0, -35.0f * dt);
			}

			// 6. 生化火海 (持续向外吐泡)
			for (int i = _acidPools.Count - 1; i >= 0; i--)
			{
				var ap = _acidPools[i];
				ap.Lifetime -= dt;
				if (ap.Lifetime <= 0.0f) { _acidPools.RemoveAt(i); continue; }
				
				if (GD.Randf() > 0.7f)
				{
					Vector2 randOffset = new((float)GD.RandRange(-ap.Radius, ap.Radius), (float)GD.RandRange(-ap.Radius, ap.Radius));
					SpawnSmoke(ap.Position + randOffset, new Vector2((float)GD.RandRange(-10, 10), (float)GD.RandRange(-20, -5)), maxRadius: 6.0f, isFire: true);
				}
			}

			QueueRedraw();
		}

		public void SpawnFloatingText(Vector2 worldPos, string text, Color color)
		{
			_floatingTexts.Add(new FloatingText
			{
				Position = worldPos + new Vector2((float)GD.RandRange(-15, 15), (float)GD.RandRange(-10, 10)),
				Text = text,
				Color = color,
				Lifetime = 1.1f,
				MaxLifetime = 1.1f
			});
		}

		public void SpawnAcidPool(Vector2 worldPos, float radius = 45.0f, float duration = 4.0f)
		{
			_acidPools.Add(new AcidPool
			{
				Position = worldPos,
				Radius = radius,
				Lifetime = duration,
				MaxLifetime = duration
			});
		}

		public void SpawnImpactSparks(Vector2 worldPos, Vector2 normal, Color baseColor, bool isRicochet)
		{
			int count = isRicochet ? 18 : 12;
			float baseSpeed = isRicochet ? 320.0f : 200.0f;
			float coneAngle = isRicochet ? Mathf.DegToRad(35.0f) : Mathf.DegToRad(70.0f);
			Vector2 baseDir = normal.Normalized();

			for (int i = 0; i < count; i++)
			{
				float angle = (float)GD.RandRange(-coneAngle, coneAngle);
				Vector2 dir = baseDir.Rotated(angle);
				float speed = baseSpeed * (float)GD.RandRange(0.6, 1.4);
				float life = (float)GD.RandRange(0.12, 0.28);

				Color sparkCol = isRicochet 
					? (GD.Randf() > 0.3f ? new Color(1.0f, 0.85f, 0.3f) : Colors.White)
					: baseColor;

				_sparks.Add(new SparkParticle
				{
					Position = worldPos,
					Velocity = dir * speed,
					Color = sparkCol,
					Lifetime = life,
					MaxLifetime = life,
					Length = (float)GD.RandRange(3.0, 7.0)
				});
			}
		}

		public void SpawnModuleExplosion(Vector2 worldPos, Vector2 sizePixels, Color baseColor, int shardCount = 16)
		{
			for (int i = 0; i < shardCount; i++)
			{
				float angle = (float)GD.RandRange(0, Mathf.Tau);
				Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));
				float speed = (float)GD.RandRange(120.0, 360.0);
				float life = (float)GD.RandRange(0.8, 1.6);
				float shardSize = (float)GD.RandRange(3.0, 7.0);

				Color shardCol = baseColor.Lerp(new Color(0.2f, 0.2f, 0.25f), (float)GD.RandRange(0.3, 0.8));

				_shrapnels.Add(new ShrapnelParticle
				{
					Position = worldPos + (dir * (float)GD.RandRange(2.0, 10.0)),
					Velocity = dir * speed,
					Rotation = (float)GD.RandRange(0, Mathf.Tau),
					AngularVelocity = (float)GD.RandRange(-12.0, 12.0),
					Size = new Vector2(shardSize, shardSize),
					Color = shardCol,
					Lifetime = life,
					MaxLifetime = life
				});
			}

			SpawnImpactSparks(worldPos, Vector2.Up, Colors.Yellow, isRicochet: false);
			SpawnImpactSparks(worldPos, Vector2.Down, Colors.Orange, isRicochet: false);

			for (int i = 0; i < 6; i++)
			{
				float angle = (float)GD.RandRange(0, Mathf.Tau);
				Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));
				SpawnSmoke(worldPos + dir * 6.0f, dir * (float)GD.RandRange(30.0, 80.0), maxRadius: 18.0f, isFire: true);
			}
		}

		public void SpawnSmoke(Vector2 worldPos, Vector2 velocity, float maxRadius = 12.0f, bool isFire = false)
		{
			float life = (float)GD.RandRange(0.6, 1.2);
			Color smokeCol = isFire
				? (GD.Randf() > 0.4f ? new Color(1.0f, 0.45f, 0.1f, 0.85f) : new Color(0.25f, 0.25f, 0.25f, 0.85f))
				: new Color(0.2f, 0.2f, 0.22f, 0.75f);

			_smokes.Add(new SmokeParticle
			{
				Position = worldPos,
				Velocity = velocity,
				Radius = 3.0f,
				MaxRadius = maxRadius,
				Color = smokeCol,
				Lifetime = life,
				MaxLifetime = life
			});
		}

		public void SpawnElectricArc(Vector2 startPos, Vector2 endPos, Color arcColor)
		{
			var points = new List<Vector2> { startPos };
			int segments = 4;
			Vector2 step = (endPos - startPos) / segments;
			Vector2 normal = new(-step.Y, step.X);

			for (int i = 1; i < segments; i++)
			{
				float offset = (float)GD.RandRange(-6.0, 6.0);
				points.Add(startPos + (step * i) + (normal.Normalized() * offset));
			}
			points.Add(endPos);

			_arcs.Add(new ElectricArc
			{
				Points = points,
				Color = arcColor,
				Lifetime = 0.08f,
				MaxLifetime = 0.08f
			});
		}

		public override void _Draw()
		{
			// 1. 绘制生化毒火海 (绿色沸腾毒圈)
			foreach (var ap in _acidPools)
			{
				float alpha = ap.Lifetime / ap.MaxLifetime;
				DrawCircle(ToLocal(ap.Position), ap.Radius, new Color(0.2f, 0.9f, 0.2f, alpha * 0.35f));
				DrawArc(ToLocal(ap.Position), ap.Radius, 0, Mathf.Tau, 24, new Color(0.4f, 1.0f, 0.3f, alpha * 0.7f), 2.0f);
			}

			// 2. 浓烟
			foreach (var sm in _smokes)
			{
				float alpha = sm.Lifetime / sm.MaxLifetime;
				Color col = new(sm.Color.R, sm.Color.G, sm.Color.B, sm.Color.A * alpha);
				DrawCircle(ToLocal(sm.Position), sm.Radius, col);
			}

			// 3. 电弧
			foreach (var a in _arcs)
			{
				float alpha = a.Lifetime / a.MaxLifetime;
				Color outerCol = new(a.Color.R, a.Color.G, a.Color.B, alpha * 0.8f);
				Color coreCol = new(1.0f, 1.0f, 1.0f, alpha);

				for (int i = 0; i < a.Points.Count - 1; i++)
				{
					Vector2 p1 = ToLocal(a.Points[i]);
					Vector2 p2 = ToLocal(a.Points[i + 1]);
					DrawLine(p1, p2, outerCol, 3.0f);
					DrawLine(p1, p2, coreCol, 1.2f);
				}
			}

			// 4. 破片
			foreach (var sh in _shrapnels)
			{
				float alpha = Mathf.Clamp(sh.Lifetime / sh.MaxLifetime, 0.0f, 1.0f);
				Color col = new(sh.Color.R, sh.Color.G, sh.Color.B, alpha);

				Vector2 localPos = ToLocal(sh.Position);
				var transform = Transform2D.Identity.Rotated(sh.Rotation);
				Vector2 half = sh.Size * 0.5f;

				Vector2[] pts =
				{
					localPos + transform * new Vector2(-half.X, -half.Y),
					localPos + transform * new Vector2(half.X, -half.Y),
					localPos + transform * new Vector2(half.X, half.Y),
					localPos + transform * new Vector2(-half.X, half.Y)
				};

				DrawColoredPolygon(pts, col);
			}

			// 5. 火星
			foreach (var s in _sparks)
			{
				float alpha = s.Lifetime / s.MaxLifetime;
				Color col = new(s.Color.R, s.Color.G, s.Color.B, alpha);

				Vector2 localPos = ToLocal(s.Position);
				Vector2 tail = localPos - (s.Velocity.Normalized() * s.Length);
				DrawLine(tail, localPos, col, 2.0f);
			}

			// 6. 绘制浮空伤害跳字
			foreach (var ft in _floatingTexts)
			{
				float alpha = ft.Lifetime / ft.MaxLifetime;
				Color textCol = new(ft.Color.R, ft.Color.G, ft.Color.B, alpha);
				DrawString(ThemeDB.FallbackFont, ToLocal(ft.Position), ft.Text, HorizontalAlignment.Center, -1, 16, textCol);
			}
		}
	}
}
