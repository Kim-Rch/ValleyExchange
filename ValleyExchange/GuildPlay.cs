using StardewValley;

namespace ValleyExchange;

/// <summary>
/// Game side of the guild and the pirate fleet (same rules, different flags): hiring and upgrading the party or crew, the hall
/// or ship, and paying out the dungeon runs each evening or the voyage when the ship comes home.
/// </summary>
internal static class GuildPlay
{
    public static GuildState Of(MarketState state, bool fleet) => fleet ? state.Fleet : state.Guild;
    public static Role Leader(bool fleet) => fleet ? Role.Pirate : Role.Adventurer;

    /// <summary>A day count (days since the save began) as a calendar date: "Summer 10", plus the year after the first.</summary>
    public static string Date(int totalDays)
    {
        var date = new WorldDate { TotalDays = totalDays };
        return $"{Utility.capitalizeFirstLetter(date.SeasonKey)} {date.DayOfMonth}{(date.Year > Game1.year ? $", year {date.Year}" : "")}";
    }

    private static readonly string[] Pirates =
    {
        "Bilge", "Scurvy", "Cutlass", "Hook", "Peg", "Squid", "Anchor", "Rumpot", "Plank", "Gull", "Kraken", "Powder", "Mast", "Brine", "Keel",
        "Patch", "Davy", "Rigger", "Tar", "Cove", "Doubloon", "Gale", "Bones", "Mutiny", "Chum", "Barrel", "Crowsnest", "Starboard", "Loot", "Swab",
    };

    /// <summary>The fleet can only be changed in port. Null when it's fine to go ahead.</summary>
    private static string? AtSea(GuildState g, bool fleet) =>
        fleet && g.AtSea ? $"The ship's at sea till {Date(g.ReturnDay)}. Wait for port." : null;
    private static readonly string[] Recruits =
    {
        "Grim", "Bolt", "Tusk", "Gravel", "Pike", "Mace", "Dirk", "Brick", "Sparks", "Hymn", "Ruckus", "Fang", "Quill", "Shiv", "Lark",
        "Banjo", "Clank", "Tater", "Bristle", "Rook", "Crag", "Moxie", "Pip", "Gus", "Thimble", "Ironjaw", "Ditty", "Haggis", "Boomer", "Nettle",
    };

    /// <summary>A party member is out by the guild hall now: the guild's open, they're not laid up, and it's daytime.</summary>
    public static bool HeroAt(string name, GameLocation location) =>
        Workers.State is { } state && Game1.timeOfDay is >= 600 and < 2200
        && (location.Name == "Mine" && Market.IsHired(state, Role.Adventurer) && state.Guild.Members.Any(m => m.Name == name && !m.Injured(Game1.Date.TotalDays))
            || location.Name == "Beach" && Market.IsHired(state, Role.Pirate) && !state.Fleet.AtSea && state.Fleet.Members.Any(m => m.Name == name && !m.Injured(Game1.Date.TotalDays)));

    /// <summary>Little scenes between the captain (true) and one of his pirates (false), in port.</summary>
    public static List<(bool Gm, string Text)> SeaBanter(string captain, string pirate, string cls, string stage)
    {
        var scenes = new List<(bool, string)>[]
        {
            new() { (true, $"Swab the deck, {pirate}!"), (false, "We're ON LAND, cap'n."), (true, "Swab it anyway.") },
            new() { (false, "Cap'n, why's the flag got a beard on it?"), (true, "'Cause WE got beards, ya landlubber.") },
            new() { (true, $"Next stop: {stage}!"), (false, "Is that a place?"), (true, "It is now.") },
            new() { (false, "Saw a Junimo on the dock."), (true, "Make it walk the plank."), (false, "It floated, cap'n."), (true, "...Course it did.") },
            new() { (true, "What do we want?"), (false, "PLUNDER!"), (true, "When do we want it?"), (false, "After lunch!") },
            new() { (false, "Ah get seasick, cap'n."), (true, $"You're the {cls.ToLower()}, {pirate}!"), (false, "Ah get seasick AT that too.") },
            new() { (true, "A pirate's life for me!"), (false, "Ah thought we were ranchers."), (true, "Roy's a rancher. WE are PIRATES.") },
            new() { (false, "Where's the rum?"), (true, "Why's the rum always gone?"), (false, "...Ah plead the fifth.") },
            new() { (true, $"{pirate}, ya ever seen the Kraken?"), (false, "Saw Willy's cousin. Close enough.") },
            new() { (false, "Cap'n, the map's upside down."), (true, "Then we're sailin' upside down. Hush.") },
            new() { (true, "Ah lost this eye to a shark."), (false, "Ya got both eyes, cap'n."), (true, "It grew back. Pirate eyes do.") },
            new() { (false, "Can Ah have a parrot?"), (true, "Ya can have a chicken."), (false, "...Can it talk?"), (true, "It can SCREAM.") },
            new() { (true, $"{captain}'s rule one: no Junimos aboard."), (false, "What's rule two?"), (true, "SEE RULE ONE.") },
        };
        return scenes[Game1.random.Next(scenes.Length)];
    }

