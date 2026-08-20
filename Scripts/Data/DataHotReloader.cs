using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Godot;

namespace BreakerProtocol.Data
{
	/// <summary>
	/// 数据运行时热重载监听器
	/// 自动监听 core_data/ 与 mods/ 目录下的 JSON 变动，支持防抖、文件锁重试与主线程事件分发
	/// </summary>
	public class DataHotReloader : IDisposable
	{
		// 监听的文件系统观察者列表
		private readonly List<FileSystemWatcher> _watchers = new();

		// 待处理的文件变动队列（记录绝对路径与触发时间戳，用于防抖）
		private readonly ConcurrentDictionary<string, double> _pendingChanges = new();

		// 防抖延迟时间（秒）：保存后等待 0.2 秒再执行读取，避免多重触发与半写入状态
		private const double DebounceDelaySeconds = 0.2;

		// 当检测到合法 JSON 文件发生修改并准备重载时的回调：(文件绝对路径)
		public event Action<string>? OnFileReloadRequested;

		/// <summary>
		/// 初始化并启动监听指定目录列表
		/// </summary>
		public void StartWatching(params string[] directoryPaths)
		{
			StopWatching();

			foreach (var dir in directoryPaths)
			{
				if (!Directory.Exists(dir)) continue;

				try
				{
					var watcher = new FileSystemWatcher(dir)
					{
						Filter = "*.json",
						IncludeSubdirectories = true,
						NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
					};

					watcher.Changed += OnFileSystemEvent;
					watcher.Created += OnFileSystemEvent;
					watcher.EnableRaisingEvents = true;

					_watchers.Add(watcher);
					GD.Print($"[DataHotReloader] 已启动对目录的热重载监听: {dir}");
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[DataHotReloader] 监听目录 [{dir}] 失败: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// 接收到文件系统原始变动事件 (运行在后台系统线程)
		/// </summary>
		private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
		{
			if (e.ChangeType is WatcherChangeTypes.Changed or WatcherChangeTypes.Created)
			{
				// 记录最新触发时间戳
				double now = Time.GetTicksMsec() / 1000.0;
				_pendingChanges[e.FullPath] = now;
			}
		}

		/// <summary>
		/// 轮询处理待热更队列（必须在 Godot 主线程的 _Process 中每帧调用）
		/// </summary>
		public void Poll()
		{
			if (_pendingChanges.IsEmpty) return;

			double now = Time.GetTicksMsec() / 1000.0;

			foreach (var kvp in _pendingChanges)
			{
				string filePath = kvp.Key;
				double triggerTime = kvp.Value;

				// 超过防抖间隔，可以安全读取
				if (now - triggerTime >= DebounceDelaySeconds)
				{
					_pendingChanges.TryRemove(filePath, out _);

					// 异步小幅重试读取，防止文本编辑器占用文件锁
					ExecuteSafeReload(filePath);
				}
			}
		}

		/// <summary>
		/// 安全读取文件并触发重载事件（带文件锁重试）
		/// </summary>
		private void ExecuteSafeReload(string filePath)
		{
			if (!File.Exists(filePath)) return;

			// 最多尝试 3 次，每次间隔 50ms
			for (int i = 0; i < 3; i++)
			{
				try
				{
					// 显式指定 System.IO.FileAccess 与 System.IO.FileMode，消除与 Godot.FileAccess 的歧义
					using var stream = File.Open(
						filePath, 
						System.IO.FileMode.Open, 
						System.IO.FileAccess.Read, 
						System.IO.FileShare.ReadWrite
					);
					
					// 文件就绪，触发重载
					GD.PrintRich($"[color=yellow][DataHotReloader] 检测到数据文件变更: {Path.GetFileName(filePath)}，正在触发热更新...[/color]");
					OnFileReloadRequested?.Invoke(filePath);
					return;
				}
				catch (IOException)
				{
					// 文件被占用，等待 50ms 后重试
					Thread.Sleep(50);
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[DataHotReloader] 读取文件 [{filePath}] 发生未知错误: {ex.Message}");
					return;
				}
			}

			GD.PrintErr($"[DataHotReloader] 文件 [{filePath}] 被外部编辑器持续锁定，跳过本次热更。");
		}

		public void StopWatching()
		{
			foreach (var w in _watchers)
			{
				w.EnableRaisingEvents = false;
				w.Dispose();
			}
			_watchers.Clear();
			_pendingChanges.Clear();
		}

		public void Dispose()
		{
			StopWatching();
		}
	}
}
