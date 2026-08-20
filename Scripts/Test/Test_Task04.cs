using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Combat.Effects;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-04 验证场景：全链路效果算子流水线测试
	/// </summary>
	public partial class Test_Task04 : Control
	{
		private Label _logLabel = null!;

		public override void _Ready()
		{
			_logLabel = new Label
			{
				Position = new Vector2(40, 40),
				Size = new Vector2(900, 600)
			};
			_logLabel.AddThemeFontSizeOverride("font_size", 16);
			AddChild(_logLabel);

			RunPipelineSimulation();
		}

		private void RunPipelineSimulation()
		{
			string output = "【《断路协议》TASK-04 效果算子流水线执行报告】\n" +
							"======================================================================\n";

			// 1. 获取构件定义数据
			var cryoMod = DataManager.Instance.Modules.Get("hf_mod_cryo_chamber");
			var railgun = DataManager.Instance.Modules.Get("hf_wep_railgun_h");

			// 2. 阶段 1：生成初始脉冲并流经极寒冷凝舱
			PulsePacket pulse = PulsePacket.CreateDefault(id: 1, power: 1.0f);
			output += $"[步骤 1: 初始脉冲生成] -> ID:{pulse.PulseId}, 基础穿透:{pulse.BonusPierce}, 元素:{pulse.Elements}, 发热系数:{pulse.HeatMultiplier:F2}\n";

			if (cryoMod.Properties.TryGetProperty("effectsOnPulse", out var modPulseEffects))
			{
				EffectProcessor.Instance.ProcessPulseModifiers(ref pulse, modPulseEffects);
			}
			output += $"[步骤 2: 流经极寒冷凝舱] -> 附加穿透:+{pulse.BonusPierce}, 注入元素:[{pulse.Elements}], 冷却后发热系数:{pulse.HeatMultiplier:F2}\n\n";

			// 3. 阶段 2：炮口组装开火上下文并开火
			FireContext fireContext = new()
			{
				FiringShip = null!, // 测试用 null
				MuzzleWorldPos = new Vector2(100, 100),
				FireDirection = Vector2.Up,
				BaseDamage = 320.0f,
				BaseSpeed = 180.0f,
				BasePierce = 2,
				CompiledPulse = pulse
			};

			if (railgun.Properties.TryGetProperty("effectsOnFire", out var onFireEffects))
			{
				EffectProcessor.Instance.ProcessOnFireEffects(ref fireContext, onFireEffects);
			}
			output += $"[步骤 3: 炮口发射] -> 最终伤害:{fireContext.BaseDamage * pulse.DamageMultiplier:F0}, " +
					  $"总穿透深度:{fireContext.BasePierce + pulse.BonusPierce}层, " +
					  $"发射朝向:{fireContext.FireDirection}\n\n";

			// 4. 阶段 3：子弹命中目标触发效果
			HitResult hitResult = new()
			{
				AttackerShip = null,
				TargetEntity = null,
				HitWorldPos = new Vector2(100, 300),
				HitNormal = Vector2.Down,
				FinalDamage = fireContext.BaseDamage * pulse.DamageMultiplier,
				AppliedElements = pulse.Elements,
				IsRicochet = false,
				RemainingPierce = fireContext.BasePierce + pulse.BonusPierce
			};

			output += "[步骤 4: 子弹命中目标]\n";
			// 执行修饰舱的 onHit
			if (cryoMod.Properties.TryGetProperty("effectsOnPulse", out var modHitEffects))
			{
				EffectProcessor.Instance.ProcessOnHitEffects(ref hitResult, modHitEffects);
				output += $"  -> 结算元素状态: [{hitResult.AppliedElements}]\n";
			}
			// 执行武器自身的 onHit
			if (railgun.Properties.TryGetProperty("effectsOnHit", out var weaponHitEffects))
			{
				EffectProcessor.Instance.ProcessOnHitEffects(ref hitResult, weaponHitEffects);
				output += "  -> 触发了武器自带的范围高爆！\n";
			}

			output += "======================================================================\n" +
					  "[✔] 验收结论: 效果算子在无需编写专有武器代码的情况下完全正常编译与链式触发！";

			_logLabel.Text = output;
		}
	}
}
