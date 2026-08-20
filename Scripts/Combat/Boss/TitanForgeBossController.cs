using System.Collections.Generic;
using Godot;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.Ship.Abilities;
using BreakerProtocol.Ship.AI;

namespace BreakerProtocol.Combat.Boss
{
	public enum BossPhase
	{
		Phase1_Fortress,          // 阶段一：防御要塞 (重装甲外壳 + 双舷加特林火力压制)
		Phase2_EscortSeparation,  // 阶段二：子舰分离 (爆甲生成2艘浮游炮舰 + 核心暴露旋转弹幕)
		Phase3_BerserkRamming,    // 阶段三：狂暴冲撞 (30s熔毁倒计时 + 极速泰坦撞角冲撞)
		Defeated                  // 彻底击溃殉爆解体
	}

	/// <summary>
	/// 重工决战移动要塞「泰坦熔炉」多阶段解体控制器
	/// </summary>
	public partial class TitanForgeBossController : Node2D
	{
		public ShipEntity BossShip { get; private set; } = null!;
		public BossPhase CurrentPhase { get; private set; } = BossPhase.Phase1_Fortress;
		public Node2D? TargetPlayer { get; set; }

		// 狂暴阶段 30 秒自毁倒计时
		public float MeltdownTimer { get; private set; } = 30.0f;
		public bool IsMeltdownActive => CurrentPhase == BossPhase.Phase3_BerserkRamming;

		// 阶段二分离出的独立护航子舰
		public List<ShipEntity> SpawnedEscorts { get; } = new();

		private float _fireTimer = 0.0f;
		private float _barrageAngleOffset = 0.0f;
		private float _initialTotalHp = 0.0f;
		private bool _phase2Triggered = false;
		private bool _phase3Triggered = false;

		public void Initialize(ShipEntity bossShip, Node2D targetPlayer)
		{
			BossShip = bossShip;
			TargetPlayer = targetPlayer;
			_initialTotalHp = CalculateCurrentTotalHp();
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;
			if (!GodotObject.IsInstanceValid(BossShip) || CurrentPhase == BossPhase.Defeated) return;

			// 1. 动态评估阶段流转条件
			EvaluatePhaseTransitions(dt);

			// 2. 执行当前阶段专属的攻击与移动行为
			ExecutePhaseBehaviors(dt);
		}

		private void EvaluatePhaseTransitions(float dt)
		{
			float currentHp = CalculateCurrentTotalHp();
			float hpRatio = _initialTotalHp > 0.0f ? (currentHp / _initialTotalHp) : 0.0f;

			// -------------------------------------------------------------
			// 触发阶段二：总血量低于 60% 时，两翼爆甲脱离生成浮游子舰
			// -------------------------------------------------------------
			if (hpRatio <= 0.60f && !_phase2Triggered && CurrentPhase == BossPhase.Phase1_Fortress)
			{
				EnterPhase2();
			}

			// -------------------------------------------------------------
			// 触发阶段三：核心血量低于 30% 时，进入 30s 狂暴熔毁冲撞
			// -------------------------------------------------------------
			if (hpRatio <= 0.30f && !_phase3Triggered && CurrentPhase == BossPhase.Phase2_EscortSeparation)
			{
				EnterPhase3();
			}

			// 阶段三倒计时扣减
			if (CurrentPhase == BossPhase.Phase3_BerserkRamming)
			{
				MeltdownTimer -= dt;
				if (MeltdownTimer <= 0.0f)
				{
					TriggerBossCoreMeltdownDefeat();
				}
			}

			// 彻底击毁判定
			if (currentHp <= 0.0f)
			{
				TriggerBossCoreMeltdownDefeat();
			}
		}

		/// <summary>
		/// 进入阶段二：两翼战术脱落，生成 2 艘自主机动浮游炮台
		/// </summary>
		public void EnterPhase2()
		{
			_phase2Triggered = true;
			CurrentPhase = BossPhase.Phase2_EscortSeparation;

			JuiceManager.Instance?.TriggerHitstop(0.08f, 0.04f);
			JuiceManager.Instance?.TriggerExplosionJuice(BossShip.GlobalPosition, 1.2f);
			VfxManager.Instance?.SpawnFloatingText(BossShip.GlobalPosition, "⚠️ PHASE 2: 两翼脱离！浮游子舰分离！核心超载！", Colors.Cyan);

			// 引爆两翼爆炸螺栓脱落
			if (BossShip.AblativeDetonation != null)
			{
				BossShip.AblativeDetonation.TriggerDetonation(DetonationSection.LeftWing);
				BossShip.AblativeDetonation.TriggerDetonation(DetonationSection.RightWing);
			}

			// 生成 2 艘护卫浮游炮台
			SpawnEscortGunboat(new Vector2(-220, -50), Mathf.DegToRad(-45));
			SpawnEscortGunboat(new Vector2(220, -50), Mathf.DegToRad(45));
		}

