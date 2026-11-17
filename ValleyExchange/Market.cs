using System;
using System.Collections.Generic;
using System.Linq;

namespace ValleyExchange;

public enum Sector { Tech, Retail, Food, Auto, Media, Finance, Energy }

/// <summary>Hired-hand roles. Order matches the sprite sheet rows (tools/make_crew_sprites.py).</summary>
/// <summary>How a hand's day went off the farm.</summary>
public enum DayLuck { Bad, Normal, Good, Jackpot }

public enum Role { Field, Ranch, Fisher, Crabber, Miner, Adventurer, Chef, Salesman, Oil, Harvester, Forager, Brewer, Blacksmith, Artisan, Courier, Banker, Scientist, Groundskeeper, Scavenger, Pirate, TreasureHunter, Shopkeeper, Scareboy }

public sealed record RoleDef(Role Role, string Title, string[] Names, string Wage, string Pitch);

public sealed record CompanyDef(string Ticker, string Name, Sector Sector, double BasePrice, double Volatility, double SeasonDividend, string Blurb);

public sealed class StockState
{
    public double Price { get; set; }
    public double Trend { get; set; }
    /// <summary>Daily closing prices, oldest first.</summary>
    public List<double> History { get; set; } = new();
}

public sealed class Holding
{
    public int Shares { get; set; }
    /// <summary>Total gold paid for the shares currently held.</summary>
    public long CostBasis { get; set; }
}

public sealed class MarketState
{
    public int LastDay { get; set; } = -1;
    public Dictionary<string, StockState> Stocks { get; set; } = new();
    public Dictionary<string, Holding> Holdings { get; set; } = new();
    /// <summary>Newest first.</summary>
    public List<string> News { get; set; } = new();
    public bool RanchHands { get; set; }
    public bool FieldHands { get; set; }
    /// <summary>Old saves only: hired specialists. <see cref="Market.Migrate"/> moves these flags into <see cref="Crew"/>.</summary>
    public List<Role> Staff { get; set; } = new();
    /// <summary>How many hands of each role are hired.</summary>
    public Dictionary<Role, int> Crew { get; set; } = new();
    /// <summary>Gold the salesman owes you when he gets back, and the day he does (-1 = not on the road).</summary>
    public long TripPayout { get; set; }
    public long TripCut { get; set; }
    /// <summary>What sold on the trip: item id -> (count, base value), for the report.</summary>
    public Dictionary<string, long[]> TripItems { get; set; } = new();
    public int TripSold { get; set; }
    public int TripOffered { get; set; }
    public int TripReturnDay { get; set; } = -1;
    public bool TripToCity { get; set; }
    public int AdventurerRestUntil { get; set; } = -1;
    /// <summary>The day you paid for a fishing expedition (every water in the valley), or -1.</summary>
    public int ExpeditionDay { get; set; } = -1;
    /// <summary>Skill XP per role, and the perk picked at each tier (0/1, -1 = not yet).</summary>
    public Dictionary<Role, int> SkillXp { get; set; } = new();
    public Dictionary<Role, List<int>> SkillPicks { get; set; } = new();
    /// <summary>Field hands plant empty tilled soil with in-season seeds from the crate on their own.</summary>
    public bool AutoPlant { get; set; } = true;
    /// <summary>The courier runs gifts from the crate to the villagers.</summary>
    public bool CourierGifts { get; set; } = true;
    /// <summary>The bank: savings (earning interest), what you owe, and the wallet floor he tops you up to each morning.</summary>
    public long Savings { get; set; }
    public long Loan { get; set; }
    public int WalletFloor { get; set; }
    /// <summary>The shopkeeper: most she'll spend on seeds a day, whether she fills empty soil on her own, and your pinned seeds (qualified id → how many to keep in the crate).</summary>
    public int ShopBudget { get; set; } = 5_000;
    public bool ShopAuto { get; set; } = true;
    public Dictionary<string, int> ShopList { get; set; } = new();
    /// <summary>The lab: finished projects, the one in progress (and the day it's done), and banked research points.</summary>
    public List<string> Researched { get; set; } = new();
    public string? Researching { get; set; }
    public int ResearchDoneDay { get; set; }
    public int ResearchPoints { get; set; }
    /// <summary>What the groundskeepers keep clear (job names: Weeds, Branches, Stones, Grass, Trees).</summary>
    public List<string> ClearKinds { get; set; } = new() { "Weeds", "Branches", "Stones" };
    /// <summary>The Guild Master's party and the endless dungeon.</summary>
    public GuildState Guild { get; set; } = new();
    /// <summary>The pirate captain's crew and ship (same shape as the guild).</summary>
    public GuildState Fleet { get; set; } = new();
}

public readonly record struct DayContext(int Season, int DayOfMonth, bool Rainy, bool Festival);

public sealed record DayResult(IReadOnlyList<string> Headlines, long Dividends);

/// <summary>Pure market simulation: no game types, so it can be checked outside Stardew.</summary>
public static class Market
{
    public const int HistoryDays = 28;
    public const int MaxNews = 8;

