using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Ship;
using BreakerProtocol.Ship.Pipeline;
using BreakerProtocol.World.Economy;
using BreakerProtocol.World.Meta;
using BreakerProtocol.World.Pacts;
using BreakerProtocol.World.Sector;

namespace BreakerProtocol.Data.Persistence
{
	/// <summary>
	/// 全局战役存档、母港 Meta 进度与持久化总管中枢 (单例模式)
	/// </summary>
	public class SaveManager
	{
		private static SaveManager? _instance;
		public static SaveManager Instance => _instance ??= new SaveManager();

		private const string SaltKey = "BREAKER_PROTOCOL_DATA_INTEGRITY_SALT_2026";
		private readonly string _metaSavePath;
		private readonly string _runSavePath;

		public static readonly JsonSerializerOptions JsonOpts = new()
		{
			WriteIndented = true,
			PropertyNameCaseInsensitive = true
		};

		private SaveManager()
		{
			string userDir = ProjectSettings.GlobalizePath("user://");
			if (!Directory.Exists(userDir))
			{
				Directory.CreateDirectory(userDir);
			}

			_metaSavePath = Path.Combine(userDir, "meta_progression.json");
			_runSavePath = Path.Combine(userDir, "current_run.json");
		}

		// =====================================================================
		// 1. 母港永久 Meta 科技树存档管理
		// =====================================================================

