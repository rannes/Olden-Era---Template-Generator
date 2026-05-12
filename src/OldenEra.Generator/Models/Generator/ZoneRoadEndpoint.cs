namespace OldenEra.Generator.Models
{
    public enum ZoneRoadEndpointKind
    {
        Connection,
        MainObject,
        MandatoryContent,
    }

    public sealed class ZoneRoadEndpoint
    {
        public ZoneRoadEndpointKind Kind { get; set; } = ZoneRoadEndpointKind.Connection;
        public string Arg { get; set; } = "";
    }
}
