using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Machines;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Shops;
using SObject = StardewValley.Object;

namespace ValleyExchange;

/// <summary>Pumpjacks, a refinery, crude priced off the energy stocks, and the occasional oil-town drama.</summary>
public static class Oil
{
    private const string Pumpjack = "evant.ValleyExchange_Pumpjack", Refinery = "evant.ValleyExchange_Refinery";
    private const string Crude = "evant.ValleyExchange_CrudeOil", Gasoline = "evant.ValleyExchange_Gasoline";
    public const string QPumpjack = "(BC)" + Pumpjack, QRefinery = "(BC)" + Refinery, QCrude = "(O)" + Crude, QGasoline = "(O)" + Gasoline;
    private const string PumpjackTexture = "Mods/evant.ValleyExchange/Pumpjack", RefineryTexture = "Mods/evant.ValleyExchange/Refinery", ItemTexture = "Mods/evant.ValleyExchange/Oil";
    public const string IntroMail = "evant.ValleyExchange_OilIntro";
    private const string SpillMail = "evant.ValleyExchange_Spill", ProtestMail = "evant.ValleyExchange_Protest";
    private const int RefineBatch = 5, PumpjackPrice = 40_000, RefineryPrice = 25_000;

    public static void OnAssetRequested(AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(PumpjackTexture))
            e.LoadFromModFile<Texture2D>("assets/pumpjack.png", AssetLoadPriority.Exclusive);
        else if (e.NameWithoutLocale.IsEquivalentTo(RefineryTexture))
            e.LoadFromModFile<Texture2D>("assets/refinery.png", AssetLoadPriority.Exclusive);
        else if (e.NameWithoutLocale.IsEquivalentTo(ItemTexture))
            e.LoadFromModFile<Texture2D>("assets/oil.png", AssetLoadPriority.Exclusive);
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/BigCraftables"))
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, BigCraftableData>().Data;
                data[Pumpjack] = new BigCraftableData
                {
                    Name = Pumpjack, DisplayName = "Pumpjack", Price = 0, Texture = PumpjackTexture, SpriteIndex = 0,
                    Description = "Nods all day, pumps crude all night. Some ground is dry, some ground gushes.",
                    CanBePlacedIndoors = false, CanBePlacedOutdoors = true,
                };
                data[Refinery] = new BigCraftableData
                {
                    Name = Refinery, DisplayName = "Oil Refinery", Price = 0, Texture = RefineryTexture, SpriteIndex = 0,
                    Description = $"Turns {RefineBatch} barrels of crude into {RefineBatch} cans of gasoline overnight.",
                    CanBePlacedIndoors = true, CanBePlacedOutdoors = true,
                };
            });
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, ObjectData>().Data;
                data[Crude] = new ObjectData
                {
                    Name = Crude, DisplayName = "Crude Oil", Type = "Basic", Category = 0, Price = Market.CrudeBasePrice, Texture = ItemTexture, SpriteIndex = 0,
                    Description = "Texas tea. Ship it and it sells at today's crude price on the Valley Exchange.",
                };
                data[Gasoline] = new ObjectData
                {
                    Name = Gasoline, DisplayName = "Gasoline", Type = "Basic", Category = 0, Price = (int)(Market.CrudeBasePrice * Market.GasolineMultiplier), Texture = ItemTexture, SpriteIndex = 1,
                    Description = "Refined and ready. Ships for more than crude, priced off the Valley Exchange.",
                };
            });
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Machines"))
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, MachineData>().Data;
                data[QPumpjack] = new MachineData
                {
                    OutputRules = new()
                    {
                        new MachineOutputRule
                        {
                            Id = "Pump",
                            Triggers = new()
                            {
                                new MachineOutputTriggerRule { Id = "PutDown", Trigger = MachineOutputTrigger.MachinePutDown },
                                new MachineOutputTriggerRule { Id = "Collected", Trigger = MachineOutputTrigger.OutputCollected },
                            },
                            OutputItem = new() { new MachineItemOutput { Id = "Crude", OutputMethod = $"{typeof(Oil).FullName}, {typeof(Oil).Assembly.GetName().Name}: {nameof(PumpOutput)}" } },
                            DaysUntilReady = 1,
                        },
                    },
                    WorkingEffects = new() { new MachineEffects { Id = "Nod", Frames = new() { 1, 0, 1, 0, 1, 0 }, Interval = 350 } },
                    WorkingEffectChance = 1f,
                };
                data[QRefinery] = new MachineData
                {
                    OutputRules = new()
                    {
                        new MachineOutputRule
                        {
                            Id = "Refine",
                            Triggers = new() { new MachineOutputTriggerRule { Id = "Crude", Trigger = MachineOutputTrigger.ItemPlacedInMachine, RequiredItemId = QCrude, RequiredCount = RefineBatch } },
                            OutputItem = new() { new MachineItemOutput { Id = "Gasoline", ItemId = QGasoline, MinStack = RefineBatch, MaxStack = RefineBatch } },
                            DaysUntilReady = 1,
                        },
                    },
                    InvalidCountMessage = $"The refinery needs {RefineBatch} barrels of crude at a time.",
                    LoadEffects = new() { new MachineEffects { Id = "Load", Sounds = new() { new MachineSoundData { Id = "Ship" } }, ShakeDuration = 100 } },
                    WobbleWhileWorking = true,
                };
            });
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
            e.Edit(asset =>
            {
                if (!asset.AsDictionary<string, ShopData>().Data.TryGetValue("Carpenter", out var robin))
                    return;
                robin.Items.Add(new ShopItemData { Id = Pumpjack, ItemId = QPumpjack, Price = PumpjackPrice });
                robin.Items.Add(new ShopItemData { Id = Refinery, ItemId = QRefinery, Price = RefineryPrice });
            });
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/mail"))
            e.Edit(asset =>
            {
                var mail = asset.AsDictionary<string, string>().Data;
                mail[IntroMail] = "Hey @,^^Rumor around town is there's oil under your land. I've started stocking pumpjacks and refineries at the shop.^^"
                    + "Fair warning: not every spot has oil. Place a pumpjack and you'll know right away if it's a dry hole. Crude sells at whatever the Valley Exchange says that day.^^   -Robin[#]Oil Under Your Farm";
                mail[SpillMail] = "@,^^One of your pumpjacks leaked crude all over the place last night. It reached the river and Willy is beside himself.^^"
                    + "The cleanup fine has been taken from your account. Don't make me write this letter again.^^   -Mayor Lewis[#]Oil Spill Fine";
                mail[ProtestMail] = "@!^^I can feel the earth crying under your farm. Those pumpjacks are poisoning the valley's spirit!^^"
                    + "I'm organizing a protest. Haley said no, but I made the signs anyway.^^   -Emily[#]A Protest";
            });
    }

    /// <summary>Data/Machines OutputMethod for the pumpjack: barrels depend on the tile, dry wells produce nothing.</summary>
    public static Item? PumpOutput(SObject machine, Item inputItem, bool probe, MachineItemOutput outputData, Farmer player, out int? overrideMinutesUntilReady)
    {
        overrideMinutesUntilReady = null;
        int barrels = Barrels(machine.Location ?? Game1.currentLocation, machine.TileLocation);
        return barrels > 0 ? ItemRegistry.Create(QCrude, barrels) : null;
    }

    public static int Barrels(GameLocation location, Microsoft.Xna.Framework.Vector2 tile) =>
        Market.WellBarrels(ModEntry.Seed, location.NameOrUniqueName, (int)tile.X, (int)tile.Y);

    /// <summary>The "survey": as soon as a pumpjack goes down, tell the player what's under it.</summary>
    public static void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
    {
        if (e.Location != Game1.currentLocation)
            return;
        foreach (var (tile, obj) in e.Added)
        {
            if (obj.QualifiedItemId != QPumpjack)
                continue;
            int barrels = Barrels(e.Location, tile);
            Game1.addHUDMessage(new HUDMessage(Market.WellReport(barrels), barrels == 0 ? HUDMessage.error_type : HUDMessage.achievement_type));
            Game1.playSound(barrels == 0 ? "cancel" : barrels >= Market.GusherBarrels ? "reward" : "coin");
        }
    }

    /// <summary>Ship crude and gasoline at today's market price (call before the game sells the bin overnight).</summary>
    public static void PriceShipments(MarketState state)
    {
        // shortcut: main farm shipping bin only; mini shipping bins and separate farmhand bins sell at the base price
        int crude = Market.CrudePrice(state), gas = Market.GasolinePrice(state);
        foreach (var item in Game1.getFarm().getShippingBin(Game1.player))
        {
            if (item is SObject o && o.QualifiedItemId == QCrude)
                o.Price = crude;
            else if (item is SObject g && g.QualifiedItemId == QGasoline)
                g.Price = gas;
        }
    }

    /// <summary>Every pumpjack and refinery in the world.</summary>
    public static List<(GameLocation Location, Microsoft.Xna.Framework.Vector2 Tile, SObject Machine)> Machines()
    {
        var found = new List<(GameLocation, Microsoft.Xna.Framework.Vector2, SObject)>();
        Utility.ForEachLocation(location =>
        {
            foreach (var pair in location.objects.Pairs)
            {
                if (pair.Value.QualifiedItemId is QPumpjack or QRefinery)
                    found.Add((location, pair.Key, pair.Value));
            }
            return true;
        });
        return found;
    }

    /// <summary>The roughneck's morning round: crude and gasoline into the crates, pumpjacks restarted, refineries refilled from the crates.</summary>
    public static string Tend()
    {
        int crude = 0, gas = 0, batches = 0;
        foreach (var (location, tile, machine) in Machines())
        {
            if (machine.readyForHarvest.Value && machine.heldObject.Value is { } output)
            {
                if (output.QualifiedItemId == QGasoline)
                    gas += output.Stack;
                else
                    crude += output.Stack;
                CrewSkills.Work(Role.Oil, 2);
                CrewReport.Got(Role.Oil, output.QualifiedItemId == QGasoline ? "gasoline" : "crude", output);
                Crate.Store(output);
                machine.heldObject.Value = null;
                machine.readyForHarvest.Value = false;
                machine.MinutesUntilReady = 0;
            }
            if (machine.heldObject.Value != null)
                continue; // still working
            Item? next = null;
            if (machine.QualifiedItemId == QPumpjack)
            {
                int barrels = Barrels(location, tile);
                if (barrels > 0) // dry wells stay idle
                    next = ItemRegistry.Create(QCrude, (int)Math.Round(barrels * CrewSkills.More(Role.Oil)));
            }
            else if (TakeCrude(RefineBatch))
            {
                next = ItemRegistry.Create(QGasoline, RefineBatch);
                batches++;
            }
            if (next is SObject product)
            {
                machine.heldObject.Value = product;
                machine.MinutesUntilReady = Utility.CalculateMinutesUntilMorning(Game1.timeOfDay);
            }
        }
        return $"Pulled {crude} barrels of crude and {gas} cans of gas this mornin'. Refineries got {batches} fresh loads.";
    }

    /// <summary>Remove crude from the crates, all or nothing.</summary>
    private static bool TakeCrude(int count)
    {
        var crates = Crate.All();
        if (crates.SelectMany(c => c.Items).Where(i => i?.QualifiedItemId == QCrude).Sum(i => i.Stack) < count)
            return false;
        foreach (var items in crates.Select(c => c.Items))
        {
            for (int i = 0; i < items.Count && count > 0; i++)
            {
                if (items[i] is not { QualifiedItemId: QCrude } stack)
                    continue;
                int take = Math.Min(count, stack.Stack);
                items[i] = stack.ConsumeStack(take);
                count -= take;
            }
        }
        crates.ForEach(c => c.clearNulls());
        return true;
    }

    /// <summary>All pumpjacks in the world, and how many sit on gushers.</summary>
    public static (int Pumpjacks, int Gushers) CountWells()
    {
        int pumpjacks = 0, gushers = 0;
        Utility.ForEachLocation(location =>
        {
            foreach (var pair in location.objects.Pairs)
            {
                if (pair.Value.QualifiedItemId != QPumpjack)
                    continue;
                pumpjacks++;
                if (Barrels(location, pair.Key) >= Market.GusherBarrels)
                    gushers++;
            }
            return true;
        });
        return (pumpjacks, gushers);
    }

    /// <summary>Small daily chance of an oil spill fine or an Emily protest. Returns a line for the AI-dialogue brief, if something happened.</summary>
    public static string? DailyDrama(int pumpjacks, int day, bool roughneck)
    {
        if (pumpjacks == 0)
            return null;
        var rng = new Random(ModEntry.Seed ^ (day * 7907)); // fixed per day: reloading doesn't reroll
        double roll = rng.NextDouble();
        string farmer = Game1.player.Name;
        double safety = Workers.State is { } st ? Math.Min(1, Research.Total(st, Effect.SpillSafety)) : 0; // Petroleum research
        if (roll < (CrewSkills.Has(Role.Oil, PerkKind.NoSpills) ? 0 : roughneck ? 0.01 : 0.03) * (1 - safety)) // a roughneck on site stops most spills; Safety First stops them all
        {
            int fine = Math.Min(Game1.player.Money, Math.Min(30_000, 1_500 * pumpjacks));
            Game1.player.Money -= fine;
            Game1.player.mailbox.Add(SpillMail);
            Game1.addHUDMessage(new HUDMessage($"Oil spill! Lewis fined you {fine:N0}g.", HUDMessage.error_type));
            return $"Today Mayor Lewis fined {farmer} {fine:N0}g because a pumpjack spilled crude into the river.";
        }
        if (roll >= 0.03 && roll < 0.06)
        {
            Game1.player.mailbox.Add(ProtestMail);
            return $"Emily is organizing a protest against {farmer}'s oil drilling.";
        }
        return null;
    }
}
