using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;

namespace ValleyExchange;

/// <summary>
/// Where the hired hands put everything they gather: orange big chests by the cabin. When they're full the hands set up another one.
/// The hands never ship anything: selling goes through you or the salesman.
/// </summary>
internal static class Crate
{
    private const string CrateKey = "evant.ValleyExchange/HarvestCrate";

    /// <summary>Every crate on the farm, oldest spot first.</summary>
    public static List<Chest> All() =>
        Game1.getFarm().objects.Pairs
            .Where(p => p.Value is Chest chest && chest.modData.ContainsKey(CrateKey))
            .OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X)
            .Select(p => (Chest)p.Value)
            .ToList();

    /// <summary>Everything in all the crates.</summary>
    public static IEnumerable<Item> Items() => All().SelectMany(c => c.Items).Where(i => i != null);

    /// <summary>Put an item in the crates, setting up a new crate when they're all full.</summary>
    public static void Store(Item item)
    {
        Item? left = item;
        foreach (var crate in All())
        {
            left = crate.addItem(left);
            if (left == null)
                return;
        }
        for (int tries = 0; left != null && tries < 3 && Place() is { } fresh; tries++)
            left = fresh.addItem(left);
        if (left != null)
        {
            // no free tile for another crate anywhere near: drop it by the farmhouse rather than lose it
            var door = Game1.getFarm().GetMainFarmHouse()?.getPointForHumanDoor() ?? Point.Zero;
            Game1.createItemDebris(left, new Vector2(door.X * 64 + 32, (door.Y + 1) * 64 + 32), -1, Game1.getFarm());
        }
    }

    /// <summary>How many of an item (any quality) are in the crates.</summary>
    public static int Count(string qualifiedId) => Items().Where(i => i.QualifiedItemId == qualifiedId).Sum(i => i.Stack);

    /// <summary>Remove some of an item from the crates, all or nothing.</summary>
    public static bool Take(string qualifiedId, int count)
    {
        if (Count(qualifiedId) < count)
            return false;
        var crates = All();
        foreach (var items in crates.Select(c => c.Items))
        {
            for (int i = 0; i < items.Count && count > 0; i++)
            {
                if (items[i] is not { } stack || stack.QualifiedItemId != qualifiedId)
                    continue;
                int take = Math.Min(count, stack.Stack);
                items[i] = stack.ConsumeStack(take);
                count -= take;
            }
        }
        crates.ForEach(c => c.clearNulls());
        return true;
    }

    /// <summary>Sweep the drops lying around a tile into the crates.</summary>
    public static void SweepAround(GameLocation location, Vector2 tile, int radius)
    {
        var center = tile * 64 + new Vector2(32, 32);
        foreach (var debris in location.debris.Where(d => d.Chunks.Count > 0 && Vector2.Distance(d.Chunks[0].position.Value, center) < 64 * radius).ToList())
        {
            if (Take(location, debris) is { } item)
                Store(item);
        }
    }

    /// <summary>Remove a dropped item from the ground and return it, or null if it isn't an item drop.</summary>
    public static Item? Take(GameLocation location, Debris debris)
    {
        Item? item = debris.item;
        if (item == null && !string.IsNullOrEmpty(debris.itemId.Value) && debris.debrisType.Value is Debris.DebrisType.OBJECT or Debris.DebrisType.RESOURCE)
            item = ItemRegistry.Create(debris.itemId.Value, Math.Max(1, debris.Chunks.Count), debris.itemQuality); // wood/stone chunks: one item per chunk
        if (item != null)
            location.debris.Remove(debris);
        return item;
    }

    /// <summary>Set up a new crate below the first cabin (or the farmhouse), next to the others.</summary>
    private static Chest? Place()
    {
        var farm = Game1.getFarm();
        var anchor = Workers.FirstCabin() ?? farm.GetMainFarmHouse();
        if (anchor == null)
            return null;
        var door = anchor.getPointForHumanDoor();
        for (int dy = 1; dy <= 12; dy++)
        {
            for (int dist = 1; dist <= 6; dist++)
            {
                foreach (int dx in new[] { dist, -dist }) // never directly below the door, keep the path clear
                {
                    var tile = new Vector2(door.X + dx, door.Y + dy);
                    if (!farm.CanItemBePlacedHere(tile))
                        continue;
                    var crate = new Chest(true, tile, "BigChest") { SpecialChestType = Chest.SpecialChestTypes.BigChest };
                    crate.modData[CrateKey] = "1";
                    crate.playerChoiceColor.Value = Color.DarkOrange;
                    farm.objects.Add(tile, crate);
                    bool first = All().Count == 1;
                    Game1.addHUDMessage(new HUDMessage(first ? "Your hands set up an orange crate by the cabin." : "The crates are full: your hands set up another one.", HUDMessage.newQuest_type));
                    return crate;
                }
            }
        }
        return null;
    }
}
