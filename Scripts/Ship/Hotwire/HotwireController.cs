using System.Collections.Generic;
using System.Linq;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.Ship.Pipeline;
using BreakerProtocol.Combat.Effects;

namespace BreakerProtocol.Ship.Hotwire
{
	/// <summary>
	/// 战地应急飞线抢修控制器 (规范 06《战地应急飞线系统》)
	/// </summary>
	public class HotwireController
	{
		private readonly ShipEntity _ship;

		/// <summary>
		/// 剩余应急飞线补丁数 (默认 5 个)
		/// </summary>
		public int HotwirePatchesRemaining { get; set; } = 5;

		/// <summary>
		/// 是否当前正处于战地飞线模式 (按住 F 键)
		/// </summary>
		public bool IsInHotwireMode { get; private set; } = false;

		/// <summary>
		/// 子弹时间最长持续时间 (真实时间 2.0 秒)
		/// </summary>
		public const float MaxBulletTimeDuration = 2.0f;
		public float CurrentBulletTimeTimer { get; private set; } = 0.0f;

		// 鼠标拖拽划线状态
		public PinInstance? DragStartPin { get; private set; }
		public Vector2 DragCurrentMouseWorldPos { get; private set; }

		public HotwireController(ShipEntity ship)
		{
			_ship = ship;
		}

		public void Update(float realDelta)
		{
			// 1. 监听 F 键按住状态
			bool isFPressed = Input.IsKeyPressed(Key.F);

			if (isFPressed && HotwirePatchesRemaining > 0)
			{
				if (!IsInHotwireMode)
				{
					EnterHotwireMode();
				}

				// 子弹时间计时消耗 (按不受 TimeScale 影响的真实时间递减)
				CurrentBulletTimeTimer -= realDelta;
				if (CurrentBulletTimeTimer <= 0.0f)
				{
					// 持续时间耗尽，强制恢复常速但仍保持飞线视口
					Engine.TimeScale = 1.0f;
				}

				HandleMouseDrag();
			}
			else
			{
				if (IsInHotwireMode)
				{
					ExitHotwireMode();
				}
			}
		}

		private void EnterHotwireMode()
		{
			IsInHotwireMode = true;
			CurrentBulletTimeTimer = MaxBulletTimeDuration;
			Engine.TimeScale = 0.20f; // 0.2x 战术子弹时间

			VfxManager.Instance?.SpawnFloatingText(_ship.GlobalPosition, "⚡ 战术飞线子弹时间 [0.2x]", Colors.Yellow);
			GD.PrintRich("[color=yellow][Hotwire] ⏱️ 进入战地飞线模式：0.2x 战术子弹时间激活，引脚透视高亮！[/color]");
		}

		private void ExitHotwireMode()
		{
			IsInHotwireMode = false;
			DragStartPin = null;
			Engine.TimeScale = 1.0f; // 恢复常速
			GD.Print("[Hotwire] 退出战地飞线模式，恢复常速物理运行。");
		}

		private void HandleMouseDrag()
		{
			var viewport = _ship.GetViewport();
			if (viewport == null) return;

			Vector2 mouseWorld = _ship.GetGlobalMousePosition();
			DragCurrentMouseWorldPos = mouseWorld;

			// 将鼠标世界坐标转为战舰局部网格坐标
			Vector2 localMousePixels = _ship.ToLocal(mouseWorld);
			Vector2 localMouseGrid = GlobalMetrics.PixelsToMeters(localMousePixels);

			// 鼠标左键按下：拾取起点 OUT 引脚
			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				if (DragStartPin == null)
				{
					var hoveredPin = FindClosestPin(localMouseGrid, PinType.OUT, maxDistanceGu: 1.5f);
					if (hoveredPin != null)
					{
						DragStartPin = hoveredPin;
						VfxManager.Instance?.SpawnElectricArc(mouseWorld, mouseWorld + new Vector2(10, -10), Colors.Cyan);
					}
				}
			}
			else
			{
				// 鼠标左键松开：若有起点引脚，检查是否释放在合法的 IN 引脚上
				if (DragStartPin != null)
				{
					var targetInPin = FindClosestPin(localMouseGrid, PinType.IN, maxDistanceGu: 1.5f);
					if (targetInPin != null)
					{
						TryDeployHotwire(DragStartPin, targetInPin);
					}

					DragStartPin = null;
				}
			}
		}

		private bool TryDeployHotwire(PinInstance srcPin, PinInstance dstPin)
		{
			if (HotwirePatchesRemaining <= 0)
			{
				VfxManager.Instance?.SpawnFloatingText(_ship.GlobalPosition, "❌ 应急飞线补丁耗尽！", Colors.Red);
				return false;
			}

			// 尝试接入管线
			if (_ship.Pipeline.TryAddWire(srcPin, dstPin, _ship.Grid, out var wire))
			{
				if (wire != null)
				{
					// 标记为战地应急飞线：耐久仅 15 HP
					wire.IsHotwire = true;
					wire.DurabilityHp = 15.0f;

					HotwirePatchesRemaining--;

					// 刷新物理与武器缓存
					_ship.RebuildPhysics();

					Vector2 pinPosWorld = _ship.GlobalTransform * GlobalMetrics.MetersToPixels((Vector2)dstPin.AbsoluteGridPos + new Vector2(0.5f, 0.5f));
					VfxManager.Instance?.SpawnElectricArc(pinPosWorld, pinPosWorld + new Vector2(20, -20), Colors.Gold);
					VfxManager.Instance?.SpawnFloatingText(pinPosWorld, $"⚡ 应急飞线接通！(剩余补丁: {HotwirePatchesRemaining})", Colors.GreenYellow);
					JuiceManager.Instance?.TriggerHitstop(0.04f, 0.08f);

					GD.PrintRich($"[color=green][Hotwire] 🛠️ 成功部署战地应急飞线！[{wire.WireId}] 耐久: 15 HP | 剩余补丁: {HotwirePatchesRemaining}[/color]");
					return true;
				}
			}

			return false;
		}

		private PinInstance? FindClosestPin(Vector2 localGridPos, PinType requiredType, float maxDistanceGu)
		{
			PinInstance? bestPin = null;
			float minDst = maxDistanceGu;

			foreach (var pin in _ship.GetAllPins())
			{
				if (pin.Type != requiredType) continue;

				float dst = ((Vector2)pin.AbsoluteGridPos + new Vector2(0.5f, 0.5f)).DistanceTo(localGridPos);
				if (dst < minDst)
				{
					minDst = dst;
					bestPin = pin;
				}
			}

			return bestPin;
		}

		public void Reset()
		{
			HotwirePatchesRemaining = 5;
			ExitHotwireMode();
		}
	}
}
