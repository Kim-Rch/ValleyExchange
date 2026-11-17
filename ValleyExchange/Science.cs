using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;
using SObject = StardewValley.Object;

namespace ValleyExchange;

/// <summary>Game side of the research trees: the scientist's daily points, donations, and the hooks that make research count for you too.</summary>
internal static class Science
{
    private static IMonitor? monitor;
    private static IGameContentHelper? content;
    private static bool digging; // double dig: don't double the double
    private static readonly string[] DigSites = { "Town", "Mountain", "Forest", "BusStop", "Beach", "Backwoods", "Railroad" };

    /// <summary>Ore node → its ore.</summary>
    private static readonly Dictionary<string, string> OreOf = new()
    {
        ["751"] = "(O)378", ["849"] = "(O)378", ["290"] = "(O)380", ["850"] = "(O)380",
        ["764"] = "(O)384", ["765"] = "(O)386", ["95"] = "(O)909",
    };

    private static MarketState? State => Workers.State;
    private static double Total(Effect effect) => State is { } state ? Research.Total(state, effect) : 0;

    // ---- the lab ----

    /// <summary>Morning: research points, a finished project, and self-watering soil.</summary>
    public static void Morning(MarketState state, int today)
    {
        if (Market.IsHired(state, Role.Scientist))
        {
            int points = (int)Math.Round(Research.DailyPoints * CrewSkills.More(Role.Scientist));
            state.ResearchPoints += points;
            CrewReport.Count(Role.Scientist, "research points", points);
            CrewSkills.Work(Role.Scientist, 10);
            if (Research.Finish(state, today) is { } done)
            {
                CrewReport.Count(Role.Scientist, "projects finished");
                Game1.addHUDMessage(new HUDMessage($"Research complete: {done.Name}! {done.Text}.", HUDMessage.achievement_type));
                Game1.playSound("discoverMineral");
            }
            Staff.Reports[Role.Scientist] = Research.Find(state.Researching) is { } now
                ? $"Workin' on {now.Name}. {Math.Max(0, state.ResearchDoneDay - today)} more day(s). {state.ResearchPoints:N0} points banked."
                : $"Lab's idle, boss. {state.ResearchPoints:N0} research points ready to spend.";
        }
        SelfWater(Research.Total(state, Effect.SelfWater));
        SpeedMachines(Math.Min(0.3, Research.Total(state, Effect.MachineSpeed)));
        GrowTrees(Research.Total(state, Effect.FruitTreeGrowth), Research.Total(state, Effect.WildTreeGrowth));
        SpawnArtifactSpots((int)Research.Total(state, Effect.ArtifactSpots));
        if (Research.Total(state, Effect.GiantCrops) > 0)
            content?.InvalidateCache("Data/GiantCrops"); // re-apply Horticulture to the game data
        if (Research.Total(state, Effect.UpgradeDiscount) > 0)
            content?.InvalidateCache("Data/Shops"); // and Engineering's discount
        PetAnimals((int)Research.Total(state, Effect.AnimalFriendship));
        var player = Game1.player;
        if (player.daysLeftForToolUpgrade.Value > 1 && Game1.random.NextDouble() < Research.Total(state, Effect.FastToolUpgrade))
            player.daysLeftForToolUpgrade.Value--;
        double luck = Research.Total(state, Effect.DailyLuck);
        if (luck > 0)
            player.team.sharedDailyLuck.Value = Math.Min(0.125, player.team.sharedDailyLuck.Value + luck);
        Boost(Research.Total(state, Effect.PlayerSpeed), Research.Total(state, Effect.MaxStamina));
    }