    /// <summary>Little scenes between the Guild Master (true) and one of his heroes (false).</summary>
    public static List<(bool Gm, string Text)> Banter(string gm, string hero, string cls, string stage)
    {
        var scenes = new List<(bool, string)>[]
        {
            new() { (true, $"Alright {hero}, battle plan?"), (false, "Hit it till it stops movin'."), (true, "...Ah love this guild.") },
            new() { (false, "Boss, why do Ah always go in first?"), (true, $"'Cause you're the {cls}, {hero}."), (false, "Ah hate this job.") },
            new() { (true, "Who ate mah jerky?"), (false, "A Junimo."), (true, "DAMN Junimos!") },
            new() { (false, $"{gm}, you ever really kill a dragon?"), (true, "Twice. Before breakfast."), (false, "Sure ya did.") },
            new() { (true, $"Stage {stage} today, y'all!"), (false, "We're gonna die."), (true, "That's the spirit!") },
            new() { (false, "Ah need a raise."), (true, "Ah need a nap. Life's tough.") },
            new() { (true, "Remember: no coin, no..."), (false, "...TOIL!"), (true, "Damn right.") },
            new() { (false, "Saw a slime wearin' a hat."), (true, "Was it a nice hat?"), (false, "Nicer'n yours.") },
            new() { (true, $"{hero}, polish mah sword."), (false, "Polish it yerself."), (true, "...Fair.") },
            new() { (true, "Ah once punted a Junimo into next week."), (false, "That's illegal, boss."), (true, "Not in the Hollow it ain't.") },
            new() { (false, "What's the plan if we lose?"), (true, "We don't lose."), (false, "We lost Tuesday."), (true, "Tuesday don't count.") },
            new() { (true, "Who's ready to get rich?"), (false, "ME!"), (true, "Wrong. ME. You get wages.") },
            new() { (false, "This cave smells like feet."), (true, $"That's the {cls.ToLower()}."), (false, "Ah AM the {cls.ToLower()}!") },
            new() { (true, "Legend says there's a Stardrop down there."), (false, "Legend says a lot of crap, boss.") },
            new() { (false, "Can we take a day off?"), (true, "Sure. Tomorrow."), (false, "You said that yesterday."), (true, "And Ah meant it then too.") },
            new() { (true, $"{hero}, you scared?"), (false, "No."), (true, "Your knees are knockin'."), (false, "That's... a dance.") },
        };
        return scenes[Game1.random.Next(scenes.Length)];
    }

    /// <summary>Talking to the Guild Master: his report, and the guild ledger.</summary>
    public static void Talk(MarketState state, string name)
    {
        var g = state.Guild;
        string report = Staff.Reports.TryGetValue(Role.Adventurer, out var r) ? r
            : g.Members.Count == 0 ? "Guild's open for business, boss. Need heroes though." : $"Party's ready. Next up: {Guild.Label(g.Stage)}.";
        Personas.Ask(name, Role.Adventurer, $"{name}: \"{report}\"", new[] { new Response("ledger", "Open the guild ledger") },
            _ => WhenFree.Run(() => Game1.activeClickableMenu = new GuildMenu(state)));
    }

