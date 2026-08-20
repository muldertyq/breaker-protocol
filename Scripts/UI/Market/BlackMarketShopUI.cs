using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Market;

namespace BreakerProtocol.UI.Market
{
	/// <summary>
	/// 全息废土黑市交易操作面板 (支持双栏买卖、三项战备服务、悬停光效与离港返回)
	/// </summary>
	public partial class BlackMarketShopUI : Control
	{
		public event Action? OnCloseRequested;

		public ShipEntity? TargetShip { get; private set; }
		public List<MarketItem> CurrentStock { get; private set; } = new();

		private float _animTime = 0.0f;
		private string _transactionFeedback = "欢迎光临黑市，请选择在售构件或战备服务！";
		private Color _feedbackColor = Colors.Gold;
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

		public void Initialize(ShipEntity ship)
		{
			TargetShip = ship;
			RefreshStock();
		}

		public void RefreshStock()
		{
			CurrentStock = BlackMarketService.GenerateMarketStock(5);
			QueueRedraw();
		}

		public override void _Process(double delta)
		{
			if (!Visible) return;

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
			bool isHoveringInteractive = false;

			// 1. 检查货架买入按钮
			for (int i = 0; i < CurrentStock.Count; i++)
			{
				if (!CurrentStock[i].IsSoldOut && GetBuyButtonRect(i).HasPoint(mousePos))
				{
					isHoveringInteractive = true;
					break;
				}
			}

			// 2. 检查拆解按钮
			if (!isHoveringInteractive && TargetShip != null)
			{
				var modules = TargetShip.Grid.Modules.ToList();
				for (int i = 0; i < Mathf.Min(6, modules.Count); i++)
				{
					if (!modules[i].IsDestroyed && GetSellButtonRect(i).HasPoint(mousePos))
					{
						isHoveringInteractive = true;
						break;
					}
				}
			}

			// 3. 检查底部服务按钮与离港返回按钮
			if (!isHoveringInteractive)
			{
				if (GetRepairButtonRect().HasPoint(mousePos) ||
					GetAblativeButtonRect().HasPoint(mousePos) ||
					GetRerollButtonRect().HasPoint(mousePos) ||
					GetLeaveButtonRect().HasPoint(mousePos))
				{
					isHoveringInteractive = true;
				}
			}

			MouseDefaultCursorShape = isHoveringInteractive ? CursorShape.PointingHand : CursorShape.Arrow;
		}

		public override void _GuiInput(InputEvent @event)
		{
			if (!Visible) return;
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
			else if (@event is InputEventKey ek && ek.Pressed && !ek.Echo)
			{
				if (ek.Keycode == Key.Escape || ek.Keycode == Key.Backspace)
				{
					OnCloseRequested?.Invoke();
				}
			}
		}

		private void HandleClick(Vector2 clickPos)
		{
			// 1. 离港返回星图按钮
			if (GetLeaveButtonRect().HasPoint(clickPos))
			{
				OnCloseRequested?.Invoke();
				return;
			}

			// 2. 点击购买货架商品 (左栏)
			for (int i = 0; i < CurrentStock.Count; i++)
			{
				var item = CurrentStock[i];
				if (item.IsSoldOut) continue;

				if (GetBuyButtonRect(i).HasPoint(clickPos))
				{
					TryBuyItem(item);
					return;
				}
			}

			// 3. 点击出售/拆解本舰构件 (右栏)
			if (TargetShip != null)
			{
				var modules = TargetShip.Grid.Modules.ToList();
				for (int i = 0; i < Mathf.Min(6, modules.Count); i++)
				{
					var mod = modules[i];
					if (mod.IsDestroyed) continue;

					if (GetSellButtonRect(i).HasPoint(clickPos))
					{
						TrySellModule(mod);
						return;
					}
				}
			}

			// 4. 点击底部专属服务
			if (GetRepairButtonRect().HasPoint(clickPos))
			{
				TryFieldRepair();
			}
			else if (GetAblativeButtonRect().HasPoint(clickPos))
			{
				TryResetAblativeDetonation();
			}
			else if (GetRerollButtonRect().HasPoint(clickPos))
			{
				TryRerollStock();
			}
		}

		private void TryBuyItem(MarketItem item)
		{
			if (PlayerEconomyManager.Instance.SpendScraps(item.BuyPrice))
			{
				item.IsSoldOut = true;
				_transactionFeedback = $"✔ 成功购买【{item.Definition.Name}】！(-{item.BuyPrice} 废料)";
				_feedbackColor = Colors.LimeGreen;
			}
			else
			{
				_transactionFeedback = $"❌ 废料不足！购买【{item.Definition.Name}】需要 {item.BuyPrice} 废料。";
				_feedbackColor = Colors.OrangeRed;
			}
			QueueRedraw();
		}

