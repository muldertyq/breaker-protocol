using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Ship;

namespace BreakerProtocol.Ship.Pipeline
{
	/// <summary>
	/// 全舰 PCB 能量脉冲时空流动仿真器 (支持即时元素重载、Mixer 混流合成与战地飞线发热惩罚)
	/// </summary>
	public class PulseSimulator
	{
		private readonly ShipEntity _ship;

		public List<ActivePulse> InFlightPulses { get; } = new();
		public Dictionary<string, TerminalWeaponBuffer> WeaponBuffers { get; } = new();

		private readonly Dictionary<string, float> _powerSourceTimers = new();
		private readonly LogicComponentRuntime _logicRuntime = new();
		private readonly Dictionary<string, PulsePacket> _mixerPendingPackets = new();

		private int _pulseIdCounter = 0;
		public const float DefaultPulseSpeedGu = 20.0f;

		public event Action<string, PulsePacket>? OnWeaponFired;

		public PulseSimulator(ShipEntity ship)
		{
			_ship = ship;
			RebuildBuffers();
		}

		public void RebuildBuffers()
		{
			WeaponBuffers.Clear();
			_powerSourceTimers.Clear();
			_mixerPendingPackets.Clear();

			foreach (var module in _ship.Grid.Modules)
			{
				if (module.IsDestroyed) continue;

				if (module.Definition.Category == "Weapon")
				{
					var props = module.Definition.Properties;
					int cost = props.TryGetProperty("pulseCost", out var pc) ? pc.GetInt32() : 1;
					float fireRate = props.TryGetProperty("fireRate", out var fr) ? fr.GetSingle() : 6.0f;

					WeaponBuffers[module.InstanceId] = new TerminalWeaponBuffer(
						module.InstanceId,
						capacity: 8,
						pulsesPerShot: cost,
						maxFireRate: fireRate
					);
				}
				else if (module.Definition.Category == "PowerSource")
				{
					_powerSourceTimers[module.InstanceId] = 0.0f;
				}
			}
		}

		public void Update(float dt)
		{
			UpdatePowerSources(dt);
			UpdateInFlightPulses(dt);

			foreach (var buffer in WeaponBuffers.Values)
			{
				buffer.UpdateTimer(dt);
			}
		}

		private void UpdatePowerSources(float dt)
		{
			foreach (var module in _ship.Grid.Modules)
			{
				if (module.IsDestroyed || module.Definition.Category != "PowerSource") continue;

				if (!_powerSourceTimers.ContainsKey(module.InstanceId))
				{
					_powerSourceTimers[module.InstanceId] = 0.0f;
				}

				_powerSourceTimers[module.InstanceId] -= dt;

				float pulsesPerSec = module.Definition.Properties.TryGetProperty("pulseOutput", out var po) 
					? po.GetSingle() 
					: 2.5f;
				
				float emitInterval = 1.0f / Mathf.Max(0.1f, pulsesPerSec);

				if (_powerSourceTimers[module.InstanceId] <= 0.0f)
				{
					_powerSourceTimers[module.InstanceId] = emitInterval;
					EmitPulseFromPowerSource(module);
				}
			}
		}

		private void EmitPulseFromPowerSource(ModuleInstance powerModule)
		{
			foreach (var wire in _ship.Pipeline.Wires)
			{
				if (wire.IsSevered) continue;

				if (wire.SourceModuleId == powerModule.InstanceId)
				{
					var initialPacket = PulsePacket.CreateDefault(++_pulseIdCounter, power: 1.0f);
					
					if (powerModule.Definition.Faction == "BioChitin") initialPacket.Elements |= ElementFlags.Acid;
					if (powerModule.Definition.Faction == "VoidSyndicate") initialPacket.Elements |= ElementFlags.Void;

					var activePulse = new ActivePulse(initialPacket.PulseId, wire.WireId, initialPacket, wire.GridPath, DefaultPulseSpeedGu);
					InFlightPulses.Add(activePulse);
				}
			}
		}

		private void UpdateInFlightPulses(float dt)
		{
			for (int i = InFlightPulses.Count - 1; i >= 0; i--)
			{
				var pulse = InFlightPulses[i];
				var wire = GetWireById(pulse.WireId);
				if (wire == null || wire.IsSevered)
				{
					InFlightPulses.RemoveAt(i);
					continue;
				}

				// ============================================================
				// 核心发热机制：若经由战地应急飞线，额外增加 +35% 发热负荷
				// ============================================================
				if (wire.IsHotwire)
				{
					_ship.Thermal?.AddHeat(0.35f * pulse.Packet.Power * dt);
				}

				float progressDelta = (dt * pulse.Speed) / pulse.TotalLengthGu;
				pulse.Progress += progressDelta;

				if (pulse.Progress >= 1.0f)
				{
					InFlightPulses.RemoveAt(i);
					HandlePulseArrival(pulse);
				}
			}
		}

