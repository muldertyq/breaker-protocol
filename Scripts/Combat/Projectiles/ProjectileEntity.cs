using System.Collections.Generic;
using Godot;
using BreakerProtocol.Combat.Armor;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Trauma;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;

namespace BreakerProtocol.Combat.Projectiles
{
	/// <summary>
	/// 高速物理子弹实体 (集成 2D DDA 舰内射线步进穿透创伤)
	/// </summary>
	public partial class ProjectileEntity : Node2D
	{
		public Node2D? AttackerShip { get; set; }
		public Vector2 Velocity { get; set; }
		public float BaseDamage { get; set; } = 50.0f;
		public int RemainingPierce { get; set; } = 0;
		public ElementFlags Elements { get; set; } = ElementFlags.None;
		public float RemainingLifeTime { get; set; } = 3.0f;

		private readonly List<Vector2> _trailPoints = new();
		private const int MaxTrailPoints = 8;
		private float _trailRecordTimer = 0.0f;
		private float _bounceCooldown = 0.0f;

		public override void _Ready()
		{
			ZIndex = 10;
			AddToGroup("Projectile");
		}

		public override void _PhysicsProcess(double delta)
		{
			float dt = (float)delta;
			RemainingLifeTime -= dt;
			if (_bounceCooldown > 0.0f) _bounceCooldown -= dt;

			if (RemainingLifeTime <= 0.0f)
			{
				QueueFree();
				return;
			}

			_trailRecordTimer += dt;
			if (_trailRecordTimer >= 0.016f)
			{
				_trailRecordTimer = 0.0f;
				_trailPoints.Insert(0, GlobalPosition);
				if (_trailPoints.Count > MaxTrailPoints)
				{
					_trailPoints.RemoveAt(_trailPoints.Count - 1);
				}
			}

			Vector2 stepMovement = Velocity * dt;
			Vector2 nextPos = GlobalPosition + stepMovement;

			var spaceState = GetWorld2D().DirectSpaceState;
			var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, nextPos);
			query.CollideWithAreas = true;
			query.CollideWithBodies = true;

			if (AttackerShip is RigidBody2D rb)
			{
				query.Exclude = new Godot.Collections.Array<Rid> { rb.GetRid() };
			}

			var result = spaceState.IntersectRay(query);
			if (result.Count > 0)
			{
				var hitCollider = result["collider"].As<Node2D>();
				Vector2 hitPoint = result["position"].AsVector2();
				Vector2 hitNormal = result["normal"].AsVector2();

				bool shouldDestroy = HandleHit(hitCollider, hitPoint, hitNormal);
				if (shouldDestroy)
				{
					QueueFree();
					return;
				}
			}

			GlobalPosition = nextPos;
			QueueRedraw();
		}

