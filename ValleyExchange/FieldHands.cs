using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace ValleyExchange;

/// <summary>Hired help for the fields: every morning they clear dead crops, harvest ripe ones into the harvest crate, and water the rest.</summary>
internal static class FieldHands
{
    /// <summary>
    /// Seeds in the crates the field crew may plant here today, most plentiful first: in season (or anywhere seasons don't
    /// matter, like the greenhouse) and able to ripen before the season's out (unless it keeps growing next season).
    /// </summary>
    /// <summary>The game's crop lookups want the bare seed id ("472"); the crate works with qualified ones ("(O)472").</summary>
    public static string Bare(string seed) => ItemRegistry.GetData(seed)?.ItemId ?? seed;

    public static List<string> SeedsFor(GameLocation location)
    {
        if (Workers.State is not { AutoPlant: true })
            return new();
        bool anySeason = location.SeedsIgnoreSeasonsHere();
        var next = (StardewValley.Season)(((int)Game1.season + 1) % 4);
        int daysLeft = 28 - Game1.dayOfMonth;
        return Crate.Items()
            .Where(i => i.Category == StardewValley.Object.SeedsCategory && Game1.cropData.TryGetValue(i.ItemId, out var crop)
                && (anySeason || (crop.Seasons.Contains(Game1.season) && (crop.Seasons.Contains(next) || crop.DaysInPhase.Sum() <= daysLeft))))
            .GroupBy(i => i.QualifiedItemId)
            .OrderByDescending(g => g.Sum(i => i.Stack))
            .Select(g => g.Key)
            .ToList();
    }

    /// <summary>Empty tilled soil the crew can sow right now, in reading order (row by row, left to right) so fields fill in neat rows.</summary>
    public static List<Vector2> Unsown(GameLocation location)
    {
        var seeds = SeedsFor(location);
        if (seeds.Count == 0)
            return new();
        return location.terrainFeatures.Pairs
            .Where(p => p.Value is HoeDirt { crop: null } dirt && seeds.Any(s => dirt.canPlantThisSeedHere(Bare(s))))
            .Select(p => p.Key)
            .OrderBy(t => t.Y).ThenBy(t => t.X)
            .ToList();
    }

    /// <summary>The seed growing next door (left, above, right, below), so a field keeps one crop in a block instead of a patchwork.</summary>
    private static string? NeighborSeed(GameLocation location, Vector2 tile) =>
        new[] { new Vector2(-1, 0), new Vector2(0, -1), new Vector2(1, 0), new Vector2(0, 1) }
            .Select(d => location.terrainFeatures.TryGetValue(tile + d, out var f) && f is HoeDirt { crop: { } crop } ? crop.netSeedIndex.Value : null)
            .FirstOrDefault(id => id != null);

    /// <summary>Sow one tile with the best fitting seed from the crate, for the planting fee. Returns whether it got planted.</summary>
    public static bool Sow(GameLocation location, Vector2 tile)
    {
        int fee = CrewSkills.Wage(Role.Field, Jobs.Price(JobKind.Plant));
        if (Game1.player.Money < fee || !location.terrainFeatures.TryGetValue(tile, out var feature) || feature is not HoeDirt { crop: null } dirt)
            return false;
        string? neighbor = NeighborSeed(location, tile);
        foreach (string seed in SeedsFor(location).OrderBy(s => Bare(s) == neighbor ? 0 : 1)) // same as the neighbors first, then the biggest stack
        {
            if (!dirt.canPlantThisSeedHere(Bare(seed)) || !Crate.Take(seed, 1))
                continue;
            if (!dirt.plant(Bare(seed), Game1.player, false))
            {
                Crate.Store(ItemRegistry.Create(seed)); // didn't take: seed back in the crate
                continue;
            }
            Game1.player.Money -= fee;
            CrewReport.Paid(Role.Field, fee);
            CrewReport.Count(Role.Field, "planted");
            location.localSound("dirtyHit", tile);
            CrewSkills.Work(Role.Field, 1);
            return true;
        }
        return false;
    }

