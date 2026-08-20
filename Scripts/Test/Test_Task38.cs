using System.Collections.Generic;
using Godot;
using BreakerProtocol.Audio;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Abilities;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.PlayerInput;
using BreakerProtocol.Ship;
using BreakerProtocol.UI.CombatHUD;
using BreakerProtocol.UI.Events;
using BreakerProtocol.UI.Market;
using BreakerProtocol.UI.Meta;
using BreakerProtocol.UI.Pacts;
using BreakerProtocol.UI.Sandbox;
using BreakerProtocol.UI.Settlement;
using BreakerProtocol.UI.SectorMap;
using BreakerProtocol.World.Director;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Events;
using BreakerProtocol.World.Meta;
using BreakerProtocol.World.Pacts;
using BreakerProtocol.World.Sandbox;
using BreakerProtocol.World.Sector;
using BreakerProtocol.World.Settlement;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-38 最终总装验证场景：母港风洞测试靶场 + 灾厄契约热度 + 全流程总装联调演练场
	/// </summary>
	public partial class Test_Task38 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private BulletManager _bulletManager = null!;
		private AudioManager _audio = null!;
		private TractorBeamController _tractorBeam = null!;
		private SandboxBayManager _sandboxMgr = null!;
		private GameDirector _director = null!;

		// UI 集合
		private SectorMapUI _mapUI = null!;
		private BlackMarketShopUI _marketUI = null!;
		private SpaceEventDialogueUI _eventUI = null!;
		private MetaTechTreeUI _metaTechUI = null!;
		private CalamityPactsUI _pactsUI = null!;
		private SandboxBayUI _sandboxUI = null!;
		private RunSummaryUI _summaryUI = null!;
		private CombatHUD _combatHUD = null!;
		private RichTextLabel _hudLabel = null!;

		public override void _Ready()
		{
			// 1. 初始化底层单例架构
			_audio = new AudioManager { Name = "AudioManager" };
			AddChild(_audio);

			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			// 2. 初始化经济与玩家旗舰
			PlayerEconomyManager.Instance.Reset(initialScraps: 300, initialCores: 2);

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

			// 3. 挂载物理牵引光束
			_tractorBeam = new TractorBeamController
			{
				TargetShip = _playerShip
			};
			AddChild(_tractorBeam);

			// 4. 挂载风洞靶场管理器
			_sandboxMgr = new SandboxBayManager
			{
				PlayerShip = _playerShip
			};
			AddChild(_sandboxMgr);

			// 5. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateAllUIs();
			CreateDirector();

			// 默认在前方生成 2 艘测试装甲靶舰
			_sandboxMgr.SpawnTarget(TargetShipType.StaticDummy, new Vector2(350, -80));
			_sandboxMgr.SpawnTarget(TargetShipType.HeavyCruiser, new Vector2(480, 120));

			// 默认进入战术打靶模式
			_director.SwitchState(GameFlowState.CombatBattle);
		}

		private void CreateAllUIs()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_combatHUD = new CombatHUD { TargetShip = _playerShip };
			canvasLayer.AddChild(_combatHUD);

			_sandboxUI = new SandboxBayUI();
			canvasLayer.AddChild(_sandboxUI);

			_mapUI = new SectorMapUI();
			var sectorGraph = SectorMapGenerator.GenerateSector(8);
			_mapUI.SetGraph(sectorGraph);
			_mapUI.OnNodeSelected += (node) => _director.EnterEncounterFromSector(node.Type);
			canvasLayer.AddChild(_mapUI);

			_marketUI = new BlackMarketShopUI();
			canvasLayer.AddChild(_marketUI);

			_eventUI = new SpaceEventDialogueUI();
			_eventUI.OnEventResolved += (outcome) => _director.ReturnToSectorMap();
			canvasLayer.AddChild(_eventUI);

			_metaTechUI = new MetaTechTreeUI();
			canvasLayer.AddChild(_metaTechUI);

			_pactsUI = new CalamityPactsUI();
			_pactsUI.OnStartWithPacts += () => _director.SwitchState(GameFlowState.SectorNavigation);
			canvasLayer.AddChild(_pactsUI);

			_summaryUI = new RunSummaryUI();
			_summaryUI.OnNavigateToMetaTech += () => _director.SwitchState(GameFlowState.Hangar);
			_summaryUI.OnStartNewRun += () => _director.ReturnToSectorMap();
			canvasLayer.AddChild(_summaryUI);

			_hudLabel = new RichTextLabel
			{
				Position = new Vector2(30, 12),
				Size = new Vector2(1220, 115),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_hudLabel.AddThemeFontSizeOverride("normal_font_size", 13);
			canvasLayer.AddChild(_hudLabel);
		}

		private void CreateDirector()
		{
			_director = new GameDirector
			{
				PlayerShip = _playerShip,
				MapUI = _mapUI,
				MarketUI = _marketUI,
				EventUI = _eventUI,
				MetaTechUI = _metaTechUI,
				SummaryUI = _summaryUI,
				CombatHUD = _combatHUD
			};
			AddChild(_director);
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (@event is InputEventKey ek && ek.Pressed && !ek.Echo)
			{
				// [按 1 键]: 生成静止轻甲靶舰
				if (ek.Keycode == Key.Key1)
				{
					_sandboxMgr.SpawnTarget(TargetShipType.StaticDummy, _playerShip.Position + new Vector2(320, (float)GD.RandRange(-150, 150)));
				}
				// [按 2 键]: 生成机动风筝靶舰
				else if (ek.Keycode == Key.Key2)
				{
					_sandboxMgr.SpawnTarget(TargetShipType.MobileKiter, _playerShip.Position + new Vector2(400, (float)GD.RandRange(-150, 150)));
				}
				// [按 3 键]: 生成重装巡洋舰靶舰
				else if (ek.Keycode == Key.Key3)
				{
					_sandboxMgr.SpawnTarget(TargetShipType.HeavyCruiser, _playerShip.Position + new Vector2(500, (float)GD.RandRange(-150, 150)));
				}
				// [按 K 键]: 一键清空所有靶舰
				else if (ek.Keycode == Key.K)
				{
					_sandboxMgr.ClearAllTargets();
				}
				// [按 U 键]: 重置 DPS 统计
				else if (ek.Keycode == Key.U)
				{
					_sandboxMgr.ResetStats();
				}
				// [按 P 键]: 切换无限能量开关
				else if (ek.Keycode == Key.P)
				{
					_sandboxMgr.InfinitePower = !_sandboxMgr.InfinitePower;
				}
				// [按 O 键]: 切换零发热开关
				else if (ek.Keycode == Key.O)
				{
					_sandboxMgr.ZeroThermal = !_sandboxMgr.ZeroThermal;
				}
				// [按 J 键]: 呼出灾厄契约选择界面
				else if (ek.Keycode == Key.J)
				{
					_pactsUI.Visible = !_pactsUI.Visible;
				}
				// [按 M 键]: 切换星图
				else if (ek.Keycode == Key.M)
				{
					if (_director.CurrentState == GameFlowState.SectorNavigation)
						_director.SwitchState(GameFlowState.CombatBattle);
					else
						_director.SwitchState(GameFlowState.SectorNavigation);
				}
				// [按 B 键]: 黑市空间站
				else if (ek.Keycode == Key.B)
				{
					_director.EnterEncounterFromSector(SectorNodeType.Market);
				}
				// [按 H 键]: 母港科研科技树
				else if (ek.Keycode == Key.H)
				{
					_director.SwitchState(GameFlowState.Hangar);
				}
				// [按 V 键]: 模拟战役胜利
				else if (ek.Keycode == Key.V)
				{
					_director.TriggerGameOver(RunEndingType.Victory);
				}
			}
		}

		public override void _Process(double delta)
		{
			// 鼠标左键持续开火打靶
			if (Godot.Input.IsMouseButtonPressed(MouseButton.Left) &&
				(_director.CurrentState == GameFlowState.CombatBattle || _director.CurrentState == GameFlowState.BossEncounter))
			{
				if (Engine.GetProcessFrames() % 6 == 0)
				{
					_audio.PlaySfx(SoundType.ShootKinetic, 0.1f);
					foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
					{
						_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
					}

					// 模拟实弹击中靶舰记录 DPS
					if (_sandboxMgr.ActiveTargets.Count > 0)
					{
						float simDmg = (float)GD.RandRange(35.0, 65.0);
						bool isRicochet = GD.Randf() < 0.18f;
						_sandboxMgr.RecordDamage(isRicochet ? 0.0f : simDmg, isRicochet);

						var target = _sandboxMgr.ActiveTargets[0];
						if (isRicochet)
						{
							_vfx.SpawnFloatingText(target.GlobalPosition, "⚡ 跳弹偏折！", Colors.Cyan);
						}
						else
						{
							_vfx.SpawnModuleExplosion(target.GlobalPosition, new Vector2(25, 25), Colors.Orange, 6);
						}
					}
				}
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			double fps = Engine.GetFramesPerSecond();
			int heat = CalamityPactManager.Instance.GetTotalHeatLevel();

			string stateStr = _director.CurrentState switch
			{
				GameFlowState.CombatBattle     => "[color=lime]🎯 风洞打靶/战斗模式[/color]",
				GameFlowState.SectorNavigation => "[color=cyan]🌌 星区 DAG 跃迁星图[/color]",
				GameFlowState.BlackMarketShop  => "[color=gold]🛒 废土黑市改装终端[/color]",
				GameFlowState.AnomalyDialogue  => "[color=magenta]🛸 深空异象日志[/color]",
				GameFlowState.Hangar           => "[color=yellow]🔬 母港科研总局[/color]",
				GameFlowState.RunSettlement    => "[color=green]🏆 战役结算评分[/color]",
				_                              => "[color=white]未知[/color]"
			};

			DisplayServer.WindowSetTitle($"《断路协议》| 终章总装演练场 | 帧率: {fps:F0} FPS | 热度: 🔥 {heat} 级 | 实时DPS: {_sandboxMgr.CurrentDPS:F0} HP/s");

			_hudLabel.Text = $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"[b][color=yellow]【TASK-38 母港风洞测试靶场 + 灾厄契约 + Demo 最终总装演练场】[/color][/b] 当前状态: {stateStr} | 灾厄热度: [color=orange]🔥 {heat} 级[/color]\n" +
							 $"• 玩家旗舰: [color=cyan]重工巡洋舰[/color] | 实时 DPS: [color=lightgreen]{_sandboxMgr.CurrentDPS:F0} HP/s[/color] | 峰值: [color=gold]{_sandboxMgr.PeakDPS:F0} HP/s[/color] | 跳弹率: [color=cyan]{_sandboxMgr.GetRicochetRate():F1}%[/color]\n" +
							 $"[总装核心快捷指令]:\n" +
							 $"• [鼠标左键]: [b][color=white]实弹开火打靶 (右侧仪表实时解算 DPS/峰值/跳弹率)[/color][/b] | [按 1/2/3 键]: 生成轻/机动/重装靶舰\n" +
							 $"• [按 P/O 键]: 无限能量 / 零发热开关 | [按 J 键]: [b][color=orange]签署 6 大灾厄契约热度[/color][/b] | [按 M/B/H/V 键]: 星图 / 黑市 / 科研 / 胜利结算";
		}
	}
}
