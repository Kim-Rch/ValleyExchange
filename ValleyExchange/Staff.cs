using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace ValleyExchange;

/// <summary>What the specialists actually produce. Morning: wages, crab pots, salesman back from the city. Evening: everyone else's haul.</summary>
internal static class Staff
{
    /// <summary>Last thing each role did, for when you talk to them.</summary>
    public static readonly Dictionary<Role, string> Reports = new();

    public static IEnumerable<CrabPot> CrabPots()
    {
        var pots = new List<CrabPot>();
        Utility.ForEachLocation(location =>
        {
            pots.AddRange(location.objects.Values.OfType<CrabPot>());
            return true;
        });
        return pots;
    }

    public static int WageFor(Role role) => CrewSkills.Wage(role, role switch
    {
        Role.Crabber => Market.CrabWage(CrabPots().Count()),
        Role.Oil => Market.OilWage(Oil.Machines().Count),
        Role.Adventurer => Market.DailyWage(role) + (int)Guild.Wages(Workers.State?.Guild), // the Guild Master pays his party
        Role.Pirate => Market.DailyWage(role) + (int)Guild.Wages(Workers.State?.Fleet), // and the captain his crew, at sea or not
        _ => Market.DailyWage(role),
    });

    /// <summary>Sales markup bonus and cut rate from the salesman's skills.</summary>
    public static (double Markup, double Cut) SalesTerms() =>
        (CrewSkills.Total(Role.Salesman, PerkKind.Yield), CrewSkills.Has(Role.Salesman, PerkKind.HalfCut) ? Market.SalesCut / 2 : Market.SalesCut);

    public static void Morning(MarketState state, int today)
    {
        int paid = 0;
        foreach (var role in state.Crew.Keys.Where(r => r is not (Role.Field or Role.Ranch or Role.Harvester)).ToList())
        {
            int wage = WageFor(role) * Market.Count(state, role);
            if (Game1.player.Money < wage)
            {
                Market.SetCount(state, role, 0);
                Game1.addHUDMessage(new HUDMessage($"Your {Market.RoleInfo(role).Title.ToLower()} quit: you couldn't pay {wage:N0}g.", HUDMessage.error_type));
                continue;
            }
            Game1.player.Money -= wage;
            paid += wage;
            CrewReport.Paid(role, wage);
        }

        if (Market.IsHired(state, Role.Crabber))
            Reports[Role.Crabber] = TendCrabPots();
        if (Market.IsHired(state, Role.Oil))
            Reports[Role.Oil] = Oil.Tend();
        if (state.TripReturnDay >= 0 && state.TripToCity && today >= state.TripReturnDay)
            PayTrip(state);
    }

    /// <summary>Mood lines from today's off-farm hands (one per hand), said as they walk back onto the farm.</summary>
    private static readonly Dictionary<Role, List<string>> Moods = new();
    private static int eveningDone = -1;

    /// <summary>A new day (or a reloaded one): the evening hauls haven't come in yet.</summary>
    public static void NewDay()
    {
        eveningDone = -1;
        Moods.Clear();
    }

    /// <summary>A random mood line from today's haul for this role, if they've come home.</summary>
    public static string? HomecomingLine(Role role) =>
        Moods.TryGetValue(role, out var lines) && lines.Count > 0 ? lines[Game1.random.Next(lines.Count)] : null;

