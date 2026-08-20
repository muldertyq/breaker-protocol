using Godot;

namespace BreakerProtocol.Ship
{
	/// <summary>
	/// 单个 1x1 GU 空间网格单元数据
	/// </summary>
	public struct GridCell
	{
		/// <summary>
		/// 占用该单元的构件实例 ID（若为空字符串表示空格子）
		/// </summary>
		public string ModuleInstanceId;

		/// <summary>
		/// 该网格是否被实体构件占据
		/// </summary>
		public bool IsOccupied => !string.IsNullOrEmpty(ModuleInstanceId);

		public static GridCell Empty => new()
		{
			ModuleInstanceId = string.Empty
		};
	}
}
