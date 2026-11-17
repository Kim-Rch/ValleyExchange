using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Buildings;
using StardewValley.Extensions;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace ValleyExchange;

/// <summary>The hired crew: lives in the cabins, walks the farm by day, leaves for work off the farm, takes orders when you talk to them.</summary>
internal static class Workers
{
    public const string TextureAsset = "Mods/evant.ValleyExchange/Workers";

    private static readonly List<Worker> Crew = new();
    /// <summary>Targets someone is already working on, so two hands don't chop the same tree.</summary>
    public static readonly HashSet<Vector2> Claimed = new();
    /// <summary>Tiles you've marked for plowing (the plow menu), worked through by any field hand on a plow job.</summary>
    public static readonly HashSet<Vector2> PlowOrder = new();
    /// <summary>Tiles you've marked for planting, and the seed (from the crate) to plant there.</summary>
    public static readonly HashSet<Vector2> PlantOrder = new();
    public static string? PlantSeed;
    public static MarketState? State { get; private set; }
    private static List<Point>? waterSpots;

    public static IEnumerable<Building> Cabins() =>
        Game1.getFarm().buildings.Where(b => b.isCabin && !b.isUnderConstruction());

    public static Building? FirstCabin() => Cabins().FirstOrDefault();

    /// <summary>Beds for hands across all cabins: each bed inside (double = 2), capped by each cabin's upgrade level.</summary>
    public static int Beds() =>
        Cabins().Select(b => b.GetIndoors()).OfType<StardewValley.Locations.Cabin>()
            .Sum(c => Market.CabinBeds(c.furniture.OfType<StardewValley.Objects.BedFurniture>().Sum(bed => bed.bedType == StardewValley.Objects.BedFurniture.BedType.Double ? 2 : 1), c.upgradeLevel)
                + (State is { } s ? (int)Research.Total(s, Effect.ExtraBeds) : 0)); // Logistics: bunk beds

    /// <summary>Match the crew to who's hired. A new day sends everyone back to the cabins; jobs carry over.</summary>
    public static void Refresh(MarketState state, Texture2D texture, bool newDay)
    {
        State = state;
        var old = Crew.ToDictionary(w => w.Name);
        Crew.Clear();
        Claimed.Clear();
        if (newDay)
            waterSpots = null;
        var cabins = Cabins().ToList();
        var farm = Game1.getFarm();
        int slot = 0;
        if (cabins.Count == 0)
        {
            Attach(Game1.currentLocation); // nobody to house: clears any leftover workers
            return;
        }
        foreach (var info in Market.Roles)
        {
            for (int i = 0; i < Market.Count(state, info.Role); i++, slot++)
            {
                string name = info.Names[i];
                if (old.TryGetValue(name, out var existing) && !newDay)
                {
                    Crew.Add(existing);
                    continue;
                }
                var door = cabins[slot % cabins.Count].getPointForHumanDoor();
                if (TilePath.Nearest(farm, new Point(door.X, door.Y + 1)) is not { } start)
                    continue;
                int variant = info.Names.Length > 1 ? i % 3 : name.Length % 3;
                var worker = new Worker(name, info.Role, variant, start, texture);
                if (existing != null)
                    worker.CarryJob(existing);
                Crew.Add(worker);
            }
        }
        Attach(Game1.currentLocation);
    }

    /// <summary>Put the crew on the farm when the player is there (critters are reset on every warp).</summary>
    public static void Attach(GameLocation? location)
    {
        if (location != Game1.getFarm() || location is not Farm farm)
            return;
        farm.critters ??= new List<Critter>();
        farm.critters.RemoveAll(c => c is Worker);
        if (Game1.timeOfDay < Worker.HomeTime)
            farm.critters.AddRange(Crew);
    }

    /// <summary>Every 10 game minutes while you're off the farm: hands with walking work do it off-screen.</summary>
    public static void Offscreen()
    {
        var farm = Game1.getFarm();
        foreach (var worker in Crew)
            worker.Offscreen(farm);
    }

    /// <summary>Where a role works off the farm right now ("Mine", "Beach", "Town", "City"), or null if they're on the farm.</summary>
    public static string? Station(Role role)
    {
        if (State is not { } state)
            return null;
        int time = Game1.timeOfDay;
        bool workHours = time >= 900 && time < 1800;
        return role switch
        {
            Role.Miner => workHours ? "Mine" : null,
            Role.Forager => workHours ? "Forest" : null,
            Role.Adventurer => workHours ? "Mine" : null, // the cave entrance, with his party
            Role.Fisher or Role.Crabber => workHours && !HasWater(Game1.getFarm()) ? "Beach" : null, // no water on the farm: off to the beach
            Role.Salesman when state.TripReturnDay >= 0 => state.TripToCity ? "City" : time < 1800 ? "Town" : null,
            Role.Courier => workHours && state.CourierGifts ? "Town" : null,
            Role.Scavenger => workHours ? "Town" : null,
            Role.Shopkeeper => time is >= 900 and < Shopkeeper.ShopTime ? "Town" : null, // market in the morning, home with the seeds by noon
            Role.TreasureHunter => workHours ? "Mountain" : null,
            Role.Pirate => state.Fleet.AtSea || workHours ? "Beach" : null, // at sea he's gone till the ship's back
            _ => null,
        };
    }

    public static IReadOnlyList<Worker> Members => Crew;

    /// <summary>Sprite sheet blocks: the crew roles first, then the guild's eight hero classes, then the fleet's eight pirates.</summary>
    public static int HeroBlock => Market.Roles.Count;
    public static int PirateBlock => HeroBlock + 8;

    /// <summary>Sprite row facing from one position toward another: 0 down, 1 right, 2 up, 3 left.</summary>
    public static int FacingToward(Vector2 from, Vector2 to)
    {
        var d = to - from;
        return Math.Abs(d.X) > Math.Abs(d.Y) ? (d.X > 0 ? 1 : 3) : (d.Y > 0 ? 0 : 2);
    }

    /// <summary>An item pops up over a hand's head and fades: into the pocket (the crate) it goes.</summary>
    public static void PopItem(GameLocation location, Vector2 position, Item item)
    {
        var data = ItemRegistry.GetDataOrErrorItem(item.QualifiedItemId);
        location.temporarySprites.Add(new TemporaryAnimatedSprite(data.TextureName, data.GetSourceRect(), 900f, 1, 0, position + new Vector2(8, -40), false, false,
            (position.Y + 70) / 10000f, 0.02f, Color.White, 3f, 0f, 0f, 0f)
        {
            motion = new Vector2(0f, -1.2f),
        });
    }

    /// <summary>Draw one 16x16 hand frame (with a shadow) at a world position. Rows: 0-3 walk, 4 work, 5-7 tools.</summary>
    public static void DrawHand(SpriteBatch b, Texture2D texture, Role role, int variant, int row, int frame, Vector2 position) =>
        DrawHand(b, texture, (int)role, variant, row, frame, position);

    /// <summary>Draw from any block of the sheet (crew roles, then the guild's hero classes from block 18).</summary>
    public static void DrawHand(SpriteBatch b, Texture2D texture, int block, int variant, int row, int frame, Vector2 position)
    {
        var source = new Rectangle(variant * 64 + frame * 16, block * 128 + row * 16, 16, 16);
        var screen = Game1.GlobalToLocal(Game1.viewport, position);
        float depth = (position.Y + 64) / 10000f;
        b.Draw(Game1.shadowTexture, screen + new Vector2(32, 60), Game1.shadowTexture.Bounds, Color.White * 0.5f, 0f,
            new Vector2(Game1.shadowTexture.Bounds.Center.X, Game1.shadowTexture.Bounds.Center.Y), 2.5f, SpriteEffects.None, depth - 1e-5f);
        b.Draw(texture, screen, source, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, depth);
    }

