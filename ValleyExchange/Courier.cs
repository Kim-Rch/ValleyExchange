using StardewValley;
using SObject = StardewValley.Object;

namespace ValleyExchange;

/// <summary>
/// The courier runs gifts from the crate to the villagers while you work. Birthdays first, then whoever you're least close to.
/// He picks the cheapest thing they'll love (else like), never a neutral gift, keeps the game's gift limits, and skips anyone
/// you've already gifted today.
/// </summary>
internal static class Courier
{
    public const int GiftsPerDay = 8;

    /// <summary>Too precious to hand out, even if somebody loves it.</summary>
    private static readonly HashSet<string> Keep = new() { "(O)74", "(O)373", "(O)797", "(O)279" };

    public static string Deliver()
    {
        int max = (int)Math.Round(GiftsPerDay * CrewSkills.More(Role.Courier));
        float charm = 1 + (float)CrewSkills.Total(Role.Courier, PerkKind.Charm);
        var stock = Crate.Items().OfType<SObject>()
            .Where(o => !o.bigCraftable.Value && !Keep.Contains(o.QualifiedItemId) && o.canBeGivenAsGift())
            .GroupBy(o => o.QualifiedItemId).Select(g => g.First()).ToList();
        var delivered = new List<string>();

        var people = Game1.player.friendshipData.Pairs
            .Select(p => (Npc: Game1.getCharacterFromName(p.Key), Friend: p.Value))
            .Where(p => p.Npc != null && p.Npc.CanReceiveGifts() && !p.Friend.IsDivorced() && p.Friend.GiftsToday == 0 && (p.Friend.GiftsThisWeek < 2 || p.Npc.isBirthday()))
            .OrderByDescending(p => p.Npc.isBirthday())
            .ThenBy(p => p.Friend.Points)
            .ToList();
        foreach (var (npc, _) in people)
        {
            if (delivered.Count >= max)
                break;
            SObject? gift = null;
            foreach (int taste in new[] { NPC.gift_taste_love, NPC.gift_taste_like })
            {
                gift = stock.Where(o => Crate.Count(o.QualifiedItemId) > 0 && npc.getGiftTasteForThisItem(o) == taste).MinBy(o => o.sellToStorePrice());
                if (gift != null)
                    break;
            }
            if (gift == null || gift.getOne() is not SObject one || !Crate.Take(gift.QualifiedItemId, 1))
                continue;
            npc.receiveGift(one, Game1.player, true, charm, false);
            CrewReport.Count(Role.Courier, "gifts delivered");
            delivered.Add(npc.isBirthday() ? $"{npc.displayName} (birthday!)" : npc.displayName);
        }
        CrewSkills.Work(Role.Courier, delivered.Count * 3);
        return delivered.Count == 0
            ? "Nobody in town wants anything from the crate today, boss. Toss in some stuff they love."
            : $"Delivered {delivered.Count} gifts: {string.Join(", ", delivered.Take(5))}{(delivered.Count > 5 ? " and more" : "")}.";
    }
}