    public static readonly IReadOnlyList<CompanyDef> Companies = new CompanyDef[]
    {
        new("NILE", "Nile", Sector.Retail, 1800, 0.030, 0.000, "Sells everything. Delivers it by river barge."),
        new("PEAR", "Pear Inc.", Sector.Tech, 2200, 0.025, 0.005, "Overpriced gadgets. Everyone owns one anyway."),
        new("MCRH", "Macrohard", Sector.Tech, 4100, 0.020, 0.007, "Makes the OS on Mayor Lewis's ancient PC."),
        new("GOGL", "Goggle", Sector.Tech, 1700, 0.025, 0.003, "Search engine. Knows what you did at the Saloon."),
        new("TSLO", "Telsa Motors", Sector.Auto, 2400, 0.050, 0.000, "Electric carts. The CEO posts a lot."),
        new("ENVY", "Envydia", Sector.Tech, 1300, 0.045, 0.001, "Graphics chips. Up 900% because of 'AI'."),
        new("KOLA", "Koala-Cola", Sector.Food, 600, 0.012, 0.030, "Fizzy drinks. Boring, reliable, pays dividends."),
        new("MCDG", "McDougal's", Sector.Food, 2800, 0.015, 0.022, "Burgers. Gus takes them as a personal insult."),
        new("MOON", "Moonbucks", Sector.Food, 900, 0.020, 0.025, "Overpriced coffee. Day traders' fuel."),
        new("NFLK", "Netflicks", Sector.Media, 6500, 0.035, 0.000, "Streaming. Booms when it rains."),
        new("DIZY", "Dizzney", Sector.Media, 1100, 0.020, 0.010, "Movies and theme parks. Owns half of Zuzu City."),
        new("JOJA", "JojaCorp", Sector.Retail, 500, 0.030, 0.040, "Morris's pride and joy. Life's better with Joja."),
        new("VMRT", "Valmart", Sector.Retail, 950, 0.012, 0.012, "Everything cheap. Greeters included."),
        new("PSYK", "Psyke", Sector.Retail, 800, 0.020, 0.010, "Sneakers. Just do it... eventually."),
        new("BETA", "Beta Platforms", Sector.Tech, 3000, 0.035, 0.002, "Social network. Your grandma is on it."),
        new("SMSG", "Samsong", Sector.Tech, 700, 0.020, 0.015, "Phones that sing. Occasionally they explode."),
        new("OUTL", "Outel", Sector.Tech, 300, 0.030, 0.020, "Chips. Losing to Envydia, still trying."),
        new("PEPS", "Pepsee", Sector.Food, 1500, 0.012, 0.028, "Koala-Cola's eternal rival."),
        new("PHNY", "Phony", Sector.Media, 900, 0.022, 0.006, "Game consoles. Abigail owns three."),
        new("SPFY", "Spotifly", Sector.Media, 3000, 0.035, 0.000, "Music streaming. Sam's band still isn't on it."),
        new("BOIN", "Boing", Sector.Auto, 1900, 0.040, 0.000, "Airplanes. Doors sold separately."),
        new("TOYD", "Toyoda", Sector.Auto, 1800, 0.015, 0.020, "Reliable carts. Your dad drives one."),
        new("UBRR", "Ubear", Sector.Auto, 700, 0.035, 0.000, "Rideshare. The driver is a bear."),
        new("VEES", "Veesa", Sector.Finance, 2600, 0.014, 0.008, "Payment cards. Accepted everywhere except Krobus's."),
        new("GSNX", "Goldman Snacks", Sector.Finance, 4500, 0.025, 0.020, "Investment bank. Eats your fees."),
        new("JPMG", "JP Morgum", Sector.Finance, 2100, 0.018, 0.025, "Big bank. Very big. Too big."),
        new("SHFS", "Shellfish Oil", Sector.Energy, 650, 0.020, 0.035, "Oil. Willy hates them."),
        new("EXOF", "ExxOff", Sector.Energy, 1100, 0.018, 0.030, "More oil. Willy hates them even more."),
    };

    private static readonly (string Text, double Size)[] GoodNews =
    {
        ("{0} beats earnings! The Stardrop Saloon erupts in cheers", 0.08),
        ("{0} CEO buys a round for the whole Saloon", 0.05),
        ("Mayor Lewis spotted wearing {0} merch", 0.06),
        ("{0} unveils a new product, Pelican Town lines up overnight", 0.12),
        ("Junimos rumored to be loyal {0} customers", 0.10),
        ("{0} lands a huge Zuzu City government contract", 0.15),
    };

    private static readonly (string Text, double Size)[] BadNews =
    {
        ("Scandal! {0} caught dumping waste in Cindersap Forest", 0.15),
        ("{0} recalls products after complaints from Pam", 0.08),
        ("The Wizard curses {0} headquarters", 0.10),
        ("{0} sued by the Ferngill Republic", 0.12),
        ("Shadow People raid a {0} warehouse", 0.09),
        ("Clint says {0} 'isn't all that', stock slips", 0.04),
    };

    private static readonly string[] QuietNews =
    {
        "Quiet day on the Valley Exchange.",
        "Traders spend the day fishing. Markets drift.",
        "Gus reports record coffee sales to day traders.",
        "Linus offers stock tips nobody asked for.",
    };

    public static CompanyDef Def(string ticker) => Companies.First(c => c.Ticker == ticker);

    public static MarketState NewState(int seed)
    {
        var state = new MarketState();
        EnsureStocks(state, seed);
        return state;
    }

    /// <summary>Add any companies missing from the state (new game, or companies added by a mod update).</summary>
    public static void EnsureStocks(MarketState state, int seed)
    {
        var rng = new Random(seed);
        foreach (var c in Companies)
        {
            if (state.Stocks.ContainsKey(c.Ticker))
                continue;
            var stock = new StockState { Price = c.BasePrice };
            // warm-up so charts aren't flat on the first day
            for (int i = 0; i < HistoryDays; i++)
            {
                stock.Price = Step(stock.Price, c, 0, rng);
                stock.History.Add(stock.Price);
            }
            state.Stocks[c.Ticker] = stock;
        }
    }

