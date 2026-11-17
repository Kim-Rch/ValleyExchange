using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace ValleyExchange;

/// <summary>Runs something once no menu or dialogue is up (opening a menu while a dialogue box is still closing freezes the player).</summary>
internal static class WhenFree
{
    private static Action? pending;

    public static void Run(Action action) => pending = action;

    /// <summary>Every tick.</summary>
    public static void Tick()
    {
        if (pending == null || Game1.activeClickableMenu != null || Game1.dialogueUp || Game1.eventUp)
            return;
        var action = pending;
        pending = null;
        action();
    }
}

/// <summary>
/// The lab: trees on the left; the chosen tree's projects as one tidy line each; the picked project's details, cost and a
/// Research button at the bottom. One project at a time, in order down each tree.
/// </summary>
internal sealed class ResearchMenu : IClickableMenu
{
    private const int Pad = 32, ListWidth = 270, Header = 76, NodeRow = 46, Detail = 150;
    private static readonly Color Up = new(30, 140, 30), Down = new(190, 40, 40), Faded = Color.DimGray;

    private readonly MarketState state;
    private readonly List<ClickableComponent> trees = new();
    private readonly List<ClickableComponent> nodes = new();
    private ClickableComponent research = new(Rectangle.Empty, "");
    private string tree;
    private ResearchNode picked;
    private string? note;
    private Color noteColor = Up;
    private int mouseX, mouseY;

    private static int MaxNodes => Research.Trees.Max(t => Research.In(t).Count());
    private List<ResearchNode> Projects => Research.In(this.tree).ToList();
    private double Speed => CrewSkills.Total(Role.Scientist, PerkKind.Speed);

    public ResearchMenu(MarketState state)
    {
        this.state = state;
        this.tree = Research.Find(state.Researching)?.Tree ?? Research.Trees[0];
        this.picked = this.NextUp();
        this.Layout();
        Game1.playSound("bigSelect");
    }

    /// <summary>The project worth looking at in this tree: the one in the lab, else the next not done.</summary>
    private ResearchNode NextUp() =>
        this.Projects.FirstOrDefault(n => n.Id == this.state.Researching)
        ?? this.Projects.FirstOrDefault(n => !Research.Done(this.state, n))
        ?? this.Projects.Last();

