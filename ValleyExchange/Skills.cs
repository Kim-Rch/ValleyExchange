using System;
using System.Collections.Generic;
using System.Linq;

namespace ValleyExchange;

/// <summary>What a perk does. Most are a number added up across chosen perks; a few are one-off specials.</summary>
public enum PerkKind
{
    /// <summary>Cheaper wages / fees (fraction off).</summary>
    Wage,
    /// <summary>Walk and work faster (fraction faster); the brewer's also ferments faster more often.</summary>
    Speed,
    /// <summary>More output (fraction more, or a chance of a bonus item).</summary>
    Yield,
    /// <summary>Chance of +1 quality star on what they make.</summary>
    Quality,
    /// <summary>Chance to shrug off a bad day (miner rocks, skunked fisher, rough hunt, slow sales).</summary>
    Luck,
    WideSpray,      // field: water 5x5
    NoSpills,       // oil: no spill fines
    Tough,          // adventurer: never comes back hurt
    HalfCut,        // salesman: keeps 5% instead of 10%
    CheapCharter,   // fisher: expeditions half price
    SeedSaver,      // harvester: chance to get the seed back
    Charm,          // courier: more friendship per gift (fraction more)
    LowRate,        // banker: loan interest cut (fraction off)
}

public sealed record PerkDef(string Name, PerkKind Kind, double Amount, string Text);

/// <summary>Crew skill trees: every role levels 1-25 from its work, gets small passives per level, and picks 1 of 2 perks every 5 levels.</summary>
public static class Skills
{
    public const int MaxLevel = 25, TierEvery = 5, DailyXp = 20, DailyWorkXpCap = 80;
    public static readonly int[] TierLevels = { 5, 10, 15, 20, 25 };

    /// <summary>XP needed to reach a level: 0, 30, 90, 180 ... 9,000 at level 25.</summary>
    public static int XpFor(int level) => 15 * (level - 1) * level;

    public static int Level(int xp)
    {
        int level = 1;
        while (level < MaxLevel && xp >= XpFor(level + 1))
            level++;
        return level;
    }

    public static int Xp(MarketState state, Role role) => state.SkillXp.GetValueOrDefault(role);
    public static int Level(MarketState state, Role role) => Level(Xp(state, role));

    /// <summary>Add XP; returns the new level if this crossed one, else 0.</summary>
    public static int AddXp(MarketState state, Role role, int amount)
    {
        if (amount <= 0)
            return 0;
        int before = Level(state, role);
        state.SkillXp[role] = Math.Min(Xp(state, role) + amount, XpFor(MaxLevel));
        int after = Level(state, role);
        return after > before ? after : 0;
    }

    /// <summary>The chosen option (0 or 1) per tier, or -1 if not chosen yet.</summary>
    public static int Pick(MarketState state, Role role, int tier) =>
        state.SkillPicks.TryGetValue(role, out var picks) && tier < picks.Count ? picks[tier] : -1;

    /// <summary>Choose a perk. Only once per tier, and only once the role has reached that tier's level.</summary>
    public static bool Choose(MarketState state, Role role, int tier, int option)
    {
        if (tier is < 0 or >= 5 || option is not (0 or 1) || Pick(state, role, tier) != -1 || Level(state, role) < TierLevels[tier])
            return false;
        if (!state.SkillPicks.TryGetValue(role, out var picks))
            state.SkillPicks[role] = picks = new List<int>();
        while (picks.Count <= tier)
            picks.Add(-1);
        picks[tier] = option;
        return true;
    }

    /// <summary>Tiers reached but not chosen yet.</summary>
    public static int Unpicked(MarketState state, Role role) =>
        Enumerable.Range(0, 5).Count(t => Level(state, role) >= TierLevels[t] && Pick(state, role, t) == -1);

    public static IEnumerable<PerkDef> Chosen(MarketState state, Role role) =>
        Enumerable.Range(0, 5).Where(t => Pick(state, role, t) is 0 or 1).Select(t => Tree(role)[t][Pick(state, role, t)]);

    public static bool Has(MarketState state, Role role, PerkKind kind) => Chosen(state, role).Any(p => p.Kind == kind);

