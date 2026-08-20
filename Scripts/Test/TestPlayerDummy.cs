using Godot;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// 白盒测试战舰假人 (含自动分组与推力系统)
	/// </summary>
	public partial class TestPlayerDummy : RigidBody2D
	{
		[Export] public float BaseThrustForce { get; set; } = 1500.0f; // 基础推进力
		[Export] public float BoostMultiplier { get; set; } = 2.2f;    // Shift 氮气加力
		[Export] public float TorqueForce { get; set; } = 5000.0f;     // 转向力矩

		public override void _Ready()
		{
			// 关键：将自己加入 "Player" 组，方便摄像机 100% 自动找到自己
			AddToGroup("Player");

			GravityScale = 0.0f; // 太空零重力
			LinearDamp = 1.5f;   // 巡航阻尼
			AngularDamp = 5.0f;  // 旋转阻尼
		}

		public override void _PhysicsProcess(double delta)
		{
			// 1. 机头朝向鼠标旋转
			Vector2 mousePos = GetGlobalMousePosition();
			Vector2 dirToMouse = (mousePos - GlobalPosition).Normalized();
			float targetAngle = dirToMouse.Angle() + (Mathf.Pi * 0.5f);
			
			float angleDiff = Mathf.Wrap(targetAngle - Rotation, -Mathf.Pi, Mathf.Pi);
			ApplyTorque(angleDiff * TorqueForce);

			// 2. WASD 空间移动
			Vector2 input = Vector2.Zero;
			if (Input.IsKeyPressed(Key.W)) input.Y -= 1.0f;
			if (Input.IsKeyPressed(Key.S)) input.Y += 1.0f;
			if (Input.IsKeyPressed(Key.A)) input.X -= 1.0f;
			if (Input.IsKeyPressed(Key.D)) input.X += 1.0f;

			// 3. Shift 氮气加速与空格漂移
			bool isBoosting = Input.IsKeyPressed(Key.Shift);
			float currentThrust = isBoosting ? (BaseThrustForce * BoostMultiplier) : BaseThrustForce;

			LinearDamp = Input.IsKeyPressed(Key.Space) ? 0.0f : 1.5f;

			if (input != Vector2.Zero)
			{
				Vector2 forward = -Transform.Y;
				Vector2 right = Transform.X;
				Vector2 force = (right * input.X + forward * -input.Y).Normalized() * currentThrust;
				ApplyCentralForce(force);
			}
		}
	}
}