    /// <summary>The hauls land when the off-farm hands come home at 6pm (or at bedtime if you sleep first). Runs once a day.</summary>
    public static void Evening(MarketState state, int today)
    {
        if (eveningDone == today)
            return;
        eveningDone = today;
        var rng = new Random(ModEntry.Seed ^ (today * 4099));
        int deepest = Game1.player.deepestMineLevel;

        if (Market.IsHired(state, Role.Fisher))
        {
            bool expedition = state.ExpeditionDay == today;
            int caught = 0, extras = 0;
            var lucks = new List<DayLuck>();
            for (int hand = 0; hand < Market.Count(state, Role.Fisher); hand++)
            {
                var day = Market.FisherDay(rng);
                if (day.Luck == DayLuck.Bad && CrewSkills.Lucky(Role.Fisher))
                    day = Market.FisherDay(rng); // shook off a skunked day
                lucks.Add(day.Luck);
                int bites = (int)Math.Round(day.Fish * (expedition ? Market.ExpeditionCatch : 1) * CrewSkills.More(Role.Fisher));
                for (int i = 0; i < bites; i++)
                {
                    if (CatchFish(rng, expedition) is { } fish)
                    {
                        fish.Quality = Market.FishQuality(day.Luck, rng);
                        CrewSkills.Polish(fish, Role.Fisher);
                        CrewReport.Got(Role.Fisher, "fish", fish);
                        Crate.Store(fish);
                        caught++;
                    }
                }
                extras += StoreAll(day.Extras, Role.Fisher, "other catches");
            }
            Moods[Role.Fisher] = lucks.Select(l => Mood(Role.Fisher, l)).ToList();
            string extra = lucks.Contains(DayLuck.Jackpot) ? " And a treasure chest!" : lucks.Contains(DayLuck.Bad) ? " Plus some junk, sorry." : "";
            string where = expedition ? $" Fished {AllWaters().Count} waters all over the valley." : "";
            Reports[Role.Fisher] = $"{Mood(Role.Fisher, lucks.Max())} {caught} fish in the crate.{extra}{where}";
        }
        if (Market.IsHired(state, Role.Miner))
        {
            int count = 0;
            var lucks = new List<DayLuck>();
            for (int hand = 0; hand < Market.Count(state, Role.Miner); hand++)
            {
                var day = Market.MinerDay(deepest, rng);
                if (day.Luck == DayLuck.Bad && CrewSkills.Lucky(Role.Miner))
                    day = Market.MinerDay(deepest, rng); // read the rock better
                lucks.Add(day.Luck);
                count += StoreAll(CrewSkills.Scale(day.Haul, Role.Miner), Role.Miner, "mined");
                CrewSkills.Work(Role.Miner, 10);
            }
            Moods[Role.Miner] = lucks.Select(l => Mood(Role.Miner, l)).ToList();
            int rockDays = lucks.Count(l => l == DayLuck.Bad);
            string rocks = lucks.Count > 1 && rockDays > 0 ? $" {rockDays} of us dug nothin' but rocks." : "";
            Reports[Role.Miner] = $"{Mood(Role.Miner, lucks.Max())} Hauled {count} things into the crate.{rocks}";
            if (lucks.Contains(DayLuck.Jackpot))
                Game1.addHUDMessage(new HUDMessage("Your miner struck it rich: diamonds in the crate!", HUDMessage.achievement_type));
        }
        if (Market.IsHired(state, Role.Adventurer))
            Moods[Role.Adventurer] = GuildPlay.Evening(state, today, rng);
        if (Market.IsHired(state, Role.Pirate) && GuildPlay.FleetEvening(state, today, rng) is { Count: > 0 } sea)
            Moods[Role.Pirate] = sea;
        if (Market.IsHired(state, Role.Scavenger))
        {
            Reports[Role.Scavenger] = Scavenger.Day();
            Moods[Role.Scavenger] = new() { Reports[Role.Scavenger].StartsWith("Opened") ? "Smells like profit, boss!" : "Slim pickin's in the bins." };
        }
        if (Market.IsHired(state, Role.Scareboy))
        {
            Reports[Role.Scareboy] = Scareboy.Day(rng, CrewSkills.More(Role.Scareboy));
            Moods[Role.Scareboy] = new() { "Not one crow, boss. Not one." };
        }
        if (Market.IsHired(state, Role.TreasureHunter))
        {
            Reports[Role.TreasureHunter] = TreasureHunter.Day(rng, CrewSkills.More(Role.TreasureHunter));
            Moods[Role.TreasureHunter] = new() { Reports[Role.TreasureHunter].StartsWith("Dug up") ? "X marked the spot, boss!" : "Dry day. Dirt's dirt." };
        }
        if (Market.IsHired(state, Role.Forager))
        {
            int foraged = 0, chopped = 0, planted = 0;
            for (int hand = 0; hand < Market.Count(state, Role.Forager); hand++)
            {
                var day = Forager.Day(rng, CrewSkills.More(Role.Forager));
                (foraged, chopped, planted) = (foraged + day.Foraged, chopped + day.Chopped, planted + day.Planted);
            }
            CrewSkills.Work(Role.Forager, foraged + chopped * 3 + planted);
            Moods[Role.Forager] = new() { foraged > 10 ? "Basket's full, boss!" : foraged > 0 ? "Slim pickin's out there." : "Woods were bare today." };
            Reports[Role.Forager] = $"Foraged {foraged} things and chopped {chopped} trees into the crate. Planted {planted} saplings so the woods keep growin'.";
        }
        if (Market.IsHired(state, Role.Blacksmith))
            Reports[Role.Blacksmith] = MachineTender.CrackGeodes(Market.Count(state, Role.Blacksmith));
        if (Market.IsHired(state, Role.Courier) && state.CourierGifts)
        {
            Reports[Role.Courier] = Courier.Deliver();
            Moods[Role.Courier] = new() { Reports[Role.Courier].StartsWith("Delivered") ? "Whole town says thanks, boss!" : "Nobody wanted nothin'. Rude." };
        }
        if (Market.IsHired(state, Role.Chef))
            Reports[Role.Chef] = Cook(max: (int)Math.Round(5 * Market.Count(state, Role.Chef) * CrewSkills.More(Role.Chef)));
        if (state.TripReturnDay >= 0 && !state.TripToCity && today >= state.TripReturnDay)
            PayTrip(state);
    }