    /// <summary>Total of a numeric effect: chosen perks plus the per-level passive (wages -0.5%, output +1% a level), capped.</summary>
    public static double Total(MarketState state, Role role, PerkKind kind)
    {
        int level = Level(state, role);
        double sum = Chosen(state, role).Where(p => p.Kind == kind).Sum(p => p.Amount) + Researched(state, role, kind);
        if (kind == PerkKind.Wage)
            sum += 0.005 * level;
        if (kind == PerkKind.Yield)
            sum += 0.01 * level;
        return kind switch
        {
            PerkKind.Wage => Math.Min(sum, 0.6),
            PerkKind.Quality => Math.Min(sum, 0.75),
            PerkKind.Luck => Math.Min(sum, 0.9),
            PerkKind.LowRate => Math.Min(sum, 0.75),
            _ => sum,
        };
    }

    /// <summary>What the scientist's research adds to a crew stat.</summary>
    private static double Researched(MarketState state, Role role, PerkKind kind)
    {
        double R(Effect effect) => Research.Total(state, effect);
        return kind switch
        {
            PerkKind.Wage => R(Effect.CrewWage),
            PerkKind.Speed => R(Effect.CrewSpeed) + role switch
            {
                Role.Blacksmith => R(Effect.SmithSpeed),
                Role.Brewer or Role.Artisan => R(Effect.ArtisanSpeed),
                _ => 0,
            },
            PerkKind.Yield => R(Effect.CrewOutput) + role switch
            {
                Role.Forager => R(Effect.ForagerHaul),
                Role.Miner => R(Effect.MinerHaul),
                Role.Fisher or Role.Crabber => R(Effect.FisherHaul),
                Role.Blacksmith => R(Effect.SmithOutput),
                Role.Oil => R(Effect.OilYield),
                Role.Salesman => R(Effect.SalesMarkup),
                Role.Banker => R(Effect.Interest),
                Role.Courier => R(Effect.CourierDeliveries),
                Role.Ranch => R(Effect.RanchOutput),
                Role.Chef => R(Effect.ChefOutput),
                Role.Pirate => R(Effect.FleetGold),
                _ => 0,
            },
            PerkKind.Quality => role switch
            {
                Role.Brewer or Role.Artisan => R(Effect.ArtisanQuality),
                Role.Ranch => R(Effect.RanchQuality),
                Role.Chef => R(Effect.ChefQuality),
                _ => 0,
            },
            PerkKind.Luck => role == Role.Adventurer ? R(Effect.AdventurerLuck) : 0,
            _ => 0,
        };
    }

    /// <summary>A wage after the role's discounts.</summary>
    public static int Wage(MarketState state, Role role, int wage) => (int)Math.Ceiling(wage * (1 - Total(state, role, PerkKind.Wage)));

    /// <summary>The perk choices for a role: 5 tiers (levels 5/10/15/20/25) of 2 options.</summary>
    public static PerkDef[][] Tree(Role role) => Trees[role];

    /// <summary>"20%" regardless of the Windows number format.</summary>
    public static string Pct(double fraction) => $"{fraction * 100:0.#}%";

    private static PerkDef W(string name, double amount) => new(name, PerkKind.Wage, amount, $"-{Pct(amount)} wages");
    private static PerkDef S(string name, double amount) => new(name, PerkKind.Speed, amount, $"+{Pct(amount)} work speed");
    private static PerkDef Y(string name, double amount, string what) => new(name, PerkKind.Yield, amount, $"+{Pct(amount)} {what}");
    private static PerkDef Q(string name, double amount, string what) => new(name, PerkKind.Quality, amount, $"{Pct(amount)}: +1 star on {what}");
    private static PerkDef L(string name, double amount, string what) => new(name, PerkKind.Luck, amount, $"{Pct(amount)} dodge {what}");
    private static PerkDef C(string name, double amount) => new(name, PerkKind.Charm, amount, $"+{Pct(amount)} friendship per gift");
    private static PerkDef R(string name, double amount) => new(name, PerkKind.LowRate, amount, $"-{Pct(amount)} loan interest");
    private static PerkDef Lab(string name, double amount) => new(name, PerkKind.Speed, amount, $"+{Pct(amount)} research speed");
    private static PerkDef[] T(PerkDef a, PerkDef b) => new[] { a, b };