    public static string Hire(MarketState state, GuildClass cls, bool fleet = false)
    {
        var g = Of(state, fleet);
        var def = Guild.Def(cls, fleet);
        if (!Market.IsHired(state, Leader(fleet)))
            return fleet ? "Hire a Pirate Captain first." : "Hire a Guild Master first.";
        if (AtSea(g, fleet) is { } away)
            return away;
        if (g.Members.Count >= Guild.Slots(g.HallLevel))
            return fleet ? $"The ship only has {Guild.Slots(g.HallLevel)} berths. Refit it for more." : $"The hall only fits {Guild.Slots(g.HallLevel)}. Upgrade it for more.";
        if (Game1.player.Money < def.Hire)
            return $"A {def.Title} costs {def.Hire:N0}g to sign.";
        var taken = new HashSet<string>(state.Guild.Members.Concat(state.Fleet.Members).Select(m => m.Name).Concat(Market.Roles.SelectMany(r => r.Names)));
        string name = (fleet ? Pirates : Recruits).FirstOrDefault(n => !taken.Contains(n)) ?? $"Recruit {g.Members.Count + 1}";
        Game1.player.Money -= def.Hire;
        CrewReport.Paid(Leader(fleet), def.Hire);
        g.Members.Add(new GuildMember { Name = name, Class = cls });
        return $"{name} the {def.Title} signed on! ({Personas.Of(name).Kind})";
    }

    private static double R(MarketState state, Effect effect) => Research.Total(state, effect);

    /// <summary>Doc's Dungeoneering research on top of the party's own power.</summary>
    public static double PowerBonus(MarketState state, bool fleet = false) => R(state, fleet ? Effect.FleetPower : Effect.GuildPower);

    /// <summary>Training cost after Doc's Combat Drills.</summary>
    public static long TrainCost(MarketState state, GuildMember m, bool fleet = false) =>
        (long)Math.Ceiling(Guild.LevelCost(m.Level) * (1 - (fleet ? 0 : Math.Min(0.75, R(state, Effect.GuildTraining)))));

    public static string LevelUp(MarketState state, GuildMember m, bool fleet = false)
    {
        if (AtSea(Of(state, fleet), fleet) is { } away)
            return away;
        if (m.Level >= Guild.MaxLevel)
            return $"{m.Name} is maxed out.";
        long cost = TrainCost(state, m, fleet);
        if (Game1.player.Money < cost)
            return $"Training costs {cost:N0}g.";
        Game1.player.Money -= (int)cost;
        CrewReport.Paid(Leader(fleet), cost);
        m.Level++;
        return $"{m.Name} trained up to level {m.Level}!";
    }

    public static string Forge(MarketState state, GuildMember m, bool fleet = false)
    {
        if (AtSea(Of(state, fleet), fleet) is { } away)
            return away;
        if (m.Gear >= Guild.MaxGear)
            return $"{m.Name}'s gear is the best there is.";
        var (id, count) = Guild.GearItem(m.Gear);
        long gold = Guild.GearGold(m.Gear);
        string item = ItemRegistry.GetDataOrErrorItem(id).DisplayName;
        if (Crate.Count(id) < count)
            return $"Forging needs {count} {item} in the crate.";
        if (Game1.player.Money < gold)
            return $"Forging costs {gold:N0}g too.";
        Crate.Take(id, count);
        Game1.player.Money -= (int)gold;
        CrewReport.Paid(Leader(fleet), gold);
        m.Gear++;
        return $"{m.Name}'s new gear: tier {m.Gear}!";
    }

    public static string Promote(MarketState state, GuildMember m, bool fleet = false)
    {
        var def = Guild.Def(m.Class, fleet);
        if (AtSea(Of(state, fleet), fleet) is { } away)
            return away;
        if (m.Promoted)
            return $"{m.Name} is already a {def.Promoted}.";
        if (m.Level < Guild.PromoteLevel)
            return $"Promotion to {def.Promoted} at level {Guild.PromoteLevel}.";
        if (Crate.Count(Guild.PromoteItem) < 1 || Game1.player.Money < Guild.PromoteGold)
            return $"Promotion costs {Guild.PromoteGold:N0}g and a Diamond from the crate.";
        Crate.Take(Guild.PromoteItem, 1);
        Game1.player.Money -= Guild.PromoteGold;
        CrewReport.Paid(Leader(fleet), Guild.PromoteGold);
        m.Promoted = true;
        return $"{m.Name} is now a {def.Promoted}!";
    }

