using ValleyExchange;

static void Check(bool ok, string what)
{
    if (!ok) { Console.WriteLine("FAIL: " + what); Environment.Exit(1); }
    Console.WriteLine("ok   " + what);
}

// determinism + 10 in-game years of sanity
var a = Market.NewState(42);
var b = Market.NewState(42);
for (int d = 1; d <= 112 * 10; d++)
{
    var ctx = new DayContext(d / 28 % 4, d % 28 + 1, d % 5 == 0, d % 28 == 13);
    Market.AdvanceDay(a, d, 42, ctx);
    Market.AdvanceDay(b, d, 42, ctx);
}
Check(Market.Companies.All(c => a.Stocks[c.Ticker].Price == b.Stocks[c.Ticker].Price), "same seed + day => same prices (no reload rerolls)");
Check(Market.Companies.All(c => a.Stocks[c.Ticker].Price > c.BasePrice / 20 && a.Stocks[c.Ticker].Price < c.BasePrice * 20), "prices stay sane after 10 years");
Check(a.Stocks.Values.All(s => s.History.Count == Market.HistoryDays), "history capped at 28 days");
Check(a.News.Count == Market.MaxNews, "news capped");
foreach (var c in Market.Companies)
    Console.WriteLine($"     {c.Ticker} base {c.BasePrice,6:N0} -> {a.Stocks[c.Ticker].Price,8:N0}");

// trading
var s = Market.NewState(1);
int p = Market.SharePrice(s, "KOLA");
var (n, cost) = Market.Buy(s, "KOLA", 10, p * 5 + 1);
Check(n == 5 && cost == p * 5, "buy is capped by gold");
Check(Market.Buy(s, "KOLA", 0, 1_000_000).Shares == 0, "buying 0 does nothing");
Check(Market.Buy(s, "KOLA", 5, p - 1).Shares == 0, "can't buy when broke");
var (sold, proceeds) = Market.Sell(s, "KOLA", 100);
Check(sold == 5 && proceeds == p * 5 && s.Holdings["KOLA"].Shares == 0 && s.Holdings["KOLA"].CostBasis == 0, "sell capped at owned, cost basis cleared");
Check(Market.Sell(s, "PEAR", 3).Shares == 0, "can't sell what you don't own");

Market.Buy(s, "KOLA", 4, long.MaxValue);
long basis = s.Holdings["KOLA"].CostBasis;
Market.Sell(s, "KOLA", 1);
Check(s.Holdings["KOLA"].CostBasis == basis - basis / 4, "partial sell reduces cost basis proportionally");

// dividends: paid on day 1 of a season, at the previous close
var dv = Market.NewState(7);
Market.GetHolding(dv, "KOLA").Shares = 100;
double close = dv.Stocks["KOLA"].Price;
var mid = Market.AdvanceDay(dv, 5, 7, new DayContext(0, 5, false, false));
Check(mid.Dividends == 0, "no dividends mid-season");
close = dv.Stocks["KOLA"].Price;
var res = Market.AdvanceDay(dv, 29, 7, new DayContext(1, 1, false, false));
Check(res.Dividends == (long)Math.Round(100 * close * 0.03), "season-start dividend = shares x close x yield");

// old saves (12 companies) pick up companies added later, without touching existing prices
var old = Market.NewState(3);
double kolaBefore = old.Stocks["KOLA"].Price;
old.Stocks.Remove("GSNX");
old.Stocks.Remove("EXOF");
Market.EnsureStocks(old, 3);
Check(Market.Companies.All(c => old.Stocks.ContainsKey(c.Ticker)) && old.Stocks["GSNX"].History.Count == Market.HistoryDays, "missing companies are added with history");
Check(old.Stocks["KOLA"].Price == kolaBefore, "existing prices untouched by EnsureStocks");
Market.AdvanceDay(old, 1, 3, new DayContext(0, 2, false, false));
Check(Market.Companies.Select(c => c.Ticker).Distinct().Count() == Market.Companies.Count, "tickers are unique");

// ValleyTalk prompt brief
var pb = Market.NewState(9);
Check(Market.PortfolioBrief(pb, "Roy", "Olivia").Contains("Roy owns no stocks yet"), "brief: empty portfolio");
Market.Buy(pb, "ENVY", 15, long.MaxValue);
Market.Buy(pb, "KOLA", 120, long.MaxValue);
string brief = Market.PortfolioBrief(pb, "Roy", "Olivia");
Console.WriteLine(brief);
Check(brief.Contains("120 shares of Koala-Cola") && brief.Contains("15 shares of Envydia") && brief.Contains("Olivia follows the market"), "brief: lists holdings and the NPC");
Check(!brief.Contains("-+") && !brief.Contains("+-"), "brief: clean percent signs");
Check(brief.Length < 900, $"brief stays small for token cost ({brief.Length} chars)");