    public static void DrawBubble(SpriteBatch b, string? text, int ticks, Vector2 position)
    {
        if (ticks <= 0 || text == null)
            return;
        // a tooltip-style box with the normal font: every character renders (the scroll font can't do '*' and friends), long lines wrap
        string wrapped = Game1.parseText(text, Game1.smallFont, 300);
        var size = Game1.smallFont.MeasureString(wrapped);
        int width = (int)size.X + 32, height = (int)size.Y + 24;
        var screen = Game1.GlobalToLocal(Game1.viewport, position);
        int x = (int)screen.X + 32 - width / 2, y = (int)screen.Y - 12 - height;
        float alpha = Math.Min(1f, ticks / 30f);
        StardewValley.Menus.IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), x, y, width, height, Color.White * alpha, 1f, drawShadow: false, draw_layer: 0.95f);
        Utility.drawTextWithShadow(b, wrapped, Game1.smallFont, new Vector2(x + 16, y + 12), Game1.textColor * alpha, 1f, 0.96f);
    }

    public static Worker? At(Vector2 pixel) =>
        Game1.currentLocation == Game1.getFarm() && Game1.timeOfDay < Worker.HomeTime ? Crew.FirstOrDefault(w => w.Visible && w.Bounds.Contains(pixel)) : null;

    public static void Clear()
    {
        Crew.Clear();
        Claimed.Clear();
        State = null;
        waterSpots = null;
    }

    // ---- places on the farm ----

    public static Point? CropSpot(Farm farm)
    {
        var crops = farm.terrainFeatures.Pairs.Where(p => p.Value is HoeDirt { crop: not null }).Select(p => p.Key).ToList();
        if (crops.Count == 0)
            return null;
        var crop = crops[Game1.random.Next(crops.Count)];
        return TilePath.Nearest(farm, new Point((int)crop.X, (int)crop.Y), maxDistance: 1); // trellis crops block their own tile
    }

    public static Point? BarnSpot(Farm farm)
    {
        var barns = farm.buildings.Where(b => b.GetIndoors() is AnimalHouse).ToList();
        if (barns.Count == 0)
            return null;
        var b = barns[Game1.random.Next(barns.Count)];
        var front = new Point(b.tileX.Value + Game1.random.Next(0, b.tilesWide.Value), b.tileY.Value + b.tilesHigh.Value + Game1.random.Next(0, 2));
        return TilePath.Nearest(farm, front, maxDistance: 2);
    }

    /// <summary>A random walkable tile next to water (cached per day: it scans the whole map).</summary>
    public static Point? WaterSpot(Farm farm)
    {
        if (waterSpots == null)
        {
            waterSpots = new List<Point>();
            var back = farm.Map.Layers[0];
            for (int x = 1; x < back.LayerWidth - 1; x++)
            {
                for (int y = 1; y < back.LayerHeight - 1; y++)
                {
                    bool shore = farm.isWaterTile(x + 1, y) || farm.isWaterTile(x - 1, y) || farm.isWaterTile(x, y + 1) || farm.isWaterTile(x, y - 1);
                    if (shore && TilePath.Passable(farm, new Point(x, y)))
                        waterSpots.Add(new Point(x, y));
                }
            }
        }
        return waterSpots.Count == 0 ? null : waterSpots[Game1.random.Next(waterSpots.Count)];
    }

    public static bool HasWater(Farm farm) => WaterSpot(farm) != null;

    /// <summary>Half the time, a walkable tile next to a random fruit tree (else null, so the harvester also walks the crops).</summary>
    public static Point? TreeSpot(Farm farm)
    {
        var trees = farm.terrainFeatures.Pairs.Where(p => p.Value is FruitTree).Select(p => p.Key).ToList();
        if (trees.Count == 0 || Game1.random.NextDouble() < 0.5)
            return null;
        var tree = trees[Game1.random.Next(trees.Count)];
        return TilePath.Nearest(farm, new Point((int)tree.X, (int)tree.Y + 1), maxDistance: 2);
    }

    /// <summary>A walkable tile next to a random machine this tender runs on the farm.</summary>
    public static Point? MachineSpot(Farm farm, MachineTender tender)
    {
        var kegs = farm.objects.Pairs.Where(p => tender.IsMachine(p.Value)).Select(p => p.Key).ToList();
        if (kegs.Count == 0)
            return null;
        var keg = kegs[Game1.random.Next(kegs.Count)];
        return TilePath.Nearest(farm, new Point((int)keg.X, (int)keg.Y + 1), maxDistance: 2);
    }

    /// <summary>A walkable tile next to a random pumpjack or refinery on the farm.</summary>
    public static Point? RigSpot(Farm farm)
    {
        var rigs = farm.objects.Pairs.Where(p => p.Value.QualifiedItemId is Oil.QPumpjack or Oil.QRefinery).Select(p => p.Key).ToList();
        if (rigs.Count == 0)
            return null;
        var rig = rigs[Game1.random.Next(rigs.Count)];
        return TilePath.Nearest(farm, new Point((int)rig.X, (int)rig.Y + 1), maxDistance: 2);
    }

    /// <summary>Walkable tile in front of a building's door (animal door for barns and coops).</summary>
    public static Point? DoorSpot(Farm farm, Building b)
    {
        Point door;
        if (b.GetIndoors() is AnimalHouse)
            door = new Point(b.tileX.Value + b.animalDoor.X, b.tileY.Value + b.animalDoor.Y);
        else if (b.humanDoor.X >= 0)
            door = b.getPointForHumanDoor();
        else
            return null;
        return TilePath.Nearest(farm, new Point(door.X, door.Y + 1), maxDistance: 1);
    }

    /// <summary>Where hands leave the farm for town (the bus stop exit).</summary>
    public static Point? ExitSpot(Farm farm)
    {
        var warp = farm.warps.FirstOrDefault(w => w.TargetName == "BusStop") ?? farm.warps.FirstOrDefault();
        if (warp == null)
            return null;
        var back = farm.Map.Layers[0];
        var edge = new Point(Math.Clamp(warp.X, 0, back.LayerWidth - 1), Math.Clamp(warp.Y, 0, back.LayerHeight - 1));
        return TilePath.Nearest(farm, edge, maxDistance: 8);
    }

    public static Point? NearSpot(Farm farm, Point? around) =>
        around is { } p ? TilePath.Nearest(farm, new Point(p.X + Game1.random.Next(-3, 4), p.Y + Game1.random.Next(1, 4)), maxDistance: 3) : null;
}

internal enum JobKind { Weeds, Branches, Stones, Trees, Plow, Plant, Grass }

/// <summary>Paid clearing work. Hits use the real axe/pickaxe code, so drops, ore, seeds and falling trees are vanilla.</summary>
internal static class Jobs
{
    public static readonly JobKind[] All = { JobKind.Weeds, JobKind.Branches, JobKind.Stones, JobKind.Grass, JobKind.Trees };

    // shortcut: steel-level tools, so big stumps/boulders (resource clumps) are out of scope
    private static readonly Axe Axe = new() { UpgradeLevel = 2 };
    private static readonly Pickaxe Pickaxe = new() { UpgradeLevel = 2 };
    private static readonly MeleeWeapon Scythe = new("47");

    public static int Price(JobKind kind) => kind switch { JobKind.Plant => 3, JobKind.Plow => 5, JobKind.Grass => 5, JobKind.Weeds => 10, JobKind.Branches => 15, JobKind.Stones => 25, _ => 100 };
    public static string Label(JobKind kind) => kind is JobKind.Plow or JobKind.Plant ? "tiles" : kind.ToString().ToLowerInvariant();
    public static string Verb(JobKind kind) => kind switch { JobKind.Weeds => "Cut weeds", JobKind.Branches => "Clear branches", JobKind.Stones => "Break stones", JobKind.Plow => "Plow land", JobKind.Plant => "Plant seeds", JobKind.Grass => "Cut grass", _ => "Chop trees" };
    /// <summary>Targets cleared per 10 game minutes while you're off the farm (about what they manage on screen).</summary>
    public static int OffscreenQuota(JobKind kind) => kind switch { JobKind.Plow or JobKind.Plant or JobKind.Grass => 6, JobKind.Weeds => 4, JobKind.Branches => 3, JobKind.Stones => 3, _ => 1 };

    /// <summary>Tool sprite row offset: 0 axe, 1 pickaxe, 2 scythe.</summary>
    public static int Tool(JobKind kind) => kind switch { JobKind.Plant => -1, JobKind.Weeds or JobKind.Grass => 2, JobKind.Stones or JobKind.Plow => 1, _ => 0 }; // the pickaxe swing doubles as a hoe; -1 = the role's own work animation

    public static bool IsTarget(Farm farm, Vector2 tile, JobKind kind) =>
        kind == JobKind.Plow ? Workers.PlowOrder.Contains(tile) && AreaMenu.Plowable(farm, tile)
        : kind == JobKind.Plant ? Workers.PlantOrder.Contains(tile) && CanPlant(farm, tile)
        : kind == JobKind.Grass ? farm.terrainFeatures.TryGetValue(tile, out var tuft) && tuft is Grass
        : kind == JobKind.Trees
            ? farm.terrainFeatures.TryGetValue(tile, out var feature) && Choppable(farm, tile, feature)
            : farm.objects.TryGetValue(tile, out var obj) && Matches(obj, kind);

    public static IEnumerable<Vector2> Targets(Farm farm, JobKind kind) =>
        kind == JobKind.Plow ? PlowLeft(farm)
        : kind == JobKind.Plant ? Workers.PlantOrder.Where(t => CanPlant(farm, t)).ToList()
        : kind == JobKind.Grass ? farm.terrainFeatures.Pairs.Where(p => p.Value is Grass).Select(p => p.Key)
        : kind == JobKind.Trees
            ? farm.terrainFeatures.Pairs.Where(p => Choppable(farm, p.Key, p.Value)).Select(p => p.Key)
            : farm.objects.Pairs.Where(p => Matches(p.Value, kind)).Select(p => p.Key);

    /// <summary>Wild trees only: never fruit trees (a different kind of tree), never one with a tapper on it (vanilla or modded).</summary>
    private static bool Choppable(Farm farm, Vector2 tile, TerrainFeature feature) =>
        feature is Tree { tapped.Value: false } && !(farm.objects.TryGetValue(tile, out var obj) && obj.IsTapper());

    private static List<Vector2> PlowLeft(Farm farm)
    {
        var left = Workers.PlowOrder.Where(t => AreaMenu.Plowable(farm, t)).ToList();
        if (left.Count == 0)
            Workers.PlowOrder.Clear();
        return left;
    }

    /// <summary>Empty soil, the chosen seed grows here now, and there's still one in the crate.</summary>
    public static bool CanPlant(GameLocation location, Vector2 tile) =>
        Workers.PlantSeed is { } seed && AreaMenu.Plantable(location, tile) && Crate.Count(seed) > 0
        && ((HoeDirt)location.terrainFeatures[tile]).canPlantThisSeedHere(FieldHands.Bare(seed));

    /// <summary>How a finished target reads in the crew report.</summary>
    public static string Tallied(JobKind kind) => kind switch
    {
        JobKind.Plow => "tiles plowed",
        JobKind.Plant => "tiles planted",
        _ => $"{Label(kind)} cleared",
    };

