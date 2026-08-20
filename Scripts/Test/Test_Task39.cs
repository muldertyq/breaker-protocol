using System.Linq;
using Godot;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Ship;
using BreakerProtocol.UI.Events;
using BreakerProtocol.UI.Market;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Events;
using BreakerProtocol.World.Market;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-39 演练场：全域游戏规则与外循环 JSON 数据驱动解耦验证中枢
	/// </summary>
	public partial class Test_Task39 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private BulletManager _bulletManager = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;

		private SpaceEventDialogueUI _eventUI = null!;
		private BlackMarketShopUI _marketUI = null!;
		private RichTextLabel _hudLabel = null!;

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			// 1. 初始化经济与玩家战舰
			PlayerEconomyManager.Instance.Reset(initialScraps: 500, initialCores: 3);

			_playerShip = new ShipEntity
			{
				Name = "PlayerShip_T39",
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

			// 3. 构建全息 UI
			CreateUI();
		}

		private void CreateUI()
		{
			var canvas = new CanvasLayer();
			AddChild(canvas);

			_eventUI = new SpaceEventDialogueUI();
			canvas.AddChild(_eventUI);

			_marketUI = new BlackMarketShopUI();
			_marketUI.Initialize(_playerShip);
			_marketUI.Visible = false;
			canvas.AddChild(_marketUI);

			_hudLabel = new RichTextLabel
			{
				Position = new Vector2(30, 20),
				Size = new Vector2(1220, 260),
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
				// [按 R 键]: 重新扫描全域 JSON 配置并热重载
				if (ek.Keycode == Key.R)
				{
					DataManager.Instance.LoadAllData();
					_marketUI.RefreshStock();
					UpdateHUD();
				}
				// [按 1 键]: 触发由 JSON 加载的异象事件
				else if (ek.Keycode == Key.Key1)
				{
					_marketUI.Visible = false;
					_eventUI.OpenEvent(SpaceEventDatabase.GetRandomEvent(), _playerShip);
				}
				// [按 2 键]: 打开由 JSON 价格表驱动的黑市
				else if (ek.Keycode == Key.Key2)
				{
					_eventUI.Visible = false;
					_marketUI.Visible = !_marketUI.Visible;
				}
				// [按 + 键]: 快速增加废料
				else if (ek.Keycode == Key.Equal || ek.Keycode == Key.KpAdd)
				{
					PlayerEconomyManager.Instance.AddScraps(100);
					UpdateHUD();
				}
			}
		}

		public override void _Process(double delta)
		{
			// 鼠标左键开火 (UI 关闭时)
			if (Godot.Input.IsMouseButtonPressed(MouseButton.Left) && !_eventUI.Visible && !_marketUI.Visible)
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
			var eco = PlayerEconomyManager.Instance;

			_hudLabel.Text =
				$"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
				$"[b][color=yellow]【TASK-39 全域游戏规则与外循环 JSON 数据驱动解耦验证演练场】[/color][/b]\n" +
				$"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
				$"• [color=white]内存构件总数:[/color] [color=cyan]{dm.Modules.Count}[/color] 个   " +
				$"• [color=white]预设战舰蓝图:[/color] [color=cyan]{dm.Blueprints.Count}[/color] 艘   " +
				$"• [color=white]外部异象事件:[/color] [color=gold]{dm.Events.Count}[/color] 个\n" +
				$"• [color=white]母港科技节点:[/color] [color=gold]{dm.Techs.Count}[/color] 项   " +
				$"• [color=white]灾厄高危契约:[/color] [color=gold]{dm.Pacts.Count}[/color] 份   " +
				$"• [color=white]黑市默认货架:[/color] [color=gold]{dm.MarketConfig.DefaultStockCount}[/color] 格\n" +
				$"• [color=white]当前玩家资产:[/color] [color=yellow]{eco.Scraps} ⚙ 废料[/color] | [color=cyan]{eco.ComputeCores} 💠 算力核心[/color]\n" +
				$"------------------------------------------------------------------------------------\n" +
				$"[color=yellow][数据驱动热重载测试指南][/color]:\n" +
				$"1. [按 1 键]: 呼出外部 JSON 驱动的【深空随机异象多分支树】；\n" +
				$"2. [按 2 键]: 开闭外部 JSON 价格表驱动的【废土黑市交易终端】；\n" +
				$"3. [切出窗口]: 修改 core_data/events/ 或 core_data/markets/ 下的 JSON 参数，\n" +
				$"   切回游戏 [按 R 键] 立即热加载生效，体验 100% 零代码全域数据解耦！";
		}
	}
}
