using System;
using System.Collections.Generic;
using System.Linq;

namespace ValleyExchange;

public enum GuildClass { Tank, Knight, Priest, Brawler, Mage, Ranger, Rogue, Bard }

/// <summary>A class in the guild: base power, hire cost, its promoted title and what it brings to a party.</summary>
public sealed record ClassDef(GuildClass Class, string Title, string Promoted, double Power, int Hire, string Blurb);

/// <summary>A party member: a Brawnie of some class, with a level, a gear tier and maybe a promotion.</summary>
public sealed class GuildMember
{
    public string Name { get; set; } = "";
    public GuildClass Class { get; set; }
    public int Level { get; set; } = 1;
    public int Gear { get; set; }
    public bool Promoted { get; set; }
    /// <summary>Back on their feet on this day (injured until then).</summary>
    public int InjuredUntil { get; set; }

    public bool Injured(int today) => today < this.InjuredUntil;
}

/// <summary>The guild: its party, the hall, how deep they are in the endless dungeon, and the run log.</summary>
public sealed class GuildState
{
    public List<GuildMember> Members { get; set; } = new();
    public int HallLevel { get; set; } = 1;
    /// <summary>The next stage to clear (0 = 1.1.1) and the deepest one ever reached.</summary>
    public int Stage { get; set; }
    public int Best { get; set; }
    public List<string> Log { get; set; } = new();
    /// <summary>The fleet only: the day the ship sailed (-1: in port), the day it's due back, the last day it came home.</summary>
    public int SailDay { get; set; } = -1;
    public int ReturnDay { get; set; } = -1;
    public int LastPort { get; set; } = -1;
    public bool AtSea => this.SailDay >= 0;
}

/// <summary>One attempt at a stage.</summary>
public sealed record RunLog(string Stage, bool Win, long Gold, List<(string Id, int Count, string Rarity)> Loot, List<string> Injured, bool Record, bool Farm = false);

/// <summary>
/// The Guild Master's endless dungeon. Stages run 1.1.1 → 1.1.10 → 1.2.1 → ... → 2.1.1 forever, each one harder. The party's
/// power against the stage's difficulty gives the odds on a steep curve: a lone tank walks 1.1.1, the deep stages are
/// one-in-a-billion until you level, gear and promote the party and build up the hall.
/// </summary>
public static class Guild
{
    public const int MaxHall = 10, MaxLevel = 100, MaxGear = 10, PromoteLevel = 25, PromoteGold = 25_000;
    public const string PromoteItem = "(O)72"; // a diamond
    private const double Steepness = 6, Growth = 1.10, BaseDifficulty = 2;

    public static readonly IReadOnlyList<ClassDef> Classes = new ClassDef[]
    {
        new(GuildClass.Tank, "Tank", "Juggernaut", 10, 1_000, "Soaks every hit. A frontliner: +15% party power."),
        new(GuildClass.Knight, "Knight", "Paladin", 12, 1_200, "Sword and board. A frontliner: +15% party power."),
        new(GuildClass.Priest, "Priest", "Archbishop", 8, 1_500, "Heals: +15% party power, injuries halved."),
        new(GuildClass.Brawler, "Brawler", "Warlord", 14, 1_000, "Punches first, asks never. Big damage."),
        new(GuildClass.Mage, "Mage", "Archmage", 16, 1_800, "Fireballs. The hardest hitter, made of paper."),
        new(GuildClass.Ranger, "Ranger", "Sharpshooter", 13, 1_200, "Arrows from the back row. Steady damage."),
        new(GuildClass.Rogue, "Rogue", "Shadow", 11, 1_400, "Picks every pocket: 25% chance of extra loot."),
        new(GuildClass.Bard, "Bard", "Virtuoso", 7, 1_600, "Plays the banjo: +10% party power."),
    };

