using System.Collections.Generic;
using System.Linq;
using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.World.Events
{
	/// <summary>
	/// 深空随机异象事件机总管 (完全由 JSON 数据驱动)
	/// </summary>
	public static class SpaceEventDatabase
	{
		/// <summary>
		/// 获取当前所有已加载的异象列表 (兼容早期 Task 属性访问)
		/// </summary>
		public static List<SpaceEventNode> Events => DataManager.Instance.Events.GetAll().ToList();

		public static bool TryGetEvent(string id, out SpaceEventNode? ev)
		{
			return DataManager.Instance.Events.TryGet(id, out ev);
		}

		public static SpaceEventNode GetRandomEvent()
		{
			var list = Events;
			if (list.Count == 0)
			{
				return new SpaceEventNode
				{
					Id = "ev_fallback",
					Title = "【虚空静默】",
					Description = "雷达未在当前区域扫描到任何有效特征信号。"
				};
			}
			return list[(int)GD.RandRange(0, list.Count - 1)];
		}
	}
}