    /// <summary>What the hand says when a job runs out.</summary>
    public static string Done(JobKind kind, int done, int left) =>
        left == 0 ? $"All done! {(kind == JobKind.Plow ? "Plowed" : kind == JobKind.Plant ? "Planted" : "Cleared")} {done} {Label(kind)}."
        : kind == JobKind.Plant && (Workers.PlantSeed == null || Crate.Count(Workers.PlantSeed) == 0) ? $"Planted {done}, then ran out of seeds."
        : $"Did {done}. Can't get to the rest.";

    private static bool Matches(StardewValley.Object obj, JobKind kind) =>
        kind switch { JobKind.Weeds => obj.IsWeeds(), JobKind.Branches => obj.IsTwig(), _ => obj.IsBreakableStone() };

    /// <summary>One swing at the target, as if you did it (your skills and professions count), without costing you stamina.</summary>
    public static void Hit(GameLocation location, Vector2 tile, JobKind kind)
    {
        if (kind == JobKind.Plant)
        {
            if (Workers.PlantSeed is { } seed && location.terrainFeatures.TryGetValue(tile, out var soil) && soil is HoeDirt dirt
                && dirt.canPlantThisSeedHere(FieldHands.Bare(seed)) && Crate.Take(seed, 1) && dirt.plant(FieldHands.Bare(seed), Game1.player, false))
                location.localSound("dirtyHit", tile);
            return;
        }
        if (kind == JobKind.Plow)
        {
            if (location.terrainFeatures.TryGetValue(tile, out var grass) && grass is Grass)
                location.terrainFeatures.Remove(tile); // cut the grass first
            if (location.makeHoeDirt(tile))
            {
                location.localSound("hoeHit", tile);
                location.temporarySprites.Add(new TemporaryAnimatedSprite(12, tile * 64, Color.White, 8, Game1.random.NextBool(), 50f));
            }
            return;
        }
        if (kind == JobKind.Grass)
        {
            Scythe.lastUser = Game1.player; // hay goes to your silos, like your own scything
            if (location.terrainFeatures.TryGetValue(tile, out var feature) && feature is Grass grass && grass.performToolAction(Scythe, 0, tile))
                location.terrainFeatures.Remove(tile);
            location.localSound("cut", tile);
            return;
        }
        Tool tool = kind == JobKind.Stones ? Pickaxe : Axe; // the axe also cuts weeds, like any tool does
        float stamina = Game1.player.Stamina;
        tool.DoFunction(location, (int)tile.X * 64, (int)tile.Y * 64, 1, Game1.player);
        Game1.player.Stamina = stamina;
    }
}

/// <summary>Tile pathfinding for the workers: walls, buildings, fences, trees, rocks and water block; crops and animals don't.</summary>
internal static class TilePath
{
    public static bool Passable(GameLocation location, Point tile)
    {
        var v = new Vector2(tile.X, tile.Y);
        if (!location.isTileOnMap(v) || !location.isTilePassable(v) || location.isWaterTile(tile.X, tile.Y))
            return false;
        if (location.Map.GetLayer("Back")?.Tiles[tile.X, tile.Y] == null)
            return false; // no floor: the black void around cave maps (the game calls it passable)
        if (location.objects.TryGetValue(v, out var obj) && !obj.isPassable())
            return false;
        // knee-high Brawnies squeeze between trellis crops (beans, hops) that block the farmer
        if (location.terrainFeatures.TryGetValue(v, out var feature) && feature is not StardewValley.TerrainFeatures.HoeDirt && !feature.isPassable())
            return false;
        if (location.getLargeTerrainFeatureAt(tile.X, tile.Y) != null)
            return false;
        if (location.GetFurnitureAt(v) is { } furniture && !furniture.isPassable())
            return false;
        return !location.buildings.Any(b => b.occupiesTile(v)) && !location.resourceClumps.Any(r => r.occupiesTile(tile.X, tile.Y));
    }

    /// <summary>The closest walkable tile to <paramref name="tile"/>, searching outward.</summary>
    public static Point? Nearest(GameLocation location, Point tile, int maxDistance = 4)
    {
        for (int r = 0; r <= maxDistance; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    var p = new Point(tile.X + dx, tile.Y + dy);
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && Passable(location, p))
                        return p;
                }
            }
        }
        return null;
    }
}

/// <summary>
/// A hired hand. Wanders between role spots doing their job animation, pops into buildings, leaves the farm for off-farm work,
/// and (field/ranch) clears the farm one target at a time when ordered. Critters aren't saved, so there's no save risk.
/// </summary>
internal sealed class Worker : Critter
{
    public const int HomeTime = 1900;
    /// <summary>Pathfinding budgets: crossing the farm to wander, and leaving it (big modded farms need room).</summary>
    private const int FarmTrip = 12_000, LongTrip = 30_000;
    private static readonly string[] BackSoon = { "Be right back.", "Forgot my hat.", "Water break.", "Gotta grab somethin'.", "Checkin' inside." };
    /// <summary>Walking pace, quicker with the role's speed perks.</summary>
    private float Speed => 1.6f * CrewSkills.Faster(this.Role);
    private const int SwingTicks = 45, MaxHits = 40, SiteTicks = 300;
    private static readonly string[] Greetings = { "Howdy, boss.", "Hot one today, ain't it?", "What needs doin'?", "Fine spread you got here.", "Yessir?" };

    public string Name { get; }
    public Role Role { get; }
    public int Variant { get; }
    private readonly Texture2D texture;
    private readonly Queue<Point> path = new();
    private int waitTicks, workTicks, insideTicks, facing; // facing: 0 down, 1 right, 2 up, 3 left (sprite row)
    private bool away, headingOut, enteringBuilding;
    private int buildingCooldown; // activities to go before popping into a building again (no in-out-in blinking)
    private string? visitName;    // the building they went into, for the tracker
    private Point? awayDoor; // where they stepped off the farm map, to come back the same way

    // clearing job (field and ranch crews)
    private JobKind? job;
    private int jobDone, hits, swingTimer, swingFrame;
    private Vector2? target;
    private readonly HashSet<Vector2> unreachable = new();
    private int unreachableHour = -1;
    private readonly List<(Vector2 Tile, int Ticks)> sites = new(); // finished targets whose drops may still be landing (falling trees)

    private string? bubble;
    private int bubbleTicks, chatterTicks = Game1.random.Next(600, 4000), greetCooldown;

    public Worker(string name, Role role, int variant, Point start, Texture2D texture)
    {
        this.Name = name;
        this.Role = role;
        this.Variant = variant;
        this.position = new Vector2(start.X, start.Y) * 64;
        this.texture = texture;
        this.waitTicks = Game1.random.Next(30, 120);
    }

    public bool Away => this.away;

    /// <summary>Working on an order you gave (plowing, clearing).</summary>
    public bool HasJob => this.job != null;

    /// <summary>What this hand is doing and where, in a few words (the crew tracker).</summary>
    public string Status
    {
        get
        {
            if (Game1.timeOfDay >= HomeTime)
                return "Home for the night";
            if (Travel.InTransit(this))
                return Travel.Describe(this);
            if (Workers.Station(this.Role) is { } leaving && !this.away)
                return $"Heading out to {Travel.PlaceName(leaving)}";
            if (Lands.LandOf(this) is { } nextLand && !this.away)
                return $"Heading to {nextLand.DisplayName}";
            if (Workers.Station(this.Role) is { } station)
                return station switch
                {
                    "Mine" => "Working the mine entrance",
                    "Mountain" => "Diggin' around the mountain",
                    "Beach" when this.Role == Role.Pirate => Workers.State?.Fleet is { AtSea: true } fleet ? $"At sea till {GuildPlay.Date(fleet.ReturnDay)}" : "At the docks with the crew",
                    "Beach" => "Working at the beach",
                    "Forest" => "Out in Cindersap Forest",
                    "Town" => this.Role switch { Role.Courier => "Running gifts around town", Role.Scavenger => "Diggin' through the trash", Role.Shopkeeper => "Hagglin' at the shops", _ => "Selling in Pelican Town" },
                    "City" => "Selling in the city",
                    _ => $"At {Travel.PlaceName(station)}",
                };
            if (Lands.LandOf(this) is { } land)
                return $"Working in {land.DisplayName}";
            if (this.insideFloors.Count > 0)
                return $"Picking inside {this.insideFloors.Peek().DisplayName}";
            if (this.insideTicks > 0)
                return this.visiting != null ? $"Watering inside {this.visiting.DisplayName}" : $"Inside {this.visitName ?? "a building"}";
            if (this.job is { } kind)
                return $"{Jobs.Verb(kind)} ({this.jobDone} done)";
            if (this.pick != null)
                return this.path.Count > 0 ? "Heading to pick something" : this.Role switch { Role.Brewer => "Tending a keg", Role.Blacksmith => "At the furnace", Role.Artisan => "Running a machine", _ => "Picking" };
            if (this.workTicks > 0)
                return this.Role switch
                {
                    Role.Field => "Watering",
                    Role.Ranch => "Tending the animals",
                    Role.Fisher => "Fishing",
                    Role.Crabber => "Checking traps",
                    Role.Chef => "Cooking",
                    Role.Oil => "Working the rigs",
                    Role.Brewer => "Tending the kegs",
                    Role.Blacksmith => "Hammerin'",
                    Role.Artisan => "Crafting",
                    Role.Banker => "Countin' your money",
                    Role.Scientist => "Experimentin'",
                    Role.Scareboy => "Spookin' crows",
                    _ => "Working",
                };
            return this.path.Count > 0 ? "Walking" : "Taking a breather";
        }
    }

    /// <summary>The map they're on, for the tracker (null if not on a map: in the city).</summary>
    public string Where =>
        Game1.timeOfDay >= HomeTime ? "Cabin"
        : Travel.InTransit(this) ? Travel.CurrentMap(this) ?? "On the road"
        : Workers.Station(this.Role) is { } station ? Travel.PlaceName(station)
        : Lands.LandOf(this)?.DisplayName ?? "Farm";

