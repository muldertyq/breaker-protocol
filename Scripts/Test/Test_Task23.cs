using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-23 交互式验证场景：三大势力战术主动爆甲 (Q/E/Z Ablative Detonation) 试验场
	/// </summary>
	public partial class Test_Task23 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private Label _hudLabel = null!;

		private string _currentFactionBlueprint = "bp_hf_m_anvil";
		private string _factionNameDisplay = "重工联合 (Heavy Foundry)";

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			// 1. 创建玩家战舰 (中心 600, 500)
			_playerShip = new ShipEntity
			{
				Name = "AblativePlayerShip",
				Position = new Vector2(600, 500)
			};
			_playerShip.AddToGroup("Player");
			_playerShip.AddToGroup("Ship");
			AddChild(_playerShip);

			LoadBlueprint(_currentFactionBlueprint, FactionPalettes.HeavyFoundry, "重工联合 (Heavy Foundry)");

			// 2. 生成若干环绕的敌方靶舰 (用于测试破片清屏与黑洞吸附)
			SpawnSurroundingDummies();

			// 3. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateHUD();
		}

		private void LoadBlueprint(string bpId, ShipPalette palette, string displayName)
		{
			_currentFactionBlueprint = bpId;
			_factionNameDisplay = displayName;
			_playerShip.CurrentPalette = palette;

			if (DataManager.Instance.Blueprints.TryGet(bpId, out var bp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, bp!);
			}

			_playerShip.LinearVelocity = Vector2.Zero;
			_playerShip.AngularVelocity = 0.0f;
		}

		private void SpawnSurroundingDummies()
		{
			// 清理旧靶标
			var oldTargets = GetTree().GetNodesInGroup("DummyTarget");
			foreach (var t in oldTargets) t.QueueFree();

			// 生成 4 艘环绕的重装靶舰
			Vector2[] offsets = {
				new(-220, -100),
				new(220, -100),
				new(-180, 150),
				new(180, 150)
			};

			foreach (var off in offsets)
			{
				var target = new ShipEntity
				{
					Position = _playerShip.Position + off,
					Rotation = off.Angle() + Mathf.Pi * 0.5f
				};
				target.AddToGroup("DummyTarget");
				target.AddToGroup("Ship");
				AddChild(target);

				if (DataManager.Instance.Blueprints.TryGet("bp_vs_s_ghost", out var bp))
				{
					ShipBlueprintLoader.ApplyBlueprint(target, bp!);
				}
			}
		}

		private void CreateHUD()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_hudLabel = new Label
			{
				Position = new Vector2(25, 25),
				Size = new Vector2(620, 650)
			};
			_hudLabel.AddThemeFontSizeOverride("font_size", 15);
			_hudLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			canvasLayer.AddChild(_hudLabel);
		}

		public override void _Process(double delta)
		{
			// 切换势力战舰
			if (Input.IsKeyPressed(Key.Key1))
			{
				LoadBlueprint("bp_hf_m_anvil", FactionPalettes.HeavyFoundry, "重工联合 (Heavy Foundry)");
			}
			else if (Input.IsKeyPressed(Key.Key2))
			{
				LoadBlueprint("bp_vs_m_prism", FactionPalettes.VoidSyndicate, "虚空财团 (Void Syndicate)");
			}
			else if (Input.IsKeyPressed(Key.Key3))
			{
				LoadBlueprint("bp_bc_m_carapace", FactionPalettes.BioChitin, "深空生化 (Bio Chitin)");
			}

			// 开火
			if (Input.IsMouseButtonPressed(MouseButton.Left) && !_playerShip.Hotwire.IsInHotwireMode)
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			// 重置场景
			if (Input.IsKeyPressed(Key.R))
			{
				LoadBlueprint(_currentFactionBlueprint, _playerShip.CurrentPalette, _factionNameDisplay);
				SpawnSurroundingDummies();
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			int moduleCount = _playerShip.Grid.ModuleCount;
			float totalMass = _playerShip.PhysicsData.TotalMass;

			_hudLabel.Text = $"【《断路协议》TASK-23 三大势力战术主动爆甲展厅】\n" +
							 $"==================================================\n" +
							 $"当前战舰势力:   [{_factionNameDisplay}]\n" +
							 $"全舰存活构件:   {moduleCount} 个 | 实时总质量: {totalMass:F0} 吨\n" +
							 $"--------------------------------------------------\n" +
							 $"[三大爆甲战术按键]\n" +
							 $"■ [Q 键] -> 引爆【左舷机翼】爆炸螺栓 (战舰受反冲瞬间右闪！)\n" +
							 $"■ [E 键] -> 引爆【右舷机翼】爆炸螺栓 (战舰受反冲瞬间左闪！)\n" +
							 $"■ [Z 键] -> 引爆【舰尾舱段】爆炸螺栓 (战舰受反冲向前极速猛冲！)\n" +
							 $"--------------------------------------------------\n" +
							 $"[势力特技切换验证]\n" +
							 $"[按 1 键] 重工联合 -> 爆甲化作【24 枚高爆破片散弹】扇形清屏！\n" +
							 $"[按 2 键] 虚空财团 -> 爆甲原地坍缩为【2秒微型引力黑洞】吸附聚怪！\n" +
							 $"[按 3 键] 深空生化 -> 爆甲释放【5秒强酸火海毒雾】并射出自爆毒刺！\n" +
							 $"--------------------------------------------------\n" +
							 $"[操控] WASD 飞行 | 空格牛顿漂移 | [Q/E/Z 爆甲] | [按 R 键]: 满血重置战局";
		}
	}
}
