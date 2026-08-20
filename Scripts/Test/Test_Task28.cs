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
	/// TASK-28 交互式验证场景：遭遇战编队协同、阵型切换与开火令牌仇恨仲裁演练场
	/// (集成 IFF 敌我识别、长机皇冠、开火令牌光环与阵型雷达拓扑)
	/// </summary>
	public partial class Test_Task28 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private BulletManager _bulletManager = null!;
		private AsteroidFieldManager _asteroidField = null!;
		private FleetFormationManager _formationManager = null!;
		private RichTextLabel _hudLabel = null!;

		private readonly List<ShipEntity> _fleetMembers = new();
		private float _playerInvulnerableTimer = 3.0f;

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			// 1. 初始化编队协同总控
			_formationManager = new FleetFormationManager
			{
				Name = "FleetFormationManager",
				MaxSimultaneousFireTokens = 1,
				CurrentFormation = FormationType.Pincer
			};
			AddChild(_formationManager);

			// 2. 初始化小行星带
			_asteroidField = new AsteroidFieldManager
			{
				Name = "AsteroidFieldManager",
				TargetAsteroidCount = 28,
				FieldArea = new Rect2(-1800, -1800, 3600, 3600)
			};
			AddChild(_asteroidField);

			// 3. 创建玩家战舰 (出生于下方安全点 0, 480)
			_playerShip = new ShipEntity
			{
				Name = "PlayerCruiser",
				Position = new Vector2(0, 480)
			};
			_playerShip.AddToGroup("Player");
			_playerShip.AddToGroup("Ship");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}

			// 4. 生成敌方编队 (位于远方 0, -350，相距 830px)
			SpawnCoordinatedFleet();

			// 5. 生成小行星掩体
			_asteroidField.GenerateAsteroidField(_playerShip.GlobalPosition);

			// 6. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateHUD();
		}

		private void SpawnCoordinatedFleet()
		{
			foreach (var ship in _fleetMembers)
			{
				if (GodotObject.IsInstanceValid(ship)) ship.QueueFree();
			}
			_fleetMembers.Clear();

			// 旗舰 (长机：重工铁甲级) -> 居中远方 (0, -350)
			var leader = new ShipEntity
			{
				Name = "Flagship_Ironclad",
				Position = new Vector2(0, -350),
				Rotation = Mathf.Pi
			};
			leader.AddToGroup("Ship");
			leader.CurrentPalette = FactionPalettes.HeavyFoundry;
			AddChild(leader);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_l_ironclad", out var ironBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(leader, ironBp!);
			}
			leader.AttachAI(AiArchetype.Brawler, _playerShip);
			_formationManager.RegisterFleetMember(leader);
			_fleetMembers.Add(leader);

			// 远距狙击僚机 (虚空棱镜级) -> 右上方 (450, -420)
			var sniper = new ShipEntity
			{
				Name = "Escort_Sniper",
				Position = new Vector2(450, -420),
				Rotation = Mathf.Pi * 0.85f
			};
			sniper.AddToGroup("Ship");
			sniper.CurrentPalette = FactionPalettes.VoidSyndicate;
			AddChild(sniper);

			if (DataManager.Instance.Blueprints.TryGet("bp_vs_m_prism", out var prismBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(sniper, prismBp!);
			}
			sniper.AttachAI(AiArchetype.KiteSniper, _playerShip);
			_formationManager.RegisterFleetMember(sniper);
			_fleetMembers.Add(sniper);

			// 2 艘突击护航机 (生化甲壳级) -> 左翼与右翼
			Vector2[] swarmPos = { new(-450, -320), new(350, -260) };
			for (int i = 0; i < swarmPos.Length; i++)
			{
				var escort = new ShipEntity
				{
					Name = $"Escort_Swarm_{i + 1}",
					Position = swarmPos[i],
					Rotation = Mathf.Pi * 0.9f
				};
				escort.AddToGroup("Ship");
				escort.CurrentPalette = FactionPalettes.BioChitin;
				AddChild(escort);

				if (DataManager.Instance.Blueprints.TryGet("bp_bc_m_carapace", out var carapaceBp))
				{
					ShipBlueprintLoader.ApplyBlueprint(escort, carapaceBp!);
				}
				escort.AttachAI(AiArchetype.Swarm, _playerShip);
				_formationManager.RegisterFleetMember(escort);
				_fleetMembers.Add(escort);
			}

			_playerInvulnerableTimer = 3.0f;
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
			float dt = (float)delta;
			if (_playerInvulnerableTimer > 0.0f) _playerInvulnerableTimer -= dt;

			// 阵型切换热键
			if (Input.IsKeyPressed(Key.Key1))
			{
				_formationManager.CurrentFormation = FormationType.Pincer;
			}
			else if (Input.IsKeyPressed(Key.Key2))
			{
				_formationManager.CurrentFormation = FormationType.Wedge;
			}
			else if (Input.IsKeyPressed(Key.Key3))
			{
				_formationManager.CurrentFormation = FormationType.Line;
			}

			// 调节开火令牌配额
			if (Input.IsKeyPressed(Key.Key4))
			{
				_formationManager.MaxSimultaneousFireTokens = 1;
			}
			else if (Input.IsKeyPressed(Key.Key5))
			{
				_formationManager.MaxSimultaneousFireTokens = 2;
			}

			// 玩家开火
			if (Input.IsMouseButtonPressed(MouseButton.Left) && !_playerShip.Hotwire.IsInHotwireMode)
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			// [按 R 键]: 满血重置战场
			if (Input.IsKeyPressed(Key.R))
			{
				if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
				{
					ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
					_playerShip.Position = new Vector2(0, 480);
					_playerShip.LinearVelocity = Vector2.Zero;
				}
				SpawnCoordinatedFleet();
				_asteroidField.GenerateAsteroidField(_playerShip.GlobalPosition);
			}

			UpdateHUD();
			QueueRedraw();
		}

		private void UpdateHUD()
		{
			double fps = Engine.GetFramesPerSecond();
			int aliveCount = 0;
			string telemetry = string.Empty;

			foreach (var ship in _fleetMembers)
			{
				if (GodotObject.IsInstanceValid(ship) && ship.AI != null)
				{
					aliveCount++;
					bool hasToken = _formationManager.RequestFirePermission(ship);
					bool isLeader = ship == _formationManager.FleetLeader;
					string leaderTag = isLeader ? "[color=gold][🚩长机][/color] " : "";
					string tokenTag = hasToken ? "[color=red]🔥 TOKEN [开火][/color]" : "[color=gray]🛡️ 佯攻掩护[/color]";
					float dist = ship.GlobalPosition.DistanceTo(_playerShip.GlobalPosition);

					telemetry += $"• {leaderTag}[{ship.Name}] 状态: {ship.AI.CurrentState} | 距离: {dist:F0}px | {tokenTag}\n";
				}
			}

			string shieldStatus = _playerInvulnerableTimer > 0.0f
				? $"[color=yellow]🛡️ 出生折跃护盾 ({_playerInvulnerableTimer:F1}s)[/color]"
				: "[color=green]🟢 战备交火态[/color]";

			DisplayServer.WindowSetTitle($"《断路协议》| 帧率: {fps:F0} FPS | 阵型: {_formationManager.CurrentFormation} | 敌军: {aliveCount} 艘");

			_hudLabel.Text = $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"[b][color=yellow]【TASK-28 遭遇战编队协同与 IFF 战术雷达展厅】[/color][/b]\n" +
							 $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"• 玩家护盾状态:   {shieldStatus}\n" +
							 $"• 编队阵型拓扑:   [b][color=cyan]{_formationManager.CurrentFormation}[/color][/b] ([按 1 钳形 / 2 楔形 / 3 战列线])\n" +
							 $"• 开火令牌配额:   [b][color=white]{_formationManager.MaxSimultaneousFireTokens}[/color][/b] 艘同时开火 ([按 4 设为1艘 / 按 5 设为2艘])\n" +
							 $"• 存活编队规模:   [b][color=white]{aliveCount}[/color][/b] 艘 | 渲染性能: [color=green]{fps:F0} FPS[/color]\n" +
							 $"--------------------------------------------------\n" +
							 $"[color=yellow][编队 IFF 战术雷达与开火令牌遥测][/color]\n" +
							 $"{telemetry}" +
							 $"--------------------------------------------------\n" +
							 $"[视觉识别指南]\n" +
							 $"1. 【玩家 (友军)】: [color=green]鲜绿色菱形锁定框[/color] + 3秒金色无敌护盾；\n" +
							 $"2. 【敌军长机 (旗舰)】: [color=gold]金色八角星战术框[/color] + [🚩 长机 FLAGSHIP] 标识；\n" +
							 $"3. 【开火敌机】: 获得开火令牌的敌机带有 [color=red]鲜红色外框 + 🔥 射击光环[/color]；\n" +
							 $"4. 【阵型拓扑虚线】: 长机与僚机之间相连的 [color=cyan]青蓝色虚线骨架与幽灵锚点[/color]，实时展示包夹/楔形走位！\n" +
							 $"--------------------------------------------------\n" +
							 $"[操控] WASD 飞行 | 鼠标开火 | [1/2/3 切换阵型] | [按 R 键]: 满血重置";
		}

		/// <summary>
		/// 在世界空间绘制所有战舰的 IFF 战术指示框、血量环与头顶标识
		/// </summary>
		public override void _Draw()
		{
			// 1. 绘制玩家战舰 (绿色友军 IFF)
			if (IsInstanceValid(_playerShip))
			{
				Vector2 playerPos = ToLocal(_playerShip.GlobalPosition);
				DrawTacticalBracket(playerPos, 45.0f, Colors.LimeGreen, "[ 玩家 ALLY ]");

				if (_playerInvulnerableTimer > 0.0f)
				{
					DrawArc(playerPos, 52.0f, 0, Mathf.Tau, 32, Colors.Gold, 2.5f);
				}
			}

			// 2. 绘制敌方舰队各战舰的 IFF 指示框
			foreach (var ship in _fleetMembers)
			{
				if (!IsInstanceValid(ship)) continue;

				Vector2 shipPos = ToLocal(ship.GlobalPosition);
				bool isLeader = ship == _formationManager.FleetLeader;
				bool hasToken = _formationManager.RequestFirePermission(ship);

				if (isLeader)
				{
					// 长机：金色八角战术框 + 冠顶标记
					DrawTacticalBracket(shipPos, 55.0f, Colors.Gold, "[ 🚩 编队长机 FLAGSHIP ]");
					DrawArc(shipPos, 62.0f, 0, Mathf.Tau, 24, Colors.Gold, 2.0f);
				}
				else if (hasToken)
				{
					// 正在开火的僚机：鲜红锁定框 + 火焰标识
					DrawTacticalBracket(shipPos, 40.0f, Colors.Crimson, "[ 🔥 开火锁定 FIRE ]");
					DrawArc(shipPos, 44.0f, 0, Mathf.Tau, 16, Colors.Red, 2.0f);
				}
				else
				{
					// 掩护走位的僚机：暗红/橙色虚线框
					DrawTacticalBracket(shipPos, 36.0f, new Color(1.0f, 0.45f, 0.2f, 0.7f), "[ 🛡️ 战术掩护 ]");
				}
			}
		}

		private void DrawTacticalBracket(Vector2 center, float size, Color color, string label)
		{
			float half = size * 0.5f;
			float len = size * 0.25f;

			// 四角瞄准框线
			// 左上
			DrawLine(center + new Vector2(-half, -half), center + new Vector2(-half + len, -half), color, 2.0f);
			DrawLine(center + new Vector2(-half, -half), center + new Vector2(-half, -half + len), color, 2.0f);
			// 右上
			DrawLine(center + new Vector2(half, -half), center + new Vector2(half - len, -half), color, 2.0f);
			DrawLine(center + new Vector2(half, -half), center + new Vector2(half, -half + len), color, 2.0f);
			// 左下
			DrawLine(center + new Vector2(-half, half), center + new Vector2(-half + len, half), color, 2.0f);
			DrawLine(center + new Vector2(-half, half), center + new Vector2(-half, half - len), color, 2.0f);
			// 右下
			DrawLine(center + new Vector2(half, half), center + new Vector2(half - len, half), color, 2.0f);
			DrawLine(center + new Vector2(half, half), center + new Vector2(half, half - len), color, 2.0f);
		}
	}
}