    /// <summary>Night: the shipping bonus, and maybe rain tomorrow.</summary>
    public static void Evening(MarketState state)
    {
        double bonus = Research.Total(state, Effect.ShippingBonus);
        if (bonus > 0)
        {
            long value = Game1.getFarm().getShippingBin(Game1.player).Where(i => i != null).Sum(i => (long)Utility.getSellToStorePriceOfItem(i));
            int extra = (int)Math.Round(value * bonus);
            if (extra > 0)
            {
                Game1.player.Money += extra;
                CrewReport.Earned(Role.Scientist, extra);
                CrewReport.Count(Role.Scientist, "g shipping bonus (Economics)", extra);
            }
        }
        double rain = Math.Min(0.5, Research.Total(state, Effect.RainChance));
        if (rain > 0 && Game1.weatherForTomorrow == Game1.weather_sunny && Game1.season != Season.Winter && Game1.dayOfMonth < 28
            && !Utility.isFestivalDay(Game1.dayOfMonth + 1, Game1.season) && Game1.random.NextDouble() < rain)
        {
            Game1.weatherForTomorrow = Game1.weather_rain;
            Game1.netWorldState.Value.GetWeatherForLocation("Default").WeatherForTomorrow = Game1.weather_rain;
        }
    }

    /// <summary>Noon: a second wind (Medicine).</summary>
    public static void OnTimeChanged(MarketState state, int time)
    {
        double share = Research.Total(state, Effect.NoonStamina);
        if (time == 1200 && share > 0)
            Game1.player.Stamina = Math.Min(Game1.player.MaxStamina, Game1.player.Stamina + (float)(Game1.player.MaxStamina * share));
    }

    /// <summary>Every machine in the valley gets a little closer to done (Chemistry).</summary>
    private static void SpeedMachines(double cut)
    {
        if (cut <= 0)
            return;
        Utility.ForEachLocation(location =>
        {
            foreach (var machine in location.objects.Values)
            {
                if (machine.bigCraftable.Value && machine.heldObject.Value != null && !machine.readyForHarvest.Value && machine.MinutesUntilReady > 10)
                    machine.MinutesUntilReady = Math.Max(10, (int)(machine.MinutesUntilReady * (1 - cut)));
            }
            return true;
        });
    }

    /// <summary>Farm animals warm up to you (Animal Science).</summary>
    private static void PetAnimals(int points)
    {
        if (points <= 0)
            return;
        Utility.ForEachLocation(location =>
        {
            foreach (var animal in location.animals.Values)
                animal.friendshipTowardFarmer.Value = Math.Min(1000, animal.friendshipTowardFarmer.Value + points);
            return true;
        });
    }

    /// <summary>Hermes Boots and Medicine: an all-day buff, renewed every morning.</summary>
    private static void Boost(double speed, double stamina)
    {
        if (speed <= 0 && stamina <= 0)
            return;
        var effects = new StardewValley.Buffs.BuffEffects();
        effects.Speed.Value = (float)speed;
        effects.MaxStamina.Value = (float)stamina;
        Game1.player.applyBuff(new Buff("evant.ValleyExchange_Science", "Valley Exchange", "Doc's research", Buff.ENDLESS, effects: effects, displayName: "Science!", description: "Doc's research at work."));
    }

    /// <summary>Some dry crops water themselves (capillary soil and friends).</summary>
    private static void SelfWater(double chance)
    {
        if (chance <= 0)
            return;
        Utility.ForEachLocation(location =>
        {
            foreach (var dirt in location.terrainFeatures.Values.OfType<HoeDirt>())
            {
                if (dirt.crop != null && dirt.state.Value == HoeDirt.dry && Game1.random.NextDouble() < chance)
                    dirt.state.Value = HoeDirt.watered;
            }
            return true;
        });
    }

    /// <summary>Spare samples: minerals and artifacts in the crate that the museum already has. Returns what they were worth.</summary>
    public static string Donate(MarketState state)
    {
        var spares = Crate.Items()
            .Where(i => i is SObject { bigCraftable.Value: false } o && (o.Category is SObject.GemCategory or SObject.mineralsCategory || o.Type == "Arch")
                && i.QualifiedItemId != "(O)74" && LibraryMuseum.HasDonatedArtifact(i.ItemId))
            .GroupBy(i => i.QualifiedItemId)
            .Select(g => (Id: g.Key, Count: g.Sum(i => i.Stack), Each: Math.Max(2, g.First().sellToStorePrice() / 10)))
            .ToList();
        int points = 0, items = 0;
        foreach (var (id, count, each) in spares)
        {
            if (!Crate.Take(id, count))
                continue;
            points += count * each;
            items += count;
        }
        points = (int)Math.Round(points * (1 + Research.Total(state, Effect.SampleBonus))); // Archaeology
        state.ResearchPoints += points;
        CrewSkills.Work(Role.Scientist, items);
        return items == 0
            ? "No spare samples in the crate, boss. Ah only take what the museum's already got."
            : $"{items} samples for the lab! That's {points:N0} research points. Now we're cookin'.";
    }

