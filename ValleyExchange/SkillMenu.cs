using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace ValleyExchange;

/// <summary>The crew's skill trees: pick a role on the left, see its level and passives, and choose a perk at levels 5/10/15/20/25.</summary>
internal sealed class SkillMenu : IClickableMenu
{
    private const int Pad = 32, ListWidth = 300, TierRow = 84, TopBlock = 120;
    /// <summary>Role list rows shrink to fit every role on screen.</summary>
    private static int ListRow => Math.Clamp((Game1.uiViewport.Height - 180) / Market.Roles.Count, 30, 46);
    private static readonly Color Up = new(30, 140, 30), Down = new(190, 40, 40);

    private readonly MarketState state;
    private readonly Texture2D texture;
    private readonly List<ClickableComponent> roles = new();
    private readonly List<ClickableComponent> cards = new(); // tier * 2 + option
    private Role selected;
    private string status = "Hands earn XP every day they work. Perks unlock every 5 levels.";
    private Color statusColor = Game1.textColor;
    private int mouseX, mouseY;

    public SkillMenu(MarketState state)
    {
        this.state = state;
        this.texture = Game1.content.Load<Texture2D>(Workers.TextureAsset);
        this.selected = Market.Roles.Select(r => r.Role).FirstOrDefault(r => Skills.Unpicked(state, r) > 0 && Market.IsHired(state, r),
            Market.Roles.Select(r => r.Role).FirstOrDefault(r => Market.IsHired(state, r), Role.Field)); // jump to a waiting perk
        this.Layout();
    }

