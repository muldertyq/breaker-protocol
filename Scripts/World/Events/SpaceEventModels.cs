using System;
using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.World.Events
{
	public enum OutcomeType
	{
		GainCurrency,     // 获得金属废料 / 算力核心
		SpendCurrency,    // 消耗金属废料 / 算力核心
		RepairShip,       // 维修舰体构件
		DamageModule,     // 随机构件受到损伤
		GainRandomModule, // 获得随机高级构件
		TriggerAmbush,    // 触发突发伏击
		NextEventNode     // 流转至后续阶段事件
	}

	/// <summary>
	/// 单个事件抉择项的结算后果
	/// </summary>
	public class EventOutcome
	{
		public OutcomeType Type { get; set; }
		public bool IsSuccess { get; set; } = true;
		public float RollPercent { get; set; } = 0.0f;
		public float TargetThreshold { get; set; } = 1.0f;
		public int ScrapDelta { get; set; } = 0;
		public int CoreDelta { get; set; } = 0;
		public float RepairRatio { get; set; } = 0.0f;
		public float DamageAmount { get; set; } = 0.0f;
		public string? NextNodeId { get; set; }
		public string ResultLog { get; set; } = string.Empty;
	}

	/// <summary>
	/// 单个事件分支选项
	/// </summary>
	public class EventChoice
	{
		public string ChoiceText { get; set; } = string.Empty;
		public string RequiredConditionTag { get; set; } = string.Empty;
		public int RequiredScraps { get; set; } = 0;
		public int RequiredCores { get; set; } = 0;
		public float SuccessRate { get; set; } = 1.0f; // 成功几率 0.0 ~ 1.0

		public EventOutcome SuccessOutcome { get; set; } = new();
		public EventOutcome? FailureOutcome { get; set; }
	}

	/// <summary>
	/// 单个完整深空随机事件节点
	/// </summary>
	public class SpaceEventNode
	{
		public string Id { get; set; } = string.Empty;
		public string Title { get; set; } = string.Empty;
		public string FactionTag { get; set; } = "深空异象";
		public string Description { get; set; } = string.Empty;
		public Color ThemeColor { get; set; } = new Color(0.35f, 0.85f, 0.95f);
		public List<EventChoice> Choices { get; } = new();
	}
}
