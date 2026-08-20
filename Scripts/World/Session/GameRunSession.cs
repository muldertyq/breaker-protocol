using System;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Persistence;
using BreakerProtocol.Ship;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Meta;
using BreakerProtocol.World.Pacts;
using BreakerProtocol.World.Sector;
using BreakerProtocol.World.Settlement;

namespace BreakerProtocol.World.Session
{
	/// <summary>
	/// 单局战役上下文实体与跨关卡状态同步总线 (单例模式)
	/// 持有玩家战舰实体引用、星图拓扑、战役统计与资产，保障关卡流转零损耗
	/// </summary>
	public class GameRunSession
	{
		private static GameRunSession? _instance;
		public static GameRunSession Instance => _instance ??= new GameRunSession();

		public bool IsSessionActive { get; private set; } = false;
		public ShipEntity? PlayerShip { get; private set; }
		public SectorGraph? CurrentSectorGraph { get; private set; }
		public RunStatistics CurrentStats { get; private set; } = new();

		/// <summary>
		/// 当前战舰所停靠/探索的星区拓扑节点
		/// </summary>
		public SectorNode? CurrentNode
		{
			get
			{
				if (CurrentSectorGraph != null && !string.IsNullOrEmpty(CurrentSectorGraph.CurrentNodeId))
				{
					if (CurrentSectorGraph.AllNodes.TryGetValue(CurrentSectorGraph.CurrentNodeId, out var node))
					{
						return node;
					}
				}
				return null;
			}
		}

		public float SessionElapsedSeconds => CurrentStats.DurationSeconds;

		public event Action? OnSessionStarted;
		public event Action<SectorNode>? OnNodeAdvanced;
		public event Action<RunStatistics>? OnSessionEnded;
		public event Action? OnShipStateMutated;

		private GameRunSession() { }

		/// <summary>
		/// 绑定全局唯一的玩家战舰实体引用
		/// </summary>
		public void BindPlayerShip(ShipEntity ship)
		{
			PlayerShip = ship;
		}

		/// <summary>
		/// 初始化一轮全新战役会话
		/// </summary>
		public void InitializeNewRun(string blueprintId = "bp_hf_m_anvil")
		{
			SaveManager.Instance.DeleteRunSave();
			PlayerEconomyManager.Instance.Reset(initialScraps: 200, initialCores: 1);
			CalamityPactManager.Instance.Reset();

			// 1. 初始化战役战果统计数据
			CurrentStats = new RunStatistics
			{
				Ending = RunEndingType.Victory,
				SectorsCleared = 1,
				StandardEnemiesKilled = 0,
				ElitesKilled = 0,
				BossesKilled = 0,
				TotalScrapsEarned = 0,
				ComputeCoresEarned = 0,
				DamageTakenTotal = 0.0f,
				FinalHullIntegrityPercent = 100.0f,
				DurationSeconds = 0.0f
			};

			// 2. 组装玩家初始战舰并应用母港科技增益
			if (PlayerShip != null)
			{
				if (DataManager.Instance.Blueprints.TryGet(blueprintId, out var bp) && bp != null)
				{
					ShipBlueprintLoader.ApplyBlueprint(PlayerShip, bp);
				}
				MetaProgressionManager.Instance.ApplyMetaBuffsToNewRun(PlayerShip);
			}

			// 3. 生成全新 8 列 DAG 星图
			CurrentSectorGraph = SectorMapGenerator.GenerateSector(totalColumns: 8);
			if (CurrentSectorGraph.NodesByColumn.Count > 0 && CurrentSectorGraph.NodesByColumn[0].Count > 0)
			{
				var startNode = CurrentSectorGraph.NodesByColumn[0][0];
				CurrentSectorGraph.CurrentNodeId = startNode.Id;
				startNode.State = NodeExplorationState.Visited;
			}

			IsSessionActive = true;
			SaveCurrentRun();

			GD.PrintRich($"[color=green][GameRunSession] 🚀 全新战役会话建立就绪！初始蓝图: [{blueprintId}][/color]");
			OnSessionStarted?.Invoke();
		}

