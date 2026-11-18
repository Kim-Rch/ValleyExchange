using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace ValleyExchange;

/// <summary>
/// The shopkeeper's shopping list: every seed the shops sell this season, its price, what a tile of it earns by season's end,
/// what's in the crate, and how many you want her to keep there (-/+ 10, shift for 50). Her pick for the empty soil is starred.
/// </summary>
internal sealed class ShopListMenu : IClickableMenu
{
    private const int Pad = 32, Row = 44, Header = 96, Footer = 60, Button = 36;
    private static readonly Color Up = new(30, 140, 30), Down = new(190, 40, 40);

    private readonly MarketState state;
    private readonly List<Market.SeedOffer> offers;
    private readonly HashSet<string> picks;
    private int scroll, mouseX, mouseY;
    private Rectangle autoRect;

    public ShopListMenu(MarketState state)
    {
        this.state = state;
        this.offers = Shopkeeper.Offers().OrderBy(o => ItemRegistry.GetDataOrErrorItem(o.Id).DisplayName).ToList();
        this.picks = Market.SeedPicks(this.offers).Select(o => o.Id).ToHashSet();
        this.Layout();
        Game1.playSound("bigSelect");
    }

    private int PerPage => Math.Max(1, (this.height - Header - Footer) / Row);

    private void Layout()
    {
        this.width = Math.Min(980, Game1.uiViewport.Width - 32);
        this.height = Math.Min(Game1.uiViewport.Height - 100, Header + Footer + Math.Max(4, this.offers.Count) * Row);
        this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
        this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2 + 30;
        this.initializeUpperRightCloseButton();
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

    public override void receiveScrollWheelAction(int direction) =>
        this.scroll = Math.Clamp(this.scroll + (direction > 0 ? -1 : 1), 0, Math.Max(0, this.offers.Count - this.PerPage));

    private int RowY(int i) => this.yPositionOnScreen + Header + i * Row;
    private Rectangle Minus(int i) => new(this.xPositionOnScreen + this.width - Pad - Button * 2 - 70, this.RowY(i) + 4, Button, Button);
    private Rectangle Plus(int i) => new(this.xPositionOnScreen + this.width - Pad - Button, this.RowY(i) + 4, Button, Button);

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);
        if (Game1.activeClickableMenu != this)
            return;
        if (this.autoRect.Contains(x, y))
        {
            this.state.ShopAuto = !this.state.ShopAuto;
            Game1.playSound(this.state.ShopAuto ? "purchaseClick" : "trashcan");
            return;
        }
        int step = Game1.oldKBState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift) ? 50 : 10;
        for (int i = 0; i < this.PerPage && this.scroll + i < this.offers.Count; i++)
        {
            string id = this.offers[this.scroll + i].Id;
            int want = this.state.ShopList.GetValueOrDefault(id);
            if (this.Plus(i).Contains(x, y))
                want += step;
            else if (this.Minus(i).Contains(x, y))
                want = Math.Max(0, want - step);
            else
                continue;
            if (want > 0)
                this.state.ShopList[id] = want;
            else
                this.state.ShopList.Remove(id);
            Game1.playSound("smallSelect");
            return;
        }
    }

    private void DrawButton(SpriteBatch b, Rectangle r, string text)
    {
        drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9), r.X, r.Y, r.Width, r.Height, r.Contains(this.mouseX, this.mouseY) ? Color.Wheat : Color.White, 4f, false);
        var size = Game1.smallFont.MeasureString(text);
        Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(r.Center.X - size.X / 2, r.Center.Y - size.Y / 2 + 2), Game1.textColor);
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
        drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);
        SpriteText.drawStringWithScrollCenteredAt(b, "Mae's Shopping List", this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen - 64);
        var font = Game1.smallFont;
        int left = this.xPositionOnScreen + Pad, top = this.yPositionOnScreen + Pad - 6;

        this.autoRect = new Rectangle(left, top, 330, Button);
        this.DrawButton(b, this.autoRect, this.state.ShopAuto ? "Auto-fill empty soil: ON" : "Auto-fill empty soil: OFF");
        Utility.drawTextWithShadow(b, $"Budget {this.state.ShopBudget:N0}g/day (ask her to change)   * = her picks for empty soil", font, new Vector2(left + 346, top + 6), Color.SaddleBrown);
        int columns = top + 46;
        Utility.drawTextWithShadow(b, "Seed", font, new Vector2(left + 44, columns), Color.SaddleBrown);
        Utility.drawTextWithShadow(b, "Price   Earns/tile   In crate", font, new Vector2(left + 330, columns), Color.SaddleBrown);
        Utility.drawTextWithShadow(b, "Keep in crate", font, new Vector2(this.Minus(0).X, columns), Color.SaddleBrown);

        if (this.offers.Count == 0)
            Utility.drawTextWithShadow(b, "The shops have no seeds for this season today.", font, new Vector2(left, this.RowY(0) + 8), Color.DimGray);
        for (int i = 0; i < this.PerPage && this.scroll + i < this.offers.Count; i++)
        {
            var offer = this.offers[this.scroll + i];
            var data = ItemRegistry.GetDataOrErrorItem(offer.Id);
            int y = this.RowY(i);
            if (i % 2 == 0)
                b.Draw(Game1.staminaRect, new Rectangle(left, y, this.width - Pad * 2, Row - 2), Color.SaddleBrown * 0.06f);
            b.Draw(data.GetTexture(), new Vector2(left + 4, y + 6), data.GetSourceRect(), Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 1f);
            Utility.drawTextWithShadow(b, (this.picks.Contains(offer.Id) ? "* " : "") + data.DisplayName, font, new Vector2(left + 44, y + 10), Game1.textColor);
            Utility.drawTextWithShadow(b, $"{offer.Price:N0}g", font, new Vector2(left + 330, y + 10), Game1.textColor);
            Utility.drawTextWithShadow(b, offer.Profit > 0 ? $"{offer.Profit:N0}g" : "too late", font, new Vector2(left + 420, y + 10), offer.Profit > 0 ? Up : Down);
            Utility.drawTextWithShadow(b, $"{Crate.Count(offer.Id):N0}", font, new Vector2(left + 560, y + 10), Game1.textColor);
            int want = this.state.ShopList.GetValueOrDefault(offer.Id);
            this.DrawButton(b, this.Minus(i), "-");
            this.DrawButton(b, this.Plus(i), "+");
            string target = want > 0 ? $"{want:N0}" : "-";
            var size = font.MeasureString(target);
            Utility.drawTextWithShadow(b, target, font, new Vector2((this.Minus(i).Right + this.Plus(i).X) / 2 - size.X / 2, y + 10), want > 0 ? Up : Color.DimGray);
        }
        string help = this.offers.Count > this.PerPage ? "Scroll for more. " : "";
        Utility.drawTextWithShadow(b, help + "Click +/- for 10 (hold Shift: 50). She shops at noon: your list first, then the empty soil.", font,
            new Vector2(left, this.yPositionOnScreen + this.height - Footer + 14), Color.SaddleBrown);
        base.draw(b);
        this.drawMouse(b);
    }
}
