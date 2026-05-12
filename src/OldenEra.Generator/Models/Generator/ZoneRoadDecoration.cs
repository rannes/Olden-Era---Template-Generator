namespace OldenEra.Generator.Models
{
    public sealed class ZoneRoadDecoration
    {
        public string Zone { get; set; } = "";
        public ZoneRoadType RoadType { get; set; } = ZoneRoadType.Stone;
        public ZoneRoadEndpoint From { get; set; } = new();
        public ZoneRoadEndpoint To { get; set; } = new();
    }
}