// oil wells: fixed per tile, roughly 25% dry / 15% gushers
var wells = Enumerable.Range(0, 4000).Select(i => Market.WellBarrels(77, "Farm", i % 80, i / 80)).ToList();
Check(wells.SequenceEqual(Enumerable.Range(0, 4000).Select(i => Market.WellBarrels(77, "Farm", i % 80, i / 80))), "well yield is stable per tile");
double dry = wells.Count(w => w == 0) / 4000.0, gush = wells.Count(w => w >= Market.GusherBarrels) / 4000.0;
Console.WriteLine($"     dry {dry:P0}, gushers {gush:P0}");
Check(dry is > 0.2 and < 0.3 && gush is > 0.1 and < 0.2, "dry/gusher odds about 25%/15%");
Check(wells.All(w => w == 0 || w is >= 3 and <= 6 || w is >= 12 and <= 18), "barrels in expected ranges");
Check(Market.WellBarrels(77, "Farm", 3, 4) != Market.WellBarrels(78, "Farm", 3, 4) || Market.WellBarrels(77, "Farm", 5, 4) != Market.WellBarrels(78, "Farm", 5, 4) || Market.WellBarrels(77, "Farm", 9, 9) != Market.WellBarrels(78, "Farm", 9, 9), "different saves have different oil maps");

// crude follows the energy stocks
var oil = Market.NewState(5);
foreach (var t in new[] { "SHFS", "EXOF" })
{
    oil.Stocks[t].History.Add(Market.Def(t).BasePrice);
    oil.Stocks[t].History.Add(Market.Def(t).BasePrice * 2);
}
Check(Market.CrudePrice(oil) == Market.CrudeBasePrice * 2, "crude doubles when oil stocks double");
Check(Math.Abs(Market.CrudeChange(oil) - 1) < 1e-9, "crude day change matches");
Check(Market.GasolinePrice(oil) == (int)Math.Round(Market.CrudeBasePrice * 2 * Market.GasolineMultiplier), "gasoline priced off crude");

Check(Market.RanchWage(0) == Market.RanchWageMin && Market.RanchWage(50) == 1000, "ranch wage: minimum, then per animal");
Check(Market.FieldWage(10) == Market.FieldWageMin && Market.FieldWage(500) == 1000, "field wage: minimum, then per crop");

string biz = Market.PortfolioBrief(pb, "Roy", "Olivia", new[] { "Roy has struck oil: 3 pumpjacks." });
Check(biz.Contains("struck oil") && biz.IndexOf("struck oil") < biz.IndexOf("Olivia follows"), "brief: business lines before the instruction");
Check(biz.Length < 1100, $"brief with business stays small ({biz.Length} chars)");

// worker pathfinding: goes around walls, gives up when walled in
var walls = new HashSet<Microsoft.Xna.Framework.Point>(Enumerable.Range(0, 9).Select(y => new Microsoft.Xna.Framework.Point(5, y))); // wall x=5, gap at y=9
bool Open(Microsoft.Xna.Framework.Point p) => p.X is >= 0 and < 12 && p.Y is >= 0 and < 12 && !walls.Contains(p);
var route = PathGrid.Find(Open, new(1, 1), new(9, 1));
Check(route != null && route[^1] == new Microsoft.Xna.Framework.Point(9, 1) && route.All(Open), "path reaches the goal on open tiles only");
Check(route!.Zip(route.Skip(1)).All(s => Math.Abs(s.First.X - s.Second.X) + Math.Abs(s.First.Y - s.Second.Y) == 1), "path moves one tile at a time");
Check(route.Count == 8 + 8 + 8, $"path is shortest around the wall ({route.Count} steps)");
walls.Add(new(5, 9)); walls.Add(new(5, 10)); walls.Add(new(5, 11));
Check(PathGrid.Find(Open, new(1, 1), new(9, 1)) == null, "no path through a closed wall");
Check(PathGrid.Find(Open, new(1, 1), new(1, 1))!.Count == 0, "already there = empty path");
var partialRoute = PathGrid.Find(Open, new(1, 1), new(9, 1), partial: true);
Check(partialRoute != null && partialRoute.Count > 0 && partialRoute.All(Open) && partialRoute[^1].X == 4, "walled in: partial path walks up to the wall instead of giving up");
var tooFar = PathGrid.Find(_ => true, new(0, 0), new(500, 500), maxNodes: 200, partial: true);
Check(PathGrid.Find(_ => true, new(0, 0), new(500, 500), maxNodes: 200) == null && tooFar!.Count > 0, "out of budget: partial path heads toward the goal");

