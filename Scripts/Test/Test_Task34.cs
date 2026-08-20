using Godot;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.UI.Meta;
using BreakerProtocol.UI.Settlement;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Meta;
using BreakerProtocol.World.Settlement;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-34 交互式验证场景：战役结算、战利品评分与残局继承演练场
	/// </summary>
	public partial class Test_Task34 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private BulletManager _bulletManager = null!;
		private RunSummaryUI _summaryUI = null!;
		private MetaTechTreeUI _techTreeUI = null!;
		private RichTextLabel _hudLabel = null!;

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			// 1. 初始化经济系统
			PlayerEconomyManager.Instance.Reset(initialScraps: 200, initialCores: 1);

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

			MetaProgressionManager.Instance.ApplyMetaBuffsToNewRun(_playerShip);

			// 3. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateUI();

			// 默认触发一次 S 级辉煌胜利战报
			TriggerSimulatedSettlement(EvaluationRank.S);
		}

		private void CreateUI()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_summaryUI = new RunSummaryUI();
			_summaryUI.OnNavigateToMetaTech += () => _techTreeUI.Visible = true;
			_summaryUI.OnStartNewRun += StartNewRun;
			canvasLayer.AddChild(_summaryUI);

			_techTreeUI = new MetaTechTreeUI();
			_techTreeUI.Visible = false;
			canvasLayer.AddChild(_techTreeUI);

			_hudLabel = new RichTextLabel
			{
				Position = new Vector2(25, 10),
				Size = new Vector2(1230, 45),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_hudLabel.AddThemeFontSizeOverride("normal_font_size", 13);
			canvasLayer.AddChild(_hudLabel);
		}

		private void TriggerSimulatedSettlement(EvaluationRank rank)
		{
			_techTreeUI.Visible = false;

			var stats = rank switch
			{
				EvaluationRank.S => new RunStatistics
				{
					Ending = RunEndingType.Victory,
					SectorsCleared = 8,
					StandardEnemiesKilled = 18,
					ElitesKilled = 3,
					BossesKilled = 1,
					TotalScrapsEarned = 780,
					ComputeCoresEarned = 4,
					DamageTakenTotal = 240,
					FinalHullIntegrityPercent = 90,
					DurationSeconds = 540
				},
				EvaluationRank.A => new RunStatistics
				{
					Ending = RunEndingType.Victory,
					SectorsCleared = 8,
					StandardEnemiesKilled = 12,
					ElitesKilled = 2,
					BossesKilled = 1,
					TotalScrapsEarned = 520,
					ComputeCoresEarned = 2,
					DamageTakenTotal = 460,
					FinalHullIntegrityPercent = 65,
					DurationSeconds = 480
				},
				EvaluationRank.B => new RunStatistics
				{
					Ending = RunEndingType.Defeat_Destroyed,
					SectorsCleared = 5,
					StandardEnemiesKilled = 9,
					ElitesKilled = 1,
					BossesKilled = 0,
					TotalScrapsEarned = 310,
					ComputeCoresEarned = 1,
					DamageTakenTotal = 680,
					FinalHullIntegrityPercent = 0,
					DurationSeconds = 320
				},
				_ => new RunStatistics
				{
					Ending = RunEndingType.Defeat_Overrun,
					SectorsCleared = 2,
					StandardEnemiesKilled = 3,
					ElitesKilled = 0,
					BossesKilled = 0,
					TotalScrapsEarned = 90,
					ComputeCoresEarned = 0,
					DamageTakenTotal = 550,
					FinalHullIntegrityPercent = 0,
					DurationSeconds = 140
				}
			};

			_summaryUI.OpenSummary(stats);
		}

		private void StartNewRun()
		{
			PlayerEconomyManager.Instance.Reset(200, 1);
			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}
			MetaProgressionManager.Instance.ApplyMetaBuffsToNewRun(_playerShip);
			_summaryUI.Visible = false;
			_techTreeUI.Visible = false;
		}

		public override void _Process(double delta)
		{
			// [按 1 键]: 模拟 S 级辉煌胜利战报
			if (Input.IsKeyPressed(Key.Key1)) TriggerSimulatedSettlement(EvaluationRank.S);
			// [按 2 键]: 模拟 A 级险胜战报
			else if (Input.IsKeyPressed(Key.Key2)) TriggerSimulatedSettlement(EvaluationRank.A);
			// [按 3 键]: 模拟 B 级中途战损战报
			else if (Input.IsKeyPressed(Key.Key3)) TriggerSimulatedSettlement(EvaluationRank.B);
			// [按 4 键]: 模拟 D 级折戟沦陷战报
			else if (Input.IsKeyPressed(Key.Key4)) TriggerSimulatedSettlement(EvaluationRank.D);

			// [按 TAB 键]: 切换科研局面板
			if (Input.IsActionJustPressed("ui_focus_next") || Input.IsKeyPressed(Key.Tab))
			{
				_techTreeUI.Visible = !_techTreeUI.Visible;
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			double fps = Engine.GetFramesPerSecond();
			DisplayServer.WindowSetTitle($"《断路协议》| 战役结算演练场 | 帧率: {fps:F0} FPS | 母港研发碎片: {MetaProgressionManager.Instance.DataFragments} 💾");

			_hudLabel.Text = $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"[b][color=yellow]【TASK-34 战役结算与战利品评分演练场】[/color][/b] " +
							 $"母港总研发碎片: [color=cyan]{MetaProgressionManager.Instance.DataFragments} 💾[/color] | [color=white][按 1~4 键]: 触发 S/A/B/D 级结算战报 | [按 TAB 键]: 开闭母港科研局[/color]";
		}
	}
}
