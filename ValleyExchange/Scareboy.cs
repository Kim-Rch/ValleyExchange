using StardewValley;
using StardewValley.TerrainFeatures;
using SObject = StardewValley.Object;

namespace ValleyExchange;

/// <summary>
/// The scareboy, a walking scarecrow. While he's hired no crows land on the farm (Science.BeforeCrows); every evening he spreads
/// the crate's fertilizer on growing crops and robs the crows' nests for the shiny things they stole.
/// </summary>
internal static class Scareboy
{
    public const int Tiles = 60, StashRolls = 3;

    public static string Day(Random rng, double more)
    {
        // what's in the crate, most first (tree fertilizer is for trees)
        var sacks = Crate.Items().Where(i => i.Category == SObject.fertilizerCategory && i.QualifiedItemId != "(O)805")
            .GroupBy(i => i.QualifiedItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Stack));
        var used = new Dictionary<string, int>();
        int max = (int)Math.Round(Tiles * more), spread = 0;
        foreach (var land in Lands.All().Prepend(Game1.getFarm()))
        {
            foreach (var dirt in land.terrainFeatures.Values.OfType<HoeDirt>())
            {
                if (spread >= max || sacks.Count == 0)
                    break;
                if (dirt.crop is not { } crop || crop.dead.Value || dirt.readyForHarvest())
                    continue;
                string? id = sacks.OrderByDescending(s => s.Value).Select(s => s.Key)
                    .FirstOrDefault(f => dirt.CheckApplyFertilizerRules(f) == HoeDirtFertilizerApplyStatus.Okay);
                if (id == null)
                    continue;
                dirt.fertilizer.Value = id;
                dirt.applySpeedIncreases(Game1.player);
                used[id] = used.GetValueOrDefault(id) + 1;
                if (--sacks[id] == 0)
                    sacks.Remove(id);
                spread++;
            }
        }
        foreach (var (id, count) in used)
            Crate.Take(id, count);

        int found = 0;
        for (int roll = 0; roll < (int)Math.Round(StashRolls * more); roll++)
        {
            if (Market.CrowStash(rng) is not { } shiny)
                continue;
            var item = ItemRegistry.Create(shiny.Id, shiny.Count);
            CrewReport.Got(Role.Scareboy, "crow loot", item);
            Crate.Store(item);
            found++;
        }
        CrewSkills.Work(Role.Scareboy, spread + found * 5);
        string fed = spread > 0 ? $"Spread fertilizer on {spread} crops." : used.Count == 0 && sacks.Count == 0 ? "No fertilizer in the crate to spread." : "Every crop's already fed.";
        return $"Not one crow on the farm. {fed} Robbed {found} shiny thing(s) from the crows' nests.";
    }
}
