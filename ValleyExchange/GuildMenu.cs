using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace ValleyExchange;

/// <summary>
/// The guild ledger, or the pirate fleet's ship's log (same layout, sea words). Left: the party (click one), their upgrades (a button, then exactly what it needs and whether you have it),
/// and the hiring board. Right: the endless dungeon and its odds, the hall, the latest runs. Laid out top to bottom each frame,
/// so it fits small screens and long text wraps instead of overlapping.
/// </summary>
internal sealed class GuildMenu : IClickableMenu
{
    private const int Pad = 32, Row = 40, ButtonWidth = 130, ButtonHeight = 38, LineHeight = 26;
    private static readonly Color Up = new(30, 140, 30), Down = new(190, 40, 40), Faded = Color.DimGray;

    private readonly MarketState state;
    private readonly bool fleet;
    private int selected;
    private string status = "Click a party member to upgrade them. Hover anything for details.";
    private Color statusColor = Game1.textColor;
    private int mouseX, mouseY;

    // where things were drawn last frame (clicks use these)
    private readonly List<Rectangle> memberRects = new(), upgradeRects = new(), hireRects = new();
    private Rectangle dismissRect, hallRect;

    private GuildState G => GuildPlay.Of(this.state, this.fleet);
    private string Noun => this.fleet ? "pirate" : "hero";
    private int Today => Game1.Date.TotalDays;
    private GuildMember? Picked => this.selected >= 0 && this.selected < this.G.Members.Count ? this.G.Members[this.selected] : null;

    public GuildMenu(MarketState state, bool fleet = false)
    {
        this.state = state;
        this.fleet = fleet;
        this.Layout();
        Game1.playSound("bigSelect");
    }

    private void Layout()
    {
        this.width = Math.Min(1240, Game1.uiViewport.Width - 32);
        this.height = Math.Min(Game1.uiViewport.Height - 100, 800);
        this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
        this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2 + 30;
        this.initializeUpperRightCloseButton();
    }

