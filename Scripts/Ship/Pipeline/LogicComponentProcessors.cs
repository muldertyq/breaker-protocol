using System.Collections.Generic;
using Godot;
using BreakerProtocol.Combat.Effects;

namespace BreakerProtocol.Ship.Pipeline
{
	/// <summary>
	/// 逻辑元件运行时状态管理器
	/// 维护分流器轮询计数、电容容量注入等
	/// </summary>
	public class LogicComponentRuntime
	{
		// 记录分流器 (Splitter) 当前轮询到的通道 (0 = out_a, 1 = out_b)
		private readonly Dictionary<string, int> _splitterToggleState = new();

		/// <summary>
		/// 当脉冲到达分流器时，裁决应该从哪根下游导线转发
		/// </summary>
		public PipelineWire? RouteSplitter(string splitterModuleId, List<PipelineWire> downstreamWires)
		{
			if (downstreamWires.Count == 0) return null;
			if (downstreamWires.Count == 1) return downstreamWires[0];

			if (!_splitterToggleState.ContainsKey(splitterModuleId))
			{
				_splitterToggleState[splitterModuleId] = 0;
			}

			int index = _splitterToggleState[splitterModuleId] % downstreamWires.Count;
			_splitterToggleState[splitterModuleId] = (index + 1) % downstreamWires.Count;

			return downstreamWires[index];
		}

		/// <summary>
		/// 当脉冲到达立体跨线桥时，保持原有的水平/垂直行进朝向不变，继续转发给同轴下游导线
		/// </summary>
		public PipelineWire? RouteJumper(ActivePulse incomingPulse, List<PipelineWire> downstreamWires)
		{
			if (downstreamWires.Count == 0) return null;

			// 获取脉冲进入 Jumper 时的最后一步位移向量
			Vector2I incomingDir = Vector2I.Zero;
			if (incomingPulse.GridPath.Count >= 2)
			{
				incomingDir = incomingPulse.GridPath[^1] - incomingPulse.GridPath[^2];
			}

			// 优先寻找延伸方向一致的下游导线 (水平进 -> 水平出；垂直进 -> 垂直出)
			foreach (var wire in downstreamWires)
			{
				if (wire.GridPath.Count >= 2)
				{
					Vector2I outgoingDir = wire.GridPath[1] - wire.GridPath[0];
					if (outgoingDir == incomingDir)
					{
						return wire; // 完美同轴绝缘直通
					}
				}
			}

			// 兜底返回第一条
			return downstreamWires[0];
		}
	}
}
