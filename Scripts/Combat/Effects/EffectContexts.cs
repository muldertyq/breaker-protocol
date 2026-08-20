using System;
using Godot;

namespace BreakerProtocol.Combat.Effects
{
	/// <summary>
	/// 元素属性标签掩码（支持按位或组合）
	/// </summary>
	[Flags]
	public enum ElementFlags
	{
		None     = 0,
		Kinetic  = 1 << 0, // 常规动能实弹
		Energy   = 1 << 1, // 高能光子/激光
		Cryo     = 1 << 2, // 极寒 (虚空/冷凝)
		Thermal  = 1 << 3, // 热核/火 (重工燃烧)
		Acid     = 1 << 4, // 生化强酸/腐蚀
		Void     = 1 << 5  // 高维湮灭/奇点
	}

	/// <summary>
	/// 在内部 PCB 管线中流动的能量脉冲数据包
	/// </summary>
	public struct PulsePacket
	{
		public int PulseId;                  // 脉冲序号
		public float Power;                  // 脉冲能量强度 (基准 1.0)
		public ElementFlags Elements;        // 当前携带的元素标签
		public float HeatMultiplier;         // 发热量系数 (默认 1.0)
		public float SpeedMultiplier;        // 弹速加成系数 (默认 1.0)
		public float DamageMultiplier;       // 伤害加成系数 (默认 1.0)
		public int BonusPierce;              // 额外附加的穿透层数
		public int SplitCount;               // 弹道分裂数量 (默认 1，不分裂)

		public static PulsePacket CreateDefault(int id, float power = 1.0f)
		{
			return new PulsePacket
			{
				PulseId = id,
				Power = power,
				Elements = ElementFlags.None,
				HeatMultiplier = 1.0f,
				SpeedMultiplier = 1.0f,
				DamageMultiplier = 1.0f,
				BonusPierce = 0,
				SplitCount = 1
			};
		}
	}

	/// <summary>
	/// 武器开火瞬间的物理与发射上下文
	/// </summary>
	public struct FireContext
	{
		public Node2D FiringShip;            // 发射该子弹的战舰节点
		public Vector2 MuzzleWorldPos;       // 炮口在世界空间中的像素坐标
		public Vector2 FireDirection;        // 发射基准朝向向量 (归一化)
		public float BaseDamage;             // 基础伤害
		public float BaseSpeed;              // 基础弹速 (像素/秒)
		public int BasePierce;               // 基础穿透层数
		public float BaseRange;              // 基础射程 (像素)
		public PulsePacket CompiledPulse;    // 经过所有修饰舱编译后的最终脉冲数据
	}

	/// <summary>
	/// 子弹命中目标时的物理与创伤结算上下文
	/// </summary>
	public struct HitResult
	{
		public Node2D? AttackerShip;         // 攻击者战舰
		public Node2D? TargetEntity;         // 被击中的目标 (战舰/小行星/护盾)
		public Vector2 HitWorldPos;          // 命中点世界像素坐标
		public Vector2 HitNormal;            // 受击表面法线向量
		public float FinalDamage;            // 最终结算伤害
		public ElementFlags AppliedElements; // 附带的元素类型
		public bool IsRicochet;              // 是否发生了跳弹
		public int RemainingPierce;          // 剩余可穿透层数
	}
}
