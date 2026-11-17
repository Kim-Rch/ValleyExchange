using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Locations;

namespace ValleyExchange;

/// <summary>
/// Hands going to and from work off the farm take a real route over the maps. Each map takes as long as it takes to walk
/// across it (entry to exit, at road pace). If you're on a map they're crossing you'll see them walking it, at the right spot
/// for how far along they are. They only show up at work once they've arrived, and only walk back onto the farm once home.
/// </summary>
internal static class Travel
{
    /// <summary>Walking pace on the road (pixels per tick), used both to time each map and to move them on screen.</summary>
    public const float RoadSpeed = 2.4f;

    /// <summary>A trip: the maps, when it started, and how many game minutes each map in between takes to walk across.</summary>
    private sealed record Trip(string To, List<string> Route, double Start, int[] Legs);

    private static readonly Dictionary<string, Trip> Trips = new();   // hand name: trip in progress
    private static readonly Dictionary<string, string> Places = new(); // hand name: where they are when not travelling ("Farm" if missing)

    public static void Reset()
    {
        Trips.Clear();
        Places.Clear();
    }

    /// <summary>Game minutes since 6am, smooth between the 10-minute ticks (a game minute is 0.7 real seconds).</summary>
    private static double Now => Utility.ConvertTimeToMinutes(Game1.timeOfDay) + Math.Clamp(Game1.gameTimeInterval / 700.0, 0, 9.99);

    /// <summary>Where a hand should be right now: an off-farm station or farm map, or "Farm" (the farm and its buildings).</summary>
    public static string Destination(Worker worker)
    {
        if (Workers.Station(worker.Role) is { } station)
            return station;
        if (Lands.LandOf(worker) is { } land && land.ParentBuilding == null && land is not FarmHouse and not Cellar)
            return land.NameOrUniqueName; // Ginger Island, farm expansions: a trip; sheds and the greenhouse are just a door away
        return "Farm";
    }

    public static bool InTransit(Worker worker)
    {
        Sync(worker);
        return Trips.ContainsKey(worker.Name);
    }

    /// <summary>Where a hand is, if they've arrived somewhere (null while travelling).</summary>
    public static string? At(Worker worker)
    {
        Sync(worker);
        return Trips.ContainsKey(worker.Name) ? null : Places.GetValueOrDefault(worker.Name, "Farm");
    }

    /// <summary>Finish trips that are over and start the ones that are due. Leaving the farm waits until they've walked off it.</summary>
    public static void Sync(Worker worker)
    {
        if (Trips.TryGetValue(worker.Name, out var trip))
        {
            if (Now < trip.Start + trip.Legs.Sum())
                return;
            Places[worker.Name] = trip.To;
            Trips.Remove(worker.Name);
        }
        string here = Places.GetValueOrDefault(worker.Name, "Farm");
        string there = Destination(worker);
        if (here == there || (here == "Farm" && !worker.Away))
            return;
        if (Route(here, there) is { } route)
            Trips[worker.Name] = new Trip(there, route, Now, Legs(route));
        else
            Places[worker.Name] = there; // no known way: they just get there
    }

    /// <summary>Maps from one place to another. The island is a boat ride from Willy's; the city is the bus.</summary>
    private static List<string>? Route(string from, string to)
    {
        static List<string>? Walk(string a, string b) => a == b ? new() { a } : Maps(a, b);
        static bool Island(string place) => place.StartsWith("Island");
        static string Ground(string place) => place == "City" ? "BusStop" : place;

        if (Island(to) && !Island(from))
            return Walk(Ground(from), "Beach") is { } toDock ? toDock.Append("IslandSouth").Append(to).Distinct().ToList() : null;
        if (Island(from) && !Island(to))
            return Walk("Beach", Ground(to)) is { } fromDock ? new List<string> { from, "IslandSouth" }.Concat(fromDock).Distinct().ToList() : null;
        var route = Walk(Ground(from), Ground(to));
        if (route != null && to == "City")
            route.Add("City"); // onto the bus
        if (route != null && from == "City")
            route.Insert(0, "City");
        return route;
    }

    /// <summary>
    /// Fewest-maps route between two places over outdoor warps (the game's NPC routes leave out the farm, so this is our own).
    /// Only outdoor maps in between: nobody cuts through Pierre's.
    /// </summary>
    private static List<string>? Maps(string from, string to)
    {
        var cameFrom = new Dictionary<string, string> { [from] = from };
        var queue = new Queue<string>();
        queue.Enqueue(from);
        while (queue.Count > 0 && !cameFrom.ContainsKey(to))
        {
            if (Game1.getLocationFromName(queue.Dequeue()) is not { } here)
                continue;
            foreach (string next in here.warps.Select(w => w.TargetName).Distinct())
            {
                if (cameFrom.ContainsKey(next) || (next != to && Game1.getLocationFromName(next) is not { IsOutdoors: true }))
                    continue;
                cameFrom[next] = here.Name;
                queue.Enqueue(next);
            }
        }
        if (!cameFrom.ContainsKey(to))
            return null;
        var route = new List<string> { to };
        while (route[0] != from)
            route.Insert(0, cameFrom[route[0]]);
        return route;
    }

