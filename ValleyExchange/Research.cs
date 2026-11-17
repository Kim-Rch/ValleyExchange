using System;
using System.Collections.Generic;
using System.Linq;

namespace ValleyExchange;

/// <summary>What a finished research project changes. Crew effects fold into the skill totals; the rest hook the game.</summary>
public enum Effect
{
    DoubleForage, ForagerHaul,
    DoubleHarvest, SelfWater, NoCrows,
    ExtraOre, StoneCoal, MinerHaul,
    CrewWage, CrewSpeed, CrewOutput,
    DoubleFish, FisherHaul,
    SmithOutput, SmithSpeed, FastToolUpgrade,
    ArtisanSpeed, ArtisanQuality, MachineSpeed,
    OilYield, SpillSafety,
    ShippingBonus, SalesMarkup, Interest,
    ExtraBeds, CourierDeliveries, PlayerSpeed,
    DailyLuck, RainChance,
    MaxStamina, NoonStamina, AdventurerLuck,
    AnimalFriendship, RanchOutput, RanchQuality,
    ToolEnergy, UpgradeDiscount, ArtisanStar, DoubleArtisan,
    GiantCrops, FruitTreeGrowth, WildTreeGrowth,
    DoubleDig, ArtifactSpots, SampleBonus,
    FoodRestore, BuffDuration, ChefOutput, ChefQuality,
    GuildPower, GuildInjury, GuildGold, GuildTraining, GuildRuns, GuildLoot,
    FleetPower, FleetInjury, FleetGold, FleetSpeed, FleetLoot,
}

/// <summary>One project in a research tree. Tiers go in order: tier N needs tier N-1 of the same tree.</summary>
public sealed record ResearchNode(string Tree, int Tier, string Name, Effect Effect, double Amount)
{
    public string Id => $"{this.Tree}/{this.Tier}";
    public int Points => 15 * this.Tier + 5 * this.Tier * this.Tier; // 20 ... 440
    public int Gold => 500 * this.Tier * this.Tier;                  // 500 ... 32,000
    public int Days => 1 + (this.Tier - 1) / 2;                     // 1 ... 4

