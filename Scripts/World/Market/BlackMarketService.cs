using System.Collections.Generic;
using System.Linq;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.World.Market
{
	/// <summary>
	/// 单个在售黑市商品条目
	/// </summary>
	public class MarketItem
	{
		public string ItemId { get; set; } = string.Empty;
		public ModuleDataDefinition Definition { get; set; } = null!;
		public int BuyPrice { get; set; }
		public bool IsSoldOut { get; set; } = false;
	}

	/// <summary>
	/// 黑市货架生成器与定价服务
	/// </summary>
	public static class BlackMarketService
	{
		/// <summary>
		/// 随机生成 4~6 个在售构件
		/// </summary>
		public static List<MarketItem> GenerateMarketStock(int count = 5)
		{
			var result = new List<MarketItem>();
			var allModules = DataManager.Instance.Modules.GetAll().ToList();

			if (allModules.Count == 0) return result;

			// 洗牌算法随机挑选
			var shuffled = allModules.OrderBy(_ => GD.Randf()).Take(count);

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

		/// <summary>
		/// 根据构件类型、质量与基础血量计算买入基准价格
		/// </summary>
		public static int CalculateBuyPrice(ModuleDataDefinition def)
		{
			int basePrice = def.Category switch
			{
				"Weapon"      => 140,
				"Thruster"    => 90,
				"PowerSource" => 180,
				"Modifier"    => 120,
				"Logic"       => 80,
				"Armor"       => 60,
				_             => 50
			};

			// 根据体量与耐久加权
			float hpFactor = Mathf.Clamp(def.BaseHp / 200.0f, 0.8f, 2.5f);
			return (int)(basePrice * hpFactor);
		}

		/// <summary>
		/// 计算回收/拆解折旧价格 (原价 60%)
		/// </summary>
		public static int CalculateSellPrice(ModuleDataDefinition def, float currentHpRatio = 1.0f)
		{
			int fullBuyPrice = CalculateBuyPrice(def);
			float scrapRate = 0.60f; // 60% 折旧率
			return Mathf.Max(15, (int)(fullBuyPrice * scrapRate * Mathf.Clamp(currentHpRatio, 0.3f, 1.0f)));
		}
	}
}