// crew: hand counts, beds, migration
Check(Market.CabinBeds(1, 0) == 1 && Market.CabinBeds(5, 0) == 2 && Market.CabinBeds(5, 1) == 4 && Market.CabinBeds(20, 3) == 8 && Market.CabinBeds(20, 9) == 8, "cabin beds: count beds, capped 2/4/6/8 by upgrade");
var crewState = new MarketState();
Check(!Market.HasRoom(crewState, Role.Harvester, 0), "no beds, no hiring");
Market.SetCount(crewState, Role.Harvester, 3);
Market.SetCount(crewState, Role.Fisher, 1);
Check(Market.HeadCount(crewState) == 4 && Market.HasRoom(crewState, Role.Harvester, 5) && !Market.HasRoom(crewState, Role.Harvester, 4), "beds limit the head count");
Market.SetCount(crewState, Role.Harvester, 50);
Check(Market.Count(crewState, Role.Harvester) == Market.MaxPerRole && !Market.HasRoom(crewState, Role.Harvester, 100), "harvesters cap at 10");
Market.SetCount(crewState, Role.Salesman, 3);
Check(Market.Count(crewState, Role.Salesman) == 1 && !Market.HasRoom(crewState, Role.Salesman, 100), "one salesman handles it all");
Market.SetCount(crewState, Role.Fisher, 0);
Check(!Market.IsHired(crewState, Role.Fisher) && !crewState.Crew.ContainsKey(Role.Fisher), "letting the last one go removes the role");
Check(Market.Roles.All(r => r.Names.Length >= Market.MaxCount(r.Role)) && Market.Roles.SelectMany(r => r.Names).Distinct().Count() == Market.Roles.Sum(r => r.Names.Length), "enough unique names for every hand");
Check(Market.ExtraHandsWage(1) == 0 && Market.ExtraHandsWage(4) == 3 * Market.ExtraHandWage, "extra crew hands cost a flat wage each");

var legacy = new MarketState { FieldHands = true, RanchHands = true, Staff = { Role.Fisher, Role.Chef } };
Market.Migrate(legacy, cabins: 2);
Check(Market.Count(legacy, Role.Field) == 2 && Market.Count(legacy, Role.Ranch) == 2 && Market.Count(legacy, Role.Fisher) == 1 && Market.Count(legacy, Role.Chef) == 1, "old saves keep their crew");
Check(!legacy.FieldHands && !legacy.RanchHands && legacy.Staff.Count == 0, "old flags cleared after migrating");
Market.Migrate(legacy, cabins: 2);
Check(Market.HeadCount(legacy) == 6, "migrating twice changes nothing");
Check(Market.Roles.Select((r, i) => (int)r.Role == i).All(x => x), "role table matches enum order (sprite rows)");

// salesman
Check(Market.OilWage(2) == Market.OilWageMin && Market.OilWage(10) == 500, "roughneck wage: minimum, then per rig");
Check(Market.SalesPayout(1000, city: false) == (1200, 120, 1080), "town: +20%, he keeps 10%: still beats the shipping bin");
Check(Market.SalesPayout(1000, city: true) == (1500, 150, 1350), "city: +50%, he keeps 10% on a small haul");
Check(Market.SalesPayout(10_000, city: false).Cut == 200 + 1_200 + 400, "big haul (12k gross): 10% of 2k, 15% up to 10k, 20% above");
Check(Market.SalesPayout(10_000, city: false, cutRate: Market.SalesCut / 2).Cut == 900, "Honest Slick halves every tier");
var sr = new Random(11);
var quotas = Enumerable.Range(0, 4000).Select(_ => Market.SalesQuota(200, sr)).ToList();
Check(quotas.All(q => q is >= 1 and <= 200), "sales quota stays within the crate");
double all = quotas.Count(q => q == 200) / 4000.0, few = quotas.Count(q => q <= 10) / 4000.0;
Console.WriteLine($"     sold everything {all:P0}, only a handful {few:P0}");
Check(all is > 0.1 and < 0.2 && few is > 0.06 and < 0.14, "great days ~15%, bad days ~10%");
Check(Market.SalesQuota(3, sr) <= 3 && Market.SalesQuota(0, sr) == 0, "tiny or empty crates");

// hauls
var hr = new Random(4);
Check(Market.MinerHaul(0, hr).All(h => h.Id is not ("(O)380" or "(O)384" or "(O)386")), "shallow miner: no iron/gold/iridium");
Check(Market.MinerHaul(130, hr).Any(h => h.Id == "(O)386"), "skull cavern miner brings iridium");
Check(Enumerable.Range(0, 50).SelectMany(_ => Market.MinerHaul(50, hr)).All(h => h.Count >= 1), "haul counts positive");
int hurt = Enumerable.Range(0, 2000).Count(_ => Market.AdventurerHaul(60, hr).Hurt);
Check(hurt is > 120 and < 280, $"adventurer gets hurt ~10% ({hurt}/2000)");
var adv = Enumerable.Range(0, 20).Select(_ => Market.AdventurerHaul(10, hr)).First(a => !a.Hurt);
Check(adv.Gold >= 100 && adv.Loot.All(l => l.Id is not ("(O)768" or "(O)769")), "shallow adventurer: gold, no essences");

