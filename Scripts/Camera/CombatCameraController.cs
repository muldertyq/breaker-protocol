using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Utils;

namespace BreakerProtocol.Camera
{
	public enum CameraZoomTier
	{
		WiringMode,   // 1.5x: Tab 键装配/走线微观视图
		Standard,     // 1.0x: 常规交火视口
		Tactical,     // 0.7x: 高速战术拉远
		CapitalBoss   // 0.5x: 巨舰决战
	}

	/// <summary>
	/// 战术战斗摄像机控制器 (集成 4 阶缩放、前瞻瞄准与 Trauma 创伤震颤系统)
	/// </summary>
	[GlobalClass]
	public partial class CombatCameraController : Camera2D
	{
		[ExportGroup("追踪目标与权重 (Lookahead)")]
		[Export] public Node2D? TargetShip { get; set; }

		[Export] public float VelocityLookaheadTime { get; set; } = 0.40f;
		[Export] public float CursorLookaheadWeight { get; set; } = 0.20f;

		[ExportGroup("平滑阻尼时间 (Damping)")]
		[Export] public float PositionSmoothTime { get; set; } = 0.08f;
		[Export] public float ZoomSmoothTime { get; set; } = 0.18f;

		[ExportGroup("自适应缩放阈值 (px/s)")]
		[Export] public float SpeedZoomOutThreshold { get; set; } = 300.0f;
		[Export] public float SpeedZoomInThreshold { get; set; } = 180.0f;

		[ExportGroup("创伤震颤参数 (Trauma Shake)")]
		[Export] public float MaxShakeOffsetPixels { get; set; } = 28.0f; // 最大震屏位移
		[Export] public float MaxShakeRotationDeg { get; set; } = 3.5f;   // 最大震屏旋转角
		[Export] public float TraumaDecayRate { get; set; } = 1.4f;       // 创伤值衰减速度 (每秒)

		private CameraZoomTier _currentZoomTier = CameraZoomTier.Standard;
		private Vector2 _currentVelocity = Vector2.Zero;
		private float _currentZoomVelocity = 0.0f;

		private bool _isOverrideZoomActive = false;
		private float _overrideZoomValue = 1.0f;

		// 创伤值状态 (0.0 ~ 1.0)
		public float CurrentTrauma { get; private set; } = 0.0f;
		private Vector2 _directionalKick = Vector2.Zero;
		private float _noiseTime = 0.0f;

		private Label? _debugLabel;

		public override void _Ready()
		{
			Enabled = true;
			PositionSmoothingEnabled = false;
			MakeCurrent();

			FindAndBindTarget();
			CreateDebugHUD();

			// 注册绑定至 JuiceManager
			Combat.Effects.JuiceManager.Instance?.BindCamera(this);
		}

		public void AddTrauma(float amount)
		{
			CurrentTrauma = Mathf.Clamp(CurrentTrauma + amount, 0.0f, 1.0f);
		}

		public void ApplyDirectionalKick(Vector2 kickVector)
		{
			_directionalKick += kickVector;
		}

		public override void _PhysicsProcess(double delta)
		{
			if (TargetShip == null || !IsInstanceValid(TargetShip))
			{
				FindAndBindTarget();
				if (TargetShip == null) return;
			}

			float dt = (float)delta;

			// 1. 读取飞船物理线速度
			Vector2 shipPos = TargetShip.GlobalPosition;
			Vector2 shipVelocity = Vector2.Zero;
			if (TargetShip is RigidBody2D rb)
			{
				shipVelocity = rb.LinearVelocity;
			}

			// 2. 自适应缩放状态机
			float currentSpeed = shipVelocity.Length();
			if (!_isOverrideZoomActive)
			{
				if (_currentZoomTier == CameraZoomTier.Standard && currentSpeed > SpeedZoomOutThreshold)
				{
					_currentZoomTier = CameraZoomTier.Tactical;
				}
				else if (_currentZoomTier == CameraZoomTier.Tactical && currentSpeed < SpeedZoomInThreshold)
				{
					_currentZoomTier = CameraZoomTier.Standard;
				}
			}

			// 3. 前瞻中心计算
			Vector2 mouseWorldPos = GetGlobalMousePosition();
			Vector2 velocityOffset = shipVelocity * VelocityLookaheadTime;
			Vector2 cursorOffset = (mouseWorldPos - shipPos) * CursorLookaheadWeight;
			Vector2 targetPos = shipPos + velocityOffset + cursorOffset;

			// 4. 临界阻尼平滑追踪
			GlobalPosition = MathUtils.SmoothDampVec2(
				GlobalPosition,
				targetPos,
				ref _currentVelocity,
				PositionSmoothTime,
				dt
			);

			// 5. 缩放平滑
			float targetZoomVal = _isOverrideZoomActive ? _overrideZoomValue : GetTargetZoomScale(_currentZoomTier);
			float newZoom = MathUtils.SmoothDamp(
				Zoom.X,
				targetZoomVal,
				ref _currentZoomVelocity,
				ZoomSmoothTime,
				dt
			);
			Zoom = new Vector2(newZoom, newZoom);

			// 6. Trauma 创伤值震屏解算 (Jeremy Stewart 创伤平方模型)
			UpdateCameraShake(dt);

			// 7. 调试信息刷新
			UpdateDebugHUD(currentSpeed, velocityOffset.Length(), cursorOffset.Length());
		}