		/// <summary>
		/// 从本地安全存档恢复战役会话
		/// </summary>
		public bool RestoreSavedRun(ShipEntity targetShip)
		{
			BindPlayerShip(targetShip);

			if (SaveManager.Instance.RestoreCurrentRun(targetShip, out var restoredGraph))
			{
				CurrentSectorGraph = restoredGraph;
				IsSessionActive = true;

				// 恢复战况统计
				CurrentStats = new RunStatistics
				{
					SectorsCleared = CurrentSectorGraph?.NodesByColumn.FindIndex(col => col.Exists(n => n.Id == CurrentSectorGraph.CurrentNodeId)) + 1 ?? 1,
					TotalScrapsEarned = PlayerEconomyManager.Instance.Scraps,
					ComputeCoresEarned = PlayerEconomyManager.Instance.ComputeCores
				};

				GD.PrintRich("[color=green][GameRunSession] ✔ 战役会话已从存档完整恢复！[/color]");
				OnSessionStarted?.Invoke();
				return true;
			}

			return false;
		}

		/// <summary>
		/// 战役进行期间的战舰状态突变通知 (战损、改装、飞线)
		/// </summary>
		public void NotifyShipStateChanged()
		{
			OnShipStateMutated?.Invoke();
		}

		/// <summary>
		/// 记录战局内时间流逝
		/// </summary>
		public void UpdateSessionTimer(float dt)
		{
			if (!IsSessionActive) return;
			CurrentStats.DurationSeconds += dt;
		}

		/// <summary>
		/// 记录击杀敌舰
		/// </summary>
		public void RecordEnemyKilled(string role)
		{
			if (!IsSessionActive) return;

			switch (role)
			{
				case "Boss":
					CurrentStats.BossesKilled++;
					break;
				case "Elite":
					CurrentStats.ElitesKilled++;
					break;
				default:
					CurrentStats.StandardEnemiesKilled++;
					break;
			}
		}

		/// <summary>
		/// 记录承受装甲创伤
		/// </summary>
		public void RecordDamageTaken(float damage)
		{
			if (!IsSessionActive) return;
			CurrentStats.DamageTakenTotal += damage;
		}

		/// <summary>
		/// 星区航路推进跃迁至下一节点
		/// </summary>
		public void AdvanceToNode(SectorNode nextNode)
		{
			if (CurrentSectorGraph == null) return;

			CurrentSectorGraph.CurrentNodeId = nextNode.Id;
			nextNode.State = NodeExplorationState.Visited;

			// 追击舰队波前推进 1.0 列
			CurrentSectorGraph.PursuitWavefrontColumn += 1.0f;
			CurrentStats.SectorsCleared = Mathf.Max(CurrentStats.SectorsCleared, nextNode.Column + 1);

			SaveCurrentRun();
			OnNodeAdvanced?.Invoke(nextNode);
		}

		/// <summary>
		/// 备份当前会话现场到持久化磁盘 (user://current_run.json)
		/// </summary>
		public bool SaveCurrentRun()
		{
			if (!IsSessionActive || PlayerShip == null) return false;
			return SaveManager.Instance.SaveCurrentRun(PlayerShip, CurrentSectorGraph);
		}

		/// <summary>
		/// 战役终局结算 (通关或殉爆)
		/// </summary>
		public RunStatistics TerminateRun(RunEndingType ending)
		{
			IsSessionActive = false;
			CurrentStats.Ending = ending;
			CurrentStats.TotalScrapsEarned = PlayerEconomyManager.Instance.Scraps;
			CurrentStats.ComputeCoresEarned = PlayerEconomyManager.Instance.ComputeCores;

			// 计算完好度
			float curHp = 0, maxHp = 0;
			if (PlayerShip?.Grid != null)
			{
				foreach (var m in PlayerShip.Grid.Modules)
				{
					if (!m.IsDestroyed) curHp += m.CurrentHp;
					maxHp += m.MaxHp;
				}
			}
			CurrentStats.FinalHullIntegrityPercent = maxHp > 0 ? (curHp / maxHp * 100.0f) : 0.0f;

			SaveManager.Instance.DeleteRunSave();
			var finalSettlement = RunSettlementService.CalculateSettlement(CurrentStats);

			OnSessionEnded?.Invoke(finalSettlement);
			return finalSettlement;
		}
	}
}