// day luck
var lr = new Random(21);
var minerDays = Enumerable.Range(0, 5000).Select(_ => Market.MinerDay(130, lr)).ToList();
double rocky = minerDays.Count(d => d.Luck == DayLuck.Bad) / 5000.0, jackpot = minerDays.Count(d => d.Luck == DayLuck.Jackpot) / 5000.0;
Console.WriteLine($"     miner: rock days {rocky:P0}, jackpots {jackpot:P1}");
Check(rocky is > 0.66 and < 0.74 && jackpot is > 0.008 and < 0.035, "miner: ~70% rock days, ~2% jackpots");
Check(minerDays.Where(d => d.Luck == DayLuck.Bad).All(d => d.Haul.All(h => h.Id is "(O)390" or "(O)382" or "(O)378" or "(O)535")), "rock days are just rocks, coal, copper, maybe a geode");
Check(minerDays.Where(d => d.Luck == DayLuck.Jackpot).All(d => d.Haul.Any(h => h.Id == "(O)72") && d.Haul.Any(h => h.Id == "(O)74")), "skull cavern jackpot: diamonds and a prismatic shard");
Check(Enumerable.Range(0, 2000).Select(_ => Market.MinerDay(10, lr)).All(d => d.Haul.All(h => h.Id != "(O)74")), "no prismatic shard before skull cavern");
var fishDays = Enumerable.Range(0, 5000).Select(_ => Market.FisherDay(lr)).ToList();
Check(fishDays.Where(d => d.Luck == DayLuck.Bad).All(d => d.Fish <= 2 && d.Extras.Count >= 2) && fishDays.Where(d => d.Luck == DayLuck.Jackpot).All(d => d.Extras.Any(e => e.Id == "(O)166")), "skunked days bring trash, jackpots a treasure chest");
Check(fishDays.Count(d => d.Luck == DayLuck.Bad) / 5000.0 is > 0.12 and < 0.18, "fisherman skunked ~15%");
var advDays = Enumerable.Range(0, 5000).Select(_ => Market.AdventurerDay(90, lr)).ToList();
Check(advDays.Count(d => d.Hurt) / 5000.0 is > 0.08 and < 0.12 && advDays.Where(d => d.Hurt).All(d => d.Gold == 0 && d.Loot.Count == 0), "adventurer hurt ~10%, brings nothing");
Check(advDays.Where(d => d.Luck == DayLuck.Jackpot).All(d => d.Gold >= 1100 && d.Loot.Any(l => l.Id == "(O)72")), "monster stash: diamond and 1000g+");

// fish quality follows the fisherman's day
var qr = new Random(5);
double Share(DayLuck luck, int quality) => Enumerable.Range(0, 20000).Count(_ => Market.FishQuality(luck, qr) == quality) / 20000.0;
Check(Share(DayLuck.Bad, 2) == 0 && Share(DayLuck.Bad, 4) == 0 && Share(DayLuck.Bad, 1) is > 0.08 and < 0.12, "skunked: just the odd silver");
Check(Share(DayLuck.Normal, 0) is > 0.62 and < 0.68 && Share(DayLuck.Normal, 4) is > 0.005 and < 0.02, "normal day: mostly plain, rare iridium");
Check(Share(DayLuck.Jackpot, 2) is > 0.37 and < 0.43 && Share(DayLuck.Jackpot, 4) is > 0.13 and < 0.17, "jackpot: lots of gold, some iridium");
Check(Enumerable.Range(0, 1000).All(_ => Market.FishQuality(DayLuck.Good, qr) is 0 or 1 or 2 or 4), "only real quality values (no 3)");

// fishing expeditions skip the special waters
Check(new[] { "VolcanoDungeon0", "Caldera", "Sewer", "BugLand", "WitchSwamp", "Submarine", "UndergroundMine77", "SkullCave", "Mine", "Default" }.All(Market.IsSpecialWater), "lava, sewer, bug lair, swamp, sub, mines are skipped");
Check(!new[] { "Beach", "Town", "Mountain", "Forest", "Desert", "IslandSouth", "IslandNorth", "Woods", "Custom_HighlandsCavern" }.Any(Market.IsSpecialWater), "ordinary and modded waters are fished");