    public static string UpgradeHall(MarketState state, bool fleet = false)
    {
        var g = Of(state, fleet);
        if (AtSea(g, fleet) is { } away)
            return away;
        if (g.HallLevel >= Guild.MaxHall)
            return fleet ? "The ship's as grand as it gets." : "The hall's as grand as it gets.";
        long cost = Guild.HallCost(g.HallLevel);
        if (Game1.player.Money < cost)
            return fleet ? $"Refitting the ship costs {cost:N0}g." : $"The next hall costs {cost:N0}g.";
        Game1.player.Money -= (int)Math.Min(cost, int.MaxValue);
        CrewReport.Paid(Leader(fleet), cost);
        g.HallLevel++;
        return fleet ? $"Ship level {g.HallLevel}: {Guild.Slots(g.HallLevel)} berths, +{g.HallLevel * 5}% crew power."
            : $"Guild Hall level {g.HallLevel}: {Guild.Slots(g.HallLevel)} party slots, {Guild.Attempts(g.HallLevel)} runs a day.";
    }

    /// <summary>Let a member go (not while the ship's out). Null when done, else why not.</summary>
    public static string? Dismiss(MarketState state, GuildMember m, bool fleet = false)
    {
        if (AtSea(Of(state, fleet), fleet) is { } away)
            return away;
        Of(state, fleet).Members.Remove(m);
        return null;
    }

    /// <summary>Talking to the Pirate Captain: his report, and the ship's log.</summary>
    public static void CaptainTalk(MarketState state, string name)
    {
        var f = state.Fleet;
        int today = Game1.Date.TotalDays;
        string report = f.AtSea ? $"Crew's at sea, boss. Back with the haul on {Date(f.ReturnDay)} ({f.ReturnDay - today} day(s))."
            : Staff.Reports.TryGetValue(Role.Pirate, out var r) ? r
            : f.Members.Count == 0 ? "Got a ship, boss. Need a crew though." : $"In port. We sail at 6pm for {Guild.Label(f.Stage)}.";
        Personas.Ask(name, Role.Pirate, $"{name}: \"{report}\"", new[] { new Response("log", "Open the ship's log") },
            _ => WhenFree.Run(() => Game1.activeClickableMenu = new GuildMenu(state, fleet: true)));
    }

    /// <summary>
    /// 6pm for the fleet: a ship that's due comes home and pays out; a ship that's had a day in port sails for its next stage.
    /// Returns the mood lines for homecoming (none on days at sea).
    /// </summary>
    public static List<string> FleetEvening(MarketState state, int today, Random rng)
    {
        var f = state.Fleet;
        if (f.AtSea)
        {
            if (today < f.ReturnDay)
                return new();
            var run = Guild.Sail(f, today, rng,
                retry: CrewSkills.Total(Role.Pirate, PerkKind.Luck),
                tough: CrewSkills.Has(Role.Pirate, PerkKind.Tough),
                more: CrewSkills.More(Role.Pirate),
                powerBonus: PowerBonus(state, fleet: true),
                injuryCut: R(state, Effect.FleetInjury),
                extraLoot: R(state, Effect.FleetLoot));
            (f.SailDay, f.LastPort) = (-1, today);
            Payout(f, today, run, Role.Pirate, "fleet");
            CrewReport.Count(Role.Pirate, run.Win ? "voyages won" : "voyages lost");
            CrewSkills.Work(Role.Pirate, run.Win ? 40 : 10);
            Game1.addHUDMessage(run.Win
                ? new HUDMessage($"The ship's home from {run.Stage} with {run.Gold:N0}g and a hold full of plunder!", HUDMessage.achievement_type)
                : new HUDMessage($"The ship limped home from {run.Stage} empty-handed.", HUDMessage.error_type));
            Staff.Reports[Role.Pirate] = run.Win
                ? $"Took {run.Stage}! {run.Gold:N0}g for you, loot's in the crate, and the crew's a level wiser. Next: {Guild.Label(f.Stage)}."
                : $"Lost at {run.Stage}.{(run.Injured.Count > 0 ? $" {string.Join(", ", run.Injured)} got hurt." : "")} We'll try again.";
            return new() { run.Win ? "Land ho! We're RICH, boss!" : "Sea beat us this time, boss." };
        }
        if (f.LastPort < today && Guild.Healthy(f, today).Count > 0)
        {
            int days = Guild.Voyage((int)R(state, Effect.FleetSpeed));
            (f.SailDay, f.ReturnDay) = (today, today + days);
            f.Log.Insert(0, $"{Date(today)}  Sailed for {Guild.Label(f.Stage)}, back {Date(f.ReturnDay)}");
            CrewReport.Count(Role.Pirate, "voyages started");
            Staff.Reports[Role.Pirate] = $"Anchors aweigh! Sailin' for {Guild.Label(f.Stage)}. Back on {Date(f.ReturnDay)}.";
        }
        return new();
    }