    /// <summary>Close yesterday's session and open today's. Seeded by day, so reloading a save gives the same prices.</summary>
    public static DayResult AdvanceDay(MarketState state, int day, int seed, DayContext ctx)
    {
        var rng = new Random(unchecked(seed * 31 + day * 7919));
        var headlines = new List<string>();

        // new season: pay last season's dividends at yesterday's close
        long dividends = 0;
        if (ctx.DayOfMonth == 1)
        {
            foreach (var c in Companies)
            {
                if (state.Holdings.TryGetValue(c.Ticker, out var h) && h.Shares > 0)
                    dividends += (long)Math.Round(h.Shares * state.Stocks[c.Ticker].Price * c.SeasonDividend);
            }
        }

        string? eventTicker = null;
        double shock = 0;
        if (rng.NextDouble() < 0.4)
        {
            var c = Companies[rng.Next(Companies.Count)];
            bool good = rng.NextDouble() < 0.5;
            var pool = good ? GoodNews : BadNews;
            var (text, size) = pool[rng.Next(pool.Length)];
            eventTicker = c.Ticker;
            shock = good ? size : -size;
            headlines.Add(string.Format(text, c.Name));
        }

        foreach (var c in Companies)
        {
            var stock = state.Stocks[c.Ticker];
            stock.Trend = Math.Clamp(0.92 * stock.Trend + 0.0015 * Normal(rng), -0.01, 0.01);
            double bias = stock.Trend + SectorBias(c.Sector, ctx) + (c.Ticker == eventTicker ? shock : 0);
            stock.Price = Step(stock.Price, c, bias, rng);
            stock.History.Add(stock.Price);
            if (stock.History.Count > HistoryDays)
                stock.History.RemoveAt(0);
        }

        if (headlines.Count == 0)
            headlines.Add(QuietNews[rng.Next(QuietNews.Length)]);
        if (dividends > 0)
            headlines.Add($"Dividend day! You were paid {dividends:N0}g.");

        foreach (var line in headlines)
            state.News.Insert(0, line);
        if (state.News.Count > MaxNews)
            state.News.RemoveRange(MaxNews, state.News.Count - MaxNews);

        state.LastDay = day;
        return new DayResult(headlines, dividends);
    }

    public static int SharePrice(MarketState state, string ticker) => Math.Max(1, (int)Math.Round(state.Stocks[ticker].Price));

    public static double DayChange(MarketState state, string ticker)
    {
        var h = state.Stocks[ticker].History;
        return h.Count < 2 ? 0 : (h[^1] - h[^2]) / h[^2];
    }

    public static Holding GetHolding(MarketState state, string ticker)
    {
        if (!state.Holdings.TryGetValue(ticker, out var h))
            state.Holdings[ticker] = h = new Holding();
        return h;
    }

    /// <summary>Buy up to <paramref name="qty"/> shares, as many as <paramref name="money"/> covers.</summary>
    public static (int Shares, long Gold) Buy(MarketState state, string ticker, int qty, long money)
    {
        int price = SharePrice(state, ticker);
        int shares = (int)Math.Min(qty, money / price);
        if (shares <= 0)
            return (0, 0);
        long cost = (long)price * shares;
        var h = GetHolding(state, ticker);
        h.Shares += shares;
        h.CostBasis += cost;
        return (shares, cost);
    }

    /// <summary>Sell up to <paramref name="qty"/> shares (capped at what you own).</summary>
    public static (int Shares, long Gold) Sell(MarketState state, string ticker, int qty)
    {
        var h = GetHolding(state, ticker);
        int shares = Math.Min(qty, h.Shares);
        if (shares <= 0)
            return (0, 0);
        h.CostBasis -= h.CostBasis * shares / h.Shares;
        h.Shares -= shares;
        return (shares, (long)SharePrice(state, ticker) * shares);
    }

    public static long PortfolioValue(MarketState state) =>
        state.Holdings.Sum(kv => (long)kv.Value.Shares * SharePrice(state, kv.Key));

    public static long Profit(MarketState state) => PortfolioValue(state) - state.Holdings.Values.Sum(h => h.CostBasis);

    // ---- oil: crude follows the energy stocks ----
    public const int CrudeBasePrice = 150;
    public const double GasolineMultiplier = 1.8;
    private static readonly string[] OilTickers = { "SHFS", "EXOF" };

    /// <summary>1.0 when the oil stocks sit at their base price.</summary>
    public static double OilIndex(MarketState state, int daysAgo = 0) =>
        OilTickers.Average(t => { var h = state.Stocks[t].History; return h[Math.Max(0, h.Count - 1 - daysAgo)] / Def(t).BasePrice; });

    public static int CrudePrice(MarketState state) => Math.Max(1, (int)Math.Round(CrudeBasePrice * OilIndex(state)));
    public static int GasolinePrice(MarketState state) => (int)Math.Round(CrudePrice(state) * GasolineMultiplier);
    public static double CrudeChange(MarketState state) => OilIndex(state) / OilIndex(state, 1) - 1;

    /// <summary>Barrels a day for a well on this tile: 25% dry, 60% decent (3-6), 15% gusher (12-18). Fixed per save + tile.</summary>
    public static int WellBarrels(int seed, string location, int x, int y)
    {
        uint h = 2166136261; // FNV-1a: string.GetHashCode is randomized per process, this isn't
        foreach (char ch in $"{seed}|{location}|{x}|{y}")
            h = (h ^ ch) * 16777619;
        h ^= h >> 15;
        h *= 0x2c1b3c6d;
        h ^= h >> 12;
        uint roll = h % 100, pick = h / 100;
        return roll < 25 ? 0 : roll < 85 ? 3 + (int)(pick % 4) : 12 + (int)(pick % 7);
    }

    public const int GusherBarrels = 12;

    public static string WellReport(int barrels) =>
        barrels == 0 ? "Dry hole... pick it up and drill somewhere else."
        : barrels >= GusherBarrels ? $"GUSHER! About {barrels} barrels a day!"
        : $"Struck oil: about {barrels} barrels a day.";

    /// <summary>Daily ranch hand payroll.</summary>
    public const int RanchWagePerAnimal = 20, RanchWageMin = 200;
    public static int RanchWage(int animals) => Math.Max(RanchWageMin, animals * RanchWagePerAnimal);