    /// <summary>The routes from the farm to every work spot, for the vx_routes console command.</summary>
    public static string DescribeRoutes() =>
        string.Join("\n", new[] { "Mine", "Mountain", "Beach", "Forest", "Town", "City", "IslandWest" }.Select(to =>
            $"  Farm -> {to}: {(Route("Farm", to) is { } r ? string.Join(" > ", r) + $" ({Legs(r).Sum()} min)" : "no route (they just appear there)")}"));

    /// <summary>Game minutes to walk across each map between the start and the end: entry to exit distance at road pace.</summary>
    private static int[] Legs(List<string> route)
    {
        var legs = new int[Math.Max(0, route.Count - 2)];
        for (int i = 1; i < route.Count - 1; i++)
        {
            legs[i - 1] = 20; // unknown map: a fair guess
            if (Game1.getLocationFromName(route[i]) is not { } map || (Gate(map, route[i - 1]) ?? Middle(map)) is not { } a || (Gate(map, route[i + 1]) ?? Middle(map)) is not { } b)
                continue;
            double tiles = (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y)) * 1.2; // paths wind a bit
            double minutes = tiles * 64 / RoadSpeed / 60 / 0.7; // ticks, then seconds, then game minutes
            legs[i - 1] = Math.Clamp((int)Math.Round(minutes), 10, 150);
        }
        return legs;
    }

    /// <summary>The map they're crossing right now (index into the route) and when that leg started and ends; null once there.</summary>
    private static (int Index, double Start, double End)? Leg(Trip trip)
    {
        double t = trip.Start;
        for (int i = 0; i < trip.Legs.Length; i++)
        {
            double end = t + trip.Legs[i];
            if (Now < end)
                return (i + 1, t, end);
            t = end;
        }
        return null;
    }

    /// <summary>How far through a leg we are right now (0 to 1).</summary>
    public static float Progress(double start, double end) => (float)Math.Clamp((Now - start) / Math.Max(1, end - start), 0, 1);

    /// <summary>Hands walking through this map right now, with where they came from, where they're going, and when.</summary>
    private static IEnumerable<(Worker Worker, string From, string Next, double Start, double End)> Crossing(GameLocation location)
    {
        foreach (var worker in Workers.Members)
        {
            Sync(worker);
            if (Trips.TryGetValue(worker.Name, out var trip) && Leg(trip) is { } leg && trip.Route[leg.Index] == location.Name)
                yield return (worker, trip.Route[leg.Index - 1], trip.Route[leg.Index + 1], leg.Start, leg.End);
        }
    }

    /// <summary>The map a travelling hand is crossing right now (null when not travelling).</summary>
    public static string? CurrentMap(Worker worker)
    {
        if (!Trips.TryGetValue(worker.Name, out var trip))
            return null;
        return PlaceName(Leg(trip) is { } leg ? trip.Route[leg.Index] : trip.To);
    }

    /// <summary>"On the road to the beach" for the tracker.</summary>
    public static string Describe(Worker worker) =>
        Trips.TryGetValue(worker.Name, out var trip) ? $"On the road to {PlaceName(trip.To)}" : "On the road";

    /// <summary>A friendly name for a place.</summary>
    public static string PlaceName(string place) => place switch
    {
        "Farm" => "the farm",
        "Mine" => "the mine entrance",
        "Mountain" => "the mountain",
        "City" => "the city",
        "IslandWest" => "Ginger Island",
        _ => Game1.getLocationFromName(place)?.DisplayName ?? place,
    };

    public static bool IsCrossing(Worker worker, GameLocation location) => Crossing(location).Any(c => c.Worker == worker);

    /// <summary>Show anyone passing through here (on arrival and every 10 minutes).</summary>
    public static void Attach(GameLocation? location, Texture2D texture)
    {
        if (location == null || location == Game1.getFarm())
            return;
        location.critters ??= new List<Critter>();
        var here = location.critters.OfType<Traveler>().ToList();
        foreach (var (worker, from, next, legStart, legEnd) in Crossing(location))
        {
            if (here.Any(t => t.Worker == worker))
                continue;
            var start = Gate(location, from) ?? Middle(location); // e.g. stepping off the boat
            var end = Gate(location, next);
            if (start is { } a && end is { } b && PathGrid.Find(p => TilePath.Passable(location, p), a, b, maxNodes: 25_000, partial: true) is { } path)
                location.critters.Add(new Traveler(worker, path.Prepend(a).ToList(), next, legStart, legEnd, texture));
        }
    }

    /// <summary>The walkable tile by the way to another map: its warp, or its door (Willy's door for the boat).</summary>
    private static Point? Gate(GameLocation location, string other)
    {
        var back = location.Map.Layers[0];
        Point? at = null;
        if (location.warps.FirstOrDefault(w => w.TargetName == other) is { } warp)
            at = new Point(warp.X, warp.Y);
        else if (location.doors.Pairs.FirstOrDefault(d => d.Value == other || (other == "IslandSouth" && d.Value == "FishShop")) is { Value: not null } door)
            at = new Point(door.Key.X, door.Key.Y + 1);
        else if (other == "City" && location.Name == "BusStop")
            at = new Point(back.LayerWidth / 2, back.LayerHeight / 2); // the bus stop itself
        if (at is not { } p)
            return null;
        return TilePath.Nearest(location, new Point(Math.Clamp(p.X, 1, back.LayerWidth - 2), Math.Clamp(p.Y, 1, back.LayerHeight - 2)), maxDistance: 6);
    }

    private static Point? Middle(GameLocation location)
    {
        var back = location.Map.Layers[0];
        return TilePath.Nearest(location, new Point(back.LayerWidth / 2, back.LayerHeight / 2), maxDistance: 10);
    }

    /// <summary>Every 10 minutes: hands due somewhere off the farm while you're not watching the farm just set off.</summary>
    public static void Tick()
    {
        bool watchingFarm = Game1.currentLocation == Game1.getFarm();
        foreach (var worker in Workers.Members)
        {
            if (!watchingFarm && !worker.Away && Destination(worker) != "Farm")
                worker.LeaveOffscreen();
            Sync(worker);
        }
    }

    public static Traveler? At(Vector2 pixel) =>
        Game1.currentLocation?.critters?.OfType<Traveler>().FirstOrDefault(t => t.Bounds.Contains(pixel));
}

