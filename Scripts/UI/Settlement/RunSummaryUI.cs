using System;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.World.Meta;
using BreakerProtocol.World.Settlement;

namespace BreakerProtocol.UI.Settlement
{
	/// <summary>
	/// 全屏战役结算与战利品评分终端 UI
	/// </summary>
	public partial class RunSummaryUI : Control
	{
		public RunStatistics CurrentStats { get; private set; } = null!;
		public event Action? OnNavigateToMetaTech;
		public event Action? OnStartNewRun;

		private Rect2 _panelArea;
		private float _animTime = 0.0f;
		private Vector2 _currentMousePos = Vector2.Zero;

		public override void _Ready()
		{
			SetAnchorsPreset(LayoutPreset.FullRect);
			GrowHorizontal = GrowDirection.Both;
			GrowVertical = GrowDirection.Both;
			MouseFilter = MouseFilterEnum.Stop;

			Vector2 vpSize = GetViewportRect().Size;
			CustomMinimumSize = vpSize;
			Size = vpSize;
		}

		public void OpenSummary(RunStatistics stats)
		{
			CurrentStats = RunSettlementService.CalculateSettlement(stats);
			Visible = true;
			QueueRedraw();
		}

		public override void _Process(double delta)
		{
			_animTime += (float)delta * 3.0f;

			Vector2 vpSize = GetViewportRect().Size;
			if (vpSize.X > 100 && vpSize.Y > 100 && Size != vpSize)
			{
				Size = vpSize;
				CustomMinimumSize = vpSize;
			}

			_currentMousePos = GetLocalMousePosition();
			UpdateCursorState(_currentMousePos);

			QueueRedraw();
		}

		private void UpdateCursorState(Vector2 mousePos)
		{
			bool isHover = GetMetaTechButtonRect().HasPoint(mousePos) || GetNewRunButtonRect().HasPoint(mousePos);
			MouseDefaultCursorShape = isHover ? CursorShape.PointingHand : CursorShape.Arrow;
		}