    /// <summary>The pirate captain's crew: the same eight roles under sea names, a bit pricier to sign.</summary>
    public static readonly IReadOnlyList<ClassDef> PirateClasses = new ClassDef[]
    {
        new(GuildClass.Tank, "Bosun", "Iron Bosun", 10, 1_500, "Built like an anchor. A frontliner: +15% crew power."),
        new(GuildClass.Knight, "Boarder", "Corsair", 12, 1_800, "First over the rail. A frontliner: +15% crew power."),
        new(GuildClass.Priest, "Surgeon", "Sawbones", 8, 2_250, "Patches holes in hulls and hides: +15% power, injuries halved."),
        new(GuildClass.Brawler, "Deckhand", "Sea Dog", 14, 1_500, "Swabs, hauls, punches. Big damage."),
        new(GuildClass.Mage, "Gunner", "Master Gunner", 16, 2_700, "Runs the cannons. The hardest hitter, deaf as a post."),
        new(GuildClass.Ranger, "Navigator", "Sea Wolf", 13, 1_800, "Reads the stars and the spyglass. Steady damage."),
        new(GuildClass.Rogue, "Quartermaster", "Pirate Lord", 11, 2_100, "Splits the plunder: 25% chance of extra loot."),
        new(GuildClass.Bard, "Shantyman", "Siren", 7, 2_400, "Sings the shanties: +10% crew power."),
    };

    public static IReadOnlyList<ClassDef> ClassesFor(bool fleet) => fleet ? PirateClasses : Classes;
    public static ClassDef Def(GuildClass c, bool fleet = false) => ClassesFor(fleet)[(int)c];
    public static string Title(GuildMember m, bool fleet = false) => m.Promoted ? Def(m.Class, fleet).Promoted : Def(m.Class, fleet).Title;

    // ---- stages ----

    /// <summary>Stage index → "world.zone.stage" (10 stages a zone, 10 zones a world).</summary>
    public static string Label(int stage) => $"{stage / 100 + 1}.{stage / 10 % 10 + 1}.{stage % 10 + 1}";

    public static double Difficulty(int stage) => BaseDifficulty * Math.Pow(Growth, stage);

    /// <summary>Chance to clear a stage with this much power.</summary>
    public static double WinChance(double power, int stage) => power <= 0 ? 0 : 1 / (1 + Math.Pow(Difficulty(stage) / power, Steepness));

    /// <summary>"100%", "87.3%", or "0.0000012%" for the hopeless ones.</summary>
    public static string Odds(double chance)
    {
        double pct = chance * 100;
        if (pct >= 99.995)
            return "100%";
        if (pct >= 0.01)
            return $"{pct:0.##}%";
        if (pct < 1e-12)
            return "< 0.000000000001%"; // one in a hundred trillion and worse
        int digits = Math.Min(14, (int)Math.Ceiling(-Math.Log10(pct)) + 1);
        return pct.ToString("0." + new string('#', digits)) + "%";
    }

    // ---- the party ----

    public static double MemberPower(GuildMember m) =>
        Def(m.Class).Power * (1 + 0.25 * (m.Level - 1)) * (1 + 0.35 * m.Gear) * (m.Promoted ? 1.5 : 1);

    public static List<GuildMember> Healthy(GuildState g, int today) => g.Members.Where(m => !m.Injured(today)).ToList();

    /// <summary>Party power: everyone fit to fight, times composition bonuses and the hall's aura.</summary>
    public static double Power(GuildState g, int today, double extra = 0)
    {
        var party = Healthy(g, today);
        if (party.Count == 0)
            return 0;
        var classes = party.Select(m => m.Class).Distinct().ToList();
        double synergy = 1
            + (classes.Contains(GuildClass.Tank) || classes.Contains(GuildClass.Knight) ? 0.15 : 0)
            + (classes.Contains(GuildClass.Priest) ? 0.15 : 0)
            + (classes.Contains(GuildClass.Bard) ? 0.10 : 0)
            + 0.05 * (classes.Count - 1);
        return party.Sum(MemberPower) * synergy * (1 + 0.05 * g.HallLevel) * (1 + extra);
    }

    public static int Slots(int hall) => 3 + (Math.Clamp(hall, 1, MaxHall) - 1) / 3; // 3, 4, 5, then 6 at hall 10
    public static int Attempts(int hall) => 3 + (Math.Clamp(hall, 1, MaxHall) - 1) / 2; // 3 ... 7 a day

    // ---- costs ----

    public static long Wage(GuildMember m) => 30 + 10L * m.Level;
    public static long Wages(GuildState? g) => g?.Members.Sum(Wage) ?? 0;
    public static long LevelCost(int level) => (long)Math.Round(100 * level * Math.Pow(1.08, level));
    public static long HallCost(int hall) => 5_000L << (hall - 1); // 5k, 10k, 20k ... 1.28M
    public static long GearGold(int gear) => 500L * (gear + 1) * (gear + 1);

