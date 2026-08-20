using Godot;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Ship;

namespace BreakerProtocol.Combat.Abilities
{
	/// <summary>
	/// 虚空战术爆甲产生的微型引力黑洞实体 (具备向外抛射、弹幕吞噬与敌舰聚怪绞杀)
	/// </summary>
	public partial class VoidSingularityEntity : Node2D
	{
		public float RemainingDuration { get; set; } = 2.0f;
		public float PullRadiusPixels { get; set; } = 420.0f;
		public float PullForce { get; set; } = 1500.0f;
		public Vector2 Velocity { get; set; } = Vector2.Zero;
		public Node2D? OwnerShip { get; set; }

		private float _damageTickTimer = 0.0f;
		private float _rotationAngle = 0.0f;

		public override void _Ready()
		{
			ZIndex = 8;
			JuiceManager.Instance?.AddCameraTrauma(0.50f);
		}

		public override void _PhysicsProcess(double delta)
		{
			float dt = (float)delta;
			RemainingDuration -= dt;
			_rotationAngle += dt * 8.0f;

			// 1. 抛射滑行与阻尼减速
			GlobalPosition += Velocity * dt;
			Velocity = Velocity.Lerp(Vector2.Zero, dt * 2.5f);

			if (RemainingDuration <= 0.0f)
			{
				// 坍缩消散时产生一次向外的 EMP 冲击波
				VfxManager.Instance?.SpawnElectricArc(GlobalPosition, GlobalPosition + new Vector2(35, 35), Colors.Purple);
				VfxManager.Instance?.SpawnFloatingText(GlobalPosition, "🌌 奇点坍缩湮灭", Colors.MediumPurple);
				JuiceManager.Instance?.TriggerExplosionJuice(GlobalPosition, intensity: 0.8f);
				QueueFree();
				return;
			}

			// 2. 引力拉拽周围敌方刚体 (对母舰施加安全反斥，防止被拉撞)
			var bodies = GetTree().GetNodesInGroup("Ship");
			foreach (var node in bodies)
			{
				if (node is RigidBody2D rb)
				{
					float dist = GlobalPosition.DistanceTo(rb.GlobalPosition);

					if (rb == OwnerShip)
					{
						// 母舰安全保护：若距离过近，施加向外排斥推力，确保玩家不被卷入
						if (dist < 180.0f)
						{
							Vector2 pushDir = (rb.GlobalPosition - GlobalPosition).Normalized();
							rb.ApplyCentralForce(pushDir * 800.0f);
						}
					}
					else if (dist < PullRadiusPixels && dist > 10.0f)
					{
						// 强力拉拽敌方战舰进入黑洞核心
						Vector2 pullDir = (GlobalPosition - rb.GlobalPosition).Normalized();
						float strength = (1.0f - (dist / PullRadiusPixels)) * PullForce;
						rb.ApplyCentralForce(pullDir * strength);
					}
				}
			}

			// 3. 黑洞视界吞噬敌方子弹 (Event Horizon Bullet Absorption)
			var projectiles = GetTree().GetNodesInGroup("Projectile");
			foreach (var pNode in projectiles)
			{
				if (pNode is ProjectileEntity proj && proj.AttackerShip != OwnerShip)
				{
					if (GlobalPosition.DistanceTo(proj.GlobalPosition) < 140.0f)
					{
						VfxManager.Instance?.SpawnElectricArc(proj.GlobalPosition, GlobalPosition, Colors.MediumPurple);
						proj.QueueFree(); // 吞噬销毁敌方子弹
					}
				}
			}

			// 4. 周期性高维湮灭伤害
			_damageTickTimer += dt;
			if (_damageTickTimer >= 0.20f)
			{
				_damageTickTimer = 0.0f;
				foreach (var node in bodies)
				{
					if (node is ShipEntity targetShip && targetShip != OwnerShip)
					{
						if (GlobalPosition.DistanceTo(targetShip.GlobalPosition) < 140.0f)
						{
							ElementalSynthesisMatrix.ApplyHit(targetShip, ElementFlags.Void, 60.0f, out _);
						}
					}
				}
			}

			QueueRedraw();
		}

		public override void _Draw()
		{
			float alpha = Mathf.Clamp(RemainingDuration / 2.0f, 0.2f, 1.0f);
			
			// 绘制中心黑洞核心
			DrawCircle(Vector2.Zero, 16.0f, Colors.Black);
			DrawArc(Vector2.Zero, 17.0f, 0, Mathf.Tau, 32, new Color(0.85f, 0.25f, 1.0f, alpha), 3.0f);

			// 绘制旋转引力光环
			for (int i = 0; i < 3; i++)
			{
				float ringRadius = 28.0f + (i * 26.0f);
				float offset = _rotationAngle * (i % 2 == 0 ? 1.0f : -1.0f) + (i * 1.5f);
				DrawArc(Vector2.Zero, ringRadius, offset, offset + 2.8f, 24, new Color(0.55f, 0.15f, 0.95f, alpha * 0.75f), 2.5f);
			}

			// 绘制吸附外边界光圈
			DrawArc(Vector2.Zero, PullRadiusPixels, 0, Mathf.Tau, 48, new Color(0.6f, 0.2f, 1.0f, 0.15f * alpha), 1.5f);
		}
	}
}
