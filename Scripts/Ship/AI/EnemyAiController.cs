using System.Collections.Generic;
using Godot;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship.Abilities;
using BreakerProtocol.Environment.Hazards;

namespace BreakerProtocol.Ship.AI
{
	public enum AiArchetype
	{
		Brawler,    // 重装肉搏 (正面压制 180~220px)
		KiteSniper, // 风筝狙击 (保持 450~550px 射程拉扯)
		Swarm       // 蜂群突袭 (包抄切后，残血自爆)
	}

	public enum AiState
	{
		Approach,   // 编队阵型平稳靠位集结
		Engage,     // 优势交火
		KiteRetreat,// 风筝后撤
		Flank,      // 切线包抄
		Disengage,  // 物理反粘连滑开脱困
		Stationary, // 引擎损毁原地炮台
		Flee,       // 武器全毁紧急撤离
		Kamikaze,   // 死士绝命撞击
		Return      // 超出战区折返
	}

	/// <summary>
	/// 敌机战术行为树 AI 决策中枢 (集成雷诺兹 Arrive 速度闭环与精准编队跟随)
	/// </summary>
	public class EnemyAiController
	{
		private readonly ShipEntity _ship;

		public AiArchetype Archetype { get; set; } = AiArchetype.Brawler;
		public AiState CurrentState { get; private set; } = AiState.Approach;
		public Node2D? CurrentTarget { get; set; }

		public Vector2 MoveInput { get; private set; } = Vector2.Zero;
		public Vector2 AimTargetWorldPos { get; private set; } = Vector2.Zero;
		public bool IsBoosting { get; private set; } = false;
		public bool IsDrift { get; private set; } = false;
		public bool WantsToFire { get; private set; } = false;

		private bool _hasFunctionalWeapons = true;
		private bool _hasForwardEngines = true;
		private bool _isHeavyShip = false;
		private float _hpRatio = 1.0f;

		private float _weaveTime = 0.0f;
		private float _fireCooldown = 0.0f;
		private float _swarmAngleOffset = 0.0f;
		private float _ablativeAbilityCooldown = 0.0f;

		public const float MinSeparationDistance = 140.0f;

		public EnemyAiController(ShipEntity ship, AiArchetype archetype = AiArchetype.Brawler)
		{
			_ship = ship;
			Archetype = archetype;
			_weaveTime = (float)GD.RandRange(0.0, 10.0);
			_swarmAngleOffset = GD.Randf() > 0.5f ? Mathf.DegToRad(40.0f) : Mathf.DegToRad(-40.0f);
		}

		public void Update(double dt)
		{
			float fdt = (float)dt;
			_weaveTime += fdt * 3.0f;
			if (_ablativeAbilityCooldown > 0.0f) _ablativeAbilityCooldown -= fdt;

			InspectShipCapabilities();

			if (CurrentTarget == null || !GodotObject.IsInstanceValid(CurrentTarget))
			{
				CurrentTarget = _ship.GetTree().GetFirstNodeInGroup("Player") as Node2D;
			}

			if (CurrentTarget == null || !GodotObject.IsInstanceValid(CurrentTarget))
			{
				MoveInput = Vector2.Zero;
				WantsToFire = false;
				return;
			}

			EvaluateDynamicStateTransitions();
			ExecuteMovementSteering(fdt);
			ExecuteAimAndFiring(fdt);
			CheckEmergencyAblativeDetonation();
		}

		private void InspectShipCapabilities()
		{
			int activeWeapons = 0;
			int activeThrusters = 0;
			float currentHp = 0.0f;
			float maxHp = 0.0f;

			foreach (var mod in _ship.Grid.Modules)
			{
				if (!mod.IsDestroyed)
				{
					currentHp += mod.CurrentHp;
					maxHp += mod.MaxHp;

					if (mod.Definition.Category == "Weapon") activeWeapons++;
					if (mod.Definition.Category == "Thruster") activeThrusters++;
				}
			}

			_hasFunctionalWeapons = activeWeapons > 0;
			_hasForwardEngines = activeThrusters > 0;
			_hpRatio = maxHp > 0.0f ? (currentHp / maxHp) : 0.0f;

			float mass = _ship.Mass > 0 ? _ship.Mass : _ship.PhysicsData.TotalMass;
			_isHeavyShip = mass > 140.0f;
		}

