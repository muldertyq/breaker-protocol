using System;
using Godot;

namespace BreakerProtocol.Ship.Thermal
{
	/// <summary>
	/// 全舰热力学仿真系统 (规范 03《管线发热》与规范 09)
	/// </summary>
	public class ThermalSystem
	{
		private readonly ShipEntity _ship;

		/// <summary>
		/// 全舰最大热容量 (默认 100.0)
		/// </summary>
		public float HeatCapacity { get; set; } = 100.0f;

		/// <summary>
		/// 当前累积热量 Q(t)
		/// </summary>
		public float CurrentHeat { get; private set; } = 0.0f;

		/// <summary>
		/// 基础深空辐射散热速率 (热量/秒，默认 18.0)
		/// </summary>
		public float CoolingRate { get; set; } = 18.0f;

		/// <summary>
		/// 是否处于过热强制熔断停火状态 (Lockout)
		/// </summary>
		public bool IsOverheated { get; private set; } = false;

		/// <summary>
		/// 熔断冷却倒计时 (达到 100% 强制冷却 3.0 秒)
		/// </summary>
		public float LockoutTimer { get; private set; } = 0.0f;

		/// <summary>
		/// 归一化发热比例 (0.0 ~ 1.0)，直接驱动 Shader 的 u_overheat_ratio
		/// </summary>
		public float OverheatRatio => Mathf.Clamp(CurrentHeat / Mathf.Max(1.0f, HeatCapacity), 0.0f, 1.0f);

		/// <summary>
		/// 是否处于 90% 高温预警状态
		/// </summary>
		public bool IsWarning => OverheatRatio >= 0.90f;

		public event Action? OnOverheatTriggered;
		public event Action? OnOverheatRecovered;

		public ThermalSystem(ShipEntity ship)
		{
			_ship = ship;
		}

		/// <summary>
		/// 注入发热量 (开火、脉冲过载或遭受热能武器攻击)
		/// </summary>
		public void AddHeat(float heatAmount)
		{
			if (IsOverheated) return;

			CurrentHeat = Mathf.Clamp(CurrentHeat + heatAmount, 0.0f, HeatCapacity);

			// 热量达到 100% 触发强制熔断
			if (CurrentHeat >= HeatCapacity && !IsOverheated)
			{
				TriggerOverheatLockout();
			}
		}

		private void TriggerOverheatLockout()
		{
			IsOverheated = true;
			LockoutTimer = 3.0f; // 强制停火 3.0 秒
			OnOverheatTriggered?.Invoke();

			Combat.Effects.VfxManager.Instance?.SpawnFloatingText(
				_ship.GlobalPosition,
				"🔥 全舰过热强制熔断停火 3.0s!",
				Colors.OrangeRed
			);
			Combat.Effects.JuiceManager.Instance?.TriggerHitstop(0.06f, 0.05f);
			Combat.Effects.JuiceManager.Instance?.AddCameraTrauma(0.40f);

			GD.PrintRich("[color=red][ThermalSystem] 🚨 全舰热量达到 100%！触发热熔断保护，武器强制断电停火 3.0 秒！[/color]");
		}

		public void Update(float dt)
		{
			if (IsOverheated)
			{
				LockoutTimer -= dt;
				// 熔断期间以 1.5 倍极速被动排热
				CurrentHeat = Mathf.Max(0.0f, CurrentHeat - (CoolingRate * 1.5f * dt));

				if (LockoutTimer <= 0.0f && CurrentHeat <= HeatCapacity * 0.30f)
				{
					IsOverheated = false;
					OnOverheatRecovered?.Invoke();
					Combat.Effects.VfxManager.Instance?.SpawnFloatingText(
						_ship.GlobalPosition,
						"❄️ 散热完毕，武器系统重启",
						Colors.Cyan
					);
					GD.PrintRich("[color=green][ThermalSystem] ❄️ 热量降至安全水平 (30% 以下)，熔断解除，武器恢复就绪！[/color]");
				}
			}
			else
			{
				// 常规自然深空散热
				if (CurrentHeat > 0.0f)
				{
					CurrentHeat = Mathf.Max(0.0f, CurrentHeat - (CoolingRate * dt));
				}
			}
		}

		public void Reset()
		{
			CurrentHeat = 0.0f;
			IsOverheated = false;
			LockoutTimer = 0.0f;
		}
	}
}