    /// <summary>Daily field hand payroll.</summary>
    public const int FieldWagePerCrop = 2, FieldWageMin = 200;
    public static int FieldWage(int crops) => Math.Max(FieldWageMin, crops * FieldWagePerCrop);

    // ---- the crew ----
    public const int MaxPerRole = 10, ExtraHandWage = 100, CrabWagePerPot = 15, CrabWageMin = 200, OilWagePerMachine = 50, OilWageMin = 300, HarvestFee = 10;

    public static readonly IReadOnlyList<RoleDef> Roles = new RoleDef[]
    {
        new(Role.Field, "Field hands", new[] { "Dusty", "Hank", "Earl", "Clem", "Jeb", "Otis", "Merle", "Hoyt", "Amos", "Luther" }, $"{FieldWagePerCrop}g/crop", "Water and harvest every morning. Clearing jobs."),
        new(Role.Ranch, "Ranch hands", new[] { "Bubba", "Waylon", "Cletus", "Darla", "Boone", "Coy", "Wade", "Tex", "Hattie", "Cody" }, $"{RanchWagePerAnimal}g/animal", "Tend the animals every morning. Clearing jobs."),
        new(Role.Fisher, "Fisherman", new[] { "Salty", "Gil", "Finn", "Marlin", "Skip", "Bo", "Hal", "Wes", "Nate", "Cappy" }, "300g/day", "Fishes all day, the catch goes in the crate."),
        new(Role.Crabber, "Crab catcher", new[] { "Marla" }, $"{CrabWagePerPot}g/pot", "Empties and re-baits all your crab pots."),
        new(Role.Miner, "Miner", new[] { "Buck", "Flint", "Rocky", "Slate", "Stony", "Clay", "Cobb", "Grit", "Jasper", "Brock" }, "400g/day", "Brings ore, coal, geodes and gems from the mines."),
        new(Role.Adventurer, "Guild Master", new[] { "Rex" }, "500g/day + party", "Runs the guild: hire a party, push the endless dungeon."),
        new(Role.Chef, "Chef", new[] { "Beau", "Remy", "Lou", "Gigi", "Andre", "Rocco", "Fifi", "Basil", "Sage", "Pepper" }, "300g/day", "Cooks your recipes from the crate every evening."),
        new(Role.Salesman, "Salesman", new[] { "Slick" }, "500g/day + 10-20%", "Sells what buyers want from the crate. Talk to him."),
        new(Role.Oil, "Oil roughneck", new[] { "Duke" }, $"{OilWagePerMachine}g/rig", "Runs your pumpjacks and refineries. Fewer spills."),
        new(Role.Harvester, "Harvester", new[] { "Rosie", "Daisy", "Poppy", "Lily", "Ivy", "Clover", "Fern", "Hazel", "Willow", "Maple" }, $"{HarvestFee}g/harvest", "Walks the farm picking crops, fruit, honey, sap."),
        new(Role.Forager, "Forager", new[] { "Hawk", "Bramble", "Thorn", "Moss", "Birch", "Rowan", "Aspen", "Briar", "Cedar", "Fox" }, "600g/day", "Forages, chops and replants the woods off the farm."),
        new(Role.Brewer, "Brewer", new[] { "Hooch", "Barley", "Rye", "Bourbon", "Malt", "Shandy", "Porter", "Stout", "Mash", "Brandy" }, "350g/day", "Keeps your kegs and jars full. Drinks on the job."),
        new(Role.Blacksmith, "Blacksmith", new[] { "Smitty", "Cinder", "Ingot", "Tongs", "Rivet", "Bellows", "Ember", "Slag", "Anvil", "Flux" }, "350g/day", "Runs your furnaces from the crate, cracks geodes (25g each)."),
        new(Role.Artisan, "Artisan", new[] { "Mabel", "Brie", "Curdie", "Wicker", "Bodkin", "Nell", "Pearl", "Opal", "Tess", "Wren" }, "350g/day", "Runs cheese presses, mayo, looms, smokers, oil makers..."),
        new(Role.Courier, "Courier", new[] { "Scoot" }, "250g/day", "Delivers gifts from the crate to the villagers."),
        new(Role.Banker, "Banker", new[] { "Sterling" }, "300g/day", "Savings with daily interest, loans, payroll top-ups."),
        new(Role.Scientist, "Scientist", new[] { "Doc" }, "500g/day", "Researches upgrades for you and the crew. Talk to him."),
        new(Role.Groundskeeper, "Groundskeeper", new[] { "Axel", "Timber", "Chip", "Stumpy", "Brush", "Rake", "Thatch", "Scrub", "Hatchet", "Burl" }, "300g/day", "Keeps the farm clear: weeds, branches, stones, grass, trees."),
        new(Role.Scavenger, "Scavenger", new[] { "Rusty" }, "10g/can", "Digs through every trash can in the valley at 6pm."),
        new(Role.Pirate, "Pirate Captain", new[] { "Barnacle" }, "600g/day + crew", "Runs the fleet: week-long voyages, one stage each, big hauls."),
        new(Role.TreasureHunter, "Treasure hunter", new[] { "Dixie" }, "400g/day", "Digs up artifact spots across the valley, plus test pits."),
        new(Role.Shopkeeper, "Shopkeeper", new[] { "Mae" }, "250g/day", "Buys seeds at Pierre's or Joja every morning, haggles, fills the crate."),
        new(Role.Scareboy, "Scareboy", new[] { "Shucks" }, "200g/day", "A walkin' scarecrow: no crows on the farm, spreads the crate's fertilizer, robs the crows' nests."),
    };

