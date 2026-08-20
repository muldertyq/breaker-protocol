using Godot;
using BreakerProtocol.Core;

namespace BreakerProtocol.Ship.Physics
{
	/// <summary>
	/// 飞控运行模式
	/// </summary>
	public enum FlightAssistMode
	{
		CruiseAssist,   // 巡航辅助：自动反向推力纠偏制动 (默认)
		NewtonianDrift  // 纯牛顿漂移：惯性阻尼归零，航向与航速解耦 (按住空格)
	}

	/// <summary>
	/// 三轴联动飞控系统组件 (同时支持玩家键鼠与敌方 AI 战术驱动)
	/// </summary>
	public class FlightController
	{
		private readonly ShipEntity _ship;
		public ShipThrustCapability ThrustCapability { get; private set; }

		public FlightAssistMode AssistMode { get; private set; } = FlightAssistMode.CruiseAssist;

		/// <summary>
		/// 漂移滑移角 (度, 0° 表示机头正对前进方向，90° 表示完全横向侧滑)
		/// </summary>
		public float SlipAngleDegrees { get; private set; }

		/// <summary>
		/// 是否处于氮气加力状态
		/// </summary>
		public bool IsBoosting { get; private set; }

		public FlightController(ShipEntity ship)
		{
			_ship = ship;
			RefreshThrusterCapabilities();
		}

		/// <summary>
		/// 重新解算推进器能力
		/// </summary>
		public void RefreshThrusterCapabilities()
		{
			ThrustCapability = ThrusterSolver.Solve(_ship.Grid);
		}

		/// <summary>
		/// 物理更新轮询 (在 ShipEntity 的 _PhysicsProcess 中调用)
		/// </summary>
		public void PhysicsUpdate(double delta)
		{
			float dt = (float)delta;

			Vector2 input = Vector2.Zero;
			Vector2 targetAimWorldPos = Vector2.Zero;
			bool isDriftHeld = false;

			// ============================================================
			// 1. 读取操作输入 (兼容：玩家键鼠 OR 敌方 AI)
			// ============================================================
			if (_ship.IsInGroup("Player"))
			{
				if (Input.IsKeyPressed(Key.W)) input.Y -= 1.0f; // 前进
				if (Input.IsKeyPressed(Key.S)) input.Y += 1.0f; // 后退
				if (Input.IsKeyPressed(Key.A)) input.X -= 1.0f; // 左平移
				if (Input.IsKeyPressed(Key.D)) input.X += 1.0f; // 右平移

				IsBoosting = Input.IsKeyPressed(Key.Shift);
				isDriftHeld = Input.IsKeyPressed(Key.Space);
				targetAimWorldPos = _ship.GetGlobalMousePosition();
			}
			else if (_ship.AI != null)
			{
				input = _ship.AI.MoveInput;
				IsBoosting = _ship.AI.IsBoosting;
				isDriftHeld = _ship.AI.IsDrift;
				targetAimWorldPos = _ship.AI.AimTargetWorldPos;
			}

			AssistMode = isDriftHeld ? FlightAssistMode.NewtonianDrift : FlightAssistMode.CruiseAssist;

			// 2. 模式与阻尼状态切换
			if (AssistMode == FlightAssistMode.NewtonianDrift)
			{
				_ship.LinearDamp = 0.0f; // 纯惯性滑行
			}
			else
			{
				_ship.LinearDamp = 1.2f; // 巡航抓地阻尼
			}

			// 3. 机体坐标轴向量
			Vector2 forward = -_ship.Transform.Y; // 舰首正前方向
			Vector2 right = _ship.Transform.X;   // 右舷正右方向

			// 4. 推进力合成与应用
			float forwardThrust = ThrustCapability.MaxForwardThrust * (IsBoosting ? ThrustCapability.BoostFactor : 1.0f);
			float reverseThrust = ThrustCapability.MaxReverseThrust;
			float strafeThrust = ThrustCapability.MaxStrafeThrust;

			Vector2 appliedForce = Vector2.Zero;

			if (input.Y < 0) // 前进
			{
				appliedForce += forward * (-input.Y * forwardThrust);
			}
			else if (input.Y > 0) // 后退制动
			{
				appliedForce += -forward * (input.Y * reverseThrust);
			}

			if (input.X != 0) // 左右平移
			{
				appliedForce += right * (input.X * strafeThrust);
			}

			if (appliedForce != Vector2.Zero)
			{
				_ship.ApplyCentralForce(appliedForce);
			}

			// 5. 航向追踪与 PD 扭矩解算
			if (targetAimWorldPos != Vector2.Zero)
			{
				Vector2 dirToTarget = (targetAimWorldPos - _ship.GlobalPosition).Normalized();
				float targetAngle = dirToTarget.Angle() + (Mathf.Pi * 0.5f);
				float angleDiff = Mathf.Wrap(targetAngle - _ship.Rotation, -Mathf.Pi, Mathf.Pi);

				// 比例-微分 (PD) 转向控制力矩
				float pTorque = angleDiff * ThrustCapability.MaxAngularTorque;
				float dTorque = -_ship.AngularVelocity * (ThrustCapability.MaxAngularTorque * 0.15f);
				_ship.ApplyTorque(pTorque + dTorque);
			}

			// 6. 计算漂移滑移角 Slip Angle β = ∠(Velocity, Heading)
			Vector2 velocity = _ship.LinearVelocity;
			if (velocity.Length() > 20.0f)
			{
				float forwardDotVel = forward.Dot(velocity.Normalized());
				forwardDotVel = Mathf.Clamp(forwardDotVel, -1.0f, 1.0f);
				SlipAngleDegrees = Mathf.RadToDeg(Mathf.Acos(forwardDotVel));
			}
			else
			{
				SlipAngleDegrees = 0.0f;
			}

			// 7. 更新各推进器油门与尾焰状态
			UpdateThrusterThrottleStates(input);
		}

		private void UpdateThrusterThrottleStates(Vector2 input)
		{
			foreach (var thruster in ThrustCapability.Thrusters)
			{
				// 如果是后向主推进器
				if (thruster.ThrustDirectionVector == Vector2.Up)
				{
					if (input.Y < 0) // 按住前进
					{
						thruster.CurrentThrottle = IsBoosting ? 1.5f : 1.0f;
					}
					else
					{
						thruster.CurrentThrottle = 0.0f;
					}
				}
				else // RCS 姿态微喷
				{
					bool isMoving = input != Vector2.Zero;
					bool isTurning = Mathf.Abs(_ship.AngularVelocity) > 0.5f;
					thruster.CurrentThrottle = (isMoving || isTurning) ? 0.8f : 0.0f;
				}
			}
		}
	}
}
