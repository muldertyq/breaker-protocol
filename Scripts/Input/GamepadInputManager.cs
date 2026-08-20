using Godot;
using BreakerProtocol.Ship;
using BreakerProtocol.UI.CombatHUD;

namespace BreakerProtocol.PlayerInput
{
	/// <summary>
	/// 双摇杆手柄映射与无障碍色彩总控管理器
	/// </summary>
	public partial class GamepadInputManager : Node
	{
		public ShipEntity? TargetShip { get; set; }
		public CombatHUD? TargetHUD { get; set; }

		public float Deadzone { get; set; } = 0.15f;
		public bool IsGamepadConnected => Godot.Input.GetConnectedJoypads().Count > 0;

		public override void _Process(double delta)
		{
			if (TargetShip == null || !GodotObject.IsInstanceValid(TargetShip)) return;

			HandleGamepadFlightSteering();
		}

		private void HandleGamepadFlightSteering()
		{
			// 1. 左摇杆：四向矢量动力
			float moveX = Godot.Input.GetJoyAxis(0, JoyAxis.LeftX);
			float moveY = Godot.Input.GetJoyAxis(0, JoyAxis.LeftY);
			Vector2 moveVector = new(ApplyDeadzone(moveX), ApplyDeadzone(moveY));

			// 2. 右摇杆：360° 瞄准矢量
			float aimX = Godot.Input.GetJoyAxis(0, JoyAxis.RightX);
			float aimY = Godot.Input.GetJoyAxis(0, JoyAxis.RightY);
			Vector2 aimVector = new(ApplyDeadzone(aimX), ApplyDeadzone(aimY));

			// 3. 扳机与肩键 (RT/R1: 开火 / LT/L1: 推进喷射 / B: 惯性漂移)
			bool isFiring = Godot.Input.GetJoyAxis(0, JoyAxis.TriggerRight) > 0.3f || Godot.Input.IsJoyButtonPressed(0, JoyButton.RightShoulder);
			bool isBoosting = Godot.Input.GetJoyAxis(0, JoyAxis.TriggerLeft) > 0.3f || Godot.Input.IsJoyButtonPressed(0, JoyButton.LeftShoulder);
			bool isDrifting = Godot.Input.IsJoyButtonPressed(0, JoyButton.B);

			if (aimVector.LengthSquared() > 0.1f)
			{
				float targetAngle = aimVector.Angle() + Mathf.Pi * 0.5f;
				TargetShip.Rotation = Mathf.LerpAngle(TargetShip.Rotation, targetAngle, 0.25f);
			}

			if (moveVector.LengthSquared() > 0.05f)
			{
				TargetShip.ApplyCentralForce(moveVector * 180000.0f);
			}

			if (isFiring)
			{
				foreach (var weaponId in TargetShip.Pulses.WeaponBuffers.Keys)
				{
					TargetShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}
		}

		private float ApplyDeadzone(float val)
		{
			return Mathf.Abs(val) < Deadzone ? 0.0f : val;
		}

		/// <summary>
		/// 循环切换色盲模式 (Normal -> Protanopia -> Deuteranopia -> Tritanopia)
		/// </summary>
		public void CycleColorblindMode()
		{
			if (TargetHUD == null) return;

			TargetHUD.CurrentColorblindMode = TargetHUD.CurrentColorblindMode switch
			{
				ColorblindMode.Normal       => ColorblindMode.Protanopia,
				ColorblindMode.Protanopia   => ColorblindMode.Deuteranopia,
				ColorblindMode.Deuteranopia => ColorblindMode.Tritanopia,
				_                           => ColorblindMode.Normal
			};
		}
	}
}
