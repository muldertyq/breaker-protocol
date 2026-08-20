using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;
using BreakerProtocol.World.Meta;

namespace BreakerProtocol.UI.Hangar
{
	public class ShipOptionData
	{
		public string BlueprintId { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string FactionName { get; set; } = string.Empty;
		public string RoleDesc { get; set; } = string.Empty;
		public Color ThemeColor { get; set; } = Colors.Cyan;
		public string TraitDescription { get; set; } = string.Empty;
		public string PrimaryWeapons { get; set; } = string.Empty;
	}

	/// <summary>
	/// 母港出征选船机库界面 (全息战舰预览、性能遥测与出征总装)
	/// </summary>
	public partial class FleetHangarUI : Control
	{
		public event Action<string>? OnShipSelectedAndEngage;
		public event Action? OnBackToMainMenu;

		private readonly List<ShipOptionData> _availableShips = new();
		private int _selectedIndex = 0;
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

			InitializeShipRoster();
		}

		private void InitializeShipRoster()
		{
			_availableShips.Clear();

			// 1. 重工联合 (Heavy Foundry) - 铁砧级中型巡洋舰
			_availableShips.Add(new ShipOptionData
			{
				BlueprintId = "bp_hf_m_anvil",
				Name = "【铁砧级】中型装甲巡洋舰",
				FactionName = "重工联合 (Heavy Foundry)",
				RoleDesc = "重甲突击 / 动能穿甲 / 预应力战术爆甲",
				ThemeColor = new Color(1.0f, 0.55f, 0.2f),
				TraitDescription = "• 装甲基础抗性极高，前倾角装甲极易引发物理跳弹\n" +
								   "• 搭载重型电磁轨道炮与大口径加特林速射炮阵列\n" +
								   "• 专属特技 [Q/E]：战术爆甲化作 24 枚破片散弹清屏",
				PrimaryWeapons = "重型磁轨炮 + 旋转加特林机炮"
			});

			// 2. 虚空财团 (Void Syndicate) - 棱镜级中型截击舰
			_availableShips.Add(new ShipOptionData
			{
				BlueprintId = "bp_vs_m_prism",
				Name = "【棱镜级】中型相位截击舰",
				FactionName = "虚空财团 (Void Syndicate)",
				RoleDesc = "超频能量 / 光子分束 / 引力坍缩控制",
				ThemeColor = new Color(0.2f, 0.85f, 1.0f),
				TraitDescription = "• 极低船体质量与极高回转推重比，机动性能卓绝\n" +
								   "• 搭载相位聚能激光束与分光三棱镜流水线\n" +
								   "• 专属特技 [Q/E]：引爆侧翼生成微型引力坍缩黑洞聚怪",
				PrimaryWeapons = "相位激光脉冲 + 光子分束修饰舱"
			});

			// 3. 深空生化 (Bio Chitin) - 甲壳级中型甲壳舰
			_availableShips.Add(new ShipOptionData
			{
				BlueprintId = "bp_bc_m_carapace",
				Name = "【甲壳级】中型几丁质重母舰",
				FactionName = "深空生化帮 (Bio Chitin)",
				RoleDesc = "生化酸蚀 / 几丁质自愈 / 孢子蜂群",
				ThemeColor = new Color(0.45f, 0.95f, 0.35f),
				TraitDescription = "• 几丁质外骨骼受创后具备缓慢自我生物代谢再生能力\n" +
								   "• 搭载高腐蚀酸液喷射器与自动引导孢子发射器\n" +
								   "• 专属特技 [Q/E]：爆破释放大范围生化强酸毒雾并孵化飞虫",
				PrimaryWeapons = "强酸喷射器 + 追踪孢子发射器"
			});
		}