    /// <summary>How they feel about their day.</summary>
    private static string Mood(Role role, DayLuck luck)
    {
        string[] lines = (role, luck) switch
        {
            (Role.Miner, DayLuck.Bad) => new[] { "Nothin' but rocks today. ROCKS!", "Swung all day for gravel. Hmph.", "I ain't paid enough for rocks." },
            (Role.Miner, DayLuck.Normal) => new[] { "Decent vein today.", "Honest day's dig." },
            (Role.Miner, DayLuck.Good) => new[] { "Struck a rich vein, boss!", "Ore was fallin' outta the walls!" },
            (Role.Miner, _) => new[] { "DIAMONDS, boss! I ain't lyin'!" },
            (Role.Fisher, DayLuck.Bad) => new[] { "Got skunked. Caught a boot.", "Nothin' bit but the mosquitoes." },
            (Role.Fisher, DayLuck.Normal) => new[] { "Fair day on the water.", "They were bitin' alright." },
            (Role.Fisher, DayLuck.Good) => new[] { "They were jumpin' in the boat!", "Best day this week!" },
            (Role.Fisher, _) => new[] { "Hooked a treasure chest!" },
            (_, DayLuck.Bad) => new[] { "Slimes everywhere, loot nowhere.", "Rough day down there." },
            (_, DayLuck.Normal) => new[] { "Another day, another slime.", "Got the job done." },
            (_, DayLuck.Good) => new[] { "Cleared a whole floor!", "They didn't stand a chance." },
            _ => new[] { "Found a monster's stash, boss!" },
        };
        return lines[Game1.random.Next(lines.Length)];
    }