// skill trees
Check(Skills.Level(0) == 1 && Skills.Level(Skills.XpFor(5)) == 5 && Skills.Level(Skills.XpFor(5) - 1) == 4 && Skills.Level(999_999) == Skills.MaxLevel, "levels 1-25 from XP");
Check(Enumerable.Range(2, 24).All(l => Skills.XpFor(l) > Skills.XpFor(l - 1)), "each level costs more XP");
Check(Market.Roles.All(r => Skills.Tree(r.Role).Length == 5 && Skills.Tree(r.Role).All(t => t.Length == 2)), "every role has 5 tiers of 2 perks");
// the bank
Check(Market.SavingsInterest(100_000, 0) == 200 && Market.SavingsInterest(100_000, 0.5) == 300 && Market.SavingsInterest(0, 1) == 0, "savings earn 0.2%/day, more with skill");
Check(Market.LoanInterest(10_000, 0) == 50 && Market.LoanInterest(10_000, 0.75) == 13 && Market.LoanInterest(1, 0) == 1, "loans cost 0.5%/day, rounded up");
Check(Market.LoanPayment(100_000) == 5_000 && Market.LoanPayment(4_000) == 500 && Market.LoanPayment(300) == 300, "morning payment: 5%, at least 500g, never more than owed");
var bank = new MarketState();
Skills.AddXp(bank, Role.Banker, 999_999);
for (int t = 0; t < 5; t++) Skills.Choose(bank, Role.Banker, t, t == 0 ? 1 : 0); // as many rate cuts as possible
Check(Skills.Total(bank, Role.Banker, PerkKind.LowRate) <= 0.75, "loan interest never goes negative");

// research
var lab = new MarketState { ResearchPoints = 10_000 };
var field1 = Research.In("Botany").First();
var field2 = Research.In("Botany").ElementAt(1);
Check(Research.Trees.All(t => Research.In(t).Select(n => n.Tier).SequenceEqual(Enumerable.Range(1, Research.In(t).Count()))), "every tree's tiers run 1, 2, 3...");
Check(Research.Trees.Count == 19 && Research.Nodes.Count == 152 && Research.Nodes.Select(n => n.Id).Distinct().Count() == 152, "19 trees, 152 projects");
Check(Enum.GetValues<Effect>().All(e => Research.Nodes.Any(n => n.Effect == e)), "every effect is researchable somewhere");
var fish = new MarketState();
fish.Researched.Add(Research.In("Ichthyology").ElementAt(1).Id);
Check(Skills.Total(fish, Role.Crabber, PerkKind.Yield) > Skills.Total(fish, Role.Chef, PerkKind.Yield), "role research only helps its roles");
Check(Research.WhyNot(lab, field2, 1_000_000) != null && Research.WhyNot(lab, field1, 0) != null && Research.WhyNot(lab, field1, 1_000_000) == null, "tiers in order, gold needed");
Research.Start(lab, field1, today: 10, speed: 0);
Check(lab.ResearchPoints == 10_000 - field1.Points && Research.WhyNot(lab, Research.In("Geology").First(), 1_000_000) != null, "one project at a time");
Check(Research.Finish(lab, 10) == null && Research.Finish(lab, 10 + field1.Days) == field1 && Research.Done(lab, field1) && lab.Researching == null, "a project finishes on its day");
Check(Math.Abs(Research.Total(lab, Effect.DoubleForage) - field1.Amount) < 1e-9, "finished research adds up");
Check(Research.Days(Research.In("Botany").Last(), 1.0) < Research.In("Botany").Last().Days, "a faster scientist finishes sooner");
var wages = new MarketState();
wages.Researched.Add(Research.In("Brawnie Studies").First().Id);
Check(Skills.Total(wages, Role.Chef, PerkKind.Wage) > Skills.Total(new MarketState(), Role.Chef, PerkKind.Wage), "crew research cuts every role's wages");

Check(new MarketState().ClearKinds.SequenceEqual(new[] { "Weeds", "Branches", "Stones" }), "groundskeepers start on weeds, branches, stones (grass and trees are opt-in)");

// the guild and its endless dungeon
Check(Guild.Label(0) == "1.1.1" && Guild.Label(9) == "1.1.10" && Guild.Label(10) == "1.2.1" && Guild.Label(100) == "2.1.1", "stages count 1.1.1 ... 1.1.10, 1.2.1 ... 2.1.1");
var lone = new GuildState { Members = { new GuildMember { Name = "T", Class = GuildClass.Tank } } };
Check(Guild.WinChance(Guild.Power(lone, 0), 0) > 0.9999 && Guild.Odds(Guild.WinChance(Guild.Power(lone, 0), 0)) == "100%", "a lone tank walks 1.1.1");
Check(Guild.WinChance(Guild.Power(lone, 0), 150) < 1e-8, "and has no hope deep down");
var tiny = Guild.Odds(Guild.WinChance(Guild.Power(lone, 0), 150));
Check(tiny.Contains("0.0000") && tiny.EndsWith("%") && Guild.Odds(1e-10) == "0.00000001%", $"hopeless odds read like {tiny} (and 1e-10 like {Guild.Odds(1e-10)})");
Check(Enumerable.Range(0, 300).All(n => Guild.Difficulty(n + 1) > Guild.Difficulty(n)), "every stage is harder than the last");
var strong = new GuildMember { Class = GuildClass.Tank, Level = 30, Gear = 5, Promoted = true };
Check(Guild.MemberPower(strong) > 20 * Guild.MemberPower(lone.Members[0]), "levels, gear and promotion add up");
var mixed = new GuildState { Members = { new() { Class = GuildClass.Tank }, new() { Class = GuildClass.Priest }, new() { Class = GuildClass.Mage } } };
var samey = new GuildState { Members = { new() { Class = GuildClass.Mage }, new() { Class = GuildClass.Mage }, new() { Class = GuildClass.Mage } } };
Check(Guild.Power(mixed, 0) / mixed.Members.Sum(Guild.MemberPower) > Guild.Power(samey, 0) / samey.Members.Sum(Guild.MemberPower), "a balanced party gets a bigger synergy bonus");
Check(Guild.Slots(1) == 3 && Guild.Slots(4) == 4 && Guild.Slots(10) == 6 && Guild.Attempts(1) == 3 && Guild.Attempts(10) == 7, "the hall: 3 to 6 slots, 3 to 7 runs a day");
Check(Enumerable.Range(1, 99).All(l => Guild.LevelCost(l + 1) > Guild.LevelCost(l)) && Guild.HallCost(2) == 2 * Guild.HallCost(1), "upgrades cost more each time");
var day = Guild.Run(lone, today: 5, new Random(1));
var pushes = day.Where(r => !r.Farm).ToList();
Check(pushes.Count >= 1 && pushes.TakeWhile(r => r.Win).Count() == lone.Stage && lone.Best == lone.Stage && pushes.All(r => r.Win || r == pushes.Last())
    && day.SkipWhile(r => !r.Farm).All(r => r.Farm) && (day.Any(r => r.Farm) == pushes.Any(r => !r.Win) || !day.Any(r => r.Farm)), "wins push deeper; after a loss they only farm");