    // ---- research that counts for you too ----

    public static void Patch(string harmonyId, IMonitor log, IGameContentHelper gameContent)
    {
        monitor = log;
        content = gameContent;
        var harmony = new Harmony(harmonyId + ".science");
        harmony.Patch(AccessTools.Method(typeof(Crop), nameof(Crop.harvest)),
            prefix: new HarmonyMethod(typeof(Science), nameof(BeforeHarvest)), postfix: new HarmonyMethod(typeof(Science), nameof(AfterHarvest)));
        harmony.Patch(AccessTools.Method(typeof(Farm), nameof(Farm.addCrows)), prefix: new HarmonyMethod(typeof(Science), nameof(BeforeCrows)));
        harmony.Patch(AccessTools.Method(typeof(GameLocation), nameof(GameLocation.OnStoneDestroyed)), postfix: new HarmonyMethod(typeof(Science), nameof(AfterStone)));
        harmony.Patch(AccessTools.Method(typeof(StardewValley.Tools.FishingRod), nameof(StardewValley.Tools.FishingRod.pullFishFromWater)), prefix: new HarmonyMethod(typeof(Science), nameof(BeforeFishPulled)));
        foreach (var tool in new[] { typeof(StardewValley.Tools.Hoe), typeof(StardewValley.Tools.Axe), typeof(StardewValley.Tools.Pickaxe), typeof(StardewValley.Tools.WateringCan) })
            harmony.Patch(AccessTools.Method(tool, nameof(Tool.DoFunction)), prefix: new HarmonyMethod(typeof(Science), nameof(BeforeTool)), postfix: new HarmonyMethod(typeof(Science), nameof(AfterTool)));
        harmony.Patch(AccessTools.Method(typeof(SObject), nameof(SObject.OutputMachine)), postfix: new HarmonyMethod(typeof(Science), nameof(AfterOutputMachine)));
        harmony.Patch(AccessTools.Method(typeof(GameLocation), nameof(GameLocation.digUpArtifactSpot)), postfix: new HarmonyMethod(typeof(Science), nameof(AfterDig)));
        harmony.Patch(AccessTools.Method(typeof(Farmer), nameof(Farmer.applyBuff), new[] { typeof(Buff) }), prefix: new HarmonyMethod(typeof(Science), nameof(BeforeBuff)));
        harmony.Patch(AccessTools.Method(typeof(Farmer), nameof(Farmer.doneEating)), prefix: new HarmonyMethod(typeof(Science), nameof(BeforeDoneEating)), postfix: new HarmonyMethod(typeof(Science), nameof(AfterDoneEating)));
    }

    // ---- Engineering ----

    private static void BeforeTool(Farmer who, out float __state) => __state = who?.Stamina ?? 0;

    /// <summary>Lighter tools: part of the energy a swing cost comes back.</summary>
    private static void AfterTool(Farmer who, float __state)
    {
        if (who == Game1.player && who.Stamina < __state)
            who.Stamina += (__state - who.Stamina) * (float)Math.Min(0.8, Total(Effect.ToolEnergy));
    }

