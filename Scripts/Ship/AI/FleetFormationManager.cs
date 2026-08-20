using System.Collections.Generic;
using System.Linq;
using Godot;
using BreakerProtocol.Combat.Effects;
using BreakerProtocol.Core;

namespace BreakerProtocol.Ship.AI
{
	public enum FormationType
	{
		Pincer,  // 钳形包围阵：长机居中，僚机左右大角度前突包夹
		Wedge,   // 楔形三角阵：长机居中突前，僚机梯次排开成锐角箭头
		Line     // 齐射战列线：横向一字排开，火力扇面全面覆盖
	}

	/// <summary>
	/// 遭遇战编队协同、开火令牌仲裁与阵型拓扑雷达投影中枢
	/// </summary>
	public partial class FleetFormationManager : Node2D
	{
		public static FleetFormationManager Instance { get; private set; } = null!;

		public FormationType CurrentFormation { get; set; } = FormationType.Pincer;
		public ShipEntity? FleetLeader { get; set; }
		public List<ShipEntity> FleetMembers { get; } = new();

		public int MaxSimultaneousFireTokens { get; set; } = 1;
		private readonly HashSet<string> _activeTokenHolders = new();
		private float _tokenRebalanceTimer = 0.0f;

		private readonly Dictionary<Node2D, float> _threatScores = new();

		public override void _Ready()
		{
			Instance = this;
			ZIndex = 5;
		}

		public void RegisterFleetMember(ShipEntity ship)
		{
			if (!FleetMembers.Contains(ship))
			{
				FleetMembers.Add(ship);
				if (FleetLeader == null || !GodotObject.IsInstanceValid(FleetLeader))
				{
					FleetLeader = ship;
				}
			}
		}

		public void UnregisterFleetMember(ShipEntity ship)
		{
			FleetMembers.Remove(ship);
			_activeTokenHolders.Remove(ship.Name);
			if (FleetLeader == ship)
			{
				FleetLeader = FleetMembers.FirstOrDefault();
			}
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;
			CleanupInvalidMembers();

			_tokenRebalanceTimer += dt;
			if (_tokenRebalanceTimer >= 0.60f)
			{
				_tokenRebalanceTimer = 0.0f;
				ArbitrateFireTokens();
			}

			DecayThreatScores(dt);
			QueueRedraw();
		}

		private void CleanupInvalidMembers()
		{
			FleetMembers.RemoveAll(s => !GodotObject.IsInstanceValid(s) || s.IsQueuedForDeletion());
			if (FleetLeader != null && (!GodotObject.IsInstanceValid(FleetLeader) || FleetLeader.IsQueuedForDeletion()))
			{
				FleetLeader = FleetMembers.FirstOrDefault();
			}
		}

		public Vector2 GetFormationOffsetWorldPos(ShipEntity member, Node2D target)
		{
			if (FleetLeader == null || !GodotObject.IsInstanceValid(FleetLeader) || FleetMembers.Count <= 1)
			{
				return target.GlobalPosition;
			}

			int memberIndex = FleetMembers.IndexOf(member);
			if (memberIndex <= 0)
			{
				return target.GlobalPosition;
			}

			Vector2 leaderPos = FleetLeader.GlobalPosition;
			Vector2 toTargetDir = (target.GlobalPosition - leaderPos).Normalized();
			// 2D 右舷法线向量 (顺时针旋转 90 度)
			Vector2 leaderRight = new(-toTargetDir.Y, toTargetDir.X);

			Vector2 offsetPos = leaderPos;
			float spacing = 160.0f;

			switch (CurrentFormation)
			{
				case FormationType.Pincer:
					int pincerSide = (memberIndex % 2 == 1) ? -1 : 1;
					int pincerTier = (memberIndex + 1) / 2;
					offsetPos = leaderPos + (toTargetDir * (pincerTier * 30.0f)) + (leaderRight * (pincerSide * pincerTier * spacing * 1.1f));
					break;

				case FormationType.Wedge:
					int wedgeSide = (memberIndex % 2 == 1) ? -1 : 1;
					int wedgeTier = (memberIndex + 1) / 2;
					offsetPos = leaderPos - (toTargetDir * (wedgeTier * spacing * 0.65f)) + (leaderRight * (wedgeSide * wedgeTier * spacing * 0.85f));
					break;

				case FormationType.Line:
					int lineSide = (memberIndex % 2 == 1) ? -1 : 1;
					int lineTier = (memberIndex + 1) / 2;
					offsetPos = leaderPos + (leaderRight * (lineSide * lineTier * spacing));
					break;
			}

			return offsetPos;
		}

