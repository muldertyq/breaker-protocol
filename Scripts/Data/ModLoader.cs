using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Registries;
using BreakerProtocol.Data.Validation;

namespace BreakerProtocol.Data
{
	public class ModLoader
	{
		public Registry<ModuleDataDefinition> ModuleRegistry { get; } = new("Modules");
		public Registry<BlueprintDataDefinition> BlueprintRegistry { get; } = new("Blueprints");
		public Dictionary<string, ModManifest> LoadedMods { get; } = new();

		private readonly Dictionary<string, string> _filePathToModuleId = new();

		public static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true
		};

		public void LoadAllMods()
		{
			ModuleRegistry.Clear();
			BlueprintRegistry.Clear();
			LoadedMods.Clear();
			_filePathToModuleId.Clear();

			GD.PrintRich("[color=cyan]================ [BreakerProtocol ModLoader 全量加载] ================[/color]");

			string rootPath = OS.HasFeature("editor") 
				? ProjectSettings.GlobalizePath("res://") 
				: OS.GetExecutablePath().GetBaseDir();

			string coreDataPath = Path.Combine(rootPath, "core_data");
			string modsRootPath = Path.Combine(rootPath, "mods");

			List<Tuple<ModManifest, string>> modPacks = new();

			if (Directory.Exists(coreDataPath))
			{
				var coreManifest = LoadManifestFromDirectory(coreDataPath);
				if (coreManifest != null)
				{
					coreManifest.Priority = 0;
					modPacks.Add(new Tuple<ModManifest, string>(coreManifest, coreDataPath));
				}
			}

			if (Directory.Exists(modsRootPath))
			{
				foreach (string subDir in Directory.GetDirectories(modsRootPath))
				{
					var modManifest = LoadManifestFromDirectory(subDir);
					if (modManifest != null && modManifest.Enabled)
					{
						modPacks.Add(new Tuple<ModManifest, string>(modManifest, subDir));
					}
				}
			}

			var sortedMods = modPacks.OrderBy(m => m.Item1.Priority).ToList();

			foreach (var (manifest, dirPath) in sortedMods)
			{
				GD.PrintRich($"[color=green]>>> 正在加载: [{manifest.Name}] (v{manifest.Version}) 来自: {dirPath}[/color]");
				LoadedMods[manifest.Id] = manifest;

				LoadModulesFromMod(dirPath);
				LoadBlueprintsFromMod(dirPath);
			}

			GD.PrintRich($"[color=cyan]================ [加载完成: {LoadedMods.Count} Mod | {ModuleRegistry.Count} 构件 | {BlueprintRegistry.Count} 蓝图] ================[/color]");
		}

		public bool ReloadSingleFile(string filePath)
		{
			string fileName = Path.GetFileName(filePath);

			if (fileName.Equals("mod_manifest.json", StringComparison.OrdinalIgnoreCase))
			{
				LoadAllMods();
				return true;
			}

			if (filePath.Contains("modules", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					string json = File.ReadAllText(filePath);
					var moduleDef = JsonSerializer.Deserialize<ModuleDataDefinition>(json, JsonOptions);
					if (moduleDef != null && DataValidator.ValidateModule(moduleDef, filePath, out _))
					{
						ModuleRegistry.Register(moduleDef.Id, moduleDef, allowOverwrite: true);
						_filePathToModuleId[filePath] = moduleDef.Id;
						return true;
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[ModLoader] 构件热更新失败: {ex.Message}");
				}
			}
			else if (filePath.Contains("blueprints", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					string json = File.ReadAllText(filePath);
					var bpDef = JsonSerializer.Deserialize<BlueprintDataDefinition>(json, JsonOptions);
					if (bpDef != null && !string.IsNullOrWhiteSpace(bpDef.Id))
					{
						BlueprintRegistry.Register(bpDef.Id, bpDef, allowOverwrite: true);
						return true;
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[ModLoader] 蓝图热更新失败: {ex.Message}");
				}
			}

			return false;
		}

		private ModManifest? LoadManifestFromDirectory(string dirPath)
		{
			string manifestPath = Path.Combine(dirPath, "mod_manifest.json");
			if (!File.Exists(manifestPath)) return null;

			try
			{
				string jsonContent = File.ReadAllText(manifestPath);
				return JsonSerializer.Deserialize<ModManifest>(jsonContent, JsonOptions);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ModLoader] 解析清单 [{manifestPath}] 失败：{ex.Message}");
				return null;
			}
		}

		private void LoadModulesFromMod(string modDirPath)
		{
			string modulesPath = Path.Combine(modDirPath, "modules");
			if (!Directory.Exists(modulesPath)) return;

			string[] moduleFiles = Directory.GetFiles(modulesPath, "*.json", SearchOption.AllDirectories);
			foreach (string filePath in moduleFiles)
			{
				try
				{
					string jsonContent = File.ReadAllText(filePath);
					var moduleDef = JsonSerializer.Deserialize<ModuleDataDefinition>(jsonContent, JsonOptions);
					if (moduleDef != null && DataValidator.ValidateModule(moduleDef, filePath, out _))
					{
						ModuleRegistry.Register(moduleDef.Id, moduleDef, allowOverwrite: true);
						_filePathToModuleId[filePath] = moduleDef.Id;
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[ModLoader] 解析构件 [{filePath}] 失败：{ex.Message}");
				}
			}
		}

		private void LoadBlueprintsFromMod(string modDirPath)
		{
			string bpPath = Path.Combine(modDirPath, "blueprints");
			if (!Directory.Exists(bpPath)) return;

			string[] bpFiles = Directory.GetFiles(bpPath, "*.json", SearchOption.AllDirectories);
			foreach (string filePath in bpFiles)
			{
				try
				{
					string jsonContent = File.ReadAllText(filePath);
					var bpDef = JsonSerializer.Deserialize<BlueprintDataDefinition>(jsonContent, JsonOptions);
					if (bpDef != null && !string.IsNullOrWhiteSpace(bpDef.Id))
					{
						BlueprintRegistry.Register(bpDef.Id, bpDef, allowOverwrite: true);
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[ModLoader] 解析蓝图 [{filePath}] 失败：{ex.Message}");
				}
			}
		}
	}
}
