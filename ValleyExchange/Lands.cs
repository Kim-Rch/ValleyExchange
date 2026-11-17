using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Extensions;
using StardewValley.TerrainFeatures;

namespace ValleyExchange;

/// <summary>
/// Work beyond the farm map itself: sheds, the greenhouse, the farmhouse and cellar, Ginger Island's farm and any other farm map
/// (Stardew Realty expansions). Every hour the field hands, harvesters and brewers are spread over every place with work for
/// them (the farm first). They walk off to it, and you'll find them there working; when you're elsewhere it runs off-screen.
/// </summary>
internal static class Lands
{
    private static readonly Role[] LandRoles = { Role.Field, Role.Harvester, Role.Brewer, Role.Blacksmith, Role.Artisan };
    private static readonly Dictionary<string, GameLocation> Assigned = new(); // hand name → where they work this hour
    /// <summary>Tiles a land hand is already headed for, so two don't take the same one.</summary>
    public static readonly HashSet<(GameLocation, Vector2)> Claimed = new();
    private static int assignedHour = -1;

    public static void Reset()
    {
        Assigned.Clear();
        Claimed.Clear();
        assignedHour = -1;
    }

    /// <summary>Re-spread the hands now (a place ran out of work).</summary>
    public static void Invalidate() => assignedHour = -1;

    /// <summary>Every place besides the farm map with possible work.</summary>
    public static List<GameLocation> All()
    {
        var farm = Game1.getFarm();
        var lands = new List<GameLocation>();
        foreach (var building in farm.buildings)
        {
            if (!building.isCabin && !building.isUnderConstruction() && building.GetIndoors() is { } inside && inside is not AnimalHouse)
                lands.Add(inside); // sheds, greenhouse, farmhouse
        }
        if (Game1.getLocationFromName("Cellar") is { } cellar)
            lands.Add(cellar);
        Utility.ForEachLocation(location =>
        {
            if (location != farm && location.ParentBuilding == null && (location.IsFarm || location.Name == "IslandWest") && !lands.Contains(location))
                lands.Add(location); // Ginger Island's farm, farm expansions
            return true;
        });
        return lands;
    }

    public static List<Vector2> Targets(Role role, GameLocation location) =>
        role == Role.Field ? FieldHands.Dry(location).Concat(FieldHands.Unsown(location)).ToList() : TileWorks.For(role)?.Targets(location) ?? new();

    /// <summary>Where this hand works this hour, or null for the farm map itself.</summary>
    public static GameLocation? LandOf(Worker worker)
    {
        if (!LandRoles.Contains(worker.Role) || Game1.timeOfDay >= Worker.HomeTime || worker.HasJob) // on your orders: stays on the farm
            return null;
        if (assignedHour != Game1.timeOfDay / 100)
            Assign();
        return Assigned.GetValueOrDefault(worker.Name);
    }

    /// <summary>Spread each role's hands by how much work each place has (far-off farms count for less, it's a long walk).</summary>
    private static void Assign()
    {
        assignedHour = Game1.timeOfDay / 100;
        Assigned.Clear();
        var farm = Game1.getFarm();
        var lands = All();
        foreach (var role in LandRoles)
        {
            var hands = Workers.Members.Where(w => w.Role == role && !w.HasJob).ToList(); // hands on a job you gave stay put
            if (hands.Count == 0)
                continue;
            // (place, work waiting there): null is the farm map itself
            var places = new List<(GameLocation? Place, int Work)> { (null, Targets(role, farm).Count) };
            places.AddRange(lands.Select(l => ((GameLocation?)l, Targets(role, l).Count)));
            var weights = places.Select(p => Market.LandWeight(p.Work, far: p.Place != null && p.Place.ParentBuilding == null && p.Place is not StardewValley.Locations.FarmHouse and not StardewValley.Locations.Cellar)).ToList();
            var spread = Market.SpreadHands(weights, hands.Count);
            for (int i = 0; i < hands.Count; i++)
            {
                if (spread[i] >= 0 && places[spread[i]].Place is { } land)
                    Assigned[hands[i].Name] = land;
            }
        }
    }

    /// <summary>Where on the farm map a hand goes to get to a land: the building's door, or the farm exit.</summary>
    public static Point? Entrance(Farm farm, GameLocation land)
    {
        var building = land.ParentBuilding ?? (land is StardewValley.Locations.FarmHouse or StardewValley.Locations.Cellar ? farm.GetMainFarmHouse() : null);
        return building != null ? Workers.DoorSpot(farm, building) : Workers.ExitSpot(farm);
    }

