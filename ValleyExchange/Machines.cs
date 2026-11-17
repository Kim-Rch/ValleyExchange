using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.Machines;
using SObject = StardewValley.Object;

namespace ValleyExchange;

/// <summary>
/// A hand who tends machines one at a time: empties finished ones into the crate, then refills them with the most valuable thing in
/// the crate they accept (paying any coal from the crate too). Each tender has their own machines and their own knack.
/// </summary>
internal sealed class MachineTender : ITileWork
{
    /// <summary>The drunk brewer: kegs and jars (modded ones too). 25% faster, 15% a quality star better.</summary>
    public static readonly MachineTender Brewer = new(Role.Brewer, IsBrewMachine, Brewable, "kegs and jars filled", "brews bottled", "bubbles", faster: 0.25, better: 0.15);

    /// <summary>The blacksmith: furnaces, heavy furnaces and the geode crusher. Hot forge: 30% faster.</summary>
    public static readonly MachineTender Smith = new(Role.Blacksmith, IsSmithMachine, i => i.QualifiedItemId != Coal, "furnaces loaded", "bars and finds", "furnace", faster: 0.30, better: 0);

    /// <summary>The artisan: every other machine that takes farm goods (cheese press, mayo, loom, oil maker, smoker, dehydrator, modded ones). 10% faster, 20% a star better.</summary>
    public static readonly MachineTender Artisan = new(Role.Artisan, IsArtisanMachine, ArtisanInput, "machines loaded", "artisan goods", "Ship", faster: 0.10, better: 0.20);

    private const string Coal = "(O)382";
    private const double FasterFactor = 0.7;

    /// <summary>Machines the artisan must never touch: they'd eat seeds-to-be, hatch eggs, recycle or destroy things.</summary>
    private static readonly HashSet<string> NotArtisan = new()
    {
        "(BC)25", "(BC)20", "(BC)265", "(BC)BaitMaker", "(BC)Anvil", "(BC)MiniForge", "(BC)21", "(BC)101", "(BC)254", "(BC)156", "(BC)158", "(BC)163",
        Oil.QPumpjack, Oil.QRefinery,
    };

    private readonly Role role;
    private readonly Func<SObject, bool> isMachine;
    private readonly Func<Item, bool> usable;
    private readonly string filled, emptied;
    private readonly double faster, better;

    /// <summary>Machines where nothing in the crate fits, so they stop walking back to them (cleared each morning).</summary>
    private readonly HashSet<(string, Vector2)> nothingFits = new();

    private MachineTender(Role role, Func<SObject, bool> isMachine, Func<Item, bool> usable, string filled, string emptied, string sound, double faster, double better)
    {
        (this.role, this.isMachine, this.usable, this.filled, this.emptied, this.Sound, this.faster, this.better) = (role, isMachine, usable, filled, emptied, sound, faster, better);
    }

    public static IEnumerable<MachineTender> All => new[] { Brewer, Smith, Artisan };

    public static void NewDay()
    {
        foreach (var tender in All)
            tender.nothingFits.Clear();
    }

    public bool IsMachine(SObject obj) => this.isMachine(obj);

    public string Sound { get; }
    public bool CanPay => true; // on a daily wage
    public int PerTenMinutes => 4;
    public bool Barns => this.role == Role.Artisan; // mayo machines live in the coop, cheese presses in the barn

    // ---- which machines ----

    private static bool Takes(SObject obj) =>
        obj.bigCraftable.Value && obj.GetMachineData() is { } data
        && data.OutputRules?.Any(r => r.Triggers?.Any(t => t.Trigger.HasFlag(MachineOutputTrigger.ItemPlacedInMachine)) == true) == true;

    public static bool IsBrewMachine(SObject obj) =>
        obj.bigCraftable.Value && obj.GetMachineData() is { } data && (data.AdditionalConsumedItems?.Count ?? 0) == 0 // no coal-style extras
        && (obj.QualifiedItemId is "(BC)12" or "(BC)15"
            || obj.Name.Contains("Keg", StringComparison.OrdinalIgnoreCase)
            || obj.Name.Contains("Jar", StringComparison.OrdinalIgnoreCase));

    private static bool IsSmithMachine(SObject obj) =>
        Takes(obj) && obj.QualifiedItemId != "(BC)MiniForge"
        && (obj.QualifiedItemId is "(BC)13" or "(BC)HeavyFurnace" or "(BC)182"
            || obj.Name.Contains("Furnace", StringComparison.OrdinalIgnoreCase)
            || obj.Name.Contains("Smelter", StringComparison.OrdinalIgnoreCase));

    private static bool IsArtisanMachine(SObject obj) =>
        Takes(obj) && !NotArtisan.Contains(obj.QualifiedItemId) && !IsBrewMachine(obj) && !IsSmithMachine(obj);

    // ---- which inputs ----

    /// <summary>Things worth brewing: fruit, vegetables, flowers, greens, honey, coffee beans, tea leaves.</summary>
    private static bool Brewable(Item item) =>
        item.Category is SObject.FruitsCategory or SObject.VegetableCategory or SObject.flowersCategory or SObject.GreensCategory
        || item.QualifiedItemId is "(O)340" or "(O)433" or "(O)815";

    /// <summary>Eggs, milk, wool and feathers, fish, mushrooms and forage, truffles, corn and sunflowers. Fruit is the brewer's.</summary>
    private static bool ArtisanInput(Item item) =>
        item.Category is SObject.EggCategory or SObject.MilkCategory or SObject.sellAtPierresAndMarnies or SObject.FishCategory or SObject.GreensCategory
        || item.QualifiedItemId is "(O)430" or "(O)270" or "(O)421";

