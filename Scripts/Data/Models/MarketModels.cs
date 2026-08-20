using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models
{
	/// <summary>
	/// 黑市经济规则配置 DTO (严格映射 core_data/markets/market_config.json)
	/// </summary>
	public class MarketConfigDef
	{
		[JsonPropertyName("defaultStockCount")] public int DefaultStockCount { get; set; } = 5;
		[JsonPropertyName("scrapResellRate")] public float ScrapResellRate { get; set; } = 0.60f;
		[JsonPropertyName("fieldRepairBaseCost")] public int FieldRepairBaseCost { get; set; } = 100;
		[JsonPropertyName("ablativeResetCost")] public int AblativeResetCost { get; set; } = 75;
		[JsonPropertyName("stockRerollCost")] public int StockRerollCost { get; set; } = 30;
		[JsonPropertyName("categoryBasePrices")] public Dictionary<string, int> CategoryBasePrices { get; set; } = new();
	}
}