    /// <summary>Show the hands working here (called on arrival and every 10 minutes).</summary>
    public static void Attach(GameLocation? location, Texture2D texture)
    {
        if (location == null || location == Game1.getFarm())
            return;
        location.critters ??= new List<StardewValley.BellsAndWhistles.Critter>();
        var here = location.critters.OfType<LandHand>().ToList();
        foreach (var worker in Workers.Members.Where(w => Present(w, location) && here.All(h => h.Worker != w)))
        {
            if (Arrival(location) is { } start)
                location.critters.Add(new LandHand(worker, start, texture));
        }
    }

    /// <summary>Working here now: assigned here, and arrived if it's a trip away (the island, other farms).</summary>
    public static bool Present(Worker worker, GameLocation location) =>
        LandOf(worker) == location && (Travel.Destination(worker) == "Farm" || Travel.At(worker) == location.NameOrUniqueName);

    /// <summary>Just inside the door you come in by.</summary>
    private static Point? Arrival(GameLocation location)
    {
        var warp = location.warps.FirstOrDefault(w => w.TargetName == "Farm") ?? location.warps.FirstOrDefault();
        var back = location.Map.Layers[0];
        var at = warp != null ? new Point(Math.Clamp(warp.X, 1, back.LayerWidth - 2), Math.Clamp(warp.Y, 1, back.LayerHeight - 2)) : new Point(back.LayerWidth / 2, back.LayerHeight / 2);
        return TilePath.Nearest(location, at, maxDistance: 6);
    }

    public static LandHand? At(Vector2 pixel) =>
        Game1.currentLocation?.critters?.OfType<LandHand>().FirstOrDefault(h => h.Bounds.Contains(pixel));

    /// <summary>Every 10 minutes: hands working somewhere you aren't do their share without the walking.</summary>
    public static void Offscreen()
    {
        foreach (var worker in Workers.Members)
        {
            if (LandOf(worker) is not { } land || land == Game1.currentLocation || !Present(worker, land))
                continue; // still on the road: no work yet
            if (worker.Role == Role.Field)
            {
                for (int stop = 0; stop < Math.Round(2 * CrewSkills.Faster(Role.Field)); stop++)
                {
                    if (FieldHands.Dry(land) is { Count: > 0 } dry)
                        FieldHands.WaterAround(land, new Point((int)dry[0].X, (int)dry[0].Y));
                    else if (FieldHands.Unsown(land) is { Count: > 0 } unsown && FieldHands.Sow(land, unsown[0]))
                        FieldHands.WaterAround(land, new Point((int)unsown[0].X, (int)unsown[0].Y));
                }
            }
            else if (TileWorks.For(worker.Role) is { } work)
            {
                for (int i = 0; i < Math.Round(work.PerTenMinutes * CrewSkills.Faster(worker.Role)) && work.CanPay && work.Targets(land) is { Count: > 0 } targets; i++)
                    work.Act(land, targets[0]);
            }
        }
    }
}

/// <summary>A field hand, harvester or brewer seen working away from the farm map: walks to each job, does it, moves on.</summary>
internal sealed class LandHand : Critter
{
    private const int ActTicks = 70, WaterTicks = 120, ScanTicks = 60;
    public Worker Worker { get; }
    private readonly Texture2D texture;
    private readonly Queue<Point> path = new();
    private Vector2? target;
    private int actTimer, waitTicks = Game1.random.Next(20, 90), scanTimer, facing, bubbleTicks, chatterTicks = Game1.random.Next(600, 3000);
    private string? bubble;

    public LandHand(Worker worker, Point start, Texture2D texture)
    {
        this.Worker = worker;
        this.texture = texture;
        this.position = new Vector2(start.X, start.Y) * 64;
    }

    public Rectangle Bounds => new((int)this.position.X, (int)this.position.Y, 64, 64);
    private Point Tile => new((int)Math.Round(this.position.X / 64), (int)Math.Round(this.position.Y / 64));
    private Role Role => this.Worker.Role;

    public void Talk()
    {
        this.facing = Workers.FacingToward(this.position, Game1.player.Position); // look at the boss
        string line = Staff.Reports.TryGetValue(this.Role, out var report) ? report : Chatter.Line(this.Worker.Name, this.Role);
        Personas.Ask(this.Worker.Name, this.Role, $"{this.Worker.Name}: \"{line}\"");
    }

