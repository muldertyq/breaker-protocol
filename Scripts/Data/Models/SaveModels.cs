using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models
{
	// =========================================================================
	// 1. 安全存档外层包装信封 (含 SHA-256 防篡改签名)
	// =========================================================================
	public class SaveEnvelope<T>
	{
		[JsonPropertyName("version")] public string Version { get; set; } = "1.0.0";
		[JsonPropertyName("timestamp")] public string Timestamp { get; set; } = string.Empty;
		[JsonPropertyName("sha256Signature")] public string Sha256Signature { get; set; } = string.Empty;
		[JsonPropertyName("payload")] public T? Payload { get; set; }
	}

	// =========================================================================
	// 2. 母港永久 Meta 科技树与生涯存档数据
	// =========================================================================
	public class MetaSaveData
	{
		[JsonPropertyName("dataFragments")] public int DataFragments { get; set; } = 0;
		[JsonPropertyName("unlockedTechIds")] public List<string> UnlockedTechIds { get; set; } = new();
		[JsonPropertyName("totalRunsCount")] public int TotalRunsCount { get; set; } = 0;
		[JsonPropertyName("victoriesCount")] public int VictoriesCount { get; set; } = 0;
		[JsonPropertyName("highestScore")] public int HighestScore { get; set; } = 0;
		[JsonPropertyName("unlockedBlueprints")] public List<string> UnlockedBlueprints { get; set; } = new();
	}

	// =========================================================================
	// 3. 单局战役实时战况快照 (支持断线恢复与中途暂存)
	// =========================================================================

	/// <summary>
	/// 单个构件实时战损状态
	/// </summary>
	public class SavedModuleState
	{
		[JsonPropertyName("moduleId")] public string ModuleId { get; set; } = string.Empty;
		[JsonPropertyName("gridX")] public int GridX { get; set; }
		[JsonPropertyName("gridY")] public int GridY { get; set; }
		[JsonPropertyName("rotation")] public int Rotation { get; set; }
		[JsonPropertyName("currentHp")] public float CurrentHp { get; set; }
		[JsonPropertyName("maxHp")] public float MaxHp { get; set; }
		[JsonPropertyName("isDestroyed")] public bool IsDestroyed { get; set; }
	}

	/// <summary>
	/// 单条 PCB 导线连接状态
	/// </summary>
	public class SavedWireState
	{
		[JsonPropertyName("sourceGridX")] public int SourceGridX { get; set; }
		[JsonPropertyName("sourceGridY")] public int SourceGridY { get; set; }
		[JsonPropertyName("targetGridX")] public int TargetGridX { get; set; }
		[JsonPropertyName("targetGridY")] public int TargetGridY { get; set; }
	}

	/// <summary>
	/// 战舰全量物理结构与走线快照
	/// </summary>
	public class SavedShipState
	{
		[JsonPropertyName("baseBlueprintId")] public string BaseBlueprintId { get; set; } = "bp_hf_m_anvil";
		[JsonPropertyName("modules")] public List<SavedModuleState> Modules { get; set; } = new();
		[JsonPropertyName("wires")] public List<SavedWireState> Wires { get; set; } = new();
	}

	/// <summary>
	/// 星图单个航路节点状态
	/// </summary>
	public class SavedNodeState
	{
		[JsonPropertyName("nodeId")] public string NodeId { get; set; } = string.Empty;
		[JsonPropertyName("column")] public int Column { get; set; }
		[JsonPropertyName("row")] public int Row { get; set; }
		[JsonPropertyName("type")] public string Type { get; set; } = "Combat";
		[JsonPropertyName("state")] public string State { get; set; } = "Unreachable";
		[JsonPropertyName("normX")] public float NormX { get; set; }
		[JsonPropertyName("normY")] public float NormY { get; set; }
		[JsonPropertyName("outgoingConnections")] public List<string> OutgoingConnections { get; set; } = new();
	}

	/// <summary>
	/// 星区 DAG 拓扑全景快照
	/// </summary>
	public class SavedSectorState
	{
		[JsonPropertyName("totalColumns")] public int TotalColumns { get; set; } = 8;
		[JsonPropertyName("currentNodeId")] public string? CurrentNodeId { get; set; }
		[JsonPropertyName("pursuitWavefrontColumn")] public float PursuitWavefrontColumn { get; set; } = -1.5f;
		[JsonPropertyName("nodes")] public List<SavedNodeState> Nodes { get; set; } = new();
	}

	/// <summary>
	/// 单局战役完整现场数据包 (user://current_run.json)
	/// </summary>
	public class RunSaveData
	{
		[JsonPropertyName("runSeed")] public int RunSeed { get; set; }
		[JsonPropertyName("currentScraps")] public int CurrentScraps { get; set; } = 0;
		[JsonPropertyName("currentCores")] public int CurrentCores { get; set; } = 0;
		[JsonPropertyName("activePactIds")] public List<string> ActivePactIds { get; set; } = new();
		[JsonPropertyName("ship")] public SavedShipState Ship { get; set; } = new();
		[JsonPropertyName("sector")] public SavedSectorState Sector { get; set; } = new();
	}
}