    // ---- the bank ----
    public const double SavingsRate = 0.002, LoanRate = 0.005;
    public const long LoanLimit = 100_000, LoanMinPayment = 500;
    /// <summary>A night's interest on savings, boosted by the banker's skill.</summary>
    public static long SavingsInterest(long savings, double bonus) => (long)Math.Floor(savings * SavingsRate * (1 + bonus));
    /// <summary>A night's interest on a loan, cut by the banker's connections.</summary>
    public static long LoanInterest(long balance, double cut) => (long)Math.Ceiling(balance * LoanRate * (1 - cut));
    /// <summary>The morning payment: 5% of what's owed, at least 500g (or the rest).</summary>
    public static long LoanPayment(long balance) => Math.Min(balance, Math.Max(LoanMinPayment, balance / 20));

    public static RoleDef RoleInfo(Role role) => Roles[(int)role];

    /// <summary>Flat daily wage per head for specialists; crews, the crab catcher and the roughneck are paid per unit.</summary>
    public static int DailyWage(Role role) => role switch { Role.Fisher => 300, Role.Miner => 400, Role.Adventurer => 500, Role.Chef => 300, Role.Salesman => 500, Role.Forager => 600, Role.Brewer => 350, Role.Blacksmith => 350, Role.Artisan => 350, Role.Courier => 250, Role.Banker => 300, Role.Scientist => 500, Role.Groundskeeper => 300, Role.Pirate => 600, Role.TreasureHunter => 400, Role.Shopkeeper => 250, Role.Scareboy => 200, _ => 0 }; // the scavenger is paid per can
    public static int CrabWage(int pots) => Math.Max(CrabWageMin, pots * CrabWagePerPot);
    public static int OilWage(int machines) => Math.Max(OilWageMin, machines * OilWagePerMachine);
    /// <summary>Field and ranch crews: the per-crop/animal wage covers the first hand, each extra hand costs a flat daily wage.</summary>
    public static int ExtraHandsWage(int hands) => ExtraHandWage * Math.Max(0, hands - 1);

    /// <summary>One crab catcher, salesman or roughneck covers everything; the rest scale up to 10.</summary>
    public static int MaxCount(Role role) => role is Role.Crabber or Role.Salesman or Role.Oil or Role.Courier or Role.Banker or Role.Scientist or Role.Adventurer or Role.Scavenger or Role.Pirate or Role.TreasureHunter or Role.Shopkeeper or Role.Scareboy ? 1 : Math.Min(MaxPerRole, RoleInfo(role).Names.Length);

    public static int Count(MarketState state, Role role) => state.Crew.GetValueOrDefault(role);

    /// <summary>Fit an older save to today's limits (e.g. a crew of adventurers becomes one Guild Master).</summary>
    public static void Normalize(MarketState state)
    {
        foreach (var role in state.Crew.Keys.ToList())
            SetCount(state, role, Count(state, role));
    }
    public static bool IsHired(MarketState state, Role role) => Count(state, role) > 0;

    public static void SetCount(MarketState state, Role role, int count)
    {
        count = Math.Clamp(count, 0, MaxCount(role));
        if (count == 0)
            state.Crew.Remove(role);
        else
            state.Crew[role] = count;
    }

    public static int HeadCount(MarketState state) => state.Crew.Values.Sum();

    /// <summary>Hands a cabin can sleep: its beds (a double bed sleeps 2), capped by the cabin's upgrade level (2 / 4 / 6 / 8).</summary>
    public static int CabinBeds(int bedSlots, int upgradeLevel) => Math.Min(bedSlots, 2 + 2 * Math.Clamp(upgradeLevel, 0, 3));

    public static bool HasRoom(MarketState state, Role role, int beds) => Count(state, role) < MaxCount(role) && HeadCount(state) < beds;

    /// <summary>Saves from before hand counts kept on/off flags: turn them into counts once. Crews had one hand per cabin, up to 3.</summary>
    public static void Migrate(MarketState state, int cabins)
    {
        if (state.FieldHands)
            SetCount(state, Role.Field, Math.Clamp(cabins, 1, 3));
        if (state.RanchHands)
            SetCount(state, Role.Ranch, Math.Clamp(cabins, 1, 3));
        foreach (var role in state.Staff)
            SetCount(state, role, Math.Max(1, Count(state, role)));
        state.FieldHands = state.RanchHands = false;
        state.Staff.Clear();
    }

    // ---- salesman ----
    public const double TownMarkup = 1.20, CityMarkup = 1.5, SalesCut = 0.10;

    /// <summary>What a load worth <paramref name="value"/> at the shipping bin brings in: the markup, minus his 10% of the takings.</summary>
    /// <summary>His cut climbs with the haul: the base rate on the first 2,000g, 1.5x it up to 10,000g, 2x above (10% / 15% / 20%).</summary>
    public const long CutTier1 = 2_000, CutTier2 = 10_000;

    public static (long Gross, long Cut, long Net) SalesPayout(long value, bool city, double extraMarkup = 0, double cutRate = SalesCut)
    {
        long gross = (long)Math.Round(value * ((city ? CityMarkup : TownMarkup) + extraMarkup));
        double cut = Math.Min(gross, CutTier1) * cutRate
            + Math.Clamp(gross - CutTier1, 0, CutTier2 - CutTier1) * cutRate * 1.5
            + Math.Max(0, gross - CutTier2) * cutRate * 2;
        long kept = (long)Math.Round(cut);
        return (gross, kept, gross - kept);
    }

    /// <summary>How many of the crate's items buyers take on a trip: 15% great day (all), 50% normal (40-90%), 25% slow (10-40%), 10% bad (5-10 items).</summary>
    public static int SalesQuota(int items, Random rng)
    {
        double roll = rng.NextDouble();
        int quota = roll < 0.15 ? items
            : roll < 0.65 ? (int)Math.Ceiling(items * (0.4 + rng.NextDouble() * 0.5))
            : roll < 0.90 ? (int)Math.Ceiling(items * (0.1 + rng.NextDouble() * 0.3))
            : rng.Next(5, 11);
        return Math.Clamp(quota, Math.Min(1, items), items);
    }