    public bool Visible => !this.away && this.insideTicks == 0 && this.insideFloors.Count == 0;
    public Rectangle Bounds => new((int)this.position.X, (int)this.position.Y, 64, 64);
    private Point Tile => new((int)Math.Round(this.position.X / 64), (int)Math.Round(this.position.Y / 64));
    private bool Crew => this.Role is Role.Field or Role.Ranch;

    public void CarryJob(Worker from)
    {
        this.job = from.job;
        this.jobDone = from.jobDone;
    }

    // ---- talking ----

    public void Talk()
    {
        this.facing = Workers.FacingToward(this.position, Game1.player.Position); // look at the boss
        var state = Workers.State;
        if (this.Role == Role.Salesman && state != null)
        {
            this.TalkSales(state);
            return;
        }
        if (this.Role == Role.Fisher && state != null)
        {
            Staff.TalkFisher(state, this.Name);
            return;
        }
        if (this.Role == Role.Groundskeeper && state != null)
        {
            this.TalkGrounds(state);
            return;
        }
        if (this.Role == Role.Pirate && state != null)
        {
            GuildPlay.CaptainTalk(state, this.Name);
            return;
        }
        if (this.Role == Role.Shopkeeper && state != null)
        {
            Shopkeeper.Talk(state, this.Name, line => this.Say(line));
            return;
        }
        if (this.Role == Role.Adventurer && state != null)
        {
            GuildPlay.Talk(state, this.Name);
            return;
        }
        if (this.Role == Role.Scientist && state != null)
        {
            string report = Staff.Reports.TryGetValue(Role.Scientist, out var lab) ? lab : $"{state.ResearchPoints:N0} research points in the bank, boss.";
            var options = new[] { new Response("lab", "Open the research lab"), new Response("donate", "Take spare museum samples from the crate") };
            Personas.Ask(this.Name, this.Role, $"{this.Name}: \"{report}\"", options, answer =>
            {
                if (answer == "lab")
                    WhenFree.Run(() => Game1.activeClickableMenu = new ResearchMenu(state)); // once Doc's dialogue is fully closed
                else
                    DelayedAction.functionAfterDelay(() => Game1.drawObjectDialogue($"{this.Name}: \"{Science.Donate(state)}\""), 50);
            });
            return;
        }
        if (this.Role == Role.Banker && state != null)
        {
            Banker.Talk(state, this.Name, line => this.Say(line));
            return;
        }
        if (this.Role == Role.Courier && state != null)
        {
            string report = Staff.Reports.TryGetValue(Role.Courier, out var done) ? done : "Satchel's packed, boss. Back from my route at six.";
            var toggle = new Response("gifts", state.CourierGifts ? "Stop the gift runs" : "Start the gift runs again");
            Personas.Ask(this.Name, this.Role, $"{this.Name}: \"{report}\"", new[] { toggle }, _ =>
            {
                state.CourierGifts = !state.CourierGifts;
                this.Say(state.CourierGifts ? "Back on the route, boss!" : "Satchel's hung up. Holler if ya need me.");
                Game1.playSound(state.CourierGifts ? "purchaseClick" : "trashcan");
            });
            return;
        }
        if (!this.Crew)
        {
            string line = Staff.Reports.TryGetValue(this.Role, out var report) ? report : Greetings[Game1.random.Next(Greetings.Length)];
            Personas.Ask(this.Name, this.Role, $"{this.Name} the {Market.RoleInfo(this.Role).Title.ToLower()}: \"{line}\"");
            return;
        }

        var farm = Game1.getFarm();
        string greeting = this.job is { } current
            ? $"Still on them {Jobs.Label(current)}, boss. {this.jobDone} done, {Jobs.Targets(farm, current).Count()} to go."
            : Greetings[Game1.random.Next(Greetings.Length)];
        var answers = new List<Response>();
        foreach (var kind in Jobs.All)
        {
            int count = Jobs.Targets(farm, kind).Count();
            if (count > 0)
                answers.Add(new Response(kind.ToString(), $"{Jobs.Verb(kind)}: {Jobs.Price(kind)}g each ({count} on the farm)"));
        }
        if (this.Role == Role.Field)
        {
            answers.Add(new Response("plow", $"Plow new land: {Jobs.Price(JobKind.Plow)}g a tile (pick the spot)"));
            answers.Add(new Response("plant", $"Plant seeds: {Jobs.Price(JobKind.Plant)}g a tile (seeds from the crate)"));
            answers.Add(new Response("autoplant", Workers.State?.AutoPlant == true ? "Auto-planting is ON: stop it" : "Auto-planting is OFF: start it"));
        }
        if (this.job != null)
            answers.Add(new Response("stop", "Stop that job"));

        Personas.Ask(this.Name, this.Role, $"{this.Name}: \"{greeting}\"", answers, answer =>
        {
            if (answer == "plow")
                WhenFree.Run(() => this.PickPlowArea(farm)); // after this dialogue has closed
            else if (answer == "plant")
                WhenFree.Run(() => this.PickSeed(farm));
            else if (answer == "autoplant" && Workers.State is { } crewState)
            {
                crewState.AutoPlant = !crewState.AutoPlant;
                this.Say(crewState.AutoPlant ? "We'll sow any empty soil, boss." : "Hands off the soil. Got it.");
                Game1.playSound(crewState.AutoPlant ? "purchaseClick" : "trashcan");
            }
            else if (answer == "stop")
            {
                this.StopJob($"Alright. {this.jobDone} done.");
                Game1.playSound("trashcan");
            }
            else if (Enum.TryParse(answer, out JobKind kind))
            {
                this.StopJob(null);
                this.job = kind;
                Lands.Invalidate(); // he's busy here now: re-spread the others
                this.jobDone = 0;
                this.Say($"On it, boss! {Jobs.Price(kind)}g a pop.");
                Game1.playSound("purchaseClick");
            }
        });
    }

    /// <summary>"How big, boss?": pick the field on the map, confirm the price, and he gets the hoe.</summary>
    private void PickPlowArea(Farm farm)
    {
        Game1.activeClickableMenu = new AreaMenu(farm, AreaMenu.Plowable, Jobs.Price(JobKind.Plow), "plowed", tiles => DelayedAction.functionAfterDelay(() =>
        {
            int cost = tiles.Count * Jobs.Price(JobKind.Plow);
            var answers = new[] { new Response("yes", $"Plow it: {cost:N0}g"), new Response("no", "Forget it") };
            Game1.currentLocation.createQuestionDialogue($"{this.Name}: \"{tiles.Count} tiles, {Jobs.Price(JobKind.Plow)}g a tile, paid as I go. Deal?\"", answers, (_, answer) =>
            {
                if (answer != "yes")
                    return;
                Workers.PlowOrder.UnionWith(tiles);
                this.StopJob(null);
                this.job = JobKind.Plow;
                Lands.Invalidate();
                this.jobDone = 0;
                this.Say("Gettin' the hoe!");
                Game1.playSound("purchaseClick");
            }, null);
        }, 50));
    }

    /// <summary>"What're we plantin', boss?": an in-season seed from the crate, then where.</summary>
    private void PickSeed(Farm farm)
    {
        bool anySeason = farm.SeedsIgnoreSeasonsHere();
        var seeds = Crate.Items()
            .Where(i => i.Category == StardewValley.Object.SeedsCategory && Game1.cropData.TryGetValue(i.ItemId, out var crop) && (anySeason || crop.Seasons.Contains(Game1.season)))
            .GroupBy(i => i.QualifiedItemId)
            .Select(g => (Id: g.Key, Name: g.First().DisplayName, Count: g.Sum(i => i.Stack)))
            .OrderByDescending(s => s.Count)
            .Take(8)
            .ToList();
        if (seeds.Count == 0)
        {
            Game1.drawObjectDialogue($"{this.Name}: \"No seeds for this season in the crate, boss. Drop some in and I'll get plantin'.\"");
            return;
        }
        var answers = seeds.Select(s => new Response(s.Id, $"{s.Name} ({s.Count} in the crate)")).Append(new Response("bye", "Never mind")).ToArray();
        Game1.currentLocation.createQuestionDialogue($"{this.Name}: \"What're we plantin', boss?\"", answers, (_, seed) =>
        {
            if (seed == "bye")
                return;
            WhenFree.Run(() => this.PickPlantArea(farm, seed));
        }, null);
    }

    private void PickPlantArea(Farm farm, string seed)
    {
        string name = ItemRegistry.GetDataOrErrorItem(seed).DisplayName;
        bool Fits(GameLocation location, Vector2 tile) =>
            AreaMenu.Plantable(location, tile) && ((HoeDirt)location.terrainFeatures[tile]).canPlantThisSeedHere(FieldHands.Bare(seed));
        Game1.activeClickableMenu = new AreaMenu(farm, Fits, Jobs.Price(JobKind.Plant), "planted", tiles => DelayedAction.functionAfterDelay(() =>
        {
            int have = Crate.Count(seed), planting = Math.Min(tiles.Count, have);
            var answers = new[] { new Response("yes", $"Plant {planting}: {planting * Jobs.Price(JobKind.Plant):N0}g"), new Response("no", "Forget it") };
            string short_ = have < tiles.Count ? $" Only {have} seeds in the crate, so I'll do {have}." : "";
            Game1.currentLocation.createQuestionDialogue($"{this.Name}: \"{tiles.Count} tiles of {name}, {Jobs.Price(JobKind.Plant)}g a tile.{short_} Deal?\"", answers, (_, answer) =>
            {
                if (answer != "yes")
                    return;
                if (Workers.PlantSeed != seed)
                    Workers.PlantOrder.Clear(); // one seed at a time: a new seed replaces the old order
                Workers.PlantSeed = seed;
                Workers.PlantOrder.UnionWith(tiles);
                this.StopJob(null);
                this.job = JobKind.Plant;
                this.jobDone = 0;
                Lands.Invalidate();
                this.Say("Seeds in hand, boss!");
                Game1.playSound("purchaseClick");
            }, null);
        }, 50));
    }

