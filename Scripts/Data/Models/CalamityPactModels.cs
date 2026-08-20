using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models
{
	/// <summary>
	/// 灾厄契约数据定义模型 (严格映射 pact_core_rules.json)
	/// </summary>
	public class CalamityPactDef
	{
		[JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
		[JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
		[JsonPropertyName("penalty")] public string Penalty { get; set; } = "ThermalOverload";
		[JsonPropertyName("reward")] public string Reward { get; set; } = "HyperPulse";
		[JsonPropertyName("scrapBonusMultiplier")] public float ScrapBonusMultiplier { get; set; } = 0.5f;
		[JsonPropertyName("themeColor")] public string ThemeColorHex { get; set; } = "#DC143C";
		[JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
	}

	/// <summary>
	/// 灾厄契约 JSON 文件包装 DTO
	/// </summary>
	public class CalamityPactsFileDef
	{
		[JsonPropertyName("pacts")] public List<CalamityPactDef> Pacts { get; set; } = new();
	}
}
