using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models
{
	/// <summary>
	/// Mod 元信息清单数据模型
	/// 对应 mod_manifest.json 文件
	/// </summary>
	public class ModManifest
	{
		/// <summary>
		/// Mod 唯一标识符（如 "core_data", "laser_expansion_pack"）
		/// </summary>
		[JsonPropertyName("id")]
		public string Id { get; set; } = string.Empty;

		/// <summary>
		/// 显示名称（如 "《断路协议》官方核心数据包"）
		/// </summary>
		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;

		/// <summary>
		/// Mod 版本号（如 "1.0.0"）
		/// </summary>
		[JsonPropertyName("version")]
		public string Version { get; set; } = "1.0.0";

		/// <summary>
		/// 作者名称
		/// </summary>
		[JsonPropertyName("author")]
		public string Author { get; set; } = "Unknown";

		/// <summary>
		/// 简要描述信息
		/// </summary>
		[JsonPropertyName("description")]
		public string Description { get; set; } = string.Empty;

		/// <summary>
		/// 加载优先级：数字越小越先加载。
		/// 官方 core_data 固定为 0；普通 Mod 默认为 100。
		/// 后加载的 Mod 同名 ID 构件可覆盖先加载的构件（支持数据覆写与平衡性 Mod）。
		/// </summary>
		[JsonPropertyName("priority")]
		public int Priority { get; set; } = 100;

		/// <summary>
		/// 依赖的前置 Mod ID 列表
		/// </summary>
		[JsonPropertyName("dependencies")]
		public string[] Dependencies { get; set; } = System.Array.Empty<string>();

		/// <summary>
		/// 是否启用此 Mod
		/// </summary>
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; } = true;
	}
}
