using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models
{
	public class SpawnOffsetDef
	{
		[JsonPropertyName("x")] public float X { get; set; } = 0.0f;
		[JsonPropertyName("y")] public float Y { get; set; } = 0.0f;
	}

	public class EncounterShipDef
	{
		[JsonPropertyName("blueprintId")] public string BlueprintId { get; set; } = string.Empty;
		[JsonPropertyName("role")] public string Role { get; set; } = "Scout";
		[JsonPropertyName("spawnOffset")] public SpawnOffsetDef SpawnOffset { get; set; } = new();
	}

	public class EncounterDef
	{
		[JsonPropertyName("encounterId")] public string EncounterId { get; set; } = string.Empty;
		[JsonPropertyName("displayName")] public string DisplayName { get; set; } = string.Empty;
		[JsonPropertyName("category")] public string Category { get; set; } = "Combat";
		[JsonPropertyName("difficultyRating")] public int DifficultyRating { get; set; } = 1;
		[JsonPropertyName("ships")] public List<EncounterShipDef> Ships { get; set; } = new();
	}

	public class EncounterPoolFileDef
	{
		[JsonPropertyName("encounters")] public List<EncounterDef> Encounters { get; set; } = new();
	}
}
