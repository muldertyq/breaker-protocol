using System.Collections.Generic;
using System.Linq;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.World.Market
{
	public class MarketItem
	{
		public string ItemId { get; set; } = string.Empty;
		public ModuleDataDefinition Definition { get; set; } = null!;
		public int BuyPrice { get; set; }
		public bool IsSoldOut { get; set; } = false;
	}

	/// <summary>
	/// 黑市定价与货架生成服务 (完全由 market_config.json 驱动)
	/// </summary>
	public static class BlackMarketService
	{
		public static List<MarketItem> GenerateMarketStock(int count = -1)
		{
			var result = new List<MarketItem>();
			var allModules = DataManager.Instance.Modules.GetAll().ToList();
			if (allModules.Count == 0) return result;

			int finalCount = count > 0 ? count : DataManager.Instance.MarketConfig.DefaultStockCount;
			var shuffled = allModules.OrderBy(_ => GD.Randf()).Take(finalCount);

			foreach (var mod in shuffled)
			{
				int price = CalculateBuyPrice(mod);
				result.Add(new MarketItem
				{
					ItemId = mod.Id,
					Definition = mod,
					BuyPrice = price,
					IsSoldOut = false
				});
			}
			return result;
		}

		public static int CalculateBuyPrice(ModuleDataDefinition def)
		{
			var cfg = DataManager.Instance.MarketConfig;
			int basePrice = 50;
			if (cfg.CategoryBasePrices.TryGetValue(def.Category, out int cfgPrice))
			{
				basePrice = cfgPrice;
			}

			float hpFactor = Mathf.Clamp(def.BaseHp / 200.0f, 0.8f, 2.5f);
			return (int)(basePrice * hpFactor);
		}

		public static int CalculateSellPrice(ModuleDataDefinition def, float currentHpRatio = 1.0f)
		{
			int fullBuyPrice = CalculateBuyPrice(def);
			float scrapRate = DataManager.Instance.MarketConfig.ScrapResellRate;
			return Mathf.Max(15, (int)(fullBuyPrice * scrapRate * Mathf.Clamp(currentHpRatio, 0.3f, 1.0f)));
		}
	}
}
