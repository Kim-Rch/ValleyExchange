using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace ValleyExchange;

/// <summary>
/// Tallies what each role did (counts, what it cost, what it brought in) and shows it every morning on one screen, like the
/// shipping summary, instead of a pile of HUD messages. Covers yesterday after the last report plus this morning's round.
/// </summary>
internal static class CrewReport
{
    internal sealed class Tally
    {
        public readonly Dictionary<string, int> Counts = new();
        public long Cost, Goods, Cash;
        /// <summary>Line items for the detail view (e.g. every item the salesman sold, and what it went for).</summary>
        public readonly List<(string Text, long Gold)> Details = new();
    }

    private static readonly Dictionary<Role, Tally> Tallies = new();
    private static bool pending;

    private static Tally For(Role role) => Tallies.TryGetValue(role, out var t) ? t : Tallies[role] = new Tally();

    public static void Count(Role role, string what, int n = 1)
    {
        if (n <= 0)
            return;
        var counts = For(role).Counts;
        counts[what] = counts.GetValueOrDefault(what) + n;
    }

    /// <summary>A line for the role's detail view.</summary>
    public static void Detail(Role role, string text, long gold = 0) => For(role).Details.Add((text, gold));

    /// <summary>Wages and fees.</summary>
    public static void Paid(Role role, long gold)
    {
        if (gold > 0)
            For(role).Cost += gold;
    }

    /// <summary>Real gold for you (sales, gold from the mines, interest, bonuses).</summary>
    public static void Earned(Role role, long gold)
    {
        if (gold > 0)
            For(role).Cash += gold;
    }

    /// <summary>An item into the crate: counted, and its shipping value noted. Not money until it's sold.</summary>
    public static void Got(Role role, string what, Item item)
    {
        Count(role, what, item.Stack);
        For(role).Goods += Utility.getSellToStorePriceOfItem(item);
    }

    public static void Clear()
    {
        Tallies.Clear();
        pending = false;
    }

    /// <summary>After the morning round: show the report as soon as the screen is free.</summary>
    public static void Ready() => pending = Tallies.Count > 0;

    /// <summary>Called every tick: opens the report once nothing else (level-ups, events) is on screen.</summary>
    public static void TryShow(Texture2D texture)
    {
        if (!pending || !Context.IsPlayerFree || Game1.activeClickableMenu != null)
            return;
        pending = false;
        var rows = Market.Roles.Where(r => Tallies.ContainsKey(r.Role)).Select(r => (r.Role, Tallies[r.Role])).ToList();
        Tallies.Clear();
        Game1.activeClickableMenu = new CrewReportMenu(rows, texture);
    }
}

/// <summary>
/// The morning crew report: a row per role with what they did, what they cost, the goods they put in the crate (worth it once
/// sold) and the cash they actually brought in. Net is cash only, so goods the salesman sells aren't counted twice.
/// </summary>
internal sealed class CrewReportMenu : IClickableMenu
{
    private const int RowHeight = 50, Pad = 36, Header = 92, Footer = 170;
    private static readonly Color Up = new(30, 140, 30), Down = new(190, 40, 40), GoodsColor = new(150, 110, 40);

    private readonly List<(Role Role, CrewReport.Tally Tally)> rows;
    private readonly Texture2D texture;
    private readonly long cost, goods, cash;
    private readonly ClickableTextureComponent ok;
    private int ticks;
    private int detail = -1, scroll; // the row opened in the detail view (-1: the table)

