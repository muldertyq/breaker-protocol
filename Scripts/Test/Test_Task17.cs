using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-17 交互式验证场景：三通道调色板、战损掉漆噪点与全舰网格着色
	/// </summary>
	public partial class Test_Task17 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private ShipEntity _targetShip = null!;
		private CombatCameraController _camera = null!;
		private VfxManager _vfx = null!;
		private Label _hudLabel = null!;

		public override void _Ready()
		{
			// 1. 特效管理器
			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			// 2. 玩家战舰
			_playerShip = new ShipEntity
			{
				Name = "ShaderPlayerShip",
				Position = new Vector2(600, 560)
			};
			_playerShip.AddToGroup("Player");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
			}

			// 3. 靶舰 (600, 220)
			_targetShip = new ShipEntity
			{
				Name = "ShaderTargetShip",
				Position = new Vector2(600, 220),
				Rotation = Mathf.Pi
			};
			AddChild(_targetShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var targetBp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_targetShip, targetBp!);
			}

			// 4. 摄像机
			_camera = new CombatCameraController
			{
				TargetShip = _playerShip
			};
			AddChild(_camera);

			CreateHUD();
		}

		private void CreateHUD()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_hudLabel = new Label
			{
				Position = new Vector2(25, 25),
				Size = new Vector2(520, 600)
			};
			_hudLabel.AddThemeFontSizeOverride("font_size", 15);
			_hudLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f));
			canvasLayer.AddChild(_hudLabel);
		}

		public override void _Process(double delta)
		{
			// 色板切换热键 (1~4)
			if (Input.IsKeyPressed(Key.Key1))
			{
				_playerShip.CurrentPalette = FactionPalettes.HeavyFoundry;
			}
			else if (Input.IsKeyPressed(Key.Key2))
			{
				_playerShip.CurrentPalette = FactionPalettes.VoidSyndicate;
			}
			else if (Input.IsKeyPressed(Key.Key3))
			{
				_playerShip.CurrentPalette = FactionPalettes.BioChitin;
			}
			else if (Input.IsKeyPressed(Key.Key4))
			{
				_playerShip.CurrentPalette = FactionPalettes.OutlawScrapper;
			}

			// 鼠标左键开火
			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}

			// 按 R 键满血复原靶舰
			if (Input.IsKeyPressed(Key.R))
			{
				if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var targetBp))
				{
					ShipBlueprintLoader.ApplyBlueprint(_targetShip, targetBp!);
				}
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			_hudLabel.Text = $"【《断路协议》TASK-17 调色板着色器与战损掉漆展厅】\n" +
							 $"==================================================\n" +
							 $"当前玩家色板:   {_playerShip.CurrentPalette.Name}\n" +
							 $"主色 (Primary):   {_playerShip.CurrentPalette.PrimaryColor.ToHtml()}\n" +
							 $"副色 (Secondary): {_playerShip.CurrentPalette.SecondaryColor.ToHtml()}\n" +
							 $"发光 (Accent):    {_playerShip.CurrentPalette.AccentColor.ToHtml()}\n" +
							 $"--------------------------------------------------\n" +
							 $"[色板即时换装快捷键]\n" +
							 $"[按 1 键]: 【重工联合】 钛合金灰 + 工业工程橙\n" +
							 $"[按 2 键]: 【虚空财团】 虚空深紫 + 霓虹紫罗兰\n" +
							 $"[按 3 键]: 【深空生化】 异星暗绿 + 强酸毒液荧光\n" +
							 $"[按 4 键]: 【赏金猎人】 废土铁锈红 + 斑马警示黄\n" +
							 $"--------------------------------------------------\n" +
							 $"[Shader 战损演变验证]\n" +
							 $"1. 鼠标瞄准前方靶舰射击，观察外层装甲【HP < 60% 剥落露出银灰色铁皮】\n" +
							 $"2. 受击处自动【泛出渐变焦黑灼烧痕迹】\n" +
							 $"3. [按 R 键]: 满血重构复原靶舰";
		}
	}
}