		private void HandlePulseArrival(ActivePulse pulse)
		{
			var wire = GetWireById(pulse.WireId);
			if (wire == null) return;

			var targetModule = _ship.Grid.GetModuleAt(wire.TargetGridPos);
			if (targetModule == null || targetModule.IsDestroyed) return;

			if (targetModule.Definition.Category == "Logic")
			{
				string logicType = targetModule.Definition.Properties.TryGetProperty("logicType", out var lt)
					? lt.GetString() ?? "Splitter"
					: "Splitter";

				var downstreamWires = new List<PipelineWire>();
				foreach (var w in _ship.Pipeline.Wires)
				{
					if (!w.IsSevered && w.SourceModuleId == targetModule.InstanceId)
					{
						downstreamWires.Add(w);
					}
				}

				if (downstreamWires.Count == 0) return;

				if (logicType == "Mixer")
				{
					string mixerId = targetModule.InstanceId;
					if (_mixerPendingPackets.TryGetValue(mixerId, out var pendingPacket))
					{
						_mixerPendingPackets.Remove(mixerId);

						var fusedPacket = pulse.Packet;
						fusedPacket.Elements |= pendingPacket.Elements;
						fusedPacket.Power += pendingPacket.Power * 0.5f;
						fusedPacket.DamageMultiplier = Mathf.Max(pulse.Packet.DamageMultiplier, pendingPacket.DamageMultiplier) * 1.2f;

						var outWire = downstreamWires[0];
						var fusedPulse = new ActivePulse(++_pulseIdCounter, outWire.WireId, fusedPacket, outWire.GridPath, DefaultPulseSpeedGu);
						InFlightPulses.Add(fusedPulse);
					}
					else
					{
						_mixerPendingPackets[mixerId] = pulse.Packet;
					}
					return;
				}

				PipelineWire? targetWire = (logicType == "Splitter")
					? _logicRuntime.RouteSplitter(targetModule.InstanceId, downstreamWires)
					: downstreamWires[0];

				if (targetWire != null)
				{
					var forwardedPulse = new ActivePulse(pulse.PulseId, targetWire.WireId, pulse.Packet, targetWire.GridPath, DefaultPulseSpeedGu);
					InFlightPulses.Add(forwardedPulse);
				}
			}
			else if (targetModule.Definition.Category == "Modifier")
			{
				if (targetModule.Definition.Properties.TryGetProperty("effectsOnPulse", out var effects))
				{
					EffectProcessor.Instance.ProcessPulseModifiers(ref pulse.Packet, effects);
				}

				foreach (var downstreamWire in _ship.Pipeline.Wires)
				{
					if (!downstreamWire.IsSevered && downstreamWire.SourceModuleId == targetModule.InstanceId)
					{
						var forwardedPulse = new ActivePulse(pulse.PulseId, downstreamWire.WireId, pulse.Packet, downstreamWire.GridPath, DefaultPulseSpeedGu);
						InFlightPulses.Add(forwardedPulse);
					}
				}
			}
			else if (targetModule.Definition.Category == "Weapon")
			{
				if (WeaponBuffers.TryGetValue(targetModule.InstanceId, out var buffer))
				{
					buffer.TryEnqueue(pulse.Packet);
				}
			}
		}

		/// <summary>
		/// 武器开火触发 (支持热力熔断拦截与元素标志位覆盖)
		/// </summary>
		public bool TriggerWeaponFire(string weaponModuleInstanceId, out PulsePacket firedPulse, ElementFlags? overrideElements = null)
		{
			firedPulse = default;

			// 热熔断状态下禁止开火
			if (_ship.Thermal != null && _ship.Thermal.IsOverheated)
			{
				return false;
			}

			if (WeaponBuffers.TryGetValue(weaponModuleInstanceId, out var buffer))
			{
				if (buffer.TryConsumeForFire(out firedPulse))
				{
					if (overrideElements.HasValue)
					{
						firedPulse.Elements = overrideElements.Value;
					}
					OnWeaponFired?.Invoke(weaponModuleInstanceId, firedPulse);
					return true;
				}
			}

			return false;
		}

		private PipelineWire? GetWireById(string wireId)
		{
			foreach (var w in _ship.Pipeline.Wires)
			{
				if (w.WireId == wireId) return w;
			}
			return null;
		}
	}
}