    private void Layout()
    {
        this.width = Math.Min(1150, Game1.uiViewport.Width - 32);
        this.height = Math.Max(Market.Roles.Count * ListRow, TopBlock + 5 * TierRow) + Pad * 2 + 40;
        this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
        this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2 + 32;
        this.initializeUpperRightCloseButton();
        this.roles.Clear();
        this.cards.Clear();
        int left = this.xPositionOnScreen + Pad, top = this.yPositionOnScreen + Pad;
        for (int i = 0; i < Market.Roles.Count; i++)
            this.roles.Add(new ClickableComponent(new Rectangle(left, top + i * ListRow, ListWidth, ListRow - 4), ""));
        int panel = left + ListWidth + 24, panelWidth = this.xPositionOnScreen + this.width - Pad - panel;
        int cardWidth = (panelWidth - 90 - 12) / 2;
        for (int tier = 0; tier < 5; tier++)
        {
            int y = top + TopBlock + tier * TierRow;
            for (int option = 0; option < 2; option++)
                this.cards.Add(new ClickableComponent(new Rectangle(panel + 90 + option * (cardWidth + 12), y, cardWidth, TierRow - 10), ""));
        }
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        this.Layout();
    }

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        this.mouseX = x;
        this.mouseY = y;
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);
        if (Game1.activeClickableMenu != this)
            return;
        for (int i = 0; i < this.roles.Count; i++)
        {
            if (!this.roles[i].containsPoint(x, y))
                continue;
            this.selected = Market.Roles[i].Role;
            Game1.playSound("smallSelect");
            return;
        }
        for (int i = 0; i < this.cards.Count; i++)
        {
            if (!this.cards[i].containsPoint(x, y))
                continue;
            int tier = i / 2, option = i % 2;
            var perk = Skills.Tree(this.selected)[tier][option];
            int level = Skills.Level(this.state, this.selected);
            if (Skills.Pick(this.state, this.selected, tier) != -1)
                this.Fail("Already chosen. Perks are for keeps.");
            else if (level < Skills.TierLevels[tier])
                this.Fail($"Unlocks at level {Skills.TierLevels[tier]} (now {level}).");
            else if (Skills.Choose(this.state, this.selected, tier, option))
            {
                Game1.playSound("achievement");
                this.Say($"{Market.RoleInfo(this.selected).Title} learned {perk.Name}!", Up);
            }
            return;
        }
        if (this.isWithinBounds(x, y))
            Game1.playSound("smallSelect");
    }

    private void Say(string text, Color color) => (this.status, this.statusColor) = (text, color);

    private void Fail(string text)
    {
        Game1.playSound("cancel");
        this.Say(text, Down);
    }

    /// <summary>One line, trimmed with "..." if it would spill out of the card.</summary>
    private static string Fit(string text, int width)
    {
        if (Game1.smallFont.MeasureString(text).X <= width)
            return text;
        while (text.Length > 1 && Game1.smallFont.MeasureString(text + "...").X > width)
            text = text[..^1];
        return text.TrimEnd() + "...";
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
        drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);
        SpriteText.drawStringWithScrollCenteredAt(b, "Crew Skills", this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen - 64);
        var font = Game1.smallFont;

        // role list
        for (int i = 0; i < Market.Roles.Count; i++)
        {
            var info = Market.Roles[i];
            var r = this.roles[i].bounds;
            bool hired = Market.IsHired(this.state, info.Role);
            if (info.Role == this.selected)
                b.Draw(Game1.staminaRect, r, Color.SaddleBrown * 0.25f);
            else if (r.Contains(this.mouseX, this.mouseY))
                b.Draw(Game1.staminaRect, r, Color.SaddleBrown * 0.12f);
            b.Draw(this.texture, new Vector2(r.X + 4, r.Y + 2), new Rectangle(0, (int)info.Role * 128, 16, 16), Color.White * (hired ? 1f : 0.45f), 0f, Vector2.Zero, Math.Min(2.5f, (ListRow - 4) / 16f), SpriteEffects.None, 1f);
            Utility.drawTextWithShadow(b, info.Title, font, new Vector2(r.X + 52, r.Y + 8), hired ? Game1.textColor : Color.DimGray);
            string level = $"Lv {Skills.Level(this.state, info.Role)}";
            Utility.drawTextWithShadow(b, level, font, new Vector2(r.Right - font.MeasureString(level).X - (Skills.Unpicked(this.state, info.Role) > 0 ? 34 : 6), r.Y + 8), Color.SaddleBrown);
            if (Skills.Unpicked(this.state, info.Role) > 0)
                Utility.drawTextWithShadow(b, "!", Game1.dialogueFont, new Vector2(r.Right - 22, r.Y - 4), Down); // a perk is waiting
        }

        // selected role: level, XP bar, passives
        var role = Market.RoleInfo(this.selected);
        int panel = this.roles[0].bounds.Right + 24, top = this.yPositionOnScreen + Pad, panelWidth = this.xPositionOnScreen + this.width - Pad - panel;
        int lvl = Skills.Level(this.state, this.selected), xp = Skills.Xp(this.state, this.selected);
        Utility.drawTextWithShadow(b, $"{role.Title}: level {lvl}/{Skills.MaxLevel}", Game1.dialogueFont, new Vector2(panel, top - 4), Game1.textColor);
        float fill = lvl >= Skills.MaxLevel ? 1f : (xp - Skills.XpFor(lvl)) / (float)(Skills.XpFor(lvl + 1) - Skills.XpFor(lvl));
        var bar = new Rectangle(panel, top + 50, panelWidth, 18);
        b.Draw(Game1.staminaRect, bar, Color.SaddleBrown * 0.3f);
        b.Draw(Game1.staminaRect, new Rectangle(bar.X, bar.Y, (int)(bar.Width * fill), bar.Height), Up);
        string xpText = lvl >= Skills.MaxLevel ? "MAX" : $"{xp - Skills.XpFor(lvl):N0}/{Skills.XpFor(lvl + 1) - Skills.XpFor(lvl):N0} XP";
        Utility.drawTextWithShadow(b, xpText, font, new Vector2(bar.Right - font.MeasureString(xpText).X, bar.Bottom + 4), Color.SaddleBrown);
        Utility.drawTextWithShadow(b, $"Passive: wages -{Skills.Pct(0.005 * lvl)}, output +{Skills.Pct(0.01 * lvl)}", font, new Vector2(panel, bar.Bottom + 4), Color.SaddleBrown);

        // perk tiers
        for (int tier = 0; tier < 5; tier++)
        {
            int y = top + TopBlock + tier * TierRow;
            bool unlocked = lvl >= Skills.TierLevels[tier];
            int pick = Skills.Pick(this.state, this.selected, tier);
            Utility.drawTextWithShadow(b, $"Lv {Skills.TierLevels[tier]}", font, new Vector2(panel, y + 24), unlocked ? Game1.textColor : Color.DimGray);
            for (int option = 0; option < 2; option++)
            {
                var card = this.cards[tier * 2 + option].bounds;
                var perk = Skills.Tree(this.selected)[tier][option];
                bool chosen = pick == option, open = unlocked && pick == -1, hover = open && card.Contains(this.mouseX, this.mouseY);
                // chosen: green; can pick now: bright; locked or passed over: faded but readable
                Color tint = chosen ? Color.LightGreen : open ? (hover ? Color.Wheat : Color.White) : Color.White * 0.45f;
                drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9), card.X, card.Y, card.Width, card.Height, tint, 4f, false);
                var textColor = chosen || open ? Game1.textColor : Game1.textColor * 0.6f;
                Utility.drawTextWithShadow(b, Fit(perk.Name + (chosen ? " (chosen)" : ""), card.Width - 32), font, new Vector2(card.X + 16, card.Y + 10), textColor);
                Utility.drawTextWithShadow(b, Fit(perk.Text, card.Width - 32), font, new Vector2(card.X + 16, card.Y + 40), textColor * 0.9f);
            }
        }

        Utility.drawTextWithShadow(b, this.status, font, new Vector2(panel, this.yPositionOnScreen + this.height - Pad - 28), this.statusColor);
        base.draw(b);
        this.drawMouse(b);
    }
}
