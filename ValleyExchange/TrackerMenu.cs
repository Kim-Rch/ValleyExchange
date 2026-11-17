using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace ValleyExchange;

/// <summary>Where's everybody? Every hand with what they're doing and where, live, a page at a time.</summary>
internal sealed class TrackerMenu : IClickableMenu
{
    private const int RowHeight = 56, PageSize = 10, Pad = 36, Header = 44, Footer = 76, ButtonSize = 44;

    private readonly MarketState state;
    private readonly Texture2D texture;
    private ClickableComponent prev = null!, next = null!;
    private int page, mouseX, mouseY;

    public TrackerMenu(MarketState state)
    {
        this.state = state;
        this.texture = Game1.content.Load<Texture2D>(Workers.TextureAsset);
        this.Layout();
    }

    private static IReadOnlyList<Worker> Crew => Workers.Members;
    private static int Pages => Math.Max(1, (Crew.Count + PageSize - 1) / PageSize);

    private void Layout()
    {
        this.width = Math.Min(1100, Game1.uiViewport.Width - 32);
        this.height = Header + PageSize * RowHeight + Footer;
        this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
        this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2 + 32;
        this.initializeUpperRightCloseButton();
        int right = this.xPositionOnScreen + this.width - Pad, y = this.yPositionOnScreen + Header + PageSize * RowHeight + 12;
        this.prev = new ClickableComponent(new Rectangle(right - ButtonSize * 2 - 120, y, ButtonSize, ButtonSize), "<");
        this.next = new ClickableComponent(new Rectangle(right - ButtonSize, y, ButtonSize, ButtonSize), ">");
    }

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

    public override void receiveScrollWheelAction(int direction)
    {
        base.receiveScrollWheelAction(direction);
        this.Flip(direction > 0 ? -1 : 1);
    }

    private void Flip(int delta)
    {
        int old = this.page;
        this.page = Math.Clamp(this.page + delta, 0, Pages - 1);
        Game1.playSound(this.page != old ? "shwip" : "cancel");
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);
        if (Game1.activeClickableMenu != this)
            return;
        if (this.prev.containsPoint(x, y) || this.next.containsPoint(x, y))
            this.Flip(this.prev.containsPoint(x, y) ? -1 : 1);
        else if (this.isWithinBounds(x, y))
            Game1.playSound("smallSelect");
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
        drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);
        SpriteText.drawStringWithScrollCenteredAt(b, "Crew Tracker", this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen - 64);
        var font = Game1.smallFont;
        int left = this.xPositionOnScreen + Pad, top = this.yPositionOnScreen + Header - 24;
        int colStatus = left + 330, colWhere = left + this.width - Pad * 2 - 220;

        Utility.drawTextWithShadow(b, "Hand", font, new Vector2(left + 56, top - 4), Color.SaddleBrown);
        Utility.drawTextWithShadow(b, "Doing", font, new Vector2(colStatus, top - 4), Color.SaddleBrown);
        Utility.drawTextWithShadow(b, "Where", font, new Vector2(colWhere, top - 4), Color.SaddleBrown);

        if (Crew.Count == 0)
            Utility.drawTextWithShadow(b, "Nobody on the payroll yet. Hire hands from the Hired Hands menu.", font, new Vector2(left, top + 40), Game1.textColor);
        for (int i = 0; i < PageSize && this.page * PageSize + i < Crew.Count; i++)
        {
            var hand = Crew[this.page * PageSize + i];
            int y = this.yPositionOnScreen + Header + i * RowHeight;
            if (i % 2 == 0)
                b.Draw(Game1.staminaRect, new Rectangle(left - 8, y, this.width - Pad * 2 + 16, RowHeight), Color.SaddleBrown * 0.08f);
            b.Draw(this.texture, new Vector2(left + 4, y + 4), new Rectangle(hand.Variant * 64, (int)hand.Role * 128, 16, 16), Color.White, 0f, Vector2.Zero, 3f, SpriteEffects.None, 1f);
            Utility.drawTextWithShadow(b, $"{hand.Name}, {Market.RoleInfo(hand.Role).Title.ToLower()} (Lv {Skills.Level(this.state, hand.Role)})", font, new Vector2(left + 56, y + 14), Game1.textColor);
            Utility.drawTextWithShadow(b, Fit(hand.Status, colWhere - colStatus - 16), font, new Vector2(colStatus, y + 14), Game1.textColor);
            Utility.drawTextWithShadow(b, Fit(hand.Where, left + this.width - Pad * 2 - colWhere), font, new Vector2(colWhere, y + 14), Color.SaddleBrown);
        }

        int fy = this.prev.bounds.Y;
        TrackerButton(b, this.prev, this.mouseX, this.mouseY, this.page > 0);
        TrackerButton(b, this.next, this.mouseX, this.mouseY, this.page < Pages - 1);
        string pageText = $"Page {this.page + 1}/{Pages}";
        Utility.drawTextWithShadow(b, pageText, font, new Vector2((this.prev.bounds.Right + this.next.bounds.X) / 2 - font.MeasureString(pageText).X / 2, fy + 10), Color.SaddleBrown);
        Utility.drawTextWithShadow(b, $"{Crew.Count} hands on the payroll. Updates live.", font, new Vector2(left, fy + 10), Color.SaddleBrown);
        base.draw(b);
        this.drawMouse(b);
    }

    private static string Fit(string text, int width)
    {
        if (Game1.smallFont.MeasureString(text).X <= width)
            return text;
        while (text.Length > 1 && Game1.smallFont.MeasureString(text + "...").X > width)
            text = text[..^1];
        return text.TrimEnd() + "...";
    }

    private static void TrackerButton(SpriteBatch b, ClickableComponent button, int mouseX, int mouseY, bool enabled)
    {
        var r = button.bounds;
        drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9), r.X, r.Y, r.Width, r.Height, !enabled ? Color.Gray : r.Contains(mouseX, mouseY) ? Color.Wheat : Color.White, 4f, false);
        var size = Game1.smallFont.MeasureString(button.name);
        Utility.drawTextWithShadow(b, button.name, Game1.smallFont, new Vector2(r.Center.X - size.X / 2, r.Center.Y - size.Y / 2 + 2), enabled ? Game1.textColor : Color.DimGray);
    }
}
