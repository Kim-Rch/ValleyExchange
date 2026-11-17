using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Extensions;
using StardewValley.Objects;

namespace ValleyExchange;

/// <summary>
/// The hands you can bump into off the farm while they work: fishermen and the crab catcher at the beach, miners and
/// adventurers at the mine entrance, the salesman outside Pierre's. Just for show (their haul is worked out at night).
/// </summary>
internal static class Outposts
{
    private static readonly Dictionary<string, List<Point>> SpotCache = new();

    public static void Reset() => SpotCache.Clear();

    /// <summary>Show whoever works here right now; called on arrival and every 10 minutes (shifts start and end).</summary>
    public static void Attach(GameLocation? location, Texture2D texture)
    {
        if (location == null || location == Game1.getFarm() || location.Name is not ("Beach" or "Mine" or "Town" or "Forest" or "Mountain"))
            return;
        location.critters ??= new List<Critter>();
        var here = location.critters.OfType<Visitor>().ToList();
        foreach (var gone in here.Where(v => !Present(v.Name, location)))
            location.critters.Remove(gone);
        foreach (var worker in Workers.Members.Where(w => Present(w.Name, location)))
        {
            if (here.Any(v => v.Name == worker.Name))
                continue;
            var spots = Spots(location, worker.Role);
            if (spots.Count > 0)
                location.critters.Add(new Visitor(worker.Name, worker.Role, worker.Variant, spots[Game1.random.Next(spots.Count)], texture));
        }
        // the guild's heroes hang around the mine entrance all day, the pirates on the docks when they're in port (not the ones laid up)
        if (Workers.State is { } state && location.Name is "Mine" or "Beach")
        {
            bool fleet = location.Name == "Beach";
            var leader = GuildPlay.Leader(fleet);
            foreach (var hero in GuildPlay.Of(state, fleet).Members.Where(m => GuildPlay.HeroAt(m.Name, location) && here.All(v => v.Name != m.Name)))
            {
                var spots = Spots(location, leader);
                if (spots.Count > 0)
                    location.critters.Add(new Visitor(hero.Name, leader, hero.Name.Sum(c => c) % 3, spots[Game1.random.Next(spots.Count)], texture,
                        block: (fleet ? Workers.PirateBlock : Workers.HeroBlock) + (int)hero.Class, hero: true));
            }
        }
    }

    /// <summary>Is this hand at work here right now (on shift and arrived, not still on the road)?</summary>
    public static bool Present(string name, GameLocation location) =>
        (Workers.Members.FirstOrDefault(w => w.Name == name) is { } worker && Workers.Station(worker.Role) == location.Name && Travel.At(worker) == location.Name
            && !(worker.Role == Role.Pirate && Workers.State?.Fleet.AtSea == true)) // the captain sails with his crew
        || GuildPlay.HeroAt(name, location);

    public static Visitor? At(Vector2 pixel) =>
        Game1.currentLocation?.critters?.OfType<Visitor>().FirstOrDefault(v => v.Bounds.Contains(pixel));

    /// <summary>Walkable spots where this role hangs out in this location (cached per day: some scan the map).</summary>
    public static List<Point> Spots(GameLocation location, Role role)
    {
        string key = $"{location.Name}/{role}";
        if (SpotCache.TryGetValue(key, out var cached))
            return cached;
        var spots = location.Name switch
        {
            "Beach" when role == Role.Crabber => location.objects.Pairs.Where(p => p.Value is CrabPot)
                .Select(p => TilePath.Nearest(location, new Point((int)p.Key.X, (int)p.Key.Y), maxDistance: 2))
                .OfType<Point>().ToList() is { Count: > 0 } pots ? pots : Shore(location),
            "Beach" when role == Role.Pirate => Around(location, DoorOf(location, "FishShop") ?? Center(location), 5), // on Willy's dock
            "Beach" => Shore(location),
            "Town" when role == Role.Scavenger && Scavenger.CanSpots(location) is { Count: > 0 } cans => cans,
            "Town" => Around(location, DoorOf(location, "SeedShop") ?? Center(location), 4),
            "Mountain" => Around(location, DoorOf(location, "AdventureGuild") ?? Center(location), 5), // outside the guild hall
            "Forest" => Around(location, ArrivalFrom(location, "Farm") ?? Center(location), 7), // by the path down from the farm
            _ => Around(location, ArrivalFrom(location, "Mountain") ?? Center(location), 7), // mine entrance: by the way in from the mountain
        };
        return SpotCache[key] = spots;
    }