    public CrewReportMenu(List<(Role Role, CrewReport.Tally Tally)> rows, Texture2D texture)
    {
        this.rows = rows;
        this.texture = texture;
        this.cost = rows.Sum(r => r.Tally.Cost);
        this.goods = rows.Sum(r => r.Tally.Goods);
        this.cash = rows.Sum(r => r.Tally.Cash);
        this.width = Math.Min(1200, Game1.uiViewport.Width - 32);
        this.height = Header + Math.Max(1, rows.Count) * RowHeight + Footer;
        this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
        this.yPositionOnScreen = Math.Max(80, (Game1.uiViewport.Height - this.height) / 2 + 32);
        this.ok = new ClickableTextureComponent(new Rectangle(this.xPositionOnScreen + this.width - Pad - 64, this.yPositionOnScreen + this.height - 84, 64, 64), Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 46), 1f);
        Game1.playSound("bigSelect");
    }

    /// <summary>The totals count up over about a second, ticking like coins.</summary>
    private double Shown => Math.Min(1.0, this.ticks / 60.0);

    public override void update(GameTime time)
    {
        base.update(time);
        if (this.ticks <= 60)
        {
            this.ticks++;
            if (this.ticks % 6 == 0 && this.ticks < 60)
                Game1.playSound("coin");
            if (this.ticks == 60)
                Game1.playSound(this.cash >= this.cost ? "money" : "cancel");
        }
    }

    private Rectangle RowBounds(int i) =>
        new(this.xPositionOnScreen + Pad - 8, this.yPositionOnScreen + Header + i * RowHeight, this.width - Pad * 2 + 16, RowHeight);

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.ticks < 60)
        {
            this.ticks = 60; // skip the count
            return;
        }
        if (this.detail >= 0)
        {
            this.detail = -1; // back to the table
            Game1.playSound("smallSelect");
            return;
        }
        for (int i = 0; i < this.rows.Count; i++)
        {
            if (this.RowBounds(i).Contains(x, y))
            {
                (this.detail, this.scroll) = (i, 0);
                Game1.playSound("bigSelect");
                return;
            }
        }
        this.exitThisMenu();
    }

    public override void receiveScrollWheelAction(int direction)
    {
        if (this.detail >= 0)
            this.scroll = Math.Max(0, this.scroll + (direction > 0 ? -2 : 2));
    }

    public override void receiveKeyPress(Microsoft.Xna.Framework.Input.Keys key)
    {
        if (this.ticks < 60)
            this.ticks = 60;
        else
            base.receiveKeyPress(key);
    }

    private int mouseX, mouseY;

    public override void performHoverAction(int x, int y)
    {
        this.ok.tryHover(x, y);
        (this.mouseX, this.mouseY) = (x, y);
    }

    private static string Fit(string text, int width)
    {
        if (Game1.smallFont.MeasureString(text).X <= width)
            return text;
        while (text.Length > 1 && Game1.smallFont.MeasureString(text + "...").X > width)
            text = text[..^1];
        return text.TrimEnd() + "...";
    }

    /// <summary>One role's full story: every count, then every line item, in two columns; scroll for more, click to go back.</summary>
    private void DrawDetail(SpriteBatch b, int left, int right)
    {
        var font = Game1.smallFont;
        var (role, tally) = this.rows[this.detail];
        b.Draw(this.texture, new Vector2(left, this.yPositionOnScreen + 14), new Rectangle(0, (int)role * 128, 16, 16), Color.White, 0f, Vector2.Zero, 2.75f, SpriteEffects.None, 1f);
        Utility.drawTextWithShadow(b, $"{Market.RoleInfo(role).Title}: the full story", Game1.dialogueFont, new Vector2(left + 56, this.yPositionOnScreen + 12), Game1.textColor);
        var lines = tally.Counts.Select(kv => ($"{kv.Value:N0} {kv.Key}", 0L)).Concat(tally.Details).ToList();
        if (tally.Cost > 0)
            lines.Add(("Wages & fees", -tally.Cost));
        int top = this.yPositionOnScreen + 76, lineHeight = 32, perColumn = Math.Max(1, (this.yPositionOnScreen + this.height - 110 - top) / lineHeight);
        this.scroll = Math.Min(this.scroll, Math.Max(0, lines.Count - perColumn * 2));
        int columnWidth = (right - left - 40) / 2;
        for (int i = 0; i < perColumn * 2 && this.scroll + i < lines.Count; i++)
        {
            var (text, gold) = lines[this.scroll + i];
            int x = left + (i / perColumn) * (columnWidth + 40), y = top + (i % perColumn) * lineHeight;
            string amount = gold > 0 ? $"{gold:N0}g" : gold < 0 ? $"-{-gold:N0}g" : "";
            Utility.drawTextWithShadow(b, Fit(text, columnWidth - (int)font.MeasureString(amount).X - 16), font, new Vector2(x, y), Game1.textColor);
            if (amount != "")
                Right(b, amount, x + columnWidth, y, gold > 0 ? Up : Down);
        }
        string footer = lines.Count > perColumn * 2
            ? $"Showing {this.scroll + 1}-{Math.Min(lines.Count, this.scroll + perColumn * 2)} of {lines.Count}. Scroll for more, click to go back."
            : "Click to go back.";
        Utility.drawTextWithShadow(b, footer, font, new Vector2(left, this.yPositionOnScreen + this.height - 70), Color.SaddleBrown);
    }

    /// <summary>A number right-aligned to a column's right edge.</summary>
    private static void Right(SpriteBatch b, string text, int right, int y, Color color) =>
        Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(right - Game1.smallFont.MeasureString(text).X, y), color);

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
        drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);
        SpriteText.drawStringWithScrollCenteredAt(b, "Crew Report", this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen - 64);
        var font = Game1.smallFont;
        int left = this.xPositionOnScreen + Pad, right = this.xPositionOnScreen + this.width - Pad;
        if (this.detail >= 0)
        {
            this.DrawDetail(b, left, right);
            this.ok.draw(b);
            this.drawMouse(b);
            return;
        }
        int colCash = right, colGoods = right - 150, colCost = right - 300; // right edges of the number columns
        int titleWidth = this.rows.Count == 0 ? 120 : (int)this.rows.Max(r => font.MeasureString(Market.RoleInfo(r.Role).Title).X);
        int didX = left + 56 + titleWidth + 24, didWidth = colCost - 110 - didX;

        Utility.drawTextWithShadow(b, $"Yesterday's work and this morning's chores, {Game1.Date.Localize()}. Click a row for details.", font, new Vector2(left, this.yPositionOnScreen + 16), Color.SaddleBrown);
        int hy = this.yPositionOnScreen + 52;
        Right(b, "Cost", colCost, hy, Color.SaddleBrown);
        Right(b, "Goods", colGoods, hy, Color.SaddleBrown);
        Right(b, "Cash", colCash, hy, Color.SaddleBrown);

        for (int i = 0; i < this.rows.Count; i++)
        {
            var (role, tally) = this.rows[i];
            int y = this.yPositionOnScreen + Header + i * RowHeight;
            if (this.ticks >= 60 && this.RowBounds(i).Contains(this.mouseX, this.mouseY))
                b.Draw(Game1.staminaRect, this.RowBounds(i), Color.SaddleBrown * 0.2f);
            else if (i % 2 == 0)
                b.Draw(Game1.staminaRect, new Rectangle(left - 8, y, this.width - Pad * 2 + 16, RowHeight), Color.SaddleBrown * 0.08f);
            b.Draw(this.texture, new Vector2(left, y + 4), new Rectangle(0, (int)role * 128, 16, 16), Color.White, 0f, Vector2.Zero, 2.75f, SpriteEffects.None, 1f);
            Utility.drawTextWithShadow(b, Market.RoleInfo(role).Title, font, new Vector2(left + 56, y + 11), Game1.textColor);
            string did = string.Join(", ", tally.Counts.Select(kv => $"{kv.Value:N0} {kv.Key}"));
            Utility.drawTextWithShadow(b, Fit(did, didWidth), font, new Vector2(didX, y + 11), Game1.textColor * 0.85f);
            if (tally.Cost > 0)
                Right(b, $"-{tally.Cost:N0}g", colCost, y + 11, Down);
            if (tally.Goods > 0)
                Right(b, $"{tally.Goods:N0}g", colGoods, y + 11, GoodsColor);
            if (tally.Cash > 0)
                Right(b, $"+{tally.Cash:N0}g", colCash, y + 11, Up);
        }

        // totals, counting up: net is real cash only
        int fy = this.yPositionOnScreen + Header + Math.Max(1, this.rows.Count) * RowHeight + 14;
        double k = this.Shown;
        long shownCost = (long)(this.cost * k), shownCash = (long)(this.cash * k), net = shownCash - shownCost;
        Utility.drawTextWithShadow(b, "Wages & fees", font, new Vector2(left, fy), Game1.textColor);
        Right(b, $"-{shownCost:N0}g", colCost, fy, Down);
        Utility.drawTextWithShadow(b, "Cash in (sales, gold, interest)", font, new Vector2(left, fy + 30), Game1.textColor);
        Right(b, $"+{shownCash:N0}g", colCash, fy + 30, Up);
        Utility.drawTextWithShadow(b, "Goods put in the crate (worth this once sold)", font, new Vector2(left, fy + 60), Game1.textColor * 0.8f);
        Right(b, $"{(long)(this.goods * k):N0}g", colGoods, fy + 60, GoodsColor);
        b.Draw(Game1.staminaRect, new Rectangle(left, fy + 94, this.width - Pad * 2, 3), Color.SaddleBrown * 0.4f);
        Utility.drawTextWithShadow(b, $"Net cash: {(net >= 0 ? "+" : "")}{net:N0}g", Game1.dialogueFont, new Vector2(left, fy + 104), net >= 0 ? Up : Down);
        this.ok.draw(b);
        this.drawMouse(b);
    }
}
