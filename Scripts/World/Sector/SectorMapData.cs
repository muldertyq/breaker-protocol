using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.World.Sector
{
	public enum SectorNodeType
	{
		Combat,  // 常规遭遇战 (标准废料与构件掉落)
		Elite,   // 精英悬赏战 (高风险高收益，必掉紫色稀有构件)
		Event,   // 深空异象事件 (随机抉择，概率升级或负面创伤)
		Market,  // 废土黑市站 (构件图纸交易与改装)
		Repair,  // 紧急维护坞 (战舰修补与爆甲重置)
		Boss     // 星区决战 (迎战移动要塞「泰坦熔炉」)
	}

	public enum NodeExplorationState
	{
		Unreachable, // 尚未连通
		Reachable,   // 可跃迁 (青绿高亮呼吸)
		Current,     // 舰队当前驻泊点 (金色)
		Visited,     // 已探索完成 (灰色已过)
		Overrun      // 追击前线已沦陷 (深红高危)
	}

	/// <summary>
	/// 单个星区航路节点数据模型
	/// </summary>
	public class SectorNode
	{
		public string Id { get; set; } = string.Empty;
		public int Column { get; set; }
		public int Row { get; set; }
		public SectorNodeType Type { get; set; }
		public Vector2 NormalizedPosition { get; set; }
		public List<string> OutgoingConnections { get; } = new();
		public NodeExplorationState State { get; set; } = NodeExplorationState.Unreachable;

		// 战术情报数据
		public string FactionAffiliation { get; set; } = "重工联合 (Heavy Foundry)";
		public string PotentialLoot { get; set; } = "工业装甲板 / 加特林机炮";
		public string DangerLevel { get; set; } = "中度威胁 (Threat Level: II)";

		public string GetDisplayName()
		{
			return Type switch
			{
				SectorNodeType.Combat => "常规遭遇战",
				SectorNodeType.Elite  => "精英悬赏令",
				SectorNodeType.Event  => "深空异象群",
				SectorNodeType.Market => "废土改装黑市",
				SectorNodeType.Repair => "维保船坞",
				SectorNodeType.Boss   => "移动要塞决战",
				_ => "未知节点"
			};
		}

		public Color GetTypeColor()
		{
			return Type switch
			{
				SectorNodeType.Combat => new Color(0.95f, 0.45f, 0.35f),
				SectorNodeType.Elite  => new Color(0.95f, 0.20f, 0.45f),
				SectorNodeType.Event  => new Color(0.35f, 0.85f, 0.95f),
				SectorNodeType.Market => new Color(0.95f, 0.85f, 0.30f),
				SectorNodeType.Repair => new Color(0.35f, 0.90f, 0.50f),
				SectorNodeType.Boss   => new Color(1.0f, 0.20f, 0.20f),
				_ => Colors.White
			};
		}
	}

	/// <summary>
	/// 整个星区 DAG 图谱数据包
	/// </summary>
	public class SectorGraph
	{
		public int TotalColumns { get; set; } = 8;
		public List<List<SectorNode>> NodesByColumn { get; } = new();
		public Dictionary<string, SectorNode> AllNodes { get; } = new();
		public string? CurrentNodeId { get; set; }
		public float PursuitWavefrontColumn { get; set; } = -1.5f;
	}
}
