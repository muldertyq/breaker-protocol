using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Combat.Armor;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Trauma;
using BreakerProtocol.Environment.Asteroids;
using BreakerProtocol.Environment.Hazards;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;

namespace BreakerProtocol.Combat.Projectiles
{
	/// <summary>
	/// 单颗轻量合批子弹运行时数据 (紧凑值类型结构体，零 GC 分配)
	/// </summary>
	public struct BatchedBullet
	{
		public bool IsActive;
		public Vector2 Position;
		public Vector2 Velocity;
		public float BaseDamage;
		public int RemainingPierce;
		public ElementFlags Elements;
		public float RemainingLifeTime;
		public Color GlowColor;
		public Node2D? AttackerShip;
		public float Size;
	}

	/// <summary>
	/// MultiMeshInstance2D 弹幕合批与引力透镜偏转中枢 (规范 07 / TASK-24 / TASK-26)
	/// </summary>
	public partial class BulletManager : Node2D
	{
		public static BulletManager Instance { get; private set; } = null!;

		public const int MaxBulletCapacity = 10000;

		private MultiMeshInstance2D _multiMeshInstance = null!;
		private MultiMesh _multiMesh = null!;
		private readonly BatchedBullet[] _bulletPool = new BatchedBullet[MaxBulletCapacity];

		private int _activeBulletCount = 0;
		public int ActiveBulletCount => _activeBulletCount;

		// 空间粗筛：缓存战舰与大障碍物的空间包围盒
		private readonly List<(Node2D Target, Vector2 Center, float RadiusSq)> _activeObstacles = new();

		// 复用射线查询对象 (零 GC 堆分配)
		private PhysicsRayQueryParameters2D _cachedRayQuery = null!;

		public override void _Ready()
		{
			Instance = this;
			ZIndex = 9;

			InitMultiMesh();
			_cachedRayQuery = new PhysicsRayQueryParameters2D
			{
				CollideWithAreas = true,
				CollideWithBodies = true
			};
		}

		private void InitMultiMesh()
		{
			_multiMeshInstance = new MultiMeshInstance2D { Name = "BulletMultiMesh" };
			AddChild(_multiMeshInstance);

			_multiMesh = new MultiMesh
			{
				TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
				UseColors = true,
				InstanceCount = MaxBulletCapacity
			};

			var mesh = new ArrayMesh();
			var arrays = new Godot.Collections.Array();
			arrays.Resize((int)Mesh.ArrayType.Max);

			Vector2[] vertices = {
				new(-4, -2), new(4, -2), new(6, 0),
				new(4, 2), new(-4, 2), new(-6, 0)
			};
			int[] indices = { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5 };
			Color[] colors = { Colors.White, Colors.White, Colors.White, Colors.White, Colors.White, Colors.White };

			arrays[(int)Mesh.ArrayType.Vertex] = vertices;
			arrays[(int)Mesh.ArrayType.Index] = indices;
			arrays[(int)Mesh.ArrayType.Color] = colors;

			mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
			_multiMesh.Mesh = mesh;
			_multiMeshInstance.Multimesh = _multiMesh;

			var zeroTransform = new Transform2D(0.0f, Vector2.Zero);
			for (int i = 0; i < MaxBulletCapacity; i++)
			{
				_multiMesh.SetInstanceTransform2D(i, zeroTransform);
				_multiMesh.SetInstanceColor(i, Colors.Transparent);
			}
		}

		public bool SpawnBullet(
			Vector2 spawnWorldPos,
			Vector2 velocity,
			float damage,
			int pierce,
			ElementFlags elements,
			Node2D? attackerShip,
			float lifeTime = 2.5f,
			float size = 1.0f)
		{
			for (int i = 0; i < MaxBulletCapacity; i++)
			{
				if (!_bulletPool[i].IsActive)
				{
					_bulletPool[i].IsActive = true;
					_bulletPool[i].Position = spawnWorldPos;
					_bulletPool[i].Velocity = velocity;
					_bulletPool[i].BaseDamage = damage;
					_bulletPool[i].RemainingPierce = pierce;
					_bulletPool[i].Elements = elements;
					_bulletPool[i].RemainingLifeTime = lifeTime;
					_bulletPool[i].AttackerShip = attackerShip;
					_bulletPool[i].Size = size;
					_bulletPool[i].GlowColor = ResolveBulletColor(elements);

					_activeBulletCount++;
					return true;
				}
			}

			return false;
		}

