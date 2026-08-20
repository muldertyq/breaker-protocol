using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Events;

namespace BreakerProtocol.UI.Events
{
	/// <summary>
	/// 全息深空异象日志与文本交互终端 (集成实时数据遥测、置灰拦截与红绿分明结算)
	/// </summary>
	public partial class SpaceEventDialogueUI : Control
	{
		public SpaceEventNode? CurrentEvent { get; private set; }
		public ShipEntity? TargetShip { get; private set; }

		public event Action<EventOutcome>? OnEventResolved;

		// 调试强制判定模式: 0 = 正常几率, 1 = 强制必成功, 2 = 强制必失败
		public int DebugForceMode { get; set; } = 0;

		private Rect2 _panelArea;
		private float _animTime = 0.0f;
		private Vector2 _currentMousePos = Vector2.Zero;
		private int _hoveredChoiceIndex = -1;
		private bool _isResolved = false;
		private EventOutcome? _resolvedOutcome;
		private string _warningMessage = string.Empty;

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

		public void OpenEvent(SpaceEventNode ev, ShipEntity ship)
		{
			CurrentEvent = ev;
			TargetShip = ship;
			_isResolved = false;
			_resolvedOutcome = null;
			_warningMessage = string.Empty;
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
			UpdateHoverState(_currentMousePos);

			QueueRedraw();
		}

		private void UpdateHoverState(Vector2 mousePos)
		{
			_hoveredChoiceIndex = -1;
			if (CurrentEvent == null || _isResolved)
			{
				MouseDefaultCursorShape = CursorShape.Arrow;
				return;
			}

			for (int i = 0; i < CurrentEvent.Choices.Count; i++)
			{
				if (GetChoiceButtonRect(i).HasPoint(mousePos))
				{
					_hoveredChoiceIndex = i;
					var choice = CurrentEvent.Choices[i];
					bool canAfford = (choice.RequiredScraps == 0 || PlayerEconomyManager.Instance.Scraps >= choice.RequiredScraps) &&
									 (choice.RequiredCores == 0 || PlayerEconomyManager.Instance.ComputeCores >= choice.RequiredCores);

					MouseDefaultCursorShape = canAfford ? CursorShape.PointingHand : CursorShape.Forbidden;
					return;
				}
			}

			MouseDefaultCursorShape = CursorShape.Arrow;
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
			if (CurrentEvent == null) return;

			if (_isResolved)
			{
				if (GetContinueButtonRect().HasPoint(clickPos))
				{
					Visible = false;
					OnEventResolved?.Invoke(_resolvedOutcome!);
				}
				return;
			}

			for (int i = 0; i < CurrentEvent.Choices.Count; i++)
			{
				if (GetChoiceButtonRect(i).HasPoint(clickPos))
				{
					ExecuteChoice(CurrentEvent.Choices[i]);
					return;
				}
			}
		}

		private void ExecuteChoice(EventChoice choice)
		{
			// 1. 严格校验前置条件
			if (choice.RequiredScraps > 0 && PlayerEconomyManager.Instance.Scraps < choice.RequiredScraps)
			{
				_warningMessage = $"❌ 废料不足！执行该行动需要 {choice.RequiredScraps} ⚙，当前仅有 {PlayerEconomyManager.Instance.Scraps} ⚙。";
				QueueRedraw();
				return;
			}
			if (choice.RequiredCores > 0 && PlayerEconomyManager.Instance.ComputeCores < choice.RequiredCores)
			{
				_warningMessage = $"❌ 算力核心不足！执行该行动需要 {choice.RequiredCores} 💠，当前仅有 {PlayerEconomyManager.Instance.ComputeCores} 💠。";
				QueueRedraw();
				return;
			}

			// 2. 扣除消耗
			if (choice.RequiredScraps > 0) PlayerEconomyManager.Instance.SpendScraps(choice.RequiredScraps);
			if (choice.RequiredCores > 0) PlayerEconomyManager.Instance.SpendComputeCores(choice.RequiredCores);

			// 3. 概率掷骰解算
			float roll = GD.Randf();
			bool isSuccess = roll <= choice.SuccessRate;

			// 调试强制干预
			if (DebugForceMode == 1) isSuccess = true;
			else if (DebugForceMode == 2 && choice.FailureOutcome != null) isSuccess = false;

			EventOutcome outcome = isSuccess ? choice.SuccessOutcome : (choice.FailureOutcome ?? choice.SuccessOutcome);
			outcome.IsSuccess = isSuccess;
			outcome.RollPercent = roll * 100.0f;
			outcome.TargetThreshold = choice.SuccessRate * 100.0f;

			// 4. 应用结果与数据更新
			ApplyOutcome(outcome);

			_isResolved = true;
			_resolvedOutcome = outcome;
			QueueRedraw();
		}