		public override void _Process(double delta)
		{
			if (!Visible) return;

			Vector2 vpSize = GetViewportRect().Size;
			if (vpSize.X > 100 && vpSize.Y > 100 && Size != vpSize)
			{
				Size = vpSize;
				CustomMinimumSize = vpSize;
			}

			_currentMousePos = GetLocalMousePosition();
			QueueRedraw();
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
				if (ek.Keycode == Key.A || ek.Keycode == Key.Left)
				{
					_selectedIndex = (_selectedIndex - 1 + _availableShips.Count) % _availableShips.Count;
				}
				else if (ek.Keycode == Key.D || ek.Keycode == Key.Right)
				{
					_selectedIndex = (_selectedIndex + 1) % _availableShips.Count;
				}
				else if (ek.Keycode == Key.Enter || ek.Keycode == Key.Space)
				{
					ConfirmEngage();
				}
				else if (ek.Keycode == Key.Escape)
				{
					OnBackToMainMenu?.Invoke();
				}
			}
		}

		private void HandleClick(Vector2 clickPos)
		{
			Vector2 vpSize = GetViewportRect().Size;
			Rect2 displayBox = new(60, 95, vpSize.X * 0.44f, vpSize.Y * 0.66f);

			Rect2 leftArrowRect = new(displayBox.Position.X + 15, displayBox.Position.Y + (displayBox.Size.Y * 0.5f) - 25, 45, 50);
			Rect2 rightArrowRect = new(displayBox.End.X - 60, displayBox.Position.Y + (displayBox.Size.Y * 0.5f) - 25, 45, 50);

			if (leftArrowRect.HasPoint(clickPos))
			{
				_selectedIndex = (_selectedIndex - 1 + _availableShips.Count) % _availableShips.Count;
				return;
			}
			if (rightArrowRect.HasPoint(clickPos))
			{
				_selectedIndex = (_selectedIndex + 1) % _availableShips.Count;
				return;
			}

			if (GetLaunchButtonRect().HasPoint(clickPos))
			{
				ConfirmEngage();
				return;
			}

			if (GetBackButtonRect().HasPoint(clickPos))
			{
				OnBackToMainMenu?.Invoke();
				return;
			}
		}

		private void ConfirmEngage()
		{
			var chosen = _availableShips[_selectedIndex];
			OnShipSelectedAndEngage?.Invoke(chosen.BlueprintId);
		}

		private Rect2 GetLaunchButtonRect()
		{
			Vector2 vpSize = GetViewportRect().Size;
			return new Rect2(vpSize.X - 380, vpSize.Y - 100, 320, 52);
		}

		private Rect2 GetBackButtonRect()
		{
			Vector2 vpSize = GetViewportRect().Size;
			return new Rect2(60, vpSize.Y - 100, 200, 52);
		}

