using Godot;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;

namespace BreakerProtocol.Environment.Hazards
{
	/// <summary>
	/// EMP 电磁脉冲星云风暴实体 (规范 07 / TASK-26)
	/// </summary>
	public partial class EmpNebulaStormEntity : Node2D
	{
		[Export] public float StormRadius { get; set; } = 320.0f; // 星云覆盖半径
		[Export] public float HeatPenetrationRate { get; set; } = 15.0f; // 每秒向舰内渗透的发热量
		[Export] public Color NebulaColor { get; set; } = new(0.15f, 0.65f, 0.95f, 0.35f);

		private float _pulseInterferenceTimer = 0.0f;
		private float _cloudPulseAnim = 0.0f;

		public override void _Ready()
		{
			ZIndex = -2; // 位于深空背景层之上，掩体与战舰之下
			AddToGroup("NebulaHazard");
		}

		public override void _PhysicsProcess(double delta)
		{
			float dt = (float)delta;
			_cloudPulseAnim += dt * 1.5f;

			var ships = GetTree().GetNodesInGroup("Ship");
			foreach (var node in ships)
			{
				if (node is ShipEntity ship)
				{
					float dist = GlobalPosition.DistanceTo(ship.GlobalPosition);
					if (dist < StormRadius)
					{
						// 1. 热量渗透累积
						ship.Thermal?.AddHeat(HeatPenetrationRate * dt);

						// 2. 舰体导线高频漏电电弧
						if (GD.Randf() > 0.70f)
						{
							Vector2 offset = new((float)GD.RandRange(-25, 25), (float)GD.RandRange(-25, 25));
							VfxManager.Instance?.SpawnElectricArc(
								ship.GlobalPosition + offset,
								ship.GlobalPosition + offset + new Vector2(18, -18),
								Colors.Cyan
							);
						}
					}
				}
			}

			// 3. 周期性引发武器终端电磁卡壳 (EMP Jitter)
			_pulseInterferenceTimer += dt;
			if (_pulseInterferenceTimer >= 0.8f)
			{
				_pulseInterferenceTimer = 0.0f;
				foreach (var node in ships)
				{
					if (node is ShipEntity ship && GlobalPosition.DistanceTo(ship.GlobalPosition) < StormRadius)
					{
						if (GD.Randf() > 0.40f)
						{
							VfxManager.Instance?.SpawnFloatingText(ship.GlobalPosition, "⚡ EMP 干扰：武器总线跳闸!", Colors.DodgerBlue);
						}
					}
				}
			}

			QueueRedraw();
		}

		public override void _Draw()
		{
			float pulse = 0.95f + (Mathf.Sin(_cloudPulseAnim) * 0.08f);
			float currentR = StormRadius * pulse;

			// 绘制星云多层等离子体晕层
			DrawCircle(Vector2.Zero, currentR, NebulaColor);
			DrawCircle(Vector2.Zero, currentR * 0.65f, new Color(NebulaColor.R, NebulaColor.G, NebulaColor.B, 0.45f));
			DrawCircle(Vector2.Zero, currentR * 0.30f, new Color(0.8f, 0.95f, 1.0f, 0.55f));

			// 绘制星云闪电边界
			DrawArc(Vector2.Zero, currentR, 0, Mathf.Tau, 48, Colors.Cyan, 2.0f);
		}
	}
}