var hurtParty = new GuildState { Stage = 400, Members = { new() { Name = "A", Class = GuildClass.Brawler }, new() { Name = "B", Class = GuildClass.Brawler } } };
var bad = Guild.Run(hurtParty, today: 5, new Random(3));
Check(bad.Count(r => !r.Farm) == 1 && !bad[0].Win && hurtParty.Stage == 400, "a hopeless stage: one loss, no progress");
Check(Guild.Run(hurtParty, today: 5, new Random(3), tough: true) is var safe && safe[0].Injured.Count == 0, "Tough as Nails: nobody gets hurt");
Check(safe.Any(r => r.Farm && r.Win && r.Gold > 0) && hurtParty.Stage == 400 && safe.Where(r => r.Farm).All(r => Guild.WinChance(Guild.Power(hurtParty, 5), 0) > 0), "a stuck party farms easier stages for gold");
var mobRng = new Random(11);
var mobs = Enumerable.Range(0, 100_000).Select(_ => Guild.RollMonster(5, mobRng)).ToList();
Check(Math.Abs(mobs.Count(m => m != null) / 100_000.0 - 0.5) < 0.01 && mobs.OfType<(string, int, string)>().All(m => m.Item2 is >= 1 and <= 5), "monster drops: half the clears, 1-5 of them");
var lootRng = new Random(42);
var rolls = Enumerable.Range(0, 2_000_000).Select(_ => Guild.RollLoot(lootRng)?.Rarity ?? "none").GroupBy(r => r).ToDictionary(g => g.Key, g => g.Count() / 2_000_000.0);
Check(Math.Abs(rolls["none"] - 0.60) < 0.003 && Guild.Rarities.All(r => Math.Abs(rolls.GetValueOrDefault(r.Name) - Guild.Chance(r.Name)) < Math.Max(0.0001, Guild.Chance(r.Name) * 0.1))
    && Math.Abs(Guild.Chance("Common") / Guild.Chance("Uncommon") - 30.0 / 25) < 1e-9,
    $"loot: nothing {rolls["none"]:P1}, common {rolls["Common"]:P1}, uncommon {rolls["Uncommon"]:P1}, rare {rolls["Rare"]:P1}, epic {rolls["Epic"]:P2}, legendary {rolls["Legendary"]:P3}, mythical {rolls.GetValueOrDefault("Mythical"):P4}");
var goldRng = new Random(7);
var golds = Enumerable.Range(0, 100_000).Select(_ => Guild.RollGold(9, goldRng, 1)).ToList();
Check(golds.All(x => x is >= 1 and <= 500) && golds.Max() > 450, "gold: every clear, 1g up to 50g a stage number (stage 1.1.10: up to 500g)");
var medic = new GuildState { Stage = 400, Members = { new() { Name = "A", Class = GuildClass.Brawler }, new() { Name = "B", Class = GuildClass.Brawler } } };
Check(Guild.Run(medic, today: 5, new Random(3), injuryCut: 1)[0].Injured.Count == 0, "Doc's Field Dressing cuts injuries");
var mapped = new GuildState { Members = { new() { Class = GuildClass.Tank, Level = 50, Gear = 10, Promoted = true } } };
Check(Guild.Run(mapped, today: 5, new Random(1), extraRuns: 2).Count == Guild.Attempts(1) + 2, "Dungeon Cartography adds runs");
var oldSave = new MarketState { Crew = { [Role.Adventurer] = 4 } };
Market.Normalize(oldSave);
Check(Market.Count(oldSave, Role.Adventurer) == 1, "a band of adventurers becomes one Guild Master");

