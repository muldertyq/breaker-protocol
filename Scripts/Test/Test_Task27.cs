using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.Ship.AI;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Environment.Asteroids;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-27 交互式验证场景：三大战术流派敌方行为树 AI (Brawler / Kite Sniper / Swarm) 对抗演练场
	/// </summary>
	public partial class Test_Task27 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private BulletManager _bulletManager = null!;
		private AsteroidFieldManager _asteroidField = null!;
		private RichTextLabel _hudLabel = null!;

		private readonly List<ShipEntity> _enemyShips = new();

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			_asteroidField = new AsteroidFieldManager
			{
				Name = "AsteroidFieldManager",
				TargetAsteroidCount = 24,
				FieldArea = new Rect2(-1500, -1500, 3000, 3000)
			};
			AddChild(_asteroidField);

			// 1. 创建玩家战舰 (中心 0, 300)
			_playerShip = new ShipEntity
			{
				Name = "PlayerShip",
				Position = new Vector2(0, 300)
			};
			_playerShip.AddToGroup("Player");
			_playerShip.AddToGroup("Ship");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}

			// 2. 生成三大流派战术敌机编队
			SpawnTacticalFleet();

			// 3. 生成小行星掩体
			_asteroidField.GenerateAsteroidField(_playerShip.GlobalPosition);

			// 4. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateHUD();
		}

		private void SpawnTacticalFleet()
		{
			foreach (var enemy in _enemyShips)
			{
				if (GodotObject.IsInstanceValid(enemy)) enemy.QueueFree();
			}
			_enemyShips.Clear();

			// ============================================================
			// 1. 肉搏角斗士 (Brawler - 重工联合铁甲级) -> 位于正前方 (-200, -350)
			// ============================================================
			var brawler = new ShipEntity
			{
				Name = "Brawler_Ironclad",
				Position = new Vector2(-200, -350),
				Rotation = Mathf.Pi
			};
			brawler.AddToGroup("Ship");
			brawler.CurrentPalette = FactionPalettes.HeavyFoundry;
			AddChild(brawler);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_l_ironclad", out var ironBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(brawler, ironBp!);
			}
			brawler.AttachAI(AiArchetype.Brawler, _playerShip);
			_enemyShips.Add(brawler);

			// ============================================================
			// 2. 风筝狙击手 (Kite Sniper - 虚空财团棱镜级) -> 位于远距离右侧 (500, -250)
			// ============================================================
			var sniper = new ShipEntity
			{
				Name = "Sniper_Prism",
				Position = new Vector2(500, -250),
				Rotation = Mathf.Pi * 0.75f
			};
			sniper.AddToGroup("Ship");
			sniper.CurrentPalette = FactionPalettes.VoidSyndicate;
			AddChild(sniper);

			if (DataManager.Instance.Blueprints.TryGet("bp_vs_m_prism", out var prismBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(sniper, prismBp!);
			}
			sniper.AttachAI(AiArchetype.KiteSniper, _playerShip);
			_enemyShips.Add(sniper);

			// ============================================================
			// 3. 蜂群突袭者 (Swarm - 深空生化甲壳级 x2) -> 位于左侧与后方
			// ============================================================
			Vector2[] swarmOffsets = { new(-450, 0), new(350, 450) };
			for (int i = 0; i < swarmOffsets.Length; i++)
			{
				var swarm = new ShipEntity
				{
					Name = $"Swarm_Carapace_{i + 1}",
					Position = swarmOffsets[i],
					Rotation = (swarmOffsets[i] - _playerShip.Position).Angle()
				};
				swarm.AddToGroup("Ship");
				swarm.CurrentPalette = FactionPalettes.BioChitin;
				AddChild(swarm);

				if (DataManager.Instance.Blueprints.TryGet("bp_bc_m_carapace", out var carapaceBp))
				{
					ShipBlueprintLoader.ApplyBlueprint(swarm, carapaceBp!);
				}
				swarm.AttachAI(AiArchetype.Swarm, _playerShip);
				_enemyShips.Add(swarm);
			}
		}

		private void CreateHUD()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_hudLabel = new RichTextLabel
			{
				Position = new Vector2(25, 25),
				Size = new Vector2(680, 680),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_hudLabel.AddThemeFontSizeOverride("normal_font_size", 15);
			canvasLayer.AddChild(_hudLabel);
		}

		public override void _Process(double delta)
		{
			// 玩家开火
			if (Input.IsMouseButtonPressed(MouseButton.Left) && !_playerShip.Hotwire.IsInHotwireMode)
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			// [按 R 键]: 满血重置战场与三大战术舰队
			if (Input.IsKeyPressed(Key.R))
			{
				if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
				{
					ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
					_playerShip.Position = new Vector2(0, 300);
					_playerShip.LinearVelocity = Vector2.Zero;
				}
				SpawnTacticalFleet();
				_asteroidField.GenerateAsteroidField(_playerShip.GlobalPosition);
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			double fps = Engine.GetFramesPerSecond();
			int aliveEnemies = 0;
			string aiTelemetry = string.Empty;

			foreach (var enemy in _enemyShips)
			{
				if (GodotObject.IsInstanceValid(enemy) && enemy.AI != null)
				{
					aliveEnemies++;
					float dist = enemy.GlobalPosition.DistanceTo(_playerShip.GlobalPosition);
					string stateColor = enemy.AI.CurrentState switch
					{
						AiState.Engage => "orange",
						AiState.KiteRetreat => "cyan",
						AiState.Flank => "yellow",
						AiState.Kamikaze => "red",
						_ => "white"
					};

					aiTelemetry += $"• [{enemy.AI.Archetype}] 状态: [color={stateColor}]{enemy.AI.CurrentState}[/color] | 距离: {dist:F0}px | 开火: {(enemy.AI.WantsToFire ? "[color=red]YES[/color]" : "NO")}\n";
				}
			}

			DisplayServer.WindowSetTitle($"《断路协议》| 帧率: {fps:F0} FPS | 存活敌机: {aliveEnemies} 艘");

			_hudLabel.Text = $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"[b][color=yellow]【TASK-27 敌方行为树 AI 三大战术流派演练场】[/color][/b]\n" +
							 $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"• 存活战术敌机:   [b][color=white]{aliveEnemies}[/color][/b] 艘\n" +
							 $"• 实时渲染性能:   [color=green]{fps:F0} FPS[/color]\n" +
							 $"--------------------------------------------------\n" +
							 $"[color=yellow][敌机战术行为树遥测监控][/color]\n" +
							 $"{aiTelemetry}" +
							 $"--------------------------------------------------\n" +
							 $"[三大 AI 流派实战对抗指南]\n" +
							 $"1. 【重装肉搏 (Brawler)】: 正面厚甲顶线推进，在 200px 距离持续近身倾泻火力；\n" +
							 $"2. 【风筝狙击 (Kite Sniper)】: 只要玩家靠近即倒车反推拉扯，在 550px 距离前置预测瞄准狙击；\n" +
							 $"3. 【蜂群突袭 (Swarm)】: 蛇形机动走位、极速绕背攻击机尾，残血 (<25%) 进入【死士自爆撞击】！\n" +
							 $"--------------------------------------------------\n" +
							 $"[操控] WASD 飞行 | 鼠标瞄准/开火 | [按 R 键]: 满血重置演练场";
		}
	}
}