		public override void _Draw()
		{
			if (!Visible || _availableShips.Count == 0) return;

			var font = ThemeDB.FallbackFont;
			Vector2 vpSize = GetViewportRect().Size;
			var ship = _availableShips[_selectedIndex];

			// 1. 全屏背景底板
			DrawRect(new Rect2(Vector2.Zero, vpSize), new Color(0.02f, 0.04f, 0.08f, 0.96f));

			// 2. 标头栏
			DrawString(font, new Vector2(60, 50), "✦ 母港出征战备机库 · 原型战舰总装 ✦ FLEET HANGAR NEXUS", HorizontalAlignment.Left, -1, 18, Colors.Cyan);
			DrawString(font, new Vector2(vpSize.X - 320, 50), $"已解锁研发碎片: {MetaProgressionManager.Instance.DataFragments} 💾", HorizontalAlignment.Right, -1, 14, Colors.Gold);
			DrawLine(new Vector2(50, 68), new Vector2(vpSize.X - 50, 68), new Color(0.2f, 0.4f, 0.6f, 0.5f), 1.5f);

			// 3. 左侧线框图展示舱
			Rect2 displayBox = new(60, 95, vpSize.X * 0.44f, vpSize.Y * 0.66f);
			DrawRect(displayBox, new Color(0.04f, 0.08f, 0.14f, 0.85f));
			DrawRect(displayBox, ship.ThemeColor, false, 2.0f);

			for (float y = displayBox.Position.Y + 20; y < displayBox.End.Y; y += 30)
			{
				DrawLine(new Vector2(displayBox.Position.X, y), new Vector2(displayBox.End.X, y), new Color(ship.ThemeColor.R, ship.ThemeColor.G, ship.ThemeColor.B, 0.08f), 1.0f);
			}

			DrawShipBlueprintSchematic(displayBox, ship.BlueprintId, ship.ThemeColor);

			// 左右切换箭头
			Rect2 leftArrowRect = new(displayBox.Position.X + 15, displayBox.Position.Y + (displayBox.Size.Y * 0.5f) - 25, 45, 50);
			Rect2 rightArrowRect = new(displayBox.End.X - 60, displayBox.Position.Y + (displayBox.Size.Y * 0.5f) - 25, 45, 50);

			bool isLeftHover = leftArrowRect.HasPoint(_currentMousePos);
			bool isRightHover = rightArrowRect.HasPoint(_currentMousePos);

			DrawRect(leftArrowRect, isLeftHover ? new Color(0.2f, 0.4f, 0.6f, 0.8f) : new Color(0.1f, 0.15f, 0.25f, 0.6f));
			DrawRect(leftArrowRect, isLeftHover ? Colors.White : Colors.Cyan, false, 1.5f);
			DrawString(font, leftArrowRect.Position + new Vector2(14, 32), "◀", HorizontalAlignment.Center, -1, 18, isLeftHover ? Colors.Gold : Colors.White);

			DrawRect(rightArrowRect, isRightHover ? new Color(0.2f, 0.4f, 0.6f, 0.8f) : new Color(0.1f, 0.15f, 0.25f, 0.6f));
			DrawRect(rightArrowRect, isRightHover ? Colors.White : Colors.Cyan, false, 1.5f);
			DrawString(font, rightArrowRect.Position + new Vector2(14, 32), "▶", HorizontalAlignment.Center, -1, 18, isRightHover ? Colors.Gold : Colors.White);

			// 4. 右侧详细战术遥测
			float rightX = displayBox.End.X + 40;
			float rightW = vpSize.X - rightX - 60;
			Rect2 detailBox = new(rightX, 95, rightW, vpSize.Y * 0.66f);
			DrawRect(detailBox, new Color(0.03f, 0.06f, 0.10f, 0.90f));
			DrawRect(detailBox, new Color(0.3f, 0.5f, 0.7f, 0.5f), false, 1.5f);

			DrawString(font, detailBox.Position + new Vector2(25, 42), ship.Name, HorizontalAlignment.Left, -1, 22, Colors.Gold);
			DrawString(font, detailBox.Position + new Vector2(25, 74), $"势力所属: {ship.FactionName} | 定位: {ship.RoleDesc}", HorizontalAlignment.Left, -1, 13, ship.ThemeColor);
			DrawLine(detailBox.Position + new Vector2(20, 90), detailBox.Position + new Vector2(detailBox.Size.X - 20, 90), new Color(0.3f, 0.5f, 0.7f, 0.4f), 1.0f);

			int moduleCount = 0;
			float totalHp = 0.0f;
			float totalMass = 0.0f;
			if (DataManager.Instance.Blueprints.TryGet(ship.BlueprintId, out var bp) && bp != null)
			{
				moduleCount = bp.Modules.Count;
				foreach (var m in bp.Modules)
				{
					if (DataManager.Instance.Modules.TryGet(m.ModuleId, out var mDef) && mDef != null)
					{
						totalHp += mDef.BaseHp;
						totalMass += mDef.Mass;
					}
				}
			}

			float attrY = detailBox.Position.Y + 125;
			DrawAttributeBar(font, detailBox.Position.X + 25, attrY, "舰体耐久总量", $"{totalHp:F0} HP", totalHp / 1800.0f, Colors.LimeGreen);
			DrawAttributeBar(font, detailBox.Position.X + 25, attrY + 36, "全舰整备质量", $"{totalMass:F1} 吨", totalMass / 60.0f, Colors.Yellow);
			DrawAttributeBar(font, detailBox.Position.X + 25, attrY + 72, "预装构件规模", $"{moduleCount} 模块", moduleCount / 20.0f, Colors.Cyan);
			DrawAttributeBar(font, detailBox.Position.X + 25, attrY + 108, "武器系统配置", ship.PrimaryWeapons, 1.0f, Colors.Orange);

			float traitY = attrY + 160;
			DrawString(font, new Vector2(detailBox.Position.X + 25, traitY), "【 战术特权与核心机制 】", HorizontalAlignment.Left, -1, 14, Colors.Cyan);
			DrawString(font, new Vector2(detailBox.Position.X + 25, traitY + 28), ship.TraitDescription, HorizontalAlignment.Left, (int)detailBox.Size.X - 50, 13, Colors.White);

			// 5. 底部操作按钮
			Rect2 backBtn = GetBackButtonRect();
			bool isBackHover = backBtn.HasPoint(_currentMousePos);
			DrawRect(backBtn, isBackHover ? new Color(0.4f, 0.15f, 0.15f) : new Color(0.18f, 0.08f, 0.08f));
			DrawRect(backBtn, isBackHover ? Colors.White : Colors.OrangeRed, false, 1.5f);
			DrawString(font, backBtn.Position + new Vector2(40, 32), "◀ 返回主菜单", HorizontalAlignment.Center, -1, 14, Colors.White);

			Rect2 launchBtn = GetLaunchButtonRect();
			bool isLaunchHover = launchBtn.HasPoint(_currentMousePos);
			DrawRect(launchBtn, isLaunchHover ? new Color(0.15f, 0.60f, 0.45f) : new Color(0.08f, 0.35f, 0.25f));
			DrawRect(launchBtn, isLaunchHover ? Colors.White : Colors.LimeGreen, false, 2.0f);
			DrawString(font, launchBtn.Position + new Vector2(45, 32), "🚀 选定战舰 · 启航出征", HorizontalAlignment.Center, -1, 16, Colors.Gold);
		}

