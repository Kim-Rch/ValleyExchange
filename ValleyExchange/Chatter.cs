using StardewValley;

namespace ValleyExchange;

/// <summary>
/// Random one-liners so each hand has a personality. The crew are Brawnies: knee-high, unionized working folk from the
/// Hollow under the mountains ("no coin, no toil"), who have hated the Junimos ever since they started working for fruit.
/// </summary>
internal static class Chatter
{
    private static readonly string[] Rain = { "Rain's good for the crops.", "Soaked to the bone!", "Should've brought a hat. Wait.", "Ducks love this weather." };
    private static readonly string[] Morning = { "Mornin'!", "Coffee. Need coffee.", "Early bird gets the worm." };
    private static readonly string[] Evening = { "Almost quittin' time.", "Long day, boss.", "Supper soon?" };
    private static readonly string[] Greet = { "Howdy, boss!", "Hey there, {0}!", "Mornin', {0}.", "Lookin' sharp, boss.", "Need somethin'?" };
    private static readonly string[] Broke = { "Uh, payday's still on, right?", "Boss, your wallet looks thin.", "No coin, no toil, boss. Just sayin'." };

    /// <summary>Brawnie lore, now and then.</summary>
    private static readonly string[] Lore =
    {
        "Brawnie rule one: no coin, no toil.",
        "We're knee-high, boss. Our standards ain't.",
        "This hat ain't fashion. It's credentials.",
        "Back in the Hollow, we'd be on strike by now.",
        "My great-gran dug half the mines. Nobody wrote it down.",
        "A fair day's pay for a fair day's dig.",
        "Joja pays its people. Gotta respect that. Prices are a crime though.",
        "Brawnies don't do 'favors'. Brawnies do invoices.",
    };

    /// <summary>The Great Undercutting: three hundred years of beef with the Junimos.</summary>
    private static readonly string[] Junimos =
    {
        "Junimos work for raisins. RAISINS. Scabs, the lot of 'em.",
        "A Junimo never filed a timesheet in its life.",
        "Glowing gumdrops think they own this valley.",
        "They 'magically' fixed the bridge. We call that undercutting.",
        "Community Center? More like Junimo propaganda hut.",
        "Bet the Wizard pays 'em in hugs.",
        "Don't trust anything that hums and bounces.",
        "My grandpappy lost his job to a fuzzball.",
        "Fruit-fed freeloaders, every one of 'em.",
        "If a Junimo touches my crops, I'm filin' a grievance.",
    };

    /// <summary>When there's a Junimo hut or Junimos about.</summary>
    private static readonly string[] JunimoNearby =
    {
        "Hey! Fuzzball! This is OUR shift!",
        "Look at it. Bouncin'. Showin' off.",
        "Get off my crops, you glowing raisin!",
        "Boss, why's there a scab hut on OUR farm?",
        "I see you, gumdrop. I SEE you.",
    };

    /// <summary>Junimos on the farm: their hut, or harvesters out and about.</summary>
    private static bool JunimosAround() =>
        Game1.getFarm().buildings.Any(b => b is StardewValley.Buildings.JunimoHut)
        || Game1.currentLocation?.characters.Any(c => c is StardewValley.Characters.JunimoHarvester or StardewValley.Characters.Junimo) == true;

    /// <summary>A random line for this worker, colored by the weather and time of day.</summary>
    public static string Line(string name, Role role)
    {
        var rng = Game1.random;
        double roll = rng.NextDouble();
        if (Game1.IsRainingHere(Game1.getFarm()) && roll < 0.3)
            return Pick(Rain);
        if (Game1.timeOfDay < 900 && roll < 0.25)
            return Pick(Morning);
        if (Game1.timeOfDay >= 1700 && roll < 0.25)
            return Pick(Evening);
        if (Game1.player.Money < 500 && roll < 0.1)
            return Pick(Broke);
        // the old beef: way more of it with Junimos around
        double beef = rng.NextDouble();
        if (JunimosAround() && beef < 0.4)
            return Pick(rng.NextDouble() < 0.5 ? JunimoNearby : Junimos);
        if (beef < 0.08)
            return Pick(Junimos);
        if (beef < 0.14)
            return Pick(Lore);
        if (beef < 0.24)
            return Personas.OnJunimos(name);
        return Personas.Chat(name);
    }

    /// <summary>What they say when you walk up.</summary>
    public static string Greeting() => string.Format(Pick(Greet), Game1.player.Name);

    /// <summary>The Guild's general take on the Junimos.</summary>
    public static string JunimoLine() => Pick(Junimos);

    private static string Pick(string[] lines) => lines[Game1.random.Next(lines.Length)];
}
