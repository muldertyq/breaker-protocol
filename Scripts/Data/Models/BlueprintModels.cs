using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models
{
	/// <summary>
	/// 蓝图中单个已放置构件的记录
	/// </summary>
	public class ModulePlacementRecord
	{
		[JsonPropertyName("moduleId")] public string ModuleId { get; set; } = string.Empty;
		[JsonPropertyName("gridX")] public int GridX { get; set; }
		[JsonPropertyName("gridY")] public int GridY { get; set; }
		[JsonPropertyName("rotation")] public int Rotation { get; set; }
	}

	/// <summary>
	/// 蓝图中单条预设 PCB 导线的记录
	/// </summary>
	public class WirePlacementRecord
	{
		[JsonPropertyName("sourceGridX")] public int SourceGridX { get; set; }
		[JsonPropertyName("sourceGridY")] public int SourceGridY { get; set; }
		[JsonPropertyName("targetGridX")] public int TargetGridX { get; set; }
		[JsonPropertyName("targetGridY")] public int TargetGridY { get; set; }
	}

	/// <summary>
	/// 完整预设战舰蓝图定义模型 (对应 core_data/blueprints/*.json)
	/// </summary>
	public class BlueprintDataDefinition
	{
		[JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
		[JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
		[JsonPropertyName("faction")] public string Faction { get; set; } = "HeavyFoundry"; // HeavyFoundry / VoidSyndicate / BioChitin
		[JsonPropertyName("hullClass")] public string HullClass { get; set; } = "M";       // S (轻型) / M (中型) / L (重型)
		[JsonPropertyName("description")] public string Description { get; set; } = string.Empty;

		[JsonPropertyName("modules")] public List<ModulePlacementRecord> Modules { get; set; } = new();
		[JsonPropertyName("wires")] public List<WirePlacementRecord> Wires { get; set; } = new();
	}
}