		public bool RequestFirePermission(ShipEntity ship)
		{
			return _activeTokenHolders.Contains(ship.Name);
		}

		private void ArbitrateFireTokens()
		{
			_activeTokenHolders.Clear();

			var sortedByPriority = FleetMembers
				.Where(s => GodotObject.IsInstanceValid(s) && s.AI != null)
				.OrderByDescending(s => s.AI!.Archetype == AiArchetype.Brawler ? 2 : 1)
				.ToList();

			int granted = 0;
			foreach (var ship in sortedByPriority)
			{
				if (granted < MaxSimultaneousFireTokens)
				{
					_activeTokenHolders.Add(ship.Name);
					granted++;
				}
			}
		}

		public void RecordThreatDamage(Node2D attacker, float damage)
		{
			if (!_threatScores.ContainsKey(attacker))
			{
				_threatScores[attacker] = 0.0f;
			}
			_threatScores[attacker] += damage * 1.5f;
		}

		private void DecayThreatScores(float dt)
		{
			var keys = _threatScores.Keys.ToList();
			foreach (var key in keys)
			{
				_threatScores[key] = Mathf.Max(0.0f, _threatScores[key] - (15.0f * dt));
			}
		}

		public override void _Draw()
		{
			if (FleetLeader == null || !GodotObject.IsInstanceValid(FleetLeader) || FleetMembers.Count <= 1) return;

			var player = GetTree().GetFirstNodeInGroup("Player") as Node2D;
			if (player == null || !GodotObject.IsInstanceValid(player)) return;

			Vector2 leaderPos = ToLocal(FleetLeader.GlobalPosition);

			for (int i = 1; i < FleetMembers.Count; i++)
			{
				var member = FleetMembers[i];
				if (!GodotObject.IsInstanceValid(member)) continue;

				Vector2 memberPos = ToLocal(member.GlobalPosition);
				Vector2 ghostWorld = GetFormationOffsetWorldPos(member, player);
				Vector2 ghostLocal = ToLocal(ghostWorld);

				// 1. 绘制长机到幽灵站位点的阵型骨架线 (青蓝色虚线)
				DrawDashedLine(leaderPos, ghostLocal, new Color(0.2f, 0.8f, 1.0f, 0.55f), 2.0f, 8.0f);

				// 2. 绘制幽灵期望站位点
				DrawArc(ghostLocal, 14.0f, 0, Mathf.Tau, 16, new Color(0.2f, 0.9f, 1.0f, 0.45f), 1.5f);
				DrawCircle(ghostLocal, 3.0f, new Color(0.2f, 0.9f, 1.0f, 0.60f));

				// 3. 绘制僚机当前位置到幽灵锚点的牵引线
				DrawLine(memberPos, ghostLocal, new Color(1.0f, 0.8f, 0.2f, 0.30f), 1.0f);
			}
		}

		private void DrawDashedLine(Vector2 from, Vector2 to, Color color, float width, float dashLength)
		{
			float totalDist = from.DistanceTo(to);
			if (totalDist < 1.0f) return;

			Vector2 dir = (to - from).Normalized();
			float drawn = 0.0f;
			bool isDash = true;

			while (drawn < totalDist)
			{
				float segLen = Mathf.Min(dashLength, totalDist - drawn);
				if (isDash)
				{
					DrawLine(from + (dir * drawn), from + (dir * (drawn + segLen)), color, width);
				}
				drawn += segLen;
				isDash = !isDash;
			}
		}
	}
}