		private void ApplyOutcome(EventOutcome outcome)
		{
			if (outcome.ScrapDelta > 0) PlayerEconomyManager.Instance.AddScraps(outcome.ScrapDelta);
			if (outcome.CoreDelta > 0) PlayerEconomyManager.Instance.AddComputeCores(outcome.CoreDelta);

			if (TargetShip != null)
			{
				if (outcome.RepairRatio > 0.0f)
				{
					foreach (var m in TargetShip.Grid.Modules)
					{
						if (!m.IsDestroyed)
						{
							m.CurrentHp = Mathf.Min(m.MaxHp, m.CurrentHp + (m.MaxHp * outcome.RepairRatio));
						}
					}
				}

				if (outcome.DamageAmount > 0.0f)
				{
					foreach (var m in TargetShip.Grid.Modules)
					{
						if (!m.IsDestroyed)
						{
							m.CurrentHp = Mathf.Max(10.0f, m.CurrentHp - outcome.DamageAmount);
							TargetShip.OnModuleDamaged(m, outcome.DamageAmount);
							break;
						}
					}
				}
			}
		}

		private (float currentHp, float maxHp) GetShipHpStats()
		{
			if (TargetShip == null) return (0, 0);
			float cur = 0, max = 0;
			foreach (var m in TargetShip.Grid.Modules)
			{
				if (!m.IsDestroyed)
				{
					cur += m.CurrentHp;
					max += m.MaxHp;
				}
			}
			return (cur, max);
		}

		private Rect2 GetPanelArea()
		{
			Vector2 vpSize = GetViewportRect().Size;
			float w = vpSize.X > 100 ? vpSize.X : 1280.0f;
			float h = vpSize.Y > 100 ? vpSize.Y : 720.0f;
			return new Rect2(160, 65, w - 320, h - 130);
		}

		private Rect2 GetChoiceButtonRect(int index)
		{
			var panel = GetPanelArea();
			float startY = panel.Position.Y + 265.0f;
			return new Rect2(panel.Position.X + 40, startY + (index * 68.0f), panel.Size.X - 80, 56);
		}

		private Rect2 GetContinueButtonRect()
		{
			var panel = GetPanelArea();
			return new Rect2(panel.Position.X + (panel.Size.X * 0.5f) - 110, panel.End.Y - 65, 220, 42);
		}

