using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace ValleyExchange;

internal sealed class StockMenu : IClickableMenu
{
    private static readonly int[] Quantities = { 1, 10, 100, 1000 };
    private const int RowHeight = 44, HeaderHeight = 112, FooterHeight = 224, Pad = 36, MaxRows = 14;
    private static readonly Color Up = new(30, 140, 30), Down = new(190, 40, 40);

    // column offsets from the left padding
    private const int ColName = 90, ColPrice = 330, ColChange = 450, ColChart = 560, ChartWidth = 140, ColOwned = 730;

    private readonly MarketState state;
    private readonly List<ClickableComponent> buy = new(), sell = new(), names = new(), qty = new();
    private ClickableTextureComponent upArrow = null!, downArrow = null!;
    private int visibleRows, scroll, qtyIndex;
    private string status = "Click Buy or Sell. Prices change every morning.";
    private Color statusColor = Game1.textColor;
    private int mouseX, mouseY;

    public StockMenu(MarketState state)
    {
        this.state = state;
        this.Layout();
    }

    private int Count => Market.Companies.Count;

    private void Layout()
    {
        // shortcut: fixed column layout, needs ~1150px of UI width; add responsive columns if small screens/large UI scale matter
        int maxRowsForScreen = (Game1.uiViewport.Height - 96 - HeaderHeight - FooterHeight) / RowHeight;
        this.visibleRows = Math.Clamp(Math.Min(MaxRows, maxRowsForScreen), 3, this.Count);
        this.scroll = Math.Clamp(this.scroll, 0, this.Count - this.visibleRows);

        this.width = Math.Min(1150, Game1.uiViewport.Width - 32);
        this.height = HeaderHeight + this.visibleRows * RowHeight + FooterHeight;
        this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
        this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2 + 32;
        this.initializeUpperRightCloseButton();

        this.buy.Clear();
        this.sell.Clear();
        this.names.Clear();
        this.qty.Clear();
        int left = this.xPositionOnScreen + Pad, right = this.xPositionOnScreen + this.width - Pad;
        for (int row = 0; row < this.visibleRows; row++)
        {
            int y = this.RowY(row);
            this.names.Add(new ClickableComponent(new Rectangle(left, y, ColPrice - 10, RowHeight - 4), "name"));
            this.buy.Add(new ClickableComponent(new Rectangle(right - 196, y + 2, 92, RowHeight - 6), "Buy"));
            this.sell.Add(new ClickableComponent(new Rectangle(right - 96, y + 2, 92, RowHeight - 6), "Sell"));
        }

        int arrowsY = this.yPositionOnScreen + HeaderHeight - 52;
        this.upArrow = new ClickableTextureComponent(new Rectangle(right - 96, arrowsY, 44, 48), Game1.mouseCursors, new Rectangle(421, 459, 11, 12), 4f);
        this.downArrow = new ClickableTextureComponent(new Rectangle(right - 44, arrowsY, 44, 48), Game1.mouseCursors, new Rectangle(421, 472, 11, 12), 4f);

        int qy = this.FooterY() + 8;
        for (int i = 0; i < Quantities.Length; i++)
            this.qty.Add(new ClickableComponent(new Rectangle(left + 130 + i * 104, qy, 96, 42), "x" + Quantities[i].ToString("N0")));
    }

    private int RowY(int row) => this.yPositionOnScreen + HeaderHeight + row * RowHeight;
    private int FooterY() => this.yPositionOnScreen + HeaderHeight + this.visibleRows * RowHeight + 8;

    private bool Scroll(int delta)
    {
        int old = this.scroll;
        this.scroll = Math.Clamp(this.scroll + delta, 0, this.Count - this.visibleRows);
        if (this.scroll == old)
            return false;
        Game1.playSound("shiny4");
        return true;
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        this.Layout();
    }

