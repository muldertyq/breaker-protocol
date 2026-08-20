using Godot;
using BreakerProtocol.Core;

namespace BreakerProtocol.Ship.Physics
{
	/// <summary>
	/// 飞船实时物理力学状态数据包
	/// </summary>
	public struct ShipPhysicsData
	{
		/// <summary>
		/// 全舰总装载质量 (吨, t)
		/// </summary>
		public float TotalMass;

		/// <summary>
		/// 质心在网格坐标系中的相对浮点坐标 (GU)
		/// </summary>
		public Vector2 CenterOfMassGrid;

		/// <summary>
		/// 质心在战舰局部像素坐标系中的偏移向量 (Pixels)
		/// 用于直接赋值给 Godot RigidBody2D.CenterOfMass
		/// </summary>
		public Vector2 CenterOfMassPixels;

		/// <summary>
		/// 全舰绕质心的总转动惯量 (t·m²)
		/// </summary>
		public float MomentOfInertia;

		/// <summary>
		/// 质心偏航角偏差 (度, 0° 表示绝对左右对称)
		/// </summary>
		public float CenterOfMassYawOffsetDegrees;
	}

	/// <summary>
	/// 飞船动态质心与转动惯量实时积分器
	/// 严格遵循规范 08 中的牛顿刚体动力学积分公式
	/// </summary>
	public static class CenterOfMassSolver
	{
		/// <summary>
		/// 根据当前 ShipGrid 全量积分计算物理力学参数
		/// </summary>
		public static ShipPhysicsData Solve(ShipGrid grid)
		{
			if (grid.ModuleCount == 0)
			{
				return new ShipPhysicsData
				{
					TotalMass = 1.0f,
					CenterOfMassGrid = Vector2.Zero,
					CenterOfMassPixels = Vector2.Zero,
					MomentOfInertia = 10.0f,
					CenterOfMassYawOffsetDegrees = 0.0f
				};
			}

			float totalMass = 0.0f;
			Vector2 massMomentSum = Vector2.Zero;

			// ==========================================
			// 阶段 1：积分计算全舰总质量与质心位置 (CoM)
			// ==========================================
			foreach (var module in grid.Modules)
			{
				if (module.IsDestroyed) continue;

				float mass = module.Definition.Mass;
				Vector2I size = module.GetRotatedSize();

				// 构件中心在网格坐标系中的位置 (GU)
				Vector2 moduleCenterGrid = new(
					module.GridPosition.X + (size.X * 0.5f),
					module.GridPosition.Y + (size.Y * 0.5f)
				);

				totalMass += mass;
				massMomentSum += moduleCenterGrid * mass;
			}

			// 防止除零保护
			totalMass = Mathf.Max(0.1f, totalMass);
			Vector2 comGrid = massMomentSum / totalMass;

			// 转换为局部像素坐标 (1 GU = 8 px)
			Vector2 comPixels = GlobalMetrics.MetersToPixels(comGrid);

			// ==========================================
			// 阶段 2：应用平行轴定理积分转动惯量 (MoI)
			// ==========================================
			float totalInertia = 0.0f;

			foreach (var module in grid.Modules)
			{
				if (module.IsDestroyed) continue;

				float mass = module.Definition.Mass;
				Vector2I size = module.GetRotatedSize();

				// 尺寸对应的物理米数 (1 GU = 1.0 m)
				float w = size.X * GlobalMetrics.GridUnitMeters;
				float h = size.Y * GlobalMetrics.GridUnitMeters;

				// 1. 构件自身绕其自身中心的局部转动惯量 (矩形刚体)
				float iLocal = (1.0f / 12.0f) * mass * ((w * w) + (h * h));

				// 2. 构件中心到全舰质心 CoM 的欧式距离 (米)
				Vector2 moduleCenterGrid = new(
					module.GridPosition.X + (size.X * 0.5f),
					module.GridPosition.Y + (size.Y * 0.5f)
				);
				float distanceToCom = (moduleCenterGrid - comGrid).Length() * GlobalMetrics.GridUnitMeters;

				// 3. 平行轴定理：I = I_local + m * d²
				float iContribution = iLocal + (mass * distanceToCom * distanceToCom);
				totalInertia += iContribution;
			}

			// 最小转动惯量保护
			totalInertia = Mathf.Max(1.0f, totalInertia);

			// 计算偏航偏移角 (若质心偏离 X=0 中轴)
			float yawOffset = Mathf.RadToDeg(Mathf.Atan2(comGrid.X, -comGrid.Y));

			return new ShipPhysicsData
			{
				TotalMass = totalMass,
				CenterOfMassGrid = comGrid,
				CenterOfMassPixels = comPixels,
				MomentOfInertia = totalInertia,
				CenterOfMassYawOffsetDegrees = yawOffset
			};
		}
	}
}
