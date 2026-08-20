using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.Data.Models
{
	/// <summary>
	/// 飞船三通道调色板数据模型
	/// </summary>
	public class ShipPalette
	{
		public string PaletteId { get; set; } = "default";
		public string Name { get; set; } = "标准涂装";
		public string Faction { get; set; } = "HeavyFoundry";

		// 三大核心颜色通道 (规范 07)
		public Color PrimaryColor { get; set; } = new Color("#3A3F47");   // 主装甲色 (大板区)
		public Color SecondaryColor { get; set; } = new Color("#D87D24"); // 副战术色 (条纹/折角)
		public Color AccentColor { get; set; } = new Color("#50B5FF");    // 能量发光色 (发光/脉冲)
		public Color RawMetalColor { get; set; } = new Color("#5A5D64");  // 掉漆露出的底层生铁色

		public ShipPalette() { }

		public ShipPalette(string id, string name, string faction, Color primary, Color secondary, Color accent, Color rawMetal)
		{
			PaletteId = id;
			Name = name;
			Faction = faction;
			PrimaryColor = primary;
			SecondaryColor = secondary;
			AccentColor = accent;
			RawMetalColor = rawMetal;
		}
	}

	/// <summary>
	/// 全局官方预设色板库
	/// </summary>
	public static class FactionPalettes
	{
		// 1. 人类·重工联合体 (钛灰主色 + 工业工程橙 + 电弧蓝)
		public static readonly ShipPalette HeavyFoundry = new(
			"pal_heavy_foundry", "重工联合·工业钛金", "HeavyFoundry",
			new Color("#3A3F47"), new Color("#D87D24"), new Color("#50B5FF"), new Color("#484B52")
		);

		// 2. 高维·虚空财团 (虚空深紫 + 霓虹紫罗兰 + 冷青光)
		public static readonly ShipPalette VoidSyndicate = new(
			"pal_void_syndicate", "虚空财团·高维折跃", "VoidSyndicate",
			new Color("#1A1829"), new Color("#7F30FF"), new Color("#00FFCC"), new Color("#2A283A")
		);

		// 3. 异星·深空生化 (甲壳暗绿 + 血肉深红 + 毒液荧光)
		public static readonly ShipPalette BioChitin = new(
			"pal_bio_chitin", "深空生化·异星几丁", "BioChitin",
			new Color("#243320"), new Color("#8C2D19"), new Color("#99FF33"), new Color("#1A2418")
		);

		// 4. 赏金猎人·非法改装 (铁锈深红 + 斑马警示黄 + 火花炽红)
		public static readonly ShipPalette OutlawScrapper = new(
			"pal_outlaw_scrapper", "赏金猎人·废土铁锈", "Universal",
			new Color("#6B2D25"), new Color("#E3C75F"), new Color("#FF3B30"), new Color("#3A2A28")
		);

		public static readonly Dictionary<string, ShipPalette> All = new()
		{
			{ HeavyFoundry.PaletteId, HeavyFoundry },
			{ VoidSyndicate.PaletteId, VoidSyndicate },
			{ BioChitin.PaletteId, BioChitin },
			{ OutlawScrapper.PaletteId, OutlawScrapper }
		};
	}
}
