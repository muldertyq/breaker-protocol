using System.Text.Json;

namespace BreakerProtocol.Combat.Effects
{
	/// <summary>
	/// 原子效果算子接口
	/// 每个算子负责实现一个极小颗粒度的原子化机制（如：反冲、冰冻、穿甲、爆炸）
	/// </summary>
	public interface IAtomicEffect
	{
		/// <summary>
		/// 算子唯一标识 ID（对应 JSON 配置中的 "type"，如 "RecoilImpulse", "ApplyCryo"）
		/// </summary>
		string EffectType { get; }

		/// <summary>
		/// 阶段 1：脉冲流经修饰舱时的修饰处理
		/// </summary>
		void OnModifyPulse(ref PulsePacket pulse, JsonElement config);

		/// <summary>
		/// 阶段 2：炮口正式开火发射瞬间的物理处理 (如：施加船体后坐力反冲)
		/// </summary>
		void OnFire(ref FireContext context, JsonElement config);

		/// <summary>
		/// 阶段 3：子弹命中目标瞬间的创伤与异常状态处理 (如：施加冰冻、触发 AOE 爆炸)
		/// </summary>
		void OnHit(ref HitResult hit, JsonElement config);
	}
}