    /// <summary>Artisan goods from any machine: sometimes a star better, sometimes two of them.</summary>
    private static void AfterOutputMachine(SObject __instance, bool probe, bool __result)
    {
        try
        {
            if (probe || !__result || __instance.heldObject.Value is not { Category: SObject.artisanGoodsCategory } product)
                return;
            if (product.Quality < SObject.bestQuality && Game1.random.NextDouble() < Total(Effect.ArtisanStar))
                product.Quality = product.Quality == SObject.highQuality ? SObject.bestQuality : product.Quality + 1;
            if (Game1.random.NextDouble() < Total(Effect.DoubleArtisan))
                product.Stack++;
        }
        catch (Exception ex)
        {
            monitor?.LogOnce($"Engineering bonus skipped: {ex.Message}", LogLevel.Warn);
        }
    }

    // ---- Horticulture, Engineering: game data ----

    public static void OnAssetRequested(AssetRequestedEventArgs e)
    {
        if (State is not { } state)
            return;
        double giant = Research.Total(state, Effect.GiantCrops), discount = Math.Min(0.75, Research.Total(state, Effect.UpgradeDiscount));
        if (giant > 0 && e.NameWithoutLocale.IsEquivalentTo("Data/GiantCrops"))
        {
            e.Edit(asset =>
            {
                foreach (var crop in asset.AsDictionary<string, StardewValley.GameData.GiantCrops.GiantCropData>().Data.Values)
                    crop.Chance = (float)Math.Min(1, crop.Chance * (1 + giant));
            }, AssetEditPriority.Late);
        }
        if (discount > 0 && e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
        {
            e.Edit(asset =>
            {
                if (!asset.AsDictionary<string, StardewValley.GameData.Shops.ShopData>().Data.TryGetValue("ClintUpgrade", out var shop))
                    return;
                shop.PriceModifiers ??= new();
                shop.PriceModifiers.Add(new StardewValley.GameData.QuantityModifier
                {
                    Id = "evant.ValleyExchange_Engineering",
                    Modification = StardewValley.GameData.QuantityModifier.ModificationType.Multiply,
                    Amount = (float)(1 - discount),
                });
            }, AssetEditPriority.Late);
        }
    }

    private static void GrowTrees(double fruit, double wild)
    {
        if (fruit <= 0 && wild <= 0)
            return;
        Utility.ForEachLocation(location =>
        {
            foreach (var feature in location.terrainFeatures.Values)
            {
                if (feature is FruitTree fruitTree && Game1.random.NextDouble() < fruit)
                    fruitTree.daysUntilMature.Value--; // grows sooner; once grown, ages toward better fruit
                else if (feature is Tree { stump.Value: false } tree && tree.growthStage.Value < Tree.treeStage && Game1.random.NextDouble() < wild)
                    tree.growthStage.Value++;
            }
            return true;
        });
    }

    // ---- Archaeology ----

    /// <summary>A few more worms wiggling around the valley.</summary>
    private static void SpawnArtifactSpots(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (Game1.getLocationFromName(DigSites[Game1.random.Next(DigSites.Length)]) is not { Map: { } map } location)
                continue;
            int width = map.Layers[0].LayerWidth, height = map.Layers[0].LayerHeight;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var tile = new Vector2(Game1.random.Next(width), Game1.random.Next(height));
                if (location.doesTileHaveProperty((int)tile.X, (int)tile.Y, "Diggable", "Back") == null || !location.CanItemBePlacedHere(tile))
                    continue;
                location.objects.Add(tile, ItemRegistry.Create<SObject>("(O)590"));
                break;
            }
        }
    }

    /// <summary>Sometimes the spot had more in it.</summary>
    private static void AfterDig(GameLocation __instance, int xLocation, int yLocation, Farmer who)
    {
        if (digging || who != Game1.player || Game1.random.NextDouble() >= Total(Effect.DoubleDig))
            return;
        try
        {
            digging = true;
            __instance.digUpArtifactSpot(xLocation, yLocation, who);
        }
        catch (Exception ex)
        {
            monitor?.LogOnce($"Double dig skipped: {ex.Message}", LogLevel.Warn);
        }
        finally
        {
            digging = false;
        }
    }

    // ---- Culinary Science ----

    /// <summary>Food and drink buffs last longer.</summary>
    private static void BeforeBuff(Farmer __instance, Buff buff)
    {
        if (__instance != Game1.player || buff?.id is not ("food" or "drink") || buff.millisecondsDuration <= 0)
            return;
        float more = 1 + (float)Total(Effect.BuffDuration);
        buff.millisecondsDuration = (int)(buff.millisecondsDuration * more);
        buff.totalMillisecondsDuration = (int)(buff.totalMillisecondsDuration * more);
    }

    private static void BeforeDoneEating(Farmer __instance, out SObject? __state) => __state = __instance == Game1.player ? __instance.itemToEat as SObject : null;

    /// <summary>Food and drinks restore more.</summary>
    private static void AfterDoneEating(Farmer __instance, SObject? __state)
    {
        double more = Total(Effect.FoodRestore);
        if (__state == null || more <= 0)
            return;
        __instance.Stamina = Math.Min(__instance.MaxStamina, __instance.Stamina + (float)(Math.Max(0, __state.staminaRecoveredOnConsumption()) * more));
        __instance.health = Math.Min(__instance.maxHealth, __instance.health + (int)(Math.Max(0, __state.healthRecoveredOnConsumption()) * more));
    }

    /// <summary>Two on the line: sometimes you land a second fish (never from a pond, never a legendary).</summary>
    private static void BeforeFishPulled(bool fromFishPond, bool isBossFish, ref int numCaught)
    {
        if (!fromFishPond && !isBossFish && Game1.random.NextDouble() < Total(Effect.DoubleFish))
            numCaught++;
    }

    private static void BeforeHarvest(HoeDirt soil, out bool __state) => __state = soil?.readyForHarvest() == true;

    /// <summary>Double harvest: a second crop pops out (your harvests and the field crew's; not the Junimos', obviously).</summary>
    private static void AfterHarvest(Crop __instance, int xTile, int yTile, HoeDirt soil, JunimoHarvester junimoHarvester, bool __result, bool __state)
    {
        try
        {
            if (!__state || junimoHarvester != null || (!__result && soil.readyForHarvest()) || Game1.random.NextDouble() >= Total(Effect.DoubleHarvest))
                return;
            var extra = ItemRegistry.Create(__instance.indexOfHarvest.Value, allowNull: true);
            if (extra != null)
                Game1.createItemDebris(extra, new Vector2(xTile * 64 + 32, yTile * 64 + 32), -1, soil.Location);
        }
        catch (Exception ex)
        {
            monitor?.LogOnce($"Double harvest skipped: {ex.Message}", LogLevel.Warn);
        }
    }

    private static bool BeforeCrows() => Total(Effect.NoCrows) <= 0 && !(State is { } state && Market.IsHired(state, Role.Scareboy)); // false = skip: no crows today

    /// <summary>Extra ore from ore nodes, and coal from any stone, when you break them.</summary>
    private static void AfterStone(GameLocation __instance, string stoneId, int x, int y, Farmer who)
    {
        try
        {
            if (who == null || stoneId == null)
                return;
            string id = stoneId.Replace("(O)", "");
            if (OreOf.TryGetValue(id, out var ore) && Game1.random.NextDouble() < Total(Effect.ExtraOre))
                Game1.createObjectDebris(ore, x, y, __instance);
            if (Game1.random.NextDouble() < Total(Effect.StoneCoal))
                Game1.createObjectDebris("(O)382", x, y, __instance);
        }
        catch (Exception ex)
        {
            monitor?.LogOnce($"Geology bonus skipped: {ex.Message}", LogLevel.Warn);
        }
    }

    /// <summary>Double forage: when you pick up forage, sometimes there's a second one.</summary>
    public static void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
    {
        if (!e.IsCurrentLocation || !Context.IsWorldReady || Total(Effect.DoubleForage) <= 0)
            return;
        foreach (var (tile, obj) in e.Removed)
        {
            if (!obj.IsSpawnedObject || Vector2.Distance(Game1.player.Tile, tile) > 1.5f || Game1.random.NextDouble() >= Total(Effect.DoubleForage))
                continue;
            Game1.createItemDebris(obj.getOne(), tile * 64 + new Vector2(32, 32), -1, e.Location);
        }
    }
}
