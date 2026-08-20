using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models
{
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum DropItemType
	{
		None,       // 空掉落
		Module,     // 构件物品
		Blueprint   // 蓝图图纸
	}

	public class DropEntryDef
	{
		[JsonPropertyName("moduleId")] public string ModuleId { get; set; } = string.Empty;
		[JsonPropertyName("weight")] public int Weight { get; set; } = 10;
		[JsonPropertyName("dropType")] public DropItemType DropType { get; set; } = DropItemType.Module;
	}

	public class DropTableDef
	{
		[JsonPropertyName("tableId")] public string TableId { get; set; } = string.Empty;
		[JsonPropertyName("displayName")] public string DisplayName { get; set; } = string.Empty;
		[JsonPropertyName("minScraps")] public int MinScraps { get; set; } = 10;
		[JsonPropertyName("maxScraps")] public int MaxScraps { get; set; } = 30;
		[JsonPropertyName("coreDropChance")] public float CoreDropChance { get; set; } = 0.0f;
		[JsonPropertyName("entries")] public List<DropEntryDef> Entries { get; set; } = new();
	}

	public class DropTableFileDef
	{
		[JsonPropertyName("dropTables")] public List<DropTableDef> DropTables { get; set; } = new();
	}
}
