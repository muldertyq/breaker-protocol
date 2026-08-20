using System;
using Godot;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;

namespace BreakerProtocol.Combat
{
	public enum GateState
	{
		Spawning,      // 展开就绪动画
		IdleActive,    // 等待战舰驶入
		ChargingJump,  // 处于圈内蓄能折跃中
		WarpEngage     // 触发跃迁，战舰吸入切关
	}

	/// <summary>
	/// 超空间跃迁撤离门实体 (全息双旋引力光圈、战舰蓄能判定与跃迁闭环)
	/// </summary>
	public partial class HyperspaceGateEntity : Node2D
	{
		public GateState CurrentState { get; private set; } = GateState.Spawning;
		public float TriggerRadius { get; set; } = 140.0f;
		public float RequiredChargeTime { get; set; } = 2.0f;
		public float CurrentChargeTimer { get; private set; } = 0.0f;

		public ShipEntity? TargetShip { get; set; }

		public event Action? OnJumpSequenceInitiated;
		public event Action? OnGateJumpCompleted;

		private float _animTime = 0.0f;
		private float _spawnProgress = 0.0f;
		private float _warpOutTimer = 0.0f;

		public override void _Ready()
		{
			AddToGroup("HyperspaceGate");
			VfxManager.Instance?.SpawnFloatingText(GlobalPosition, "✦ 超空间跃迁信标已上线 ✦", Colors.Cyan);
			JuiceManager.Instance?.AddCameraTrauma(0.3f);
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;
			_animTime += dt * 3.5f;

			switch (CurrentState)
			{
				case GateState.Spawning:
					_spawnProgress += dt * 2.0f;
					if (_spawnProgress >= 1.0f)
					{
						_spawnProgress = 1.0f;
						CurrentState = GateState.IdleActive;
					}
					break;

				case GateState.IdleActive:
				case GateState.ChargingJump:
					UpdateGateTriggerLogic(dt);
					break;

				case GateState.WarpEngage:
					UpdateWarpOutAnimation(dt);
					break;
			}

			QueueRedraw();
		}

		private void UpdateGateTriggerLogic(float dt)
		{
			if (TargetShip == null || !GodotObject.IsInstanceValid(TargetShip))
			{
				TargetShip = GetTree().GetFirstNodeInGroup("Player") as ShipEntity;
				if (TargetShip == null) return;
			}

			float dist = GlobalPosition.DistanceTo(TargetShip.GlobalPosition);

			if (dist <= TriggerRadius)
			{
				CurrentState = GateState.ChargingJump;
				CurrentChargeTimer += dt;

				// 引力轻微牵引战舰向中心靠拢
				Vector2 pullDir = (GlobalPosition - TargetShip.GlobalPosition).Normalized();
				TargetShip.LinearVelocity += pullDir * (80.0f * dt);
				TargetShip.LinearVelocity *= Mathf.Clamp(1.0f - (0.4f * dt), 0.1f, 1.0f); // 减速稳定

				if (CurrentChargeTimer >= RequiredChargeTime)
				{
					TriggerWarpEngage();
				}
			}
			else
			{
				CurrentState = GateState.IdleActive;
				CurrentChargeTimer = Mathf.Max(0.0f, CurrentChargeTimer - dt * 1.5f); // 离开圈后进度缓慢衰减
			}
		}

		private void TriggerWarpEngage()
		{
			CurrentState = GateState.WarpEngage;
			_warpOutTimer = 0.0f;
			OnJumpSequenceInitiated?.Invoke();

			VfxManager.Instance?.SpawnElectricArc(GlobalPosition - new Vector2(50, 0), GlobalPosition + new Vector2(50, 0), Colors.Cyan);
			JuiceManager.Instance?.AddCameraTrauma(0.6f);
		}

		private void UpdateWarpOutAnimation(float dt)
		{
			_warpOutTimer += dt;

			if (TargetShip != null && GodotObject.IsInstanceValid(TargetShip))
			{
				// 强力吸附战舰至门中心并加速向前弹射
				TargetShip.GlobalPosition = TargetShip.GlobalPosition.Lerp(GlobalPosition, dt * 6.0f);
				TargetShip.Rotation = Mathf.LerpAngle(TargetShip.Rotation, -Mathf.Pi * 0.5f, dt * 8.0f);
			}

			if (_warpOutTimer >= 0.8f)
			{
				OnGateJumpCompleted?.Invoke();
				SetProcess(false);
			}
		}

		public override void _Draw()
		{
			float scale = (CurrentState == GateState.Spawning) ? _spawnProgress : 1.0f;
			float radius = TriggerRadius * scale;

			// 1. 绘制外部范围虚线光圈
			Color boundaryColor = (CurrentState == GateState.ChargingJump) ? Colors.Gold : Colors.Cyan;
			boundaryColor.A = 0.45f + Mathf.Sin(_animTime * 2.0f) * 0.15f;
			DrawArc(Vector2.Zero, radius, 0, Mathf.Tau, 48, boundaryColor, 1.5f);

			// 2. 绘制双旋内环 (Outer & Inner Vortex Rings)
			float ringRadius = radius * 0.65f;
			int segments = 8;
			for (int i = 0; i < segments; i++)
			{
				float angle = (i / (float)segments) * Mathf.Tau + _animTime * 0.8f;
				Vector2 p1 = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
				Vector2 p2 = new Vector2(Mathf.Cos(angle + 0.35f), Mathf.Sin(angle + 0.35f)) * (ringRadius + 8.0f);
				DrawLine(p1, p2, Colors.Cyan, 2.5f);

				float innerAngle = -(i / (float)segments) * Mathf.Tau - _animTime * 1.4f;
				Vector2 ip1 = new Vector2(Mathf.Cos(innerAngle), Mathf.Sin(innerAngle)) * (ringRadius * 0.45f);
				Vector2 ip2 = new Vector2(Mathf.Cos(innerAngle + 0.4f), Mathf.Sin(innerAngle + 0.4f)) * (ringRadius * 0.55f);
				DrawLine(ip1, ip2, Colors.Gold, 2.0f);
			}

			// 3. 中心引力奇点光核
			float pulse = Mathf.Sin(_animTime * 3.0f) * 4.0f;
			DrawCircle(Vector2.Zero, (18.0f + pulse) * scale, new Color(0.1f, 0.8f, 1.0f, 0.4f));
			DrawCircle(Vector2.Zero, 8.0f * scale, Colors.White);

			// 4. 处于充能状态时绘制折跃进度条环与提示
			if (CurrentState == GateState.ChargingJump || CurrentChargeTimer > 0.01f)
			{
				float chargeRatio = Mathf.Clamp(CurrentChargeTimer / RequiredChargeTime, 0.0f, 1.0f);
				float chargeAngle = chargeRatio * Mathf.Tau;

				DrawArc(Vector2.Zero, radius + 12.0f, -Mathf.Pi * 0.5f, -Mathf.Pi * 0.5f + chargeAngle, 36, Colors.Gold, 3.5f);

				var font = ThemeDB.FallbackFont;
				string chargeText = $"✦ 超空间折跃引导中 {(chargeRatio * 100):F0}% ✦";
				DrawString(font, new Vector2(-120, -radius - 22.0f), chargeText, HorizontalAlignment.Center, 240, 12, Colors.Gold);
			}
			else if (CurrentState == GateState.IdleActive)
			{
				var font = ThemeDB.FallbackFont;
				string hintText = "▶ 驶入光圈启动跃迁 ◀";
				DrawString(font, new Vector2(-100, -radius - 20.0f), hintText, HorizontalAlignment.Center, 200, 11, Colors.Cyan);
			}
		}
	}
}
