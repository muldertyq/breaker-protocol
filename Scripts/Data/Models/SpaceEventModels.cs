using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Godot;

namespace BreakerProtocol.Data.Models
{
	/// <summary>
	/// 异象结算后果类型
	/// </summary>
	[JsonConverter(typeof(JsonStringEnumConverter))]
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
		[JsonPropertyName("type")] public OutcomeType Type { get; set; } = OutcomeType.GainCurrency;
		[JsonPropertyName("isSuccess")] public bool IsSuccess { get; set; } = true;
		[JsonPropertyName("rollPercent")] public float RollPercent { get; set; } = 0.0f;
		[JsonPropertyName("targetThreshold")] public float TargetThreshold { get; set; } = 1.0f;
		[JsonPropertyName("scrapDelta")] public int ScrapDelta { get; set; } = 0;
		[JsonPropertyName("coreDelta")] public int CoreDelta { get; set; } = 0;
		[JsonPropertyName("repairRatio")] public float RepairRatio { get; set; } = 0.0f;
		[JsonPropertyName("damageAmount")] public float DamageAmount { get; set; } = 0.0f;
		[JsonPropertyName("nextNodeId")] public string? NextNodeId { get; set; }
		[JsonPropertyName("resultLog")] public string ResultLog { get; set; } = string.Empty;
	}

	/// <summary>
	/// 单个事件分支选项
	/// </summary>
	public class EventChoice
	{
		[JsonPropertyName("choiceText")] public string ChoiceText { get; set; } = string.Empty;
		[JsonPropertyName("requiredConditionTag")] public string RequiredConditionTag { get; set; } = string.Empty;
		[JsonPropertyName("requiredScraps")] public int RequiredScraps { get; set; } = 0;
		[JsonPropertyName("requiredCores")] public int RequiredCores { get; set; } = 0;
		[JsonPropertyName("successRate")] public float SuccessRate { get; set; } = 1.0f;

		[JsonPropertyName("successOutcome")] public EventOutcome SuccessOutcome { get; set; } = new();
		[JsonPropertyName("failureOutcome")] public EventOutcome? FailureOutcome { get; set; }
	}

	/// <summary>
	/// 单个完整深空随机事件节点 (对应 core_data/events/*.json)
	/// </summary>
	public class SpaceEventNode
	{
		[JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
		[JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
		[JsonPropertyName("factionTag")] public string FactionTag { get; set; } = "深空异象";
		[JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
		
		[JsonPropertyName("themeColorHex")] public string ThemeColorHex { get; set; } = "#59D9F2";

		[JsonIgnore]
		public Color ThemeColor => Color.FromHtml(ThemeColorHex);

		[JsonPropertyName("choices")] public List<EventChoice> Choices { get; set; } = new();
	}
}
