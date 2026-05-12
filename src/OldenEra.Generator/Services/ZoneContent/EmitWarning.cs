namespace OldenEra.Generator.Services.ZoneContent
{
    public sealed record EmitWarning(
        string Code,
        string Message,
        string? ZoneName,
        string? Sid)
    {
        public static class Codes
        {
            public const string BiomeFilterIgnored = "BiomeFilter.Ignored";
            public const string FactionAffinityIgnored = "FactionAffinity.Ignored";
            public const string PoolNonMandatoryDropped = "Pool.NonMandatoryDropped";
            public const string MinCountRangeNarrowedToMax = "MinCount.RangeNarrowedToMax";
        }
    }
}