    /// <summary>Planted tiles that still need water today.</summary>
    public static List<Vector2> Dry(GameLocation location) =>
        location.terrainFeatures.Pairs
            .Where(p => p.Value is HoeDirt { crop: { } crop } dirt && !crop.dead.Value && dirt.state.Value != HoeDirt.watered)
            .Select(p => p.Key)
            .ToList();

    /// <summary>One tip of the watering can: the 3x3 around a tile. Returns how many crops got water.</summary>
    public static int WaterAround(GameLocation location, Point center)
    {
        int watered = 0, reach = CrewSkills.Has(Role.Field, PerkKind.WideSpray) ? 2 : 1;
        for (int dx = -reach; dx <= reach; dx++)
        {
            for (int dy = -reach; dy <= reach; dy++)
            {
                if (location.terrainFeatures.TryGetValue(new Vector2(center.X + dx, center.Y + dy), out var feature)
                    && feature is HoeDirt { crop: not null } dirt && dirt.state.Value != HoeDirt.watered)
                {
                    dirt.state.Value = HoeDirt.watered;
                    watered++;
                }
            }
        }
        if (watered > 0)
        {
            CrewSkills.Work(Role.Field, 1);
            CrewReport.Count(Role.Field, "watered", watered);
        }
        return watered;
    }

    /// <summary>Do the morning chores if hired. Returns the crop count, or -1 if they aren't working.</summary>
    public static int Work(MarketState state)
    {
        if (!Market.IsHired(state, Role.Field))
            return -1;

        var fields = new List<(GameLocation Location, Vector2 Tile, HoeDirt Dirt)>();
        Utility.ForEachLocation(location =>
        {
            foreach (var pair in location.terrainFeatures.Pairs)
            {
                if (pair.Value is HoeDirt { crop: not null } dirt)
                    fields.Add((location, pair.Key, dirt));
            }
            return true;
        });
        // shortcut: garden pots aren't tended; add IndoorPot.hoeDirt if anyone grows a lot in pots

        int wage = CrewSkills.Wage(Role.Field, Market.FieldWage(fields.Count) + Market.ExtraHandsWage(Market.Count(state, Role.Field)));
        if (Game1.player.Money < wage)
        {
            Market.SetCount(state, Role.Field, 0);
            Game1.addHUDMessage(new HUDMessage($"Your field hands quit: you couldn't make payroll ({wage:N0}g).", HUDMessage.error_type));
            return -1;
        }
        Game1.player.Money -= wage;
        CrewReport.Paid(Role.Field, wage);

        // the vanilla iridium-scythe harvest (real quality, extra harvests, regrowth) drops the crops as debris; catch it before anyone sees
        var debrisLocations = fields.Select(f => f.Location).Append(Game1.currentLocation).Distinct().ToList();
        var oldDebris = debrisLocations.SelectMany(l => l.debris).ToHashSet();
        int harvested = 0, cleared = 0;
        foreach (var (_, tile, dirt) in fields)
        {
            if (dirt.crop.dead.Value)
            {
                dirt.destroyCrop(false);
                cleared++;
                CrewReport.Count(Role.Field, "dead crops cleared");
                continue;
            }
            if (dirt.readyForHarvest() && !Market.IsHired(state, Role.Harvester)) // Rosie picks them by hand
            {
                harvested++;
                if (dirt.crop.harvest((int)tile.X, (int)tile.Y, dirt, null, isForcedScytheHarvest: true))
                    dirt.destroyCrop(false);
            }
        }

        int items = 0;
        foreach (var location in debrisLocations)
        {
            foreach (var debris in location.debris.Where(d => !oldDebris.Contains(d)).ToList())
            {
                if (Crate.Take(location, debris) is not { } item)
                    continue;
                items += item.Stack;
                CrewSkills.Polish(item, Role.Field);
                CrewReport.Got(Role.Field, "harvested", item);
                Crate.Store(item);
            }
        }

        // (the morning round shows up in the crew report)
        return fields.Count - cleared;
    }
}
