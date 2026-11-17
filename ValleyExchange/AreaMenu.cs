using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;

namespace ValleyExchange;

/// <summary>
/// Pick a rectangle of farmland for a field job (plowing, planting), like placing a building: click one corner, then the opposite one.
/// Green tiles are ones the job can do, red can't. WASD / arrows / screen edges pan, right-click resets the corner, Esc cancels.
/// </summary>
internal sealed class AreaMenu : IClickableMenu
{
    public const int MaxSide = 30;
    private readonly Farm farm;
    private readonly Action<List<Vector2>> onPicked;
    private readonly Func<GameLocation, Vector2, bool> fits;
    private readonly int fee;
    private readonly string done; // "plowed", "planted"
    private Point? first;

    public AreaMenu(Farm farm, Func<GameLocation, Vector2, bool> fits, int fee, string done, Action<List<Vector2>> onPicked)
        : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height)
    {
        this.farm = farm;
        this.onPicked = onPicked;
        this.fits = fits;
        this.fee = fee;
        this.done = done;
        Game1.viewportFreeze = true; // let the camera roam, like Robin's building placement
        Game1.displayHUD = false;
    }

    /// <summary>Empty tilled soil, ready for seeds.</summary>
    public static bool Plantable(GameLocation location, Vector2 tile) =>
        location.terrainFeatures.TryGetValue(tile, out var feature) && feature is HoeDirt { crop: null };

    /// <summary>Can a field hand till this tile? Grass gets cut first; rocks, weeds, trees and buildings are in the way.</summary>
    public static bool Plowable(GameLocation location, Vector2 tile)
    {
        if (location.doesTileHaveProperty((int)tile.X, (int)tile.Y, "Diggable", "Back") == null || location.isWaterTile((int)tile.X, (int)tile.Y))
            return false;
        if (location.terrainFeatures.TryGetValue(tile, out var feature) && feature is not Grass)
            return false; // already tilled, a tree, flooring...
        return !location.objects.ContainsKey(tile) && !location.buildings.Any(b => b.occupiesTile(tile)) && !location.resourceClumps.Any(r => r.occupiesTile((int)tile.X, (int)tile.Y));
    }

    private static Point MouseTile => new((Game1.viewport.X + Game1.getOldMouseX(false)) / 64, (Game1.viewport.Y + Game1.getOldMouseY(false)) / 64);

    /// <summary>The rectangle between the first corner and the mouse, at most 30x30.</summary>
    private IEnumerable<Vector2> Area()
    {
        var a = this.first ?? MouseTile;
        var b = MouseTile;
        int x1 = Math.Min(a.X, b.X), y1 = Math.Min(a.Y, b.Y);
        int x2 = Math.Min(Math.Max(a.X, b.X), x1 + MaxSide - 1), y2 = Math.Min(Math.Max(a.Y, b.Y), y1 + MaxSide - 1);
        for (int x = x1; x <= x2; x++)
        {
            for (int y = y1; y <= y2; y++)
                yield return new Vector2(x, y);
        }
    }

    public override void update(GameTime time)
    {
        base.update(time);
        // pan with the movement keys or by pushing the mouse against the screen edge
        var keys = Game1.GetKeyboardState();
        int mx = Game1.getOldMouseX(), my = Game1.getOldMouseY(), dx = 0, dy = 0;
        if (keys.IsKeyDown(Keys.A) || keys.IsKeyDown(Keys.Left) || mx < 48) dx -= 16;
        if (keys.IsKeyDown(Keys.D) || keys.IsKeyDown(Keys.Right) || mx > Game1.uiViewport.Width - 48) dx += 16;
        if (keys.IsKeyDown(Keys.W) || keys.IsKeyDown(Keys.Up) || my < 48) dy -= 16;
        if (keys.IsKeyDown(Keys.S) || keys.IsKeyDown(Keys.Down) || my > Game1.uiViewport.Height - 48) dy += 16;
        if (dx != 0 || dy != 0)
            Game1.panScreen(dx, dy);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.first == null)
        {
            this.first = MouseTile;
            Game1.playSound("smallSelect");
            return;
        }
        var tiles = this.Area().Where(t => this.fits(this.farm, t)).ToList();
        if (tiles.Count == 0)
        {
            Game1.playSound("cancel");
            return;
        }
        Game1.playSound("purchaseClick");
        this.exitThisMenu(playSound: false);
        this.onPicked(tiles);
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        if (this.first != null)
        {
            this.first = null; // start over
            Game1.playSound("cancel");
        }
        else
            this.exitThisMenu();
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape)
            this.exitThisMenu(); // (E and the movement keys pan instead of closing)
    }

    protected override void cleanupBeforeExit()
    {
        base.cleanupBeforeExit();
        Game1.viewportFreeze = false;
        Game1.displayHUD = true;
    }

    public override void draw(SpriteBatch b)
    {
        Game1.StartWorldDrawInUI(b);
        int good = 0, total = 0;
        foreach (var tile in this.Area())
        {
            bool ok = this.fits(this.farm, tile);
            good += ok ? 1 : 0;
            total++;
            // the green/red tiles from building placement
            b.Draw(Game1.mouseCursors, Game1.GlobalToLocal(Game1.viewport, tile * 64), new Rectangle(ok ? 194 : 210, 388, 16, 16), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.999f);
        }
        Game1.EndWorldDrawInUI(b);

        string help = this.first == null
            ? "Click the first corner of the field.   Right-click or Esc: cancel   WASD / screen edge: move the camera"
            : $"Click the opposite corner.   {good} of {total} tiles can be {this.done}: {good * this.fee:N0}g   Right-click: start over";
        var size = Game1.smallFont.MeasureString(help);
        int boxX = (Game1.uiViewport.Width - (int)size.X) / 2 - 24;
        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), boxX, 16, (int)size.X + 48, (int)size.Y + 36, Color.White, 1f, false);
        Utility.drawTextWithShadow(b, help, Game1.smallFont, new Vector2(boxX + 24, 34), Game1.textColor);
        this.drawMouse(b);
    }
}
