using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OldenEra.Generator.Models.Unfrozen
{
    public class MainObject
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("spawn")]
        public string? Spawn { get; set; }

        [JsonPropertyName("guardChance")]
        public double? GuardChance { get; set; }

        [JsonPropertyName("guardValue")]
        public int? GuardValue { get; set; }

        [JsonPropertyName("guardWeeklyIncrement")]
        public double? GuardWeeklyIncrement { get; set; }

        [JsonPropertyName("removeGuardIfHasOwner")]
        public bool? RemoveGuardIfHasOwner { get; set; }

        [JsonPropertyName("buildingsConstructionSid")]
        public string? BuildingsConstructionSid { get; set; }

        [JsonPropertyName("faction")]
        public TypedSelector? Faction { get; set; }

        // T-509: shipped templates also use a plural `factions` array on MainObjects
        // (typically empty, see Hallway/Trinity/Spider). Round-trip-only: never
        // emitted by the generator, but must survive load → save.
        [JsonPropertyName("factions")]
        public List<string>? Factions { get; set; }

        [JsonPropertyName("placement")]
        public string? Placement { get; set; }

        [JsonPropertyName("placementArgs")]
        public List<string>? PlacementArgs { get; set; }

        [JsonPropertyName("holdCityWinCon")]
        public bool? HoldCityWinCon { get; set; }

        // T-509: per-MainObject scenario-authoring fields. Round-trip-only — the
        // generator does not emit these under default settings; they exist so we
        // can load shipped hold-city / per-tier-hire templates without dropping data.
        [JsonPropertyName("owner")]
        public string? Owner { get; set; }

        [JsonPropertyName("isKeyObject")]
        public bool? IsKeyObject { get; set; }

        [JsonPropertyName("enableWeeklyUnitIncrement")]
        public bool? EnableWeeklyUnitIncrement { get; set; }

        [JsonPropertyName("initialUnitIncrement")]
        public int? InitialUnitIncrement { get; set; }
    }

    public class TypedSelector
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("args")]
        public List<string>? Args { get; set; }
    }
}