		public override void _GuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				HandleClick(_currentMousePos);
				AcceptEvent();
			}
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (!Visible) return;
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				HandleClick(_currentMousePos);
			}
		}

		private void HandleClick(Vector2 clickPos)
		{
			if (GetMetaTechButtonRect().HasPoint(clickPos))
			{
				Visible = false;
				OnNavigateToMetaTech?.Invoke();
			}
			else if (GetNewRunButtonRect().HasPoint(clickPos))
			{
				Visible = false;
				OnStartNewRun?.Invoke();
			}
		}

		private Rect2 GetPanelArea()
		{
			Vector2 vpSize = GetViewportRect().Size;
			float w = vpSize.X > 100 ? vpSize.X : 1280.0f;
			float h = vpSize.Y > 100 ? vpSize.Y : 720.0f;
			return new Rect2(120, 60, w - 240, h - 120);
		}

		private Rect2 GetMetaTechButtonRect()
		{
			var panel = GetPanelArea();
			return new Rect2(panel.Position.X + 60, panel.End.Y - 65, 380, 44);
		}

		private Rect2 GetNewRunButtonRect()
		{
			var panel = GetPanelArea();
			return new Rect2(panel.Position.X + panel.Size.X - 440, panel.End.Y - 65, 380, 44);
		}

		public override void _Draw()
		{
			if (CurrentStats == null) return;

			_panelArea = GetPanelArea();
			var font = ThemeDB.FallbackFont;
			bool isVictory = CurrentStats.Ending == RunEndingType.Victory;

			Color themeColor = isVictory ? Colors.Gold : Colors.OrangeRed;
			Color bgPanel = isVictory ? new Color(0.02f, 0.05f, 0.08f, 0.97f) : new Color(0.06f, 0.03f, 0.03f, 0.97f);

			// 1. 绘制战役结算全息背板
			DrawRect(_panelArea, bgPanel);
			DrawRect(_panelArea, themeColor, false, 2.5f);

			// 2. 标头大字
			string titleStr = isVictory ? "【 战役胜利 · 星区突围成功 】 MISSION ACCOMPLISHED" : "【 战役折戟 · 舰体信号失联 】 SIGNAL TERMINATED";
			DrawString(font, _panelArea.Position + new Vector2(35, 40), titleStr, HorizontalAlignment.Left, -1, 18, themeColor);
			DrawLine(_panelArea.Position + new Vector2(25, 55), _panelArea.Position + new Vector2(_panelArea.Size.X - 25, 55), new Color(0.4f, 0.5f, 0.6f, 0.4f), 1.5f);

			// 3. 绘制左栏：详细战地数据清单
			float startY = _panelArea.Position.Y + 80.0f;
			DrawString(font, _panelArea.Position + new Vector2(40, 85), "[ 战地作战效能统计清单 ]", HorizontalAlignment.Left, -1, 14, Colors.Cyan);

			string[] statsLines = new string[]
			{
				$"• 星区推进深度:  第 {CurrentStats.SectorsCleared} / 8 星区列",
				$"• 常规战机歼灭:  {CurrentStats.StandardEnemiesKilled} 架",
				$"• 精英猎杀旗舰:  {CurrentStats.ElitesKilled} 艘",
				$"• 移动要塞击溃:  {CurrentStats.BossesKilled} 座 (泰坦熔炉)",
				$"• 累计开采废料:  {CurrentStats.TotalScrapsEarned} ⚙️",
				$"• 缴获算力核心:  {CurrentStats.ComputeCoresEarned} 💠",
				$"• 承受装甲创伤:  {CurrentStats.DamageTakenTotal:F0} HP",
				$"• 撤离舰体完好:  {CurrentStats.FinalHullIntegrityPercent:F0}% 耐久",
				$"• 战局作战耗时:  {CurrentStats.DurationSeconds / 60:F0}分 {CurrentStats.DurationSeconds % 60:F0}秒"
			};

			for (int i = 0; i < statsLines.Length; i++)
			{
				DrawString(font, new Vector2(_panelArea.Position.X + 40, startY + 35 + (i * 28)), statsLines[i], HorizontalAlignment.Left, -1, 12, Colors.LightGray);
			}

			// 4. 绘制右栏：战役评分与研发数据结算徽章
			float rightX = _panelArea.Position.X + 540;
			DrawString(font, new Vector2(rightX, _panelArea.Position.Y + 85), "[ 战役综合评分与研发继承 ]", HorizontalAlignment.Left, -1, 14, Colors.Yellow);

			// 综合评分显示
			Rect2 scoreCard = new(rightX, startY + 25, 420, 110);
			DrawRect(scoreCard, new Color(0.04f, 0.08f, 0.14f, 0.9f));
			DrawRect(scoreCard, themeColor, false, 1.8f);

			DrawString(font, scoreCard.Position + new Vector2(20, 35), "综合战役评分 (TOTAL SCORE)", HorizontalAlignment.Left, -1, 12, Colors.White);
			DrawString(font, scoreCard.Position + new Vector2(20, 75), $"{CurrentStats.CalculatedScore:N0} PTS", HorizontalAlignment.Left, -1, 26, Colors.Gold);

			// 动态 S/A/B/C/D 勋章图标
			DrawRankBadge(scoreCard.Position + new Vector2(340, 55), CurrentStats.Rank);

			// 研发数据碎片结算卡
			Rect2 fragmentCard = new(rightX, startY + 155, 420, 130);
			DrawRect(fragmentCard, new Color(0.04f, 0.12f, 0.16f, 0.9f));
			DrawRect(fragmentCard, Colors.Cyan, false, 1.8f);

			DrawString(font, fragmentCard.Position + new Vector2(20, 32), "局外永久研发数据碎片收益", HorizontalAlignment.Left, -1, 13, Colors.Cyan);
			DrawString(font, fragmentCard.Position + new Vector2(20, 70), $"+{CurrentStats.DataFragmentsEarned} 💾", HorizontalAlignment.Left, -1, 28, Colors.LimeGreen);
			DrawString(font, fragmentCard.Position + new Vector2(20, 105), $"已自动汇入母港科研总局 (当前总计: {MetaProgressionManager.Instance.DataFragments} 💾)", HorizontalAlignment.Left, -1, 11, Colors.LightGray);

			// 5. 绘制底部交互按钮
			DrawLine(_panelArea.Position + new Vector2(25, _panelArea.Size.Y - 80), _panelArea.Position + new Vector2(_panelArea.Size.X - 25, _panelArea.Size.Y - 80), new Color(0.4f, 0.5f, 0.6f, 0.4f), 1.5f);

			// 按钮 1: 前往母港科研局
			Rect2 metaBtn = GetMetaTechButtonRect();
			bool hoverMeta = metaBtn.HasPoint(_currentMousePos);
			DrawRect(metaBtn, hoverMeta ? new Color(0.2f, 0.55f, 0.75f) : new Color(0.12f, 0.35f, 0.55f));
			DrawRect(metaBtn, hoverMeta ? Colors.White : Colors.Cyan, false, hoverMeta ? 2.0f : 1.2f);
			DrawString(font, metaBtn.Position + new Vector2(65, 27), "💾 前往母港科研局 (加点科技)", HorizontalAlignment.Center, -1, 13, Colors.White);

			// 按钮 2: 再次启航
			Rect2 newRunBtn = GetNewRunButtonRect();
			bool hoverNew = newRunBtn.HasPoint(_currentMousePos);
			DrawRect(newRunBtn, hoverNew ? new Color(0.25f, 0.75f, 0.45f) : new Color(0.18f, 0.55f, 0.35f));
			DrawRect(newRunBtn, hoverNew ? Colors.White : Colors.LimeGreen, false, hoverNew ? 2.0f : 1.2f);
			DrawString(font, newRunBtn.Position + new Vector2(75, 27), "🚀 继承战备 · 再次启航", HorizontalAlignment.Center, -1, 13, Colors.White);
		}

		private void DrawRankBadge(Vector2 center, EvaluationRank rank)
		{
			Color rankColor = rank switch
			{
				EvaluationRank.S => Colors.Gold,
				EvaluationRank.A => Colors.Cyan,
				EvaluationRank.B => Colors.LimeGreen,
				EvaluationRank.C => Colors.Yellow,
				_                => Colors.OrangeRed
			};

			float radius = 32.0f;
			float pulse = Mathf.Sin(_animTime * 2.0f) * 2.0f;
			DrawCircle(center, radius + pulse, new Color(rankColor.R, rankColor.G, rankColor.B, 0.25f));
			DrawArc(center, radius + 2.0f, 0, Mathf.Tau, 24, rankColor, 2.5f);

			var font = ThemeDB.FallbackFont;
			string rankLetter = rank.ToString();
			DrawString(font, center + new Vector2(-10, 12), rankLetter, HorizontalAlignment.Center, -1, 32, rankColor);
		}
	}
}