    public string Text => this.Effect switch
    {
        Effect.DoubleForage => $"+{Skills.Pct(this.Amount)} chance of double forage (you & the crew)",
        Effect.ForagerHaul => $"+{Skills.Pct(this.Amount)} forager haul",
        Effect.DoubleHarvest => $"+{Skills.Pct(this.Amount)} chance of a double crop harvest (you & the crew)",
        Effect.SelfWater => $"+{Skills.Pct(this.Amount)} of dry crops water themselves each morning",
        Effect.NoCrows => "Crows never touch your crops again",
        Effect.ExtraOre => $"+{Skills.Pct(this.Amount)} chance of extra ore from ore nodes",
        Effect.StoneCoal => $"+{Skills.Pct(this.Amount)} chance of coal from any stone",
        Effect.MinerHaul => $"+{Skills.Pct(this.Amount)} miner haul",
        Effect.CrewWage => $"-{Skills.Pct(this.Amount)} wages for the whole crew",
        Effect.CrewSpeed => $"+{Skills.Pct(this.Amount)} work speed for the whole crew",
        Effect.DoubleFish => $"+{Skills.Pct(this.Amount)} chance of catching two fish at once",
        Effect.FisherHaul => $"+{Skills.Pct(this.Amount)} fisherman and crab catcher haul",
        Effect.SmithOutput => $"+{Skills.Pct(this.Amount)} blacksmith output (free smelts, more geodes)",
        Effect.SmithSpeed => $"+{Skills.Pct(this.Amount)} blacksmith furnace speed",
        Effect.FastToolUpgrade => $"+{Skills.Pct(this.Amount)} chance each morning Clint finishes your tool a day early",
        Effect.ArtisanSpeed => $"+{Skills.Pct(this.Amount)} brewer and artisan speed",
        Effect.ArtisanQuality => $"+{Skills.Pct(this.Amount)} chance of a better star on brews and artisan goods",
        Effect.MachineSpeed => $"Every machine in the valley loses {Skills.Pct(this.Amount)} more of its wait each morning",
        Effect.OilYield => $"+{Skills.Pct(this.Amount)} crude from your pumpjacks",
        Effect.SpillSafety => $"-{Skills.Pct(this.Amount)} oil spill chance",
        Effect.ShippingBonus => $"+{Skills.Pct(this.Amount)} on everything you ship",
        Effect.SalesMarkup => $"+{Skills.Pct(this.Amount)} salesman markup",
        Effect.Interest => $"+{Skills.Pct(this.Amount)} savings interest",
        Effect.ExtraBeds => $"+{this.Amount:0} crew bed(s) in every cabin",
        Effect.CourierDeliveries => $"+{Skills.Pct(this.Amount)} courier deliveries",
        Effect.PlayerSpeed => $"+{this.Amount:0} walking speed for you, every day",
        Effect.DailyLuck => $"+{Skills.Pct(this.Amount)} daily luck (mines, geodes, everything)",
        Effect.RainChance => $"+{Skills.Pct(this.Amount)} chance a sunny tomorrow turns to rain",
        Effect.MaxStamina => $"+{this.Amount:0} max energy for you",
        Effect.NoonStamina => $"Get back {Skills.Pct(this.Amount)} of your energy at noon",
        Effect.AdventurerLuck => $"{Skills.Pct(this.Amount)} chance the guild gets a second go at a lost run",
        Effect.AnimalFriendship => $"+{this.Amount:0} friendship with every farm animal each morning",
        Effect.RanchOutput => $"+{Skills.Pct(this.Amount)} ranch hand produce",
        Effect.RanchQuality => $"+{Skills.Pct(this.Amount)} chance of a better star on ranch produce",
        Effect.ToolEnergy => $"Hoe, axe, pickaxe and watering can cost {Skills.Pct(this.Amount)} less energy",
        Effect.UpgradeDiscount => $"-{Skills.Pct(this.Amount)} on Clint's tool upgrades",
        Effect.ArtisanStar => $"+{Skills.Pct(this.Amount)} chance any artisan good comes out a star better (yours too)",
        Effect.DoubleArtisan => $"+{Skills.Pct(this.Amount)} chance any artisan machine makes two",
        Effect.GiantCrops => $"+{Skills.Pct(this.Amount)} giant crop chance",
        Effect.FruitTreeGrowth => $"+{Skills.Pct(this.Amount)} chance a day fruit trees grow (and age to better fruit) twice as fast",
        Effect.WildTreeGrowth => $"+{Skills.Pct(this.Amount)} chance a day wild trees grow an extra stage",
        Effect.DoubleDig => $"+{Skills.Pct(this.Amount)} chance an artifact spot gives double loot",
        Effect.ArtifactSpots => $"+{this.Amount:0} extra artifact spots around the valley each morning",
        Effect.SampleBonus => $"+{Skills.Pct(this.Amount)} research points from donated samples",
        Effect.FoodRestore => $"Food and drinks restore {Skills.Pct(this.Amount)} more energy and health",
        Effect.BuffDuration => $"Food and drink buffs last {Skills.Pct(this.Amount)} longer",
        Effect.ChefOutput => $"+{Skills.Pct(this.Amount)} chef dishes",
        Effect.ChefQuality => $"+{Skills.Pct(this.Amount)} chance of a better star on the chef's dishes",
        Effect.GuildPower => $"+{Skills.Pct(this.Amount)} guild party power",
        Effect.GuildInjury => $"-{Skills.Pct(this.Amount)} injuries when the guild loses a run",
        Effect.GuildGold => $"+{Skills.Pct(this.Amount)} dungeon gold",
        Effect.GuildTraining => $"-{Skills.Pct(this.Amount)} on training guild members",
        Effect.GuildRuns => $"+{this.Amount:0} dungeon run(s) a day",
        Effect.GuildLoot => $"+{Skills.Pct(this.Amount)} chance of an extra loot drop per cleared stage",
        Effect.FleetPower => $"+{Skills.Pct(this.Amount)} pirate crew power",
        Effect.FleetInjury => $"-{Skills.Pct(this.Amount)} chance of crew getting hurt on a lost voyage",
        Effect.FleetGold => $"+{Skills.Pct(this.Amount)} voyage gold",
        Effect.FleetSpeed => $"Voyages take {this.Amount:0} day(s) less (4 days at the shortest)",
        Effect.FleetLoot => $"+{Skills.Pct(this.Amount)} chance of an extra loot roll per voyage",
        _ => $"+{Skills.Pct(this.Amount)} output for the whole crew",
    };
}

