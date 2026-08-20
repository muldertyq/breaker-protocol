using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BreakerProtocol.Ship;

namespace BreakerProtocol.Combat.Effects
{
	public class TargetElementalStatus
	{
		public Node2D TargetEntity { get; }
		
		public float FireDuration { get; set; } = 0.0f;
		public float CryoDuration { get; set; } = 0.0f;
		public float AcidDuration { get; set; } = 0.0f;
		public float VoidDuration { get; set; } = 0.0f;

		public float FreezeLockTimer { get; set; } = 0.0f;
		public float EntropyCurseTimer { get; set; } = 0.0f;
		public float AcidPoolTimer { get; set; } = 0.0f;

		public bool IsFrozen => FreezeLockTimer > 0.0f;
		public bool HasEntropyCurse => EntropyCurseTimer > 0.0f;

		public TargetElementalStatus(Node2D target)
		{
			TargetEntity = target;
		}

		public void Update(float dt)
		{
			if (FireDuration > 0.0f) FireDuration -= dt;
			if (CryoDuration > 0.0f) CryoDuration -= dt;
			if (AcidDuration > 0.0f) AcidDuration -= dt;
			if (VoidDuration > 0.0f) VoidDuration -= dt;

			if (FreezeLockTimer > 0.0f) FreezeLockTimer -= dt;
			if (EntropyCurseTimer > 0.0f) EntropyCurseTimer -= dt;
			if (AcidPoolTimer > 0.0f) AcidPoolTimer -= dt;
		}
	}

	public static class ElementalSynthesisMatrix
	{
		private static readonly Dictionary<ulong, TargetElementalStatus> _statusRegistry = new();

		public static TargetElementalStatus GetOrCreateStatus(Node2D entity)
		{
			ulong id = entity.GetInstanceId();
			if (!_statusRegistry.TryGetValue(id, out var status) || !GodotObject.IsInstanceValid(status.TargetEntity))
			{
				status = new TargetElementalStatus(entity);
				_statusRegistry[id] = status;
			}
			return status;
		}

