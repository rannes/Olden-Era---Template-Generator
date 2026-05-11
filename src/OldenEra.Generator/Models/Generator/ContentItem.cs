using System.Collections.Generic;

namespace OldenEra.Generator.Models
{
    public sealed class ContentItem
    {
        public string Sid { get; set; } = "";
        public bool IsGroup { get; set; }
        public int MinCount { get; set; } = 1;
        public int MaxCount { get; set; } = 1;
        public ZoneContentPool Pool { get; set; } = ZoneContentPool.Mandatory;
        public bool IsGuarded { get; set; }
        public bool NearCastle { get; set; }
        public string? RoadDistance { get; set; }
        public List<string> FactionAffinity { get; set; } = new();
        public List<string> BiomeFilter { get; set; } = new();
    }
}
