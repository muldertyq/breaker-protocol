using System.Collections.Generic;
using System.Linq;
using Godot;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.PlayerInput;
using BreakerProtocol.Ship;
using BreakerProtocol.Ship.AI;
using BreakerProtocol.UI.CombatHUD;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-36 交互式验证场景：战斗 HUD 准星遥测环 + 全息战损纸娃娃演练场
	/// </summary>
	public partial class Test_Task36 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private BulletManager _bulletManager = null!;
		private CombatHUD _combatHUD = null!;
		private GamepadInputManager _gamepad = null!;
		private RichTextLabel _hudLabel = null!;

		private readonly List<ShipEntity> _dummyEnemies = new();

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			// 1. 生成玩家战舰
			_playerShip = new ShipEntity
			{
				Name = "PlayerCruiser",
				Position = Vector2.Zero
			};
			_playerShip.AddToGroup("Player");
			_playerShip.AddToGroup("Ship");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}

			// 2. 生成离屏测试敌舰
			SpawnDummyEnemy(new Vector2(1400, -200));
			SpawnDummyEnemy(new Vector2(-1200, 600));
			SpawnDummyEnemy(new Vector2(300, 1500));

			// 3. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateHUD();

			// 4. 手柄与无障碍输入管理器
			_gamepad = new GamepadInputManager
			{
				TargetShip = _playerShip,
				TargetHUD = _combatHUD
			};
			AddChild(_gamepad);
		}

		private void SpawnDummyEnemy(Vector2 pos)
		{
			var enemy = new ShipEntity
			{
				Name = $"Enemy_Target_{_dummyEnemies.Count + 1}",
				Position = pos
			};
			enemy.AddToGroup("Ship");
			enemy.CurrentPalette = FactionPalettes.VoidSyndicate;
			AddChild(enemy);

			if (DataManager.Instance.Blueprints.TryGet("bp_vs_s_phantom", out var bp))
			{
				ShipBlueprintLoader.ApplyBlueprint(enemy, bp!);
			}

			_dummyEnemies.Add(enemy);
		}

		private void CreateHUD()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_combatHUD = new CombatHUD
			{
				TargetShip = _playerShip
			};
			foreach (var e in _dummyEnemies)
			{
				_combatHUD.TrackedEnemies.Add(e);
			}
			canvasLayer.AddChild(_combatHUD);

			_hudLabel = new RichTextLabel
			{
				Position = new Vector2(30, 20),
				Size = new Vector2(1220, 120),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_hudLabel.AddThemeFontSizeOverride("normal_font_size", 14);
			canvasLayer.AddChild(_hudLabel);
		}

		// -------------------------------------------------------------
		// 单次按键监听：单次轻度扣血，杜绝每帧触发秒爆
		// -------------------------------------------------------------
		public override void _UnhandledInput(InputEvent @event)
		{
			if (@event is InputEventKey ek && ek.Pressed && !ek.Echo)
			{
				// [按 1 键]: 单次轻度受创 -30 HP (观察纸娃娃构件由绿变黄、变红)
				if (ek.Keycode == Key.Key1)
				{
					ApplySingleStepDamage(30.0f);
				}
				// [按 2 键]: 一键满血修复
				else if (ek.Keycode == Key.Key2)
				{
					RestoreShipFullHp();
				}
				// [按 C 键]: 切换色盲模式
				else if (ek.Keycode == Key.C)
				{
					_gamepad.CycleColorblindMode();
				}
				// [按 R 键]: 重新生成战舰
				else if (ek.Keycode == Key.R)
				{
					if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
					{
						ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
					}
				}
			}
		}

		private void ApplySingleStepDamage(float damage)
		{
			// 优先挑选一个当前血量最多的非核心构件进行受创
			var targetMod = _playerShip.Grid.Modules
				.Where(m => !m.IsDestroyed && m.Definition.Category != "Core")
				.OrderByDescending(m => m.CurrentHp)
				.FirstOrDefault();

			targetMod ??= _playerShip.Grid.Modules.FirstOrDefault(m => !m.IsDestroyed);

			if (targetMod != null)
			{
				targetMod.CurrentHp = Mathf.Max(0.0f, targetMod.CurrentHp - damage);
				_playerShip.OnModuleDamaged(targetMod, damage);

				Vector2 hitPos = _playerShip.GlobalPosition;
				_vfx.SpawnModuleExplosion(hitPos, new Vector2(25, 25), Colors.Orange, 8);
				_vfx.SpawnFloatingText(hitPos, $"-{damage:F0} HP", Colors.OrangeRed);
			}
		}

		private void RestoreShipFullHp()
		{
			foreach (var m in _playerShip.Grid.Modules)
			{
				m.CurrentHp = m.MaxHp;
			}
			_vfx.SpawnFloatingText(_playerShip.GlobalPosition, "🔧 全舰耐久已彻底修满！", Colors.LimeGreen);
		}

		public override void _Process(double delta)
		{
			// [鼠标左键]: 开火
			if (Godot.Input.IsMouseButtonPressed(MouseButton.Left) && !_playerShip.Hotwire.IsInHotwireMode)
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			double fps = Engine.GetFramesPerSecond();
			string modeName = _combatHUD.CurrentColorblindMode.ToString();
			string gamepadStatus = _gamepad.IsGamepadConnected ? "[color=green]🟢 已连接[/color]" : "[color=gray]未连接[/color]";

			DisplayServer.WindowSetTitle($"《断路协议》| 战术战斗 HUD | 帧率: {fps:F0} FPS | 色盲模式: {modeName}");

			_hudLabel.Text = $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"[b][color=yellow]【TASK-36 战斗 HUD 准星遥测环 + 全息战损纸娃娃演练场】[/color][/b]\n" +
							 $"• 色盲无障碍模式: [b][color=cyan]{modeName}[/color][/b] (按 [C] 键切换) | 手柄输入: {gamepadStatus}\n" +
							 $"------------------------------------------------------------------------------------\n" +
							 $"[调试指令说明]:\n" +
							 $"• [按 1 键]: [b][color=yellow]单次轻度受创 -30 HP (观察纸娃娃色块由绿 ──► 黄 ──► 红 ──► 闪烁红叉)[/color][/b]\n" +
							 $"• [按 2 键]: 一键满血修复 | [按 R 键]: 满血重置飞船\n" +
							 $"• [准星随动]: 左弧带宽负载 / 右弧发热量积分 (连续开火出现 OVERHEAT 报警)\n" +
							 $"• [屏幕边缘]: 离屏敌舰全向红色雷达箭头 (附带动态距离数值)";
		}
	}
}
