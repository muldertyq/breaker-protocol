using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.Ship.Pipeline
{
	/// <summary>
	/// 构件引脚端口在飞船网格上的运行时描述
	/// </summary>
	public class PinInstance
	{
		public string OwnerModuleInstanceId { get; }
		public PinDefinition Definition { get; }
		
		/// <summary>
		/// 经过旋转变换后在飞船网格上的绝对坐标 (GU)
		/// </summary>
		public Vector2I AbsoluteGridPos { get; set; }

		public PinType Type => Definition.Type.Equals("OUT", StringComparison.OrdinalIgnoreCase) 
			? PinType.OUT 
			: PinType.IN;

		public PinInstance(string ownerModuleId, PinDefinition definition, Vector2I absoluteGridPos)
		{
			OwnerModuleInstanceId = ownerModuleId;
			Definition = definition;
			AbsoluteGridPos = absoluteGridPos;
		}
	}

	/// <summary>
	/// 单条单向 PCB 导线数据模型
	/// 记录从源输出引脚 (OUT) 到目标输入引脚 (IN) 的离散网格折线路径
	/// </summary>
	public class PipelineWire
	{
		public string WireId { get; }
		public string SourceModuleId { get; }
		public string SourcePinId { get; }
		public Vector2I SourceGridPos { get; }

		public string TargetModuleId { get; }
		public string TargetPinId { get; }
		public Vector2I TargetGridPos { get; }

		/// <summary>
		/// 曼哈顿正交离散网格路径（包含起点与终点）
		/// </summary>
		public List<Vector2I> GridPath { get; }

		/// <summary>
		/// 导线当前最大脉冲承载带宽 (默认 10 发/秒)
		/// </summary>
		public float BandwidthCapacity { get; set; } = 10.0f;

		/// <summary>
		/// 导线当前剩余耐久 (标准铜排 50 HP，应急飞线仅 15 HP)
		/// </summary>
		public float DurabilityHp { get; set; } = 50.0f;

		/// <summary>
		/// 导线是否已被打断
		/// </summary>
		public bool IsSevered => DurabilityHp <= 0.0f;

		/// <summary>
		/// 是否为战地应急飞线 (极度脆弱，流经增加 +35% 发热)
		/// </summary>
		public bool IsHotwire { get; set; } = false;

		public PipelineWire(
			string wireId,
			string srcModId, string srcPinId, Vector2I srcPos,
			string dstModId, string dstPinId, Vector2I dstPos,
			List<Vector2I> path)
		{
			WireId = wireId;
			SourceModuleId = srcModId;
			SourcePinId = srcPinId;
			SourceGridPos = srcPos;
			TargetModuleId = dstModId;
			TargetPinId = dstPinId;
			TargetGridPos = dstPos;
			GridPath = path;
		}
	}

	/// <summary>
	/// 引脚连接兼容性校验器
	/// </summary>
	public static class PinCompatibilityValidator
	{
		public static bool CanConnect(PinInstance sourcePin, PinInstance targetPin, out string errorMessage)
		{
			errorMessage = string.Empty;

			if (sourcePin.OwnerModuleInstanceId == targetPin.OwnerModuleInstanceId)
			{
				errorMessage = "严禁将同一构件的引脚进行自连短路！";
				return false;
			}

			if (sourcePin.Type != PinType.OUT || targetPin.Type != PinType.IN)
			{
				errorMessage = $"引脚方向错误！只能从 [OUT] 引脚连接到 [IN] 引脚 (当前: {sourcePin.Type} -> {targetPin.Type})";
				return false;
			}

			string srcCat = sourcePin.Definition.Category;
			string dstCat = targetPin.Definition.Category;

			if (srcCat != "Universal" && dstCat != "Universal" && !srcCat.Equals(dstCat, StringComparison.OrdinalIgnoreCase))
			{
				errorMessage = $"引脚总线协议不兼容！来源类型 [{srcCat}] 无法接入目标类型 [{dstCat}]";
				return false;
			}

			return true;
		}
	}
}
