using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using SObject = StardewValley.Object;

namespace ValleyExchange;

/// <summary>
/// What the harvester can pick, one tile at a time, 10g per pick: ripe crops (ground and pots), fruit trees, finished machines
/// (tappers, bee houses, mushroom logs, kegs...) and forage. Picks go into the crates; self-running machines restart.
/// The walking and picking animation live in <see cref="Worker"/>.
/// </summary>
internal static class Harvester
{
    private enum Kind { None, Crop, Pot, Fruit, Machine, Forage }

    public static int PickedToday { get; set; }

    /// <summary>Her fee per pick, after skill discounts.</summary>
    public static int Fee => CrewSkills.Wage(Role.Harvester, Market.HarvestFee);

    public static bool CanPay => Game1.player.Money >= Fee;

    /// <summary>Every tile in the location with something ready to pick.</summary>
    public static List<Vector2> Targets(GameLocation location) =>
        location.terrainFeatures.Keys.Concat(location.objects.Keys).Distinct().Where(tile => KindAt(location, tile) != Kind.None).ToList();

    public static bool IsReady(GameLocation location, Vector2 tile) => KindAt(location, tile) != Kind.None;

    private static Kind KindAt(GameLocation location, Vector2 tile)
    {
        if (location.terrainFeatures.TryGetValue(tile, out var feature))
        {
            if (feature is HoeDirt { crop: not null } dirt && dirt.readyForHarvest())
                return Kind.Crop;
            if (feature is FruitTree { fruit.Count: > 0 })
                return Kind.Fruit;
        }
        if (!location.objects.TryGetValue(tile, out var obj))
            return Kind.None;
        if (obj is IndoorPot { hoeDirt.Value: { crop: not null } potDirt } && potDirt.readyForHarvest())
            return Kind.Pot;
        if (obj is CrabPot or Chest || obj.QualifiedItemId is Oil.QPumpjack or Oil.QRefinery) // Marla's and Duke's jobs
            return Kind.None;
        if (obj.readyForHarvest.Value && obj.heldObject.Value != null)
            return Kind.Machine;
        bool forage = location.IsOutdoors && obj.IsSpawnedObject && !obj.bigCraftable.Value && !obj.IsWeeds() && !obj.IsTwig() && !obj.IsBreakableStone()
            && obj.QualifiedItemId is not ("(O)590" or "(O)SeedSpot"); // not artifact or seed spots
        return forage ? Kind.Forage : Kind.None;
    }

    /// <summary>Pick whatever's ready on this tile, pay the fee, put it in the crates. Returns what was picked (empty if nothing or you're broke).</summary>
    public static List<Item> Pick(GameLocation location, Vector2 tile)
    {
        var picked = new List<Item>();
        var kind = KindAt(location, tile);
        if (kind == Kind.None || !CanPay)
            return picked;
        Game1.player.Money -= Fee;
        CrewReport.Paid(Role.Harvester, Fee);
        PickedToday++;

        switch (kind)
        {
            case Kind.Crop or Kind.Pot:
                var dirt = kind == Kind.Crop ? (HoeDirt)location.terrainFeatures[tile] : ((IndoorPot)location.objects[tile]).hoeDirt.Value;
                // the vanilla iridium-scythe harvest (real quality, extra harvests, regrowth) drops the crop as debris: catch it
                var places = new[] { location, Game1.currentLocation }.Distinct().ToList();
                var before = places.SelectMany(l => l.debris).ToHashSet();
                string? seed = dirt.crop.netSeedIndex.Value;
                if (seed != null && CrewSkills.Has(Role.Harvester, PerkKind.SeedSaver) && Game1.random.NextDouble() < 0.15)
                    picked.Add(ItemRegistry.Create(seed)); // Seed Saver
                if (dirt.crop.harvest((int)tile.X, (int)tile.Y, dirt, null, isForcedScytheHarvest: true))
                    dirt.destroyCrop(false);
                foreach (var place in places)
                {
                    foreach (var debris in place.debris.Where(d => !before.Contains(d)).ToList())
                    {
                        if (Crate.Take(place, debris) is { } item)
                            picked.Add(item);
                    }
                }
                break;
            case Kind.Fruit:
                var tree = (FruitTree)location.terrainFeatures[tile];
                picked.AddRange(tree.fruit);
                tree.fruit.Clear();
                break;
            case Kind.Machine:
                picked.Add(Collect(location, tile, location.objects[tile]));
                break;
            case Kind.Forage:
                picked.Add(location.objects[tile]);
                location.objects.Remove(tile);
                break;
        }
        picked.ForEach(item => CrewSkills.Polish(item, Role.Harvester));
        if (picked.Count > 0 && CrewSkills.Bonus(Role.Harvester))
            picked.Add(picked[0].getOne()); // a little extra in the basket
        picked.ForEach(item => CrewReport.Got(Role.Harvester, "picked", item));
        picked.ForEach(Crate.Store);
        CrewSkills.Work(Role.Harvester, 1);
        Staff.Reports[Role.Harvester] = $"Picked {PickedToday} spots today. It's all in the crate.";
        return picked;
    }

    /// <summary>Take a machine's finished output, then restart it the way the game does when you collect by hand.</summary>
    private static Item Collect(GameLocation location, Vector2 tile, SObject machine)
    {
        var output = machine.heldObject.Value;
        machine.heldObject.Value = null;
        machine.readyForHarvest.Value = false;
        machine.showNextIndex.Value = false;
        if (machine.IsTapper())
        {
            if (location.terrainFeatures.TryGetValue(tile, out var feature) && feature is Tree tree)
                tree.UpdateTapperProduct(machine, output);
        }
        else if (machine.GetMachineData() is { } data
            && MachineDataUtility.TryGetMachineOutputRule(machine, data, MachineOutputTrigger.OutputCollected, output.getOne(), Game1.player, location, out var rule, out _, out _, out _))
        {
            machine.OutputMachine(data, rule, machine.lastInputItem.Value, Game1.player, location, false); // bee houses, mushroom logs, crystalariums...
        }
        return output;
    }

    /// <summary>A building's floors the harvester walks into: its interior, plus the cellar for the farmhouse.</summary>
    public static List<GameLocation> Floors(StardewValley.Buildings.Building building, Farm farm, bool barns = false)
    {
        var floors = new List<GameLocation>();
        if (building.GetIndoors() is { } inside && (barns || inside is not AnimalHouse))
            floors.Add(inside);
        if (building == farm.GetMainFarmHouse() && Game1.getLocationFromName("Cellar") is { } cellar)
            floors.Add(cellar);
        return floors;
    }
}