    /// <summary>What forging the next gear tier eats from the crate: copper, iron, gold, iridium, radioactive bars, then a prismatic shard.</summary>
    public static (string Id, int Count) GearItem(int gear) => gear switch
    {
        < 2 => ("(O)334", 3),
        < 4 => ("(O)335", 3),
        < 6 => ("(O)336", 3),
        < 8 => ("(O)337", 3),
        < 9 => ("(O)910", 2),
        _ => ("(O)74", 1),
    };

    // ---- the runs ----

    /// <summary>Gold for clearing a stage, every time: anywhere from 1g to 50g per stage number (stage 10: up to 500g).</summary>
    public static long MaxGold(int stage) => 50L * (stage + 1);

    public static long RollGold(int stage, Random rng, double more) => (long)Math.Round(rng.NextInt64(1, MaxGold(stage) + 1) * more);

    /// <summary>60% of loot rolls come up empty; the other 40% split by rarity weight (common 30 : uncommon 25 : ... : mythical 0.025).</summary>
    public const double NothingChance = 0.60;

    /// <summary>Loot rarities: (name, weight, pool). A rarity's chance per roll is its share of the weights times 40%.</summary>
    public static readonly (string Name, double Weight, (string Id, int Min, int Max)[] Pool)[] Rarities =
    {
        ("Mythical", 0.00025, new[] { ("(O)434", 1, 1), ("(O)74", 5, 5), ("(O)279", 3, 3) }),
        ("Legendary", 0.003, new[] { ("(O)74", 1, 1), ("(O)373", 1, 1), ("(O)896", 1, 1), ("(O)107", 1, 1), ("(O)125", 1, 1) }),
        ("Epic", 0.05, new[] { ("(O)72", 1, 1), ("(O)386", 2, 5), ("(O)337", 1, 2), ("(O)909", 1, 3), ("(O)852", 1, 1), ("(O)768", 2, 5), ("(O)769", 2, 5), ("(O)499", 1, 1) }),
        ("Rare", 0.15, new[] { ("(O)384", 2, 5), ("(O)336", 1, 2), ("(O)537", 1, 2), ("(O)749", 1, 1), ("(O)62", 1, 1), ("(O)70", 1, 1), ("(O)60", 1, 1), ("(O)64", 1, 1), ("(O)787", 1, 1), ("(O)430", 1, 2) }),
        ("Uncommon", 0.25, new[] { ("(O)378", 3, 8), ("(O)380", 2, 6), ("(O)80", 1, 3), ("(O)535", 1, 2), ("(O)536", 1, 2), ("(O)66", 1, 1), ("(O)68", 1, 1), ("(O)139", 1, 2), ("(O)286", 2, 4), ("(O)334", 1, 2) }),
        ("Common", 0.30, new[] { ("(O)390", 3, 10), ("(O)388", 3, 10), ("(O)771", 3, 8), ("(O)92", 2, 6), ("(O)330", 1, 4), ("(O)382", 1, 3), ("(O)770", 2, 6), ("(O)24", 1, 3), ("(O)192", 1, 3), ("(O)131", 1, 3), ("(O)129", 1, 3), ("(O)142", 1, 2) }),
    };

    private static readonly double TotalWeight = Rarities.Sum(r => r.Weight);

    /// <summary>A rarity's chance per loot roll.</summary>
    public static double Chance(string rarity) => Rarities.First(r => r.Name == rarity).Weight / TotalWeight * (1 - NothingChance);

    /// <summary>One loot roll: nothing 60%; else common ~15.9%, uncommon ~13.3%, rare ~8%, epic ~2.7%, legendary ~0.16%, mythical ~0.013%.</summary>
    public static (string Id, int Count, string Rarity)? RollLoot(Random rng)
    {
        double roll = rng.NextDouble(), edge = 0;
        foreach (var (name, weight, pool) in Rarities)
        {
            edge += weight / TotalWeight * (1 - NothingChance);
            if (roll >= edge)
                continue;
            var (id, min, max) = pool[rng.Next(pool.Length)];
            return (id, rng.Next(min, max + 1), name);
        }
        return null;
    }

