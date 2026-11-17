using Microsoft.Xna.Framework;
using StardewValley;

namespace ValleyExchange;

/// <summary>Work done one tile at a time by a walking hand (the harvester, the brewer): what's waiting, and doing one.</summary>
internal interface ITileWork
{
    List<Vector2> Targets(GameLocation location);
    bool IsReady(GameLocation location, Vector2 tile);
    /// <summary>Do the job on this tile; returns the item(s) to show popping up (empty if nothing happened).</summary>
    List<Item> Act(GameLocation location, Vector2 tile);
    bool CanPay { get; }
    string Sound { get; }
    int PerTenMinutes { get; }
    /// <summary>Also works inside coops and barns.</summary>
    bool Barns => false;
}

internal static class TileWorks
{
    private static readonly ITileWork Harvest = new HarvestWork();

    public static ITileWork? For(Role role) => role switch
    {
        Role.Harvester => Harvest,
        Role.Brewer => MachineTender.Brewer,
        Role.Blacksmith => MachineTender.Smith,
        Role.Artisan => MachineTender.Artisan,
        _ => null,
    };

    private sealed class HarvestWork : ITileWork
    {
        public List<Vector2> Targets(GameLocation location) => Harvester.Targets(location);
        public bool IsReady(GameLocation location, Vector2 tile) => Harvester.IsReady(location, tile);
        public List<Item> Act(GameLocation location, Vector2 tile) => Harvester.Pick(location, tile);
        public bool CanPay => Harvester.CanPay;
        public string Sound => "harvest";
        public int PerTenMinutes => 4;
    }
}
