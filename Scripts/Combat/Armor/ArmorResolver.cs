using Godot;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Ship;

namespace BreakerProtocol.Combat.Armor
{
	/// <summary>
	/// 装甲受击结算结果数据包
	/// </summary>
	public struct ArmorHitOutcome
	{
		public bool IsRicochet;              // 是否发生跳弹偏转
		public Vector2 ReflectedVelocity;    // 跳弹后的反射速度向量
		public float ActualDamageDealt;      // 实际扣除构件的伤害
		public bool IsModuleDestroyed;       // 该构件是否被击碎爆甲
		public float RemainingHp;            // 构件剩余血量
	}

	/// <summary>
	/// 装甲受击与跳弹物理结算器
	/// 严格遵循规范 08 中的倾角偏转与穿深衰减数学模型
	/// </summary>
	public static class ArmorResolver
	{
		/// <summary>
		/// 结算子弹对特定装甲/构件的撞击
		/// </summary>
		public static ArmorHitOutcome ResolveImpact(
			ModuleInstance targetModule,
			Vector2 bulletVelocity,
			Vector2 hitNormal,
			float incomingDamage,
			ElementFlags elements,
			int remainingPierce)
		{
			var outcome = new ArmorHitOutcome
			{
				IsRicochet = false,
				ReflectedVelocity = Vector2.Zero,
				ActualDamageDealt = 0.0f,
				IsModuleDestroyed = false,
				RemainingHp = targetModule.CurrentHp
			};

			var props = targetModule.Definition.Properties;
			float bounceThresholdDeg = props.TryGetProperty("bounceAngleThreshold", out var bt) 
				? bt.GetSingle() 
				: 70.0f;

			Vector2 bulletDir = bulletVelocity.Normalized();

			// ============================================================
			// 阶段 1：入射角与跳弹偏转判定 (Ricochet Check)
			// 入射角 θ 为子弹反方向与受击表面法线的夹角 (0° = 垂直撞击, 90° = 擦边掠射)
			// ============================================================
			float cosTheta = Mathf.Clamp((-bulletDir).Dot(hitNormal), -1.0f, 1.0f);
			float impactAngleDeg = Mathf.RadToDeg(Mathf.Acos(cosTheta));

			// 若入射角超过阈值，且子弹非高爆电浆，判定为跳弹！
			bool canBounce = targetModule.Definition.Category == "Armor" && !elements.HasFlag(ElementFlags.Thermal);

			if (canBounce && impactAngleDeg >= bounceThresholdDeg)
			{
				outcome.IsRicochet = true;
				// 计算反射向量: r = d - 2(d · n)n
				Vector2 reflectDir = bulletDir - (2.0f * bulletDir.Dot(hitNormal) * hitNormal);
				// 跳弹后保留 65% 的飞行初速
				outcome.ReflectedVelocity = reflectDir.Normalized() * (bulletVelocity.Length() * 0.65f);
				
				// 跳弹仅造成 5% 的擦伤划痕
				float scratchDamage = Mathf.Max(1.0f, incomingDamage * 0.05f);
				targetModule.CurrentHp = Mathf.Max(0.0f, targetModule.CurrentHp - scratchDamage);
				outcome.ActualDamageDealt = scratchDamage;
				outcome.RemainingHp = targetModule.CurrentHp;
				outcome.IsModuleDestroyed = targetModule.IsDestroyed;

				GD.PrintRich($"[color=yellow][Armor] ⚡ 触发跳弹偏转！入射角: {impactAngleDeg:F1}° >= 阈值 {bounceThresholdDeg:F1}° | 划痕伤害: {scratchDamage:F1}[/color]");
				return outcome;
			}

			// ============================================================
			// 阶段 2：常规正面击穿伤害结算 (Penetration & Damage Mitigation)
			// ============================================================
			float baseResistance = targetModule.Definition.ArmorResistance;

			// 虚空力场装甲对能量伤害有双倍抗性
			if (targetModule.Definition.Faction == "VoidSyndicate" && elements.HasFlag(ElementFlags.Energy))
			{
				baseResistance *= 1.8f;
			}

			// 穿甲计算：伤害减去固定护甲阻抗
			float effectiveDamage = Mathf.Max(5.0f, incomingDamage - baseResistance);

			targetModule.CurrentHp = Mathf.Max(0.0f, targetModule.CurrentHp - effectiveDamage);
			outcome.ActualDamageDealt = effectiveDamage;
			outcome.RemainingHp = targetModule.CurrentHp;
			outcome.IsModuleDestroyed = targetModule.IsDestroyed;

			GD.PrintRich($"[color=red][Armor] 💥 击穿装甲！原始:{incomingDamage:F0} -> 减免后:{effectiveDamage:F0} (护甲值:{baseResistance:F0}) | 构件剩余 HP: {outcome.RemainingHp:F0}/{targetModule.MaxHp:F0}[/color]");

			return outcome;
		}
	}
}
