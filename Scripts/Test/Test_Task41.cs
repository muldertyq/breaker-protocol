using System.IO;
using System.Linq;
using Godot;
using BreakerProtocol.Camera;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Combat.Projectiles;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Persistence;
using BreakerProtocol.Ship;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Meta;
using BreakerProtocol.World.Pacts;
using BreakerProtocol.World.Sector;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-41 演练场：战役存档、母港 Meta 进度与持久化系统验证中枢
	/// </summary>
	public partial class Test_Task41 : Node2D
	{
		private ShipEntity _playerShip = null!;
		private CombatCameraController _camera = null!;
		private BulletManager _bulletManager = null!;
		private JuiceManager _juice = null!;
		private VfxManager _vfx = null!;
		private RichTextLabel _hudLabel = null!;

		private SectorGraph _sectorGraph = null!;
		private string _operationLog = "🚀 演练场已就绪。支持暂存战局、母港科技加点与现场 1:1 复原！";

		public override void _Ready()
		{
			_juice = new JuiceManager { Name = "JuiceManager" };
			AddChild(_juice);

			_vfx = new VfxManager { Name = "VfxManager" };
			AddChild(_vfx);

			_bulletManager = new BulletManager { Name = "BulletManager" };
			AddChild(_bulletManager);

			// 1. 初始化经济与玩家飞船
			PlayerEconomyManager.Instance.Reset(initialScraps: 320, initialCores: 2);

			_playerShip = new ShipEntity
			{
				Name = "PlayerShip_T41",
				Position = Vector2.Zero
			};
			_playerShip.AddToGroup("Player");
			_playerShip.AddToGroup("Ship");
			AddChild(_playerShip);

			if (DataManager.Instance.Blueprints.TryGet("bp_hf_m_anvil", out var bp))
			{
				ShipBlueprintLoader.ApplyBlueprint(_playerShip, bp!);
			}

			// 2. 生成模拟星图
			_sectorGraph = SectorMapGenerator.GenerateSector(totalColumns: 8);
			_sectorGraph.CurrentNodeId = _sectorGraph.NodesByColumn[0][0].Id;
			_sectorGraph.NodesByColumn[0][0].State = NodeExplorationState.Visited;

			// 3. 摄像机
			_camera = new CombatCameraController { TargetShip = _playerShip };
			AddChild(_camera);
			_juice.BindCamera(_camera);

			CreateUI();
		}

		private void CreateUI()
		{
			var canvas = new CanvasLayer();
			AddChild(canvas);

			_hudLabel = new RichTextLabel
			{
				Position = new Vector2(30, 20),
				Size = new Vector2(1220, 320),
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
				// [按 1 键]: 制造战损 (随机扣减构件血量，消耗/增加废料)
				if (ek.Keycode == Key.Key1)
				{
					InflictDamageAndMutate();
				}
				// [按 2 键]: 星图跃迁推进 (模拟下一星区)
				else if (ek.Keycode == Key.Key2)
				{
					AdvanceSectorTest();
				}
				// [按 3 键]: 母港科技点亮并保存 Meta
				else if (ek.Keycode == Key.Key3)
				{
					UnlockMetaTechTest();
				}
				// [按 S 键]: 执行全量存盘 (Save Run & Meta)
				else if (ek.Keycode == Key.S)
				{
					bool s1 = SaveManager.Instance.SaveCurrentRun(_playerShip, _sectorGraph);
					bool s2 = SaveManager.Instance.SaveMeta();
					_operationLog = (s1 && s2)
						? "[color=lime]💾 【战局+母港全量存盘成功】 SHA-256 签名已封包写入 user:// 目录下！[/color]"
						: "[color=red]❌ 存盘失败！[/color]";
					UpdateHUD();
				}
				// [按 L 键]: 从本地磁盘读取并完全复原现场
				else if (ek.Keycode == Key.L)
				{
					bool r1 = SaveManager.Instance.RestoreCurrentRun(_playerShip, out var restoredGraph);
					bool r2 = SaveManager.Instance.LoadMeta();
					if (restoredGraph != null) _sectorGraph = restoredGraph;

					_operationLog = (r1 && r2)
						? "[color=cyan]🔄 【战局现场 100% 复原成功】 战损耐久、导线网络、星图与母港数据完全对齐！[/color]"
						: "[color=red]❌ 读取失败或未找到存档文件！[/color]";
					UpdateHUD();
				}
				// [按 X 键]: 故意破坏存档触发 SHA-256 防篡改拦截
				else if (ek.Keycode == Key.X)
				{
					TamperSaveFileTest();
				}
				// [按 D 键]: 清除战局暂存 (模拟通关/阵亡删除)
				else if (ek.Keycode == Key.D)
				{
					SaveManager.Instance.DeleteRunSave();
					_operationLog = "[color=yellow]🗑️ 已删除局内存档 (user://current_run.json)。[/color]";
					UpdateHUD();
				}
			}
		}

		private void InflictDamageAndMutate()
		{
			var modules = _playerShip.Grid.Modules.ToList();
			if (modules.Count > 0)
			{
				var target = modules[(int)GD.RandRange(0, modules.Count - 1)];
				target.CurrentHp = Mathf.Max(10.0f, target.CurrentHp - 60.0f);
			}

			PlayerEconomyManager.Instance.AddScraps(75);
			_operationLog = $"[color=orange]💥 战况突变：随机构件受到损伤，当前废料: {PlayerEconomyManager.Instance.Scraps} ⚙[/color]";
			UpdateHUD();
		}

		private void AdvanceSectorTest()
		{
			int nextCol = Mathf.Min(_sectorGraph.TotalColumns - 1, (int)_sectorGraph.PursuitWavefrontColumn + 2);
			if (nextCol < _sectorGraph.TotalColumns && _sectorGraph.NodesByColumn[nextCol].Count > 0)
			{
				var nextNode = _sectorGraph.NodesByColumn[nextCol][0];
				_sectorGraph.CurrentNodeId = nextNode.Id;
				nextNode.State = NodeExplorationState.Visited;
				_sectorGraph.PursuitWavefrontColumn += 1.0f;
				_operationLog = $"[color=cyan]🚀 星区跃迁：抵达第 {nextCol + 1} 列【{nextNode.GetDisplayName()}】，追击线推至 {_sectorGraph.PursuitWavefrontColumn} 列！[/color]";
			}
			UpdateHUD();
		}

		private void UnlockMetaTechTest()
		{
			var meta = MetaProgressionManager.Instance;
			meta.AddDataFragments(200);

			// 查找下一个当前满足前置条件且未解锁的科技
			var nextEligible = meta.AllTechs.Values.FirstOrDefault(t => 
				!t.IsUnlocked && 
				(string.IsNullOrEmpty(t.PrerequisiteId) || meta.IsUnlocked(t.PrerequisiteId))
			);

			if (nextEligible != null)
			{
				if (meta.UnlockTech(nextEligible.Id))
				{
					// 仅在内存中解锁，移除此处的自动 SaveMeta()，统一由 [S 键] 控制存盘
					_operationLog = $"[color=gold]🔬 母港研发：内存点亮科技【{nextEligible.Name}】(未存盘，按 S 存盘，按 L 可回退)！[/color]";
				}
				else
				{
					_operationLog = $"[color=red]❌ 解锁【{nextEligible.Name}】失败！研发碎片不足 (需要 {nextEligible.Cost} 💾)。[/color]";
				}
			}
			else
			{
				_operationLog = "[color=yellow]✨ 所有母港科技已全部解锁完毕！[/color]";
			}

			UpdateHUD();
		}

		private void TamperSaveFileTest()
		{
			string userDir = ProjectSettings.GlobalizePath("user://");
			string path = Path.Combine(userDir, "current_run.json");
			if (File.Exists(path))
			{
				string text = File.ReadAllText(path);
				// 恶意修改废料数值
				text = text.Replace("\"currentScraps\":", "\"currentScraps\": 999999, \"_tampered\":");
				File.WriteAllText(path, text);
				_operationLog = "[color=crimson]⚠️ [恶意篡改模拟] 已直接修改 current_run.json 废料数值，现在按 [L] 键测试校验器！[/color]";
			}
			else
			{
				_operationLog = "[color=yellow]请先按 [S 键] 存盘后再测试篡改！[/color]";
			}
			UpdateHUD();
		}

		public override void _Process(double delta)
		{
			// 鼠标开火
			if (Godot.Input.IsMouseButtonPressed(MouseButton.Left))
			{
				foreach (var weaponId in _playerShip.Pulses.WeaponBuffers.Keys)
				{
					_playerShip.Pulses.TriggerWeaponFire(weaponId, out _);
				}
			}
		}

		private void UpdateHUD()
		{
			var eco = PlayerEconomyManager.Instance;
			var meta = MetaProgressionManager.Instance;

			float curHp = 0, maxHp = 0;
			foreach (var m in _playerShip.Grid.Modules)
			{
				if (!m.IsDestroyed)
				{
					curHp += m.CurrentHp;
					maxHp += m.MaxHp;
				}
			}

			_hudLabel.Text =
				$"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
				$"[b][color=yellow]【TASK-41 战役存档、母港 Meta 进度与持久化系统演练场】[/color][/b]\n" +
				$"[color=lime]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/color]\n" +
				$"• [color=white]战舰总耐久:[/color] [color=lightgreen]{curHp:F0} / {maxHp:F0} HP[/color]   " +
				$"• [color=white]局内资产:[/color] [color=yellow]{eco.Scraps} ⚙ 废料[/color] | [color=cyan]{eco.ComputeCores} 💠 核心[/color]   " +
				$"• [color=white]当前星图位置:[/color] [color=gold]{_sectorGraph.CurrentNodeId ?? "起始"}[/color]\n" +
				$"• [color=white]母港研发碎片:[/color] [color=cyan]{meta.DataFragments} 💾[/color]   " +
				$"• [color=white]已激活科技:[/color] [color=gold]{meta.AllTechs.Values.Count(t => t.IsUnlocked)} / {meta.AllTechs.Count}[/color] 项   " +
				$"• [color=white]局内存档状态:[/color] {(SaveManager.Instance.HasActiveRunSave() ? "[color=green]存在暂存 (user://)[/color]" : "[color=gray]无暂存[/color]")}\n" +
				$"• [color=white]最近操作日志:[/color] {_operationLog}\n" +
				$"------------------------------------------------------------------------------------\n" +
				$"[color=yellow][持久化安全测试指南][/color]:\n" +
				$"1. [按 1 键]: 制造构件战损与资产变动 | [按 2 键]: 推进星区列与追击波前；\n" +
				$"2. [按 3 键]: 获得碎片并点亮母港科技 | [按 S 键]: 触发全量保存 (生成 SHA-256 签名信封)；\n" +
				$"3. [按 L 键]: 从磁盘 100% 复原现场战损与星图 | [按 X 键]: 模拟外部篡改测试防爆拦截！";
		}
	}
}