		private void DrawShipBlueprintSchematic(Rect2 box, string blueprintId, Color themeColor)
		{
			if (!DataManager.Instance.Blueprints.TryGet(blueprintId, out var bp) || bp == null) return;

			Vector2 center = box.Position + box.Size * 0.5f;
			float cellSize = 18.0f;

			foreach (var mod in bp.Modules)
			{
				if (!DataManager.Instance.Modules.TryGet(mod.ModuleId, out var def) || def == null) continue;

				Vector2 pos = center + new Vector2(mod.GridX * cellSize, mod.GridY * cellSize);
				Vector2 size = new(def.Width * cellSize - 2, def.Height * cellSize - 2);

				Color modColor = def.Category switch
				{
					"PowerSource" => Colors.Gold,
					"Weapon"      => Colors.Crimson,
					"Thruster"    => Colors.DodgerBlue,
					"Armor"       => Colors.DimGray,
					_             => themeColor
				};

				DrawRect(new Rect2(pos, size), new Color(modColor.R, modColor.G, modColor.B, 0.45f));
				DrawRect(new Rect2(pos, size), modColor, false, 1.2f);
			}
		}

		private void DrawAttributeBar(Font font, float x, float y, string title, string valStr, float ratio, Color barColor)
		{
			DrawString(font, new Vector2(x, y + 14), title, HorizontalAlignment.Left, -1, 12, Colors.LightGray);
			DrawString(font, new Vector2(x + 110, y + 14), valStr, HorizontalAlignment.Left, -1, 12, Colors.White);

			float barX = x + 260;
			float barW = 180.0f;
			float barH = 10.0f;

			DrawRect(new Rect2(barX, y + 4, barW, barH), new Color(0.1f, 0.15f, 0.2f, 0.8f));
			DrawRect(new Rect2(barX, y + 4, barW * Mathf.Clamp(ratio, 0.05f, 1.0f), barH), barColor);
			DrawRect(new Rect2(barX, y + 4, barW, barH), new Color(0.4f, 0.5f, 0.6f, 0.5f), false, 1.0f);
		}
	}
}