    /// <summary>Talk to a fisherman: his report, and the offer of a fishing expedition (every water in the valley, for a price).</summary>
    public static void TalkFisher(MarketState state, string name)
    {
        int today = Game1.Date.TotalDays;
        string report = Reports.TryGetValue(Role.Fisher, out var r) ? r : "Fish are out there, boss. Just gotta go get 'em.";
        if (state.ExpeditionDay == today)
        {
            Personas.Ask(name, Role.Fisher, $"{name}: \"We're fishin' every water in the valley today, boss. Wait till you see the haul!\"");
            return;
        }
        if (Game1.timeOfDay >= 1800)
        {
            Personas.Ask(name, Role.Fisher, $"{name}: \"{report}\"");
            return;
        }
        int fee = Market.ExpeditionFee * Market.Count(state, Role.Fisher) / (CrewSkills.Has(Role.Fisher, PerkKind.CheapCharter) ? 2 : 1);
        var answers = new[]
        {
            new Response("go", $"Fishing expedition today: all {AllWaters().Count} waters in the valley, {fee:N0}g"),
        };
        Personas.Ask(name, Role.Fisher, $"{name}: \"{report} Want the crew to hit every water out there today? Costs extra, but we bring back more, and you never know what bites.\"", answers, answer =>
        {
            if (answer != "go")
                return;
            if (Game1.player.Money < fee)
            {
                Game1.playSound("cancel");
                Game1.addHUDMessage(new HUDMessage($"An expedition costs {fee:N0}g. You're short.", HUDMessage.error_type));
                return;
            }
            Game1.player.Money -= fee;
            CrewReport.Paid(Role.Fisher, fee);
            state.ExpeditionDay = today;
            Game1.playSound("purchaseClick");
            Game1.addHUDMessage(new HUDMessage($"Fishing expedition on! (-{fee:N0}g) The catch comes home at 6pm.", HUDMessage.achievement_type));
        });
    }

    private static int StoreAll(IEnumerable<(string Id, int Count)> haul, Role role, string what)
    {
        int total = 0;
        foreach (var (id, count) in haul)
        {
            var item = ItemRegistry.Create(id, count);
            CrewReport.Got(role, what, item);
            Crate.Store(item);
            total += count;
        }
        return total;
    }

    private static readonly string[] HomeWaters = { "Beach", "Town", "Mountain", "Forest" };
    private static List<string>? allWaters;

    /// <summary>Every water an expedition fishes: each map with fish data (modded ones too), minus the special places.</summary>
    private static List<string> AllWaters() =>
        allWaters ??= Game1.content.Load<Dictionary<string, StardewValley.GameData.Locations.LocationData>>("Data/Locations")
            .Where(kv => kv.Value.Fish?.Count > 0 && !Market.IsSpecialWater(kv.Key) && Game1.getLocationFromName(kv.Key) != null)
            .Select(kv => kv.Key)
            .Union(HomeWaters)
            .ToList();

