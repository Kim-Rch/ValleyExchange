using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace ValleyExchange;

/// <summary>Hire and let go of hands, one at a time, a page of roles at a time. Each hand needs a bed in one of your cabins.</summary>
internal sealed class HireMenu : IClickableMenu
{
    private const int RowHeight = 68, Header = 40, Footer = 132, Pad = 36, ButtonSize = 44, PageSize = 6;
    private static readonly Color Up = new(30, 140, 30), Down = new(190, 40, 40);

    private readonly MarketState state;
    private readonly Texture2D texture;
    private readonly List<ClickableComponent> minus = new(), plus = new();
    private ClickableComponent prev = null!, next = null!, skills = null!, tracker = null!, guild = null!, lab = null!, fleet = null!;
    private int page;
    private string status = "Every Brawnie needs a bed in a cabin, and wages every morning. No coin, no toil.";
    private Color statusColor = Game1.textColor;
    private int mouseX, mouseY;

    public HireMenu(MarketState state)
    {
        this.state = state;
        this.texture = Game1.content.Load<Texture2D>(Workers.TextureAsset);
        this.Layout();
    }

    private static int Pages => (Market.Roles.Count + PageSize - 1) / PageSize;

    /// <summary>The roles on this page, with their row slot.</summary>
    private IEnumerable<(int Slot, RoleDef Info)> Rows() =>
        Market.Roles.Skip(this.page * PageSize).Take(PageSize).Select((info, slot) => (slot, info));

    private void Layout()
    {
        this.width = Math.Min(1000, Game1.uiViewport.Width - 32);
        this.height = Header + PageSize * RowHeight + Footer;
        this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
        this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2 + 32;
        this.initializeUpperRightCloseButton();
        this.minus.Clear();
        this.plus.Clear();
        int right = this.xPositionOnScreen + this.width - Pad;
        for (int slot = 0; slot < PageSize; slot++)
        {
            int y = this.RowY(slot) + 12;
            this.minus.Add(new ClickableComponent(new Rectangle(right - ButtonSize * 2 - 56, y, ButtonSize, ButtonSize), "-"));
            this.plus.Add(new ClickableComponent(new Rectangle(right - ButtonSize, y, ButtonSize, ButtonSize), "+"));
        }
        int py = this.RowY(PageSize) + 10;
        this.prev = new ClickableComponent(new Rectangle(right - ButtonSize * 2 - 120, py, ButtonSize, ButtonSize), "<");
        this.next = new ClickableComponent(new Rectangle(right - ButtonSize, py, ButtonSize, ButtonSize), ">");
        this.skills = new ClickableComponent(new Rectangle(right - 150, py + 50, 150, ButtonSize), "Skills");
        this.tracker = new ClickableComponent(new Rectangle(right - 316, py + 50, 156, ButtonSize), "Tracker");
        this.lab = new ClickableComponent(new Rectangle(right - 452, py + 50, 126, ButtonSize), "Lab");
        this.guild = new ClickableComponent(new Rectangle(right - 588, py + 50, 126, ButtonSize), "Guild");
        this.fleet = new ClickableComponent(new Rectangle(right - 724, py + 50, 126, ButtonSize), "Fleet");
    }

