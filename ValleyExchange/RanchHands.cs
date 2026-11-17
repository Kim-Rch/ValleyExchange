using StardewValley;
using StardewValley.GameData.FarmAnimals;

namespace ValleyExchange;

/// <summary>Hired help: every morning they pet every animal, fill the troughs from your silos, and put what the animals made in the crate.</summary>
internal static class RanchHands
{
    /// <summary>Do the morning chores if hired. Returns the animal count, or -1 if they aren't working.</summary>
    public static int Work(MarketState state)
    {
        if (!Market.IsHired(state, Role.Ranch))
            return -1;

        var animals = new List<FarmAnimal>();
        var barns = new List<AnimalHouse>();
        Utility.ForEachLocation(location =>
        {
            animals.AddRange(location.animals.Values);
            if (location is AnimalHouse house)
                barns.Add(house);
            return true;
        });

        int wage = CrewSkills.Wage(Role.Ranch, Market.RanchWage(animals.Count) + Market.ExtraHandsWage(Market.Count(state, Role.Ranch)));
        if (Game1.player.Money < wage)
        {
            Market.SetCount(state, Role.Ranch, 0);
            Game1.addHUDMessage(new HUDMessage($"Your ranch hands quit: you couldn't make payroll ({wage:N0}g).", HUDMessage.error_type));
            return -1;
        }
        Game1.player.Money -= wage;
        CrewReport.Paid(Role.Ranch, wage);

        int petted = 0, collected = 0;
        foreach (var animal in animals)
        {
            if (!animal.wasPet.Value)
            {
                animal.pet(Game1.player, is_auto_pet: false); // vanilla petting: friendship, mood, profession bonuses
                petted++;
            }
            // milk, wool, etc.; auto-grabbers already took theirs overnight
            if (animal.currentProduce.Value is { } produce && animal.GetHarvestType() == FarmAnimalHarvestType.HarvestWithTool)
            {
                var item = ItemRegistry.Create(produce, 1, animal.produceQuality.Value);
                animal.HandleStatsOnProduceCollected(item, 1);
                animal.currentProduce.Value = null;
                animal.ReloadTextureIfNeeded(false);
                collected += Produce(item);
            }
        }
        // shortcut: truffles (dig-up produce) stay outside for you or Automate to find
        foreach (var barn in barns)
        {
            foreach (var pair in barn.objects.Pairs.Where(p => p.Value.IsSpawnedObject).ToList()) // eggs, feathers, rabbit feet on the floor
            {
                barn.objects.Remove(pair.Key);
                collected += Produce(pair.Value);
            }
            barn.feedAllAnimals(); // hay from your silos into empty troughs
        }

        CrewSkills.Work(Role.Ranch, petted / 2 + collected);
        CrewReport.Count(Role.Ranch, "animals petted", petted);
        return animals.Count;
    }

    /// <summary>Into the crate, with the ranch hands' skill: maybe a better star, maybe a bonus one.</summary>
    private static int Produce(Item item)
    {
        CrewSkills.Polish(item, Role.Ranch);
        CrewReport.Got(Role.Ranch, "animal products", item);
        Crate.Store(item);
        if (!CrewSkills.Bonus(Role.Ranch))
            return 1;
        var bonus = item.getOne();
        CrewReport.Got(Role.Ranch, "animal products", bonus);
        Crate.Store(bonus);
        return 2;
    }
}
