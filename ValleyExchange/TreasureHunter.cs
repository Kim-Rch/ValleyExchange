using Microsoft.Xna.Framework;
using StardewValley;

namespace ValleyExchange;

/// <summary>
/// The treasure hunter: at 6pm she's dug up artifact spots all over the valley (the real game drops: artifacts, lost books,
/// geodes... and Doc's Archaeology applies), plus a few test pits of her own for clay, geodes and the odd artifact trove.
/// </summary>
internal static class TreasureHunter
{
    public const int DigsPerDay = 6, PitsPerDay = 3;

    private static readonly string[] Sites =
        { "Town", "Mountain", "Forest", "BusStop", "Beach", "Backwoods", "Railroad", "Woods", "Desert", "IslandWest", "IslandNorth", "IslandSouth" };

    public static string Day(Random rng, double more)
    {
        var spots = Sites.Select(Game1.getLocationFromName).OfType<GameLocation>()
            .SelectMany(l => l.objects.Pairs.Where(p => p.Value.QualifiedItemId == "(O)590").Select(p => (Location: l, Tile: p.Key)))
            .OrderBy(_ => rng.Next()).Take((int)Math.Round(DigsPerDay * more)).ToList();
        int dug = 0, found = 0;
        foreach (var (location, tile) in spots)
        {
            var before = location.debris.ToHashSet();
            location.objects.Remove(tile);
            location.digUpArtifactSpot((int)tile.X, (int)tile.Y, Game1.player);
            foreach (var debris in location.debris.Where(d => !before.Contains(d)).ToList())
            {
                if (Crate.Take(location, debris) is not { } item)
                    continue;
                CrewReport.Got(Role.TreasureHunter, "dug up", item);
                Crate.Store(item);
                found += item.Stack;
            }
            dug++;
        }
        int pits = (int)Math.Round(PitsPerDay * more);
        for (int i = 0; i < pits; i++)
        {
            if (Market.TestPit(rng) is not { } pit)
                continue;
            var item = ItemRegistry.Create(pit.Id, pit.Count);
            CrewReport.Got(Role.TreasureHunter, "test pits", item);
            Crate.Store(item);
            found += pit.Count;
        }
        CrewReport.Count(Role.TreasureHunter, "spots dug", dug);
        CrewSkills.Work(Role.TreasureHunter, dug * 3 + pits);
        return dug == 0 ? $"No worms wigglin' anywhere today, boss. Dug {pits} test pits instead: {found} things in the crate."
            : $"Dug up {dug} artifact spots and {pits} test pits. {found} things in the crate.";
    }
}