		private bool HandleHit(Node2D target, Vector2 hitPoint, Vector2 hitNormal)
		{
			if (Elements != ElementFlags.None)
			{
				ElementalSynthesisMatrix.ApplyHit(target, Elements, BaseDamage, out _);
			}

			if (target is ShipEntity shipTarget)
			{
				if (_bounceCooldown > 0.0f) return false;

				// 1. 计算受击网格起始点
				Vector2 insideHitPoint = hitPoint - (hitNormal * 1.5f);
				Vector2 localPixels = shipTarget.ToLocal(insideHitPoint);
				Vector2 localGrid = GlobalMetrics.PixelsToMeters(localPixels);
				Vector2I gridPos = new(Mathf.FloorToInt(localGrid.X), Mathf.FloorToInt(localGrid.Y));

				var hitModule = shipTarget.Grid.GetModuleAt(gridPos);
				if (hitModule != null)
				{
					// 2. 外层跳弹判定
					var outcome = ArmorResolver.ResolveImpact(
						hitModule,
						Velocity,
						hitNormal,
						BaseDamage,
						Elements,
						RemainingPierce
					);

					shipTarget.OnModuleDamaged(hitModule, outcome.ActualDamageDealt);

					if (outcome.IsRicochet)
					{
						Velocity = outcome.ReflectedVelocity;
						_bounceCooldown = 0.06f;
						_trailPoints.Clear();

						VfxManager.Instance?.SpawnImpactSparks(hitPoint, outcome.ReflectedVelocity.Normalized(), Colors.Yellow, isRicochet: true);
						JuiceManager.Instance?.TriggerHitstop(0.035f, 0.06f);
						JuiceManager.Instance?.AddCameraTrauma(0.25f);
						return false;
					}

					// ============================================================
					// 核心关键：击穿外壳！激活 2D DDA 舰内射线步进穿透创伤！
					// ============================================================
					VfxManager.Instance?.SpawnImpactSparks(hitPoint, hitNormal, GetElementGlowColor(), isRicochet: false);
					JuiceManager.Instance?.TriggerHitstop(0.045f, 0.05f);
					JuiceManager.Instance?.AddCameraTrauma(0.35f);
					JuiceManager.Instance?.ApplyDirectionalKick(Velocity.Normalized() * 8.0f);

					// 计算子弹在飞船局部网格中的行进方向
					Vector2 localDir = shipTarget.Transform.AffineInverse().BasisXform(Velocity).Normalized();

					float remainingDamage = BaseDamage * 0.8f;
					int curPierce = RemainingPierce;

					// 启动 2D DDA 遍历
					var marchResult = InternalTraumaRaymarcher.MarchThroughShip(
						shipTarget,
						localGrid,
						localDir,
						ref remainingDamage,
						ref curPierce,
						Elements
					);

					BaseDamage = remainingDamage;
					RemainingPierce = curPierce;

					if (RemainingPierce < 0 || !marchResult.FullyPenetrated)
					{
						return true; // 穿深耗尽，子弹停留在舰内销毁
					}

					return false; // 彻底打穿整舰，继续飞向深空
				}
			}
			else if (target is TargetDummy dummy)
			{
				dummy.TakeDamage(BaseDamage, Elements);
				VfxManager.Instance?.SpawnImpactSparks(hitPoint, hitNormal, GetElementGlowColor(), isRicochet: false);
				JuiceManager.Instance?.TriggerHitstop(0.030f, 0.08f);
				JuiceManager.Instance?.AddCameraTrauma(0.20f);
				return true;
			}

			return true;
		}

		public override void _Draw()
		{
			Color glowColor = GetElementGlowColor();

			if (_trailPoints.Count > 1)
			{
				for (int i = 0; i < _trailPoints.Count - 1; i++)
				{
					Vector2 p1 = ToLocal(_trailPoints[i]);
					Vector2 p2 = ToLocal(_trailPoints[i + 1]);
					float alpha = 1.0f - ((float)i / _trailPoints.Count);
					float width = Mathf.Lerp(1.5f, 5.0f, alpha);

					Color trailColor = new(glowColor.R, glowColor.G, glowColor.B, alpha * 0.7f);
					DrawLine(p1, p2, trailColor, width);
				}
			}

			DrawCircle(Vector2.Zero, 4.0f, glowColor);
			DrawCircle(Vector2.Zero, 2.0f, Colors.White);
		}

		private Color GetElementGlowColor()
		{
			if (Elements.HasFlag(ElementFlags.Thermal) && Elements.HasFlag(ElementFlags.Cryo)) return new Color(1.0f, 0.5f, 1.0f);
			if (Elements.HasFlag(ElementFlags.Thermal) && Elements.HasFlag(ElementFlags.Acid)) return new Color(1.0f, 0.8f, 0.1f);
			if (Elements.HasFlag(ElementFlags.Cryo) && Elements.HasFlag(ElementFlags.Void)) return new Color(0.1f, 1.0f, 1.0f);
			if (Elements.HasFlag(ElementFlags.Acid) && Elements.HasFlag(ElementFlags.Void)) return new Color(0.6f, 0.1f, 0.9f);

			if (Elements.HasFlag(ElementFlags.Cryo)) return new Color(0.2f, 0.9f, 1.0f, 1.0f);
			if (Elements.HasFlag(ElementFlags.Thermal)) return new Color(1.0f, 0.4f, 0.1f, 1.0f);
			if (Elements.HasFlag(ElementFlags.Acid)) return new Color(0.3f, 1.0f, 0.3f, 1.0f);
			if (Elements.HasFlag(ElementFlags.Void)) return new Color(0.8f, 0.3f, 1.0f, 1.0f);
			return new Color(1.0f, 0.8f, 0.2f, 1.0f);
		}
	}
}
