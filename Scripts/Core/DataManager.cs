using System.IO;
using Godot;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Registries;

namespace BreakerProtocol.Core
{
	public partial class DataManager : Node
	{
		public static DataManager Instance { get; private set; } = null!;

		public ModLoader Loader { get; private set; } = null!;
		public DataHotReloader HotReloader { get; private set; } = null!;

		public Registry<ModuleDataDefinition> Modules => Loader.ModuleRegistry;
		public Registry<BlueprintDataDefinition> Blueprints => Loader.BlueprintRegistry;

		[Signal] public delegate void DataReloadedEventHandler();

		public override void _EnterTree()
		{
			Instance = this;
			Loader = new ModLoader();
			HotReloader = new DataHotReloader();

			Loader.LoadAllMods();
			HotReloader.OnFileReloadRequested += OnFileChanged;

			string rootPath = OS.HasFeature("editor") 
				? ProjectSettings.GlobalizePath("res://") 
				: OS.GetExecutablePath().GetBaseDir();

			HotReloader.StartWatching(
				Path.Combine(rootPath, "core_data"),
				Path.Combine(rootPath, "mods")
			);
		}

		public override void _Process(double delta)
		{
			HotReloader.Poll();
		}

		private void OnFileChanged(string fullPath)
		{
			bool success = Loader.ReloadSingleFile(fullPath);
			if (success)
			{
				EmitSignal(SignalName.DataReloaded);
			}
		}

		public override void _ExitTree()
		{
			HotReloader.Dispose();
		}
	}
}
