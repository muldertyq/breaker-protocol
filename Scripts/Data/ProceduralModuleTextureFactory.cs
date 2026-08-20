using Godot;
using BreakerProtocol.Core;

namespace BreakerProtocol.Data
{
	/// <summary>
	/// 程序化自闭合灰度构件纹理工厂
	/// 自动生成带机械倒角、主装甲区、战术条纹与焊缝高光的标准灰度贴图 (0~255)
	/// </summary>
	public static class ProceduralModuleTextureFactory
	{
		private static readonly System.Collections.Generic.Dictionary<string, ImageTexture> _textureCache = new();

		public static ImageTexture GetOrCreateModuleTexture(string category, int widthGu, int heightGu)
		{
			string key = $"{category}_{widthGu}x{heightGu}";
			if (_textureCache.TryGetValue(key, out var cached))
			{
				return cached;
			}

			int widthPx = (int)(widthGu * GlobalMetrics.PixelsPerMeter);
			int heightPx = (int)(heightGu * GlobalMetrics.PixelsPerMeter);

			var image = Image.CreateEmpty(widthPx, heightPx, false, Image.Format.Rgba8);

			// 基础灰度常数 (规范 07)
			Color seamDark = new(0.12f, 0.12f, 0.12f, 1.0f);     // 灰度 30 (阴影/焊缝)
			Color armorGray = new(0.42f, 0.42f, 0.42f, 1.0f);    // 灰度 110 (主装甲区)
			Color stripeLight = new(0.75f, 0.75f, 0.75f, 1.0f);  // 灰度 190 (副战术条纹)
			Color highlight = new(0.95f, 0.95f, 0.95f, 1.0f);    // 灰度 245 (金属高光)

			for (int x = 0; x < widthPx; x++)
			{
				for (int y = 0; y < heightPx; y++)
				{
					// 1. 四角 45° 倒角切除
					bool isCorner = (x == 0 && y == 0) || (x == widthPx - 1 && y == 0) ||
								   (x == 0 && y == heightPx - 1) || (x == widthPx - 1 && y == heightPx - 1);
					if (isCorner)
					{
						image.SetPixel(x, y, new Color(0, 0, 0, 0)); // 透明
						continue;
					}

					// 2. 自闭合边缘处理 (左上高光，右下深色焊缝)
					bool isTopLeftEdge = (x <= 1 || y <= 1);
					bool isBottomRightEdge = (x >= widthPx - 2 || y >= heightPx - 2);

					if (isTopLeftEdge)
					{
						image.SetPixel(x, y, highlight);
					}
					else if (isBottomRightEdge)
					{
						image.SetPixel(x, y, seamDark);
					}
					else
					{
						// 3. 内部图案：根据类别生成专属战术斜纹与机械线条
						if (category == "Weapon")
						{
							// 武器：中央带有炮管战术条纹
							bool isStripe = (x >= widthPx / 2 - 2 && x <= widthPx / 2 + 1) || (y % 6 == 0);
							image.SetPixel(x, y, isStripe ? stripeLight : armorGray);
						}
						else if (category == "PowerSource")
						{
							// 动力源：中央核心发光圆环区
							float cx = widthPx * 0.5f;
							float cy = heightPx * 0.5f;
							float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
							bool isRing = Mathf.Abs(dist - (widthPx * 0.25f)) < 2.0f;
							image.SetPixel(x, y, isRing ? stripeLight : armorGray);
						}
						else if (category == "Modifier" || category == "Logic")
						{
							// 修饰舱/逻辑：双斜切条纹
							bool isStripe = ((x + y) % 8 == 0 || (x + y) % 8 == 1);
							image.SetPixel(x, y, isStripe ? stripeLight : armorGray);
						}
						else // Armor 或其他
						{
							// 纯重装甲：规整的加固铆钉与装甲大板
							bool isRivet = (x % 8 == 2 && y % 8 == 2);
							image.SetPixel(x, y, isRivet ? highlight : armorGray);
						}
					}
				}
			}

			var texture = ImageTexture.CreateFromImage(image);
			_textureCache[key] = texture;
			return texture;
		}
	}
}