/// <summary>A hand passing through a map on the way somewhere: walks from one side to the other in step with the trip's clock.</summary>
internal sealed class Traveler : Critter
{
    public Worker Worker { get; }
    private readonly Texture2D texture;
    private readonly List<Point> points;
    private readonly string headingTo;
    private readonly double legStart, legEnd;
    private int next, facing, bubbleTicks;
    private string? bubble;

    public Traveler(Worker worker, List<Point> points, string headingTo, double legStart, double legEnd, Texture2D texture)
    {
        this.Worker = worker;
        this.texture = texture;
        this.points = points;
        this.headingTo = headingTo;
        this.legStart = legStart;
        this.legEnd = legEnd;
        int at = this.Due(); // you may run into them halfway across
        this.position = new Vector2(points[at].X, points[at].Y) * 64;
        this.next = at + 1;
        if (Game1.random.NextDouble() < 0.5)
            (this.bubble, this.bubbleTicks) = (Chatter.Line(worker.Name, worker.Role), 240);
    }

    /// <summary>The path point they should be at by now.</summary>
    private int Due() => (int)(Travel.Progress(this.legStart, this.legEnd) * (this.points.Count - 1));

    public Rectangle Bounds => new((int)this.position.X, (int)this.position.Y, 64, 64);

    public void Talk()
    {
        this.facing = Workers.FacingToward(this.position, Game1.player.Position);
        string where = this.headingTo switch { "IslandSouth" => "the boat", "City" => "the bus", "Farm" => "home", _ => Travel.PlaceName(this.headingTo) };
        Personas.Ask(this.Worker.Name, this.Worker.Role, $"{this.Worker.Name}: \"On my way to {where}, boss.\"");
    }

    public override bool update(GameTime time, GameLocation environment)
    {
        if (!Game1.shouldTimePass())
            return false;
        if (!Travel.IsCrossing(this.Worker, environment))
            return true; // their time on this map is up: out the other side
        if (this.bubbleTicks > 0)
            this.bubbleTicks--;
        int due = this.Due();
        if (this.next >= this.points.Count || this.next > due + 1)
            return false; // at the exit, or ahead of schedule: wait a moment
        float speed = this.next < due - 2 ? Travel.RoadSpeed * 2 : Travel.RoadSpeed; // behind schedule: hurry
        var goal = new Vector2(this.points[this.next].X, this.points[this.next].Y) * 64;
        var delta = goal - this.position;
        if (delta.Length() <= speed)
        {
            this.position = goal;
            this.next++;
            return false;
        }
        this.position += Vector2.Normalize(delta) * speed;
        this.facing = Workers.FacingToward(Vector2.Zero, delta);
        return false;
    }

    public override void draw(SpriteBatch b)
    {
        bool walking = Game1.shouldTimePass() && this.next < this.points.Count && this.next <= this.Due() + 1;
        int frame = walking ? (int)(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 150) % 4 : 0;
        Workers.DrawHand(b, this.texture, this.Worker.Role, this.Worker.Variant, this.facing, frame, this.position);
    }

    public override void drawAboveFrontLayer(SpriteBatch b) => Workers.DrawBubble(b, this.bubble, this.bubbleTicks, this.position);

    public override Rectangle getBoundingBox(int xOffset, int yOffset) => new((int)this.position.X + 16, (int)this.position.Y + 32, 32, 32);
}
