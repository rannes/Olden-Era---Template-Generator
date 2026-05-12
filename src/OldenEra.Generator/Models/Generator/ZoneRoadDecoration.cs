namespace OldenEra.Generator.Models
{
    public sealed class ZoneRoadDecoration
    {
        public string Zone { get; set; } = "";
        public string RoadType { get; set; } = "Stone";
        public ZoneRoadEndpoint From { get; set; } = new();
        public ZoneRoadEndpoint To { get; set; } = new();
    }
}