    /// <summary>
    /// The guild's day: up to the hall's attempts, pushing deeper on every win. A loss ends the day (a lucky guild master
    /// gets one retry) and may injure members for a few days. Updates the stage, record and injuries; the caller pays out.
    /// </summary>
    public static List<RunLog> Run(GuildState g, int today, Random rng, double retry = 0, bool tough = false, double more = 1, double powerBonus = 0,
        double injuryCut = 0, int extraRuns = 0, double extraLoot = 0)
    {
        var logs = new List<RunLog>();
        bool retried = false, farming = false;
        for (int attempt = 0; attempt < Attempts(g.HallLevel) + extraRuns; attempt++)
        {
            double power = Power(g, today, powerBonus);
            if (power <= 0)
                break;
            int stage = farming ? FarmStage(power, g.Stage) : g.Stage;
            if (stage < 0)
                break; // nothing easy enough to farm
            if (rng.NextDouble() < WinChance(power, stage))
            {
                bool record = !farming && stage + 1 > g.Best;
                logs.Add(Clear(g, today, rng, stage, record, farming, more, extraLoot));
                if (!farming)
                {
                    g.Stage = stage + 1;
                    g.Best = Math.Max(g.Best, g.Stage);
                }
                continue;
            }
            if (farming)
            {
                logs.Add(new RunLog(Label(stage), false, 0, new(), new(), false, Farm: true)); // a bad farm run: home early, nobody hurt
                break;
            }
            if (!retried && rng.NextDouble() < retry)
            {
                retried = true; // talked the party into one more go
                attempt--;
                continue;
            }
            var hurt = new List<string>();
            if (!tough)
            {
                double chance = (Healthy(g, today).Any(m => m.Class == GuildClass.Priest) ? 0.15 : 0.30) * (1 - Math.Clamp(injuryCut, 0, 1));
                foreach (var m in Healthy(g, today))
                {
                    if (rng.NextDouble() < chance)
                    {
                        m.InjuredUntil = today + 1 + rng.Next(1, 4);
                        hurt.Add(m.Name);
                    }
                }
            }
            logs.Add(new RunLog(Label(stage), false, 0, new(), hurt, false));
            farming = true; // stuck: the rest of the day goes to farming a stage they can handle
        }
        return logs;
    }

    /// <summary>Where a stuck party farms: the deepest stage below the wall they win at least 90% of the time (-1: none).</summary>
    public static int FarmStage(double power, int stage)
    {
        for (int s = stage - 1; s >= 0; s--)
        {
            if (WinChance(power, s) >= 0.9)
                return s;
        }
        return -1;
    }

    /// <summary>A cleared stage pays: gold always, the loot rolls, and half the time 1-5 monster drops.</summary>
    private static RunLog Clear(GuildState g, int today, Random rng, int stage, bool record, bool farm, double more, double extraLoot)
    {
        int rolls = 1 + (Healthy(g, today).Any(m => m.Class == GuildClass.Rogue) && rng.NextDouble() < 0.25 ? 1 : 0) + (rng.NextDouble() < extraLoot ? 1 : 0);
        var loot = Enumerable.Range(0, rolls).Select(_ => RollLoot(rng)).OfType<(string, int, string)>().ToList();
        if (RollMonster(stage, rng) is { } drop)
            loot.Add(drop);
        long gold = RollGold(stage, rng, more) + (record ? (long)Math.Round(MaxGold(stage) * more) : 0); // a new record always pays
        return new RunLog(Label(stage), true, gold, loot, new(), record, farm);
    }

    /// <summary>What the monsters drop: half the time, 1-5 of something; nastier things deeper down.</summary>
    public const double MonsterChance = 0.5;

    private static readonly (int From, string[] Ids)[] MonsterDrops =
    {
        (0, new[] { "(O)766", "(O)767", "(O)684" }),                                   // slime, bat wing, bug meat
        (30, new[] { "(O)766", "(O)767", "(O)684", "(O)881", "(O)814" }),             // + bone fragment, squid ink
        (60, new[] { "(O)767", "(O)881", "(O)814", "(O)768", "(O)769" }),             // + solar and void essence
        (100, new[] { "(O)881", "(O)768", "(O)769", "(O)848" }),                      // + cinder shards
    };

