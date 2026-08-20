using Godot;
using BreakerProtocol.Ship;

namespace BreakerProtocol.Combat.Armor
{
	/// <summary>
	/// 深空生化角质装甲自愈系统
	/// </summary>
	public class BioArmorRegenSystem
	{
		private readonly ShipEntity _ship;
		private float _timeSinceLastDamage = 0.0f;
		private const float RegenCooldownSeconds = 3.0f; // 脱战 3 秒后开始自愈
		private const float RegenRateHpPerSec = 15.0f;   // 自愈速率 15 HP/s

		public bool IsRegenerating => _timeSinceLastDamage >= RegenCooldownSeconds;

		public BioArmorRegenSystem(ShipEntity ship)
		{
			_ship = ship;
		}

		public void NotifyDamageTaken()
		{
			_timeSinceLastDamage = 0.0f; // 重置脱战冷却
		}

		public void Update(float dt)
		{
			_timeSinceLastDamage += dt;

			if (_timeSinceLastDamage >= RegenCooldownSeconds)
			{
				foreach (var module in _ship.Grid.Modules)
				{
					if (module.IsDestroyed) continue;

					// 仅对生化角质装甲自愈
					if (module.Definition.Faction == "BioChitin" && module.Definition.Category == "Armor")
					{
						if (module.CurrentHp < module.MaxHp)
						{
							module.CurrentHp = Mathf.Min(module.MaxHp, module.CurrentHp + (RegenRateHpPerSec * dt));
						}
					}
				}
			}
		}
	}
}