		/// <summary>
		/// 保存母港 Meta 科技树与生涯进度
		/// </summary>
		public bool SaveMeta()
		{
			try
			{
				var metaData = new MetaSaveData
				{
					DataFragments = MetaProgressionManager.Instance.DataFragments,
					UnlockedTechIds = new List<string>()
				};

				foreach (var tech in MetaProgressionManager.Instance.AllTechs.Values)
				{
					if (tech.IsUnlocked)
					{
						metaData.UnlockedTechIds.Add(tech.Id);
					}
				}

				return WriteEnvelopeToFile(_metaSavePath, metaData);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[SaveManager] 保存母港 Meta 进度失败: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// 读取母港 Meta 科技树并注入 MetaProgressionManager
		/// </summary>
		public bool LoadMeta()
		{
			if (!File.Exists(_metaSavePath))
			{
				GD.Print("[SaveManager] 未发现历史母港存档，将建立全新研发档案。");
				return false;
			}

			if (ReadEnvelopeFromFile<MetaSaveData>(_metaSavePath, out var metaData) && metaData != null)
			{
				// 原子化复原母港碎片与科技激活列表
				MetaProgressionManager.Instance.LoadState(metaData.DataFragments, metaData.UnlockedTechIds);

				GD.PrintRich($"[color=green][SaveManager] ✔ 成功加载母港存档: 研发碎片={metaData.DataFragments} 💾 | 已激活科技={metaData.UnlockedTechIds.Count} 项[/color]");
				return true;
			}

			GD.PrintErr("[SaveManager] 母港存档数据校验失败或被非法篡改！");
			return false;
		}

		// =====================================================================
		// 2. 单局战役现场快照 (战损残局与星图暂存)
		// =====================================================================

		public bool HasActiveRunSave() => File.Exists(_runSavePath);

		public void DeleteRunSave()
		{
			if (File.Exists(_runSavePath))
			{
				File.Delete(_runSavePath);
				GD.Print("[SaveManager] 单局战役现场存档已清理 (战役结束/撤离归港)。");
			}
		}

		/// <summary>
		/// 捕获当前战役全景并存盘 (战舰战损、导线网络、废料、契约与星图)
		/// </summary>
		public bool SaveCurrentRun(ShipEntity playerShip, SectorGraph? sectorGraph)
		{
			try
			{
				var runData = new RunSaveData
				{
					RunSeed = (int)GD.Randi(),
					CurrentScraps = PlayerEconomyManager.Instance.Scraps,
					CurrentCores = PlayerEconomyManager.Instance.ComputeCores
				};

				// 1. 记录激活契约
				foreach (var pact in CalamityPactManager.Instance.GetActivePacts())
				{
					runData.ActivePactIds.Add(pact.Id);
				}

				// 2. 捕获战舰构件与战损残局
				if (playerShip?.Grid != null)
				{
					foreach (var m in playerShip.Grid.Modules)
					{
						runData.Ship.Modules.Add(new SavedModuleState
						{
							ModuleId = m.Definition.Id,
							GridX = m.GridPosition.X,
							GridY = m.GridPosition.Y,
							Rotation = m.Rotation,
							CurrentHp = m.CurrentHp,
							MaxHp = m.MaxHp,
							IsDestroyed = m.IsDestroyed
						});
					}

					// 捕获 PCB 导线网络
					foreach (var wire in playerShip.Pipeline.Wires)
					{
						if (wire.GridPath == null || wire.GridPath.Count < 2) continue;

						Vector2I srcPos = wire.GridPath[0];
						Vector2I dstPos = wire.GridPath[^1];

						runData.Ship.Wires.Add(new SavedWireState
						{
							SourceGridX = srcPos.X,
							SourceGridY = srcPos.Y,
							TargetGridX = dstPos.X,
							TargetGridY = dstPos.Y
						});
					}
				}

				// 3. 捕获星图状态
				if (sectorGraph != null)
				{
					runData.Sector.TotalColumns = sectorGraph.TotalColumns;
					runData.Sector.CurrentNodeId = sectorGraph.CurrentNodeId;
					runData.Sector.PursuitWavefrontColumn = sectorGraph.PursuitWavefrontColumn;

					foreach (var node in sectorGraph.AllNodes.Values)
					{
						var nodeState = new SavedNodeState
						{
							NodeId = node.Id,
							Column = node.Column,
							Row = node.Row,
							Type = node.Type.ToString(),
							State = node.State.ToString(),
							NormX = node.NormalizedPosition.X,
							NormY = node.NormalizedPosition.Y,
							OutgoingConnections = new List<string>(node.OutgoingConnections)
						};
						runData.Sector.Nodes.Add(nodeState);
					}
				}

				return WriteEnvelopeToFile(_runSavePath, runData);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[SaveManager] 战局现场暂存失败: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// 从存档恢复单局现场 (1:1 完美复原战舰战损、断线与星图拓扑)
		/// </summary>
		public bool RestoreCurrentRun(ShipEntity targetShip, out SectorGraph? restoredGraph)
		{
			restoredGraph = null;
			if (!File.Exists(_runSavePath)) return false;

			if (!ReadEnvelopeFromFile<RunSaveData>(_runSavePath, out var runData) || runData == null)
			{
				GD.PrintErr("[SaveManager] 战局存档校验失败或已损坏！");
				return false;
			}

			// 1. 恢复资产与契约
			PlayerEconomyManager.Instance.Reset(runData.CurrentScraps, runData.CurrentCores);
			CalamityPactManager.Instance.Reset();
			foreach (var pactId in runData.ActivePactIds)
			{
				CalamityPactManager.Instance.TogglePact(pactId);
			}

			// 2. 复原战舰物理网格与战损血量
			if (targetShip?.Grid != null && runData.Ship.Modules.Count > 0)
			{
				targetShip.Grid.Clear();
				targetShip.Pipeline.Clear();

				foreach (var modRec in runData.Ship.Modules)
				{
					if (DataManager.Instance.Modules.TryGet(modRec.ModuleId, out var def) && def != null)
					{
						if (targetShip.Grid.TryPlaceModule(def, new Vector2I(modRec.GridX, modRec.GridY), modRec.Rotation, out var instance))
						{
							if (instance != null)
							{
								instance.CurrentHp = modRec.CurrentHp;
							}
						}
					}
				}

				// 复原 PCB 导线网络
				var pins = new List<PinInstance>(targetShip.GetAllPins());
				foreach (var wireRec in runData.Ship.Wires)
				{
					Vector2I srcPos = new(wireRec.SourceGridX, wireRec.SourceGridY);
					Vector2I dstPos = new(wireRec.TargetGridX, wireRec.TargetGridY);

					var srcPin = pins.Find(p => p.AbsoluteGridPos == srcPos && p.Type == PinType.OUT);
					var dstPin = pins.Find(p => p.AbsoluteGridPos == dstPos && p.Type == PinType.IN);

					if (srcPin != null && dstPin != null)
					{
						targetShip.Pipeline.TryAddWire(srcPin, dstPin, targetShip.Grid, out _);
					}
				}

				targetShip.RebuildPhysics();
			}

			// 3. 复原星区 DAG 拓扑
			if (runData.Sector.Nodes.Count > 0)
			{
				var graph = new SectorGraph
				{
					TotalColumns = runData.Sector.TotalColumns,
					CurrentNodeId = runData.Sector.CurrentNodeId,
					PursuitWavefrontColumn = runData.Sector.PursuitWavefrontColumn
				};

				for (int col = 0; col < graph.TotalColumns; col++)
				{
					graph.NodesByColumn.Add(new List<SectorNode>());
				}

				foreach (var nodeData in runData.Sector.Nodes)
				{
					var node = new SectorNode
					{
						Id = nodeData.NodeId,
						Column = nodeData.Column,
						Row = nodeData.Row,
						NormalizedPosition = new Vector2(nodeData.NormX, nodeData.NormY),
						Type = Enum.TryParse<SectorNodeType>(nodeData.Type, out var t) ? t : SectorNodeType.Combat,
						State = Enum.TryParse<NodeExplorationState>(nodeData.State, out var s) ? s : NodeExplorationState.Unreachable
					};
					node.OutgoingConnections.AddRange(nodeData.OutgoingConnections);

					graph.AllNodes[node.Id] = node;
					if (node.Column >= 0 && node.Column < graph.TotalColumns)
					{
						graph.NodesByColumn[node.Column].Add(node);
					}
				}

				restoredGraph = graph;
			}

			GD.PrintRich($"[color=green][SaveManager] ✔ 战局现场 100% 复原成功！废料={runData.CurrentScraps} | 构件数={runData.Ship.Modules.Count} | 星区列={restoredGraph?.CurrentNodeId ?? "起始"}[/color]");
			return true;
		}

		// =====================================================================
		// 3. 底层安全信封与 SHA-256 哈希校验管道 (统一使用标准对象流生成签名)
		// =====================================================================

		private bool WriteEnvelopeToFile<T>(string filePath, T payload)
		{
			string payloadJson = JsonSerializer.Serialize(payload, JsonOpts);
			string signature = ComputeSha256(payloadJson + SaltKey);

			var envelope = new SaveEnvelope<T>
			{
				Version = "1.0.0",
				Timestamp = DateTime.UtcNow.ToString("o"),
				Sha256Signature = signature,
				Payload = payload
			};

			string fullJson = JsonSerializer.Serialize(envelope, JsonOpts);
			File.WriteAllText(filePath, fullJson);
			return true;
		}

		private bool ReadEnvelopeFromFile<T>(string filePath, out T? payload)
		{
			payload = default;
			try
			{
				string fullJson = File.ReadAllText(filePath);
				var envelope = JsonSerializer.Deserialize<SaveEnvelope<T>>(fullJson, JsonOpts);

				if (envelope == null || envelope.Payload == null)
				{
					return false;
				}

				// 将解析出的 Payload 按相同规范序列化后核验签名，完全规避层级缩进差异
				string canonicalPayloadJson = JsonSerializer.Serialize(envelope.Payload, JsonOpts);
				string actualSignature = ComputeSha256(canonicalPayloadJson + SaltKey);

				if (!string.Equals(envelope.Sha256Signature, actualSignature, StringComparison.OrdinalIgnoreCase))
				{
					GD.PrintErr($"[SaveManager] 存档签名不匹配！文件疑似损坏或遭外部篡改: {filePath}");
					return false;
				}

				payload = envelope.Payload;
				return true;
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[SaveManager] 反序列化存档失败: {filePath} | {ex.Message}");
				return false;
			}
		}

		private static string ComputeSha256(string rawData)
		{
			using var sha256 = SHA256.Create();
			byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
			var sb = new StringBuilder();
			foreach (var b in bytes)
			{
				sb.Append(b.ToString("x2"));
			}
			return sb.ToString();
		}
	}
}