    public override bool update(GameTime time, GameLocation environment)
    {
        if (!Game1.shouldTimePass())
            return false; // paused, a menu or a dialogue is open (talking to them, too): freeze
        if (!Lands.Present(this.Worker, environment))
        {
            if (this.target is { } t)
                Lands.Claimed.Remove((environment, t));
            return true; // sent elsewhere this hour, or quitting time
        }
        this.Chat();
        if (this.path.Count > 0)
        {
            this.Walk();
            return false;
        }
        if (this.target is { } tile)
        {
            if (!this.Ready(environment, tile))
            {
                Lands.Claimed.Remove((environment, tile));
                this.target = null;
                return false;
            }
            if (--this.actTimer > 0)
                return false;
            this.Act(environment, tile);
            Lands.Claimed.Remove((environment, tile));
            this.target = null;
            this.waitTicks = 15;
            return false;
        }
        if (this.waitTicks > 0)
        {
            this.waitTicks--;
            return false;
        }
        if (--this.scanTimer > 0)
            return false;
        this.scanTimer = ScanTicks;

        var me = this.Tile;
        var candidates = Lands.Targets(this.Role, environment)
            .Where(c => !Lands.Claimed.Contains((environment, c)))
            .OrderBy(c => Math.Abs(c.X - me.X) + Math.Abs(c.Y - me.Y))
            .Take(6)
            .ToList();
        if (candidates.Count == 0)
        {
            Lands.Invalidate(); // nothing left here: spread the crew again
            this.waitTicks = 240;
            return false;
        }
        foreach (var c in candidates)
        {
            int cx = (int)c.X, cy = (int)c.Y;
            foreach (var stand in new[] { new Point(cx, cy - 1), new Point(cx - 1, cy), new Point(cx + 1, cy), new Point(cx, cy + 1), new Point(cx, cy) })
            {
                if (stand != me && !TilePath.Passable(environment, stand))
                    continue;
                if (PathGrid.Find(p => TilePath.Passable(environment, p), me, stand, maxNodes: 12_000) is not { } route)
                    continue;
                route.ForEach(this.path.Enqueue);
                this.target = c;
                this.actTimer = Math.Max(10, (int)((this.Role == Role.Field ? WaterTicks : ActTicks) / CrewSkills.Faster(this.Role)));
                Lands.Claimed.Add((environment, c));
                return false;
            }
        }
        this.waitTicks = 60;
        return false;
    }

    private bool Ready(GameLocation location, Vector2 tile) =>
        this.Role == Role.Field
            ? location.terrainFeatures.TryGetValue(tile, out var feature) && feature is HoeDirt dirt && (dirt.crop == null ? FieldHands.SeedsFor(location).Count > 0 : dirt.state.Value != HoeDirt.watered)
            : TileWorks.For(this.Role)?.IsReady(location, tile) == true;

    private void Act(GameLocation location, Vector2 tile)
    {
        if (this.Role == Role.Field)
        {
            if (location.terrainFeatures.TryGetValue(tile, out var soil) && soil is HoeDirt { crop: null })
                FieldHands.Sow(location, tile);
            FieldHands.WaterAround(location, this.Tile);
            FieldHands.WaterAround(location, new Point((int)tile.X, (int)tile.Y)); // reach the target too when standing beside it
            location.temporarySprites.Add(new TemporaryAnimatedSprite(13, this.position + new Vector2(24, 40), Color.White, 10, Game1.random.NextBool(), 70f, 0, 64, (this.position.Y + 66) / 10000f) { scale = 0.6f });
            return;
        }
        if (TileWorks.For(this.Role) is not { } work)
            return;
        var shown = work.Act(location, tile);
        if (shown.Count > 0)
        {
            Workers.PopItem(location, this.position, shown[0]);
            location.localSound(work.Sound, tile);
        }
    }

    private void Walk()
    {
        var goal = new Vector2(this.path.Peek().X, this.path.Peek().Y) * 64;
        var delta = goal - this.position;
        float speed = 1.6f * CrewSkills.Faster(this.Role);
        if (delta.Length() <= speed)
        {
            this.position = goal;
            this.path.Dequeue();
            return;
        }
        this.position += Vector2.Normalize(delta) * speed;
        this.facing = Math.Abs(delta.X) > Math.Abs(delta.Y) ? (delta.X > 0 ? 1 : 3) : (delta.Y > 0 ? 0 : 2);
    }

    private void Chat()
    {
        if (this.bubbleTicks > 0)
        {
            this.bubbleTicks--;
            return;
        }
        if (--this.chatterTicks <= 0)
        {
            (this.bubble, this.bubbleTicks) = (Chatter.Line(this.Worker.Name, this.Role), 240);
            this.chatterTicks = Game1.random.Next(2400, 6000);
        }
    }

    public override void draw(SpriteBatch b)
    {
        double ms = Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
        var (row, frame) = this.target != null && this.path.Count == 0
            ? (4, (int)(ms / 300) % 2)
            : (this.facing, this.path.Count > 0 && Game1.shouldTimePass() ? (int)(ms / 150) % 4 : 0);
        Workers.DrawHand(b, this.texture, this.Role, this.Worker.Variant, row, frame, this.position);
    }

    public override void drawAboveFrontLayer(SpriteBatch b) => Workers.DrawBubble(b, this.bubble, this.bubbleTicks, this.position);

    public override Rectangle getBoundingBox(int xOffset, int yOffset) => new((int)this.position.X + 16, (int)this.position.Y + 32, 32, 32);
}
