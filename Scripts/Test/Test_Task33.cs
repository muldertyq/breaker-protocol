using Godot;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.UI.Meta;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Meta;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-33 交互式验证场景：母港科研局与局外永久 Meta 科技树解锁演练场
	/// </summary>
	public partial class Test_Task33 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private BulletManager _bulletManager = null!;
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

			// 1. 初始化经济系统 (开局默认 200 废料)
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

			// 注入当前已激活的 Meta 科技增益
			MetaProgressionManager.Instance.ApplyMetaBuffsToNewRun(_playerShip);

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

			_techTreeUI = new MetaTechTreeUI();
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

		public override void _Process(double delta)
		{
			// [按 TAB 键]: 随时切换科技树面板显隐
			if (Input.IsActionJustPressed("ui_focus_next") || Input.IsKeyPressed(Key.Tab))
			{
				_techTreeUI.Visible = !_techTreeUI.Visible;
			}

			// [按 + 键]: 注入 100 研发数据碎片
			if (Input.IsKeyPressed(Key.Equal) || Input.IsKeyPressed(Key.KpAdd))
			{
				MetaProgressionManager.Instance.AddDataFragments(100);
			}

			// [按 R 键]: 模拟开启一局新战役 (重新应用已解锁 Meta 增益)
			if (Input.IsKeyPressed(Key.R))
			{
				PlayerEconomyManager.Instance.Reset(200, 1);
				if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var anvilBp))
				{
					ShipBlueprintLoader.ApplyBlueprint(_playerShip, anvilBp!);
				}
				MetaProgressionManager.Instance.ApplyMetaBuffsToNewRun(_playerShip);
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

			DisplayServer.WindowSetTitle($"《断路协议》| 母港科研局 | 帧率: {fps:F0} FPS | 研发碎片: {MetaProgressionManager.Instance.DataFragments} 💾 | 开局废料: {PlayerEconomyManager.Instance.Scraps} ⚙");

			_hudLabel.Text = $"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
							 $"[b][color=yellow]【TASK-33 母港科研局演练场】[/color][/b] " +
							 $"研发碎片: [color=cyan]{MetaProgressionManager.Instance.DataFragments} 💾[/color] | 开局废料: [color=gold]{PlayerEconomyManager.Instance.Scraps} ⚙[/color] | 舰体装甲总耐久: [color=lightgreen]{curHp:F0}/{maxHp:F0} HP[/color] | [color=white][按 TAB 键]: 开闭科研面板 | [按 + 键]: +100碎片 | [按 R 键]: 模拟新开局检验增益[/color]";
		}
	}
}
