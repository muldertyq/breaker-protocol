using Godot;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.UI.Events;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Events;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-32 交互式验证场景：深空随机异象事件机与多分支文本交互树演练场
	/// </summary>
	public partial class Test_Task32 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private BulletManager _bulletManager = null!;
		private SpaceEventDialogueUI _eventUI = null!;
		private RichTextLabel _hudLabel = null!;

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			// 1. 初始化经济系统 (初始 150 废料与 1 算力核心)
			PlayerEconomyManager.Instance.Reset(initialScraps: 150, initialCores: 1);

			// 2. 生成玩家战舰
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

			// 3. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateUI();

			// 默认触发第 2 个事件 (包含 60 废料消耗，方便测试)
			TriggerEvent("ev_biohazard_smuggler");
		}

		private void CreateUI()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_eventUI = new SpaceEventDialogueUI();
			canvasLayer.AddChild(_eventUI);

			_hudLabel = new RichTextLabel
			{
				Position = new Vector2(25, 10),
				Size = new Vector2(1230, 52),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_hudLabel.AddThemeFontSizeOverride("normal_font_size", 13);
			canvasLayer.AddChild(_hudLabel);
		}

		private void TriggerEvent(string eventId)
		{
			if (SpaceEventDatabase.TryGetEvent(eventId, out var ev))
			{
				_eventUI.OpenEvent(ev, _playerShip);
			}
		}

		public override void _Process(double delta)
		{
			// [按 1~5 键]: 切换 5 大经典异象
			if (Input.IsKeyPressed(Key.Key1)) TriggerEvent("ev_pioneer_derelict");
			else if (Input.IsKeyPressed(Key.Key2)) TriggerEvent("ev_biohazard_smuggler");
			else if (Input.IsKeyPressed(Key.Key3)) TriggerEvent("ev_mech_cultist");
			else if (Input.IsKeyPressed(Key.Key4)) TriggerEvent("ev_pirate_minefield");
			else if (Input.IsKeyPressed(Key.Key5)) TriggerEvent("ev_solar_flare");

			// [按 R 键]: 随机触发异象事件
			if (Input.IsKeyPressed(Key.R))
			{
				_eventUI.OpenEvent(SpaceEventDatabase.GetRandomEvent(), _playerShip);
			}

			// [按 - 键]: 废料清零 (测试置灰拦截与禁用光标)
			if (Input.IsKeyPressed(Key.Minus) || Input.IsKeyPressed(Key.KpSubtract))
			{
				PlayerEconomyManager.Instance.Reset(0, 0);
			}

			// [按 + 键]: 注入 100 废料
			if (Input.IsKeyPressed(Key.Equal) || Input.IsKeyPressed(Key.KpAdd))
			{
				PlayerEconomyManager.Instance.AddScraps(100);
			}

			// [按 0 键]: 战舰自损 -150 HP (测试维修效果)
			if (Input.IsKeyPressed(Key.Key0))
			{
				foreach (var m in _playerShip.Grid.Modules)
				{
					if (!m.IsDestroyed)
					{
						m.CurrentHp = Mathf.Max(20.0f, m.CurrentHp - 150.0f);
						_playerShip.OnModuleDamaged(m, 150.0f);
						break;
					}
				}
			}

			// [按 F 键]: 开启强制必失败模式 (100% Failure)
			if (Input.IsKeyPressed(Key.F))
			{
				_eventUI.DebugForceMode = 2;
			}

			// [按 S 键]: 开启强制必成功模式 (100% Success)
			if (Input.IsKeyPressed(Key.S))
			{
				_eventUI.DebugForceMode = 1;
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			double fps = Engine.GetFramesPerSecond();
			float curHp = 0, maxHp = 0;
			foreach (var m in _playerShip.Grid.Modules)
			{
				if (!m.IsDestroyed)
				{
					curHp += m.CurrentHp;
					maxHp += m.MaxHp;
				}
			}

			string modeStr = _eventUI.DebugForceMode switch
			{
				1 => "[color=green]【强制必成功】[/color]",
				2 => "[color=red]【强制必失败】[/color]",
				_ => "[color=yellow]【常规概率】[/color]"
			};

			DisplayServer.WindowSetTitle($"《断路协议》| 异象事件终端 | 帧率: {fps:F0} FPS | 废料: {PlayerEconomyManager.Instance.Scraps} ⚙️ | 耐久: {curHp:F0}/{maxHp:F0} HP");

			_hudLabel.Text = $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"[b][color=yellow]【TASK-32 异象演练场】[/color][/b] " +
							 $"废料: [color=gold]{PlayerEconomyManager.Instance.Scraps} ⚙[/color] | 核心: [color=cyan]{PlayerEconomyManager.Instance.ComputeCores} 💠[/color] | 耐久: [color=lightgreen]{curHp:F0}/{maxHp:F0} HP[/color] | 判定模式: {modeStr}\n" +
							 $"[调试指令]: [b][color=white][1~5键]: 切换事件 | [-键]: 清空废料至0(测置灰) | [+键]: +100废料 | [0键]: 战舰自损150HP | [F键]: 强制失败 | [S键]: 强制成功[/color][/b]";
		}
	}
}