		public override void _PhysicsProcess(double delta)
		{
			float dt = (float)delta;
			if (_activeBulletCount == 0) return;

			// 1. 更新空间粗筛目标群 (战舰 + 小行星)
			UpdateObstacleBroadphase();

			var spaceState = GetWorld2D().DirectSpaceState;
			bool hasEnvManager = SpaceEnvironmentManager.Instance != null;

			for (int i = 0; i < MaxBulletCapacity; i++)
			{
				if (!_bulletPool[i].IsActive) continue;

				_bulletPool[i].RemainingLifeTime -= dt;
				if (_bulletPool[i].RemainingLifeTime <= 0.0f)
				{
					DeactivateBullet(i);
					continue;
				}

				// ============================================================
				// 核心特色：弹道引力透镜弯折 (Gravitational Lensing)
				// ============================================================
				if (hasEnvManager)
				{
					Vector2 gravityAccel = SpaceEnvironmentManager.Instance.SampleTotalGravitationalAcceleration(_bulletPool[i].Position);
					if (gravityAccel != Vector2.Zero)
					{
						_bulletPool[i].Velocity += gravityAccel * dt;
					}
				}

				Vector2 currentPos = _bulletPool[i].Position;
				Vector2 step = _bulletPool[i].Velocity * dt;
				Vector2 nextPos = currentPos + step;

				// 2. 空间粗筛快速命中检测
				if (IsNearAnyObstacle(nextPos, _bulletPool[i].AttackerShip))
				{
					_cachedRayQuery.From = currentPos;
					_cachedRayQuery.To = nextPos;

					if (_bulletPool[i].AttackerShip is RigidBody2D rb)
					{
						_cachedRayQuery.Exclude = new Godot.Collections.Array<Rid> { rb.GetRid() };
					}
					else
					{
						_cachedRayQuery.Exclude = new Godot.Collections.Array<Rid>();
					}

					var hitResult = spaceState.IntersectRay(_cachedRayQuery);
					if (hitResult.Count > 0)
					{
						var hitCollider = hitResult["collider"].As<Node2D>();
						Vector2 hitPoint = hitResult["position"].AsVector2();
						Vector2 hitNormal = hitResult["normal"].AsVector2();

						bool shouldDestroy = HandleBatchedHit(ref _bulletPool[i], hitCollider, hitPoint, hitNormal);
						if (shouldDestroy)
						{
							DeactivateBullet(i);
							continue;
						}
					}
				}

				// 3. 推进物理位置并提交 GPU 变换
				_bulletPool[i].Position = nextPos;

				float angle = _bulletPool[i].Velocity.Angle();
				Transform2D xform = new(angle, _bulletPool[i].Position);
				xform = xform.Scaled(new Vector2(_bulletPool[i].Size, _bulletPool[i].Size));

				_multiMesh.SetInstanceTransform2D(i, xform);
				_multiMesh.SetInstanceColor(i, _bulletPool[i].GlowColor);
			}
		}

		private void UpdateObstacleBroadphase()
		{
			_activeObstacles.Clear();

			// 战舰
			var ships = GetTree().GetNodesInGroup("Ship");
			foreach (var node in ships)
			{
				if (node is ShipEntity ship && ship.Grid.ModuleCount > 0)
				{
					Vector2 centerWorld = ship.GlobalTransform * ship.PhysicsData.CenterOfMassPixels;
					float radius = Mathf.Max(60.0f, ship.PhysicsData.TotalMass * 0.45f);
					_activeObstacles.Add((ship, centerWorld, radius * radius));
				}
			}

			// 小行星
			var asteroids = GetTree().GetNodesInGroup("Asteroid");
			foreach (var node in asteroids)
			{
				if (node is AsteroidEntity asteroid)
				{
					float r = asteroid.RadiusPixels * 1.3f;
					_activeObstacles.Add((asteroid, asteroid.GlobalPosition, r * r));
				}
			}
		}

		private bool IsNearAnyObstacle(Vector2 pos, Node2D? attacker)
		{
			foreach (var (target, center, radiusSq) in _activeObstacles)
			{
				if (target == attacker) continue;
				if (pos.DistanceSquaredTo(center) <= radiusSq)
				{
					return true;
				}
			}
			return false;
		}