    // ---- the work ----

    public List<Vector2> Targets(GameLocation location)
    {
        bool anything = Crate.Items().Any(this.usable);
        return location.objects.Pairs
            .Where(p => this.isMachine(p.Value) && (Ready(p.Value) || (anything && p.Value.heldObject.Value == null && !this.nothingFits.Contains((location.NameOrUniqueName, p.Key)))))
            .Select(p => p.Key)
            .ToList();
    }

    public bool IsReady(GameLocation location, Vector2 tile) =>
        location.objects.TryGetValue(tile, out var obj) && this.isMachine(obj) && (Ready(obj) || obj.heldObject.Value == null);

    private static bool Ready(SObject machine) => machine.readyForHarvest.Value && machine.heldObject.Value != null;

    /// <summary>Empty it if it's done, then fill it from the crate. Returns what went in or came out, for the pop-up.</summary>
    public List<Item> Act(GameLocation location, Vector2 tile)
    {
        var shown = new List<Item>();
        if (!location.objects.TryGetValue(tile, out var machine) || !this.isMachine(machine))
            return shown;

        if (Ready(machine))
        {
            var output = machine.heldObject.Value;
            machine.heldObject.Value = null;
            machine.readyForHarvest.Value = false;
            machine.showNextIndex.Value = false;
            CrewReport.Got(this.role, this.emptied, output);
            Crate.Store(output);
            shown.Add(output);
        }
        if (machine.heldObject.Value != null)
            return shown;

        var data = machine.GetMachineData();
        var extras = (data.AdditionalConsumedItems ?? new()).Select(e => (Id: ItemRegistry.QualifyItemId(e.ItemId) ?? e.ItemId, Count: Math.Max(1, e.RequiredCount))).ToList();
        if (extras.Any(e => Crate.Count(e.Id) < e.Count))
        {
            this.nothingFits.Add((location.NameOrUniqueName, tile)); // no coal in the crate
            return shown;
        }
        foreach (var input in Crate.Items().Where(this.usable).GroupBy(i => i.QualifiedItemId).Select(g => g.First()).OrderByDescending(i => i.sellToStorePrice()).ToList())
        {
            if (!MachineDataUtility.TryGetMachineOutputRule(machine, data, MachineOutputTrigger.ItemPlacedInMachine, input, Game1.player, location, out var rule, out var trigger, out _, out _))
                continue;
            int needed = Math.Max(1, trigger?.RequiredCount ?? 1);
            if (Crate.Count(input.QualifiedItemId) < needed || !machine.OutputMachine(data, rule, input.getOne(), Game1.player, location, false) || machine.heldObject.Value == null)
                continue;
            if (!CrewSkills.Bonus(this.role))
                Crate.Take(input.QualifiedItemId, needed); // (saver perks: sometimes it's on the house)
            foreach (var (id, count) in extras)
                Crate.Take(id, count);
            CrewSkills.Work(this.role, 2);
            this.Inspire(machine);
            CrewReport.Count(this.role, this.filled);
            shown.Add(input);
            return shown;
        }
        this.nothingFits.Add((location.NameOrUniqueName, tile));
        return shown;
    }

    /// <summary>The tender's knack: sometimes faster, sometimes a star better.</summary>
    private void Inspire(SObject machine)
    {
        if (Game1.random.NextDouble() < this.faster + CrewSkills.Total(this.role, PerkKind.Speed))
            machine.MinutesUntilReady = Math.Max(10, (int)(machine.MinutesUntilReady * FasterFactor));
        if (Game1.random.NextDouble() < this.better + CrewSkills.Total(this.role, PerkKind.Quality) && machine.heldObject.Value is { } product && product.Quality < SObject.bestQuality)
            product.Quality = product.Quality == SObject.highQuality ? SObject.bestQuality : product.Quality + 1;
    }

    // ---- the blacksmith's anvil ----

    private static readonly HashSet<string> Geodes = new() { "(O)535", "(O)536", "(O)537", "(O)749", "(O)275" };
    public const int GeodeFee = 25, GeodesPerSmith = 10;

    /// <summary>Evening: the blacksmiths crack geodes from the crate, Clint's price, and the treasure goes in the crate.</summary>
    public static string CrackGeodes(int smiths)
    {
        int cracked = 0, max = (int)Math.Round(GeodesPerSmith * smiths * CrewSkills.More(Role.Blacksmith));
        foreach (string id in Geodes)
        {
            while (cracked < max && Game1.player.Money >= GeodeFee && Crate.Take(id, 1))
            {
                Game1.player.Money -= GeodeFee;
                CrewReport.Paid(Role.Blacksmith, GeodeFee);
                Game1.stats.GeodesCracked++;
                var treasure = Utility.getTreasureFromGeode(ItemRegistry.Create(id));
                if (treasure != null)
                {
                    CrewReport.Got(Role.Blacksmith, "geode finds", treasure);
                    Crate.Store(treasure);
                }
                cracked++;
            }
        }
        CrewSkills.Work(Role.Blacksmith, cracked);
        return cracked > 0 ? $"Cracked {cracked} geodes at {GeodeFee}g a pop. Finds are in the crate." : "Furnaces are hot, boss. No geodes in the crate to crack.";
    }
}