		private void TrySellModule(ModuleInstance mod)
		{
			if (TargetShip == null) return;

			int sellPrice = BlackMarketService.CalculateSellPrice(mod.Definition, mod.CurrentHp / mod.MaxHp);
			PlayerEconomyManager.Instance.AddScraps(sellPrice);

			mod.CurrentHp = 0.0f;
			TargetShip.OnModuleDamaged(mod, 0.0f);

			_transactionFeedback = $"♻️ 拆解回收【{mod.Definition.Name}】！获得 +{sellPrice} 废料。";
			_feedbackColor = Colors.Gold;
			QueueRedraw();
		}

		private void TryFieldRepair()
		{
			if (TargetShip == null) return;

			int repairCost = 100;
			if (PlayerEconomyManager.Instance.SpendScraps(repairCost))
			{
				foreach (var m in TargetShip.Grid.Modules)
				{
					if (!m.IsDestroyed) m.CurrentHp = m.MaxHp;
				}
				_transactionFeedback = "🔧 全舰构件耐久度已彻底修复满额！(-100 废料)";
				_feedbackColor = Colors.LimeGreen;
			}
			else
			{
				_transactionFeedback = "❌ 废料不足，全舰大修需要 100 废料！";
				_feedbackColor = Colors.OrangeRed;
			}
			QueueRedraw();
		}

		private void TryResetAblativeDetonation()
		{
			int cost = 75;
			if (PlayerEconomyManager.Instance.SpendScraps(cost))
			{
				_transactionFeedback = "💥 战术过载爆甲机构与爆炸螺栓已重新填装！(-75 废料)";
				_feedbackColor = Colors.Cyan;
			}
			else
			{
				_transactionFeedback = "❌ 废料不足，爆甲重装需要 75 废料！";
				_feedbackColor = Colors.OrangeRed;
			}
			QueueRedraw();
		}

		private void TryRerollStock()
		{
			int cost = 30;
			if (PlayerEconomyManager.Instance.SpendScraps(cost))
			{
				RefreshStock();
				_transactionFeedback = "🔄 黑市走私渠道已刷新，上架全新构件！(-30 废料)";
				_feedbackColor = Colors.Yellow;
			}
			else
			{
				_transactionFeedback = "❌ 废料不足，刷新货架需要 30 废料！";
				_feedbackColor = Colors.OrangeRed;
			}
			QueueRedraw();
		}

		// -------------------------------------------------------------
		// 统一坐标系与按钮区域解算
		// -------------------------------------------------------------
		private Rect2 GetPanelArea()
		{
			Vector2 vpSize = GetViewportRect().Size;
			float w = vpSize.X > 100 ? vpSize.X : 1280.0f;
			float h = vpSize.Y > 100 ? vpSize.Y : 720.0f;
			return new Rect2(80, 60, w - 160, h - 120);
		}

		private Rect2 GetStockItemRect(int index)
		{
			var panel = GetPanelArea();
			float startY = panel.Position.Y + 80.0f;
			return new Rect2(panel.Position.X + 25, startY + (index * 65.0f), 500, 55);
		}

		private Rect2 GetBuyButtonRect(int index)
		{
			var itemRect = GetStockItemRect(index);
			return new Rect2(itemRect.Position.X + 400, itemRect.Position.Y + 11, 88, 33);
		}

		private Rect2 GetShipModRect(int index)
		{
			var panel = GetPanelArea();
			float startY = panel.Position.Y + 80.0f;
			return new Rect2(panel.Position.X + 555, startY + (index * 65.0f), 500, 55);
		}

		private Rect2 GetSellButtonRect(int index)
		{
			var modRect = GetShipModRect(index);
			return new Rect2(modRect.Position.X + 400, modRect.Position.Y + 11, 88, 33);
		}

		private Rect2 GetLeaveButtonRect()
		{
			var panel = GetPanelArea();
			return new Rect2(panel.Position.X + 25, panel.End.Y - 60, 180, 38);
		}

		private Rect2 GetRepairButtonRect()
		{
			var panel = GetPanelArea();
			return new Rect2(panel.Position.X + 220, panel.End.Y - 60, 200, 38);
		}

		private Rect2 GetAblativeButtonRect()
		{
			var panel = GetPanelArea();
			return new Rect2(panel.Position.X + 435, panel.End.Y - 60, 200, 38);
		}

