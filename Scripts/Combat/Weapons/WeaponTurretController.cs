using Godot;
using BreakerProtocol.Combat.Armor;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;

namespace BreakerProtocol.Combat.Weapons
{
	/// <summary>
	/// 战舰全武器发射终端与弹道发生器 (全面集成 MultiMeshInstance2D 弹幕合批管线)
	/// </summary>
	public class WeaponTurretController
	{
		private readonly ShipEntity _ship;

		public WeaponTurretController(ShipEntity ship)
		{
			_ship = ship;
			_ship.Pulses.OnWeaponFired += HandleWeaponFired;
		}

		private void HandleWeaponFired(string weaponModuleInstanceId, PulsePacket packet)
		{
			ModuleInstance? targetModule = null;
			foreach (var m in _ship.Grid.Modules)
			{
				if (m.InstanceId == weaponModuleInstanceId)
				{
					targetModule = m;
					break;
				}
			}

			if (targetModule == null || targetModule.IsDestroyed) return;

			Vector2I size = targetModule.GetRotatedSize();
			Vector2 localMuzzleGrid = new(
				targetModule.GridPosition.X + size.X * 0.5f,
				targetModule.GridPosition.Y
			);
			Vector2 localMuzzlePixels = GlobalMetrics.MetersToPixels(localMuzzleGrid);
			Vector2 worldMuzzlePos = _ship.GlobalTransform * localMuzzlePixels;

			Vector2 fireDirection = -_ship.GlobalTransform.Y.Normalized();

			var props = targetModule.Definition.Properties;
			float baseDamage = props.TryGetProperty("baseDamage", out var bd) ? bd.GetSingle() : 50.0f;
			float baseSpeed = props.TryGetProperty("baseSpeed", out var bs) ? bs.GetSingle() : 180.0f;
			int basePierce = props.TryGetProperty("basePierce", out var bp) ? bp.GetInt32() : 0;

			float finalDamage = baseDamage * packet.DamageMultiplier;
			float finalSpeed = baseSpeed * packet.SpeedMultiplier;
			int finalPierce = basePierce + packet.BonusPierce;
			int splitCount = Mathf.Max(1, packet.SplitCount);

			bool isLaser = targetModule.Definition.Id.Contains("laser") || targetModule.Definition.Id.Contains("lance");

			if (isLaser)
			{
				FireLaserBeam(worldMuzzlePos, fireDirection, finalDamage, packet.Elements, splitCount);
			}
			else
			{
				FireProjectilesBatched(worldMuzzlePos, fireDirection, finalSpeed, finalDamage, finalPierce, packet.Elements, splitCount);
			}
		}

		private void FireProjectilesBatched(Vector2 muzzlePos, Vector2 baseDir, float speedMeters, float damage, int pierce, ElementFlags elements, int splitCount)
		{
			float speedPixels = GlobalMetrics.MetersToPixels(speedMeters);
			float spreadAngleTotal = splitCount > 1 ? Mathf.DegToRad(20.0f) : 0.0f;
			float angleStep = splitCount > 1 ? spreadAngleTotal / (splitCount - 1) : 0.0f;
			float startAngle = -spreadAngleTotal * 0.5f;

			for (int i = 0; i < splitCount; i++)
			{
				float currentAngleOffset = startAngle + (i * angleStep);
				Vector2 shotDir = baseDir.Rotated(currentAngleOffset);

				// 优先使用 MultiMeshInstance2D 弹幕合批投射
				if (BulletManager.Instance != null)
				{
					BulletManager.Instance.SpawnBullet(
						muzzlePos,
						shotDir * speedPixels,
						damage,
						pierce,
						elements,
						_ship,
						lifeTime: 2.5f,
						size: 1.0f
					);
				}
				else
				{
					// 兜底降级方案
					var projectile = new ProjectileEntity
					{
						GlobalPosition = muzzlePos,
						AttackerShip = _ship,
						Velocity = shotDir * speedPixels,
						BaseDamage = damage,
						RemainingPierce = pierce,
						Elements = elements,
						RemainingLifeTime = 2.5f
					};
					_ship.GetTree().CurrentScene.AddChild(projectile);
				}
			}
		}

		private void FireLaserBeam(Vector2 muzzlePos, Vector2 dir, float damage, ElementFlags elements, int splitCount)
		{
			float maxRangePixels = 1200.0f;
			var spaceState = _ship.GetWorld2D().DirectSpaceState;

			float spreadAngleTotal = splitCount > 1 ? Mathf.DegToRad(16.0f) : 0.0f;
			float angleStep = spreadAngleTotal / (splitCount > 1 ? (splitCount - 1) : 1);
			float startAngle = -spreadAngleTotal * 0.5f;

			for (int i = 0; i < splitCount; i++)
			{
				float currentAngleOffset = startAngle + (i * angleStep);
				Vector2 beamDir = dir.Rotated(currentAngleOffset);
				Vector2 targetPos = muzzlePos + beamDir * maxRangePixels;

				var query = PhysicsRayQueryParameters2D.Create(muzzlePos, targetPos);
				query.CollideWithAreas = true;
				query.CollideWithBodies = true;
				query.Exclude = new Godot.Collections.Array<Rid> { _ship.GetRid() };

				var result = spaceState.IntersectRay(query);
				Vector2 hitPoint = targetPos;

				if (result.Count > 0)
				{
					hitPoint = result["position"].AsVector2();
					var collider = result["collider"].As<Node2D>();
					Vector2 hitNormal = result["normal"].AsVector2();

					if (collider is ShipEntity targetShip)
					{
						Vector2 insideHitPoint = hitPoint - (hitNormal * 1.5f);
						Vector2 localPixels = targetShip.ToLocal(insideHitPoint);
						Vector2 localGrid = GlobalMetrics.PixelsToMeters(localPixels);
						Vector2I gridPos = new(Mathf.FloorToInt(localGrid.X), Mathf.FloorToInt(localGrid.Y));

						var hitModule = targetShip.Grid.GetModuleAt(gridPos);
						if (hitModule != null)
						{
							var outcome = ArmorResolver.ResolveImpact(
								hitModule,
								beamDir * 1000.0f,
								hitNormal,
								damage,
								elements,
								0
							);
							targetShip.OnModuleDamaged(hitModule, outcome.ActualDamageDealt);
						}
					}
					else if (collider is TargetDummy dummy)
					{
						dummy.TakeDamage(damage, elements);
					}
				}

				Color beamColor = elements.HasFlag(ElementFlags.Cryo) 
					? new Color(0.2f, 0.9f, 1.0f, 1.0f) 
					: new Color(0.4f, 0.8f, 1.0f, 1.0f);

				var beamVisual = new LaserBeamVisual
				{
					StartPoint = muzzlePos,
					EndPoint = hitPoint,
					BeamColor = beamColor,
					Duration = 0.10f,
					BeamWidth = 5.0f
				};

				_ship.GetTree().CurrentScene.AddChild(beamVisual);
			}
		}
	}
}
