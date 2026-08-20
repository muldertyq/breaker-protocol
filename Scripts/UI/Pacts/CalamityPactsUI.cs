using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.World.Pacts;

namespace BreakerProtocol.UI.Pacts
{
	/// <summary>
	/// 灾厄契约全息交互面板 (TASK-39：完全由 CalamityPactDef 数据驱动)
	/// </summary>
	public partial class CalamityPactsUI : CanvasLayer
	{
		private PanelContainer _mainPanel = null!;
		private HBoxContainer _cardsContainer = null!;
		private Button _skipButton = null!;
		private Button _confirmButton = null!;

		public event Action<CalamityPactDef>? OnPactSelected;
		public event Action? OnStartWithPacts; // 兼容 Task-38 的出击事件
		public event Action? OnSkipped;

		public override void _Ready()
		{
			BuildInterface();
			Visible = false;
		}

		private void BuildInterface()
		{
			var overlay = new ColorRect
			{
				Color = new Color(0, 0, 0, 0.75f),
				MouseFilter = Control.MouseFilterEnum.Stop
			};
			overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			AddChild(overlay);

			_mainPanel = new PanelContainer();
			_mainPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
			_mainPanel.CustomMinimumSize = new Vector2(900, 520);
			AddChild(_mainPanel);

			var rootVBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			rootVBox.AddThemeConstantOverride("separation", 20);
			_mainPanel.AddChild(rootVBox);

			var titleLabel = new RichTextLabel
			{
				BbcodeEnabled = true,
				Text = "[center][b][font_size=28][color=crimson]✦ 虚空深处之灾厄契约 ✦[/color][/font_size][/b]\n" +
					   "[color=gray]签署灾厄法则以换取高额打捞产出与能量超载[/color][/center]",
				FitContent = true,
				CustomMinimumSize = new Vector2(800, 65),
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			rootVBox.AddChild(titleLabel);

			_cardsContainer = new HBoxContainer
			{
				Alignment = BoxContainer.AlignmentMode.Center
			};
			_cardsContainer.AddThemeConstantOverride("separation", 20);
			rootVBox.AddChild(_cardsContainer);

			var btnHBox = new HBoxContainer
			{
				Alignment = BoxContainer.AlignmentMode.Center
			};
			btnHBox.AddThemeConstantOverride("separation", 30);
			rootVBox.AddChild(btnHBox);

			_confirmButton = new Button
			{
				Text = "确认签署并出击",
				CustomMinimumSize = new Vector2(180, 40)
			};
			_confirmButton.Pressed += () =>
			{
				Visible = false;
				OnStartWithPacts?.Invoke();
			};
			btnHBox.AddChild(_confirmButton);

			_skipButton = new Button
			{
				Text = "放弃签署 (保持现状)",
				CustomMinimumSize = new Vector2(180, 40)
			};
			_skipButton.Pressed += () =>
			{
				Visible = false;
				OnSkipped?.Invoke();
			};
			btnHBox.AddChild(_skipButton);
		}

		public void PresentPactChoices(List<CalamityPactDef> candidates)
		{
			foreach (var child in _cardsContainer.GetChildren())
			{
				child.QueueFree();
			}

			foreach (var pact in candidates)
			{
				var card = CreatePactCard(pact);
				_cardsContainer.AddChild(card);
			}

			Visible = true;
		}

		private PanelContainer CreatePactCard(CalamityPactDef pact)
		{
			var cardPanel = new PanelContainer
			{
				CustomMinimumSize = new Vector2(260, 300)
			};

			var cardVBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
			cardVBox.AddThemeConstantOverride("separation", 12);
			cardPanel.AddChild(cardVBox);

			var title = new RichTextLabel
			{
				BbcodeEnabled = true,
				Text = $"[center][b][color={pact.ThemeColorHex}]{pact.Title}[/color][/b][/center]",
				FitContent = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			cardVBox.AddChild(title);

			var desc = new RichTextLabel
			{
				BbcodeEnabled = true,
				Text = $"[color=salmon]▼ 代价惩罚:[/color]\n{pact.Penalty}\n\n" +
					   $"[color=lightgreen]▲ 赐福增益:[/color]\n{pact.Reward}\n\n" +
					   $"[color=yellow]✦ 废料收益: +{(pact.ScrapBonusMultiplier * 100):F0}%[/color]",
				FitContent = true,
				CustomMinimumSize = new Vector2(230, 140),
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			cardVBox.AddChild(desc);

			var signBtn = new Button
			{
				Text = CalamityPactManager.Instance.IsActive(pact.Id) ? "✔ 已签署生效" : "签署契约",
				CustomMinimumSize = new Vector2(180, 36)
			};
			signBtn.Pressed += () =>
			{
				CalamityPactManager.Instance.TogglePact(pact.Id);
				signBtn.Text = CalamityPactManager.Instance.IsActive(pact.Id) ? "✔ 已签署生效" : "签署契约";
				OnPactSelected?.Invoke(pact);
			};
			cardVBox.AddChild(signBtn);

			return cardPanel;
		}
	}
}