    /// <summary>A real catch (season and weather apply), skipping trash: the usual four spots, or anywhere on an expedition day.</summary>
    private static Item? CatchFish(Random rng, bool expedition)
    {
        // see what's biting (the game's own odds: season, weather, water), then the fisher's own luck picks how good a fish he lands
        var waters = expedition ? AllWaters() : HomeWaters.ToList();
        var biting = new List<(Item Fish, int Price)>();
        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                var fish = Game1.getLocationFromName(waters[rng.Next(waters.Count)])?.getFish(0, null, 3, Game1.player, 0, Vector2.Zero);
                if (fish is SObject { Category: SObject.FishCategory } caught)
                    biting.Add((caught, caught.Price));
            }
            catch
            {
                // some modded waters can't be fished off-screen; try another
            }
        }
        return Market.PickFish(biting, rng);
    }

    private static string TendCrabPots()
    {
        int emptied = 0, baited = 0;
        foreach (var pot in CrabPots())
        {
            if (pot.heldObject.Value is { } catchItem)
            {
                CrewSkills.Polish(catchItem, Role.Crabber);
                CrewReport.Got(Role.Crabber, "catches", catchItem);
                Crate.Store(catchItem);
                if (CrewSkills.Bonus(Role.Crabber))
                    Crate.Store(catchItem.getOne()); // a second crab in the trap
                CrewSkills.Work(Role.Crabber, 1);
                pot.heldObject.Value = null;
                pot.readyForHarvest.Value = false;
                pot.tileIndexToShow = 710;
                emptied++;
            }
            if (pot.bait.Value == null)
            {
                pot.bait.Value = ItemRegistry.Create<SObject>("(O)685"); // her own bait
                baited++;
                CrewReport.Count(Role.Crabber, "pots baited");
            }
        }
        return $"Emptied {emptied} crab pots this mornin' and baited {baited}.";
    }

    // ---- chef ----

    /// <summary>Cook up to <paramref name="max"/> dishes from the crate, most valuable recipes first.</summary>
    private static string Cook(int max)
    {
        var crates = Crate.All();
        if (crates.Count == 0)
            return "No crate to cook out of, boss.";
        var recipes = new List<(CraftingRecipe Recipe, int Value)>();
        foreach (string name in Game1.player.cookingRecipes.Keys)
        {
            try
            {
                var recipe = new CraftingRecipe(name, true);
                if (recipe.recipeList.Count > 0 && recipe.createItem() is { } sample)
                    recipes.Add((recipe, sample.sellToStorePrice()));
            }
            catch
            {
                // a recipe from a removed mod: skip it
            }
        }
        recipes.Sort((a, b) => b.Value.CompareTo(a.Value)); // most valuable first
        var made = new List<string>();
        bool progress = true;
        while (made.Count < max && progress)
        {
            progress = false;
            foreach (var (recipe, _) in recipes)
            {
                if (made.Count >= max || !TryConsume(crates, recipe.recipeList))
                    continue;
                var dish = recipe.createItem();
                CrewSkills.Polish(dish, Role.Chef);
                CrewReport.Got(Role.Chef, "dishes", dish);
                Crate.Store(dish);
                CrewSkills.Work(Role.Chef, 3);
                made.Add(dish.DisplayName);
                progress = true;
            }
        }
        if (made.Count == 0)
            return "Couldn't cook a thing: the crate's missing ingredients for every recipe you know.";
        return $"Cooked {string.Join(", ", made.GroupBy(n => n).Select(g => g.Count() > 1 ? $"{g.Count()} {g.Key}" : g.Key))}. In the crate.";
    }

    /// <summary>Take a recipe's ingredients out of the crates, all or nothing. Keys are item IDs or negative category numbers.</summary>
    private static bool TryConsume(List<Chest> crates, Dictionary<string, int> needs)
    {
        bool Matches(Item item, string key) => key.StartsWith('-') ? item.Category.ToString() == key : item.ItemId == key || item.QualifiedItemId == key;
        if (needs.Any(n => crates.SelectMany(c => c.Items).Where(i => i != null && Matches(i, n.Key)).Sum(i => i.Stack) < n.Value))
            return false;
        foreach (var (key, count) in needs)
        {
            int left = count;
            foreach (var items in crates.Select(c => c.Items))
            {
                for (int i = 0; i < items.Count && left > 0; i++)
                {
                    if (items[i] is not { } item || !Matches(item, key))
                        continue;
                    int take = Math.Min(left, item.Stack);
                    items[i] = item.ConsumeStack(take);
                    left -= take;
                }
            }
        }
        crates.ForEach(c => c.clearNulls());
        return true;
    }

    // ---- salesman ----

    private static bool Sellable(Item item) => item is SObject o && o.canBeShipped() && Utility.getSellToStorePriceOfItem(item) > 0
        && o.Category != SObject.SeedsCategory; // seeds (and saplings) are for the field hands, not the market

    /// <summary>Shipping-bin value of some of a stack. Crude and gasoline go at today's oil price, like the bin.</summary>
    private static long Value(MarketState state, Item item, int count) => item.QualifiedItemId switch
    {
        Oil.QCrude => (long)Market.CrudePrice(state) * count,
        Oil.QGasoline => (long)Market.GasolinePrice(state) * count,
        _ => (long)Utility.getSellToStorePriceOfItem(item, countStack: false) * count,
    };

    /// <summary>Items and shipping value of what's in the crates right now (the most he could sell).</summary>
    public static (int Items, long Value) CrateGoods(MarketState state)
    {
        var goods = Crate.Items().Where(Sellable).ToList();
        return (goods.Sum(i => i.Stack), goods.Sum(i => Value(state, i, i.Stack)));
    }

    /// <summary>
    /// Head out with the goods. Buyers only take part of the load (RNG: some days all of it, some days a handful);
    /// what they take leaves the crate, the rest stays. Town: back tonight. City: back tomorrow morning.
    /// </summary>
    public static string Send(MarketState state, bool city, int today)
    {
        var stacks = Crate.All()
            .SelectMany(crate => crate.Items.Select((item, index) => (Crate: crate, Index: index, Item: item)))
            .Where(s => s.Item != null && Sellable(s.Item))
            .ToList();
        if (stacks.Count == 0)
            return "Crate's empty, boss. Nothin' to sell.";

        var rng = new Random(ModEntry.Seed ^ (today * 7919) ^ (city ? 1 : 2)); // fixed per day and trip: no rerolling
        int offered = stacks.Sum(s => s.Item.Stack);
        int quota = Market.SalesQuota(offered, rng);
        if (quota < offered * 0.4 && CrewSkills.Lucky(Role.Salesman))
            quota = Math.Max(quota, Market.SalesQuota(offered, rng)); // talked his way out of a slow day
        int left = quota;
        long value = 0;
        state.TripItems.Clear();
        foreach (var (crate, index, item) in stacks.OrderBy(_ => rng.Next()))
        {
            if (left == 0)
                break;
            int take = Math.Min(left, item.Stack);
            long worth = Value(state, item, take);
            value += worth;
            var tally = state.TripItems.TryGetValue(item.QualifiedItemId, out var t) ? t : state.TripItems[item.QualifiedItemId] = new long[2];
            tally[0] += take;
            tally[1] += worth;
            crate.Items[index] = item.ConsumeStack(take);
            left -= take;
        }
        Crate.All().ForEach(c => c.clearNulls());

        var (markup, cutRate) = SalesTerms();
        var (_, cut, net) = Market.SalesPayout(value, city, markup, cutRate);
        state.TripPayout = net;
        state.TripCut = cut;
        state.TripSold = quota - left;
        state.TripOffered = offered;
        state.TripToCity = city;
        state.TripReturnDay = city ? today + 1 : today;
        return city ? "Hittin' the road to the city! Back tomorrow mornin'." : "Off to work the town! Back by evenin'.";
    }

    private static void PayTrip(MarketState state)
    {
        Game1.player.Money += (int)Math.Min(state.TripPayout, int.MaxValue - Game1.player.Money);
        CrewSkills.Work(Role.Salesman, 10 + state.TripSold / 10);
        CrewReport.Earned(Role.Salesman, state.TripPayout);
        CrewReport.Count(Role.Salesman, "items sold", state.TripSold);
        string where = state.TripToCity ? "the city" : "town";
        string mood = state.TripSold >= state.TripOffered ? "Sold out!" : state.TripSold <= 10 ? "Tough crowd." : "Not bad.";
        var top = state.TripItems.OrderByDescending(p => p.Value[1]).Take(3)
            .Select(p => $"{ItemRegistry.GetDataOrErrorItem(p.Key).DisplayName} x{p.Value[0]} ({p.Value[1]:N0}g)").ToList();
        // every item for the report's detail view, at what it actually sold for (his markup, before his cut)
        long baseValue = state.TripItems.Values.Sum(t => t[1]);
        double toSale = baseValue > 0 ? (state.TripPayout + state.TripCut) / (double)baseValue : 1;
        foreach (var (id, tally) in state.TripItems.OrderByDescending(p => p.Value[1]))
            CrewReport.Detail(Role.Salesman, $"{ItemRegistry.GetDataOrErrorItem(id).DisplayName} x{tally[0]:N0}", (long)Math.Round(tally[1] * toSale));
        if (state.TripCut > 0)
            CrewReport.Detail(Role.Salesman, "Slick's cut", -state.TripCut);
        string best = top.Count > 0 ? $" Best sellers: {string.Join(", ", top)}." : "";
        Game1.addHUDMessage(new HUDMessage($"Slick's back from {where}: sold {state.TripSold} of {state.TripOffered} items, +{state.TripPayout:N0}g (kept {state.TripCut:N0}g)", HUDMessage.achievement_type));
        Reports[Role.Salesman] = $"{mood} Sold {state.TripSold} of {state.TripOffered} items in {where}: {state.TripPayout:N0}g for you, {state.TripCut:N0}g for me.{best} The rest's back in the crate.";
        state.TripItems.Clear();
        state.TripPayout = state.TripCut = 0;
        state.TripSold = state.TripOffered = 0;
        state.TripReturnDay = -1;
    }
}