// the pirate fleet
var ship = new GuildState { Members = { new() { Name = "S", Class = GuildClass.Brawler, Level = 3 }, new() { Name = "Q", Class = GuildClass.Rogue } } };
var won = Guild.Sail(ship, today: 10, new Random(1));
Check(won.Win && ship.Stage == 1 && ship.Best == 1 && ship.Members.All(m => m.Level >= 2) && won.Gold >= Guild.MaxGold(0) * Guild.VoyageGold && won.Loot.Count(l => l.Rarity == "Sea") == 2,
    "a won voyage: next stage, +1 level each, 25x gold, two sea drops");
var doomed = new GuildState { Stage = 400, Members = Enumerable.Range(0, 40).Select(i => new GuildMember { Name = $"P{i}", Class = GuildClass.Bard }).ToList() };
var sunk = Guild.Sail(doomed, today: 10, new Random(2));
Check(!sunk.Win && sunk.Gold == 0 && doomed.Stage == 400 && sunk.Injured.Count > 0 && doomed.Members.Where(m => m.Injured(10)).All(m => m.InjuredUntil is >= 14 and <= 20),
    "a lost voyage: nothing, no progress, hurt for 4-10 days");
Check(Guild.Sail(doomed, today: 30, new Random(2), tough: true).Injured.Count == 0, "Iron Hull: nobody hurt");
Check(Guild.Voyage(0) == 7 && Guild.Voyage(2) == 5 && Guild.Voyage(9) == Guild.MinVoyageDays, "voyages: 7 days, shorter with research, 4 at least");
Check(Guild.ClassesFor(true).Count == Guild.Classes.Count && Guild.Title(new GuildMember { Class = GuildClass.Mage }, fleet: true) == "Gunner", "pirate classes mirror the guild's");
// spreading land hands
Check(Market.SpreadHands(new[] { 200.0, Market.LandWeight(30, far: true) }, 2).SequenceEqual(new[] { 0, 0 }), "a busy farm keeps both hands home");
Check(Market.SpreadHands(new[] { 60.0, Market.LandWeight(300, far: true) }, 2).SequenceEqual(new[] { 1, 0 }), "a big job on the island draws one away, the other stays");
Check(Market.SpreadHands(new[] { 0.0, 0.0 }, 2).All(p => p == -1) && Market.SpreadHands(new[] { 0.0, 50.0 }, 1)[0] == 1, "no work: no assignment; only work elsewhere: go there");

// the fisher's own luck
var fishRng = new Random(9);
var biting = new List<(string Fish, int Price)> { ("carp", 30), ("bream", 45), ("salmon", 75), ("pike", 100), ("tuna", 100), ("catfish", 200), ("sturgeon", 200), ("lava eel", 700) };
var caught = Enumerable.Range(0, 100_000).Select(_ => Market.PickFish(biting, fishRng)!).GroupBy(f => Market.FishTier(biting.First(b => b.Fish == f).Price)).ToDictionary(g => g.Key, g => g.Count() / 100_000.0);
Check(Math.Abs(caught[0] - 0.60) < 0.01 && Math.Abs(caught[1] - 0.28) < 0.01 && Math.Abs(caught[2] - 0.10) < 0.01 && Math.Abs(caught[3] - 0.02) < 0.005,
    "fisher luck: common 60%, uncommon 28%, rare 10%, epic 2%");
var cheapOnly = new List<(string Fish, int Price)> { ("carp", 30) };
var pricyOnly = new List<(string Fish, int Price)> { ("catfish", 200) };
Check(Enumerable.Range(0, 1000).All(_ => Market.PickFish(cheapOnly, fishRng) == "carp" && Market.PickFish(pricyOnly, fishRng) == "catfish")
    && Market.PickFish(new List<(string, int)>(), fishRng) == null, "a missing rarity falls back to what's biting; nothing biting, no fish");

// the shopkeeper
var haggleRng = new Random(3);
var deals = Enumerable.Range(0, 100_000).Select(_ => Market.Haggle(haggleRng, 0)).ToList();
Check(Math.Abs(deals.Count(d => d == 0) / 100_000.0 - Market.HaggleFail) < 0.01 && deals.Where(d => d > 0).All(d => d is >= 0.05 and <= 0.15)
    && Enumerable.Range(0, 1000).All(_ => Market.Haggle(haggleRng, 0.5) is 0 or Market.HaggleCap), "haggling: 15% fail, else 5-15%, skill caps at 30%");
Check(Market.SeasonProfit(80, 1, 20, 4, 0, 27) == (80 - 20) * 6 && Market.SeasonProfit(50, 3, 80, 13, 4, 27) == 50 * 3 * (1 + 14 / 4) - 80
    && Market.SeasonProfit(300, 1, 100, 12, 0, 10) == 0, "season profit: replant cycles, regrowth, too late is zero");