		private Rect2 GetRerollButtonRect()
		{
			var panel = GetPanelArea();
			return new Rect2(panel.Position.X + 650, panel.End.Y - 60, 170, 38);
		}

		public override void _Draw()
		{
			if (!Visible) return;

			var panelArea = GetPanelArea();
			var font = ThemeDB.FallbackFont;
			var eco = PlayerEconomyManager.Instance;

			// 1. 绘制科幻黑市交易背板
			DrawRect(panelArea, new Color(0.03f, 0.05f, 0.09f, 0.96f));
			DrawRect(panelArea, new Color(0.95f, 0.75f, 0.25f, 0.75f), false, 2.5f);

			// 2. 标头与资产状态
			DrawString(font, panelArea.Position + new Vector2(25, 35), "【 废土自由走私黑市改装终端 】 BLACK MARKET OUTPOST", HorizontalAlignment.Left, -1, 16, Colors.Gold);
			string ecoTag = $"• 金属废料: {eco.Scraps} ⚙️   • 算力核心: {eco.ComputeCores} 💠";
			DrawString(font, panelArea.Position + new Vector2(panelArea.Size.X - 340, 35), ecoTag, HorizontalAlignment.Right, -1, 14, Colors.LimeGreen);
			DrawLine(panelArea.Position + new Vector2(20, 50), panelArea.Position + new Vector2(panelArea.Size.X - 20, 50), new Color(0.4f, 0.5f, 0.6f, 0.4f), 1.5f);

			// 3. 绘制左栏：在售特惠构件
			DrawString(font, panelArea.Position + new Vector2(30, 72), "[ 黑市在售走私构件 ]", HorizontalAlignment.Left, -1, 13, Colors.Cyan);

			for (int i = 0; i < CurrentStock.Count; i++)
			{
				var item = CurrentStock[i];
				Rect2 itemRect = GetStockItemRect(i);
				Color bgColor = item.IsSoldOut ? new Color(0.1f, 0.1f, 0.1f, 0.5f) : new Color(0.06f, 0.12f, 0.18f, 0.85f);
				DrawRect(itemRect, bgColor);
				DrawRect(itemRect, item.IsSoldOut ? Colors.Gray : new Color(0.2f, 0.5f, 0.7f, 0.6f), false, 1.2f);

				string nameStr = item.IsSoldOut ? $"[已售罄] {item.Definition.Name}" : $"{item.Definition.Name} ({item.Definition.Category})";
				DrawString(font, itemRect.Position + new Vector2(15, 24), nameStr, HorizontalAlignment.Left, -1, 12, item.IsSoldOut ? Colors.Gray : Colors.White);
				DrawString(font, itemRect.Position + new Vector2(15, 42), $"耐久: {item.Definition.BaseHp} HP | 质量: {item.Definition.Mass}t", HorizontalAlignment.Left, -1, 11, Colors.LightGray);

				if (!item.IsSoldOut)
				{
					Rect2 buyBtnRect = GetBuyButtonRect(i);
					bool isHover = buyBtnRect.HasPoint(_currentMousePos);
					bool canAfford = eco.Scraps >= item.BuyPrice;

					Color btnColor = canAfford ? (isHover ? new Color(0.3f, 0.85f, 0.45f) : new Color(0.2f, 0.65f, 0.35f)) : new Color(0.4f, 0.2f, 0.2f);
					DrawRect(buyBtnRect, btnColor);
					DrawRect(buyBtnRect, isHover ? Colors.White : Colors.LimeGreen, false, isHover ? 2.0f : 1.0f);
					DrawString(font, buyBtnRect.Position + new Vector2(10, 21), $"{item.BuyPrice} ⚙ 购入", HorizontalAlignment.Center, -1, 11, Colors.White);
				}
			}

			// 4. 绘制右栏：本舰构件与折旧回收
			DrawString(font, panelArea.Position + new Vector2(560, 72), "[ 本舰已挂载构件 (可拆解回收) ]", HorizontalAlignment.Left, -1, 13, Colors.Yellow);
			if (TargetShip != null)
			{
				var modules = TargetShip.Grid.Modules.ToList();
				for (int i = 0; i < Mathf.Min(6, modules.Count); i++)
				{
					var mod = modules[i];
					Rect2 modRect = GetShipModRect(i);
					Color bgModColor = mod.IsDestroyed ? new Color(0.15f, 0.05f, 0.05f, 0.6f) : new Color(0.08f, 0.14f, 0.12f, 0.85f);
					DrawRect(modRect, bgModColor);
					DrawRect(modRect, new Color(0.3f, 0.6f, 0.4f, 0.6f), false, 1.2f);

					string modName = mod.IsDestroyed ? $"[已损毁/已拆解] {mod.Definition.Name}" : $"{mod.Definition.Name} ({mod.Definition.Category})";
					DrawString(font, modRect.Position + new Vector2(15, 24), modName, HorizontalAlignment.Left, -1, 12, mod.IsDestroyed ? Colors.Gray : Colors.White);
					DrawString(font, modRect.Position + new Vector2(15, 42), $"耐久: {mod.CurrentHp:F0}/{mod.MaxHp:F0} HP", HorizontalAlignment.Left, -1, 11, mod.IsDestroyed ? Colors.Gray : Colors.LightGreen);

					if (!mod.IsDestroyed)
					{
						int sellPrice = BlackMarketService.CalculateSellPrice(mod.Definition, mod.CurrentHp / mod.MaxHp);
						Rect2 sellBtnRect = GetSellButtonRect(i);
						bool isHover = sellBtnRect.HasPoint(_currentMousePos);

						Color btnColor = isHover ? new Color(0.85f, 0.60f, 0.20f) : new Color(0.65f, 0.45f, 0.15f);
						DrawRect(sellBtnRect, btnColor);
						DrawRect(sellBtnRect, isHover ? Colors.White : Colors.Gold, false, isHover ? 2.0f : 1.0f);
						DrawString(font, sellBtnRect.Position + new Vector2(10, 21), $"+{sellPrice} ⚙ 拆解", HorizontalAlignment.Center, -1, 11, Colors.White);
					}
				}
			}

			// 5. 绘制底部专属服务与离港返回栏
			DrawLine(panelArea.Position + new Vector2(20, panelArea.Size.Y - 75), panelArea.Position + new Vector2(panelArea.Size.X - 20, panelArea.Size.Y - 75), new Color(0.4f, 0.5f, 0.6f, 0.4f), 1.5f);

			// 按钮 0: 离港返回 (ESC)
			Rect2 leaveBtn = GetLeaveButtonRect();
			bool hoverLeave = leaveBtn.HasPoint(_currentMousePos);
			DrawRect(leaveBtn, hoverLeave ? new Color(0.45f, 0.15f, 0.15f) : new Color(0.22f, 0.08f, 0.08f));
			DrawRect(leaveBtn, hoverLeave ? Colors.White : Colors.OrangeRed, false, 1.2f);
			DrawString(font, leaveBtn.Position + new Vector2(20, 24), "◀ 离港返回 (ESC)", HorizontalAlignment.Center, -1, 12, Colors.White);

			// 按钮 1: 全舰大修
			Rect2 srvARect = GetRepairButtonRect();
			bool hoverA = srvARect.HasPoint(_currentMousePos);
			DrawRect(srvARect, hoverA ? new Color(0.25f, 0.60f, 0.45f) : new Color(0.18f, 0.45f, 0.35f));
			DrawRect(srvARect, hoverA ? Colors.White : Colors.LimeGreen, false, 1.2f);
			DrawString(font, srvARect.Position + new Vector2(15, 24), "🔧 全舰大修 (100 ⚙)", HorizontalAlignment.Left, -1, 12, Colors.White);

			// 按钮 2: 爆甲重装
			Rect2 srvBRect = GetAblativeButtonRect();
			bool hoverB = srvBRect.HasPoint(_currentMousePos);
			DrawRect(srvBRect, hoverB ? new Color(0.35f, 0.48f, 0.75f) : new Color(0.25f, 0.35f, 0.55f));
			DrawRect(srvBRect, hoverB ? Colors.White : Colors.Cyan, false, 1.2f);
			DrawString(font, srvBRect.Position + new Vector2(15, 24), "💥 爆甲重装 (75 ⚙)", HorizontalAlignment.Left, -1, 12, Colors.White);

			// 按钮 3: 刷新货架
			Rect2 srvCRect = GetRerollButtonRect();
			bool hoverC = srvCRect.HasPoint(_currentMousePos);
			DrawRect(srvCRect, hoverC ? new Color(0.65f, 0.50f, 0.20f) : new Color(0.45f, 0.35f, 0.15f));
			DrawRect(srvCRect, hoverC ? Colors.White : Colors.Gold, false, 1.2f);
			DrawString(font, srvCRect.Position + new Vector2(15, 24), "🔄 刷新货架 (30 ⚙)", HorizontalAlignment.Left, -1, 12, Colors.White);

			// 交易反馈提示信息
			DrawString(font, panelArea.Position + new Vector2(835, panelArea.Size.Y - 36), _transactionFeedback, HorizontalAlignment.Left, -1, 12, _feedbackColor);
		}
	}
}