    /// <summary>One run's payout: gold to you, loot to the crate (a fanfare for the good stuff), a log line and a report detail.</summary>
    private static long Payout(GuildState g, int today, RunLog run, Role role, string who)
    {
        foreach (var (id, count, rarity) in run.Loot)
        {
            if (rarity is "Epic" or "Legendary" or "Mythical" or "Treasure")
            {
                Game1.addHUDMessage(new HUDMessage($"{rarity} loot! The {who} found {ItemRegistry.GetDataOrErrorItem(id).DisplayName} x{count}!", HUDMessage.achievement_type));
                Game1.playSound(rarity == "Epic" ? "discoverMineral" : "reward");
            }
            var item = ItemRegistry.Create(id, count);
            CrewReport.Got(role, "loot", item);
            Crate.Store(item);
        }
        string loot = run.Loot.Count == 0 ? "no loot" : string.Join(", ", run.Loot.Select(l => $"[{l.Rarity}] {ItemRegistry.GetDataOrErrorItem(l.Id).DisplayName} x{l.Count}"));
        string where = run.Farm ? $"{run.Stage} (farm)" : run.Stage;
        string line = run.Win
            ? $"{where}: WIN{(run.Record ? " (record!)" : "")} {loot}"
            : $"{where}: LOST{(run.Injured.Count > 0 ? $", hurt: {string.Join(", ", run.Injured)}" : "")}";
        CrewReport.Detail(role, line, run.Gold);
        g.Log.Insert(0, $"{Date(today)}  {line}{(run.Gold > 0 ? $" +{run.Gold:N0}g" : "")}");
        g.Log.RemoveRange(Math.Min(g.Log.Count, 30), Math.Max(0, g.Log.Count - 30));
        Game1.player.Money += (int)Math.Min(run.Gold, int.MaxValue - Game1.player.Money);
        CrewReport.Earned(role, run.Gold);
        return run.Gold;
    }

    /// <summary>6pm: the guild's runs. Gold to you, loot to the crate, the log and the report. Returns the mood lines for homecoming.</summary>
    public static List<string> Evening(MarketState state, int today, Random rng)
    {
        var g = state.Guild;
        var logs = Guild.Run(g, today, rng,
            retry: CrewSkills.Total(Role.Adventurer, PerkKind.Luck),
            tough: CrewSkills.Has(Role.Adventurer, PerkKind.Tough),
            more: CrewSkills.More(Role.Adventurer) * (1 + R(state, Effect.GuildGold)),
            powerBonus: PowerBonus(state),
            injuryCut: R(state, Effect.GuildInjury),
            extraRuns: (int)R(state, Effect.GuildRuns),
            extraLoot: R(state, Effect.GuildLoot));
        if (logs.Count == 0)
        {
            Staff.Reports[Role.Adventurer] = g.Members.Count == 0 ? "No party, no dungeon, boss. Hire some heroes." : "Whole party's laid up. No runs today.";
            return new() { "Quiet day at the guild." };
        }
        long gold = logs.Sum(run => Payout(g, today, run, Role.Adventurer, "guild"));
        int wins = logs.Count(l => l.Win && !l.Farm), farmed = logs.Count(l => l.Win && l.Farm);
        CrewReport.Count(Role.Adventurer, "stages cleared", wins);
        CrewReport.Count(Role.Adventurer, "farm runs", farmed);
        CrewReport.Count(Role.Adventurer, "runs lost", logs.Count(l => !l.Win));
        CrewSkills.Work(Role.Adventurer, 5 * wins + 2);
        if (logs.Any(l => l.Record))
            Game1.addHUDMessage(new HUDMessage($"Guild record: they cleared {logs.Last(l => l.Record).Stage}!", HUDMessage.achievement_type));
        var stopped = logs.FirstOrDefault(l => !l.Win && !l.Farm);
        Staff.Reports[Role.Adventurer] = $"Cleared {wins} new stage(s){(farmed > 0 ? $", farmed {farmed} more" : "")}, {gold:N0}g for you. Next up: {Guild.Label(g.Stage)}."
            + (stopped != null ? $" Got stopped at {stopped.Stage}." : "");
        return new()
        {
            stopped == null ? "Clean sweep, boss!" : wins > 0 ? $"Cleared {wins}, then it got ugly." : farmed > 0 ? "Hit a wall. Farmed the easy floors instead." : "Got our tails kicked. We'll be back.",
        };
    }
}
