using System.Collections.Generic;
using Godot;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.World.Director;
using BreakerProtocol.World.Economy;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-40 演练场：战利品加权掉落池与战役遭遇生成验证中枢
	/// </summary>
	public partial class Test_Task40 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private BulletManager _bulletManager = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private RichTextLabel _hudLabel = null!;

		private readonly List<ShipEntity> _activeEnemies = new();
		private string _lastLootLog = "尚未执行掉落抽取。";

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			PlayerEconomyManager.Instance.Reset(initialScraps: 100, initialCores: 0);

			// 1. 创建玩家战舰
			_playerShip = new ShipEntity
			{
				Name = "PlayerShip_T40",
				Position = Vector2.Zero
			};
			_playerShip.AddToGroup("Player");
			_playerShip.AddToGroup("Ship");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var bp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, bp!);
			}

			// 2. 摄像机
			_camera = new CombatCameraController { TargetShip = _playerShip };
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateUI();
		}

		private void CreateUI()
		{
			var canvas = new CanvasLayer();
			AddChild(canvas);

			_hudLabel = new RichTextLabel
			{
				Position = new Vector2(30, 20),
				Size = new Vector2(1220, 320),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_hudLabel.AddThemeFontSizeOverride("normal_font_size", 14);
			canvas.AddChild(_hudLabel);

			UpdateHUD();
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (@event is InputEventKey ek && ek.Pressed && !ek.Echo)
			{
				// [按 1 键]: 抽取轻型机掉落池 (drop_enemy_light)
				if (ek.Keycode == Key.Key1)
				{
					TestDrop("drop_enemy_light");
				}
				// [按 2 键]: 抽取精英旗舰掉落池 (drop_enemy_elite)
				else if (ek.Keycode == Key.Key2)
				{
					TestDrop("drop_enemy_elite");
				}
				// [按 3 键]: 抽取 Boss 掉落池 (drop_boss_titan)
				else if (ek.Keycode == Key.Key3)
				{
					TestDrop("drop_boss_titan");
				}
				// [按 4 键]: 刷出一波常规遭遇战舰队 (enc_combat_strike_group)
				else if (ek.Keycode == Key.Key4)
				{
					SpawnEncounterTest("enc_combat_strike_group");
				}
				// [按 5 键]: 刷出虚空双子精英遭遇战 (enc_elite_syndicate_duo)
				else if (ek.Keycode == Key.Key5)
				{
					SpawnEncounterTest("enc_elite_syndicate_duo");
				}
				// [按 C 键]: 清除所有敌舰
				else if (ek.Keycode == Key.C)
				{
					ClearEnemies();
				}
				// [按 R 键]: 热重载 JSON
				else if (ek.Keycode == Key.R)
				{
					DataManager.Instance.LoadAllData();
					UpdateHUD();
				}
			}
		}

		private void TestDrop(string tableId)
		{
			var loot = LootDropService.RollLoot(tableId, luckMultiplier: 1.0f);
			string modulesStr = loot.DroppedModuleIds.Count > 0 ? string.Join(", ", loot.DroppedModuleIds) : "无构件掉落";
			_lastLootLog = $"[color=cyan]【{tableId}】[/color] 抽取结果: [color=yellow]{loot.Scraps} ⚙ 废料[/color] | [color=cyan]{loot.ComputeCores} 💠 核心[/color] | 构件: [color=lime]{modulesStr}[/color]";
			
			// 在玩家前方爆出实体
			LootDropService.SpawnLootAt(this, _playerShip.Position + new Vector2(0, -100), tableId);
			UpdateHUD();
		}

		private void SpawnEncounterTest(string encId)
		{
			ClearEnemies();
			if (DataManager.Instance.Encounters.TryGet(encId, out var enc) && enc != null)
			{
				var ships = EncounterDirector.SpawnEncounter(this, enc, _playerShip.Position, _playerShip);
				_activeEnemies.AddRange(ships);
			}
			UpdateHUD();
		}

		private void ClearEnemies()
		{
			foreach (var ship in _activeEnemies)
			{
				if (IsInstanceValid(ship)) ship.QueueFree();
			}
			_activeEnemies.Clear();
		}

		public override void _Process(double delta)
		{
			// 鼠标开火
			if (Godot.Input.IsMouseButtonPressed(MouseButton.Left))
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}
		}

		private void UpdateHUD()
		{
			var dm = DataManager.Instance;
			_hudLabel.Text =
				$"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
				$"[b][color=yellow]【TASK-40 战利品加权掉落池与战役遭遇生成表演练场】[/color][/b]\n" +
				$"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
				$"• [color=white]已注册掉落池:[/color] [color=gold]{dm.DropTables.Count}[/color] 个   " +
				$"• [color=white]已注册遭遇池:[/color] [color=gold]{dm.Encounters.Count}[/color] 个   " +
				$"• [color=white]当前在场敌舰:[/color] [color=crimson]{_activeEnemies.Count}[/color] 艘\n" +
				$"• [color=white]最近一次抽卡日志:[/color] {_lastLootLog}\n" +
				$"------------------------------------------------------------------------------------\n" +
				$"[color=yellow][数据驱动加权抽取与遭遇测试指南][/color]:\n" +
				$"1. [按 1 / 2 / 3 键]: 分别测试【轻型战机 / 精英巡洋舰 / 泰坦 Boss】加权掉落抽取并在空间生成掉落物；\n" +
				$"2. [按 4 / 5 键]: 测试根据 JSON 编队配置生成【重工突击队 (1巡洋+2轻艇)】或【虚空双子精英】；\n" +
				$"3. [按 C 键]: 清除场上所有生成的敌舰；\n" +
				$"4. [按 R 键]: 热重载全域 JSON 掉落与遭遇规则！";
		}
	}
}