    // ---- hauls (qualified item IDs) ----
    private static readonly string[] Gems = { "(O)80", "(O)86", "(O)66", "(O)62", "(O)60", "(O)64", "(O)70", "(O)68" };

    /// <summary>A day in the mines: deeper floors you've reached unlock better ore and geodes.</summary>
    public static List<(string Id, int Count)> MinerHaul(int deepest, Random rng)
    {
        var haul = new List<(string, int)> { ("(O)390", rng.Next(20, 41)), ("(O)382", rng.Next(3, 9)), ("(O)378", rng.Next(10, 21)) };
        if (deepest >= 40)
            haul.Add(("(O)380", rng.Next(8, 16)));
        if (deepest >= 80)
            haul.Add(("(O)384", rng.Next(5, 11)));
        if (deepest > 120)
            haul.Add(("(O)386", rng.Next(1, 4))); // Skull Cavern
        haul.Add((deepest >= 80 ? "(O)537" : deepest >= 40 ? "(O)536" : "(O)535", rng.Next(2, 5)));
        if (rng.NextDouble() < 0.5)
            haul.Add((Gems[rng.Next(Gems.Length)], 1));
        return haul;
    }

    /// <summary>A day of monster hunting: loot by depth plus loose gold, or a 10% chance he comes back hurt with nothing.</summary>
    public static (List<(string Id, int Count)> Loot, int Gold, bool Hurt) AdventurerHaul(int deepest, Random rng)
    {
        if (rng.NextDouble() < 0.10)
            return (new(), 0, true);
        var loot = new List<(string, int)> { ("(O)766", rng.Next(5, 16)), ("(O)684", rng.Next(3, 9)) };
        if (deepest >= 30)
            loot.Add(("(O)767", rng.Next(2, 7)));
        if (deepest >= 40)
            loot.Add(("(O)768", rng.Next(1, 4)));
        if (deepest >= 80)
            loot.Add(("(O)769", rng.Next(1, 4)));
        return (loot, rng.Next(100, 301) + Math.Min(deepest, 200) * 4, false);
    }

    // ---- day luck: every hand rolls their own day ----

    private static readonly string[] Junk = { "(O)168", "(O)169", "(O)170", "(O)171", "(O)172" }; // trash, driftwood, broken glasses, broken CD, soggy newspaper

    private static List<(string Id, int Count)> Times(List<(string Id, int Count)> haul, double factor) =>
        haul.Select(h => (h.Id, Math.Max(1, (int)Math.Round(h.Count * factor)))).ToList();

    /// <summary>A miner's day: 70% mostly rocks, 20% a decent vein, 8% a rich vein, 2% jackpot (diamonds; a prismatic shard if you've reached Skull Cavern).</summary>
    public static (DayLuck Luck, List<(string Id, int Count)> Haul) MinerDay(int deepest, Random rng)
    {
        double roll = rng.NextDouble();
        if (roll < 0.70)
        {
            var rocks = new List<(string, int)> { ("(O)390", rng.Next(30, 61)), ("(O)382", rng.Next(1, 4)), ("(O)378", rng.Next(3, 9)) };
            if (rng.NextDouble() < 0.3)
                rocks.Add(("(O)535", 1));
            return (DayLuck.Bad, rocks);
        }
        var haul = MinerHaul(deepest, rng);
        if (roll < 0.90)
            return (DayLuck.Normal, haul);
        haul = Times(haul, 2);
        haul.Add((Gems[rng.Next(Gems.Length)], 1));
        haul.Add((Gems[rng.Next(Gems.Length)], 1));
        if (roll < 0.98)
            return (DayLuck.Good, haul);
        haul.Add(("(O)72", rng.Next(1, 3)));
        if (deepest > 120)
            haul.Add(("(O)74", 1));
        return (DayLuck.Jackpot, haul);
    }

    // ---- the shopkeeper ----

    /// <summary>A seed on sale: qualified id, the shop's price, and what one tile of it earns over the rest of the season.</summary>
    public sealed record SeedOffer(string Id, int Price, double Profit);

    public const double HaggleFail = 0.15, HaggleMin = 0.05, HaggleMax = 0.15, HaggleCap = 0.30;

    /// <summary>The discount she talks the shop into: 5-15% plus her skill (30% at most), or nothing when the haggle fails (15%).</summary>
    public static double Haggle(Random rng, double skill, bool lucky = false) =>
        rng.NextDouble() < HaggleFail && !lucky ? 0 : Math.Min(HaggleCap, HaggleMin + rng.NextDouble() * (HaggleMax - HaggleMin) + skill);

    /// <summary>
    /// What one tile of a crop earns over the rest of the season: a regrowing crop's harvests, or as many plant-and-pick cycles
    /// as fit, minus the seed each cycle. Zero or less when it can't ripen in time.
    /// </summary>
    public static double SeasonProfit(int harvestPrice, double perHarvest, int seedPrice, int growDays, int regrowDays, int daysLeft)
    {
        if (growDays <= 0 || growDays > daysLeft)
            return 0;
        if (regrowDays > 0)
            return harvestPrice * perHarvest * (1 + (daysLeft - growDays) / regrowDays) - seedPrice;
        return (harvestPrice * perHarvest - seedPrice) * (daysLeft / growDays);
    }

