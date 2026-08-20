using System;
using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.Ship.Pipeline
{
	/// <summary>
	/// 优化升级版：曼哈顿正交 A* 寻路器 (支持引脚穿透避让与直角平滑)
	/// </summary>
	public static class ManhattanRouter
	{
		private class Node
		{
			public Vector2I Position;
			public Vector2I Direction;
			public float GCost;
			public float HCost;
			public float FCost => GCost + HCost;
			public Node? Parent;

			public Node(Vector2I pos, Vector2I dir, float g, float h, Node? parent)
			{
				Position = pos;
				Direction = dir;
				GCost = g;
				HCost = h;
				Parent = parent;
			}
		}

		private static readonly Vector2I[] OrthoDirections =
		{
			Vector2I.Up,
			Vector2I.Down,
			Vector2I.Left,
			Vector2I.Right
		};

		private const float StepCost = 1.0f;
		private const float TurnPenalty = 2.0f;          // 拐弯代价
		private const float PinCrossPenalty = 8.0f;      // 优化点：穿过其他无关引脚的惩罚代价 (促使 A* 自动绕开其他端口)

		public static List<Vector2I> FindPath(Vector2I startPos, Vector2I endPos, ShipGrid grid)
		{
			var openList = new List<Node>();
			var closedSet = new HashSet<Vector2I>();

			var startNode = new Node(startPos, Vector2I.Zero, 0, GetManhattanDistance(startPos, endPos), null);
			openList.Add(startNode);

			// 预先收集所有非起点、非终点的其他引脚坐标
			var otherPinCoords = new HashSet<Vector2I>();
			foreach (var module in grid.Modules)
			{
				foreach (var (_, pinPos) in module.GetTransformedPins())
				{
					if (pinPos != startPos && pinPos != endPos)
					{
						otherPinCoords.Add(pinPos);
					}
				}
			}

			while (openList.Count > 0)
			{
				Node current = openList[0];
				int bestIndex = 0;
				for (int i = 1; i < openList.Count; i++)
				{
					if (openList[i].FCost < current.FCost || 
					   (Mathf.IsEqualApprox(openList[i].FCost, current.FCost) && openList[i].HCost < current.HCost))
					{
						current = openList[i];
						bestIndex = i;
					}
				}

				openList.RemoveAt(bestIndex);
				closedSet.Add(current.Position);

				if (current.Position == endPos)
				{
					return ReconstructPath(current);
				}

				foreach (var dir in OrthoDirections)
				{
					Vector2I neighborPos = current.Position + dir;

					if (closedSet.Contains(neighborPos)) continue;

					// 障碍物阻挡判定：实心重装甲不可穿行
					if (neighborPos != startPos && neighborPos != endPos)
					{
						var module = grid.GetModuleAt(neighborPos);
						if (module != null && module.Definition.Category == "Armor")
						{
							continue;
						}
					}

					// 1. 计算拐弯惩罚
					float turnCost = 0.0f;
					if (current.Direction != Vector2I.Zero && current.Direction != dir)
					{
						turnCost = TurnPenalty;
					}

					// 2. 计算引脚穿透避让代价 (防止压在别人引脚上造成视觉误会)
					float pinPenalty = otherPinCoords.Contains(neighborPos) ? PinCrossPenalty : 0.0f;

					float tentativeGCost = current.GCost + StepCost + turnCost + pinPenalty;

					var existingNode = openList.Find(n => n.Position == neighborPos);
					if (existingNode != null)
					{
						if (tentativeGCost < existingNode.GCost)
						{
							existingNode.GCost = tentativeGCost;
							existingNode.Direction = dir;
							existingNode.Parent = current;
						}
					}
					else
					{
						float h = GetManhattanDistance(neighborPos, endPos);
						var neighborNode = new Node(neighborPos, dir, tentativeGCost, h, current);
						openList.Add(neighborNode);
					}
				}
			}

			return new List<Vector2I>();
		}

		private static float GetManhattanDistance(Vector2I a, Vector2I b)
		{
			return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
		}

		private static List<Vector2I> ReconstructPath(Node endNode)
		{
			var path = new List<Vector2I>();
			Node? curr = endNode;
			while (curr != null)
			{
				path.Add(curr.Position);
				curr = curr.Parent;
			}
			path.Reverse();
			return path;
		}
	}
}