		private void EvaluateDynamicStateTransitions()
		{
			float distToPlayer = _ship.GlobalPosition.DistanceTo(CurrentTarget!.GlobalPosition);

			if (!_hasFunctionalWeapons)
			{
				CurrentState = (Archetype == AiArchetype.KiteSniper) ? AiState.Flee : AiState.Kamikaze;
				return;
			}

			if (!_hasForwardEngines)
			{
				CurrentState = AiState.Stationary;
				return;
			}

			if (distToPlayer < MinSeparationDistance && CurrentState != AiState.Kamikaze)
			{
				CurrentState = AiState.Disengage;
				return;
			}

			if (distToPlayer > 950.0f)
			{
				CurrentState = AiState.Return;
				return;
			}

			// 如果属于编队僚机（且长机存活），始终保持编队协同模式，绝不中途擅自脱节！
			bool isFormationFollower = FleetFormationManager.Instance != null &&
									   FleetFormationManager.Instance.FleetLeader != null &&
									   GodotObject.IsInstanceValid(FleetFormationManager.Instance.FleetLeader) &&
									   _ship != FleetFormationManager.Instance.FleetLeader;

			if (isFormationFollower)
			{
				CurrentState = AiState.Approach;
				return;
			}

			// 独立敌舰 / 长机状态机流转
			switch (Archetype)
			{
				case AiArchetype.Brawler:
					CurrentState = distToPlayer > 220.0f ? AiState.Approach : AiState.Engage;
					break;

				case AiArchetype.KiteSniper:
					if (distToPlayer < 380.0f) CurrentState = AiState.KiteRetreat;
					else if (distToPlayer > 560.0f) CurrentState = AiState.Approach;
					else CurrentState = AiState.Engage;
					break;

				case AiArchetype.Swarm:
					if (_hpRatio < 0.35f)
					{
						CurrentState = AiState.Kamikaze;
					}
					else
					{
						CurrentState = distToPlayer > 260.0f ? AiState.Approach : AiState.Flank;
					}
					break;
			}
		}