    /// <summary>
    /// Today's order: first top up your pinned seeds to their targets, then (auto) the most profitable seed for the empty soil
    /// the crate's seeds don't already cover. Stays inside the budget at the haggled prices.
    /// </summary>
    public static List<(string Id, int Count, int Each)> SeedOrder(IReadOnlyList<SeedOffer> offers, IReadOnlyDictionary<string, int> pinned,
        IReadOnlyDictionary<string, int> inCrate, int uncoveredSoil, bool auto, long budget, double discount)
    {
        var order = new List<(string, int, int)>();
        int Each(SeedOffer o) => Math.Max(1, (int)Math.Round(o.Price * (1 - discount)));
        void Buy(SeedOffer o, int want)
        {
            int count = (int)Math.Min(want, budget / Each(o));
            if (count <= 0)
                return;
            order.Add((o.Id, count, Each(o)));
            budget -= (long)count * Each(o);
        }
        foreach (var (id, target) in pinned)
        {
            if (offers.FirstOrDefault(o => o.Id == id) is { } offer)
                Buy(offer, target - inCrate.GetValueOrDefault(id));
        }
        if (auto && uncoveredSoil > 0 && offers.Where(o => o.Profit > 0).MaxBy(o => o.Profit) is { } best)
            Buy(best, uncoveredSoil);
        return order;
    }

    // ---- spreading land hands ----

    /// <summary>How much a place pulls hands: its work, at 40% for a separate farm map (a long walk: Ginger Island, expansions).</summary>
    public static double LandWeight(int work, bool far) => far ? work * 0.4 : work;

    /// <summary>
    /// Which place (index) each hand works: every hand in turn goes where there's the most work per hand already there.
    /// -1 when there's no work anywhere. Two hands and a busy farm both stay home; a big job elsewhere draws one away.
    /// </summary>
    public static int[] SpreadHands(IReadOnlyList<double> weights, int hands)
    {
        var picks = new int[hands];
        var crews = new int[weights.Count];
        for (int h = 0; h < hands; h++)
        {
            int best = -1;
            for (int p = 0; p < weights.Count; p++)
            {
                if (weights[p] > 0 && (best < 0 || weights[p] / (crews[p] + 1) > weights[best] / (crews[best] + 1)))
                    best = p;
            }
            picks[h] = best;
            if (best >= 0)
                crews[best]++;
        }
        return picks;
    }

    // ---- the fisher's own luck ----

    /// <summary>Fish rarity by base price: (name, from this price up, chance per catch). Common 60%, uncommon 28%, rare 10%, epic 2%.</summary>
    public static readonly (string Name, int MinPrice, double Chance)[] FishTiers =
    {
        ("Common", 0, 0.60),
        ("Uncommon", 80, 0.28),
        ("Rare", 150, 0.10),
        ("Epic", 400, 0.02),
    };

    public static int FishTier(int price) => Array.FindLastIndex(FishTiers, t => price >= t.MinPrice);

    /// <summary>
    /// Pick a catch from what's biting (each with its base price): roll a rarity, take a fish of that rarity, or the nearest
    /// rarity below that's biting (above, if nothing cheaper is). Null when nothing's biting.
    /// </summary>
    public static T? PickFish<T>(IReadOnlyList<(T Fish, int Price)> biting, Random rng) where T : class
    {
        if (biting.Count == 0)
            return null;
        double roll = rng.NextDouble(), edge = 0;
        int tier = FishTiers.Length - 1;
        for (int i = 0; i < FishTiers.Length; i++)
        {
            edge += FishTiers[i].Chance;
            if (roll < edge)
            {
                tier = i;
                break;
            }
        }
        var order = Enumerable.Range(0, tier + 1).Reverse().Concat(Enumerable.Range(tier + 1, FishTiers.Length - tier - 1));
        foreach (int t in order)
        {
            var pool = biting.Where(b => FishTier(b.Price) == t).ToList();
            if (pool.Count > 0)
                return pool[rng.Next(pool.Count)].Fish;
        }
        return null;
    }

    /// <summary>A treasure hunter's test pit: clay 35%, geode 25%, frozen geode 12%, magma geode 8%, omni geode 7%, artifact trove 2%, else nothing.</summary>
    public static (string Id, int Count)? TestPit(Random rng)
    {
        double r = rng.NextDouble();
        return r < 0.35 ? ("(O)330", rng.Next(1, 4)) : r < 0.60 ? ("(O)535", 1) : r < 0.72 ? ("(O)536", 1) : r < 0.80 ? ("(O)537", 1)
            : r < 0.87 ? ("(O)749", 1) : r < 0.89 ? ("(O)275", 1) : null;
    }

    /// <summary>What the crows stole, robbed back from their nests by the scareboy: (item, count, chance). 40% of the time just twigs.</summary>
    public static readonly (string Id, int Count, double Chance)[] CrowLoot =
    {
        ("(O)80", 1, 0.20),  // quartz
        ("(O)378", 3, 0.15), // copper ore
        ("(O)380", 2, 0.10), // iron ore
        ("(O)384", 1, 0.07), // gold ore
        ("(O)86", 1, 0.05),  // earth crystal
        ("(O)66", 1, 0.02),  // amethyst
        ("(O)72", 1, 0.01),  // diamond
    };

    public static (string Id, int Count)? CrowStash(Random rng)
    {
        double roll = rng.NextDouble();
        foreach (var loot in CrowLoot)
        {
            if (roll < loot.Chance)
                return (loot.Id, loot.Count);
            roll -= loot.Chance;
        }
        return null;
    }

    /// <summary>A fisherman's day: 15% skunked (a couple of fish and trash), 60% normal, 20% good, 5% jackpot (lots of fish and a treasure chest).</summary>
    public static (DayLuck Luck, int Fish, List<(string Id, int Count)> Extras) FisherDay(Random rng)
    {
        double roll = rng.NextDouble();
        if (roll < 0.15)
            return (DayLuck.Bad, rng.Next(0, 3), Enumerable.Range(0, rng.Next(2, 5)).Select(_ => (Junk[rng.Next(Junk.Length)], 1)).ToList());
        if (roll < 0.75)
            return (DayLuck.Normal, rng.Next(6, 11), new());
        if (roll < 0.95)
            return (DayLuck.Good, rng.Next(10, 16), new());
        return (DayLuck.Jackpot, rng.Next(15, 21), new() { ("(O)166", 1) });
    }

