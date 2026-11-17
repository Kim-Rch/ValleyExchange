using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using SObject = StardewValley.Object;

namespace ValleyExchange;

/// <summary>
/// The forager's day out in the wilds around the farm: picks up wild forage, chops a couple of big trees (the real axe, so the
/// real drops), shakes ripe berry bushes, and plants saplings so the woods never run out. Everything lands in the crate when he's home at 6pm.
/// </summary>
internal static class Forager
{
    public const int ForagePerDay = 15, ChopsPerDay = 2, StumpsPerDay = 6, PlantsPerDay = 3;

    /// <summary>Forage picked by foragers you watched working today; the evening tally only adds the rest of their quota.</summary>
    private static int pickedLive;

    /// <summary>A forager you can see picks a piece of real forage: into the crate it goes.</summary>
    public static Item? PickLive(GameLocation location, Vector2 tile)
    {
        if (!location.objects.TryGetValue(tile, out var item) || !IsForage(item))
            return null;
        location.objects.Remove(tile);
        CrewSkills.Polish(item, Role.Forager);
        CrewReport.Got(Role.Forager, "forage", item);
        Crate.Store(item);
        pickedLive++;
        return item;
    }
    private static readonly string[] Wilds = { "Forest", "Mountain", "BusStop", "Backwoods", "Woods" };
    private static readonly string[] Saplings = { "1", "2", "3" }; // oak, maple, pine

    /// <summary>One forager's day. Returns (forage picked, trees chopped, trees planted).</summary>
    public static (int Foraged, int Chopped, int Planted) Day(Random rng, double more = 1)
    {
        var wilds = Wilds.Select(Game1.getLocationFromName).OfType<GameLocation>().ToList();
        int foraged = 0, chopped = 0, planted = 0;

        int quota = Math.Max(0, (int)Math.Round(ForagePerDay * more) - pickedLive); // what they picked in front of you counts
        foraged += Math.Min(pickedLive, (int)Math.Round(ForagePerDay * more));
        pickedLive = 0;
        // ground forage and ripe berry bushes (salmonberry, blackberry) share the one daily quota
        var ground = wilds.SelectMany(l => l.objects.Pairs.Where(p => IsForage(p.Value)).Select(p => (Location: l, Tile: p.Key, Bush: (Bush?)null)));
        var bushes = wilds.SelectMany(l => l.largeTerrainFeatures.OfType<Bush>().Where(b => b.readyForHarvest() && b.GetShakeOffItem() != null).Select(b => (Location: l, Tile: b.Tile, Bush: (Bush?)b)));
        var forage = ground.Concat(bushes).OrderBy(_ => rng.Next()).Take(quota).ToList();
        foreach (var (location, tile, bush) in forage)
        {
            Item item;
            if (bush != null)
            {
                item = ItemRegistry.Create(bush.GetShakeOffItem(), 1 + Game1.player.ForagingLevel / 4); // vanilla shake count
                bush.tileSheetOffset.Value = 0; // picked clean
                bush.setUpSourceRect();
            }
            else
            {
                item = location.objects[tile];
                location.objects.Remove(tile);
            }
            CrewSkills.Polish(item, Role.Forager);
            CrewReport.Got(Role.Forager, "forage", item);
            Crate.Store(item);
            foraged++;
        }

        // any wild tree (oak, maple, pine, mahogany, palm, mystic...) grown up or left as a stump; fruit trees are a different
        // type and never touched, tapped trees keep your tapper, saplings are left to grow
        var trees = wilds.SelectMany(l => l.terrainFeatures.Pairs.Where(p => p.Value is Tree { tapped.Value: false } t && (t.growthStage.Value >= Tree.treeStage || t.stump.Value)).Select(p => (Location: l, Tile: p.Key)))
            .OrderBy(_ => rng.Next()).Take((int)Math.Round(ChopsPerDay * more)).ToList();
        foreach (var (location, tile) in trees)
        {
            if (Chop(location, tile))
            {
                chopped++;
                CrewReport.Count(Role.Forager, "trees chopped");
            }
        }

        // big stumps and hollow logs: hardwood (vanilla drops, 2 and 8). Not in the Forest: its log blocks the Secret Woods.
        var clumps = wilds.Where(l => l.Name != "Forest").SelectMany(l => l.resourceClumps.Where(c => c.parentSheetIndex.Value is ResourceClump.stumpIndex or ResourceClump.hollowLogIndex).Select(c => (Location: l, Clump: c)))
            .OrderBy(_ => rng.Next()).Take((int)Math.Round(StumpsPerDay * more)).ToList();
        foreach (var (location, clump) in clumps)
        {
            location.resourceClumps.Remove(clump);
            var wood = ItemRegistry.Create("(O)709", clump.parentSheetIndex.Value == ResourceClump.stumpIndex ? 2 : 8);
            CrewReport.Got(Role.Forager, "hardwood", wood);
            Crate.Store(wood);
            chopped++;
        }

        for (int attempt = 0; attempt < 40 && planted < PlantsPerDay && wilds.Count > 0; attempt++)
        {
            var location = wilds[rng.Next(wilds.Count)];
            var back = location.Map.Layers[0];
            var tile = new Vector2(rng.Next(2, back.LayerWidth - 2), rng.Next(2, back.LayerHeight - 2));
            if (location.doesTileHaveProperty((int)tile.X, (int)tile.Y, "Diggable", "Back") == null || !location.CanItemBePlacedHere(tile) || location.terrainFeatures.ContainsKey(tile))
                continue;
            location.terrainFeatures.Add(tile, new Tree(Saplings[rng.Next(Saplings.Length)], 1));
            planted++;
            CrewReport.Count(Role.Forager, "saplings planted");
        }
        return (foraged, chopped, planted);
    }

    public static bool IsForage(SObject obj) =>
        obj.IsSpawnedObject && !obj.bigCraftable.Value && !obj.IsWeeds() && !obj.IsTwig() && !obj.IsBreakableStone()
        && obj.QualifiedItemId is not ("(O)590" or "(O)SeedSpot"); // not artifact or seed spots

    /// <summary>Fell a tree and its stump with the real axe, playing out the fall (nobody's watching), then sweep the drops into the crate.</summary>
    private static bool Chop(GameLocation location, Vector2 tile)
    {
        for (int hit = 0; hit < 60 && location.terrainFeatures.TryGetValue(tile, out var feature) && feature is Tree tree; hit++)
        {
            Jobs.Hit(location, tile, JobKind.Trees);
            for (int frame = 0; frame < 600 && tree.falling.Value; frame++)
                tree.tickUpdate(Game1.currentGameTime);
        }
        Crate.SweepAround(location, tile, radius: 6);
        return !location.terrainFeatures.ContainsKey(tile);
    }
}