    /// <summary>What the groundskeepers keep clear: toggle each kind of mess.</summary>
    private void TalkGrounds(MarketState state)
    {
        var farm = Game1.getFarm();
        var options = Jobs.All.Select(k => new Response(k.ToString(),
            $"{Jobs.Verb(k)}: {(state.ClearKinds.Contains(k.ToString()) ? "ON" : "off")} ({Jobs.Targets(farm, k).Count()} on the farm){(k == JobKind.Trees ? " - every wild tree!" : "")}")).ToList();
        string doing = this.job is { } current ? $"On the {Jobs.Label(current)} right now, {this.jobDone} done." : "Farm's lookin' tidy, boss.";
        Personas.Ask(this.Name, this.Role, $"{this.Name}: \"{doing} What should we keep clear?\"", options, answer =>
        {
            if (!Enum.TryParse(answer, out JobKind kind))
                return;
            bool on = !state.ClearKinds.Remove(kind.ToString());
            if (on)
                state.ClearKinds.Add(kind.ToString());
            else
            {
                foreach (var hand in Workers.Members.Where(w => w.Role == Role.Groundskeeper && w.job == kind))
                    hand.StopJob(null);
            }
            this.Say(on ? $"{Jobs.Verb(kind)}? Consider it done." : $"Leavin' the {Jobs.Label(kind)} be.");
            Game1.playSound(on ? "purchaseClick" : "trashcan");
        });
    }

    /// <summary>Per-job fee: the groundskeepers are on a daily wage.</summary>
    private int Fee(JobKind kind) => this.Role == Role.Groundskeeper ? 0 : Jobs.Price(kind);

    /// <summary>A groundskeeper picks the next mess to clear (skipping kinds he just gave up on for a couple of hours).</summary>
    private void PickChore(Farm farm, bool now = false)
    {
        if (Workers.State is not { } state || (!now && --this.choreScan > 0))
            return;
        this.choreScan = 300;
        foreach (var kind in Jobs.All.Where(k => state.ClearKinds.Contains(k.ToString()) && Game1.timeOfDay >= this.stuckUntil.GetValueOrDefault(k)))
        {
            if (Jobs.Targets(farm, kind).Any(t => !Workers.Claimed.Contains(t)))
            {
                this.job = kind;
                this.jobDone = 0;
                return;
            }
        }
    }

    /// <summary>A job ran out: a groundskeeper just moves on (quietly if he did nothing), a paid hand reports.</summary>
    private void EndJob(Farm farm, JobKind kind)
    {
        int left = Jobs.Targets(farm, kind).Count();
        if (this.Role != Role.Groundskeeper)
        {
            this.StopJob(Jobs.Done(kind, this.jobDone, left));
            return;
        }
        if (left > 0)
            this.stuckUntil[kind] = Game1.timeOfDay + 200; // can't reach the rest: try again in two hours
        this.StopJob(this.jobDone > 0 ? $"{this.jobDone} {Jobs.Label(kind)} cleared." : null);
    }

    private void TalkSales(MarketState state)
    {
        if (state.TripReturnDay >= 0)
        {
            Personas.Ask(this.Name, this.Role, $"{this.Name}: \"Already out with today's load, boss. I'll settle up when I'm back.\"");
            return;
        }
        var (items, value) = Staff.CrateGoods(state);
        if (items == 0)
        {
            Personas.Ask(this.Name, this.Role, $"{this.Name}: \"{(Staff.Reports.TryGetValue(Role.Salesman, out var r) ? r + " " : "")}Crate's empty, boss. Fill it up and I'll sell it.\"");
            return;
        }
        var (markup, cutRate) = Staff.SalesTerms();
        var town = Market.SalesPayout(value, city: false, markup, cutRate);
        var city = Market.SalesPayout(value, city: true, markup, cutRate);
        var answers = new[]
        {
            new Response("town", $"Work the town today: up to ~{town.Net:N0}g (+20%, he keeps {Skills.Pct(town.Cut / (double)Math.Max(1, town.Gross))})"),
            new Response("city", $"Take it to the city: up to ~{city.Net:N0}g (+50%, he keeps {Skills.Pct(city.Cut / (double)Math.Max(1, city.Gross))}), back tomorrow"),
        };
        Personas.Ask(this.Name, this.Role, $"{this.Name}: \"{items} items worth {value:N0}g at the bin. Can't promise they'll buy it all. Where to, boss?\"", answers, answer =>
        {
            if (answer is not ("town" or "city"))
                return;
            this.Say(Staff.Send(state, answer == "city", Game1.Date.TotalDays), 360);
            Game1.playSound("purchaseClick");
        });
    }

    /// <summary>Random personality lines every minute or so, and a hello when you walk up.</summary>
    private void Chat()
    {
        if (this.greetCooldown > 0)
            this.greetCooldown--;
        if (!this.Visible || this.bubbleTicks > 0)
            return;
        if (this.greetCooldown == 0 && Vector2.Distance(Game1.player.Position, this.position) < 64 * 2.5f)
        {
            this.Say(Chatter.Greeting(), 150);
            this.greetCooldown = 3600; // once a minute at most
        }
        else if (--this.chatterTicks <= 0)
        {
            this.Say(Chatter.Line(this.Name, this.Role));
            this.chatterTicks = Game1.random.Next(2400, 6000); // 40-100 seconds
        }
    }

    private void Say(string text, int ticks = 240)
    {
        this.bubble = text;
        this.bubbleTicks = ticks;
    }

    private void StopJob(string? message)
    {
        if (this.target is { } t)
            Workers.Claimed.Remove(t);
        this.target = null;
        this.job = null;
        this.path.Clear();
        this.unreachable.Clear();
        this.hits = 0;
        if (message != null)
            this.Say(message);
    }

    // ---- updating (only runs while you're on the farm: on-farm work pauses when you leave) ----

    /// <summary>Off-farm roles leave in the morning and come back in the evening; the salesman leaves when he has goods to sell.</summary>
    private bool ShouldBeAway(Farm farm) => Workers.Station(this.Role) != null || Lands.LandOf(this) != null;

    public override bool update(GameTime time, GameLocation environment)
    {
        if (!Game1.shouldTimePass())
            return false; // paused, a menu or a dialogue is open (talking to them, too): freeze
        if (Game1.timeOfDay >= HomeTime)
        {
            if (this.target is { } t)
                Workers.Claimed.Remove(t);
            this.target = null;
            this.ReleasePick();
            this.path.Clear();
            return true; // shortcut: they vanish at 7pm instead of walking home
        }
        if (this.bubbleTicks > 0)
            this.bubbleTicks--;
        if (environment is not Farm farm)
            return false;
        this.Chat();
        this.CollectDrops(farm);

        if (this.insideTicks > 0)
        {
            this.insideTicks--;
            if (this.visiting != null && this.insideTicks % 75 == 0)
                this.WaterInside();
            return false;
        }
        if (this.ShouldBeAway(farm))
        {
            this.UpdateLeaving(farm);
            return false;
        }
        if (this.away && Travel.InTransit(this))
            return false; // still on the road home
        if (this.away)
        {
            // back from town / the mines / a building: walk in from where they left
            this.away = this.headingOut = false;
            if ((this.awayDoor ?? Workers.ExitSpot(farm)) is { } exit)
                this.position = new Vector2(exit.X, exit.Y) * 64;
            this.awayDoor = null;
            this.path.Clear();
            this.workTicks = 0;
            if (Workers.State is { } st)
                Staff.Evening(st, Game1.Date.TotalDays); // the haul comes home with them
            if (this.Role is Role.Salesman or Role.Adventurer or Role.Miner or Role.Fisher or Role.Crabber or Role.Forager or Role.Courier
                or Role.Scavenger or Role.TreasureHunter or Role.Pirate or Role.Shopkeeper)
                this.Say(Staff.HomecomingLine(this.Role) ?? (this.Role == Role.Salesman ? "Back from the road!" : "Home from work, boss."), 360);
        }

        if (this.Role == Role.Groundskeeper && this.job == null)
            this.PickChore(farm);
        if (this.job is { } kind)
            this.UpdateJob(farm, kind);
        else if (TileWorks.For(this.Role) is { } work)
            this.UpdateHarvest(farm, work);
        else
            this.UpdateWander(farm);
        return false;
    }

    private void UpdateLeaving(Farm farm)
    {
        if (this.away)
            return;
        if (this.target is { } t)
        {
            Workers.Claimed.Remove(t);
            this.target = null;
        }
        if (!this.headingOut)
        {
            this.headingOut = true;
            this.workTicks = 0;
            this.path.Clear();
            var land = Lands.LandOf(this);
            this.awayDoor = land != null ? Lands.Entrance(farm, land) : Workers.ExitSpot(farm);
            var route = this.awayDoor is { } e ? PathGrid.Find(p => TilePath.Passable(farm, p), this.Tile, e, maxNodes: LongTrip, partial: true) : null;
            if (route == null || route.Count == 0)
                this.away = true; // no way out on foot: just poof
            else
                route.ForEach(this.path.Enqueue);
            this.Say(land switch
            {
                { ParentBuilding: not null } or StardewValley.Locations.FarmHouse or StardewValley.Locations.Cellar => "Work to do inside.",
                { Name: "IslandWest" } => "Off to the island!",
                not null => "Headin' to the other fields.",
                _ => this.Role switch
                {
                    Role.Miner => "Off to the mines!",
                    Role.Adventurer => "Off to the guild!",
                    Role.Salesman => "Hittin' the road!",
                    Role.Forager => "Off to the woods!",
                    Role.Courier => "Mail's goin' out!",
                    Role.Scavenger => "Trash day, boss!",
                    Role.TreasureHunter => "X marks the spot!",
                    Role.Pirate => "Ship ahoy!",
                    Role.Shopkeeper => "Off to market!",
                    _ => "Headin' to the beach.",
                },
            });
            return;
        }
        if (this.path.Count == 0 || this.Walk())
        {
            this.away = true;
            Travel.Sync(this); // on the road
        }
    }