/// <summary>
/// The scientist's research trees. He earns research points every day (more for spare museum samples); you spend points and
/// gold on a project, it takes a few days, and the effect is permanent for you and the Brawnies.
/// </summary>
public static class Research
{
    public const int DailyPoints = 25;

    private static ResearchNode N(string tree, int tier, string name, Effect effect, double amount) => new(tree, tier, name, effect, amount);

    public static readonly IReadOnlyList<ResearchNode> Nodes = new[]
    {
        N("Botany", 1, "Field Guide", Effect.DoubleForage, 0.05),
        N("Botany", 2, "Spore Prints", Effect.ForagerHaul, 0.10),
        N("Botany", 3, "Root Study", Effect.DoubleForage, 0.05),
        N("Botany", 4, "Seasonal Charts", Effect.ForagerHaul, 0.10),
        N("Botany", 5, "Mycology", Effect.DoubleForage, 0.05),
        N("Botany", 6, "Canopy Survey", Effect.ForagerHaul, 0.15),
        N("Botany", 7, "Herbarium", Effect.DoubleForage, 0.10),
        N("Botany", 8, "Master Botanist", Effect.ForagerHaul, 0.20),

        N("Agronomy", 1, "Soil Samples", Effect.DoubleHarvest, 0.03),
        N("Agronomy", 2, "Moisture Study", Effect.SelfWater, 0.10),
        N("Agronomy", 3, "Crop Rotation", Effect.DoubleHarvest, 0.04),
        N("Agronomy", 4, "Scarecrow Theory", Effect.NoCrows, 1),
        N("Agronomy", 5, "Capillary Soil", Effect.SelfWater, 0.15),
        N("Agronomy", 6, "Hybrid Vigor", Effect.DoubleHarvest, 0.05),
        N("Agronomy", 7, "Deep Irrigation", Effect.SelfWater, 0.25),
        N("Agronomy", 8, "Golden Harvest", Effect.DoubleHarvest, 0.08),

        N("Geology", 1, "Rock Samples", Effect.ExtraOre, 0.05),
        N("Geology", 2, "Core Drilling", Effect.MinerHaul, 0.10),
        N("Geology", 3, "Vein Mapping", Effect.ExtraOre, 0.05),
        N("Geology", 4, "Coal Seams", Effect.StoneCoal, 0.05),
        N("Geology", 5, "Crystal Lattice", Effect.ExtraOre, 0.10),
        N("Geology", 6, "Deep Survey", Effect.MinerHaul, 0.20),
        N("Geology", 7, "Seismic Reading", Effect.StoneCoal, 0.10),
        N("Geology", 8, "Mother Lode", Effect.ExtraOre, 0.15),

        N("Brawnie Studies", 1, "Brawnie Anthropology", Effect.CrewWage, 0.03),
        N("Brawnie Studies", 2, "Ergonomics", Effect.CrewSpeed, 0.05),
        N("Brawnie Studies", 3, "Union Relations", Effect.CrewWage, 0.05),
        N("Brawnie Studies", 4, "Better Boots", Effect.CrewSpeed, 0.05),
        N("Brawnie Studies", 5, "Morale Studies", Effect.CrewOutput, 0.05),
        N("Brawnie Studies", 6, "Collective Bargaining", Effect.CrewWage, 0.07),
        N("Brawnie Studies", 7, "Time and Motion", Effect.CrewSpeed, 0.10),
        N("Brawnie Studies", 8, "The Brawnie Codex", Effect.CrewOutput, 0.10),

        N("Ichthyology", 1, "Tide Tables", Effect.DoubleFish, 0.05),
        N("Ichthyology", 2, "Gill Studies", Effect.FisherHaul, 0.10),
        N("Ichthyology", 3, "Lure Chemistry", Effect.DoubleFish, 0.05),
        N("Ichthyology", 4, "Current Maps", Effect.FisherHaul, 0.10),
        N("Ichthyology", 5, "Schooling Theory", Effect.DoubleFish, 0.05),
        N("Ichthyology", 6, "Sonar", Effect.FisherHaul, 0.15),
        N("Ichthyology", 7, "Spawning Grounds", Effect.DoubleFish, 0.10),
        N("Ichthyology", 8, "Master Angler", Effect.FisherHaul, 0.20),

        N("Metallurgy", 1, "Ore Assay", Effect.SmithOutput, 0.10),
        N("Metallurgy", 2, "Bellows Theory", Effect.SmithSpeed, 0.10),
        N("Metallurgy", 3, "Precision Tools", Effect.FastToolUpgrade, 0.25),
        N("Metallurgy", 4, "Flux Chemistry", Effect.SmithOutput, 0.15),
        N("Metallurgy", 5, "Blast Furnace", Effect.SmithSpeed, 0.15),
        N("Metallurgy", 6, "Clint's Apprentice", Effect.FastToolUpgrade, 0.25),
        N("Metallurgy", 7, "Alloy Science", Effect.SmithOutput, 0.20),
        N("Metallurgy", 8, "Dwarven Steel", Effect.FastToolUpgrade, 0.50),

        N("Chemistry", 1, "Yeast Cultures", Effect.ArtisanSpeed, 0.05),
        N("Chemistry", 2, "Catalysts", Effect.MachineSpeed, 0.05),
        N("Chemistry", 3, "Aging Science", Effect.ArtisanQuality, 0.05),
        N("Chemistry", 4, "Enzymes", Effect.MachineSpeed, 0.05),
        N("Chemistry", 5, "Fermentation", Effect.ArtisanSpeed, 0.10),
        N("Chemistry", 6, "Reaction Rates", Effect.MachineSpeed, 0.10),
        N("Chemistry", 7, "Flavor Science", Effect.ArtisanQuality, 0.10),
        N("Chemistry", 8, "Alchemy", Effect.MachineSpeed, 0.10),

        N("Petroleum", 1, "Seismic Survey", Effect.OilYield, 0.05),
        N("Petroleum", 2, "Blowout Preventer", Effect.SpillSafety, 0.25),
        N("Petroleum", 3, "Diamond Drill Bits", Effect.OilYield, 0.10),
        N("Petroleum", 4, "Containment Booms", Effect.SpillSafety, 0.25),
        N("Petroleum", 5, "Horizontal Drilling", Effect.OilYield, 0.10),
        N("Petroleum", 6, "Pipeline Inspection", Effect.SpillSafety, 0.25),
        N("Petroleum", 7, "Enhanced Recovery", Effect.OilYield, 0.15),
        N("Petroleum", 8, "Black Gold Rush", Effect.OilYield, 0.20),

        N("Economics", 1, "Supply and Demand", Effect.ShippingBonus, 0.02),
        N("Economics", 2, "Sales Psychology", Effect.SalesMarkup, 0.03),
        N("Economics", 3, "Compound Interest", Effect.Interest, 0.10),
        N("Economics", 4, "Market Analysis", Effect.ShippingBonus, 0.03),
        N("Economics", 5, "Brand Loyalty", Effect.SalesMarkup, 0.05),
        N("Economics", 6, "Central Banking", Effect.Interest, 0.20),
        N("Economics", 7, "Export Contracts", Effect.ShippingBonus, 0.05),
        N("Economics", 8, "Thornbury Empire", Effect.ShippingBonus, 0.05),

        N("Logistics", 1, "Bunk Beds", Effect.ExtraBeds, 1),
        N("Logistics", 2, "Route Planning", Effect.CourierDeliveries, 0.25),
        N("Logistics", 3, "Pack Mules", Effect.CrewSpeed, 0.05),
        N("Logistics", 4, "Triple Bunks", Effect.ExtraBeds, 1),
        N("Logistics", 5, "Express Post", Effect.CourierDeliveries, 0.25),
        N("Logistics", 6, "Conveyor Thinking", Effect.CrewSpeed, 0.05),
        N("Logistics", 7, "Brawnie Barracks", Effect.ExtraBeds, 2),
        N("Logistics", 8, "Hermes Boots", Effect.PlayerSpeed, 1),

        N("Meteorology", 1, "Barometer", Effect.DailyLuck, 0.01),
        N("Meteorology", 2, "Cloud Seeding", Effect.RainChance, 0.10),
        N("Meteorology", 3, "Wind Charts", Effect.DailyLuck, 0.01),
        N("Meteorology", 4, "Rain Dance Science", Effect.RainChance, 0.10),
        N("Meteorology", 5, "Farmer's Almanac", Effect.DailyLuck, 0.01),
        N("Meteorology", 6, "Storm Chasing", Effect.RainChance, 0.10),
        N("Meteorology", 7, "Lucky Stars", Effect.DailyLuck, 0.02),
        N("Meteorology", 8, "Weather Control", Effect.RainChance, 0.20),

        N("Medicine", 1, "First Aid", Effect.MaxStamina, 15),
        N("Medicine", 2, "Field Medicine", Effect.AdventurerLuck, 0.15),
        N("Medicine", 3, "Vitamins", Effect.NoonStamina, 0.10),
        N("Medicine", 4, "Physiotherapy", Effect.MaxStamina, 15),
        N("Medicine", 5, "Trauma Care", Effect.AdventurerLuck, 0.20),
        N("Medicine", 6, "Energy Tonic", Effect.NoonStamina, 0.15),
        N("Medicine", 7, "Iron Lungs", Effect.MaxStamina, 20),
        N("Medicine", 8, "Brawnie Constitution", Effect.NoonStamina, 0.25),

        N("Animal Science", 1, "Animal Husbandry", Effect.AnimalFriendship, 5),
        N("Animal Science", 2, "Feed Science", Effect.RanchOutput, 0.10),
        N("Animal Science", 3, "Vet Training", Effect.RanchQuality, 0.05),
        N("Animal Science", 4, "Pasture Rotation", Effect.AnimalFriendship, 5),
        N("Animal Science", 5, "Selective Breeding", Effect.RanchOutput, 0.15),
        N("Animal Science", 6, "Animal Psychology", Effect.AnimalFriendship, 10),
        N("Animal Science", 7, "Prize Livestock", Effect.RanchQuality, 0.10),
        N("Animal Science", 8, "Happy Herd", Effect.RanchOutput, 0.20),

        N("Engineering", 1, "Ergonomic Grips", Effect.ToolEnergy, 0.10),
        N("Engineering", 2, "Bulk Contracts", Effect.UpgradeDiscount, 0.10),
        N("Engineering", 3, "Sealed Casks", Effect.ArtisanStar, 0.10),
        N("Engineering", 4, "Balanced Heads", Effect.ToolEnergy, 0.10),
        N("Engineering", 5, "Bigger Vats", Effect.DoubleArtisan, 0.05),
        N("Engineering", 6, "Clint's Rival", Effect.UpgradeDiscount, 0.15),
        N("Engineering", 7, "Lightweight Alloys", Effect.ToolEnergy, 0.15),
        N("Engineering", 8, "Master Craftsman", Effect.DoubleArtisan, 0.10),

        N("Horticulture", 1, "Seed Selection", Effect.GiantCrops, 0.50),
        N("Horticulture", 2, "Grafting", Effect.FruitTreeGrowth, 0.25),
        N("Horticulture", 3, "Arborism", Effect.WildTreeGrowth, 0.20),
        N("Horticulture", 4, "Root Stimulant", Effect.GiantCrops, 1.0),
        N("Horticulture", 5, "Orchard Science", Effect.FruitTreeGrowth, 0.35),
        N("Horticulture", 6, "Forest Canopy", Effect.WildTreeGrowth, 0.30),
        N("Horticulture", 7, "Colossal Genetics", Effect.GiantCrops, 1.0),
        N("Horticulture", 8, "Titan Strain", Effect.GiantCrops, 1.5),

        N("Archaeology", 1, "Trowel Technique", Effect.DoubleDig, 0.10),
        N("Archaeology", 2, "Survey Maps", Effect.ArtifactSpots, 2),
        N("Archaeology", 3, "Carbon Dating", Effect.SampleBonus, 0.25),
        N("Archaeology", 4, "Fine Brushes", Effect.DoubleDig, 0.10),
        N("Archaeology", 5, "Ground Radar", Effect.ArtifactSpots, 3),
        N("Archaeology", 6, "Museum Grant", Effect.SampleBonus, 0.50),
        N("Archaeology", 7, "Lost Civilizations", Effect.DoubleDig, 0.15),
        N("Archaeology", 8, "Indiana Brawnie", Effect.ArtifactSpots, 5),

        N("Culinary Science", 1, "Spice Rack", Effect.FoodRestore, 0.10),
        N("Culinary Science", 2, "Slow Release", Effect.BuffDuration, 0.25),
        N("Culinary Science", 3, "Kitchen Brigade", Effect.ChefOutput, 0.15),
        N("Culinary Science", 4, "Nutrition", Effect.FoodRestore, 0.15),
        N("Culinary Science", 5, "Fermented Flavors", Effect.BuffDuration, 0.25),
        N("Culinary Science", 6, "Plating Science", Effect.ChefQuality, 0.15),
        N("Culinary Science", 7, "Superfoods", Effect.FoodRestore, 0.25),
        N("Culinary Science", 8, "Grandma's Recipes", Effect.BuffDuration, 0.50),

        N("Dungeoneering", 1, "Monster Manual", Effect.GuildPower, 0.05),
        N("Dungeoneering", 2, "Field Dressing", Effect.GuildInjury, 0.25),
        N("Dungeoneering", 3, "Treasure Maps", Effect.GuildGold, 0.15),
        N("Dungeoneering", 4, "Combat Drills", Effect.GuildTraining, 0.15),
        N("Dungeoneering", 5, "Arcane Theory", Effect.GuildPower, 0.10),
        N("Dungeoneering", 6, "Dungeon Cartography", Effect.GuildRuns, 1),
        N("Dungeoneering", 7, "Relic Hunting", Effect.GuildLoot, 0.25),
        N("Dungeoneering", 8, "Legends of the Hollow", Effect.GuildPower, 0.20),
        N("Seafaring", 1, "Sea Charts", Effect.FleetPower, 0.05),
        N("Seafaring", 2, "Scurvy Cures", Effect.FleetInjury, 0.25),
        N("Seafaring", 3, "Letters of Marque", Effect.FleetGold, 0.15),
        N("Seafaring", 4, "Copper Hulls", Effect.FleetSpeed, 1),
        N("Seafaring", 5, "Cannon Ballistics", Effect.FleetPower, 0.10),
        N("Seafaring", 6, "Treasure Lore", Effect.FleetLoot, 0.50),
        N("Seafaring", 7, "Trade Winds", Effect.FleetSpeed, 1),
        N("Seafaring", 8, "Kraken Hunters", Effect.FleetPower, 0.20),
    };

