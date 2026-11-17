using StardewValley;
using StardewValley.Internal;
using SObject = StardewValley.Object;

namespace ValleyExchange;

/// <summary>
/// The shopkeeper: every day at noon she's back from town with seeds bought at Pierre's or JojaMart (whichever's cheaper,
/// Joja on Pierre's day off), haggled down, into the crate for the field hands. Your pinned seeds first, then (auto) the
/// most profitable seed for the empty soil the crate can't already cover. Never past her budget or the banker's wallet floor.
/// </summary>
internal static class Shopkeeper
{
    public const int ShopTime = 1200;
    private static readonly int[] Budgets = { 1_000, 2_500, 5_000, 10_000, 25_000, 50_000 };
    private static int shoppedDay = -1;

    /// <summary>Seeds for sale today in the shops she can reach: the cheaper price per seed, with their season profit on the farm.</summary>
    public static List<Market.SeedOffer> Offers()
    {
        var farm = Game1.getFarm();
        var prices = new Dictionary<string, int>();
        foreach (string shop in Shops())
        {
            Dictionary<ISalable, ItemStockInformation> stock;
            try
            {
                stock = ShopBuilder.GetShopStock(shop);
            }
            catch
            {
                continue; // a shop some mod broke: skip it
            }
            foreach (var (item, info) in stock)
            {
                if (item is SObject { Category: SObject.SeedsCategory } seed && info.TradeItem == null && info.Price > 0
                    && (!prices.TryGetValue(seed.QualifiedItemId, out int old) || info.Price < old))
                    prices[seed.QualifiedItemId] = info.Price;
            }
        }
        int daysLeft = 28 - Game1.dayOfMonth;
        var next = (StardewValley.Season)(((int)Game1.season + 1) % 4);
        return prices.Select(p => Game1.cropData.TryGetValue(FieldHands.Bare(p.Key), out var crop) && crop.Seasons.Contains(Game1.season)
                ? new Market.SeedOffer(p.Key, p.Value, Profit(crop, p.Value, crop.Seasons.Contains(next) ? daysLeft + 28 : daysLeft))
                : null)
            .OfType<Market.SeedOffer>()
            .ToList();
    }