    /// <summary>Leave without the walk (you aren't on the farm to see it).</summary>
    public void LeaveOffscreen()
    {
        if (this.away)
            return;
        if (this.target is { } t)
            Workers.Claimed.Remove(t);
        this.target = null;
        this.ReleasePick();
        this.path.Clear();
        this.awayDoor = Workers.ExitSpot(Game1.getFarm());
        this.away = this.headingOut = true;
    }

    // ---- off-screen: while you're away from the farm, work happens every 10 game minutes without the walking ----

    /// <summary>Called every 10 in-game minutes while you're off the farm (the farm map doesn't run then).</summary>
    public void Offscreen(Farm farm)
    {
        if (this.away || Game1.timeOfDay >= HomeTime || Lands.LandOf(this) != null) // land hands work off-screen in Lands
            return;
        // drop whatever walk/animation was in progress; they'll pick up fresh when you're back
        this.path.Clear();
        this.ReleasePick();
        this.insideFloors.Clear();
        this.doorFloors = null;
        this.harvestRoute = false;
        this.workTicks = this.insideTicks = 0;
        if (this.target is { } t)
            Workers.Claimed.Remove(t);
        this.target = null;
        this.hits = 0;

        this.visiting = null;
        if (this.Role == Role.Groundskeeper && this.job == null)
            this.PickChore(farm, now: true);
        if (this.job is { } kind)
            this.OffscreenJob(farm, kind);
        else if (TileWorks.For(this.Role) is { } offWork)
            this.OffscreenHarvest(farm, offWork);
        else if (this.Role == Role.Field)
            this.OffscreenWater(farm);
    }

    /// <summary>Two stops' worth (3x3 each) per 10 minutes: the farm first, then sheds and the greenhouse.</summary>
    private void OffscreenWater(Farm farm)
    {
        for (int stop = 0; stop < Math.Round(2 * CrewSkills.Faster(Role.Field)); stop++)
        {
            var me = this.Tile;
            var dry = FieldHands.Dry(farm).OrderBy(t => Math.Abs(t.X - me.X) + Math.Abs(t.Y - me.Y)).Take(1).ToList();
            if (dry.Count > 0)
            {
                FieldHands.WaterAround(farm, new Point((int)dry[0].X, (int)dry[0].Y));
                this.StandNear(farm, dry[0]);
                continue;
            }
            if (FieldHands.Unsown(farm) is { Count: > 0 } unsown)
            {
                if (FieldHands.Sow(farm, unsown[0]))
                    FieldHands.WaterAround(farm, new Point((int)unsown[0].X, (int)unsown[0].Y));
                continue;
            }
            if (this.DryBuilding(farm)?.GetIndoors() is not { } inside)
                return; // everything's wet and sown
            var tile = FieldHands.Dry(inside)[0];
            FieldHands.WaterAround(inside, new Point((int)tile.X, (int)tile.Y));
        }
    }

    /// <summary>Inside a shed or greenhouse: water the next dry 3x3, or head back out when it's all wet.</summary>
    private void WaterInside()
    {
        if (this.visiting == null)
            return;
        var dry = FieldHands.Dry(this.visiting);
        if (dry.Count == 0)
        {
            this.insideTicks = 0; // done in here
            this.visiting = null;
            return;
        }
        FieldHands.WaterAround(this.visiting, new Point((int)dry[0].X, (int)dry[0].Y));
    }

    private void OffscreenHarvest(Farm farm, ITileWork work)
    {
        for (int i = 0; i < Math.Round(work.PerTenMinutes * CrewSkills.Faster(this.Role)) && work.CanPay; i++)
        {
            var me = this.Tile;
            var next = work.Targets(farm).Where(c => !Workers.Claimed.Contains(c)).OrderBy(c => Math.Abs(c.X - me.X) + Math.Abs(c.Y - me.Y)).Take(1).ToList();
            if (next.Count > 0)
            {
                work.Act(farm, next[0]);
                this.StandNear(farm, next[0]);
                continue;
            }
            var floor = farm.buildings.Where(b => !b.isCabin && !b.isUnderConstruction())
                .SelectMany(b => Harvester.Floors(b, farm, work.Barns))
                .FirstOrDefault(f => work.Targets(f).Count > 0);
            if (floor == null)
                return; // all picked
            work.Act(floor, work.Targets(floor)[0]);
        }
    }

    private void OffscreenJob(Farm farm, JobKind kind)
    {
        for (int i = 0; i < Math.Round(Jobs.OffscreenQuota(kind) * CrewSkills.Faster(this.Role)); i++)
        {
            if (Game1.player.Money < this.Fee(kind))
            {
                this.StopJob("Boss, you're outta cash. I'm done.");
                Game1.addHUDMessage(new HUDMessage($"{this.Name} stopped: you can't pay {Jobs.Price(kind)}g.", HUDMessage.error_type));
                return;
            }
            var me = this.Tile;
            var next = Jobs.Targets(farm, kind)
                .Where(c => !Workers.Claimed.Contains(c) && !this.unreachable.Contains(c))
                .OrderBy(c => Math.Abs(c.X - me.X) + Math.Abs(c.Y - me.Y))
                .Take(1)
                .ToList();
            if (next.Count == 0)
            {
                this.EndJob(farm, kind);
                return;
            }
            var t = next[0];
            for (int hit = 0; hit < MaxHits && Jobs.IsTarget(farm, t, kind); hit++)
            {
                Jobs.Hit(farm, t, kind);
                // nobody's watching the farm, so the falling animation won't run by itself: play it out so the tree lands and drops its wood
                if (farm.terrainFeatures.TryGetValue(t, out var feature) && feature is Tree tree)
                {
                    for (int frame = 0; frame < 600 && tree.falling.Value; frame++)
                        tree.tickUpdate(Game1.currentGameTime);
                }
            }
            if (Jobs.IsTarget(farm, t, kind))
            {
                this.unreachable.Add(t); // too tough for steel tools
                continue;
            }
            int fee = Math.Min(this.Fee(kind), Game1.player.Money);
            Game1.player.Money -= fee;
            CrewReport.Paid(this.Role, fee);
            CrewReport.Count(this.Role, Jobs.Tallied(kind));
            this.jobDone++;
            CrewSkills.Work(this.Role, 1);
            this.Sweep(farm, t);
            this.StandNear(farm, t);
        }
    }

    /// <summary>Put them next to where they just worked, so they're there when you come back.</summary>
    private void StandNear(Farm farm, Vector2 tile)
    {
        if (TilePath.Nearest(farm, new Point((int)tile.X, (int)tile.Y - 1), maxDistance: 2) is { } spot)
            this.position = new Vector2(spot.X, spot.Y) * 64;
    }

    // ---- harvester: walks to each ripe thing, picks it, pockets it ----

    private const int PickTicks = 70, FloorPickTicks = 90, ScanTicks = 60; // a pick (or a keg refill) takes about a second
    private Vector2? pick;                                   // farm tile being picked
    private int pickTimer, scanTimer;
    private bool harvestRoute;                               // current path was set by the harvest logic (not wandering)
    private List<GameLocation>? doorFloors;                  // walking to this building's door
    private readonly Queue<GameLocation> insideFloors = new(); // picking inside a building (hidden)

