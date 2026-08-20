using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models
{
	/// <summary>
	/// 引脚方向类型
	/// </summary>
	public enum PinType
	{
		IN,
		OUT
	}

	/// <summary>
	/// 引脚数据结构定义
	/// </summary>
	public class PinDefinition
	{
		[JsonPropertyName("pinId")] public string PinId { get; set; } = string.Empty;
		[JsonPropertyName("type")] public string Type { get; set; } = "IN"; // "IN" 或 "OUT"
		[JsonPropertyName("localGridX")] public int LocalGridX { get; set; } = 0;
		[JsonPropertyName("localGridY")] public int LocalGridY { get; set; } = 0;
		[JsonPropertyName("category")] public string Category { get; set; } = "Standard";
	}

	/// <summary>
	/// 单个构件完整定义模型 (对应 core_data/modules/*.json)
	/// </summary>
	public class ModuleDataDefinition
	{
		[JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
		[JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
		[JsonPropertyName("faction")] public string Faction { get; set; } = "Universal"; // HeavyFoundry / VoidSyndicate / BioChitin / Universal
		[JsonPropertyName("category")] public string Category { get; set; } = "Weapon"; // PowerSource / Weapon / Modifier / Armor / Thruster / Logistics
		
		[JsonPropertyName("width")] public int Width { get; set; } = 1;
		[JsonPropertyName("height")] public int Height { get; set; } = 1;
		[JsonPropertyName("mass")] public float Mass { get; set; } = 5.0f; // 吨
		[JsonPropertyName("baseHp")] public float BaseHp { get; set; } = 200.0f;
		[JsonPropertyName("armorResistance")] public float ArmorResistance { get; set; } = 10.0f;
		
		[JsonPropertyName("spritePath")] public string SpritePath { get; set; } = string.Empty;
		[JsonPropertyName("tags")] public string[] Tags { get; set; } = Array.Empty<string>();
		[JsonPropertyName("pins")] public PinDefinition[] Pins { get; set; } = Array.Empty<PinDefinition>();
		
		// 动态附加的武器与效果参数（存储为原始 JsonElement，交由 EffectProcessor 解析）
		[JsonPropertyName("properties")] public JsonElement Properties { get; set; }
	}
}