    public static readonly IReadOnlyList<string> Trees = Nodes.Select(n => n.Tree).Distinct().ToList();

    public static IEnumerable<ResearchNode> In(string tree) => Nodes.Where(n => n.Tree == tree).OrderBy(n => n.Tier);
    public static ResearchNode? Find(string? id) => Nodes.FirstOrDefault(n => n.Id == id);
    public static bool Done(MarketState state, ResearchNode node) => state.Researched.Contains(node.Id);

    /// <summary>Sum of an effect over finished projects.</summary>
    public static double Total(MarketState state, Effect effect) =>
        state.Researched.Select(Find).Where(n => n?.Effect == effect).Sum(n => n!.Amount);

    /// <summary>Days a project takes, shortened by the scientist's speed.</summary>
    public static int Days(ResearchNode node, double speed) => Math.Max(1, (int)Math.Ceiling(node.Days / (1 + speed)));

    /// <summary>Why this project can't start now, or null if it can.</summary>
    public static string? WhyNot(MarketState state, ResearchNode node, long money)
    {
        if (Done(state, node))
            return "Already researched.";
        if (state.Researching != null)
            return $"The lab's busy with {Find(state.Researching)?.Name ?? "something"}.";
        if (node.Tier > 1 && !Done(state, In(node.Tree).First(n => n.Tier == node.Tier - 1)))
            return "Research the one before it first.";
        if (state.ResearchPoints < node.Points)
            return $"Needs {node.Points:N0} research points (have {state.ResearchPoints:N0}).";
        if (money < node.Gold)
            return $"Needs {node.Gold:N0}g for equipment.";
        return null;
    }

    /// <summary>Spend the points and start the clock (the caller takes the gold).</summary>
    public static void Start(MarketState state, ResearchNode node, int today, double speed)
    {
        state.ResearchPoints -= node.Points;
        state.Researching = node.Id;
        state.ResearchDoneDay = today + Days(node, speed);
    }

    /// <summary>Finish the current project if its day has come; returns it.</summary>
    public static ResearchNode? Finish(MarketState state, int today)
    {
        if (Find(state.Researching) is not { } node || today < state.ResearchDoneDay)
            return null;
        state.Researched.Add(node.Id);
        state.Researching = null;
        return node;
    }
}
