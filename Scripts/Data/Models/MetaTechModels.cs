using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models
{
	/// <summary>
	/// 单个母港科技节点定义 DTO
	/// </summary>
	public class TechNodeDef
	{
		[JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
		[JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
		[JsonPropertyName("category")] public string Category { get; set; } = "Economy";
		[JsonPropertyName("tier")] public int Tier { get; set; } = 1;
		[JsonPropertyName("coreCost")] public int CoreCost { get; set; } = 1;
		[JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
		[JsonPropertyName("prerequisites")] public string[] Prerequisites { get; set; } = Array.Empty<string>();
		[JsonPropertyName("statModifier")] public string StatModifier { get; set; } = string.Empty;
		[JsonPropertyName("modifierValue")] public float ModifierValue { get; set; } = 0.0f;
	}

	/// <summary>
	/// 科技树 JSON 文件包装 DTO
	/// </summary>
	public class TechTreeFileDef
	{
		[JsonPropertyName("techNodes")] public List<TechNodeDef> TechNodes { get; set; } = new();
	}
}
