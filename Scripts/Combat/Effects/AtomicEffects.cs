using System.Text.Json;
using Godot;

namespace BreakerProtocol.Combat.Effects
{
	/// <summary>
	/// 算子 1：开火物理后坐力反冲 (RecoilImpulse)
	/// </summary>
	public class RecoilImpulseEffect : IAtomicEffect
	{
		public string EffectType => "RecoilImpulse";

		public void OnModifyPulse(ref PulsePacket pulse, JsonElement config) { }

		public void OnFire(ref FireContext context, JsonElement config)
		{
			float impulse = config.TryGetProperty("impulse", out var p) ? p.GetSingle() : 1000.0f;

			if (context.FiringShip is RigidBody2D rb)
			{
				// 向开火反方向施加冲量 (牛顿第三定律)
				Vector2 recoilDir = -context.FireDirection;
				rb.ApplyCentralImpulse(recoilDir * impulse);
			}
		}

		public void OnHit(ref HitResult hit, JsonElement config) { }
	}

	/// <summary>
	/// 算子 2：穿甲深度修饰 (ModifyPierce)
	/// </summary>
	public class ModifyPierceEffect : IAtomicEffect
	{
		public string EffectType => "ModifyPierce";

		public void OnModifyPulse(ref PulsePacket pulse, JsonElement config)
		{
			int extraPierce = config.TryGetProperty("extraPierce", out var p) ? p.GetInt32() : 1;
			pulse.BonusPierce += extraPierce;
		}

		public void OnFire(ref FireContext context, JsonElement config) { }
		public void OnHit(ref HitResult hit, JsonElement config) { }
	}

	/// <summary>
	/// 算子 3：极寒冰冻注入 (ApplyCryo)
	/// </summary>
	public class ApplyCryoEffect : IAtomicEffect
	{
		public string EffectType => "ApplyCryo";

		public void OnModifyPulse(ref PulsePacket pulse, JsonElement config)
		{
			pulse.Elements |= ElementFlags.Cryo;
			
			// 极寒修饰舱反向吸热特性 (规范 05)
			float heatReduction = config.TryGetProperty("heatReduction", out var p) ? p.GetSingle() : 0.40f;
			pulse.HeatMultiplier *= (1.0f - heatReduction);
		}

		public void OnFire(ref FireContext context, JsonElement config) { }

		public void OnHit(ref HitResult hit, JsonElement config)
		{
			hit.AppliedElements |= ElementFlags.Cryo;
			float slowDuration = config.TryGetProperty("slowDuration", out var p) ? p.GetSingle() : 2.0f;
			GD.PrintRich($"[color=cyan][ApplyCryo] 目标命中！施加极寒减速/定身，持续 {slowDuration:F1} 秒。[/color]");
		}
	}

	/// <summary>
	/// 算子 4：热核燃烧注入 (ApplyFire)
	/// </summary>
	public class ApplyFireEffect : IAtomicEffect
	{
		public string EffectType => "ApplyFire";

		public void OnModifyPulse(ref PulsePacket pulse, JsonElement config)
		{
			pulse.Elements |= ElementFlags.Thermal;
			pulse.HeatMultiplier *= 1.30f; // 增压增热
		}

		public void OnFire(ref FireContext context, JsonElement config) { }

		public void OnHit(ref HitResult hit, JsonElement config)
		{
			hit.AppliedElements |= ElementFlags.Thermal;
			float burnDps = config.TryGetProperty("burnDps", out var p) ? p.GetSingle() : 25.0f;
			GD.PrintRich($"[color=orange][ApplyFire] 目标起火！附加每秒 {burnDps:F0} 点真实燃烧伤害。[/color]");
		}
	}

	/// <summary>
	/// 算子 5：命中范围高爆 (ExplodeOnHit)
	/// </summary>
	public class ExplodeOnHitEffect : IAtomicEffect
	{
		public string EffectType => "ExplodeOnHit";

		public void OnModifyPulse(ref PulsePacket pulse, JsonElement config) { }
		public void OnFire(ref FireContext context, JsonElement config) { }

		public void OnHit(ref HitResult hit, JsonElement config)
		{
			float radius = config.TryGetProperty("radiusMeters", out var pr) ? pr.GetSingle() : 10.0f;
			float damage = config.TryGetProperty("aoeDamage", out var pd) ? pd.GetSingle() : 150.0f;

			GD.PrintRich($"[color=yellow][ExplodeOnHit] 💥 触发范围爆炸！半径: {radius:F1}m, 爆炸伤害: {damage:F0}[/color]");
		}
	}

	/// <summary>
	/// 算子 6：弹道多棱镜分裂 (SplitProjectiles)
	/// </summary>
	public class SplitProjectilesEffect : IAtomicEffect
	{
		public string EffectType => "SplitProjectiles";

		public void OnModifyPulse(ref PulsePacket pulse, JsonElement config)
		{
			int count = config.TryGetProperty("splitCount", out var p) ? p.GetInt32() : 3;
			float dmgFactor = config.TryGetProperty("damageFactor", out var df) ? df.GetSingle() : 0.45f;

			pulse.SplitCount = count;
			pulse.DamageMultiplier *= dmgFactor;
		}

		public void OnFire(ref FireContext context, JsonElement config)
		{
			GD.PrintRich($"[color=magenta][SplitProjectiles] 激光/子弹通过分光棱镜，分裂为 {context.CompiledPulse.SplitCount} 束！[/color]");
		}

		public void OnHit(ref HitResult hit, JsonElement config) { }
	}
}