var offers = new List<Market.SeedOffer> { new("(O)472", 20, 360), new("(O)481", 80, 520), new("(O)479", 50, -1) };
var order = Market.SeedOrder(offers, new Dictionary<string, int> { ["(O)472"] = 30 }, new Dictionary<string, int> { ["(O)472"] = 10 },
    uncoveredSoil: 100, auto: true, budget: 5_000, discount: 0.10);
Check(order.Count == 2 && order[0] == ("(O)472", 20, 18) && order[1].Id == "(O)481" && order[1].Each == 72 && order[1].Count == (5_000 - 20 * 18) / 72,
    "seed order: pinned top-up first, then the best crop for the empty soil, inside the budget");
Check(Market.SeedOrder(offers, new Dictionary<string, int>(), new Dictionary<string, int>(), 100, auto: false, 5_000, 0).Count == 0
    && Market.SeedOrder(offers, new Dictionary<string, int>(), new Dictionary<string, int>(), 0, auto: true, 5_000, 0).Count == 0, "no list, no auto or no soil: no shopping");

var pitRng = new Random(5);
var pits = Enumerable.Range(0, 100_000).Select(_ => Market.TestPit(pitRng)).ToList();
Check(Math.Abs(pits.Count(x => x == null) / 100_000.0 - 0.11) < 0.01 && Math.Abs(pits.Count(x => x?.Id == "(O)275") / 100_000.0 - 0.02) < 0.003, "test pits: 11% empty, 2% artifact troves");

var crowRng = new Random(5);
var stash = Enumerable.Range(0, 100_000).Select(_ => Market.CrowStash(crowRng)).ToList();
Check(Math.Abs(stash.Count(x => x == null) / 100_000.0 - 0.40) < 0.01 && Math.Abs(stash.Count(x => x?.Id == "(O)80") / 100_000.0 - 0.20) < 0.01
    && Math.Abs(stash.Count(x => x?.Id == "(O)72") / 100_000.0 - 0.01) < 0.002, "crow stash: 40% twigs, 20% quartz, 1% diamond");

var sk = new MarketState();
Check(!Skills.Choose(sk, Role.Miner, 0, 0), "can't pick a perk before level 5");
Check(Skills.AddXp(sk, Role.Miner, Skills.XpFor(5)) == 5 && Skills.Unpicked(sk, Role.Miner) == 1, "level-up reported, tier 1 open");
Check(Skills.Choose(sk, Role.Miner, 0, 0) && !Skills.Choose(sk, Role.Miner, 0, 1) && Skills.Pick(sk, Role.Miner, 0) == 0, "a tier is picked once");
Check(!Skills.Choose(sk, Role.Miner, 1, 0), "tier 2 waits for level 10");
Check(Math.Abs(Skills.Total(sk, Role.Miner, PerkKind.Luck) - 0.25) < 1e-9 && Math.Abs(Skills.Total(sk, Role.Miner, PerkKind.Yield) - 0.05) < 1e-9, "perk + per-level passive add up");
Skills.AddXp(sk, Role.Chef, 999_999);
for (int t = 0; t < 5; t++) Skills.Choose(sk, Role.Chef, t, 0);
Check(Skills.Total(sk, Role.Chef, PerkKind.Quality) <= 0.75 && Skills.Total(sk, Role.Chef, PerkKind.Wage) <= 0.6 && Skills.Xp(sk, Role.Chef) == Skills.XpFor(Skills.MaxLevel), "caps on quality, wages, XP");
Check(Skills.Wage(sk, Role.Chef, 300) < 300 && Skills.Wage(new MarketState(), Role.Chef, 300) == 299, "wages get cheaper with skill (level 1: -0.5%)");

// ValleyTalk request tweak
string sent = RequestTweak.Rewrite("https://openrouter.ai/api/v1/chat/completions", "{\"model\":\"z-ai/glm-5.3-flash:floor\",\"max_tokens\":2048,\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}");
var sentJson = System.Text.Json.Nodes.JsonNode.Parse(sent)!;
Check(sentJson["max_tokens"]!.GetValue<int>() == RequestTweak.MaxTokens && sentJson["model"]!.GetValue<string>() == "z-ai/glm-5.3-flash", "token cap raised, :floor dropped");
Check(sentJson["provider"]!["order"]![0]!.GetValue<string>() == "wafer/us" && sentJson["provider"]!["allow_fallbacks"]!.GetValue<bool>() && sentJson["messages"]![0]!["content"]!.GetValue<string>() == "hi", "Wafer first, fallbacks on, messages untouched");
Check(RequestTweak.Rewrite("https://openrouter.ai/api/v1/models", "") == "" && RequestTweak.Rewrite("http://localhost:11434/v1/chat/completions", "{\"a\":1}") == "{\"a\":1}", "other requests pass through untouched");

Console.WriteLine("All market checks passed");
