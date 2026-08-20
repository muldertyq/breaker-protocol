using System.Collections.Generic;
using Godot;
using BreakerProtocol.Combat.Effects;

namespace BreakerProtocol.Ship.Pipeline
{
	/// <summary>
	/// 当前正在 PCB 导线中流动行进的单个脉冲物理实体
	/// </summary>
	public class ActivePulse
	{
		public int PulseId { get; }
		public string WireId { get; }
		public PulsePacket Packet; // 携带的战斗与元素属性包

		/// <summary>
		/// 正在穿梭的导线网格折线路径引用
		/// </summary>
		public List<Vector2I> GridPath { get; }

		/// <summary>
		/// 在整条折线上的归一化行进进度 (0.0 = 起点 OUT 引脚, 1.0 = 终点 IN 引脚)
		/// </summary>
		public float Progress { get; set; }

		/// <summary>
		/// 脉冲在铜排中的行进速度 (GU/秒，默认 20 GU/s = 160 px/s)
		/// </summary>
		public float Speed { get; }

		/// <summary>
		/// 整条折线的总网格长度 (GU)
		/// </summary>
		public float TotalLengthGu { get; }

		public ActivePulse(int pulseId, string wireId, PulsePacket packet, List<Vector2I> path, float speed = 20.0f)
		{
			PulseId = pulseId;
			WireId = wireId;
			Packet = packet;
			GridPath = path;
			Speed = speed;
			Progress = 0.0f;
			TotalLengthGu = Mathf.Max(1.0f, path.Count - 1);
		}

		/// <summary>
		/// 计算当前脉冲在飞船局部网格空间中的精确浮点坐标 (GU)
		/// </summary>
		public Vector2 GetCurrentLocalGridPos()
		{
			if (GridPath.Count == 0) return Vector2.Zero;
			if (GridPath.Count == 1) return GridPath[0];

			float currentDist = Progress * (GridPath.Count - 1);
			int segmentIndex = Mathf.Clamp(Mathf.FloorToInt(currentDist), 0, GridPath.Count - 2);
			float segmentT = currentDist - segmentIndex;

			Vector2 p1 = GridPath[segmentIndex];
			Vector2 p2 = GridPath[segmentIndex + 1];
			return p1.Lerp(p2, segmentT);
		}
	}

	/// <summary>
	/// 武器发射终端内部的脉冲暂存缓冲池
	/// 实现“停火蓄水，开火瞬间倾泻高射速爆发”的数学模型
	/// </summary>
	public class TerminalWeaponBuffer
	{
		public string WeaponModuleInstanceId { get; }

		/// <summary>
		/// 缓冲区最大容纳脉冲数（基础容量 + 外挂电容）
		/// </summary>
		public int MaxCapacity { get; set; } = 8;

		/// <summary>
		/// 武器单次发射消耗的脉冲数
		/// </summary>
		public int PulsesPerShot { get; set; } = 1;

		/// <summary>
		/// 武器最大机械循环射速上限 (发/秒) - 默认 8.0 发/秒
		/// </summary>
		public float MaxCyclingFireRate { get; set; } = 8.0f;

		// 待发射的脉冲队列 (先进先出 FIFO)
		private readonly Queue<PulsePacket> _bufferedPulses = new();

		// 射击冷却计时器
		private float _fireCooldownTimer = 0.0f;

		// 属性双重暴露：兼容 BufferedCount 与 CurrentBufferedCount
		public int BufferedCount => _bufferedPulses.Count;
		public int CurrentBufferedCount => _bufferedPulses.Count;

		public bool CanFire => _bufferedPulses.Count >= PulsesPerShot && _fireCooldownTimer <= 0.0f;

		public TerminalWeaponBuffer(string weaponModuleId, int capacity = 8, int pulsesPerShot = 1, float maxFireRate = 8.0f)
		{
			WeaponModuleInstanceId = weaponModuleId;
			MaxCapacity = capacity;
			PulsesPerShot = pulsesPerShot;
			MaxCyclingFireRate = maxFireRate;
		}

		/// <summary>
		/// 将到达炮口的脉冲存入缓冲区
		/// </summary>
		public bool TryEnqueue(PulsePacket packet)
		{
			if (_bufferedPulses.Count >= MaxCapacity)
			{
				return false; // 缓冲区溢流
			}

			_bufferedPulses.Enqueue(packet);
			return true;
		}

		public void UpdateTimer(float dt)
		{
			if (_fireCooldownTimer > 0.0f)
			{
				_fireCooldownTimer -= dt;
			}
		}

		/// <summary>
		/// 尝试从缓冲区消耗脉冲执行开火
		/// </summary>
		public bool TryConsumeForFire(out PulsePacket outCompiledPulse)
		{
			outCompiledPulse = default;
			if (!CanFire) return false;

			outCompiledPulse = _bufferedPulses.Dequeue();
			for (int i = 1; i < PulsesPerShot && _bufferedPulses.Count > 0; i++)
			{
				var additional = _bufferedPulses.Dequeue();
				outCompiledPulse.Power += additional.Power;
				outCompiledPulse.Elements |= additional.Elements;
			}

			_fireCooldownTimer = 1.0f / MaxCyclingFireRate;
			return true;
		}
	}
}
