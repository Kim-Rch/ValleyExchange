using Microsoft.Xna.Framework;
using StardewValley;

namespace ValleyExchange;

/// <summary>
/// The scavenger: at 6pm he digs through every trash can in the valley you haven't checked today (you get first dibs), 10g a
/// can. Same loot the game would give you, and the can counts as checked for the day.
/// </summary>
internal static class Scavenger
{
    public const int FeePerCan = 10;

    /// <summary>Every trash can on the outdoor maps (found from the maps' "Garbage" tile actions, so mod maps count too). Rebuilt daily.</summary>
    private static List<(GameLocation Location, Vector2 Tile, string Id)>? cans;

    public static void Reset() => cans = null;

    private static List<(GameLocation Location, Vector2 Tile, string Id)> Cans()
    {
        if (cans != null)
            return cans;
        cans = new();
        Utility.ForEachLocation(location =>
        {
            if (!location.IsOutdoors || location.Map?.GetLayer("Buildings") is not { } layer)
                return true;
            for (int x = 0; x < layer.LayerWidth; x++)
            {
                for (int y = 0; y < layer.LayerHeight; y++)
                {
                    if (layer.Tiles[x, y] != null && location.doesTileHaveProperty(x, y, "Action", "Buildings") is { } action
                        && action.StartsWith("Garbage ") && action.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: >= 2 } parts)
                        cans.Add((location, new Vector2(x, y), parts[1]));
                }
            }
            return true;
        });
        return cans;
    }

    /// <summary>Where he stands around in a location: next to its trash cans.</summary>
    public static List<Point> CanSpots(GameLocation location) =>
        Cans().Where(c => c.Location == location)
            .Select(c => TilePath.Nearest(location, new Point((int)c.Tile.X, (int)c.Tile.Y + 1), maxDistance: 2))
            .OfType<Point>().Distinct().ToList();

    public static string Day()
    {
        var checkedCans = Game1.netWorldState.Value.CheckedGarbage;
        int opened = 0, found = 0;
        bool broke = false;
        foreach (var (location, _, id) in Cans())
        {
            if (checkedCans.Contains(id))
                continue;
            int fee = CrewSkills.Wage(Role.Scavenger, FeePerCan);
            if (Game1.player.Money < fee)
            {
                broke = true;
                break;
            }
            checkedCans.Add(id);
            Game1.player.Money -= fee;
            CrewReport.Paid(Role.Scavenger, fee);
            opened++;
            if (location.TryGetGarbageItem(id, Game1.player.DailyLuck, out var item, out _, out _, null) && item != null)
            {
                if (CrewSkills.Bonus(Role.Scavenger))
                    item.Stack++; // dug a little deeper
                CrewReport.Got(Role.Scavenger, "trash finds", item);
                Crate.Store(item);
                found++;
            }
        }
        CrewReport.Count(Role.Scavenger, "cans opened", opened);
        CrewSkills.Work(Role.Scavenger, opened);
        return opened == 0 ? "Every can was already picked clean, boss. Somebody beat me to it."
            : $"Opened {opened} cans, found {found} things worth keepin'. In the crate.{(broke ? " Ran outta your coin for the rest." : "")}";
    }
}