		private void ExecuteMovementSteering(float dt)
		{
			Vector2 targetPos = CurrentTarget!.GlobalPosition;
			bool isNavigatingToFormation = false;
			Vector2 leaderVelocity = Vector2.Zero;

			// 僚机获取阵型幽灵锚点
			if (FleetFormationManager.Instance != null &&
				FleetFormationManager.Instance.FleetLeader != null &&
				GodotObject.IsInstanceValid(FleetFormationManager.Instance.FleetLeader) &&
				_ship != FleetFormationManager.Instance.FleetLeader)
			{
				targetPos = FleetFormationManager.Instance.GetFormationOffsetWorldPos(_ship, CurrentTarget);
				isNavigatingToFormation = true;
				leaderVelocity = FleetFormationManager.Instance.FleetLeader.LinearVelocity;
			}

			Vector2 toTarget = targetPos - _ship.GlobalPosition;
			float distToTarget = toTarget.Length();

			Vector2 forward = -_ship.Transform.Y;
			Vector2 right = _ship.Transform.X;

			Vector2 localInput = Vector2.Zero;
			bool boost = false;

			switch (CurrentState)
			{
				case AiState.Approach:
				case AiState.Engage:
					if (isNavigatingToFormation)
					{
						// ============================================================
						// 核心算法：雷诺兹 Arrive 闭环速度解算 + 匹配长机航速
						// ============================================================
						const float ArriveRadius = 160.0f;
						const float MaxFormationSpeed = 300.0f;

						Vector2 desiredRelVelocity;
						if (distToTarget > ArriveRadius)
						{
							desiredRelVelocity = toTarget.Normalized() * MaxFormationSpeed;
							boost = distToTarget > 280.0f;
						}
						else
						{
							// 减速区：随着距离缩短，期望相对速度线性减为 0
							float speedFraction = distToTarget / ArriveRadius;
							desiredRelVelocity = toTarget.Normalized() * (MaxFormationSpeed * speedFraction);
							boost = false;
						}

						// 合成期望总速度（相对期望速度 + 长机速度）
						Vector2 desiredTotalVelocity = desiredRelVelocity + leaderVelocity;
						Vector2 velocityError = desiredTotalVelocity - _ship.LinearVelocity;

						// 将速度误差投影到飞船局部坐标系
						float forwardError = forward.Dot(velocityError);
						float rightError = right.Dot(velocityError);

						// 前后推力计算（严格修正 Clamp 逻辑）
						if (forwardError > 15.0f)
						{
							localInput.Y = -Mathf.Clamp(forwardError / 80.0f, 0.2f, 1.0f); // 前进
						}
						else if (forwardError < -15.0f)
						{
							localInput.Y = Mathf.Clamp(-forwardError / 80.0f, 0.2f, 1.0f);  // 反推刹车！
						}

						// 左右 RCS 侧推消除横向漂移
						if (Mathf.Abs(rightError) > 10.0f)
						{
							localInput.X = Mathf.Clamp(rightError / 50.0f, -1.0f, 1.0f);
						}
					}
					else
					{
						// 长机（或独立行动机）：稳健正面推进
						if (Archetype == AiArchetype.Brawler)
						{
							float distPlayer = _ship.GlobalPosition.DistanceTo(CurrentTarget!.GlobalPosition);
							localInput.Y = distPlayer > 220.0f ? -1.0f : (distPlayer < 160.0f ? 0.6f : 0.0f);
							localInput.X = Mathf.Sin(_weaveTime * 0.5f) * 0.3f;
							boost = distPlayer > 350.0f;
						}
						else if (Archetype == AiArchetype.KiteSniper)
						{
							float distPlayer = _ship.GlobalPosition.DistanceTo(CurrentTarget!.GlobalPosition);
							localInput.X = Mathf.Sin(_weaveTime) > 0 ? 0.8f : -0.8f;
							localInput.Y = distPlayer > 520.0f ? -0.5f : (distPlayer < 400.0f ? 0.8f : 0.0f);
						}
						else // Swarm
						{
							localInput.Y = -1.0f;
							localInput.X = _swarmAngleOffset > 0 ? 0.6f : -0.6f;
							boost = true;
						}
					}
					break;

				case AiState.Disengage:
					localInput.Y = 1.0f;
					localInput.X = Mathf.Sin(_weaveTime * 2.0f) > 0 ? 1.0f : -1.0f;
					boost = true;
					break;

				case AiState.KiteRetreat:
					localInput.Y = 1.0f;
					localInput.X = Mathf.Cos(_weaveTime * 0.8f) * 0.6f;
					boost = distToTarget < 260.0f;
					break;

				case AiState.Flank:
					localInput.Y = -1.0f;
					localInput.X = _swarmAngleOffset > 0 ? 0.7f : -0.7f;
					boost = true;
					break;

				case AiState.Kamikaze:
					localInput.Y = -1.0f;
					boost = true;

					if (distToTarget < 48.0f)
					{
						TriggerSelfDestruct();
						return;
					}
					break;

				case AiState.Flee:
					localInput.Y = 1.0f;
					boost = true;
					break;

				case AiState.Stationary:
					localInput = Vector2.Zero;
					break;
			}

			MoveInput = localInput;
			IsBoosting = boost;
			IsDrift = false;
		}