    /// <summary>Pierre's (not on Wednesdays) and JojaMart (while it's open).</summary>
    private static IEnumerable<string> Shops()
    {
        if (Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth) != "Wed" || Game1.MasterPlayer.hasOrWillReceiveMail("ccIsComplete"))
            yield return "SeedShop";
        if (Game1.isLocationAccessible("JojaMart"))
            yield return "Joja";
    }

    private static double Profit(StardewValley.GameData.Crops.CropData crop, int seedPrice, int daysLeft)
    {
        int price = ItemRegistry.GetData(crop.HarvestItemId)?.RawData is StardewValley.GameData.Objects.ObjectData data ? data.Price : 0;
        double perHarvest = (crop.HarvestMinStack + Math.Max(crop.HarvestMinStack, crop.HarvestMaxStack)) / 2.0;
        return Market.SeasonProfit(price, perHarvest, seedPrice, crop.DaysInPhase.Sum(), crop.RegrowDays, daysLeft);
    }

    /// <summary>Empty tilled soil on the farm and in the greenhouse.</summary>
    private static int EmptySoil() =>
        new[] { Game1.getFarm(), Game1.getLocationFromName("Greenhouse") }.OfType<GameLocation>()
            .Sum(l => l.terrainFeatures.Values.Count(f => f is StardewValley.TerrainFeatures.HoeDirt { crop: null }));

    /// <summary>Noon: back from town with the day's seeds. Once a day.</summary>
    public static void OnTimeChanged(MarketState state, int time)
    {
        int today = Game1.Date.TotalDays;
        if (time < ShopTime || shoppedDay == today || !Market.IsHired(state, Role.Shopkeeper))
            return;
        shoppedDay = today;
        Staff.Reports[Role.Shopkeeper] = Shop(state);
    }

    private static string Shop(MarketState state)
    {
        var offers = Offers();
        if (offers.Count == 0)
            return "Shops had nothin' in season for us today, boss.";
        var farm = Game1.getFarm();
        var inCrate = Crate.Items().Where(i => i.Category == SObject.SeedsCategory).GroupBy(i => i.QualifiedItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Stack));
        int covered = FieldHands.SeedsFor(farm).Sum(id => inCrate.GetValueOrDefault(id)); // seeds already waiting for the field hands
        bool auto = state.ShopAuto && state.AutoPlant;
        long budget = Math.Min(state.ShopBudget, Math.Max(0, Game1.player.Money - state.WalletFloor));
        double discount = Market.Haggle(Game1.random, CrewSkills.Total(Role.Shopkeeper, PerkKind.Yield), CrewSkills.Lucky(Role.Shopkeeper));
        var order = Market.SeedOrder(offers, state.ShopList, inCrate, Math.Max(0, EmptySoil() - covered), auto, budget, discount);
        if (order.Count == 0)
            return budget <= 0 ? "Couldn't spend a coin, boss: wallet's at the floor." : "Crate's got all the seeds we need. Saved ya the trip money.";
        long spent = 0, saved = 0;
        foreach (var (id, count, each) in order)
        {
            var seeds = ItemRegistry.Create(id, count);
            long full = (long)offers.First(o => o.Id == id).Price * count, cost = (long)each * count;
            Game1.player.Money -= (int)cost;
            spent += cost;
            saved += full - cost;
            CrewReport.Got(Role.Shopkeeper, "seeds bought", seeds);
            CrewReport.Detail(Role.Shopkeeper, $"{seeds.DisplayName} x{count} at {each:N0}g = {cost:N0}g");
            Crate.Store(seeds);
        }
        CrewReport.Paid(Role.Shopkeeper, spent);
        CrewSkills.Work(Role.Shopkeeper, order.Sum(o => o.Count) / 10 + 5);
        string what = string.Join(", ", order.Select(o => $"{o.Count} {ItemRegistry.GetDataOrErrorItem(o.Id).DisplayName}"));
        return discount > 0
            ? $"Bought {what} for {spent:N0}g. Haggled {Skills.Pct(discount)} off, saved ya {saved:N0}g!"
            : $"Bought {what} for {spent:N0}g. Couldn't talk 'em down today, dang it.";
    }

    /// <summary>Talking to her: the report, auto on/off, the budget, and the shopping list.</summary>
    public static void Talk(MarketState state, string name, Action<string> say)
    {
        string report = Staff.Reports.TryGetValue(Role.Shopkeeper, out var r) ? r : "Headin' to market at nine, back by noon with seeds.";
        var options = new[]
        {
            new Response("list", "Open the shopping list"),
            new Response("auto", state.ShopAuto ? "Stop buying for empty soil on your own" : "Buy for empty soil on your own"),
            new Response("budget", $"Daily budget: {state.ShopBudget:N0}g (change)"),
        };
        Personas.Ask(name, Role.Shopkeeper, $"{name}: \"{report}\"", options, answer =>
        {
            switch (answer)
            {
                case "list":
                    WhenFree.Run(() => Game1.activeClickableMenu = new ShopListMenu(state));
                    break;
                case "auto":
                    state.ShopAuto = !state.ShopAuto;
                    say(state.ShopAuto ? "Ah'll keep every bit of soil seeded, boss." : "Just your list, then.");
                    Game1.playSound(state.ShopAuto ? "purchaseClick" : "trashcan");
                    break;
                case "budget":
                    state.ShopBudget = Budgets[(Array.IndexOf(Budgets, state.ShopBudget) + 1) % Budgets.Length];
                    say($"Up to {state.ShopBudget:N0}g a day. Got it.");
                    Game1.playSound("purchaseClick");
                    break;
            }
        });
    }
}
