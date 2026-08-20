using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace BreakerProtocol.Combat.Effects
{
	/// <summary>
	/// 通用效果算子流水线执行器
	/// </summary>
	public class EffectProcessor
	{
		public static EffectProcessor Instance { get; } = new();

		// 注册的所有算子映射表：Key 为 EffectType
		private readonly Dictionary<string, IAtomicEffect> _effectRegistry = new();

		private EffectProcessor()
		{
			// 初始化注册内置的核心算子
			RegisterEffect(new RecoilImpulseEffect());
			RegisterEffect(new ModifyPierceEffect());
			RegisterEffect(new ApplyCryoEffect());
			RegisterEffect(new ApplyFireEffect());
			RegisterEffect(new ExplodeOnHitEffect());
			RegisterEffect(new SplitProjectilesEffect());
		}

		/// <summary>
		/// 注册一个原子算子（支持后续通过 Mod 扩展新的 C# 算子）
		/// </summary>
		public void RegisterEffect(IAtomicEffect effect)
		{
			_effectRegistry[effect.EffectType] = effect;
			GD.Print($"[EffectProcessor] 算子注册就绪: [{effect.EffectType}]");
		}

		/// <summary>
		/// 阶段 1：对脉冲数据包执行链式修饰
		/// </summary>
		public void ProcessPulseModifiers(ref PulsePacket pulse, JsonElement effectsArray)
		{
			if (effectsArray.ValueKind != JsonValueKind.Array) return;

			foreach (var effectConfig in effectsArray.EnumerateArray())
			{
				if (effectConfig.TryGetProperty("type", out var typeProp))
				{
					string effectType = typeProp.GetString() ?? string.Empty;
					if (_effectRegistry.TryGetValue(effectType, out var effect))
					{
						effect.OnModifyPulse(ref pulse, effectConfig);
					}
				}
			}
		}

		/// <summary>
		/// 阶段 2：执行开火瞬间效果链
		/// </summary>
		public void ProcessOnFireEffects(ref FireContext context, JsonElement effectsArray)
		{
			if (effectsArray.ValueKind != JsonValueKind.Array) return;

			foreach (var effectConfig in effectsArray.EnumerateArray())
			{
				if (effectConfig.TryGetProperty("type", out var typeProp))
				{
					string effectType = typeProp.GetString() ?? string.Empty;
					if (_effectRegistry.TryGetValue(effectType, out var effect))
					{
						effect.OnFire(ref context, effectConfig);
					}
				}
			}
		}

		/// <summary>
		/// 阶段 3：执行命中受击瞬间效果链
		/// </summary>
		public void ProcessOnHitEffects(ref HitResult hit, JsonElement effectsArray)
		{
			if (effectsArray.ValueKind != JsonValueKind.Array) return;

			foreach (var effectConfig in effectsArray.EnumerateArray())
			{
				if (effectConfig.TryGetProperty("type", out var typeProp))
				{
					string effectType = typeProp.GetString() ?? string.Empty;
					if (_effectRegistry.TryGetValue(effectType, out var effect))
					{
						effect.OnHit(ref hit, effectConfig);
					}
				}
			}
		}
	}
}