    private void UpdateHarvest(Farm farm, ITileWork work)
    {
        // wandering (nothing to pick): let the normal wander finish its walk / chore animation
        if (!this.harvestRoute && (this.path.Count > 0 || this.workTicks > 0))
        {
            this.scanTimer--; // keep counting down while strolling, or she'd stroll ~60 times between looks
            this.UpdateWander(farm);
            return;
        }
        if (this.path.Count > 0)
        {
            if (!this.Walk())
                return;
            this.harvestRoute = false;
            if (this.doorFloors != null)
            {
                this.doorFloors.ForEach(this.insideFloors.Enqueue); // through the door: pick inside, out of sight
                this.doorFloors = null;
                this.pickTimer = this.Quicker(FloorPickTicks);
                farm.localSound("doorClose", new Vector2(this.Tile.X, this.Tile.Y));
            }
            else
                this.pickTimer = this.Quicker(PickTicks); // arrived at a plant: lift the basket
            return;
        }

        if (this.insideFloors.Count > 0)
        {
            if (--this.pickTimer > 0)
                return;
            this.pickTimer = this.Quicker(FloorPickTicks);
            var floor = this.insideFloors.Peek();
            var next = work.Targets(floor);
            if (next.Count == 0 || !work.CanPay)
            {
                this.insideFloors.Dequeue(); // this floor's done (or you're broke): next floor, or back outside
                return;
            }
            work.Act(floor, next[0]);
            return;
        }

        if (this.pick is { } tile)
        {
            if (!work.IsReady(farm, tile))
            {
                this.ReleasePick();
                return;
            }
            if (--this.pickTimer > 0)
                return;
            var picked = work.Act(farm, tile);
            this.ReleasePick();
            this.waitTicks = 15;
            this.scanTimer = 0; // straight on to the next one
            if (picked.Count > 0)
            {
                this.Pocket(farm, picked[0]);
                farm.localSound(work.Sound, tile);
            }
            return;
        }

        if (this.waitTicks > 0)
        {
            this.waitTicks--;
            return;
        }
        if (--this.scanTimer > 0)
        {
            this.UpdateWander(farm);
            return;
        }
        this.scanTimer = ScanTicks;
        if (Game1.timeOfDay / 100 != this.unreachableHour)
        {
            this.unreachable.Clear(); // a blocked spot (an animal in the way, a fence since moved) gets another try every hour
            this.unreachableHour = Game1.timeOfDay / 100;
        }
        if (!work.CanPay)
        {
            this.Say("Can't pay me? Then I'm restin'.");
            this.waitTicks = 1800;
            return;
        }

        // nearest ripe thing on the farm with a reachable spot next to it
        var me = this.Tile;
        var candidates = work.Targets(farm)
            .Where(c => !this.unreachable.Contains(c) && !Workers.Claimed.Contains(c)) // another harvester's got it
            .OrderBy(c => Math.Abs(c.X - me.X) + Math.Abs(c.Y - me.Y))
            .Take(6)
            .ToList();
        foreach (var c in candidates)
        {
            if (this.RouteNextTo(farm, c))
            {
                this.pick = c;
                Workers.Claimed.Add(c);
                this.pickTimer = this.Quicker(PickTicks);
                return;
            }
            this.unreachable.Add(c);
        }

        // nothing outside: a building (greenhouse, shed, farmhouse + cellar) with something ready inside
        foreach (var building in farm.buildings.Where(b => !b.isCabin && !b.isUnderConstruction()))
        {
            var floors = Harvester.Floors(building, farm, work.Barns).Where(f => work.Targets(f).Count > 0).ToList();
            if (floors.Count == 0 || Workers.DoorSpot(farm, building) is not { } door)
                continue;
            if (PathGrid.Find(p => TilePath.Passable(farm, p), me, door, maxNodes: LongTrip) is not { } route)
                continue;
            this.doorFloors = floors;
            this.harvestRoute = true;
            route.ForEach(this.path.Enqueue);
            if (route.Count == 0)
                this.path.Enqueue(door); // already at the door
            return;
        }

        this.UpdateWander(farm); // all picked: stroll and chat
    }

    private void ReleasePick()
    {
        if (this.pick is { } tile)
            Workers.Claimed.Remove(tile);
        this.pick = null;
    }

    /// <summary>Path to a walkable tile next to the target (above it first: the picking sprite faces down).</summary>
    private bool RouteNextTo(Farm farm, Vector2 target)
    {
        var me = this.Tile;
        int cx = (int)target.X, cy = (int)target.Y;
        foreach (var stand in new[] { new Point(cx, cy - 1), new Point(cx - 1, cy), new Point(cx + 1, cy), new Point(cx, cy + 1) })
        {
            if (stand != me && !TilePath.Passable(farm, stand))
                continue;
            if (PathGrid.Find(p => TilePath.Passable(farm, p), me, stand, maxNodes: FarmTrip) is not { } route) // big farms: far crops are still reachable
                continue;
            route.ForEach(this.path.Enqueue);
            this.harvestRoute = route.Count > 0;
            return true;
        }
        return false;
    }

    /// <summary>The picked item pops up over her head and fades: into the pocket it goes.</summary>
    private void Pocket(GameLocation location, Item item) => Workers.PopItem(location, this.position, item);

    // ---- field crew: walk to dry crops and water them, 3x3 per stop ----

    private GameLocation? visiting; // building interior a field hand is watering from the inside

    /// <summary>A spot among the nearest dry crops (a little random so the crew spreads out), or null if everything's wet.</summary>
    private Point? DrySpot(Farm farm)
    {
        var me = this.Tile;
        var dry = FieldHands.Dry(farm).OrderBy(t => Math.Abs(t.X - me.X) + Math.Abs(t.Y - me.Y)).Take(8).ToList();
        if (dry.Count == 0)
            return null;
        var crop = dry[Game1.random.Next(dry.Count)];
        return TilePath.Nearest(farm, new Point((int)crop.X, (int)crop.Y), maxDistance: 1); // trellis crops block their own tile
    }

    private Vector2? sowTile; // empty soil this field hand is walking over to plant
    public Vector2? SowTarget => this.sowTile;

    /// <summary>A spot next to the next unsown tile in row order that no other field hand is already walking to, remembering which tile to plant.</summary>
    private Point? SowSpot(Farm farm)
    {
        var unsown = FieldHands.Unsown(farm);
        if (unsown.Count == 0)
            return null;
        var taken = Workers.Members.Where(w => w != this && w.SowTarget != null).Select(w => w.SowTarget!.Value).ToHashSet();
        var tile = unsown.FirstOrDefault(t => !taken.Contains(t), new Vector2(-1, -1)); // next in the row nobody else is headed for
        if (tile.X < 0)
            return null;
        this.sowTile = tile;
        return TilePath.Nearest(farm, new Point((int)tile.X, (int)tile.Y), maxDistance: 1);
    }

    /// <summary>A shed or greenhouse with dry crops inside.</summary>
    private Building? DryBuilding(Farm farm) =>
        farm.buildings.FirstOrDefault(b => !b.isCabin && !b.isUnderConstruction() && b.GetIndoors() is { } inside && inside is not AnimalHouse && FieldHands.Dry(inside).Count > 0);

    private void UpdateWander(Farm farm)
    {
        if (this.workTicks > 0)
        {
            this.workTicks--;
            if (this.workTicks % 75 == 0)
                this.WorkEffect(farm);
            return;
        }
        if (this.waitTicks > 0)
        {
            this.waitTicks--;
            return;
        }
        if (this.path.Count == 0)
        {
            this.enteringBuilding = this.buildingCooldown <= 0 && Game1.random.NextDouble() < 0.2;
            if (this.buildingCooldown > 0)
                this.buildingCooldown--;
            this.visiting = null;
            Point? spot;
            this.sowTile = null;
            if (this.Role == Role.Field && this.DrySpot(farm) is { } dry)
                (spot, this.enteringBuilding) = (dry, false);
            else if (this.Role == Role.Field && this.SowSpot(farm) is { } sow)
                (spot, this.enteringBuilding) = (sow, false);
            else if (this.Role == Role.Field && this.DryBuilding(farm) is { } greenhouse)
            {
                (spot, this.enteringBuilding) = (Workers.DoorSpot(farm, greenhouse), true);
                this.visiting = greenhouse.GetIndoors();
                this.visitName = greenhouse.GetIndoors()?.DisplayName;
            }
            else
                spot = this.enteringBuilding ? this.BuildingSpot(farm) : this.WorkSpot(farm);
            if (spot == null && this.enteringBuilding)
            {
                this.enteringBuilding = false;
                spot = this.WorkSpot(farm);
            }
            var route = spot is { } s ? PathGrid.Find(p => TilePath.Passable(farm, p), this.Tile, s, maxNodes: FarmTrip) : null;
            if (route is { Count: 0 } && this.enteringBuilding && this.visiting == null)
            {
                // already standing at that door (just came out): do some work instead of vanishing again on the spot
                this.enteringBuilding = false;
                route = this.WorkSpot(farm) is { } work ? PathGrid.Find(p => TilePath.Passable(farm, p), this.Tile, work, maxNodes: FarmTrip) : null;
            }
            if (route == null)
                this.waitTicks = 90;
            else if (route.Count == 0)
                this.Arrive();
            else
                route.ForEach(this.path.Enqueue);
            if (route != null && this.enteringBuilding)
                this.Say(this.visiting != null ? "Plants inside need water." : BackSoon[Game1.random.Next(BackSoon.Length)]);
            return;
        }
        if (this.Walk())
            this.Arrive();
    }

    private void Arrive()
    {
        if (this.enteringBuilding)
        {
            this.insideTicks = this.visiting != null ? 75 * 20 : Game1.random.Next(300, 900); // "inside" for 5-15 seconds; waterers stay till it's done
            this.buildingCooldown = 3;
            Game1.getFarm().localSound("doorClose", new Vector2(this.Tile.X, this.Tile.Y));
            return;
        }
        this.workTicks = this.Role switch
        {
            Role.Fisher or Role.Crabber => Game1.random.Next(600, 1200),
            Role.Field => this.Quicker(150), // two tips of the can (the first one waters the 3x3), then on to the next dry patch
            _ => Game1.random.Next(240, 480),
        };
    }

    /// <summary>Where this role does its on-farm work.</summary>
    private Point? WorkSpot(Farm farm)
    {
        var home = Workers.FirstCabin() is { } c ? c.getPointForHumanDoor() : (Point?)null;
        return this.Role switch
        {
            Role.Field => Workers.CropSpot(farm) ?? Workers.BarnSpot(farm),
            Role.Ranch => Workers.BarnSpot(farm) ?? Workers.CropSpot(farm),
            Role.Fisher or Role.Crabber => Workers.WaterSpot(farm) ?? Workers.NearSpot(farm, home),
            Role.Chef => Workers.NearSpot(farm, farm.GetMainFarmHouse()?.getPointForHumanDoor() ?? home),
            Role.Salesman => Workers.NearSpot(farm, farm.GetMainFarmHouse()?.getPointForHumanDoor() ?? home),
            Role.Oil => Workers.RigSpot(farm) ?? Workers.NearSpot(farm, home),
            Role.Harvester => Workers.TreeSpot(farm) ?? Workers.CropSpot(farm) ?? Workers.NearSpot(farm, home),
            Role.Brewer => Workers.MachineSpot(farm, MachineTender.Brewer) ?? Workers.NearSpot(farm, home),
            Role.Blacksmith => Workers.MachineSpot(farm, MachineTender.Smith) ?? Workers.NearSpot(farm, home),
            Role.Artisan => Workers.MachineSpot(farm, MachineTender.Artisan) ?? Workers.NearSpot(farm, home),
            Role.Courier or Role.Banker or Role.Scientist => Workers.NearSpot(farm, farm.GetMainFarmHouse()?.getPointForHumanDoor() ?? home),
            Role.Scareboy => Workers.CropSpot(farm) ?? Workers.NearSpot(farm, home), // walks the fields
            _ => Workers.NearSpot(farm, home),
        };
    }