    /// <summary>Walkable tiles touching water.</summary>
    private static List<Point> Shore(GameLocation location)
    {
        var shore = new List<Point>();
        var back = location.Map.Layers[0];
        for (int x = 1; x < back.LayerWidth - 1; x++)
        {
            for (int y = 1; y < back.LayerHeight - 1; y++)
            {
                bool wet = location.isWaterTile(x + 1, y) || location.isWaterTile(x - 1, y) || location.isWaterTile(x, y + 1) || location.isWaterTile(x, y - 1);
                if (wet && TilePath.Passable(location, new Point(x, y)))
                    shore.Add(new Point(x, y));
            }
        }
        return shore;
    }

    /// <summary>
    /// Walkable tiles within a radius that you can actually walk to from the center (a flood fill), so nobody stands in the
    /// black void past a cave wall or on a ledge cut off from the path.
    /// </summary>
    private static List<Point> Around(GameLocation location, Point center, int radius)
    {
        if ((TilePath.Passable(location, center) ? center : TilePath.Nearest(location, center, maxDistance: radius)) is not { } start)
            return new();
        var seen = new HashSet<Point> { start };
        var queue = new Queue<Point>(seen);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            foreach (var next in new[] { new Point(p.X + 1, p.Y), new Point(p.X - 1, p.Y), new Point(p.X, p.Y + 1), new Point(p.X, p.Y - 1) })
            {
                if (Math.Abs(next.X - center.X) <= radius && Math.Abs(next.Y - center.Y) <= radius && !seen.Contains(next) && TilePath.Passable(location, next))
                {
                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }
        }
        return seen.ToList();
    }

    private static Point Center(GameLocation location)
    {
        var back = location.Map.Layers[0];
        return new Point(back.LayerWidth / 2, back.LayerHeight / 2);
    }

    /// <summary>Where you walk in from another map (the warp's tile, nudged inside the map).</summary>
    private static Point? ArrivalFrom(GameLocation location, string from)
    {
        if (location.warps.FirstOrDefault(w => w.TargetName == from) is not { } warp)
            return null;
        var back = location.Map.Layers[0];
        return new Point(Math.Clamp(warp.X, 4, back.LayerWidth - 5), Math.Clamp(warp.Y, 4, back.LayerHeight - 5));
    }

    /// <summary>The street in front of a shop door (found from the door warp, so map mods that move the shop still work).</summary>
    private static Point? DoorOf(GameLocation location, string target) =>
        location.warps.FirstOrDefault(w => w.TargetName == target) is { } warp ? new Point(warp.X, warp.Y + 2) : null;
}

/// <summary>A hand seen off the farm: walks between their spots, works, chats, can be talked to. Not saved.</summary>
internal sealed class Visitor : Critter
{
    private static readonly Dictionary<Role, string[]> AtWork = new()
    {
        [Role.Fisher] = new[] { "They're bitin' today, boss!", "Quiet out here. Just how I like it.", "Got a few already. Want to see?" },
        [Role.Crabber] = new[] { "Checkin' your pots, boss.", "Crabs are fat this week.", "Mind the pincers." },
        [Role.Miner] = new[] { "Headin' down soon, boss.", "Ore's waitin' down there.", "Lamp's lit, pick's sharp." },
        [Role.Adventurer] = new[] { "The dungeon's callin', boss.", "Party's suited up.", "Deeper every day. That's the guild way." },
        [Role.Salesman] = new[] { "Sold half the load already!", "Pierre's jealous of my prices.", "Fresh from the farm, folks!" },
        [Role.Forager] = new[] { "Woods are generous today.", "Found mushrooms by the river.", "Plantin' a few for next year." },
        [Role.Courier] = new[] { "Special delivery, comin' through!", "Pierre signed for it. Barely.", "Satchel's heavy today, boss." },
        [Role.Scavenger] = new[] { "One Brawnie's trash, boss...", "Found half a sandwich. Ah'm keepin' it.", "Lewis throws out good stuff. Don't tell him." },
        [Role.Shopkeeper] = new[] { "Pierre wants HOW much? Watch this.", "Joja's got a sale on. Don't tell Pierre.", "Back by noon with your seeds, sugar." },
        [Role.TreasureHunter] = new[] { "Somethin's down there, Ah can feel it.", "Found a bone. Might be old. Might be lunch.", "Every worm's a promise, boss." },
    };

