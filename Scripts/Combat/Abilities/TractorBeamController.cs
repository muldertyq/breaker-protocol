using System.Collections.Generic;
using Godot;
using BreakerProtocol.Audio;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.World.Economy;

namespace BreakerProtocol.Combat.Abilities
{
	/// <summary>
	/// 战舰全自动高能物理牵引光束控制器 (Tractor Beam)
	/// </summary>
	public partial class TractorBeamController : Node2D
	{
		public ShipEntity TargetShip { get; set; } = null!;
		public float MaxRange { get; set; } = 280.0f; // 35 米 = 280 像素
		public float PullAcceleration { get; set; } = 480.0f; // 15 m/s^2 抓取加速度
		public float CaptureRadius { get; set; } = 32.0f;

		private readonly List<SalvageDropEntity> _activeTargets = new();
		private float _animTime = 0.0f;

		public override void _Ready()
		{
			ZIndex = 10;
		}

		public override void _Process(double delta)
		{
			if (TargetShip == null || !GodotObject.IsInstanceValid(TargetShip)) return;

			float dt = (float)delta;
			_animTime += dt * 8.0f;
			_activeTargets.Clear();

			Vector2 shipPos = TargetShip.GlobalPosition;
			var salvageNodes = GetTree().GetNodesInGroup("Salvage");

			foreach (var node in salvageNodes)
			{
				if (node is SalvageDropEntity drop && GodotObject.IsInstanceValid(drop))
				{
					float dist = shipPos.DistanceTo(drop.GlobalPosition);
					if (dist <= MaxRange)
					{
						_activeTargets.Add(drop);
						drop.IsBeingPulled = true;

						// 施加牵引引力拉向战舰中心
						Vector2 pullDir = (shipPos - drop.GlobalPosition).Normalized();
						drop.Velocity += pullDir * PullAcceleration * dt;

						// 距离小于捕获半径，吸附入舱并结算
						if (dist <= CaptureRadius)
						{
							CollectSalvage(drop);
						}
					}
				}
			}

			QueueRedraw();
		}

		private void CollectSalvage(SalvageDropEntity drop)
		{
			switch (drop.Type)
			{
				case SalvageType.Scraps:
					PlayerEconomyManager.Instance.AddScraps(drop.Amount);
					VfxManager.Instance?.SpawnFloatingText(drop.GlobalPosition, $"+{drop.Amount} ⚙ 废料", Colors.Gold);
					break;

				case SalvageType.ComputeCore:
					PlayerEconomyManager.Instance.AddComputeCores(1);
					VfxManager.Instance?.SpawnFloatingText(drop.GlobalPosition, "+1 💠 算力核心！", Colors.Cyan);
					break;

				case SalvageType.WeaponCrate:
					PlayerEconomyManager.Instance.AddScraps(drop.Amount * 2);
					VfxManager.Instance?.SpawnFloatingText(drop.GlobalPosition, "📦 缴获战备物资箱！", Colors.LimeGreen);
					break;
			}

			AudioManager.Instance?.PlaySfx(SoundType.HotwireConnect, 0.15f);
			JuiceManager.Instance?.TriggerExplosionJuice(drop.GlobalPosition, 0.4f);

			drop.QueueFree();
		}

		public override void _Draw()
		{
			if (TargetShip == null || !GodotObject.IsInstanceValid(TargetShip)) return;

			Vector2 shipLocal = ToLocal(TargetShip.GlobalPosition);

			// 绘制流动的牵引能量光束与引力涟漪
			foreach (var drop in _activeTargets)
			{
				if (!GodotObject.IsInstanceValid(drop)) continue;

				Vector2 dropLocal = ToLocal(drop.GlobalPosition);
				Color beamColor = drop.Type == SalvageType.ComputeCore ? Colors.Cyan : Colors.Gold;

				float pulseWidth = 2.0f + Mathf.Sin(_animTime) * 1.0f;
				DrawLine(shipLocal, dropLocal, new Color(beamColor.R, beamColor.G, beamColor.B, 0.4f), pulseWidth + 2.0f);
				DrawLine(shipLocal, dropLocal, Colors.White, 1.2f);

				// 绘制目标处的小引力圈
				DrawCircle(dropLocal, 14.0f + Mathf.Sin(_animTime * 1.5f) * 3.0f, new Color(beamColor.R, beamColor.G, beamColor.B, 0.2f));
			}
		}
	}
}