		private void ExecuteAimAndFiring(float dt)
		{
			Vector2 targetPos = CurrentTarget!.GlobalPosition;
			Vector2 targetVel = CurrentTarget is RigidBody2D rb ? rb.LinearVelocity : Vector2.Zero;

			float estimatedBulletSpeed = 650.0f;
			Vector2 predictedAimPos = PredictLeadPosition(_ship.GlobalPosition, targetPos, targetVel, estimatedBulletSpeed);

			// ============================================================
			// 瞄准朝向决策：
			// 僚机离锚点较远 (>80px) 时，机头对准锚点全速推进；
			// 接近就位后 (<=80px)，机头精准转回锁定玩家！
			// ============================================================
			if (FleetFormationManager.Instance != null &&
				FleetFormationManager.Instance.FleetLeader != null &&
				GodotObject.IsInstanceValid(FleetFormationManager.Instance.FleetLeader) &&
				_ship != FleetFormationManager.Instance.FleetLeader)
			{
				Vector2 anchorPos = FleetFormationManager.Instance.GetFormationOffsetWorldPos(_ship, CurrentTarget);
				float distToAnchor = _ship.GlobalPosition.DistanceTo(anchorPos);

				if (distToAnchor > 80.0f)
				{
					AimTargetWorldPos = anchorPos;
				}
				else
				{
					AimTargetWorldPos = predictedAimPos;
				}
			}
			else
			{
				AimTargetWorldPos = _isHeavyShip ? targetPos : predictedAimPos;
			}

			if (!_hasFunctionalWeapons)
			{
				WantsToFire = false;
				return;
			}

			Vector2 forward = -_ship.Transform.Y;
			Vector2 toAim = (predictedAimPos - _ship.GlobalPosition).Normalized();
			float forwardDotAim = forward.Dot(toAim);

			float dist = _ship.GlobalPosition.DistanceTo(targetPos);
			bool inRange = (Archetype == AiArchetype.KiteSniper && dist < 680.0f) ||
						   (Archetype == AiArchetype.Brawler && dist < 450.0f) ||
						   (Archetype == AiArchetype.Swarm && dist < 320.0f) ||
						   (CurrentState == AiState.Stationary && dist < 600.0f);

			bool hasFireToken = true;
			if (FleetFormationManager.Instance != null)
			{
				hasFireToken = FleetFormationManager.Instance.RequestFirePermission(_ship);
			}

			WantsToFire = forwardDotAim > 0.86f && inRange && hasFireToken;

			if (WantsToFire)
			{
				_fireCooldown -= dt;
				if (_fireCooldown <= 0.0f)
				{
					_fireCooldown = Archetype == AiArchetype.Brawler ? 0.20f : (Archetype == AiArchetype.KiteSniper ? 0.55f : 0.16f);
					foreach (var weaponId in _ship.Pulses.WeaponBuffers.Keys)
					{
						_ship.Pulses.TriggerWeaponFire(weaponId, out _);
					}
				}
			}
		}

		private void CheckEmergencyAblativeDetonation()
		{
			if (_ablativeAbilityCooldown > 0.0f || _ship.AblativeDetonation == null) return;

			float dist = _ship.GlobalPosition.DistanceTo(CurrentTarget!.GlobalPosition);
			if (_hpRatio < 0.45f && dist < 180.0f)
			{
				_ablativeAbilityCooldown = 5.0f;
				var section = GD.Randf() > 0.5f ? DetonationSection.LeftWing : DetonationSection.RightWing;
				_ship.AblativeDetonation.TriggerDetonation(section);
				VfxManager.Instance?.SpawnFloatingText(_ship.GlobalPosition, "💥 敌机绝境过载爆甲脱困！", Colors.Yellow);
			}
		}

		private Vector2 PredictLeadPosition(Vector2 shooterPos, Vector2 targetPos, Vector2 targetVel, float bulletSpeed)
		{
			float dist = shooterPos.DistanceTo(targetPos);
			if (bulletSpeed <= 10.0f) return targetPos;

			float timeToTarget = dist / bulletSpeed;
			return targetPos + (targetVel * timeToTarget);
		}

		private void TriggerSelfDestruct()
		{
			VfxManager.Instance?.SpawnAcidPool(_ship.GlobalPosition, radius: 55.0f, duration: 5.0f);
			VfxManager.Instance?.SpawnModuleExplosion(_ship.GlobalPosition, new Vector2(80, 80), Colors.LimeGreen, shardCount: 24);
			VfxManager.Instance?.SpawnFloatingText(_ship.GlobalPosition, "☣️ 敌舰绝命撞角自爆！", Colors.LimeGreen);
			JuiceManager.Instance?.TriggerExplosionJuice(_ship.GlobalPosition, intensity: 1.0f);

			if (CurrentTarget is ShipEntity targetShip)
			{
				var mod = targetShip.Grid.Modules;
				foreach (var m in mod)
				{
					if (!m.IsDestroyed)
					{
						m.CurrentHp = Mathf.Max(0.0f, m.CurrentHp - 250.0f);
						targetShip.OnModuleDamaged(m, 250.0f);
						break;
					}
				}
			}

			_ship.QueueFree();
		}
	}
}