    public string Name { get; }
    public Role Role { get; }
    private readonly int variant;
    private readonly Texture2D texture;
    private readonly Queue<Point> path = new();
    private int waitTicks = Game1.random.Next(30, 180), workTicks, facing, bubbleTicks, chatterTicks = Game1.random.Next(600, 3000), greetCooldown;
    private string? bubble;
    private Vector2? forage; // the forager's next pick
    private readonly int block; // which block of the sprite sheet: the role's, or a guild hero class (18+)
    /// <summary>A guild party member, not a crew hand.</summary>
    public bool Hero { get; }
    private readonly Queue<(Visitor Who, string Text)> banter = new();
    private int banterTicks = Game1.random.Next(600, 1500), lineTicks;
    private readonly HashSet<Vector2> cantReach = new();

    public Visitor(string name, Role role, int variant, Point start, Texture2D texture, int? block = null, bool hero = false)
    {
        this.block = block ?? (int)role;
        this.Hero = hero;
        this.Name = name;
        this.Role = role;
        this.variant = variant;
        this.texture = texture;
        this.position = new Vector2(start.X, start.Y) * 64;
    }

    public Rectangle Bounds => new((int)this.position.X, (int)this.position.Y, 64, 64);
    private Point Tile => new((int)Math.Round(this.position.X / 64), (int)Math.Round(this.position.Y / 64));

    public void Talk()
    {
        this.facing = Workers.FacingToward(this.position, Game1.player.Position); // look at the boss
        if (this.Role == Role.Fisher && Workers.State is { } state)
        {
            Staff.TalkFisher(state, this.Name);
            return;
        }
        bool sailor = this.Role == Role.Pirate;
        if (this.Hero && Workers.State is { } heroState && GuildPlay.Of(heroState, sailor).Members.FirstOrDefault(m => m.Name == this.Name) is { } member)
        {
            Personas.Ask(this.Name, this.Role, $"{this.Name} the {Guild.Title(member, sailor)} (Lv {member.Level}): \"{Personas.Chat(this.Name)}\"");
            return;
        }
        if (sailor && Workers.State is { } fleetState)
        {
            GuildPlay.CaptainTalk(fleetState, this.Name);
            return;
        }
        if (this.Role == Role.Adventurer && Workers.State is { } guildState)
        {
            GuildPlay.Talk(guildState, this.Name);
            return;
        }
        string line = AtWork.TryGetValue(this.Role, out var lines) ? lines[Game1.random.Next(lines.Length)] : "Workin' hard, boss.";
        Personas.Ask(this.Name, this.Role, $"{this.Name} the {Market.RoleInfo(this.Role).Title.ToLower()}: \"{line}\"");
    }

    public override bool update(GameTime time, GameLocation environment)
    {
        if (!Game1.shouldTimePass())
            return false; // paused, a menu or a dialogue is open (talking to them, too): freeze
        if (!Outposts.Present(this.Name, environment))
            return true; // shift's over: off home
        this.Chat();
        if (this.Role is Role.Adventurer or Role.Pirate && !this.Hero)
            this.Banter(environment);
        if (this.workTicks > 0)
        {
            if (--this.workTicks == 0 && this.forage is { } pick)
            {
                this.forage = null;
                if (Forager.PickLive(environment, pick) is { } item)
                {
                    Workers.PopItem(environment, this.position, item);
                    environment.localSound("harvest", pick);
                }
            }
            if (this.workTicks % 90 == 0 && this.Role is Role.Fisher or Role.Crabber && this.WaterNextTo(environment) is { } water)
                environment.temporarySprites.Add(new TemporaryAnimatedSprite(28, 100f, 2, 1, water * 64, false, false));
            return false;
        }
        if (this.waitTicks > 0)
        {
            this.waitTicks--;
            return false;
        }
        if (this.path.Count == 0 && this.Role == Role.Forager && this.forage == null && this.FindForage(environment))
            return false;
        if (this.path.Count == 0)
        {
            var spots = Outposts.Spots(environment, this.Role);
            var route = spots.Count > 0 ? PathGrid.Find(p => TilePath.Passable(environment, p), this.Tile, spots[Game1.random.Next(spots.Count)], maxNodes: 3000) : null;
            if (route == null)
                this.waitTicks = 120;
            else if (route.Count == 0)
                this.workTicks = this.WorkLength();
            else
                route.ForEach(this.path.Enqueue);
            return false;
        }

        var goal = new Vector2(this.path.Peek().X, this.path.Peek().Y) * 64;
        var delta = goal - this.position;
        if (delta.Length() <= 1.6f)
        {
            this.position = goal;
            this.path.Dequeue();
            if (this.path.Count == 0)
                this.workTicks = this.forage != null ? 90 : this.WorkLength();
            return false;
        }
        this.position += Vector2.Normalize(delta) * 1.6f;
        this.facing = Math.Abs(delta.X) > Math.Abs(delta.Y) ? (delta.X > 0 ? 1 : 3) : (delta.Y > 0 ? 0 : 2);
        return false;
    }

