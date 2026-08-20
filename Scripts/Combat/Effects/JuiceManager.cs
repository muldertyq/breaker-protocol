using Godot;
using BreakerProtocol.Camera;

namespace BreakerProtocol.Combat.Effects
{
	/// <summary>
	/// 全局打击感调度中枢 (Hitstop 微顿帧、创伤值震屏与方向性冲击)
	/// </summary>
	[GlobalClass]
	public partial class JuiceManager : Node
	{
		public static JuiceManager? Instance { get; private set; }

		private CombatCameraController? _activeCamera;
		
		// 微顿帧状态
		private float _hitstopTimer = 0.0f;
		private bool _isHitstopActive = false;
		private float _targetTimeScale = 1.0f;

		public override void _EnterTree()
		{
			Instance = this;
			ProcessMode = ProcessModeEnum.Always; // 确保在 TimeScale = 0 时仍可正常计时更新
		}

		public void BindCamera(CombatCameraController camera)
		{
			_activeCamera = camera;
		}

		public override void _Process(double delta)
		{
			// 使用不受 TimeScale 影响的真实时间增量 (Real-time delta)
			float realDt = (float)delta / (Engine.TimeScale > 0.0001f ? (float)Engine.TimeScale : 1.0f);

			if (_isHitstopActive)
			{
				_hitstopTimer -= realDt;
				if (_hitstopTimer <= 0.0f)
				{
					_isHitstopActive = false;
					Engine.TimeScale = 1.0f; // 恢复正常物理流速
				}
			}
		}

		// ==========================================
		// 公共打击感触发 API
		// ==========================================

		/// <summary>
		/// 触发微顿帧 (Freeze Frame)
		/// </summary>
		/// <param name="durationSeconds">冻结时长 (例如 0.04s = 40ms)</param>
		/// <param name="slowdownScale">降低到的时间流速 (默认 0.05 极缓流速)</param>
		public void TriggerHitstop(float durationSeconds, float slowdownScale = 0.05f)
		{
			if (durationSeconds <= 0.0f) return;

			// 优先保留更长更强烈的顿帧
			if (!_isHitstopActive || durationSeconds > _hitstopTimer)
			{
				_hitstopTimer = durationSeconds;
				_isHitstopActive = true;
				Engine.TimeScale = slowdownScale;
			}
		}

		/// <summary>
		/// 向摄像机注入创伤值 (Trauma 0.0 ~ 1.0)
		/// </summary>
		public void AddCameraTrauma(float amount)
		{
			_activeCamera?.AddTrauma(amount);
		}

		/// <summary>
		/// 施加方向性受击镜头微冲击
		/// </summary>
		public void ApplyDirectionalKick(Vector2 kickVector)
		{
			_activeCamera?.ApplyDirectionalKick(kickVector);
		}

		/// <summary>
		/// 构件爆甲时的组合打击感大礼包
		/// </summary>
		public void TriggerExplosionJuice(Vector2 impactDir, float intensity = 1.0f)
		{
			TriggerHitstop(0.075f * intensity, slowdownScale: 0.03f);
			AddCameraTrauma(0.70f * intensity);
			ApplyDirectionalKick(impactDir.Normalized() * 25.0f * intensity);
		}

		public override void _ExitTree()
		{
			Engine.TimeScale = 1.0f;
		}
	}
}
