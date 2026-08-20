using System.Collections.Generic;
using System.Linq;
using Godot;
using BreakerProtocol.Combat.Boss;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.Ship.Abilities;

namespace BreakerProtocol.UI.CombatHUD
{
	public enum ColorblindMode
	{
		Normal,       // 标准全彩
		Protanopia,   // 红色盲 (红转亮金/洋红)
		Deuteranopia, // 绿色盲 (绿转青蓝)
		Tritanopia    // 蓝黄色盲 (高对比青红)
	}

	/// <summary>
	/// 战术全息战斗 HUD (准星遥测环 + 1:1 饱满战损纸娃娃 + 离屏敌舰雷达 + 无障碍色彩)
	/// </summary>
	public partial class CombatHUD : Control
	{
		public ShipEntity? TargetShip { get; set; }
		public List<Node2D> TrackedEnemies { get; } = new();
		public ColorblindMode CurrentColorblindMode { get; set; } = ColorblindMode.Normal;

		private float _animTime = 0.0f;
		private Vector2 _reticlePos = Vector2.Zero;

		public override void _Ready()
		{
			SetAnchorsPreset(LayoutPreset.FullRect);
			GrowHorizontal = GrowDirection.Both;
			GrowVertical = GrowDirection.Both;
			MouseFilter = MouseFilterEnum.Ignore;
		}

		public override void _Process(double delta)
		{
			_animTime += (float)delta * 4.0f;
			_reticlePos = GetLocalMousePosition();
			QueueRedraw();
		}

		public override void _Draw()
		{
			if (TargetShip == null || !GodotObject.IsInstanceValid(TargetShip)) return;

			Vector2 vpSize = GetViewportRect().Size;
			float w = vpSize.X > 100 ? vpSize.X : 1280.0f;
			float h = vpSize.Y > 100 ? vpSize.Y : 720.0f;

			// 1. 绘制鼠标准星全息遥测双弧
			DrawReticleTelemetry(_reticlePos);

			// 2. 绘制左下角 1:1 饱满战损纸娃娃
			DrawHolographicPaperdoll(h);

			// 3. 绘制离屏敌舰全向雷达箭头
			DrawOffscreenThreatIndicators(new Rect2(30, 30, w - 60, h - 60));
		}

		// -------------------------------------------------------------
		// 1. 准星遥测环 (Reticle Telemetry)
		// -------------------------------------------------------------
		private void DrawReticleTelemetry(Vector2 center)
		{
			float radius = 32.0f;
			float heatRatio = TargetShip!.Thermal != null ? TargetShip.Thermal.OverheatRatio : 0.0f;
			bool isOverheated = TargetShip.Thermal != null && TargetShip.Thermal.IsOverheated;

			// 左弧：电容带宽负载
			float bandwidthRatio = Mathf.Clamp(0.5f + Mathf.Sin(_animTime * 0.5f) * 0.25f, 0.1f, 1.0f);
			Color bandwidthColor = GetAdaptiveColor(Colors.Cyan, Colors.SkyBlue, Colors.Cyan);

			float leftStart = Mathf.DegToRad(135);
			float leftEnd = Mathf.DegToRad(225);
			DrawArc(center, radius, leftStart, leftEnd, 16, new Color(0.2f, 0.4f, 0.6f, 0.35f), 3.0f);
			float leftFillEnd = leftStart + (leftEnd - leftStart) * bandwidthRatio;
			DrawArc(center, radius, leftStart, leftFillEnd, 16, bandwidthColor, 3.0f);

			// 右弧：热力学发热积分
			float rightStart = Mathf.DegToRad(-45);
			float rightEnd = Mathf.DegToRad(45);
			Color heatColor = isOverheated ? Colors.Red : (heatRatio > 0.7f ? Colors.Orange : Colors.LightGreen);
			heatColor = GetAdaptiveColor(heatColor, Colors.Gold, Colors.Magenta);

			DrawArc(center, radius, rightStart, rightEnd, 16, new Color(0.6f, 0.2f, 0.2f, 0.35f), 3.0f);
			float rightFillEnd = rightStart + (rightEnd - rightStart) * heatRatio;
			DrawArc(center, radius, rightStart, rightFillEnd, 16, heatColor, 3.0f);

			// 准星十字与中心点
			DrawLine(center + new Vector2(-6, 0), center + new Vector2(6, 0), Colors.White, 1.2f);
			DrawLine(center + new Vector2(0, -6), center + new Vector2(0, 6), Colors.White, 1.2f);
			DrawCircle(center, 2.0f, Colors.White);

			// 过热红闪报警
			if (isOverheated || heatRatio > 0.85f)
			{
				float flash = (Mathf.Sin(_animTime * 3.0f) + 1.0f) * 0.5f;
				var font = ThemeDB.FallbackFont;
				Color warnColor = new(1.0f, 0.2f, 0.2f, flash);
				DrawString(font, center + new Vector2(-30, -radius - 8.0f), "⚠️ OVERHEAT", HorizontalAlignment.Center, -1, 10, warnColor);
			}
		}