    /// <summary>The forager heads for the nearest real forage he can reach on this map.</summary>
    private bool FindForage(GameLocation location)
    {
        var me = this.Tile;
        var near = location.objects.Pairs
            .Where(p => Forager.IsForage(p.Value) && !this.cantReach.Contains(p.Key))
            .Select(p => p.Key)
            .OrderBy(t => Math.Abs(t.X - me.X) + Math.Abs(t.Y - me.Y))
            .Take(4)
            .ToList();
        foreach (var tile in near)
        {
            if (TilePath.Nearest(location, new Point((int)tile.X, (int)tile.Y), maxDistance: 1) is { } stand
                && PathGrid.Find(p => TilePath.Passable(location, p), me, stand, maxNodes: 8000) is { } route)
            {
                this.forage = tile;
                route.ForEach(this.path.Enqueue);
                if (route.Count == 0)
                    this.workTicks = 90; // already there: bend down and pick
                return true;
            }
            this.cantReach.Add(tile);
        }
        return false;
    }

    public void Say(string text, int ticks = 220) => (this.bubble, this.bubbleTicks) = (text, ticks);

    /// <summary>The Guild Master and his party ribbing each other: a little scene every minute or so.</summary>
    private void Banter(GameLocation location)
    {
        if (this.banter.Count > 0)
        {
            if (--this.lineTicks > 0)
                return;
            var (who, text) = this.banter.Dequeue();
            who.Say(text);
            this.lineTicks = 170;
            return;
        }
        if (--this.banterTicks > 0)
            return;
        this.banterTicks = Game1.random.Next(1800, 3600);
        var heroes = location.critters.OfType<Visitor>().Where(v => v.Hero).ToList();
        if (heroes.Count == 0 || Workers.State is not { } state)
            return;
        var hero = heroes[Game1.random.Next(heroes.Count)];
        bool fleet = this.Role == Role.Pirate;
        var g = GuildPlay.Of(state, fleet);
        string title = g.Members.FirstOrDefault(m => m.Name == hero.Name) is { } member ? Guild.Title(member, fleet) : fleet ? "pirate" : "hero";
        foreach (var (gm, text) in (fleet ? GuildPlay.SeaBanter(this.Name, hero.Name, title, Guild.Label(g.Stage)) : GuildPlay.Banter(this.Name, hero.Name, title, Guild.Label(g.Stage))))
            this.banter.Enqueue((gm ? this : hero, text));
        this.lineTicks = 1;
    }

    private int WorkLength() => this.Role is Role.Fisher ? Game1.random.Next(900, 1800) : Game1.random.Next(300, 600);

    private void Chat()
    {
        if (this.bubbleTicks > 0)
        {
            this.bubbleTicks--;
            return;
        }
        if (this.greetCooldown > 0)
            this.greetCooldown--;
        if (this.greetCooldown == 0 && Vector2.Distance(Game1.player.Position, this.position) < 64 * 2.5f)
        {
            (this.bubble, this.bubbleTicks, this.greetCooldown) = (Chatter.Greeting(), 150, 3600);
        }
        else if (--this.chatterTicks <= 0)
        {
            (this.bubble, this.bubbleTicks) = (Chatter.Line(this.Name, this.Role), 240);
            this.chatterTicks = Game1.random.Next(2400, 6000);
        }
    }

    private Vector2? WaterNextTo(GameLocation location)
    {
        var me = this.Tile;
        foreach (var d in new[] { new Point(0, 1), new Point(1, 0), new Point(-1, 0), new Point(0, -1) })
        {
            if (location.isWaterTile(me.X + d.X, me.Y + d.Y))
                return new Vector2(me.X + d.X, me.Y + d.Y);
        }
        return null;
    }

    public override void draw(SpriteBatch b)
    {
        double ms = Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
        var (row, frame) = this.workTicks > 0
            ? (4, (int)(ms / (this.Role is Role.Fisher ? 900 : 300)) % 2)
            : (this.facing, this.path.Count > 0 && Game1.shouldTimePass() ? (int)(ms / 150) % 4 : 0);
        Workers.DrawHand(b, this.texture, this.block, this.variant, row, frame, this.position);
    }

    public override void drawAboveFrontLayer(SpriteBatch b) => Workers.DrawBubble(b, this.bubble, this.bubbleTicks, this.position);

    public override Rectangle getBoundingBox(int xOffset, int yOffset) => new((int)this.position.X + 16, (int)this.position.Y + 32, 32, 32);
}
