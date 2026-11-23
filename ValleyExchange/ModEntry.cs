using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.BigCraftables;

namespace ValleyExchange;

/// <summary>Subset of ValleyTalk's public API (dandm1.ValleyTalk, IValleyTalkInterface); SMAPI proxies it.</summary>
public interface IValleyTalkApi
{
    void SetModName(string modName);
    void RegisterPromptOverride(string characterName, string promptElement, string overrideText);
    void ClearPromptOverrides(string characterName = "");
}

/// <summary>Brawnies' API (evant.Brawnies, optional): lines about the crew and the oil field for the AI brief.</summary>
public interface IBrawniesApi
{
    IEnumerable<string> BriefLines();
}

/// <summary>The Valley Exchange: a stock market you trade from a terminal. The hired crew and the oil field live in Brawnies.</summary>
public sealed class ModEntry : Mod
{
    /// <summary>NPCs whose AI dialogue knows about the farmer's portfolio.</summary>
    private static readonly string[] StockTalkers = { "Olivia" };

    // shortcut: ValleyTalk overrides replace a whole prompt element; "Trinkets" is the least useful one to lose (only mentions equipped trinkets). Switch if ValleyTalk adds an append API.
    private const string PromptElement = "Trinkets";

    private const string TerminalId = "evant.ValleyExchange_Terminal";
    private const string QualifiedTerminal = "(BC)" + TerminalId;
    private const string TextureAsset = "Mods/evant.ValleyExchange/Terminal";
    private const string MailId = "evant.ValleyExchange_Welcome";
    private const string SaveKey = "market";

    private IValleyTalkApi? valleyTalk;
    private IBrawniesApi? brawnies;
    private MarketState? state;

    internal static int Seed => (int)Game1.uniqueIDForThisGame;

    public override void Entry(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += this.OnAssetRequested;
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        helper.Events.GameLoop.Saving += (_, _) =>
        {
            if (this.state != null && Context.IsMainPlayer)
                this.Helper.Data.WriteSaveData(SaveKey, this.state);
        };
        helper.Events.GameLoop.ReturnedToTitle += (_, _) =>
        {
            this.state = null;
            this.valleyTalk?.ClearPromptOverrides();
        };
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;

        helper.ConsoleCommands.Add("vx_terminal", "Give yourself a Valley Exchange trading terminal.", (_, _) =>
        {
            if (Context.IsWorldReady)
                Game1.player.addItemByMenuIfNecessary(ItemRegistry.Create(QualifiedTerminal));
        });
        helper.ConsoleCommands.Add("vx_open", "Open the Valley Exchange menu.", (_, _) => this.OpenMenu());
        helper.ConsoleCommands.Add("vx_brief", "Show what ValleyTalk NPCs are told about your portfolio.", (_, _) =>
        {
            if (this.state == null)
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
            else
                this.Monitor.Log($"ValleyTalk {(this.valleyTalk == null ? "NOT connected" : "connected")}, prompt element '{PromptElement}':\n{this.Brief(StockTalkers[0])}", LogLevel.Info);
        });
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(TextureAsset))
            e.LoadFromModFile<Texture2D>("assets/terminal.png", AssetLoadPriority.Exclusive);
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/BigCraftables"))
        {
            e.Edit(asset => asset.AsDictionary<string, BigCraftableData>().Data[TerminalId] = new BigCraftableData
            {
                Name = TerminalId,
                DisplayName = "Trading Terminal",
                Description = "Your window into the Valley Exchange. Buy low, sell high, blame the Wizard.",
                Price = 0,
                CanBePlacedIndoors = true,
                CanBePlacedOutdoors = false,
                Texture = TextureAsset,
                SpriteIndex = 0,
            });
        }
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/mail"))
        {
            e.Edit(asset => asset.AsDictionary<string, string>().Data[MailId] =
                "Dear @,^^Word around Pelican Town is you've got money to burn. Ferngill Securities would be honored to help you burn it faster.^^"
                + "Place this terminal in your house and click it to trade on the Valley Exchange. Prices move every morning; dividends are paid at the start of each season.^^"
                + "   -Ferngill Securities %item id " + QualifiedTerminal + " 1 %%[#]Welcome to the Valley Exchange");
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.brawnies = this.Helper.ModRegistry.GetApi<IBrawniesApi>("evant.Brawnies");
        this.valleyTalk = this.Helper.ModRegistry.GetApi<IValleyTalkApi>("dandm1.ValleyTalk");
        if (this.valleyTalk == null)
            return;
        this.valleyTalk.SetModName(this.ModManifest.UniqueID);
        ValleyTalkTweaks.Apply(this.ModManifest.UniqueID, this.Monitor);
        this.Monitor.Log($"ValleyTalk found: {string.Join(", ", StockTalkers)} will know about your portfolio{(this.brawnies != null ? ", crew and oil field" : "")}.", LogLevel.Info);
    }