		public override void _Draw()
		{
			if (CurrentEvent == null) return;

			_panelArea = GetPanelArea();
			var font = ThemeDB.FallbackFont;

			// 1. 绘制科幻深空日志背板
			DrawRect(_panelArea, new Color(0.02f, 0.05f, 0.08f, 0.96f));
			DrawRect(_panelArea, CurrentEvent.ThemeColor, false, 2.5f);

			// 2. 标头与派系
			DrawString(font, _panelArea.Position + new Vector2(30, 35), CurrentEvent.Title, HorizontalAlignment.Left, -1, 17, CurrentEvent.ThemeColor);
			
			// 核心新增：面板内置实时数据遥测栏 (资产 + 耐久)
			var hp = GetShipHpStats();
			string telemetryTag = $"⚙️ 废料: {PlayerEconomyManager.Instance.Scraps} | 💠 核心: {PlayerEconomyManager.Instance.ComputeCores} | 🛡️ 耐久: {hp.currentHp:F0}/{hp.maxHp:F0} HP";
			DrawString(font, _panelArea.Position + new Vector2(_panelArea.Size.X - 440, 35), telemetryTag, HorizontalAlignment.Right, -1, 12, Colors.LimeGreen);

			DrawLine(_panelArea.Position + new Vector2(25, 48), _panelArea.Position + new Vector2(_panelArea.Size.X - 25, 48), new Color(0.3f, 0.5f, 0.7f, 0.4f), 1.5f);

			// 3. 事件正文叙述
			DrawString(font, _panelArea.Position + new Vector2(35, 78), "【 战地传感器遥测与信标日志 】", HorizontalAlignment.Left, -1, 13, Colors.Cyan);
			DrawString(font, _panelArea.Position + new Vector2(35, 105), CurrentEvent.Description, HorizontalAlignment.Left, (int)_panelArea.Size.X - 70, 14, Colors.White);

			// 4. 绘制抉择分支 / 结算弹窗
			if (!_isResolved)
			{
				DrawLine(_panelArea.Position + new Vector2(25, 245), _panelArea.Position + new Vector2(_panelArea.Size.X - 25, 245), new Color(0.3f, 0.5f, 0.7f, 0.4f), 1.5f);
				DrawString(font, _panelArea.Position + new Vector2(35, 258), "[ 请指挥官下达行动决策 ]", HorizontalAlignment.Left, -1, 12, Colors.Yellow);

				for (int i = 0; i < CurrentEvent.Choices.Count; i++)
				{
					var choice = CurrentEvent.Choices[i];
					Rect2 btnRect = GetChoiceButtonRect(i);
					bool isHover = i == _hoveredChoiceIndex;

					bool canAfford = (choice.RequiredScraps == 0 || PlayerEconomyManager.Instance.Scraps >= choice.RequiredScraps) &&
									 (choice.RequiredCores == 0 || PlayerEconomyManager.Instance.ComputeCores >= choice.RequiredCores);

					// 置灰与可用样式分明
					Color bgBtnColor;
					Color borderBtnColor;
					Color textColor;

					if (canAfford)
					{
						bgBtnColor = isHover ? new Color(0.12f, 0.25f, 0.35f, 0.95f) : new Color(0.06f, 0.12f, 0.18f, 0.85f);
						borderBtnColor = isHover ? Colors.White : Colors.Cyan;
						textColor = Colors.White;
					}
					else
					{
						// 禁用置灰样式
						bgBtnColor = new Color(0.08f, 0.08f, 0.08f, 0.80f);
						borderBtnColor = new Color(0.6f, 0.2f, 0.2f, 0.6f);
						textColor = new Color(0.55f, 0.55f, 0.55f);
					}

					DrawRect(btnRect, bgBtnColor);
					DrawRect(btnRect, borderBtnColor, false, isHover && canAfford ? 2.0f : 1.2f);

					string optPrefix = $"[{i + 1}] ";
					DrawString(font, btnRect.Position + new Vector2(18, 25), optPrefix + choice.ChoiceText, HorizontalAlignment.Left, -1, 13, textColor);

					// 条件与消耗标注
					if (!canAfford)
					{
						string lackStr = choice.RequiredScraps > PlayerEconomyManager.Instance.Scraps
							? $"[❌ 废料不足: 需要 {choice.RequiredScraps} ⚙ / 现有 {PlayerEconomyManager.Instance.Scraps} ⚙]"
							: $"[❌ 核心不足: 需要 {choice.RequiredCores} 💠 / 现有 {PlayerEconomyManager.Instance.ComputeCores} 💠]";
						DrawString(font, btnRect.Position + new Vector2(18, 45), lackStr, HorizontalAlignment.Left, -1, 11, Colors.OrangeRed);
					}
					else if (!string.IsNullOrEmpty(choice.RequiredConditionTag))
					{
						DrawString(font, btnRect.Position + new Vector2(18, 45), choice.RequiredConditionTag, HorizontalAlignment.Left, -1, 11, Colors.Gold);
					}
				}

				// 警告提示信息
				if (!string.IsNullOrEmpty(_warningMessage))
				{
					DrawString(font, _panelArea.Position + new Vector2(40, _panelArea.Size.Y - 20), _warningMessage, HorizontalAlignment.Left, -1, 12, Colors.OrangeRed);
				}
			}
			else
			{
				// 5. 核心重构：红绿分明的结果结算弹窗与掷骰数值
				Rect2 resultBox = new(_panelArea.Position.X + 40, _panelArea.Position.Y + 265, _panelArea.Size.X - 80, 150);
				bool isWin = _resolvedOutcome!.IsSuccess;

				Color boxBg = isWin ? new Color(0.04f, 0.16f, 0.10f, 0.95f) : new Color(0.20f, 0.05f, 0.05f, 0.95f);
				Color boxBorder = isWin ? Colors.LimeGreen : Colors.OrangeRed;
				DrawRect(resultBox, boxBg);
				DrawRect(resultBox, boxBorder, false, 2.5f);

				string titleTag = isWin ? "【 ✔ 决策行动执行成功 (SUCCESS) 】" : "【 ❌ 决策行动遭遇挫折 (FAILURE) 】";
				DrawString(font, resultBox.Position + new Vector2(25, 35), titleTag, HorizontalAlignment.Left, -1, 15, isWin ? Colors.LimeGreen : Colors.OrangeRed);

				// 显示掷骰细节
				if (_resolvedOutcome.TargetThreshold < 100.0f)
				{
					string rollDetail = $"[ 🎲 掷骰检定: 掷出 {_resolvedOutcome.RollPercent:F0}% / 门槛 {_resolvedOutcome.TargetThreshold:F0}% ]";
					DrawString(font, resultBox.Position + new Vector2(resultBox.Size.X - 260, 35), rollDetail, HorizontalAlignment.Right, -1, 12, Colors.Gold);
				}

				DrawString(font, resultBox.Position + new Vector2(25, 75), _resolvedOutcome.ResultLog, HorizontalAlignment.Left, (int)resultBox.Size.X - 50, 14, Colors.White);

				// 确认继续按钮
				Rect2 contBtn = GetContinueButtonRect();
				bool isContHover = contBtn.HasPoint(_currentMousePos);
				DrawRect(contBtn, isContHover ? new Color(0.25f, 0.75f, 0.45f) : new Color(0.18f, 0.55f, 0.35f));
				DrawRect(contBtn, isContHover ? Colors.White : Colors.LimeGreen, false, isContHover ? 2.0f : 1.0f);
				DrawString(font, contBtn.Position + new Vector2(25, 26), "✔ 确认记录并继续航行", HorizontalAlignment.Center, -1, 13, Colors.White);
			}
		}
	}
}
