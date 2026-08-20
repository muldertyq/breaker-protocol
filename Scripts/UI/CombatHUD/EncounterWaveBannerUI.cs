using System;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.World.Director;

namespace BreakerProtocol.UI.CombatHUD
{
	/// <summary>
	/// 遭遇战波次全息横幅与威胁度雷达 HUD
	/// </summary>
	public partial class EncounterWaveBannerUI : Control
	{
		private string _bannerText = string.Empty;
		private float _bannerAlpha = 0.0f;
		private float _bannerTimer = 0.0f;
		private Color _bannerColor = Colors.Cyan;

		public override void _Ready()
		{
			SetAnchorsPreset(LayoutPreset.FullRect);
			MouseFilter = MouseFilterEnum.Ignore;
			Size = GetViewportRect().Size;
		}

		public void ShowWaveBanner(int waveIndex, int totalWaves, string waveTitle)
		{
			_bannerText = $"⚠️ 【超空间折跃警报】 {waveTitle} ({waveIndex}/{totalWaves}) ⚠️";
			_bannerColor = waveIndex == totalWaves ? Colors.Crimson : Colors.Gold;
			_bannerAlpha = 1.0f;
			_bannerTimer = 3.5f;
		}

		public void ShowVictoryBanner()
		{
			_bannerText = "✦ 遭遇战告捷 · 敌军信号完全肃清 ✦";
			_bannerColor = Colors.LimeGreen;
			_bannerAlpha = 1.0f;
			_bannerTimer = 4.0f;
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;

			if (_bannerTimer > 0.0f)
			{
				_bannerTimer -= dt;
				if (_bannerTimer < 1.0f)
				{
					_bannerAlpha = Mathf.Lerp(_bannerAlpha, 0.0f, dt * 3.0f);
				}
			}
			else
			{
				_bannerAlpha = 0.0f;
			}

			QueueRedraw();
		}

		public override void _Draw()
		{
			var director = CombatEncounterDirector.Instance;
			if (director == null || !director.IsEncounterActive && _bannerAlpha <= 0.01f) return;

			var font = ThemeDB.FallbackFont;
			Vector2 vpSize = GetViewportRect().Size;
			Vector2 center = vpSize * 0.5f;

			// 1. 顶部常驻战术雷达与威胁度
			if (director.IsEncounterActive)
			{
				Rect2 radarRect = new(center.X - 220, 15, 440, 42);
				DrawRect(radarRect, new Color(0.02f, 0.05f, 0.08f, 0.85f));
				DrawRect(radarRect, new Color(0.2f, 0.4f, 0.6f, 0.6f), false, 1.2f);

				string waveStr = $"波次: {director.CurrentWaveIndex}/{director.TotalWaves}";
				string threatStr = $"威胁度: {director.CurrentThreatLevel:F0} ⚡";
				string enemyStr = $"存活敌机: {director.ActiveEnemies.Count} 艘";

				DrawString(font, radarRect.Position + new Vector2(20, 26), waveStr, HorizontalAlignment.Left, -1, 13, Colors.Gold);
				DrawString(font, radarRect.Position + new Vector2(160, 26), threatStr, HorizontalAlignment.Left, -1, 13, Colors.OrangeRed);
				DrawString(font, radarRect.Position + new Vector2(300, 26), enemyStr, HorizontalAlignment.Left, -1, 13, Colors.LimeGreen);
			}

			// 2. 居中动态全息警报横幅
			if (_bannerAlpha > 0.01f)
			{
				Rect2 bannerBox = new(center.X - 380, 140, 760, 50);
				Color bgCol = new(_bannerColor.R * 0.2f, _bannerColor.G * 0.2f, _bannerColor.B * 0.2f, _bannerAlpha * 0.85f);
				Color borderCol = new(_bannerColor.R, _bannerColor.G, _bannerColor.B, _bannerAlpha);

				DrawRect(bannerBox, bgCol);
				DrawRect(bannerBox, borderCol, false, 2.0f);
				DrawString(font, bannerBox.Position + new Vector2(0, 32), _bannerText, HorizontalAlignment.Center, 760, 16, borderCol);
			}
		}
	}
}