		// -------------------------------------------------------------
		// 2. 1:1 饱满战损纸娃娃 (Holographic Paperdoll)
		// -------------------------------------------------------------
		private void DrawHolographicPaperdoll(float screenH)
		{
			var font = ThemeDB.FallbackFont;
			var modules = TargetShip!.Grid.Modules.ToList();
			if (modules.Count == 0) return;

			// 1. 动态计算真实包围盒（严格计入构件实际 Width / Height）
			float minX = float.MaxValue, minY = float.MaxValue;
			float maxX = float.MinValue, maxY = float.MinValue;
			float currentTotalHp = 0.0f;
			float maxTotalHp = 0.0f;

			foreach (var mod in modules)
			{
				int modW = mod.Definition.Width > 0 ? mod.Definition.Width : 1;
				int modH = mod.Definition.Height > 0 ? mod.Definition.Height : 1;

				minX = Mathf.Min(minX, mod.GridPosition.X);
				minY = Mathf.Min(minY, mod.GridPosition.Y);
				maxX = Mathf.Max(maxX, mod.GridPosition.X + modW);
				maxY = Mathf.Max(maxY, mod.GridPosition.Y + modH);

				if (!mod.IsDestroyed) currentTotalHp += mod.CurrentHp;
				maxTotalHp += mod.MaxHp;
			}

			Vector2 shipCenterGrid = new((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
			float spanX = Mathf.Max(1.0f, maxX - minX);
			float spanY = Mathf.Max(1.0f, maxY - minY);
			float maxSpan = Mathf.Max(spanX, spanY);

			// 纸娃娃底板框 (宽 210, 高 220)
			Rect2 paperdollBox = new(20, screenH - 240, 210, 220);

			// 绘制科幻全息底板
			DrawRect(paperdollBox, new Color(0.02f, 0.06f, 0.10f, 0.94f));
			DrawRect(paperdollBox, new Color(0.2f, 0.65f, 0.95f, 0.85f), false, 2.0f);

			// 标头与总耐久状态
			float overallHpPercent = maxTotalHp > 0 ? (currentTotalHp / maxTotalHp * 100.0f) : 0.0f;
			Color titleColor = overallHpPercent > 50 ? Colors.Cyan : (overallHpPercent > 25 ? Colors.Yellow : Colors.OrangeRed);

			DrawString(font, paperdollBox.Position + new Vector2(12, 22), "【战损纸娃娃·结构投影】", HorizontalAlignment.Left, -1, 12, titleColor);
			DrawString(font, paperdollBox.Position + new Vector2(12, 40), $"总耐久: {currentTotalHp:F0}/{maxTotalHp:F0} ({overallHpPercent:F0}%)", HorizontalAlignment.Left, -1, 11, Colors.LightGreen);

			// 自适应单格像素跨度 (保证无论大小舰船都能饱满居中)
			Vector2 visualCenter = paperdollBox.Position + new Vector2(105, 118);
			float cellSize = Mathf.Clamp(110.0f / maxSpan, 8.0f, 16.0f);

			// 2. 绘制各构件真实多格实体色块
			foreach (var mod in modules)
			{
				int modW = mod.Definition.Width > 0 ? mod.Definition.Width : 1;
				int modH = mod.Definition.Height > 0 ? mod.Definition.Height : 1;

				float relX = (mod.GridPosition.X - shipCenterGrid.X) * cellSize;
				float relY = (mod.GridPosition.Y - shipCenterGrid.Y) * cellSize;
				Vector2 drawTopLeft = visualCenter + new Vector2(relX, relY);

				float cellW = modW * cellSize;
				float cellH = modH * cellSize;
				Rect2 modRect = new(drawTopLeft.X, drawTopLeft.Y, cellW - 1.5f, cellH - 1.5f);

				float hpRatio = mod.MaxHp > 0 ? (mod.CurrentHp / mod.MaxHp) : 0.0f;
				Color modColor;

				if (mod.IsDestroyed || hpRatio <= 0.0f)
				{
					modColor = new Color(0.20f, 0.08f, 0.08f, 0.85f);
				}
				else if (hpRatio > 0.70f)
				{
					modColor = GetAdaptiveColor(new Color(0.2f, 0.9f, 0.4f), Colors.Cyan, Colors.LimeGreen);
				}
				else if (hpRatio > 0.35f)
				{
					modColor = GetAdaptiveColor(Colors.Gold, Colors.Gold, Colors.Yellow);
				}
				else
				{
					modColor = GetAdaptiveColor(Colors.OrangeRed, Colors.Magenta, Colors.Red);
				}

				DrawRect(modRect, modColor);
				DrawRect(modRect, modColor.Lightened(0.35f), false, 1.0f);

				// 受损断线红叉闪烁
				if (!mod.IsDestroyed && hpRatio < 0.4f)
				{
					float flash = (Mathf.Sin(_animTime * 4.0f) + 1.0f) * 0.5f;
					DrawLine(modRect.Position, modRect.End, new Color(1, 1, 1, flash), 1.5f);
					DrawLine(new Vector2(modRect.End.X, modRect.Position.Y), new Vector2(modRect.Position.X, modRect.End.Y), new Color(1, 1, 1, flash), 1.5f);
				}
			}

			// 3. 底部爆甲状态栏
			DrawLine(paperdollBox.Position + new Vector2(8, 185), paperdollBox.Position + new Vector2(202, 185), new Color(0.3f, 0.5f, 0.7f, 0.4f), 1.0f);
			DrawString(font, paperdollBox.Position + new Vector2(10, 205), "[Q]左翼  [E]右翼  [Z]尾舱", HorizontalAlignment.Center, -1, 11, Colors.Gold);
		}

		// -------------------------------------------------------------
		// 3. 离屏敌舰全向雷达箭头
		// -------------------------------------------------------------
		private void DrawOffscreenThreatIndicators(Rect2 safeBounds)
		{
			if (TargetShip == null) return;
			var font = ThemeDB.FallbackFont;
			Vector2 playerScreenPos = TargetShip.GetGlobalTransformWithCanvas().Origin;

			foreach (var enemy in TrackedEnemies)
			{
				if (!GodotObject.IsInstanceValid(enemy)) continue;

				Vector2 enemyScreenPos = enemy.GetGlobalTransformWithCanvas().Origin;

				if (!safeBounds.HasPoint(enemyScreenPos))
				{
					Vector2 toEnemy = enemyScreenPos - playerScreenPos;
					float distanceMeters = TargetShip.GlobalPosition.DistanceTo(enemy.GlobalPosition) * 0.125f;

					Vector2 edgePos = ClampToScreenEdge(playerScreenPos, toEnemy, safeBounds);
					Vector2 arrowDir = toEnemy.Normalized();

					Color arrowColor = GetAdaptiveColor(Colors.OrangeRed, Colors.Magenta, Colors.Red);
					DrawArrow(edgePos, arrowDir, 14.0f, arrowColor);

					DrawString(font, edgePos + (arrowDir * 16.0f) + new Vector2(-15, 4), $"{distanceMeters:F0}m", HorizontalAlignment.Center, -1, 10, Colors.White);
				}
			}
		}

		private Vector2 ClampToScreenEdge(Vector2 center, Vector2 dir, Rect2 bounds)
		{
			Vector2 normDir = dir.Normalized();
			float tMin = float.MaxValue;

			if (normDir.X > 0)
			{
				float t = (bounds.End.X - center.X) / normDir.X;
				if (t > 0 && t < tMin) tMin = t;
			}
			else if (normDir.X < 0)
			{
				float t = (bounds.Position.X - center.X) / normDir.X;
				if (t > 0 && t < tMin) tMin = t;
			}

			if (normDir.Y > 0)
			{
				float t = (bounds.End.Y - center.Y) / normDir.Y;
				if (t > 0 && t < tMin) tMin = t;
			}
			else if (normDir.Y < 0)
			{
				float t = (bounds.Position.Y - center.Y) / normDir.Y;
				if (t > 0 && t < tMin) tMin = t;
			}

			return center + (normDir * tMin);
		}

		private void DrawArrow(Vector2 pos, Vector2 dir, float size, Color color)
		{
			Vector2 right = new(-dir.Y, dir.X);
			Vector2 p1 = pos + (dir * size);
			Vector2 p2 = pos - (dir * (size * 0.5f)) + (right * (size * 0.5f));
			Vector2 p3 = pos - (dir * (size * 0.5f)) - (right * (size * 0.5f));

			Vector2[] poly = new Vector2[] { p1, p2, p3 };
			DrawColoredPolygon(poly, color);
		}

		private Color GetAdaptiveColor(Color normal, Color protanopiaColor, Color tritanopiaColor)
		{
			return CurrentColorblindMode switch
			{
				ColorblindMode.Protanopia or ColorblindMode.Deuteranopia => protanopiaColor,
				ColorblindMode.Tritanopia                                => tritanopiaColor,
				_                                                        => normal
			};
		}
	}
}
