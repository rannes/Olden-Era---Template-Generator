namespace OldenEra.Generator.Models
{
    public enum ContentRuleType { Distance, OnRoad, Between }

    public sealed class ContentConnectionRule
    {
        public ContentRuleType Type { get; set; } = ContentRuleType.Distance;
        public string FromRef { get; set; } = "";
        public string ToRef { get; set; } = "";
        public string? RoadType { get; set; }
        public double? MinDistance { get; set; }
        public double? MaxDistance { get; set; }
    }
}
