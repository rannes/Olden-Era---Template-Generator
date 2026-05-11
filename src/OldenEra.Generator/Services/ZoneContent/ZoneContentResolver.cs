using OldenEra.Generator.Models;

namespace OldenEra.Generator.Services.ZoneContent
{
    public static class ZoneContentResolver
    {
        public static ZoneContentList Resolve(
            NeutralZoneContent cfg,
            NeutralZoneTier tier,
            string zoneLetter)
        {
            var result = new ZoneContentList();
            foreach (var item in cfg.Global.Items)
                result.Items.Add(item);
            return result;
        }
    }
}