    /// <summary>A building this role would pop into.</summary>
    private Point? BuildingSpot(Farm farm)
    {
        var buildings = farm.buildings.Where(b => !b.isUnderConstruction() && this.Role switch
        {
            Role.Ranch => b.GetIndoors() is AnimalHouse,
            Role.Artisan => b.GetIndoors() is { } shop && !b.isCabin && b != farm.GetMainFarmHouse(), // barns and coops too
            Role.Field or Role.Harvester or Role.Brewer or Role.Blacksmith => b.GetIndoors() is { } inside && inside is not AnimalHouse && !b.isCabin && b != farm.GetMainFarmHouse(), // sheds, greenhouse
            Role.Chef or Role.Salesman or Role.Banker or Role.Scientist => b == farm.GetMainFarmHouse() || b.isCabin,
            _ => b.isCabin,
        }).ToList();
        if (buildings.Count == 0)
            return null;
        var building = buildings[Game1.random.Next(buildings.Count)];
        this.visitName = building.isCabin ? "the cabin" : building.GetIndoors()?.DisplayName ?? building.buildingType.Value;
        return Workers.DoorSpot(farm, building);
    }

    private void UpdateJob(Farm farm, JobKind kind)
    {
        if (this.path.Count > 0)
        {
            this.Walk();
            return;
        }

        if (this.target is { } t)
        {
            if (!Jobs.IsTarget(farm, t, kind))
            {
                // gone: we finished it (or you beat us to it)
                Workers.Claimed.Remove(t);
                this.target = null;
                if (this.hits > 0)
                {
                    int price = Math.Min(this.Fee(kind), Game1.player.Money);
                    Game1.player.Money -= price;
                    CrewReport.Paid(this.Role, price);
                    CrewReport.Count(this.Role, Jobs.Tallied(kind));
                    this.jobDone++;
                    CrewSkills.Work(this.Role, 1);
                    this.sites.Add((t, SiteTicks));
                }
                this.hits = 0;
                return;
            }
            if (--this.swingTimer > 0)
                return;
            this.swingTimer = this.Quicker(SwingTicks);
            this.swingFrame ^= 1;
            if (this.swingFrame == 1)
            {
                Jobs.Hit(farm, t, kind);
                if (++this.hits > MaxHits)
                {
                    this.unreachable.Add(t); // too tough (e.g. needs a better tool); skip it
                    Workers.Claimed.Remove(t);
                    this.target = null;
                    this.hits = 0;
                }
            }
            return;
        }

        if (this.waitTicks > 0)
        {
            this.waitTicks--;
            return;
        }
        if (Game1.player.Money < this.Fee(kind))
        {
            this.StopJob("Boss, you're outta cash. I'm done.");
            Game1.addHUDMessage(new HUDMessage($"{this.Name} stopped: you can't pay {Jobs.Price(kind)}g.", HUDMessage.error_type));
            return;
        }

        // nearest unclaimed target with a reachable tile next to it (prefer standing above it: the swing sprites face down)
        var me = this.Tile;
        var candidates = Jobs.Targets(farm, kind)
            .Where(c => !Workers.Claimed.Contains(c) && !this.unreachable.Contains(c))
            .OrderBy(c => Math.Abs(c.X - me.X) + Math.Abs(c.Y - me.Y))
            .Take(6)
            .ToList();
        if (candidates.Count == 0)
        {
            this.EndJob(farm, kind);
            return;
        }
        foreach (var c in candidates)
        {
            int cx = (int)c.X, cy = (int)c.Y;
            foreach (var stand in new[] { new Point(cx, cy - 1), new Point(cx - 1, cy), new Point(cx + 1, cy), new Point(cx, cy + 1) })
            {
                if (stand != me && !TilePath.Passable(farm, stand))
                    continue;
                if (PathGrid.Find(p => TilePath.Passable(farm, p), me, stand) is not { } route)
                    continue;
                this.target = c;
                Workers.Claimed.Add(c);
                route.ForEach(this.path.Enqueue);
                this.swingTimer = SwingTicks;
                this.swingFrame = 0;
                return;
            }
            this.unreachable.Add(c);
        }
        this.waitTicks = 30;
    }

    /// <summary>A work duration, shortened by the role's speed perks.</summary>
    private int choreScan;
    private readonly Dictionary<JobKind, int> stuckUntil = new();

    private int Quicker(int ticks) => Math.Max(10, (int)(ticks / CrewSkills.Faster(this.Role)));

    /// <summary>Step along the path. Returns true on arriving at the end.</summary>
    private bool Walk()
    {
        var goal = new Vector2(this.path.Peek().X, this.path.Peek().Y) * 64;
        var delta = goal - this.position;
        if (delta.Length() <= Speed)
        {
            this.position = goal;
            this.path.Dequeue();
            return this.path.Count == 0;
        }
        this.position += Vector2.Normalize(delta) * Speed;
        this.facing = Math.Abs(delta.X) > Math.Abs(delta.Y) ? (delta.X > 0 ? 1 : 3) : (delta.Y > 0 ? 0 : 2);
        return false;
    }

    /// <summary>Sweep up drops around recently cleared targets into the crate (falling trees drop wood a moment later).</summary>
    private void CollectDrops(Farm farm)
    {
        if (this.target is { } t && this.swingTimer == SwingTicks - 1)
            this.Sweep(farm, t); // right after each hit: stones, weeds and stumps drop immediately
        for (int i = this.sites.Count - 1; i >= 0; i--)
        {
            var (tile, ticks) = this.sites[i];
            if (ticks % 20 == 0)
                this.Sweep(farm, tile);
            if (ticks <= 1)
                this.sites.RemoveAt(i);
            else
                this.sites[i] = (tile, ticks - 1);
        }
    }

    private void Sweep(Farm farm, Vector2 tile) => Crate.SweepAround(farm, tile, radius: 6);

    /// <summary>Little touches while working. Field crew watering is real (3x3).</summary>
    private void WorkEffect(GameLocation location)
    {
        float depth = (this.position.Y + 66) / 10000f;
        switch (this.Role)
        {
            case Role.Field:
                if (this.sowTile is { } sow)
                {
                    FieldHands.Sow(location, sow);
                    this.sowTile = null;
                }
                FieldHands.WaterAround(location, this.Tile);
                location.temporarySprites.Add(new TemporaryAnimatedSprite(13, this.position + new Vector2(24, 40), Color.White, 10, Game1.random.NextBool(), 70f, 0, 64, depth) { scale = 0.6f });
                break;
            case Role.Ranch:
                location.temporarySprites.Add(new TemporaryAnimatedSprite("LooseSprites\\Cursors", new Rectangle(211, 428, 7, 6), 2000f, 1, 0, this.position + new Vector2(18, -8), false, false, depth, 0.008f, Color.White, 4f, 0f, 0f, 0f)
                {
                    motion = new Vector2(0f, -0.5f),
                });
                break;
            case Role.Fisher or Role.Crabber when Game1.random.NextDouble() < 0.4:
                location.temporarySprites.Add(new TemporaryAnimatedSprite(28, 100f, 2, 1, this.WaterNextTo() * 64, false, false)); // splash on the water
                break;
        }
    }

    private Vector2 WaterNextTo()
    {
        var me = this.Tile;
        foreach (var d in new[] { new Point(0, 1), new Point(1, 0), new Point(-1, 0), new Point(0, -1) })
        {
            if (Game1.getFarm().isWaterTile(me.X + d.X, me.Y + d.Y))
                return new Vector2(me.X + d.X, me.Y + d.Y);
        }
        return new Vector2(me.X, me.Y + 1);
    }

    // ---- drawing ----

    public override void draw(SpriteBatch b)
    {
        if (!this.Visible)
            return;
        double ms = Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
        int row, frame;
        if (this.target != null && this.path.Count == 0 && this.job is { } kind)
            (row, frame) = (Jobs.Tool(kind) < 0 ? 4 : 5 + Jobs.Tool(kind), this.swingFrame);
        else if (this.pick != null && this.path.Count == 0)
            (row, frame) = (4, this.pickTimer > this.Quicker(PickTicks) / 2 ? 0 : 1); // basket down, then up: picked
        else if (this.workTicks > 0)
            (row, frame) = (4, (int)(ms / (this.Role is Role.Fisher ? 900 : 300)) % 2);
        else
            (row, frame) = (this.facing, this.path.Count > 0 && Game1.shouldTimePass() ? (int)(ms / 150) % 4 : 0);
        Workers.DrawHand(b, this.texture, this.Role, this.Variant, row, frame, this.position);
    }

    public override void drawAboveFrontLayer(SpriteBatch b)
    {
        if (this.Visible)
            Workers.DrawBubble(b, this.bubble, this.bubbleTicks, this.position);
    }

    public override Rectangle getBoundingBox(int xOffset, int yOffset) =>
        new((int)this.position.X + 16, (int)this.position.Y + 32, 32, 32);
}