		public static void ApplyHit(Node2D target, ElementFlags hitElements, float rawDamage, out string reactionLog)
		{
			reactionLog = string.Empty;
			var status = GetOrCreateStatus(target);

			bool hasIncomingFire = hitElements.HasFlag(ElementFlags.Thermal);
			bool hasIncomingCryo = hitElements.HasFlag(ElementFlags.Cryo);
			bool hasIncomingAcid = hitElements.HasFlag(ElementFlags.Acid);
			bool hasIncomingVoid = hitElements.HasFlag(ElementFlags.Void);

			// ------------------------------------------------------------
			// 反应 ①：热冲击 (Thermal Shock = Fire + Cryo)
			// ------------------------------------------------------------
			if ((hasIncomingFire && (status.CryoDuration > 0.0f || hasIncomingCryo)) ||
				(hasIncomingCryo && (status.FireDuration > 0.0f || hasIncomingFire)))
			{
				status.CryoDuration = 0.0f;
				status.FireDuration = 0.0f;

				float maxHp = GetTargetMaxHp(target);
				float trueDamage = Mathf.Max(150.0f, maxHp * 0.08f);

				ApplyDirectTrueDamage(target, trueDamage);
				VfxManager.Instance?.SpawnImpactSparks(target.GlobalPosition, Vector2.Up, Colors.Cyan, isRicochet: false);
				VfxManager.Instance?.SpawnFloatingText(target.GlobalPosition, $"💥 热冲击 碎甲内爆 -{trueDamage:F0}!", Colors.Magenta);
				JuiceManager.Instance?.TriggerHitstop(0.06f, 0.04f);
				JuiceManager.Instance?.AddCameraTrauma(0.55f);

				reactionLog = $"💥 [热冲击] 极寒与热核剧烈反应！造成 {trueDamage:F0} 点真实碎甲伤害！";
				GD.PrintRich($"[color=magenta]{reactionLog}[/color]");
				return;
			}

			// ------------------------------------------------------------
			// 反应 ②：爆燃毒爆 (Acid Combustion = Fire + Acid)
			// ------------------------------------------------------------
			if ((hasIncomingFire && (status.AcidDuration > 0.0f || hasIncomingAcid)) ||
				(hasIncomingAcid && (status.FireDuration > 0.0f || hasIncomingFire)))
			{
				status.AcidDuration = 0.0f;
				status.FireDuration = 0.0f;
				status.AcidPoolTimer = 4.0f;

				VfxManager.Instance?.SpawnAcidPool(target.GlobalPosition, radius: 55.0f, duration: 4.0f);
				VfxManager.Instance?.SpawnModuleExplosion(target.GlobalPosition, new Vector2(48, 48), Colors.LimeGreen, shardCount: 22);
				VfxManager.Instance?.SpawnFloatingText(target.GlobalPosition, "☣️ 爆燃毒爆 4s火海!", Colors.LimeGreen);
				JuiceManager.Instance?.TriggerHitstop(0.05f, 0.05f);
				JuiceManager.Instance?.AddCameraTrauma(0.45f);

				reactionLog = "☣️ [爆燃毒爆] 酸液被瞬间点燃！生成 4 秒生化烈焰腐蚀火海！";
				GD.PrintRich($"[color=green]{reactionLog}[/color]");
				return;
			}

			// ------------------------------------------------------------
			// 反应 ③：绝对零度 (Absolute Zero = Cryo + Void)
			// ------------------------------------------------------------
			if ((hasIncomingCryo && (status.VoidDuration > 0.0f || hasIncomingVoid)) ||
				(hasIncomingVoid && (status.CryoDuration > 0.0f || hasIncomingCryo)))
			{
				status.CryoDuration = 0.0f;
				status.VoidDuration = 0.0f;
				status.FreezeLockTimer = 1.5f;

				VfxManager.Instance?.SpawnImpactSparks(target.GlobalPosition, Vector2.Up, Colors.DeepSkyBlue, isRicochet: true);
				VfxManager.Instance?.SpawnFloatingText(target.GlobalPosition, "❄️ 绝对零度 冰封定身 1.5s!", Colors.Cyan);
				JuiceManager.Instance?.TriggerHitstop(0.08f, 0.02f);
				JuiceManager.Instance?.AddCameraTrauma(0.60f);

				reactionLog = "❄️ [绝对零度] 极寒与虚空坍缩！生成 12m 低温力场，强制熄火定身 1.5 秒！";
				GD.PrintRich($"[color=deep_sky_blue]{reactionLog}[/color]");
				return;
			}

			// ------------------------------------------------------------
			// 反应 ④：熵增噬灭 (Entropy Collapse = Acid + Void)
			// ------------------------------------------------------------
			if ((hasIncomingAcid && (status.VoidDuration > 0.0f || hasIncomingVoid)) ||
				(hasIncomingVoid && (status.AcidDuration > 0.0f || hasIncomingAcid)))
			{
				status.AcidDuration = 0.0f;
				status.VoidDuration = 0.0f;
				status.EntropyCurseTimer = 5.0f;

				VfxManager.Instance?.SpawnElectricArc(target.GlobalPosition, target.GlobalPosition + new Vector2(24, -24), Colors.Purple);
				VfxManager.Instance?.SpawnFloatingText(target.GlobalPosition, "🌌 熵增噬灭 印记植入!", Colors.Purple);
				JuiceManager.Instance?.TriggerHitstop(0.04f, 0.06f);
				JuiceManager.Instance?.AddCameraTrauma(0.40f);

				reactionLog = "🌌 [熵增噬灭] 噬灭印记植入！敌方开火将直接在内部触发 EMP 炸膛反噬！";
				GD.PrintRich($"[color=purple]{reactionLog}[/color]");
				return;
			}

			if (hasIncomingFire) status.FireDuration = 3.0f;
			if (hasIncomingCryo) status.CryoDuration = 3.0f;
			if (hasIncomingAcid) status.AcidDuration = 3.0f;
			if (hasIncomingVoid) status.VoidDuration = 3.0f;
		}

		private static float GetTargetMaxHp(Node2D target)
		{
			if (target is ShipEntity ship)
			{
				float total = 0.0f;
				foreach (var m in ship.Grid.Modules) total += m.MaxHp;
				return Mathf.Max(500.0f, total);
			}
			if (target is TargetDummy dummy)
			{
				return dummy.MaxHp;
			}
			return 1000.0f;
		}

		private static void ApplyDirectTrueDamage(Node2D target, float damage)
		{
			if (target is ShipEntity ship)
			{
				var firstMod = ship.Grid.Modules.FirstOrDefault();
				if (firstMod != null)
				{
					firstMod.CurrentHp = Mathf.Max(0.0f, firstMod.CurrentHp - damage);
					ship.OnModuleDamaged(firstMod, damage);
				}
			}
			else if (target is TargetDummy dummy)
			{
				dummy.TakeDamage(damage, ElementFlags.None);
			}
		}
	}
}
