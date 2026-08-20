using System;
using Godot;

namespace BreakerProtocol.Core
{
	/// <summary>
	/// 全局场景/状态无缝转场过渡中枢 (单例模式)
	/// </summary>
	public partial class SceneTransitionManager : CanvasLayer
	{
		private static SceneTransitionManager? _instance;
		public static SceneTransitionManager Instance => _instance!;

		private ColorRect _fadeOverlay = null!;
		private RichTextLabel _loadingLabel = null!;
		private Tween? _activeTween;

		public bool IsTransitioning { get; private set; } = false;

		public override void _Ready()
		{
			_instance = this;
			Layer = 128;

			Vector2 vpSize = GetViewport().GetVisibleRect().Size;

			// 1. 全屏黑色暗化遮罩
			_fadeOverlay = new ColorRect
			{
				Color = new Color(0.01f, 0.02f, 0.05f, 0.0f),
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Size = vpSize,
				CustomMinimumSize = vpSize
			};
			AddChild(_fadeOverlay);

			// 2. 转场提示文字居中
			_loadingLabel = new RichTextLabel
			{
				BbcodeEnabled = true,
				Text = "[center][b][color=cyan]✦ 超空间跳跃折跃中... ✦[/color][/b][/center]",
				FitContent = true,
				CustomMinimumSize = new Vector2(600, 50),
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Modulate = new Color(1, 1, 1, 0.0f)
			};
			_loadingLabel.Position = new Vector2((vpSize.X - 600) * 0.5f, vpSize.Y * 0.5f - 25);
			AddChild(_loadingLabel);
		}

		public void Transition(Action onBlackScreen, float fadeDuration = 0.35f, string? customHint = null)
		{
			if (IsTransitioning) return;
			IsTransitioning = true;

			Vector2 vpSize = GetViewport().GetVisibleRect().Size;
			_fadeOverlay.Size = vpSize;
			_fadeOverlay.CustomMinimumSize = vpSize;
			_loadingLabel.Position = new Vector2((vpSize.X - 600) * 0.5f, vpSize.Y * 0.5f - 25);

			_fadeOverlay.MouseFilter = Control.MouseFilterEnum.Stop;
			_loadingLabel.Text = $"[center][b][color=cyan]{(string.IsNullOrEmpty(customHint) ? "✦ 超空间折跃解算中... ✦" : customHint)}[/color][/b][/center]";

			_activeTween?.Kill();
			_activeTween = CreateTween();
			_activeTween.SetParallel(true);

			// Phase 1: 暗化渐入
			_activeTween.TweenProperty(_fadeOverlay, "color:a", 1.0f, fadeDuration)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_activeTween.TweenProperty(_loadingLabel, "modulate:a", 1.0f, fadeDuration * 0.8f);

			// Phase 2: 执行切换
			_activeTween.Chain().TweenCallback(Callable.From(() =>
			{
				onBlackScreen?.Invoke();
			}));

			// Phase 3: 渐出恢复
			_activeTween.Chain().TweenProperty(_fadeOverlay, "color:a", 0.0f, fadeDuration)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.In);
			_activeTween.TweenProperty(_loadingLabel, "modulate:a", 0.0f, fadeDuration * 0.6f);

			_activeTween.Chain().TweenCallback(Callable.From(() =>
			{
				_fadeOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
				IsTransitioning = false;
			}));
		}
	}
}
