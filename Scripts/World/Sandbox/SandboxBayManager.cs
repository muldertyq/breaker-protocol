using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.Ship.Thermal;

namespace BreakerProtocol.World.Sandbox
{
	public enum TargetShipType
	{
		StaticDummy,  // 轻型静止装甲木桩
		MobileKiter,  // 高速机动风筝靶机
		HeavyCruiser, // 满装甲巡洋舰战力靶
		TitanBossStub // 泰坦要塞重装桩
	}

	/// <summary>
	/// 母港虚拟风洞打靶场与实时 DPS 监视总控 (Sandbox Bay)
	/// </summary>
	public partial class SandboxBayManager : Node
	{
		public static SandboxBayManager Instance { get; private set; } = null!;
		public ShipEntity? PlayerShip { get; set; }
		public List<ShipEntity> ActiveTargets { get; } = new();

		// 调试环境开关
		public bool InfinitePower { get; set; } = true;
		public bool ZeroThermal { get; set; } = true;
		public bool InvinciblePlayer { get; set; } = true;

		// DPS 实时统计 (3 秒滑动窗口)
		public float CurrentDPS { get; private set; } = 0.0f;
		public float PeakDPS { get; private set; } = 0.0f;
		public float TotalDamageDealt { get; private set; } = 0.0f;
		public int TotalHits { get; private set; } = 0;
		public int TotalRicochets { get; private set; } = 0;

		private readonly Queue<(float time, float damage)> _damageRecords = new();
		private float _elapsedTime = 0.0f;

		public override void _Ready()
		{
			Instance = this;
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;
			_elapsedTime += dt;

			// 1. 维护零发热超导状态 (直接调用现有的 Reset 方法)
			if (PlayerShip != null && GodotObject.IsInstanceValid(PlayerShip))
			{
				if (ZeroThermal && PlayerShip.Thermal != null)
				{
					PlayerShip.Thermal.Reset();
				}
			}

			// 2. 清理超过 3 秒的历史伤害记录，解算滑动窗口 DPS
			while (_damageRecords.Count > 0 && (_elapsedTime - _damageRecords.Peek().time) > 3.0f)
			{
				_damageRecords.Dequeue();
			}

			float sumDmgInWindow = 0.0f;
			foreach (var r in _damageRecords) sumDmgInWindow += r.damage;
			CurrentDPS = sumDmgInWindow / 3.0f;

			if (CurrentDPS > PeakDPS) PeakDPS = CurrentDPS;

			// 3. 清理已损毁的靶舰
			ActiveTargets.RemoveAll(t => !GodotObject.IsInstanceValid(t) || t.IsQueuedForDeletion());
		}

		public void RecordDamage(float damage, bool isRicochet)
		{
			TotalHits++;
			if (isRicochet) TotalRicochets++;

			if (damage > 0.0f)
			{
				TotalDamageDealt += damage;
				_damageRecords.Enqueue((_elapsedTime, damage));
			}
		}

		public void SpawnTarget(TargetShipType type, Vector2 pos)
		{
			var target = new ShipEntity
			{
				Name = $"Sandbox_Target_{type}_{ActiveTargets.Count + 1}",
				Position = pos
			};
			target.AddToGroup("Ship");
			target.CurrentPalette = FactionPalettes.VoidSyndicate;
			GetParent().AddChild(target);

			string bpId = type switch
			{
				TargetShipType.StaticDummy  => "bp_vs_s_phantom",
				TargetShipType.MobileKiter  => "bp_bc_s_mantis",
				TargetShipType.HeavyCruiser => "bp_hf_m_anvil",
				_                           => "bp_hf_m_anvil"
			};

			if (DataManager.Instance.Blueprints.TryGet(bpId, out var bp))
			{
				ShipBlueprintLoader.ApplyBlueprint(target, bp!);
			}

			ActiveTargets.Add(target);
		}

		public void ClearAllTargets()
		{
			foreach (var t in ActiveTargets)
			{
				if (GodotObject.IsInstanceValid(t)) t.QueueFree();
			}
			ActiveTargets.Clear();
			ResetStats();
		}

		public void ResetStats()
		{
			_damageRecords.Clear();
			CurrentDPS = 0.0f;
			PeakDPS = 0.0f;
			TotalDamageDealt = 0.0f;
			TotalHits = 0;
			TotalRicochets = 0;
		}

		public float GetRicochetRate()
		{
			return TotalHits > 0 ? ((float)TotalRicochets / TotalHits * 100.0f) : 0.0f;
		}
	}
}
