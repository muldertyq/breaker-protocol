using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.World.Sector
{
	/// <summary>
	/// DAG 有向无环图分支星图生成器 (无交叉平面图算法)
	/// </summary>
	public static class SectorMapGenerator
	{
		public static SectorGraph GenerateSector(int totalColumns = 8)
		{
			var graph = new SectorGraph { TotalColumns = totalColumns };

			// 1. 生成各列节点
			for (int col = 0; col < totalColumns; col++)
			{
				var columnNodes = new List<SectorNode>();
				int nodeCount;

				if (col == 0) nodeCount = (int)GD.RandRange(2, 4);
				else if (col == totalColumns - 1) nodeCount = 1;
				else if (col == totalColumns - 2) nodeCount = (int)GD.RandRange(2, 3);
				else nodeCount = (int)GD.RandRange(2, 4);

				for (int row = 0; row < nodeCount; row++)
				{
					var node = new SectorNode
					{
						Id = $"Node_{col}_{row}",
						Column = col,
						Row = row,
						Type = RollNodeType(col, totalColumns)
					};

					// 填充节点情报卡片内容
					FillNodeIntelligence(node);

					float xNorm = (col + 0.5f) / totalColumns;
					float yStep = 1.0f / (nodeCount + 1);
					float jitterY = (float)GD.RandRange(-0.015, 0.015);
					node.NormalizedPosition = new Vector2(xNorm, (row + 1) * yStep + jitterY);

					columnNodes.Add(node);
					graph.AllNodes[node.Id] = node;
				}

				graph.NodesByColumn.Add(columnNodes);
			}

			// 2. 构建无交叉平面航道连线
			for (int col = 0; col < totalColumns - 1; col++)
			{
				var currentCols = graph.NodesByColumn[col];
				var nextCols = graph.NodesByColumn[col + 1];

				int currCount = currentCols.Count;
				int nextCount = nextCols.Count;

				// 采用梯次比例映射连线，杜绝 X 交叉
				for (int r = 0; r < currCount; r++)
				{
					var currNode = currentCols[r];
					int targetMin = Mathf.FloorToInt((float)r / currCount * nextCount);
					int targetMax = Mathf.CeilToInt((float)(r + 1) / currCount * nextCount) - 1;
					targetMax = Mathf.Clamp(targetMax, targetMin, nextCount - 1);

					for (int t = targetMin; t <= targetMax; t++)
					{
						currNode.OutgoingConnections.Add(nextCols[t].Id);
					}
				}

				// 确保每个下一列节点都有入边
				for (int nextR = 0; nextR < nextCount; nextR++)
				{
					var nextNode = nextCols[nextR];
					bool hasIncoming = false;
					foreach (var c in currentCols)
					{
						if (c.OutgoingConnections.Contains(nextNode.Id))
						{
							hasIncoming = true;
							break;
						}
					}

					if (!hasIncoming)
					{
						int fromR = Mathf.Clamp(Mathf.FloorToInt((float)nextR / nextCount * currCount), 0, currCount - 1);
						currentCols[fromR].OutgoingConnections.Add(nextNode.Id);
					}
				}
			}

			// 3. 初始列设为可达
			foreach (var startNode in graph.NodesByColumn[0])
			{
				startNode.State = NodeExplorationState.Reachable;
			}

			return graph;
		}

		private static void FillNodeIntelligence(SectorNode node)
		{
			switch (node.Type)
			{
				case SectorNodeType.Combat:
					node.FactionAffiliation = "重工联合 (Heavy Foundry) 巡逻舰队";
					node.PotentialLoot = "优质钢材配料 / 标准动能机枪 / 废料包";
					node.DangerLevel = "常规威胁 (Threat: Normal)";
					break;
				case SectorNodeType.Elite:
					node.FactionAffiliation = "虚空财阀 (Void Syndicate) 猎杀旗舰";
					node.PotentialLoot = "稀有紫品光束炮 / 强化 RCS 推进器 / 大量废料";
					node.DangerLevel = "极高威胁 (Threat: High - 危险！)";
					break;
				case SectorNodeType.Event:
					node.FactionAffiliation = "未知太空漂流遗迹 / 生化信标";
					node.PotentialLoot = "随机改装科技 / 船员特质 / 突发异象挑战";
					node.DangerLevel = "未知 (Threat: Unknown)";
					break;
				case SectorNodeType.Market:
					node.FactionAffiliation = "废土自由走私黑市";
					node.PotentialLoot = "武器图纸售卖 / 导线耗材补给 / 模块以旧换新";
					node.DangerLevel = "中立安全区 (Safe Zone)";
					break;
				case SectorNodeType.Repair:
					node.FactionAffiliation = "轨道急救工程坞";
					node.PotentialLoot = "修复 40% 舰体耐久 / 重置战术爆甲机构";
					node.DangerLevel = "中立安全区 (Safe Zone)";
					break;
				case SectorNodeType.Boss:
					node.FactionAffiliation = "重工移动要塞 · 泰坦熔炉";
					node.PotentialLoot = "星区胜利战利品 / 核心科技跃迁密钥";
					node.DangerLevel = "致命绝境 (Threat: Lethal Boss)";
					break;
			}
		}

		private static SectorNodeType RollNodeType(int col, int totalCols)
		{
			if (col == totalCols - 1) return SectorNodeType.Boss;
			if (col == 0) return SectorNodeType.Combat;
			if (col == totalCols - 2) return GD.Randf() > 0.5f ? SectorNodeType.Repair : SectorNodeType.Market;

			float roll = GD.Randf();
			if (roll < 0.38f) return SectorNodeType.Combat;
			if (roll < 0.58f) return SectorNodeType.Event;
			if (roll < 0.72f) return SectorNodeType.Market;
			if (roll < 0.86f) return SectorNodeType.Elite;
			return SectorNodeType.Repair;
		}
	}
}
