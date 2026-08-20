using System.Collections.Generic;
using Godot;
using BreakerProtocol.Core;

namespace BreakerProtocol.Ship.Physics
{
	/// <summary>
	/// 单个已挂载推进器的运行时力学描述
	/// </summary>
	public class ThrusterRuntimeData
	{
		public ModuleInstance Module { get; set; } = null!;
		public Vector2 LocalNozzlePixelPos { get; set; }  // 喷口在飞船局部空间的像素坐标
		public Vector2 ThrustDirectionVector { get; set; } // 推力作用方向向量 (沿舰首为 Up = (0, -1))
		public float BaseThrust { get; set; }
		public float BoostMultiplier { get; set; }
		public float TorqueContribution { get; set; }
		public Color FlameColor { get; set; }
		public float FlameLength { get; set; }
		public float CurrentThrottle { get; set; }        // 当前油门开度 0.0 ~ 1.0
	}

	/// <summary>
	/// 全舰总推力能力汇总包
	/// </summary>
	public struct ShipThrustCapability
	{
		public float MaxForwardThrust;   // 最大前向主推力 (N)
		public float MaxReverseThrust;   // 最大后退制动力 (N)
		public float MaxStrafeThrust;    // 最大左右侧移推力 (N)
		public float MaxAngularTorque;   // 最大角力矩 (N·m)
		public float BoostFactor;        // 氮气加力倍率
		public List<ThrusterRuntimeData> Thrusters;
	}

	/// <summary>
	/// 推进器扫描与推力分配解算器 (保留原版基础推力，适度增强转向力矩)
	/// </summary>
	public static class ThrusterSolver
	{
		public static ShipThrustCapability Solve(ShipGrid grid)
		{
			var result = new ShipThrustCapability
			{
				MaxForwardThrust = 0.0f,
				MaxReverseThrust = 0.0f,
				MaxStrafeThrust = 0.0f,
				MaxAngularTorque = 0.0f,
				BoostFactor = 1.8f,
				Thrusters = new List<ThrusterRuntimeData>()
			};

			foreach (var module in grid.Modules)
			{
				if (module.IsDestroyed || module.Definition.Category != "Thruster") continue;

				var props = module.Definition.Properties;
				float thrust = props.TryGetProperty("thrustForce", out var tf) ? tf.GetSingle() : 3000.0f;
				float boost = props.TryGetProperty("boostMultiplier", out var bm) ? bm.GetSingle() : 1.5f;
				float torque = props.TryGetProperty("torqueContribution", out var tq) ? tq.GetSingle() : 0.0f;
				string dirType = props.TryGetProperty("thrustDirection", out var dt) ? dt.GetString() ?? "Backward" : "Backward";

				// 计算喷口在飞船局部空间的坐标 (像素)
				Vector2I size = module.GetRotatedSize();
				Vector2 centerGrid = new(module.GridPosition.X + size.X * 0.5f, module.GridPosition.Y + size.Y * 0.5f);
				Vector2 nozzlePos = GlobalMetrics.MetersToPixels(centerGrid);

				// 解析火焰颜色与长度
				Color flameColor = new(1.0f, 0.6f, 0.2f, 1.0f);
				if (props.TryGetProperty("flameColor", out var fc) && fc.GetArrayLength() == 4)
				{
					flameColor = new Color(fc[0].GetSingle(), fc[1].GetSingle(), fc[2].GetSingle(), fc[3].GetSingle());
				}
				float flameLen = props.TryGetProperty("flameLength", out var fl) ? fl.GetSingle() : 20.0f;

				// 转向力矩适度增强系数 (仅增强角力矩，原版推力完全不变)
				float effectiveTorque = torque > 0 ? torque * 2.5f : thrust * 10.0f;

				var thrusterData = new ThrusterRuntimeData
				{
					Module = module,
					LocalNozzlePixelPos = nozzlePos,
					BaseThrust = thrust,
					BoostMultiplier = boost,
					TorqueContribution = effectiveTorque,
					FlameColor = flameColor,
					FlameLength = flameLen,
					CurrentThrottle = 0.0f
				};

				// 根据类型累加能力 (原版公式 100% 保留)
				if (dirType == "Backward") // 后向主喷（提供前进推力）
				{
					thrusterData.ThrustDirectionVector = Vector2.Up;
					result.MaxForwardThrust += thrust;
					result.BoostFactor = Mathf.Max(result.BoostFactor, boost);
					// 主引擎也提供适当的摆舵辅助力矩
					result.MaxAngularTorque += thrust * 3.0f;
				}
				else if (dirType == "Omni") // 全向 RCS 喷口
				{
					result.MaxForwardThrust += thrust * 0.5f;
					result.MaxReverseThrust += thrust * 0.8f;
					result.MaxStrafeThrust += thrust * 0.8f;
					result.MaxAngularTorque += effectiveTorque;
				}

				result.Thrusters.Add(thrusterData);
			}

			// 原版保底推力 (仅将转向力矩保底从 8000 提升到 24000，转弯更顺手)
			result.MaxForwardThrust = Mathf.Max(result.MaxForwardThrust, 2000.0f);
			result.MaxReverseThrust = Mathf.Max(result.MaxReverseThrust, 1500.0f);
			result.MaxStrafeThrust = Mathf.Max(result.MaxStrafeThrust, 1500.0f);
			result.MaxAngularTorque = Mathf.Max(result.MaxAngularTorque, 24000.0f);

			return result;
		}
	}
}