    private int RowY(int slot) => this.yPositionOnScreen + Header + slot * RowHeight;

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
        if (this.tracker.containsPoint(x, y))
        {
            Game1.playSound("bigSelect");
            var back = this.exitFunction;
            Game1.activeClickableMenu = new TrackerMenu(this.state)
            {
                exitFunction = () => Game1.activeClickableMenu = new HireMenu(this.state) { exitFunction = back },
            };
            return;
        }
        if (this.guild.containsPoint(x, y) || this.lab.containsPoint(x, y) || this.fleet.containsPoint(x, y))
        {
            Game1.playSound("bigSelect");
            var whenDone = this.exitFunction;
            IClickableMenu menu = this.guild.containsPoint(x, y) ? new GuildMenu(this.state) : this.fleet.containsPoint(x, y) ? new GuildMenu(this.state, fleet: true) : new ResearchMenu(this.state);
            menu.exitFunction = () => Game1.activeClickableMenu = new HireMenu(this.state) { exitFunction = whenDone };
            Game1.activeClickableMenu = menu;
            return;
        }
        if (this.skills.containsPoint(x, y))
        {
            Game1.playSound("bigSelect");
            var onClose = this.exitFunction;
            Game1.activeClickableMenu = new SkillMenu(this.state)
            {
                exitFunction = () => Game1.activeClickableMenu = new HireMenu(this.state) { exitFunction = onClose }, // back to the hiring board
            };
            return;
        }
        if (this.prev.containsPoint(x, y) || this.next.containsPoint(x, y))
        {
            this.Flip(this.prev.containsPoint(x, y) ? -1 : 1);
            return;
        }
        foreach (var (slot, info) in this.Rows())
        {
            int count = Market.Count(this.state, info.Role);
            if (this.minus[slot].containsPoint(x, y))
            {
                if (count == 0)
                {
                    this.Fail($"You don't have any {info.Title.ToLower()} to let go.");
                    return;
                }
                Market.SetCount(this.state, info.Role, count - 1);
                Game1.playSound("trashcan");
                this.Say($"Let {info.Names[count - 1]} go.", Down);
                return;
            }
            if (this.plus[slot].containsPoint(x, y))
            {
                this.Hire(info, count);
                return;
            }
        }
        if (this.isWithinBounds(x, y))
            Game1.playSound("smallSelect");
    }

    private void Hire(RoleDef info, int count)
    {
        if (count >= Market.MaxCount(info.Role))
        {
            this.Fail(Market.MaxCount(info.Role) == 1 ? $"One {info.Title.ToLower()} handles it all." : $"That's the most {info.Title.ToLower()} you can hire.");
            return;
        }
        int beds = Workers.Beds();
        if (!Market.HasRoom(this.state, info.Role, beds))
        {
            this.Fail(Workers.Cabins().Any()
                ? $"No free bed ({Market.HeadCount(this.state)}/{beds}). Put a bed in a cabin, or upgrade a cabin for room."
                : "Build a cabin first (Robin): your hands need somewhere to sleep.");
            return;
        }
        // specialists want their first day's pay up front; crews and the harvester are paid as they work
        int wage = Market.DailyWage(info.Role) > 0 ? Staff.WageFor(info.Role) : 0;
        if (Game1.player.Money < wage)
        {
            this.Fail($"You need {wage:N0}g for the first day's wage.");
            return;
        }
        Game1.player.Money -= wage;
        Market.SetCount(this.state, info.Role, count + 1);
        Game1.playSound("purchaseClick");
        string name = info.Names[count];
        this.Say(wage > 0 ? $"Hired {name}! Paid {wage:N0}g for today." : $"Hired {name}! Starts right away.", Up);
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

    /// <summary>Trim text with "..." so it never runs under the buttons or out of the box.</summary>
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
        SpriteText.drawStringWithScrollCenteredAt(b, "Hired Hands", this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen - 64);

        int left = this.xPositionOnScreen + Pad;
        int beds = Workers.Beds();
        bool roomLeft = Market.HeadCount(this.state) < beds;
        foreach (var (slot, info) in this.Rows())
        {
            int y = this.RowY(slot);
            int count = Market.Count(this.state, info.Role);
            if (slot % 2 == 0)
                b.Draw(Game1.staminaRect, new Rectangle(left - 8, y, this.width - Pad * 2 + 16, RowHeight), Color.SaddleBrown * 0.08f);

            b.Draw(this.texture, new Vector2(left, y + 2), new Rectangle(0, (int)info.Role * 128, 16, 16), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
            int textWidth = this.minus[slot].bounds.X - 16 - (left + 80);
            string lead = count > 0 ? string.Join(", ", info.Names.Take(count)) : info.Names[0];
            string title = $"{info.Title} ({lead})";
            Utility.drawTextWithShadow(b, Fit(title, textWidth), Game1.smallFont, new Vector2(left + 80, y + 6), count > 0 ? Up : Game1.textColor);
            int titleWidth = (int)Game1.smallFont.MeasureString(title).X;
            if (titleWidth + 24 + Game1.smallFont.MeasureString(info.Wage).X < textWidth)
                Utility.drawTextWithShadow(b, info.Wage, Game1.smallFont, new Vector2(left + 80 + titleWidth + 24, y + 6), Color.SaddleBrown);
            Utility.drawTextWithShadow(b, Fit(info.Pitch, textWidth), Game1.smallFont, new Vector2(left + 80, y + 36), Game1.textColor * 0.8f);

            bool canAdd = count < Market.MaxCount(info.Role) && roomLeft;
            Button(b, this.minus[slot], this.mouseX, this.mouseY, enabled: count > 0);
            Button(b, this.plus[slot], this.mouseX, this.mouseY, enabled: canAdd);
            string shown = $"{count}/{Market.MaxCount(info.Role)}";
            var size = Game1.smallFont.MeasureString(shown);
            int gapCenter = (this.minus[slot].bounds.Right + this.plus[slot].bounds.X) / 2;
            Utility.drawTextWithShadow(b, shown, Game1.smallFont, new Vector2(gapCenter - size.X / 2, y + 22), count > 0 ? Up : Game1.textColor);
        }

        // footer: beds + page controls, then the status line
        int fy = this.RowY(PageSize) + 14;
        Button(b, this.prev, this.mouseX, this.mouseY, enabled: this.page > 0);
        Button(b, this.next, this.mouseX, this.mouseY, enabled: this.page < Pages - 1);
        string pageText = $"Page {this.page + 1}/{Pages}";
        var pageSize = Game1.smallFont.MeasureString(pageText);
        Utility.drawTextWithShadow(b, pageText, Game1.smallFont, new Vector2((this.prev.bounds.Right + this.next.bounds.X) / 2 - pageSize.X / 2, fy + 4), Color.SaddleBrown);
        Utility.drawTextWithShadow(b, Fit($"Beds: {Market.HeadCount(this.state)}/{beds}. A bed in a cabin sleeps 1 (double 2), upgrades fit more: 2/4/6/8.", this.prev.bounds.X - 16 - left), Game1.smallFont, new Vector2(left, fy + 4), Color.SaddleBrown);
        Utility.drawTextWithShadow(b, Fit(this.status, this.fleet.bounds.X - 16 - left), Game1.smallFont, new Vector2(left, fy + 52), this.statusColor);
        Button(b, this.tracker, this.mouseX, this.mouseY, enabled: true);
        Button(b, this.lab, this.mouseX, this.mouseY, enabled: true);
        Button(b, this.guild, this.mouseX, this.mouseY, enabled: true);
        Button(b, this.fleet, this.mouseX, this.mouseY, enabled: true);
        int waiting = Market.Roles.Sum(r => Skills.Unpicked(this.state, r.Role));
        this.skills.name = waiting > 0 ? $"Skills ({waiting}!)" : "Skills";
        Button(b, this.skills, this.mouseX, this.mouseY, enabled: true);

        base.draw(b);
        this.drawMouse(b);
    }

    private static void Button(SpriteBatch b, ClickableComponent button, int mouseX, int mouseY, bool enabled)
    {
        var r = button.bounds;
        Color tint = !enabled ? Color.Gray : r.Contains(mouseX, mouseY) ? Color.Wheat : Color.White;
        drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9), r.X, r.Y, r.Width, r.Height, tint, 4f, false);
        var size = Game1.smallFont.MeasureString(button.name);
        Utility.drawTextWithShadow(b, button.name, Game1.smallFont, new Vector2(r.Center.X - size.X / 2, r.Center.Y - size.Y / 2 + 2), enabled ? Game1.textColor : Color.DimGray);
    }
}