    private int LeftWidth => (this.width - Pad * 3) * 54 / 100;

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        this.Layout();
    }

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        (this.mouseX, this.mouseY) = (x, y);
    }

    private void Say(string text, bool good)
    {
        (this.status, this.statusColor) = (text, good ? Up : Down);
        Game1.playSound(good ? "purchaseClick" : "cancel");
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);
        if (Game1.activeClickableMenu != this)
            return;
        for (int i = 0; i < this.memberRects.Count; i++)
        {
            if (i < this.G.Members.Count && this.memberRects[i].Contains(x, y))
            {
                this.selected = i;
                Game1.playSound("smallSelect");
                return;
            }
        }
        if (this.Picked is { } m)
        {
            if (this.dismissRect.Contains(x, y))
            {
                if (GuildPlay.Dismiss(this.state, m, this.fleet) is { } why)
                {
                    this.Say(why, false);
                    return;
                }
                this.selected = Math.Min(this.selected, this.G.Members.Count - 1);
                this.Say($"{m.Name} hung up the {Guild.Title(m, this.fleet).ToLower()} gear. Fare well.", true);
                return;
            }
            for (int i = 0; i < this.upgradeRects.Count; i++)
            {
                if (!this.upgradeRects[i].Contains(x, y))
                    continue;
                int level = m.Level, gear = m.Gear;
                bool promoted = m.Promoted;
                string result = i switch
                {
                    0 => GuildPlay.LevelUp(this.state, m, this.fleet),
                    1 => GuildPlay.Forge(this.state, m, this.fleet),
                    _ => GuildPlay.Promote(this.state, m, this.fleet),
                };
                this.Say(result, m.Level != level || m.Gear != gear || m.Promoted != promoted);
                return;
            }
        }
        for (int i = 0; i < this.hireRects.Count; i++)
        {
            if (!this.hireRects[i].Contains(x, y))
                continue;
            int before = this.G.Members.Count;
            string result = GuildPlay.Hire(this.state, Guild.ClassesFor(this.fleet)[i].Class, this.fleet);
            if (this.G.Members.Count > before)
                this.selected = this.G.Members.Count - 1;
            this.Say(result, this.G.Members.Count > before);
            return;
        }
        if (this.hallRect.Contains(x, y))
        {
            int before = this.G.HallLevel;
            this.Say(GuildPlay.UpgradeHall(this.state, this.fleet), this.G.HallLevel > before);
        }
    }

    private bool Hover(Rectangle r) => r.Contains(this.mouseX, this.mouseY);

    /// <summary>The fleet's line: at sea till when, or when it sails and for how long; and what a loss costs.</summary>
    private string Voyage()
    {
        var g = this.G;
        int days = Guild.Voyage((int)Research.Total(this.state, Effect.FleetSpeed));
        string when = g.AtSea ? $"At sea: back on {GuildPlay.Date(g.ReturnDay)} ({g.ReturnDay - this.Today} day(s)). No changes till then."
            : g.LastPort >= this.Today ? $"In port today. Sails tomorrow at 6pm for {days} days."
            : $"In port. Sails at 6pm for {days} days.";
        return $"{when} One stage a voyage. A win pays big and every sailor gains a level; a loss pays nothing and can hurt the crew for 4-10 days.";
    }

    private static void DrawButton(SpriteBatch b, Rectangle r, string text, bool enabled, bool hover)
    {
        drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9), r.X, r.Y, r.Width, r.Height, enabled ? (hover ? Color.Wheat : Color.White) : Color.White * 0.45f, 4f, false);
        var size = Game1.smallFont.MeasureString(text);
        Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(r.Center.X - size.X / 2, r.Center.Y - size.Y / 2 + 2), enabled ? Game1.textColor : Faded);
    }

    /// <summary>Text wrapped to a width; returns how tall it came out.</summary>
    private static int Wrapped(SpriteBatch b, string text, int x, int y, int width, Color color)
    {
        string wrapped = Game1.parseText(text, Game1.smallFont, width);
        Utility.drawTextWithShadow(b, wrapped, Game1.smallFont, new Vector2(x, y), color);
        return Math.Max(1, wrapped.Split('\n').Length) * LineHeight;
    }

    private static string Cut(string text, int width)
    {
        while (text.Length > 3 && Game1.smallFont.MeasureString(text).X > width)
            text = text[..^2] + "…";
        return text;
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
        drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);
        SpriteText.drawStringWithScrollCenteredAt(b, this.fleet ? "The Fleet" : "Adventurers' Guild", this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen - 64);
        var font = Game1.smallFont;
        var g = this.G;
        int left = this.xPositionOnScreen + Pad, top = this.yPositionOnScreen + Pad, w = this.LeftWidth;
        int bottom = this.yPositionOnScreen + this.height - Pad - 2 * LineHeight - 8; // the status lines live below this
        string? hoverText = null;

        // ---- the party: one row per slot ----
        int y = top;
        Utility.drawTextWithShadow(b, $"{(this.fleet ? "Crew" : "Party")} {g.Members.Count}/{Guild.Slots(g.HallLevel)}   wages {Guild.Wages(g):N0}g a day", font, new Vector2(left, y), Color.SaddleBrown);
        y += 34;
        this.memberRects.Clear();
        for (int i = 0; i < Guild.Slots(g.HallLevel); i++)
        {
            var r = new Rectangle(left, y, w, Row - 4);
            this.memberRects.Add(r);
            if (i < g.Members.Count)
            {
                var m = g.Members[i];
                if (i == this.selected)
                    b.Draw(Game1.staminaRect, r, Color.SaddleBrown * 0.25f);
                else if (this.Hover(r))
                    b.Draw(Game1.staminaRect, r, Color.SaddleBrown * 0.12f);
                string stats = m.Injured(this.Today) ? $"injured, back in {m.InjuredUntil - this.Today}d" : $"Lv {m.Level}   Gear {m.Gear}   Power {Guild.MemberPower(m):N0}";
                float statsWidth = font.MeasureString(stats).X;
                Utility.drawTextWithShadow(b, Cut($"{m.Name} the {Guild.Title(m, this.fleet)}", w - (int)statsWidth - 30), font, new Vector2(r.X + 8, r.Y + 5), m.Promoted ? Color.DarkGoldenrod : Game1.textColor);
                Utility.drawTextWithShadow(b, stats, font, new Vector2(r.Right - statsWidth - 8, r.Y + 5), m.Injured(this.Today) ? Down : Color.SaddleBrown);
            }
            else
                Utility.drawTextWithShadow(b, $"- empty slot: hire a {this.Noun} below -", font, new Vector2(r.X + 8, r.Y + 5), Faded);
            y += Row;
        }
        y += 6;
        b.Draw(Game1.staminaRect, new Rectangle(left, y, w, 2), Color.SaddleBrown * 0.3f);
        y += 12;

        // ---- the picked member's upgrades: [button] what it needs (wrapped), green if you have it all ----
        this.upgradeRects.Clear();
        this.dismissRect = Rectangle.Empty;
        if (this.Picked is { } p)
        {
            var def = Guild.Def(p.Class, this.fleet);
            Utility.drawTextWithShadow(b, $"Upgrade {p.Name} the {Guild.Title(p, this.fleet)}", font, new Vector2(left, y + 4), Game1.textColor);
            this.dismissRect = new Rectangle(left + w - 120, y - 2, 120, 34);
            DrawButton(b, this.dismissRect, "Dismiss", true, this.Hover(this.dismissRect));
            if (this.Hover(this.dismissRect))
                hoverText = $"Let {p.Name} go. Their levels and gear go with them.";
            y += 42;

            long money = Game1.player.Money, train = GuildPlay.TrainCost(this.state, p, this.fleet), gearGold = Guild.GearGold(p.Gear);
            var (gearId, gearCount) = Guild.GearItem(p.Gear);
            int haveGear = Crate.Count(gearId), haveDiamond = Crate.Count(Guild.PromoteItem);
            string gearName = ItemRegistry.GetDataOrErrorItem(gearId).DisplayName;
            var ups = new (string Button, string Needs, bool Done, bool Can, string Hover)[]
            {
                ("Train", p.Level >= Guild.MaxLevel ? "Max level reached." : $"to level {p.Level + 1}: {train:N0}g",
                    p.Level >= Guild.MaxLevel, money >= train,
                    "Training: +25% of the class's base power per level. A bit pricier each level."),
                ("Forge", p.Gear >= Guild.MaxGear ? "Best gear there is." : $"gear tier {p.Gear + 1}: {gearCount} {gearName} + {gearGold:N0}g (crate has {haveGear} {gearName})",
                    p.Gear >= Guild.MaxGear, haveGear >= gearCount && money >= gearGold,
                    "Gear: +35% power a tier. Tiers 1-2 Copper Bars, 3-4 Iron, 5-6 Gold, 7-8 Iridium, 9 Radioactive, 10 a Prismatic Shard. Bars come from the crate."),
                ("Promote", p.Promoted ? $"Already a {def.Promoted}." : $"to {def.Promoted}: level {Guild.PromoteLevel} (now {p.Level}), {Guild.PromoteGold:N0}g, 1 Diamond (crate has {haveDiamond})",
                    p.Promoted, p.Level >= Guild.PromoteLevel && money >= Guild.PromoteGold && haveDiamond >= 1,
                    $"Promotion: x1.5 power and the title {def.Promoted}. Once per {this.Noun}."),
            };
            foreach (var (button, needs, done, can, hover) in ups)
            {
                var btn = new Rectangle(left, y, ButtonWidth, ButtonHeight);
                this.upgradeRects.Add(done ? Rectangle.Empty : btn);
                DrawButton(b, btn, button, !done && can, !done && this.Hover(btn));
                int textHeight = Wrapped(b, needs, left + ButtonWidth + 12, y + 6, w - ButtonWidth - 12, done ? Faded : can ? Up : Down);
                int rowHeight = Math.Max(ButtonHeight + 6, textHeight + 12);
                if (this.Hover(new Rectangle(left, y, w, rowHeight)))
                    hoverText = hover;
                y += rowHeight;
            }
        }
        else
        {
            Utility.drawTextWithShadow(b, g.Members.Count == 0 ? $"Hire your first {this.Noun} below." : "Click a member above to see their upgrades.", font, new Vector2(left, y + 4), Faded);
            y += 42;
        }
        y += 8;

        // ---- hiring: 4 a row ----
        bool open = Market.IsHired(this.state, GuildPlay.Leader(this.fleet)), room = open && g.Members.Count < Guild.Slots(g.HallLevel);
        Utility.drawTextWithShadow(b, room ? $"Hire a {this.Noun} (hover for what they do):" : open ? (this.fleet ? "Every berth's taken. Refit the ship for more." : "Party's full. Upgrade the hall for more slots.")
            : this.fleet ? "Hire a Pirate Captain first." : "Hire a Guild Master first.", font, new Vector2(left, y), Color.SaddleBrown);
        y += 32;
        this.hireRects.Clear();
        int quarter = (w - 24) / 4;
        var classes = Guild.ClassesFor(this.fleet);
        for (int i = 0; i < classes.Count; i++)
        {
            var r = new Rectangle(left + i % 4 * (quarter + 8), y + i / 4 * (ButtonHeight + 6), quarter, ButtonHeight);
            this.hireRects.Add(room ? r : Rectangle.Empty);
            DrawButton(b, r, classes[i].Title, room, this.Hover(r));
            if (this.Hover(r))
                hoverText = $"{classes[i].Title}, {classes[i].Hire:N0}g to sign: {classes[i].Blurb}";
        }

        // ---- the dungeon ----
        int x = left + w + Pad, rw = this.xPositionOnScreen + this.width - Pad - x;
        b.Draw(Game1.staminaRect, new Rectangle(x - Pad / 2, top, 2, bottom - top), Color.SaddleBrown * 0.3f);
        double power = Guild.Power(g, this.Today, GuildPlay.PowerBonus(this.state, this.fleet));
        double win = Guild.WinChance(power, g.Stage);
        int ry = top - 8;
        Utility.drawTextWithShadow(b, this.fleet ? "The Endless Sea" : "The Endless Dungeon", Game1.dialogueFont, new Vector2(x, ry), Game1.textColor);
        ry += 52;
        var lines = new (string Text, Color Color)[]
        {
            ($"Next stage: {Guild.Label(g.Stage)}   Record: {(g.Best == 0 ? "none yet" : Guild.Label(g.Best - 1))}", Game1.textColor),
            ($"{(this.fleet ? "Crew" : "Party")} power {power:N0} vs difficulty {Guild.Difficulty(g.Stage):N0}", Game1.textColor),
            ($"Win chance: {Guild.Odds(win)}", win >= 0.5 ? Up : Down),
            ($"Then: {string.Join("  ", Enumerable.Range(1, 3).Select(k => $"{Guild.Label(g.Stage + k)} {Guild.Odds(Guild.WinChance(power, g.Stage + k))}"))}", Color.SaddleBrown),
            this.fleet ? (this.Voyage(), Color.SaddleBrown)
            : ($"{Guild.Attempts(g.HallLevel) + (int)Research.Total(this.state, Effect.GuildRuns)} runs a day at 6pm. "
                + (Guild.FarmStage(power, g.Stage) is var farm and >= 0
                    ? $"If they lose, they farm {Guild.Label(farm)} ({Guild.Odds(Guild.WinChance(power, farm))}) for the rest of the day."
                    : "If they lose, the day's over."), Color.SaddleBrown),
        };
        foreach (var (text, color) in lines)
            ry += Wrapped(b, text, x, ry, rw, color) + 4;

        // ---- the hall ----
        ry += 10;
        ry += Wrapped(b, this.fleet ? $"Ship {g.HallLevel}/{Guild.MaxHall}: +{g.HallLevel * 5}% crew power, {Guild.Slots(g.HallLevel)} berths"
            : $"Guild Hall {g.HallLevel}/{Guild.MaxHall}: +{g.HallLevel * 5}% party power, {Guild.Slots(g.HallLevel)} slots", x, ry, rw, Game1.textColor) + 4;
        this.hallRect = new Rectangle(x, ry, rw, ButtonHeight + 4);
        bool maxed = g.HallLevel >= Guild.MaxHall;
        DrawButton(b, this.hallRect, maxed ? (this.fleet ? "Ship's maxed" : "Hall's maxed") : $"{(this.fleet ? "Refit ship" : "Upgrade hall")}: {Guild.HallCost(g.HallLevel):N0}g", !maxed, this.Hover(this.hallRect));
        if (this.Hover(this.hallRect) && !maxed)
            hoverText = this.fleet ? $"Next ship: {Guild.Slots(g.HallLevel + 1)} berths, +{(g.HallLevel + 1) * 5}% crew power."
                : $"Next hall: {Guild.Slots(g.HallLevel + 1)} party slots, {Guild.Attempts(g.HallLevel + 1)} runs a day, +{(g.HallLevel + 1) * 5}% party power.";
        ry += ButtonHeight + 18;

        // ---- the log ----
        Utility.drawTextWithShadow(b, this.fleet ? "Latest voyages" : "Latest runs", font, new Vector2(x, ry), Color.SaddleBrown);
        ry += 30;
        if (g.Log.Count == 0)
            Utility.drawTextWithShadow(b, this.fleet ? "No voyages yet. They sail at 6pm." : "No runs yet. They head out at 6pm.", font, new Vector2(x, ry), Faded);
        foreach (string entry in g.Log)
        {
            if (ry + LineHeight > bottom)
                break;
            Utility.drawTextWithShadow(b, Cut(entry, rw), font, new Vector2(x, ry), entry.Contains("LOST") ? Down : Game1.textColor);
            ry += LineHeight;
        }

        // ---- status / hover help, wrapped across the bottom ----
        b.Draw(Game1.staminaRect, new Rectangle(left, bottom + 2, this.width - Pad * 2, 2), Color.SaddleBrown * 0.3f);
        Wrapped(b, hoverText ?? this.status, left, bottom + 10, this.width - Pad * 2, hoverText != null ? Game1.textColor : this.statusColor);
        base.draw(b);
        this.drawMouse(b);
    }
}
