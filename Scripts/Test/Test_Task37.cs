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
using BreakerProtocol.UI.Settlement;
using BreakerProtocol.World.Director;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Events;
using BreakerProtocol.World.Meta;
using BreakerProtocol.World.Sector;
using BreakerProtocol.World.Settlement;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-37 交互式验证场景：战场牵引光束打捞 + 全流程宏观游戏循环演练场
	/// </summary>
	public partial class Test_Task37 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private BulletManager _bulletManager = null!;
		private AudioManager _audio = null!;
		private TractorBeamController _tractorBeam = null!;
		private GameDirector _director = null!;

		// UI 集合
		private SectorMapUI _mapUI = null!;
		private BlackMarketShopUI _marketUI = null!;
		private SpaceEventDialogueUI _eventUI = null!;
		private MetaTechTreeUI _metaTechUI = null!;
		private RunSummaryUI _summaryUI = null!;
		private CombatHUD _combatHUD = null!;
		private RichTextLabel _hudLabel = null!;

		public override void _Ready()
		{
			// 1. 初始化基础架构单例
			_audio = new AudioManager { Name = "AudioManager" };
			AddChild(_audio);

			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			// 2. 初始化经济与战舰
			PlayerEconomyManager.Instance.Reset(initialScraps: 180, initialCores: 1);

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

			// 4. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateAllUIs();
			CreateDirector();

			// 在战场周围散布 6 个物理战利品漂浮物
			SpawnInitialSalvageDrops();

			// 初始进入战斗模式
			_director.SwitchState(GameFlowState.CombatBattle);
		}

		private void SpawnInitialSalvageDrops()
		{
			for (int i = 0; i < 6; i++)
			{
				Vector2 offset = new((float)GD.RandRange(-260, 260), (float)GD.RandRange(-260, 260));
				var drop = new SalvageDropEntity
				{
					Position = _playerShip.Position + offset,
					Velocity = new Vector2((float)GD.RandRange(-30, 30), (float)GD.RandRange(-30, 30)),
					Type = (i % 3 == 0) ? SalvageType.ComputeCore : SalvageType.Scraps,
					Amount = (int)GD.RandRange(35, 75)
				};
				AddChild(drop);
			}
		}

		private void CreateAllUIs()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_combatHUD = new CombatHUD { TargetShip = _playerShip };
			canvasLayer.AddChild(_combatHUD);

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

			_summaryUI = new RunSummaryUI();
			_summaryUI.OnNavigateToMetaTech += () => _director.SwitchState(GameFlowState.Hangar);
			_summaryUI.OnStartNewRun += () => _director.ReturnToSectorMap();
			canvasLayer.AddChild(_summaryUI);

			_hudLabel = new RichTextLabel
			{
				Position = new Vector2(30, 15),
				Size = new Vector2(1220, 110),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_hudLabel.AddThemeFontSizeOverride("normal_font_size", 14);
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
				// [按 M 键]: 切换星图跃迁界面
				if (ek.Keycode == Key.M)
				{
					if (_director.CurrentState == GameFlowState.SectorNavigation)
						_director.SwitchState(GameFlowState.CombatBattle);
					else
						_director.SwitchState(GameFlowState.SectorNavigation);
				}
				// [按 B 键]: 进入黑市空间站
				else if (ek.Keycode == Key.B)
				{
					_director.EnterEncounterFromSector(SectorNodeType.Market);
				}
				// [按 E 键 (非爆甲)]: 触发深空随机异象
				else if (ek.Keycode == Key.E && !Godot.Input.IsKeyPressed(Key.Shift))
				{
					_director.EnterEncounterFromSector(SectorNodeType.Event);
				}
				// [按 T 键]: 生成更多物理废料残骸 (测试牵引光束)
				else if (ek.Keycode == Key.T)
				{
					SpawnInitialSalvageDrops();
				}
				// [按 V 键]: 模拟战役胜利通关
				else if (ek.Keycode == Key.V)
				{
					_director.TriggerGameOver(RunEndingType.Victory);
				}
				// [按 H 键]: 前往母港科研局
				else if (ek.Keycode == Key.H)
				{
					_director.SwitchState(GameFlowState.Hangar);
				}
			}
		}

		public override void _Process(double delta)
		{
			// 鼠标左键开火 (仅在战斗态生效)
			if (Godot.Input.IsMouseButtonPressed(MouseButton.Left) &&
				(_director.CurrentState == GameFlowState.CombatBattle || _director.CurrentState == GameFlowState.BossEncounter))
			{
				if (Engine.GetProcessFrames() % 8 == 0)
				{
					_audio.PlaySfx(SoundType.ShootKinetic, 0.1f);
					foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
					{
						_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
					}
				}
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			double fps = Engine.GetFramesPerSecond();
			string stateStr = _director.CurrentState switch
			{
				GameFlowState.CombatBattle     => "[color=lime]⚔️ 战术空战模式[/color]",
				GameFlowState.SectorNavigation => "[color=cyan]🌌 星区 DAG 跃迁星图[/color]",
				GameFlowState.BlackMarketShop  => "[color=gold]🛒 废土黑市改装终端[/color]",
				GameFlowState.AnomalyDialogue  => "[color=magenta]🛸 深空异象日志[/color]",
				GameFlowState.Hangar           => "[color=yellow]🔬 母港科研总局[/color]",
				GameFlowState.RunSettlement    => "[color=green]🏆 战役结算评分[/color]",
				_                              => "[color=white]未知[/color]"
			};

			DisplayServer.WindowSetTitle($"《断路协议》| 全流程总控中枢 | 帧率: {fps:F0} FPS | 废料: {PlayerEconomyManager.Instance.Scraps} ⚙️");

			_hudLabel.Text = $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"[b][color=yellow]【TASK-37 物理牵引光束与全流程游戏总控演练场】[/color][/b] 当前状态: {stateStr}\n" +
							 $"• 玩家资产: [color=gold]{PlayerEconomyManager.Instance.Scraps} ⚙ 废料[/color] | [color=cyan]{PlayerEconomyManager.Instance.ComputeCores} 💠 算力核心[/color]\n" +
							 $"[流程快捷调试指令]:\n" +
							 $"• [W/A/S/D]: 驾驶飞船靠近漂浮物 ──► [b][color=cyan]自动激活金色/青色物理牵引光束吸附入舱！[/color][/b]\n" +
							 $"• [按 T 键]: 爆出 6 个废料漂浮物 | [按 M 键]: 星图开闭 | [按 B 键]: 黑市 | [按 E 键]: 异象 | [按 V 键]: 胜利结算";
		}
	}
}
