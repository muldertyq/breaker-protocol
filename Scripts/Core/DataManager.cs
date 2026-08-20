using System;
using System.IO;
using System.Text.Json;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Registries;
using BreakerProtocol.Data.Validation;

namespace BreakerProtocol.Core
{
	/// <summary>
	/// 全局数据注册与配置驱动总管中枢 (单例模式)
	/// 统一管理构件、蓝图、异象、科技树、契约与黑市规则
	/// </summary>
	public class DataManager
	{
		private static DataManager? _instance;
		public static DataManager Instance => _instance ??= new DataManager();

		// 内循环注册表
		public Registry<ModuleDataDefinition> Modules { get; } = new("Modules");
		public Registry<BlueprintDataDefinition> Blueprints { get; } = new("Blueprints");

		// 外循环注册表
		public Registry<SpaceEventNode> Events { get; } = new("Events");
		public Registry<TechNodeDef> Techs { get; } = new("Techs");
		public Registry<CalamityPactDef> Pacts { get; } = new("Pacts");
		public MarketConfigDef MarketConfig { get; private set; } = new();

		// 唯一的全局数据重载广播事件
		public event Action? OnDataReloaded;

		private DataManager()
		{
			LoadAllData();
		}

		/// <summary>
		/// 扫描 core_data 与 mods 目录并统一装载
		/// </summary>
		public void LoadAllData()
		{
			GD.Print("[DataManager] 开始全域数据驱动扫描加载...");

			Modules.Clear();
			Blueprints.Clear();
			Events.Clear();
			Techs.Clear();
			Pacts.Clear();

			string baseDir = ProjectSettings.GlobalizePath("res://");
			string coreDataDir = Path.Combine(baseDir, "core_data");

			// 1. 加载构件与蓝图
			LoadModulesAndBlueprints(coreDataDir);

			// 2. 加载深空异象事件 (core_data/events/)
			LoadEvents(Path.Combine(coreDataDir, "events"));

			// 3. 加载母港科技树 (core_data/techs/)
			LoadTechs(Path.Combine(coreDataDir, "techs"));

			// 4. 加载灾厄契约 (core_data/pacts/)
			LoadPacts(Path.Combine(coreDataDir, "pacts"));

			// 5. 加载黑市经济配置 (core_data/markets/)
			LoadMarketConfig(Path.Combine(coreDataDir, "markets"));

			// 6. 执行全域数据校验防爆
			DataValidator.ValidateAll(this);

			GD.Print($"[DataManager] 全域数据装载完成: {Modules.Count} 构件 | {Blueprints.Count} 蓝图 | {Events.Count} 异象 | {Techs.Count} 科技 | {Pacts.Count} 契约");
			OnDataReloaded?.Invoke();
		}

		private void LoadModulesAndBlueprints(string rootDir)
		{
			if (!Directory.Exists(rootDir)) return;

			foreach (var file in Directory.EnumerateFiles(rootDir, "*.json", SearchOption.AllDirectories))
			{
				try
				{
					string text = File.ReadAllText(file);
					if (file.Contains("blueprints"))
					{
						var bp = JsonSerializer.Deserialize<BlueprintDataDefinition>(text);
						if (bp != null && !string.IsNullOrEmpty(bp.Id)) Blueprints.Register(bp.Id, bp);
					}
					else if (file.Contains("modules"))
					{
						var mod = JsonSerializer.Deserialize<ModuleDataDefinition>(text);
						if (mod != null && !string.IsNullOrEmpty(mod.Id)) Modules.Register(mod.Id, mod);
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[DataManager] 解析文件失败: {file} | 错误: {ex.Message}");
				}
			}
		}

		private void LoadEvents(string eventsDir)
		{
			if (!Directory.Exists(eventsDir)) return;

			foreach (var file in Directory.EnumerateFiles(eventsDir, "*.json", SearchOption.AllDirectories))
			{
				try
				{
					string text = File.ReadAllText(file);
					var ev = JsonSerializer.Deserialize<SpaceEventNode>(text);
					if (ev != null && !string.IsNullOrEmpty(ev.Id))
					{
						Events.Register(ev.Id, ev);
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[DataManager] 解析异象事件失败: {file} | {ex.Message}");
				}
			}
		}

		private void LoadTechs(string techsDir)
		{
			if (!Directory.Exists(techsDir)) return;

			foreach (var file in Directory.EnumerateFiles(techsDir, "*.json", SearchOption.AllDirectories))
			{
				try
				{
					string text = File.ReadAllText(file);
					var tree = JsonSerializer.Deserialize<TechTreeFileDef>(text);
					if (tree?.TechNodes != null)
					{
						foreach (var node in tree.TechNodes)
						{
							if (!string.IsNullOrEmpty(node.Id)) Techs.Register(node.Id, node);
						}
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[DataManager] 解析科技树失败: {file} | {ex.Message}");
				}
			}
		}

		private void LoadPacts(string pactsDir)
		{
			if (!Directory.Exists(pactsDir)) return;

			foreach (var file in Directory.EnumerateFiles(pactsDir, "*.json", SearchOption.AllDirectories))
			{
				try
				{
					string text = File.ReadAllText(file);
					var pack = JsonSerializer.Deserialize<CalamityPactsFileDef>(text);
					if (pack?.Pacts != null)
					{
						foreach (var pact in pack.Pacts)
						{
							if (!string.IsNullOrEmpty(pact.Id)) Pacts.Register(pact.Id, pact);
						}
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[DataManager] 解析灾厄契约失败: {file} | {ex.Message}");
				}
			}
		}

		private void LoadMarketConfig(string marketDir)
		{
			if (!Directory.Exists(marketDir)) return;

			string configPath = Path.Combine(marketDir, "market_config.json");
			if (File.Exists(configPath))
			{
				try
				{
					string text = File.ReadAllText(configPath);
					var cfg = JsonSerializer.Deserialize<MarketConfigDef>(text);
					if (cfg != null) MarketConfig = cfg;
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[DataManager] 解析黑市配置失败: {configPath} | {ex.Message}");
				}
			}
		}
	}
}