    /// <summary>Refresh what ValleyTalk NPCs know about the market. Called each morning and after trading.</summary>
    private void UpdateValleyTalk()
    {
        if (this.valleyTalk == null || this.state == null)
            return;
        try
        {
            foreach (string npc in StockTalkers)
                this.valleyTalk.RegisterPromptOverride(npc, PromptElement, this.Brief(npc));
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Couldn't update ValleyTalk, NPCs won't mention stocks: {ex.Message}", LogLevel.Warn);
            this.valleyTalk = null;
        }
    }

    private string Brief(string npc)
    {
        IEnumerable<string> business = Array.Empty<string>();
        try
        {
            business = this.brawnies?.BriefLines().ToList() ?? business; // the crew and the oil field, when Brawnies is installed
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Brawnies didn't answer for the brief: {ex.Message}", LogLevel.Trace);
        }
        return Market.PortfolioBrief(this.state!, Game1.player.Name, npc, business);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsMainPlayer)
            return; // shortcut: single-player/host only, farmhands can't trade; add multiplayer messaging if anyone needs it

        if (this.state == null)
        {
            this.state = this.Helper.Data.ReadSaveData<MarketState>(SaveKey) ?? new MarketState();
            Market.EnsureStocks(this.state, Seed); // fills a new market, or companies added since this save was made
        }

        int today = Game1.Date.TotalDays;
        if (this.state.LastDay < 0)
            this.state.LastDay = today; // fresh market: opens today with its warm-up history
        else if (this.state.LastDay < today)
        {
            var ctx = new DayContext(Game1.seasonIndex, Game1.dayOfMonth, Game1.isRaining || Game1.isLightning, Utility.isFestivalDay());
            var result = Market.AdvanceDay(this.state, today, Seed, ctx);
            if (result.Dividends > 0)
            {
                Game1.player.Money += (int)Math.Min(result.Dividends, int.MaxValue - Game1.player.Money);
                Game1.addHUDMessage(new HUDMessage($"Valley Exchange dividends: +{result.Dividends:N0}g", HUDMessage.achievement_type));
            }
        }

        if (!Game1.player.mailReceived.Contains(MailId) && !Game1.player.mailbox.Contains(MailId))
            Game1.player.mailbox.Add(MailId);
        this.UpdateValleyTalk();
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsPlayerFree || !e.Button.IsActionButton())
            return;
        // the sprite is two tiles tall, so clicking the monitor half lands on the tile above the object
        var tile = e.Cursor.GrabTile;
        if (this.IsTerminal(tile) || this.IsTerminal(tile + new Vector2(0, 1)))
        {
            this.Helper.Input.Suppress(e.Button);
            this.OpenMenu();
        }
    }

    private bool IsTerminal(Vector2 tile) =>
        Game1.currentLocation?.objects.TryGetValue(tile, out var obj) == true && obj.QualifiedItemId == QualifiedTerminal;

    private void OpenMenu()
    {
        if (!Context.IsWorldReady || this.state == null)
        {
            this.Monitor.Log("The Valley Exchange is only available to the main player in a loaded save.", LogLevel.Warn);
            return;
        }
        Game1.playSound("bigSelect");
        Game1.activeClickableMenu = new StockMenu(this.state) { exitFunction = this.UpdateValleyTalk };
    }
}
