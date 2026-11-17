using StardewValley;
using SObject = StardewValley.Object;

namespace ValleyExchange;

/// <summary>Game side of the skill trees: XP from work (capped per day), level-up notices, and the perk effects the crew code asks for.</summary>
internal static class CrewSkills
{
    private static readonly Dictionary<Role, int> WorkXpToday = new();

    /// <summary>Each morning: everyone on the payroll gets their daily XP, and today's work XP starts fresh.</summary>
    public static void NewDay(MarketState state)
    {
        WorkXpToday.Clear();
        foreach (var role in state.Crew.Keys.ToList())
            Gain(state, role, Skills.DailyXp);
    }

    /// <summary>XP for doing the job (capped per role per day, so a huge farm doesn't level in a week).</summary>
    public static void Work(Role role, int xp)
    {
        if (Workers.State is not { } state || xp <= 0)
            return;
        int used = WorkXpToday.GetValueOrDefault(role);
        int give = Math.Min(xp, Skills.DailyWorkXpCap - used);
        if (give <= 0)
            return;
        WorkXpToday[role] = used + give;
        Gain(state, role, give);
    }

    private static void Gain(MarketState state, Role role, int xp)
    {
        int level = Skills.AddXp(state, role, xp);
        if (level == 0)
            return;
        string perk = Skills.TierLevels.Contains(level) ? " New perk to pick: Hired Hands > Skills." : "";
        Game1.addHUDMessage(new HUDMessage($"{Market.RoleInfo(role).Title} reached skill level {level}!{perk}", HUDMessage.achievement_type));
        Game1.playSound("achievement");
    }

    public static double Total(Role role, PerkKind kind) => Workers.State is { } state ? Skills.Total(state, role, kind) : 0;
    public static bool Has(Role role, PerkKind kind) => Workers.State is { } state && Skills.Has(state, role, kind);
    public static int Wage(Role role, int wage) => Workers.State is { } state ? Skills.Wage(state, role, wage) : wage;

    /// <summary>Output multiplier (1 + yield).</summary>
    public static double More(Role role) => 1 + Total(role, PerkKind.Yield);
    /// <summary>Walk/work speed multiplier (1 + speed).</summary>
    public static float Faster(Role role) => 1 + (float)Total(role, PerkKind.Speed);
    /// <summary>Roll a chance-style yield perk (a bonus item, an ingredient saved).</summary>
    public static bool Bonus(Role role) => Game1.random.NextDouble() < Total(role, PerkKind.Yield);
    /// <summary>Roll the luck perk: shake off a bad day.</summary>
    public static bool Lucky(Role role) => Game1.random.NextDouble() < Total(role, PerkKind.Luck);

    /// <summary>Maybe bump an item one quality star (normal → silver → gold → iridium).</summary>
    public static void Polish(Item item, Role role)
    {
        if (item is SObject obj && obj.Quality < SObject.bestQuality && Game1.random.NextDouble() < Total(role, PerkKind.Quality))
            obj.Quality = obj.Quality == SObject.highQuality ? SObject.bestQuality : obj.Quality + 1;
    }

    /// <summary>Scale a haul's counts by the role's output.</summary>
    public static List<(string Id, int Count)> Scale(IEnumerable<(string Id, int Count)> haul, Role role) =>
        haul.Select(h => (h.Id, Math.Max(1, (int)Math.Round(h.Count * More(role))))).ToList();
}