    public override void receiveScrollWheelAction(int direction)
    {
        base.receiveScrollWheelAction(direction);
        this.Scroll(direction > 0 ? -1 : 1);
    }

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        this.mouseX = x;
        this.mouseY = y;
        this.upArrow.tryHover(x, y);
        this.downArrow.tryHover(x, y);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound); // close button
        if (Game1.activeClickableMenu != this)
            return;

        if (this.upArrow.containsPoint(x, y) || this.downArrow.containsPoint(x, y))
        {
            if (!this.Scroll(this.upArrow.containsPoint(x, y) ? -this.visibleRows : this.visibleRows))
                Game1.playSound("cancel"); // already at the top/bottom
            return;
        }
        for (int i = 0; i < this.qty.Count; i++)
        {
            if (this.qty[i].containsPoint(x, y))
            {
                this.qtyIndex = i;
                Game1.playSound("smallSelect");
                return;
            }
        }


        int amount = Quantities[this.qtyIndex];
        for (int row = 0; row < this.visibleRows; row++)
        {
            var c = Market.Companies[this.scroll + row];
            if (this.buy[row].containsPoint(x, y))
            {
                var (shares, gold) = Market.Buy(this.state, c.Ticker, amount, Game1.player.Money);
                if (shares == 0)
                    this.Fail($"Not enough gold for even one share of {c.Name}.");
                else
                {
                    Game1.player.Money -= (int)gold;
                    Game1.playSound("purchaseClick");
                    this.Say($"Bought {shares:N0} {c.Ticker} for {gold:N0}g.", Up);
                }
                return;
            }
            if (this.sell[row].containsPoint(x, y))
            {
                var (shares, gold) = Market.Sell(this.state, c.Ticker, amount);
                if (shares == 0)
                    this.Fail($"You don't own any {c.Name} shares.");
                else
                {
                    Game1.player.Money += (int)Math.Min(gold, int.MaxValue - Game1.player.Money);
                    Game1.playSound("sell");
                    this.Say($"Sold {shares:N0} {c.Ticker} for {gold:N0}g.", Down);
                }
                return;
            }
        }

        // anything else inside the window (names, charts, empty space) still gets a click
        if (this.isWithinBounds(x, y))
            Game1.playSound("smallSelect");
    }

    private void Say(string text, Color color)
    {
        this.status = text;
        this.statusColor = color;
    }

    private void Fail(string text)
    {
        Game1.playSound("cancel");
        this.Say(text, Down);
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
        drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);
        SpriteText.drawStringWithScrollCenteredAt(b, "Valley Exchange", this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen - 64);

        int left = this.xPositionOnScreen + Pad;
        int top = this.yPositionOnScreen + 28;

        // summary
        long profit = Market.Profit(this.state);
        Text(b, $"Gold: {Game1.player.Money:N0}g", left, top);
        Text(b, $"Portfolio: {Market.PortfolioValue(this.state):N0}g", left + 300, top);
        Text(b, $"Profit: {(profit >= 0 ? "+" : "")}{profit:N0}g", left + 600, top, profit > 0 ? Up : profit < 0 ? Down : Game1.textColor);

        // column headers + scroll position
        int hy = top + 44;
        Text(b, "Ticker", left, hy, Color.SaddleBrown);
        Text(b, "Company", left + ColName, hy, Color.SaddleBrown);
        Text(b, "Price", left + ColPrice, hy, Color.SaddleBrown);
        Text(b, "Today", left + ColChange, hy, Color.SaddleBrown);
        Text(b, "28 days", left + ColChart, hy, Color.SaddleBrown);
        Text(b, "Owned", left + ColOwned, hy, Color.SaddleBrown);
        if (this.visibleRows < this.Count)
        {
            TextRight(b, $"{this.scroll + 1}-{this.scroll + this.visibleRows} of {this.Count}", this.upArrow.bounds.X - 12, hy, Color.SaddleBrown);
            if (this.scroll > 0)
                this.upArrow.draw(b);
            if (this.scroll < this.Count - this.visibleRows)
                this.downArrow.draw(b);
        }

        // rows
        for (int row = 0; row < this.visibleRows; row++)
        {
            var c = Market.Companies[this.scroll + row];
            var stock = this.state.Stocks[c.Ticker];
            int y = this.RowY(row);
            if (row % 2 == 0)
                b.Draw(Game1.staminaRect, new Rectangle(left - 8, y, this.width - Pad * 2 + 16, RowHeight), Color.SaddleBrown * 0.08f);

            int ty = y + 10;
            double change = Market.DayChange(this.state, c.Ticker);
            int owned = this.state.Holdings.TryGetValue(c.Ticker, out var h) ? h.Shares : 0;

            Text(b, c.Ticker, left, ty);
            Text(b, c.Name, left + ColName, ty);
            Text(b, $"{Market.SharePrice(this.state, c.Ticker):N0}g", left + ColPrice, ty);
            Text(b, $"{(change >= 0 ? "+" : "")}{change * 100:0.0}%", left + ColChange, ty, change >= 0 ? Up : Down);
            Chart(b, stock.History, new Rectangle(left + ColChart, y + 6, ChartWidth, RowHeight - 12));
            Text(b, owned > 0 ? owned.ToString("N0") : "-", left + ColOwned, ty);

            Button(b, this.buy[row], this.buy[row].containsPoint(this.mouseX, this.mouseY));
            Button(b, this.sell[row], this.sell[row].containsPoint(this.mouseX, this.mouseY), enabled: owned > 0);
        }

        // footer, one thing per line: [quantity ... ranch hands] / status / [news date ... oil prices] / headlines
        int fy = this.FooterY(), right = this.xPositionOnScreen + this.width - Pad;
        Text(b, "Quantity:", left, fy + 18);
        for (int i = 0; i < this.qty.Count; i++)
            Button(b, this.qty[i], this.qty[i].containsPoint(this.mouseX, this.mouseY), selected: i == this.qtyIndex);

        Text(b, this.status, left, fy + 62, this.statusColor);

        double crude = Market.CrudeChange(this.state);
        Text(b, $"Valley News - {Game1.Date.Localize()}", left, fy + 100, Color.SaddleBrown);
        TextRight(b, $"Crude {Market.CrudePrice(this.state)}g {(crude >= 0 ? "+" : "")}{crude * 100:0.0}%   Gasoline {Market.GasolinePrice(this.state)}g", right, fy + 100, crude >= 0 ? Up : Down);
        for (int i = 0; i < Math.Min(2, this.state.News.Count); i++)
            Text(b, "- " + this.state.News[i], left, fy + 132 + i * 30);

        // tooltip
        for (int row = 0; row < this.visibleRows; row++)
        {
            if (!this.names[row].containsPoint(this.mouseX, this.mouseY))
                continue;
            var c = Market.Companies[this.scroll + row];
            var h = this.state.Holdings.GetValueOrDefault(c.Ticker);
            string avg = h is { Shares: > 0 } ? $"\nYour avg cost: {h.CostBasis / h.Shares:N0}g" : "";
            drawHoverText(b, $"{c.Blurb}\nSector: {c.Sector}   Dividend: {c.SeasonDividend * 100:0.0}%/season{avg}", Game1.smallFont, boldTitleText: $"{c.Name} ({c.Ticker})");
        }

        base.draw(b); // close button
        this.drawMouse(b);
    }

    private static void Text(SpriteBatch b, string text, int x, int y, Color? color = null) =>
        Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(x, y), color ?? Game1.textColor);

    private static void TextRight(SpriteBatch b, string text, int right, int y, Color? color = null) =>
        Text(b, text, right - (int)Game1.smallFont.MeasureString(text).X, y, color);

    private static void Button(SpriteBatch b, ClickableComponent button, bool hovered, bool selected = false, bool enabled = true)
    {
        var r = button.bounds;
        Color tint = selected ? Color.LightGreen : !enabled ? Color.Gray : hovered ? Color.Wheat : Color.White;
        drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9), r.X, r.Y, r.Width, r.Height, tint, 4f, false);
        var size = Game1.smallFont.MeasureString(button.name);
        Utility.drawTextWithShadow(b, button.name, Game1.smallFont, new Vector2(r.Center.X - size.X / 2, r.Center.Y - size.Y / 2 + 2), enabled ? Game1.textColor : Color.DimGray);
    }

    private static void Chart(SpriteBatch b, List<double> history, Rectangle r)
    {
        if (history.Count < 2)
            return;
        double min = history.Min(), max = history.Max(), span = Math.Max(max - min, 1e-9);
        var color = history[^1] >= history[0] ? Up : Down;
        Vector2 Point(int i) => new(r.X + i * (r.Width - 1f) / (history.Count - 1), r.Bottom - 1 - (float)((history[i] - min) / span) * (r.Height - 2));
        for (int i = 1; i < history.Count; i++)
        {
            Vector2 a = Point(i - 1), d = Point(i) - a;
            b.Draw(Game1.staminaRect, a, null, color, (float)Math.Atan2(d.Y, d.X), Vector2.Zero, new Vector2(d.Length(), 2f), SpriteEffects.None, 1f);
        }
    }
}
