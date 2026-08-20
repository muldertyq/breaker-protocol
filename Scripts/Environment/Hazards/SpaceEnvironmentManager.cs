using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.Environment.Hazards
{
	/// <summary>
	/// 空间极端物理环境总控中枢 (规范 07 / TASK-26)
	/// </summary>
	public partial class SpaceEnvironmentManager : Node2D
	{
		public static SpaceEnvironmentManager Instance { get; private set; } = null!;

		private readonly List<SingularityVortexEntity> _singularities = new();
		private readonly List<EmpNebulaStormEntity> _nebulas = new();

		public IReadOnlyList<SingularityVortexEntity> Singularities => _singularities;

		public override void _Ready()
		{
			Instance = this;
			ZIndex = 0;
		}

		/// <summary>
		/// 创建一个高引力黑洞奇点漩涡
		/// </summary>
		public SingularityVortexEntity SpawnSingularity(Vector2 worldPos, float gravityRadius = 650.0f, float eventHorizon = 60.0f)
		{
			var singularity = new SingularityVortexEntity
			{
				GlobalPosition = worldPos,
				GravityRadius = gravityRadius,
				EventHorizonRadius = eventHorizon
			};

			AddChild(singularity);
			_singularities.Add(singularity);
			return singularity;
		}

		/// <summary>
		/// 创建一个 EMP 电磁脉冲星云风暴
		/// </summary>
		public EmpNebulaStormEntity SpawnNebulaStorm(Vector2 worldPos, float radius = 320.0f)
		{
			var nebula = new EmpNebulaStormEntity
			{
				GlobalPosition = worldPos,
				StormRadius = radius
			};

			AddChild(nebula);
			_nebulas.Add(nebula);
			return nebula;
		}

		/// <summary>
		/// 计算指定坐标在全战场受到的所有黑洞引力叠加加速度
		/// </summary>
		public Vector2 SampleTotalGravitationalAcceleration(Vector2 worldPos)
		{
			Vector2 totalAccel = Vector2.Zero;
			for (int i = 0; i < _singularities.Count; i++)
			{
				var s = _singularities[i];
				if (IsInstanceValid(s))
				{
					totalAccel += s.GetGravitationalAcceleration(worldPos);
				}
			}
			return totalAccel;
		}

		public void ClearAll()
		{
			foreach (var s in _singularities) if (IsInstanceValid(s)) s.QueueFree();
			foreach (var n in _nebulas) if (IsInstanceValid(n)) n.QueueFree();
			_singularities.Clear();
			_nebulas.Clear();
		}
	}
}
