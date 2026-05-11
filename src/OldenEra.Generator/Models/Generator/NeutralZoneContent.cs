using System.Collections.Generic;
namespace OldenEra.Generator.Models
{
    public sealed class NeutralZoneContent
    {
        public ZoneContentList Global { get; set; } = new();
        public Dictionary<NeutralZoneTier, ZoneContentList> ByTier { get; set; } = new();
        public Dictionary<string, ZoneContentList> ByZoneLetter { get; set; } = new();
    }
}
