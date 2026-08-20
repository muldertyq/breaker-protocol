using Godot;
using BreakerProtocol.Combat.Boss;

namespace BreakerProtocol.UI.Boss
{
	/// <summary>
	/// 战役级 Boss 顶部三段式血条与状态遥测 HUD
	/// </summary>
	public partial class BossHealthBarUI : Control
	{
		private TitanForgeBossController? _bossController;

		private Label _titleLabel = null!;
		private ProgressBar _healthProgressBar = null!;
		private RichTextLabel _phaseLabel = null!;
		private RichTextLabel _countdownLabel = null!;

		public override void _Ready()
		{
			SetAnchorsPreset(LayoutPreset.TopWide);
			CustomMinimumSize = new Vector2(800, 95);
			MouseFilter = MouseFilterEnum.Ignore;

			// 1. Boss 尊号标头
			_titleLabel = new Label
			{
				Text = "【重工移动要塞 · 泰坦熔炉】 TITAN FORGE - HEAVY FOUNDRY DREADNOUGHT",
				HorizontalAlignment = HorizontalAlignment.Center,
				Position = new Vector2(0, 12),
				Size = new Vector2(1280, 25)
			};
			_titleLabel.AddThemeFontSizeOverride("font_size", 16);
			_titleLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.2f));
			AddChild(_titleLabel);

			// 2. 主血条
			_healthProgressBar = new ProgressBar
			{
				Position = new Vector2(240, 42),
				Size = new Vector2(800, 22),
				MinValue = 0,
				MaxValue = 100,
				Value = 100,
				ShowPercentage = false
			};
			AddChild(_healthProgressBar);

			// 3. 阶段与倒计时状态 (使用 RichTextLabel 解析色彩)
			_phaseLabel = new RichTextLabel
			{
				Position = new Vector2(245, 68),
				Size = new Vector2(500, 24),
				BbcodeEnabled = true,
				MouseFilter = MouseFilterEnum.Ignore
			};
			_phaseLabel.AddThemeFontSizeOverride("normal_font_size", 14);
			AddChild(_phaseLabel);

			_countdownLabel = new RichTextLabel
			{
				Position = new Vector2(740, 68),
				Size = new Vector2(300, 24),
				BbcodeEnabled = true,
				MouseFilter = MouseFilterEnum.Ignore
			};
			_countdownLabel.AddThemeFontSizeOverride("normal_font_size", 14);
			AddChild(_countdownLabel);
		}

		public void BindBoss(TitanForgeBossController boss)
		{
			_bossController = boss;
		}

		public override void _Process(double delta)
		{
			if (_bossController == null || !GodotObject.IsInstanceValid(_bossController.BossShip))
			{
				Visible = false;
				return;
			}

			Visible = true;
			float hpRatio = _bossController.GetHpRatio();
			_healthProgressBar.Value = hpRatio * 100.0f;

			switch (_bossController.CurrentPhase)
			{
				case BossPhase.Phase1_Fortress:
					_phaseLabel.Text = "[color=yellow]PHASE 1: 重型防御要塞 (外覆穿浪装甲壳)[/color]";
					_countdownLabel.Text = "[color=gray][right]动力炉稳定[/right][/color]";
					break;

				case BossPhase.Phase2_EscortSeparation:
					_phaseLabel.Text = "[color=cyan]PHASE 2: 浮游子舰分离 · 核心过载旋转弹幕[/color]";
					_countdownLabel.Text = "[color=cyan][right]⚠️ 核心裸露易伤[/right][/color]";
					break;

				case BossPhase.Phase3_BerserkRamming:
					_phaseLabel.Text = "[color=red]PHASE 3: 动力炉熔毁狂暴冲撞！[/color]";
					_countdownLabel.Text = $"[color=red][right]🚨 自毁倒计时: {_bossController.MeltdownTimer:F1}s[/right][/color]";
					break;

				case BossPhase.Defeated:
					_phaseLabel.Text = "[color=gold]STATUS: 要塞已击溃[/color]";
					_countdownLabel.Text = string.Empty;
					break;
			}
		}
	}
}