    public static (string Id, int Count, string Rarity)? RollMonster(int stage, Random rng)
    {
        if (rng.NextDouble() >= MonsterChance)
            return null;
        var ids = MonsterDrops.Last(t => stage >= t.From).Ids;
        return (ids[rng.Next(ids.Length)], rng.Next(1, 6), "Monster");
    }

    // ---- the fleet: one stage a voyage, a week at sea ----

    public const int VoyageDays = 7, MinVoyageDays = 4, VoyageGold = 25, VoyageLootRolls = 5;
    public const double TreasureChance = 0.15;

    /// <summary>Days at sea, after Doc's Seafaring shaves some off.</summary>
    public static int Voyage(int fasterBy) => Math.Max(MinVoyageDays, VoyageDays - Math.Max(0, fasterBy));

    /// <summary>
    /// A voyage comes home: one shot at the fleet's next stage (a lucky captain gets a second go). A win pays 25x a dungeon
    /// clear (every win is a new record, so the record bonus too), five loot rolls, sea plunder, sometimes a treasure chest,
    /// and every crew member who sailed levels up. A loss pays nothing and can hurt the crew for 4-10 days.
    /// </summary>
    public static RunLog Sail(GuildState fleet, int today, Random rng, double retry = 0, bool tough = false, double more = 1, double powerBonus = 0,
        double injuryCut = 0, double extraLoot = 0)
    {
        int stage = fleet.Stage;
        double power = Power(fleet, today, powerBonus);
        bool win = rng.NextDouble() < WinChance(power, stage) || (rng.NextDouble() < retry && rng.NextDouble() < WinChance(power, stage));
        var crew = Healthy(fleet, today);
        if (win)
        {
            int rolls = VoyageLootRolls + (crew.Any(m => m.Class == GuildClass.Rogue) && rng.NextDouble() < 0.25 ? 1 : 0) + (rng.NextDouble() < extraLoot ? 1 : 0);
            var loot = Enumerable.Range(0, rolls).Select(_ => RollLoot(rng)).OfType<(string, int, string)>().ToList();
            loot.Add(RollSea(stage, rng));
            loot.Add(RollSea(stage, rng));
            if (rng.NextDouble() < TreasureChance)
                loot.Add((rng.NextDouble() < 0.5 ? "(O)166" : "(O)797", 1, "Treasure")); // a treasure chest or a pearl
            long gold = (long)Math.Round((RollGold(stage, rng, 1) + MaxGold(stage)) * VoyageGold * more);
            foreach (var m in crew.Where(m => m.Level < MaxLevel))
                m.Level++; // sea legs: a week at sea is worth a level
            fleet.Stage = stage + 1;
            fleet.Best = Math.Max(fleet.Best, fleet.Stage);
            return new RunLog(Label(stage), true, gold, loot, new(), true);
        }
        var hurt = new List<string>();
        if (!tough)
        {
            double chance = (crew.Any(m => m.Class == GuildClass.Priest) ? 0.20 : 0.40) * (1 - Math.Clamp(injuryCut, 0, 1));
            foreach (var m in crew.Where(_ => rng.NextDouble() < chance))
            {
                m.InjuredUntil = today + rng.Next(4, 11);
                hurt.Add(m.Name);
            }
        }
        return new RunLog(Label(stage), false, 0, new(), hurt, false);
    }

    private static readonly (int From, string[] Ids)[] SeaDrops =
    {
        (0, new[] { "(O)393", "(O)397", "(O)392", "(O)394", "(O)372" }),                 // coral, urchin, nautilus, rainbow shell, clam
        (30, new[] { "(O)393", "(O)397", "(O)392", "(O)394", "(O)814", "(O)SeaJelly" }), // + squid ink, sea jelly
        (60, new[] { "(O)392", "(O)394", "(O)814", "(O)SeaJelly", "(O)MysteryBox" }),    // + mystery boxes
        (100, new[] { "(O)394", "(O)814", "(O)MysteryBox", "(O)GoldenMysteryBox" }),     // + golden mystery boxes
    };

    /// <summary>Plunder from the sea itself: 1-5 of something, better the farther out.</summary>
    public static (string Id, int Count, string Rarity) RollSea(int stage, Random rng)
    {
        var ids = SeaDrops.Last(t => stage >= t.From).Ids;
        return (ids[rng.Next(ids.Length)], rng.Next(1, 6), "Sea");
    }
}