		private void UpdateCameraShake(float dt)
		{
			if (CurrentTrauma > 0.0f || _directionalKick.LengthSquared() > 0.1f)
			{
				_noiseTime += dt * 35.0f; // 高频震荡

				// Shake = Trauma^2 (产生非线性重打击冲击)
				float shakeFactor = CurrentTrauma * CurrentTrauma;

				float offsetX = (Mathf.Sin(_noiseTime * 1.7f) + Mathf.Sin(_noiseTime * 2.3f) * 0.5f) * MaxShakeOffsetPixels * shakeFactor;
				float offsetY = (Mathf.Cos(_noiseTime * 1.9f) + Mathf.Cos(_noiseTime * 2.7f) * 0.5f) * MaxShakeOffsetPixels * shakeFactor;
				float rotAngle = Mathf.Sin(_noiseTime * 1.3f) * Mathf.DegToRad(MaxShakeRotationDeg) * shakeFactor;

				// 叠加方向性冲击与弹性回弹
				_directionalKick = _directionalKick.Lerp(Vector2.Zero, dt * 10.0f);

				Offset = new Vector2(offsetX, offsetY) + _directionalKick;
				Rotation = rotAngle;

				// 创伤值自然衰减
				CurrentTrauma = Mathf.Max(0.0f, CurrentTrauma - (TraumaDecayRate * dt));
			}
			else
			{
				Offset = Vector2.Zero;
				Rotation = 0.0f;
			}
		}

		public void SetOverrideZoom(bool active, float zoomValue = 1.0f)
		{
			_isOverrideZoomActive = active;
			_overrideZoomValue = zoomValue;
		}

		private void FindAndBindTarget()
		{
			if (TargetShip != null && IsInstanceValid(TargetShip)) return;

			var playerNode = GetTree().GetFirstNodeInGroup("Player") as Node2D;
			if (playerNode != null)
			{
				TargetShip = playerNode;
			}
		}

		private float GetTargetZoomScale(CameraZoomTier tier)
		{
			return tier switch
			{
				CameraZoomTier.WiringMode => 1.5f,
				CameraZoomTier.Standard => 1.0f,
				CameraZoomTier.Tactical => 0.7f,
				CameraZoomTier.CapitalBoss => 0.5f,
				_ => 1.0f
			};
		}

		private void CreateDebugHUD()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_debugLabel = new Label { Position = new Vector2(20, 20) };
			_debugLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f, 1.0f));
			_debugLabel.AddThemeFontSizeOverride("font_size", 16);
			canvasLayer.AddChild(_debugLabel);
		}

		private void UpdateDebugHUD(float speedPx, float velOffset, float curOffset)
		{
			if (_debugLabel == null) return;
			float speedMeters = GlobalMetrics.PixelsToMeters(speedPx);
			_debugLabel.Text = $"【《断路协议》TASK-18 打击感与震屏遥测】\n" +
							   $"----------------------------------------\n" +
							   $"当前航速: {speedMeters:F1} m/s | 视口缩放: {Zoom.X:F2}x\n" +
							   $"创伤震颤 (Trauma): {CurrentTrauma * 100:F0}%\n" +
							   $"微顿流速 (TimeScale): {Engine.TimeScale:F2}x\n" +
							   $"震屏位移: ({Offset.X:F1}px, {Offset.Y:F1}px)\n" +
							   $"----------------------------------------\n" +
							   $"[操作] 左键开火射击 | WASD 推进";
		}
	}
}