		private bool HandleBatchedHit(ref BatchedBullet bullet, Node2D target, Vector2 hitPoint, Vector2 hitNormal)
		{
			if (bullet.Elements != ElementFlags.None)
			{
				ElementalSynthesisMatrix.ApplyHit(target, bullet.Elements, bullet.BaseDamage, out _);
			}

			if (target is AsteroidEntity asteroid)
			{
				asteroid.TakeDamage(bullet.BaseDamage, bullet.Elements, bullet.Velocity.Normalized(), hitPoint);
				VfxManager.Instance?.SpawnImpactSparks(hitPoint, hitNormal, bullet.GlowColor, isRicochet: false);
				JuiceManager.Instance?.TriggerHitstop(0.015f, 0.03f);
				return true;
			}

			if (target is ShipEntity shipTarget)
			{
				Vector2 insideHitPoint = hitPoint - (hitNormal * 1.5f);
				Vector2 localPixels = shipTarget.ToLocal(insideHitPoint);
				Vector2 localGrid = GlobalMetrics.PixelsToMeters(localPixels);
				Vector2I gridPos = new(Mathf.FloorToInt(localGrid.X), Mathf.FloorToInt(localGrid.Y));

				var hitModule = shipTarget.Grid.GetModuleAt(gridPos);
				if (hitModule != null)
				{
					var outcome = ArmorResolver.ResolveImpact(
						hitModule,
						bullet.Velocity,
						hitNormal,
						bullet.BaseDamage,
						bullet.Elements,
						bullet.RemainingPierce
					);

					shipTarget.OnModuleDamaged(hitModule, outcome.ActualDamageDealt);

					if (outcome.IsRicochet)
					{
						bullet.Velocity = outcome.ReflectedVelocity;
						VfxManager.Instance?.SpawnImpactSparks(hitPoint, outcome.ReflectedVelocity.Normalized(), Colors.Yellow, isRicochet: true);
						JuiceManager.Instance?.TriggerHitstop(0.020f, 0.04f);
						return false;
					}

					VfxManager.Instance?.SpawnImpactSparks(hitPoint, hitNormal, bullet.GlowColor, isRicochet: false);
					JuiceManager.Instance?.TriggerHitstop(0.030f, 0.04f);
					JuiceManager.Instance?.AddCameraTrauma(0.20f);

					Vector2 localDir = shipTarget.Transform.AffineInverse().BasisXform(bullet.Velocity).Normalized();
					float remainingDamage = bullet.BaseDamage * 0.8f;
					int curPierce = bullet.RemainingPierce;

					var marchResult = InternalTraumaRaymarcher.MarchThroughShip(
						shipTarget,
						localGrid,
						localDir,
						ref remainingDamage,
						ref curPierce,
						bullet.Elements,
						hitModule.InstanceId
					);

					bullet.BaseDamage = remainingDamage;
					bullet.RemainingPierce = curPierce;

					if (bullet.RemainingPierce < 0 || !marchResult.FullyPenetrated)
					{
						return true;
					}

					return false;
				}
			}
			else if (target is TargetDummy dummy)
			{
				dummy.TakeDamage(bullet.BaseDamage, bullet.Elements);
				VfxManager.Instance?.SpawnImpactSparks(hitPoint, hitNormal, bullet.GlowColor, isRicochet: false);
				return true;
			}

			return true;
		}

		private void DeactivateBullet(int index)
		{
			_bulletPool[index].IsActive = false;
			_multiMesh.SetInstanceTransform2D(index, new Transform2D(0.0f, Vector2.Zero));
			_multiMesh.SetInstanceColor(index, Colors.Transparent);
			_activeBulletCount = Mathf.Max(0, _activeBulletCount - 1);
		}

		public void ClearAll()
		{
			for (int i = 0; i < MaxBulletCapacity; i++)
			{
				if (_bulletPool[i].IsActive)
				{
					DeactivateBullet(i);
				}
			}
			_activeBulletCount = 0;
		}

		private Color ResolveBulletColor(ElementFlags elements)
		{
			if (elements.HasFlag(ElementFlags.Thermal) && elements.HasFlag(ElementFlags.Cryo)) return new Color(1.0f, 0.5f, 1.0f);
			if (elements.HasFlag(ElementFlags.Thermal) && elements.HasFlag(ElementFlags.Acid)) return new Color(1.0f, 0.8f, 0.1f);
			if (elements.HasFlag(ElementFlags.Cryo) && elements.HasFlag(ElementFlags.Void)) return new Color(0.1f, 1.0f, 1.0f);
			if (elements.HasFlag(ElementFlags.Acid) && elements.HasFlag(ElementFlags.Void)) return new Color(0.7f, 0.2f, 1.0f);

			if (elements.HasFlag(ElementFlags.Cryo)) return new Color(0.2f, 0.9f, 1.0f);
			if (elements.HasFlag(ElementFlags.Thermal)) return new Color(1.0f, 0.4f, 0.1f);
			if (elements.HasFlag(ElementFlags.Acid)) return new Color(0.3f, 1.0f, 0.3f);
			if (elements.HasFlag(ElementFlags.Void)) return new Color(0.8f, 0.3f, 1.0f);
			return new Color(1.0f, 0.85f, 0.2f);
		}
	}
}
