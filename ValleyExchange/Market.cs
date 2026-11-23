using System;
using System.Collections.Generic;
using System.Linq;

namespace ValleyExchange;

public enum Sector { Tech, Retail, Food, Auto, Media, Finance, Energy }

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

    /// <summary>
    /// Everything else in this save record: the crew, guild, fleet, research and oil field from before the split. Brawnies
    /// copies it from here on first load; kept untouched so loading once without Brawnies can't lose any of it.
    /// </summary>
    [Newtonsoft.Json.JsonExtensionData]
    public IDictionary<string, Newtonsoft.Json.Linq.JToken>? Kept { get; set; }
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

    // ---- the crude ticker on the trading screen: follows the energy stocks (the oil field itself is in Brawnies) ----
    public const int CrudeBasePrice = 150;
    public const double GasolineMultiplier = 1.8;
    private static readonly string[] OilTickers = { "SHFS", "EXOF" };

    /// <summary>1.0 when the oil stocks sit at their base price.</summary>
    public static double OilIndex(MarketState state, int daysAgo = 0) =>
        OilTickers.Average(t => { var h = state.Stocks[t].History; return h[Math.Max(0, h.Count - 1 - daysAgo)] / Def(t).BasePrice; });

    public static int CrudePrice(MarketState state) => Math.Max(1, (int)Math.Round(CrudeBasePrice * OilIndex(state)));
    public static int GasolinePrice(MarketState state) => (int)Math.Round(CrudePrice(state) * GasolineMultiplier);
    public static double CrudeChange(MarketState state) => OilIndex(state) / OilIndex(state, 1) - 1;


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
