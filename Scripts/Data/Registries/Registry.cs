using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.Data.Registries
{
	/// <summary>
	/// 泛型全局数据注册表
	/// 负责管理所有通过 JSON 反序列化进来的数据实体（如 ModuleDef, BlueprintDef 等）
	/// </summary>
	/// <typeparam name="T">注册的数据类型</typeparam>
	public class Registry<T> where T : class
	{
		// 注册表名称（用于日志打印与调试）
		public string RegistryName { get; }

		// 底层使用线程安全字典存储：Key 为全局唯一 string ID，Value 为实体对象
		private readonly ConcurrentDictionary<string, T> _storage = new();

		// 当注册表发生内容注册/覆写时的事件回调
		public event Action<string, T>? OnItemRegistered;

		public Registry(string registryName)
		{
			RegistryName = registryName;
		}

		/// <summary>
		/// 注册或覆盖一个数据实体
		/// </summary>
		/// <param name="id">全局唯一 ID</param>
		/// <param name="item">实体对象</param>
		/// <param name="allowOverwrite">是否允许覆盖已存在的 ID</param>
		/// <returns>是否成功注册</returns>
		public bool Register(string id, T item, bool allowOverwrite = true)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				GD.PrintErr($"[Registry:{RegistryName}] 注册失败：ID 不能为空！");
				return false;
			}

			if (item == null)
			{
				GD.PrintErr($"[Registry:{RegistryName}] 注册失败：ID [{id}] 对应的对象为 null！");
				return false;
			}

			if (_storage.ContainsKey(id))
			{
				if (!allowOverwrite)
				{
					GD.PrintErr($"[Registry:{RegistryName}] 注册冲突：ID [{id}] 已存在且不允许覆盖！");
					return false;
				}
				
				_storage[id] = item;
				GD.PrintRich($"[color=yellow][Registry:{RegistryName}] 数据覆写：[{id}][/color]");
			}
			else
			{
				_storage.TryAdd(id, item);
				GD.Print($"[Registry:{RegistryName}] 注册成功：[{id}]");
			}

			OnItemRegistered?.Invoke(id, item);
			return true;
		}

		/// <summary>
		/// 根据 ID 获取实体（若不存在则抛出异常）
		/// </summary>
		public T Get(string id)
		{
			if (_storage.TryGetValue(id, out var item))
			{
				return item;
			}

			throw new KeyNotFoundException($"[Registry:{RegistryName}] 未找到 ID 为 [{id}] 的数据项！");
		}

		/// <summary>
		/// 尝试根据 ID 获取实体
		/// </summary>
		public bool TryGet(string id, out T? item)
		{
			return _storage.TryGetValue(id, out item);
		}

		/// <summary>
		/// 检查是否包含指定 ID
		/// </summary>
		public bool Contains(string id) => _storage.ContainsKey(id);

		/// <summary>
		/// 获取所有已注册实体的只读枚举集合 (修复 CS0266 错误，支持直接 foreach 遍历且无额外内存开销)
		/// </summary>
		public IEnumerable<T> GetAll() => _storage.Values;

		/// <summary>
		/// 获取当前注册项总数
		/// </summary>
		public int Count => _storage.Count;

		/// <summary>
		/// 清空注册表（用于热重载或重新初始化）
		/// </summary>
		public void Clear()
		{
			_storage.Clear();
			GD.Print($"[Registry:{RegistryName}] 注册表已清空。");
		}
	}
}
