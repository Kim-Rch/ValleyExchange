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


string biz = Market.PortfolioBrief(pb, "Roy", "Olivia", new[] { "Roy has struck oil: 3 pumpjacks." });
Check(biz.Contains("struck oil") && biz.IndexOf("struck oil") < biz.IndexOf("Olivia follows"), "brief: business lines before the instruction");
Check(biz.Length < 1100, $"brief with business stays small ({biz.Length} chars)");

// ValleyTalk request tweak
string sent = RequestTweak.Rewrite("https://openrouter.ai/api/v1/chat/completions", "{\"model\":\"z-ai/glm-5.3-flash:floor\",\"max_tokens\":2048,\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}");
var sentJson = System.Text.Json.Nodes.JsonNode.Parse(sent)!;
Check(sentJson["max_tokens"]!.GetValue<int>() == RequestTweak.MaxTokens && sentJson["model"]!.GetValue<string>() == "z-ai/glm-5.3-flash", "token cap raised, :floor dropped");
Check(sentJson["provider"]!["order"]![0]!.GetValue<string>() == "wafer/us" && sentJson["provider"]!["allow_fallbacks"]!.GetValue<bool>() && sentJson["messages"]![0]!["content"]!.GetValue<string>() == "hi", "Wafer first, fallbacks on, messages untouched");
Check(RequestTweak.Rewrite("https://openrouter.ai/api/v1/models", "") == "" && RequestTweak.Rewrite("http://localhost:11434/v1/chat/completions", "{\"a\":1}") == "{\"a\":1}", "other requests pass through untouched");

// the crew data from before the split stays in this record until Brawnies has copied it
string oldRecord = "{\"LastDay\":5,\"Stocks\":{},\"Holdings\":{},\"News\":[],\"Crew\":{\"Field\":2},\"Savings\":12345}";
var reloaded = Newtonsoft.Json.JsonConvert.DeserializeObject<MarketState>(oldRecord)!;
string resaved = Newtonsoft.Json.JsonConvert.SerializeObject(reloaded);
Check(reloaded.LastDay == 5 && resaved.Contains("\"Crew\":{\"Field\":2}") && resaved.Contains("\"Savings\":12345"), "crew fields survive a load and save untouched");

Console.WriteLine("All market checks passed");