    private static readonly Dictionary<Role, PerkDef[][]> Trees = new()
    {
        [Role.Field] = new[]
        {
            T(S("Steady Hands", 0.20), W("Cheap Labor", 0.15)),
            T(new("Wide Spray", PerkKind.WideSpray, 1, "Waters 5x5"), Q("Green Thumb", 0.10, "crops")),
            T(S("Long Legs", 0.25), W("Bulk Rates", 0.20)),
            T(Q("Master Grower", 0.15, "crops"), S("Quick Feet", 0.30)),
            T(S("Rain Dancer", 0.40), W("Family Discount", 0.30)),
        },
        [Role.Ranch] = new[]
        {
            T(Q("Gentle Touch", 0.10, "produce"), W("Cheap Labor", 0.15)),
            T(Y("Double Udder", 0.15, "bonus produce"), Q("Animal Whisperer", 0.15, "produce")),
            T(W("Bulk Rates", 0.20), Y("Fat & Happy", 0.20, "bonus produce")),
            T(Q("Blue Ribbon", 0.20, "produce"), Y("Overflowing Pails", 0.25, "bonus produce")),
            T(Q("Golden Touch", 0.30, "produce"), Y("Abundance", 0.35, "bonus produce")),
        },
        [Role.Fisher] = new[]
        {
            T(Y("Big Net", 0.20, "fish"), Q("Keen Eye", 0.10, "fish")),
            T(L("Lucky Lure", 0.30, "skunked days"), new("Cheap Charter", PerkKind.CheapCharter, 1, "Half-price expeditions")),
            T(Y("Deep Water", 0.25, "fish"), Q("Steady Reel", 0.15, "fish")),
            T(L("Fish Whisperer", 0.40, "skunked days"), Y("Two Rods", 0.30, "fish")),
            T(Q("Legend Hunter", 0.25, "fish"), Y("Trawler", 0.50, "fish")),
        },
        [Role.Crabber] = new[]
        {
            T(Y("Sticky Bait", 0.20, "bonus catch"), W("Cheap Labor", 0.15)),
            T(Q("Picky Picker", 0.15, "catches"), Y("Bigger Traps", 0.25, "bonus catch")),
            T(W("Bulk Rates", 0.25), Q("Prize Crabs", 0.20, "catches")),
            T(Y("Crab King", 0.35, "bonus catch"), W("Family Discount", 0.30)),
            T(Q("Golden Claws", 0.30, "catches"), Y("Ocean's Bounty", 0.50, "bonus catch")),
        },
        [Role.Miner] = new[]
        {
            T(L("Prospector", 0.25, "rock days"), Y("Strong Back", 0.20, "mining haul")),
            T(W("Cheap Labor", 0.15), L("Rock Reader", 0.25, "rock days")),
            T(Y("Deep Veins", 0.25, "mining haul"), L("Gem Sense", 0.30, "rock days")),
            T(W("Bulk Rates", 0.25), Y("Ore Hauler", 0.35, "mining haul")),
            T(L("Mother Lode", 0.50, "rock days"), Y("Strip Miner", 0.50, "mining haul")),
        },
        [Role.Adventurer] = new[]
        {
            T(Y("Looter", 0.20, "dungeon gold"), L("Rally", 0.25, "a lost run (one retry)")),
            T(new("Tough as Nails", PerkKind.Tough, 1, "The party never gets injured"), W("Cheap Labor", 0.15)),
            T(Y("Treasure Nose", 0.25, "dungeon gold"), L("Veteran", 0.30, "a lost run (one retry)")),
            T(W("Bulk Rates", 0.25), Y("Monster Slayer", 0.35, "dungeon gold")),
            T(L("Legend", 0.50, "a lost run (one retry)"), Y("Dragon Hoard", 0.50, "dungeon gold")),
        },
        [Role.Chef] = new[]
        {
            T(Q("Plating", 0.15, "dishes"), Y("Big Pots", 0.20, "dishes")),
            T(W("Cheap Labor", 0.15), Q("Secret Spice", 0.20, "dishes")),
            T(Y("Line Cooks", 0.30, "dishes"), W("Bulk Rates", 0.25)),
            T(Q("Five Star", 0.30, "dishes"), Y("Buffet", 0.40, "dishes")),
            T(Q("Michelin", 0.50, "dishes"), Y("Banquet", 0.60, "dishes")),
        },
        [Role.Salesman] = new[]
        {
            T(L("Smooth Talker", 0.25, "slow days"), W("Cheap Labor", 0.15)),
            T(Y("Silver Tongue", 0.10, "markup"), new("Honest Slick", PerkKind.HalfCut, 1, "His cut: 5%")),
            T(L("Regulars", 0.30, "slow days"), W("Bulk Rates", 0.25)),
            T(Y("Gold Tongue", 0.15, "markup"), L("Hype Man", 0.40, "slow days")),
            T(L("The Closer", 0.60, "slow days"), Y("Wolf of Pelican Town", 0.25, "markup")),
        },
        [Role.Oil] = new[]
        {
            T(Y("Tuned Pumps", 0.15, "crude"), W("Cheap Labor", 0.15)),
            T(new("Safety First", PerkKind.NoSpills, 1, "No oil spills"), Y("Deeper Wells", 0.20, "crude")),
            T(W("Bulk Rates", 0.25), Y("Fracking", 0.25, "crude")),
            T(Y("Black Gold", 0.30, "crude"), W("Family Discount", 0.30)),
            T(Y("Gusher Whisperer", 0.50, "crude"), W("Oil Baron's Friend", 0.40)),
        },
        [Role.Harvester] = new[]
        {
            T(S("Nimble Fingers", 0.20), Q("Careful Picker", 0.10, "picks")),
            T(new("Seed Saver", PerkKind.SeedSaver, 0.15, "15% seed back"), W("Cheaper Picks", 0.20)),
            T(S("Two Baskets", 0.25), Q("Choice Cuts", 0.15, "picks")),
            T(W("Bulk Rates", 0.30), S("Speed Picker", 0.30)),
            T(Q("Prize Harvest", 0.25, "picks"), S("Whirlwind", 0.40)),
        },
        [Role.Forager] = new[]
        {
            T(Y("Sharp Eyes", 0.20, "forage & wood"), Q("Choice Finds", 0.10, "forage")),
            T(W("Cheap Labor", 0.15), Y("Woodsman", 0.25, "forage & wood")),
            T(Q("Botanist", 0.20, "forage"), Y("Trailblazer", 0.30, "forage & wood")),
            T(W("Bulk Rates", 0.25), Q("Herbalist", 0.25, "forage")),
            T(Y("Lord of the Woods", 0.50, "forage & wood"), Q("Druid", 0.35, "forage")),
        },
        [Role.Brewer] = new[]
        {
            T(S("Quick Pour", 0.15), Q("Fine Palate", 0.10, "brews")),
            T(Y("Ingredient Saver", 0.20, "free brews"), W("Cheap Labor", 0.15)),
            T(S("Hot Cellar", 0.20), Q("Connoisseur", 0.15, "brews")),
            T(Y("Thrifty Brewer", 0.30, "free brews"), W("Bulk Rates", 0.25)),
            T(Q("Iridium Palate", 0.30, "brews"), S("Brewmaster", 0.35)),
        },
        [Role.Blacksmith] = new[]
        {
            T(S("Hot Forge", 0.15), W("Cheap Labor", 0.15)),
            T(Y("Coal Saver", 0.20, "free smelts & geodes"), S("Big Bellows", 0.20)),
            T(W("Bulk Rates", 0.25), Y("Rock Breaker", 0.25, "free smelts & geodes")),
            T(S("Blast Furnace", 0.30), W("Family Discount", 0.30)),
            T(Y("Master Smith", 0.40, "free smelts & geodes"), S("Dwarven Forge", 0.40)),
        },
        [Role.Artisan] = new[]
        {
            T(S("Steady Hands", 0.15), Q("Fine Craft", 0.10, "goods")),
            T(Y("Waste Not", 0.20, "free batches"), W("Cheap Labor", 0.15)),
            T(S("Assembly Line", 0.20), Q("Old Recipes", 0.15, "goods")),
            T(Y("Thrifty", 0.30, "free batches"), W("Bulk Rates", 0.25)),
            T(Q("Masterwork", 0.30, "goods"), S("Workshop Wizard", 0.35)),
        },
        [Role.Courier] = new[]
        {
            T(Y("Fast Feet", 0.25, "deliveries"), W("Cheap Labor", 0.15)),
            T(C("Charmer", 0.15), Y("Shortcuts", 0.25, "deliveries")),
            T(W("Bulk Rates", 0.25), C("Sweet Talker", 0.20)),
            T(Y("Pony Express", 0.50, "deliveries"), C("Gift Wrap", 0.25)),
            T(C("Everybody's Friend", 0.35), Y("Town Legend", 0.75, "deliveries")),
        },
        [Role.Banker] = new[]
        {
            T(Y("Compound Interest", 0.15, "savings interest"), W("Cheap Labor", 0.15)),
            T(R("Friends in High Places", 0.20), Y("Sharp Pencil", 0.20, "savings interest")),
            T(W("Bulk Rates", 0.25), R("Good Credit", 0.25)),
            T(Y("Old Money", 0.30, "savings interest"), R("Gentleman's Handshake", 0.30)),
            T(Y("Bank of Thornbury", 0.50, "savings interest"), R("Sweetheart Deal", 0.40)),
        },
        [Role.Scientist] = new[]
        {
            T(Y("Grant Writer", 0.15, "research points"), W("Cheap Labor", 0.15)),
            T(Lab("Lab Assistant", 0.20), Y("Peer Review", 0.20, "research points")),
            T(W("Tenure", 0.25), Lab("Night Shift", 0.25)),
            T(Y("Breakthrough", 0.30, "research points"), Lab("Eureka!", 0.30)),
            T(Y("Nobel Brawnie", 0.50, "research points"), Lab("Mad Genius", 0.50)),
        },
        [Role.Groundskeeper] = new[]
        {
            T(S("Sharp Blades", 0.15), W("Cheap Labor", 0.15)),
            T(S("Strong Arms", 0.20), W("Bulk Rates", 0.20)),
            T(S("Second Wind", 0.25), W("Union Card", 0.20)),
            T(S("Clear Cutter", 0.30), W("Family Discount", 0.25)),
            T(S("Bulldozer", 0.40), W("Old Friend", 0.30)),
        },
        [Role.Scavenger] = new[]
        {
            T(Y("Sharp Eyes", 0.15, "finds (a second item from a can)"), W("Cheap Labor", 0.15)),
            T(W("Bulk Rates", 0.20), Y("Dumpster Diver", 0.20, "finds (a second item from a can)")),
            T(Y("Raccoon Whisperer", 0.25, "finds (a second item from a can)"), W("Union Card", 0.25)),
            T(W("Family Discount", 0.30), Y("Treasure Nose", 0.30, "finds (a second item from a can)")),
            T(Y("King of Trash", 0.50, "finds (a second item from a can)"), W("Old Friend", 0.40)),
        },
        [Role.Pirate] = new[]
        {
            T(Y("Plunderer", 0.20, "voyage gold"), L("Second Broadside", 0.25, "a lost voyage (one more go)")),
            T(new("Iron Hull", PerkKind.Tough, 1, "The crew never gets hurt"), W("Cheap Labor", 0.15)),
            T(Y("Buried Treasure", 0.25, "voyage gold"), L("Sea Legs", 0.30, "a lost voyage (one more go)")),
            T(W("Bulk Rates", 0.25), Y("Privateer", 0.35, "voyage gold")),
            T(L("Dread Pirate", 0.50, "a lost voyage (one more go)"), Y("King's Ransom", 0.50, "voyage gold")),
        },
        [Role.Shopkeeper] = new[]
        {
            T(Y("Sweet Talk", 0.03, "haggle discount"), W("Cheap Labor", 0.15)),
            T(L("Silver Tongue", 0.25, "a failed haggle"), Y("Coupon Clipper", 0.04, "haggle discount")),
            T(Y("Regular Customer", 0.05, "haggle discount"), W("Union Card", 0.25)),
            T(L("Pierre's Favorite", 0.40, "a failed haggle"), Y("Bulk Buyer", 0.06, "haggle discount")),
            T(Y("Shark", 0.08, "haggle discount"), L("Can't Say No to Mae", 0.60, "a failed haggle")),
        },
        [Role.Scareboy] = new[]
        {
            T(Y("Long Arms", 0.20, "fertilized crops and crow loot"), W("Cheap Labor", 0.15)),
            T(W("Bulk Rates", 0.20), Y("Scary Face", 0.25, "fertilized crops and crow loot")),
            T(Y("Fresh Straw", 0.30, "fertilized crops and crow loot"), W("Union Card", 0.25)),
            T(W("Family Discount", 0.30), Y("Crow's Bane", 0.35, "fertilized crops and crow loot")),
            T(Y("Harvest Moon Horror", 0.50, "fertilized crops and crow loot"), W("Old Friend", 0.40)),
        },
        [Role.TreasureHunter] = new[]
        {
            T(Y("Keen Shovel", 0.15, "digs"), W("Cheap Labor", 0.15)),
            T(W("Bulk Rates", 0.20), Y("Dowsing Rod", 0.20, "digs")),
            T(Y("Old Maps", 0.25, "digs"), W("Union Card", 0.25)),
            T(W("Family Discount", 0.30), Y("Metal Detector", 0.35, "digs")),
            T(Y("X Marks the Spot", 0.50, "digs"), W("Old Friend", 0.40)),
        },
    };
}