		/// <summary>
		/// 进入阶段三：开启 30 秒狂暴自毁熔毁，全速冲撞玩家
		/// </summary>
		public void EnterPhase3()
		{
			_phase3Triggered = true;
			CurrentPhase = BossPhase.Phase3_BerserkRamming;
			MeltdownTimer = 30.0f;

			JuiceManager.Instance?.TriggerHitstop(0.12f, 0.02f);
			JuiceManager.Instance?.TriggerExplosionJuice(BossShip.GlobalPosition, 1.5f);
			VfxManager.Instance?.SpawnFloatingText(BossShip.GlobalPosition, "🚨 PHASE 3: 动力炉过载熔毁！30秒狂暴冲撞！", Colors.OrangeRed);
		}

		private void ExecutePhaseBehaviors(float dt)
		{
			_barrageAngleOffset += dt * 2.5f;
			_fireTimer -= dt;

			switch (CurrentPhase)
			{
				// -------------------------------------------------------------
				// 阶段一行为：要塞正面压制，加特林与主炮定期齐射
				// -------------------------------------------------------------
				case BossPhase.Phase1_Fortress:
					if (_fireTimer <= 0.0f)
					{
						_fireTimer = 0.25f;
						foreach (var weaponId in BossShip.Pulses.WeaponBuffers.Keys)
						{
							BossShip.Pulses.TriggerWeaponFire(weaponId, out _);
						}
					}
					break;

				// -------------------------------------------------------------
				// 阶段二行为：暴露核心，持续发射 360° 旋转等离子弹幕
				// -------------------------------------------------------------
				case BossPhase.Phase2_EscortSeparation:
					if (_fireTimer <= 0.0f)
					{
						_fireTimer = 0.35f;
						SpawnRotatingPlasmaBarrage(12);
					}
					break;

				// -------------------------------------------------------------
				// 阶段三行为：狂暴冲撞，主推力全开笔直撞向玩家
				// -------------------------------------------------------------
				case BossPhase.Phase3_BerserkRamming:
					if (TargetPlayer != null && GodotObject.IsInstanceValid(TargetPlayer))
					{
						Vector2 toPlayer = (TargetPlayer.GlobalPosition - BossShip.GlobalPosition).Normalized();
						BossShip.ApplyCentralForce(toPlayer * 350000.0f);
					}
					if (_fireTimer <= 0.0f)
					{
						_fireTimer = 0.20f;
						SpawnRotatingPlasmaBarrage(16);
					}
					break;
			}
		}

		private void SpawnEscortGunboat(Vector2 localOffset, float angleOffset)
		{
			var escort = new ShipEntity
			{
				Name = $"Boss_Escort_{SpawnedEscorts.Count + 1}",
				Position = BossShip.GlobalPosition + localOffset.Rotated(BossShip.Rotation),
				Rotation = BossShip.Rotation + angleOffset
			};
			escort.AddToGroup("Ship");
			escort.CurrentPalette = FactionPalettes.HeavyFoundry;
			GetParent().AddChild(escort);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(escort, anvilBp!);
			}

			if (TargetPlayer != null)
			{
				escort.AttachAI(AiArchetype.Swarm, TargetPlayer);
			}

			SpawnedEscorts.Add(escort);
		}

		private void SpawnRotatingPlasmaBarrage(int bulletCount)
		{
			if (BulletManager.Instance == null) return;

			float angleStep = Mathf.Tau / bulletCount;
			for (int i = 0; i < bulletCount; i++)
			{
				float angle = (i * angleStep) + _barrageAngleOffset;
				Vector2 dir = Vector2.Right.Rotated(angle);

				BulletManager.Instance.SpawnBullet(
					BossShip.GlobalPosition + (dir * 45.0f),
					dir * 380.0f,
					damage: 18.0f,
					pierce: 0,
					elements: ElementFlags.Thermal,
					attackerShip: BossShip,
					lifeTime: 3.5f,
					size: 1.2f
				);
			}
		}

		private void TriggerBossCoreMeltdownDefeat()
		{
			CurrentPhase = BossPhase.Defeated;
			JuiceManager.Instance?.TriggerHitstop(0.16f, 0.02f);
			JuiceManager.Instance?.TriggerExplosionJuice(BossShip.GlobalPosition, 2.5f);
			VfxManager.Instance?.SpawnModuleExplosion(BossShip.GlobalPosition, new Vector2(250, 250), Colors.OrangeRed, shardCount: 64);
			VfxManager.Instance?.SpawnFloatingText(BossShip.GlobalPosition, "👑 泰坦熔炉已彻底毁灭！要塞殉爆！", Colors.Gold);

			foreach (var escort in SpawnedEscorts)
			{
				if (GodotObject.IsInstanceValid(escort)) escort.QueueFree();
			}

			BossShip.QueueFree();
		}

		public float CalculateCurrentTotalHp()
		{
			if (!GodotObject.IsInstanceValid(BossShip)) return 0.0f;
			float hp = 0.0f;
			foreach (var m in BossShip.Grid.Modules)
			{
				if (!m.IsDestroyed) hp += m.CurrentHp;
			}
			return hp;
		}

		public float GetHpRatio()
		{
			float current = CalculateCurrentTotalHp();
			return _initialTotalHp > 0.0f ? (current / _initialTotalHp) : 0.0f;
		}
	}
}