    private void Layout()
    {
        this.width = Math.Min(1100, Game1.uiViewport.Width - 32);
        this.height = Math.Min(Game1.uiViewport.Height - 140, Header + MaxNodes * NodeRow + Detail + Pad * 2 + 16);
        this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
        this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2 + 40;
        this.initializeUpperRightCloseButton();
        this.trees.Clear();
        this.nodes.Clear();
        int left = this.xPositionOnScreen + Pad, top = this.yPositionOnScreen + Pad;
        int row = Math.Max(28, (this.height - Pad * 2) / Research.Trees.Count);
        for (int i = 0; i < Research.Trees.Count; i++)
            this.trees.Add(new ClickableComponent(new Rectangle(left, top + i * row, ListWidth, row - 2), ""));
        int panel = left + ListWidth + 28, panelWidth = this.xPositionOnScreen + this.width - Pad - panel;
        int nodeRow = Math.Min(NodeRow, (this.height - Pad * 2 - Header - Detail) / MaxNodes);
        for (int i = 0; i < MaxNodes; i++)
            this.nodes.Add(new ClickableComponent(new Rectangle(panel, top + Header + i * nodeRow, panelWidth, nodeRow - 4), ""));
        int bottom = this.yPositionOnScreen + this.height - Pad;
        this.research = new ClickableComponent(new Rectangle(panel + panelWidth - 200, bottom - 64, 200, 56), "Research");
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

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);
        if (Game1.activeClickableMenu != this)
            return;
        for (int i = 0; i < this.trees.Count; i++)
        {
            if (!this.trees[i].containsPoint(x, y))
                continue;
            this.tree = Research.Trees[i];
            this.picked = this.NextUp();
            this.note = null;
            Game1.playSound("smallSelect");
            return;
        }
        var projects = this.Projects;
        for (int i = 0; i < projects.Count; i++)
        {
            if (!this.nodes[i].containsPoint(x, y))
                continue;
            this.picked = projects[i];
            this.note = null;
            Game1.playSound("smallSelect");
            return;
        }
        if (this.research.containsPoint(x, y))
            this.Start();
    }

    private void Start()
    {
        if (!Market.IsHired(this.state, Role.Scientist))
            (this.note, this.noteColor) = ("Hire Doc first: nobody's in the lab.", Down);
        else if (Research.WhyNot(this.state, this.picked, Game1.player.Money) is { } why)
            (this.note, this.noteColor) = (why, Down);
        else
        {
            Game1.player.Money -= this.picked.Gold;
            CrewReport.Paid(Role.Scientist, this.picked.Gold);
            Research.Start(this.state, this.picked, Game1.Date.TotalDays, this.Speed);
            Game1.playSound("purchaseClick");
            (this.note, this.noteColor) = ($"Doc's on it! Done in {this.state.ResearchDoneDay - Game1.Date.TotalDays} day(s).", Up);
            return;
        }
        Game1.playSound("cancel");
    }

    private enum Status { Done, InLab, Ready, Locked }

    private Status StatusOf(ResearchNode node) =>
        Research.Done(this.state, node) ? Status.Done
        : this.state.Researching == node.Id ? Status.InLab
        : node.Tier == 1 || Research.Done(this.state, this.Projects[node.Tier - 2]) ? Status.Ready
        : Status.Locked;

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
        drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);
        SpriteText.drawStringWithScrollCenteredAt(b, "Research Lab", this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen - 64);
        var font = Game1.smallFont;

        // trees: name and progress
        for (int i = 0; i < Research.Trees.Count; i++)
        {
            string name = Research.Trees[i];
            var r = this.trees[i].bounds;
            if (name == this.tree)
                b.Draw(Game1.staminaRect, r, Color.SaddleBrown * 0.25f);
            else if (r.Contains(this.mouseX, this.mouseY))
                b.Draw(Game1.staminaRect, r, Color.SaddleBrown * 0.12f);
            int done = Research.In(name).Count(n => Research.Done(this.state, n)), all = Research.In(name).Count();
            bool busy = Research.Find(this.state.Researching)?.Tree == name;
            int y = r.Y + (r.Height - 28) / 2;
            Utility.drawTextWithShadow(b, name + (busy ? " *" : ""), font, new Vector2(r.X + 8, y), Game1.textColor);
            string count = $"{done}/{all}";
            Utility.drawTextWithShadow(b, count, font, new Vector2(r.Right - font.MeasureString(count).X - 8, y), done == all ? Up : Color.SaddleBrown);
        }
        int divider = this.trees[0].bounds.Right + 12;
        b.Draw(Game1.staminaRect, new Rectangle(divider, this.yPositionOnScreen + Pad, 2, this.height - Pad * 2), Color.SaddleBrown * 0.3f);

        // header: points, and what's in the lab
        var first = this.nodes[0].bounds;
        int top = this.yPositionOnScreen + Pad;
        Utility.drawTextWithShadow(b, $"{this.state.ResearchPoints:N0} research points", Game1.dialogueFont, new Vector2(first.X, top - 6), Game1.textColor);
        string lab = Research.Find(this.state.Researching) is { } current
            ? $"In the lab: {current.Name} ({current.Tree}), {Math.Max(0, this.state.ResearchDoneDay - Game1.Date.TotalDays)} day(s) left"
            : Market.IsHired(this.state, Role.Scientist) ? "The lab's idle. Pick a project." : "No scientist hired.";
        Utility.drawTextWithShadow(b, lab, font, new Vector2(first.X, top + 40), Color.SaddleBrown);

        // projects: one line each
        var projects = this.Projects;
        for (int i = 0; i < projects.Count; i++)
        {
            var node = projects[i];
            var r = this.nodes[i].bounds;
            var status = this.StatusOf(node);
            if (node == this.picked)
                b.Draw(Game1.staminaRect, r, Color.SaddleBrown * 0.22f);
            else if (r.Contains(this.mouseX, this.mouseY))
                b.Draw(Game1.staminaRect, r, Color.SaddleBrown * 0.1f);
            int y = r.Y + (r.Height - 28) / 2;
            var color = status == Status.Locked ? Faded : Game1.textColor;
            Utility.drawTextWithShadow(b, $"{node.Tier}.  {node.Name}", font, new Vector2(r.X + 10, y), color);
            (string tag, Color tagColor) = status switch
            {
                Status.Done => ("Done", Up),
                Status.InLab => ($"In the lab ({Math.Max(0, this.state.ResearchDoneDay - Game1.Date.TotalDays)}d)", Color.DarkGoldenrod),
                Status.Ready => ($"{node.Points:N0} pts", Color.SaddleBrown),
                _ => ("Locked", Faded),
            };
            Utility.drawTextWithShadow(b, tag, font, new Vector2(r.Right - font.MeasureString(tag).X - 10, y), tagColor);
        }

        // details of the picked project
        var p = this.picked;
        int dx = first.X, dy = this.yPositionOnScreen + this.height - Pad - Detail + 8, dWidth = first.Width;
        b.Draw(Game1.staminaRect, new Rectangle(dx, dy - 10, dWidth, 2), Color.SaddleBrown * 0.3f);
        Utility.drawTextWithShadow(b, $"{p.Name}  ({p.Tree} {p.Tier})", font, new Vector2(dx, dy), Game1.textColor);
        Utility.drawTextWithShadow(b, Game1.parseText(p.Text + ".", font, dWidth - 220), font, new Vector2(dx, dy + 32), Color.SaddleBrown);
        string cost = Research.Done(this.state, p) ? "Researched for good." : $"Cost: {p.Points:N0} points, {p.Gold:N0}g, {Research.Days(p, this.Speed)} day(s)";
        Utility.drawTextWithShadow(b, cost, font, new Vector2(dx, dy + 96), Game1.textColor * 0.85f);
        if (this.note != null)
            Utility.drawTextWithShadow(b, this.note, font, new Vector2(dx, dy + 124), this.noteColor);

        // the button
        bool can = this.StatusOf(p) == Status.Ready && Research.WhyNot(this.state, p, Game1.player.Money) == null && Market.IsHired(this.state, Role.Scientist);
        var button = this.research.bounds;
        bool hover = button.Contains(this.mouseX, this.mouseY);
        drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9), button.X, button.Y, button.Width, button.Height, can ? (hover ? Color.Wheat : Color.White) : Color.White * 0.45f, 4f, false);
        var size = font.MeasureString("Research");
        Utility.drawTextWithShadow(b, "Research", font, new Vector2(button.Center.X - size.X / 2, button.Center.Y - size.Y / 2), can ? Game1.textColor : Faded);

        base.draw(b);
        this.drawMouse(b);
    }
}
