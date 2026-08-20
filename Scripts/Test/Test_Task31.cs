using Godot;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.UI.Market;
using BreakerProtocol.World.Economy;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-31 交互式验证场景：深空黑市改装站与构件交易经济循环演练场
	/// </summary>
	public partial class Test_Task31 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private BulletManager _bulletManager = null!;
		private BlackMarketShopUI _shopUI = null!;
		private RichTextLabel _hudLabel = null!;

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			// 1. 初始化经济管理器 (赠送 600 废料与 3 算力核心)
			PlayerEconomyManager.Instance.Reset(initialScraps: 600, initialCores: 3);

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
		}

		private void CreateUI()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_shopUI = new BlackMarketShopUI();
			_shopUI.Initialize(_playerShip);
			canvasLayer.AddChild(_shopUI);

			_hudLabel = new RichTextLabel
			{
				Position = new Vector2(30, 15),
				Size = new Vector2(1220, 50),
				BbcodeEnabled = true,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			_hudLabel.AddThemeFontSizeOverride("normal_font_size", 14);
			canvasLayer.AddChild(_hudLabel);
		}

		public override void _Process(double delta)
		{
			// [按 TAB 键]: 切换黑市面板开闭
			if (Input.IsActionJustPressed("ui_focus_next") || Input.IsKeyPressed(Key.Tab))
			{
				_shopUI.Visible = !_shopUI.Visible;
			}

			// [按 + 键 / 加号]: 立即注入 200 废料用于测试
			if (Input.IsKeyPressed(Key.Equal) || Input.IsKeyPressed(Key.KpAdd))
			{
				PlayerEconomyManager.Instance.AddScraps(200);
			}

			// [按 R 键]: 满额重置金钱与货架
			if (Input.IsKeyPressed(Key.R))
			{
				PlayerEconomyManager.Instance.Reset(600, 3);
				_shopUI.RefreshStock();
			}

			UpdateHUD();
		}

		private void UpdateHUD()
		{
			double fps = Engine.GetFramesPerSecond();
			DisplayServer.WindowSetTitle($"《断路协议》| 黑市改装终端 | 帧率: {fps:F0} FPS | 废料: {PlayerEconomyManager.Instance.Scraps} ⚙️");

			_hudLabel.Text = $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"[b][color=yellow]【TASK-31 深空黑市改装站与构件交易经济循环演练场】[/color][/b] " +
							 $"[color=cyan]• [按 + 键]: 注入+200废料 | [按 R 键]: 重置金钱货架 | [按 TAB 键]: 开闭交易面板[/color]";
		}
	}
}