    public const int ExpeditionFee = 500;
    /// <summary>More water, more fish: an expedition day brings home half again as many.</summary>
    public const double ExpeditionCatch = 1.5;
    private static readonly string[] SpecialWaters = { "Volcano", "Caldera", "Sewer", "BugLand", "WitchSwamp", "Submarine", "UndergroundMine", "SkullCave", "Mine" };

    /// <summary>Waters an expedition skips: lava, the sewer, the bug lair, the witch's swamp, the night market sub, the mines.</summary>
    public static bool IsSpecialWater(string location) =>
        location == "Default" || SpecialWaters.Any(s => location.Contains(s, StringComparison.OrdinalIgnoreCase));

    /// <summary>Star quality of one fish (0 normal, 1 silver, 2 gold, 4 iridium): the better the fisherman's day, the better the odds.</summary>
    public static int FishQuality(DayLuck luck, Random rng)
    {
        // per day: chance of silver or better, gold or better, iridium
        var (silver, gold, iridium) = luck switch
        {
            DayLuck.Bad => (0.10, 0.0, 0.0),
            DayLuck.Normal => (0.35, 0.10, 0.01),
            DayLuck.Good => (0.65, 0.30, 0.05),
            _ => (0.85, 0.55, 0.15),
        };
        double roll = rng.NextDouble();
        return roll < iridium ? 4 : roll < gold ? 2 : roll < silver ? 1 : 0;
    }

    /// <summary>An adventurer's day: 10% hurt, 15% rough (half loot), 50% normal, 20% big hunt (double), 5% a monster's stash (diamond and a pile of gold).</summary>
    public static (DayLuck Luck, List<(string Id, int Count)> Loot, int Gold, bool Hurt) AdventurerDay(int deepest, Random rng)
    {
        double roll = rng.NextDouble();
        if (roll < 0.10)
            return (DayLuck.Bad, new(), 0, true);
        var (loot, gold, _) = AdventurerHaulUnhurt(deepest, rng);
        if (roll < 0.25)
            return (DayLuck.Bad, Times(loot, 0.5), gold / 2, false);
        if (roll < 0.75)
            return (DayLuck.Normal, loot, gold, false);
        if (roll < 0.95)
            return (DayLuck.Good, Times(loot, 2), gold * 2, false);
        loot.Add(("(O)72", 1));
        return (DayLuck.Jackpot, loot, gold + rng.Next(1000, 3001), false);
    }

    private static (List<(string Id, int Count)> Loot, int Gold, bool Hurt) AdventurerHaulUnhurt(int deepest, Random rng)
    {
        while (true)
        {
            var trip = AdventurerHaul(deepest, rng);
            if (!trip.Hurt)
                return trip;
        }
    }

    /// <summary>Short prompt block so an AI-dialogue NPC knows about the farmer's portfolio and businesses. Kept small: it's sent with every line.</summary>
    public static string PortfolioBrief(MarketState state, string farmer, string npc, IEnumerable<string>? business = null)
    {
        var lines = new List<string> { "### Valley Exchange (stock market)" };
        var held = state.Holdings
            .Where(kv => kv.Value.Shares > 0)
            .OrderByDescending(kv => (long)kv.Value.Shares * SharePrice(state, kv.Key))
            .ToList();
        if (held.Count == 0)
            lines.Add($"{farmer} owns no stocks yet.");
        else
        {
            long profit = Profit(state);
            lines.Add($"{farmer}'s stock portfolio is worth {PortfolioValue(state):N0}g, {(profit >= 0 ? "up" : "down")} {Math.Abs(profit):N0}g overall. Biggest holdings:");
            foreach (var kv in held.Take(4))
                lines.Add($"- {kv.Value.Shares:N0} shares of {Def(kv.Key).Name}, {Math.Round(DayChange(state, kv.Key), 3):+0.0%;-0.0%;0.0%} today"); // round first: .NET prints "-+0.0%" for tiny negatives
        }
        if (state.News.Count > 0)
            lines.Add($"Today's market headline: {state.News[0]}");
        if (business != null)
            lines.AddRange(business);
        lines.Add($"{npc} follows the market and may comment on {farmer}'s investments, businesses or the headline when it fits naturally, in their own voice. Don't force it.");
        return string.Join("\n", lines);
    }

    private static double Step(double price, CompanyDef c, double bias, Random rng)
    {
        double reversion = -0.03 * Math.Log(price / c.BasePrice); // keeps prices orbiting their base over weeks
        double r = Math.Clamp(bias + reversion + c.Volatility * Normal(rng), -0.4, 0.4);
        return Math.Max(1, price * Math.Exp(r));
    }

    private static double SectorBias(Sector sector, DayContext ctx)
    {
        double b = ctx.Season switch
        {
            0 => sector == Sector.Auto ? 0.003 : 0,
            1 => sector switch { Sector.Food => 0.003, Sector.Energy => 0.001, Sector.Media => -0.001, _ => 0 },
            2 => sector switch { Sector.Retail => 0.002, Sector.Finance => 0.002, _ => 0 },
            _ => sector switch { Sector.Retail => 0.004, Sector.Energy => 0.004, Sector.Tech => 0.002, Sector.Auto => -0.002, _ => 0 },
        };
        if (ctx.Rainy)
            b += sector == Sector.Media ? 0.004 : sector == Sector.Food ? -0.002 : 0;
        if (ctx.Festival && sector == Sector.Food)
            b += 0.01;
        return b;
    }

    private static double Normal(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }
}
