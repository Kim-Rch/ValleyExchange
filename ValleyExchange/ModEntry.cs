using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Buildings;

namespace ValleyExchange;

/// <summary>Subset of ValleyTalk's public API (dandm1.ValleyTalk, IValleyTalkInterface); SMAPI proxies it.</summary>
public interface IValleyTalkApi
{
    void SetModName(string modName);
    void RegisterPromptOverride(string characterName, string promptElement, string overrideText);
    void ClearPromptOverrides(string characterName = "");
}

public sealed class ModEntry : Mod
{
    /// <summary>NPCs whose AI dialogue knows about the farmer's portfolio.</summary>
    private static readonly string[] StockTalkers = { "Olivia" };

    // shortcut: ValleyTalk overrides replace a whole prompt element; "Trinkets" is the least useful one to lose (only mentions equipped trinkets). Switch if ValleyTalk adds an append API.
    private const string PromptElement = "Trinkets";

    private IValleyTalkApi? valleyTalk;

    private const string TerminalId = "evant.ValleyExchange_Terminal";
    private const string QualifiedTerminal = "(BC)" + TerminalId;
    private const string TextureAsset = "Mods/evant.ValleyExchange/Terminal";
    private const string MailId = "evant.ValleyExchange_Welcome";
    private const string GuildMail = "evant.ValleyExchange_BrawnieGuild";
    private const string SaveKey = "market";

    private MarketState? state;
    private string? drama; // today's oil-town event, for the AI brief
    private int ranchAnimals = -1; // animals the ranch hands tended today, -1 if none
    private int fieldCrops = -1; // crops the field hands tended today, -1 if none
    private bool routesLogged;

    internal static int Seed => (int)Game1.uniqueIDForThisGame;

    public override void Entry(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += this.OnAssetRequested;
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        helper.Events.GameLoop.Saving += this.OnSaving;
        helper.Events.GameLoop.DayEnding += (_, _) =>
        {
            if (this.state != null && Context.IsMainPlayer)
            {
                Staff.Evening(this.state, Game1.Date.TotalDays); // hauls land in the crate before the night
                Oil.PriceShipments(this.state);
                Science.Evening(this.state); // after the oil's priced: the shipping bonus counts it
            }
        };
        helper.Events.World.ObjectListChanged += Oil.OnObjectListChanged;
        helper.Events.World.ObjectListChanged += Science.OnObjectListChanged;
        helper.Events.GameLoop.UpdateTicked += (_, _) =>
        {
            if (this.state != null && Context.IsMainPlayer)
                CrewReport.TryShow(this.CrewTexture);
            WhenFree.Tick();
        };
        helper.Events.GameLoop.TimeChanged += (_, e) =>
        {
            if (this.state != null)
                Science.OnTimeChanged(this.state, e.NewTime); // Medicine: second wind at noon
                Shopkeeper.OnTimeChanged(this.state, e.NewTime); // back from market with the seeds
            // the farm map only runs while you stand on it; away from it, the hands work in 10-minute steps
            if (this.state != null && Context.IsMainPlayer && Game1.currentLocation != Game1.getFarm())
                Workers.Offscreen();
            if (this.state != null && Context.IsMainPlayer)
            {
                Travel.Tick(); // set off / arrive
                Lands.Offscreen(); // sheds, greenhouse, island... wherever you aren't
                Lands.Attach(Game1.currentLocation, this.CrewTexture);
                Travel.Attach(Game1.currentLocation, this.CrewTexture);
            }
            if (this.state != null)
                Outposts.Attach(Game1.currentLocation, this.CrewTexture); // shifts start and end on the hour
            if (this.state != null && Context.IsMainPlayer && Game1.timeOfDay >= 1800)
                Staff.Evening(this.state, Game1.Date.TotalDays); // off-farm hands are home with their haul (once a day)
        };
        helper.Events.Player.Warped += (_, e) =>
        {
            if (e.IsLocalPlayer && this.state != null)
            {
                Workers.Attach(e.NewLocation);
                Outposts.Attach(e.NewLocation, this.CrewTexture);
                Lands.Attach(e.NewLocation, this.CrewTexture);
                Travel.Attach(e.NewLocation, this.CrewTexture);
            }
        };
        helper.Events.GameLoop.ReturnedToTitle += (_, _) =>
        {
            this.state = null;
            Workers.Clear();
            Travel.Reset();
            Staff.Reports.Clear();
            CrewReport.Clear();
            this.valleyTalk?.ClearPromptOverrides();
        };
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;

        helper.ConsoleCommands.Add("vx_terminal", "Give yourself a Valley Exchange trading terminal.", (_, _) =>
        {
            if (Context.IsWorldReady)
                Game1.player.addItemByMenuIfNecessary(ItemRegistry.Create(QualifiedTerminal));
        });
        helper.ConsoleCommands.Add("vx_open", "Open the Valley Exchange menu.", (_, _) => this.OpenMenu());
        helper.ConsoleCommands.Add("vx_routes", "Show the routes hired hands walk to their work spots.", (_, _) =>
        {
            if (Context.IsWorldReady)
                this.Monitor.Log("Hand routes:\n" + Travel.DescribeRoutes(), LogLevel.Info);
        });
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
        Oil.OnAssetRequested(e);
        Science.OnAssetRequested(e);
        if (e.NameWithoutLocale.IsEquivalentTo(TextureAsset))
            e.LoadFromModFile<Texture2D>("assets/terminal.png", AssetLoadPriority.Exclusive);
        else if (e.NameWithoutLocale.IsEquivalentTo(Workers.TextureAsset))
            e.LoadFromModFile<Texture2D>("assets/workers.png", AssetLoadPriority.Exclusive);
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Buildings"))
        {
            // hired hands live in cabins, so make sure Robin will build one even in single-player
            e.Edit(asset =>
            {
                if (asset.AsDictionary<string, BuildingData>().Data.TryGetValue("Cabin", out var cabin) && cabin.BuildCondition?.Contains("MULTIPLAYER", StringComparison.OrdinalIgnoreCase) == true)
                {
                    this.Monitor.Log($"Allowing cabins in single-player (was '{cabin.BuildCondition}').", LogLevel.Trace);
                    cabin.BuildCondition = null;
                }
            }, AssetEditPriority.Late);
        }
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
            e.Edit(asset => asset.AsDictionary<string, string>().Data[GuildMail] =
                "@,^^Greetings from the Brawnie Labor Guild, Local 7.^^"
                + "We hear you've taken on some of our members. Good choice. Brawnies are knee-high, short-tempered, and the finest paid labor in the valley. "
                + "We come up from the Hollow under the mountains, and every one of us carries a contract in one pocket and a grudge in the other. Our motto: no coin, no toil.^^"
                + "About that grudge. You may have seen small, glowing 'helpers' bouncing around the valley. Junimos. "
                + "Three hundred years ago the farmers here hired Brawnies. Then the Junimos started working for fruit, refused to sign anything, and the farmers stopped calling us. "
                + "We call it the Great Undercutting. Our members may voice opinions about it. Loudly. Often.^^"
                + "Pay your crew on time and they'll move mountains. Short ones.^^   -Grizzlebeard, Shop Steward[#]Welcome to the Union");
            e.Edit(asset => asset.AsDictionary<string, string>().Data[MailId] =
                "Dear @,^^Word around Pelican Town is you've got money to burn. Ferngill Securities would be honored to help you burn it faster.^^"
                + "Place this terminal in your house and click it to trade on the Valley Exchange. Prices move every morning; dividends are paid at the start of each season.^^"
                + "   -Ferngill Securities %item id " + QualifiedTerminal + " 1 %%[#]Welcome to the Valley Exchange");
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.valleyTalk = this.Helper.ModRegistry.GetApi<IValleyTalkApi>("dandm1.ValleyTalk");
        if (this.valleyTalk == null)
            return;
        this.valleyTalk.SetModName(this.ModManifest.UniqueID);
        ValleyTalkTweaks.Apply(this.ModManifest.UniqueID, this.Monitor);
        Science.Patch(this.ModManifest.UniqueID, this.Monitor, this.Helper.GameContent);
        this.Monitor.Log($"ValleyTalk found: {string.Join(", ", StockTalkers)} will know about your portfolio.", LogLevel.Info);
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
        var lines = new List<string>();
        string farmer = Game1.player.Name, his = Game1.player.IsMale ? "his" : "her";
        var (pumpjacks, gushers) = Oil.CountWells();
        if (pumpjacks > 0)
            lines.Add($"{farmer} has struck oil: {pumpjacks} pumpjack{(pumpjacks == 1 ? "" : "s")} on {his} land{(gushers > 0 ? $", {gushers} on gushers" : "")}. "
                + $"Crude sells for {Market.CrudePrice(this.state!)}g a barrel today ({Math.Round(Market.CrudeChange(this.state!), 3):+0.0%;-0.0%;0.0%}).");
        if (this.drama != null)
            lines.Add(this.drama);
        if (this.ranchAnimals >= 0)
            lines.Add($"{farmer} pays a crew of ranch hands to tend {his} {this.ranchAnimals} farm animals.");
        var staff = this.state!.Crew.Where(c => c.Key is not (Role.Field or Role.Ranch)).Select(c => $"{c.Value} {Market.RoleInfo(c.Key).Title.ToLower()}").ToList();
        if (staff.Count > 0)
            lines.Add($"{farmer} also employs: {string.Join(", ", staff)}.");
        if (this.state.TripReturnDay >= 0)
            lines.Add($"{farmer}'s salesman is on the road selling farm goods in {(this.state.TripToCity ? "the city" : "town")}.");
        if (Market.HeadCount(this.state!) > 0)
            lines.Add($"{farmer}'s hired crew are Brawnies: knee-high, gold-loving working folk from the Hollow under the mountains who demand fair wages ('no coin, no toil') and loudly despise the Junimos for working for fruit.");
        if (this.fieldCrops >= 0)
            lines.Add($"{farmer} pays field hands to water and harvest {his} {this.fieldCrops} crops.");
        return Market.PortfolioBrief(this.state!, farmer, npc, lines);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsMainPlayer)
            return; // shortcut: single-player/host only, farmhands can't trade; add multiplayer messaging if anyone needs it

        if (this.state == null)
        {
            this.state = this.Helper.Data.ReadSaveData<MarketState>(SaveKey) ?? new MarketState();
            Market.Normalize(this.state); // older saves: a band of adventurers becomes one Guild Master
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

        Market.Migrate(this.state, Workers.Cabins().Count()); // saves from before hand counts
        if (Market.HeadCount(this.state) > 0 && !Workers.Cabins().Any())
        {
            this.state.Crew.Clear();
            Game1.addHUDMessage(new HUDMessage("Your hired hands left: there's no cabin for them to live in.", HUDMessage.error_type));
        }
        Harvester.PickedToday = 0;
        Staff.NewDay();
        MachineTender.NewDay();
        Banker.Morning(this.state); // before payroll: interest, loan payment, wallet top-up
        Science.Morning(this.state, today);
        this.ranchAnimals = RanchHands.Work(this.state);
        this.fieldCrops = FieldHands.Work(this.state);
        Staff.Morning(this.state, today);
        this.RefreshCrew(newDay: true);
        CrewSkills.NewDay(this.state); // daily XP for everyone on the payroll
        if (!this.routesLogged)
        {
            this.routesLogged = true; // once per session: handy when the SMAPI console isn't open
            this.Monitor.Log("Hand routes:\n" + Travel.DescribeRoutes(), LogLevel.Info);
        }
        this.drama = Oil.DailyDrama(Oil.CountWells().Pumpjacks, today, Market.IsHired(this.state, Role.Oil));

        foreach (string mail in new[] { MailId, Oil.IntroMail }.Concat(Market.HeadCount(this.state) > 0 ? new[] { GuildMail } : Array.Empty<string>()))
        {
            if (!Game1.player.mailReceived.Contains(mail) && !Game1.player.mailbox.Contains(mail))
                Game1.player.mailbox.Add(mail);
        }

        CrewReport.Ready(); // yesterday + this morning, on one screen once it's free
        this.UpdateValleyTalk();
    }

    private Texture2D CrewTexture => this.Helper.GameContent.Load<Texture2D>(Workers.TextureAsset);

    private void RefreshCrew(bool newDay)
    {
        if (newDay)
        {
            Outposts.Reset(); // crab pots move, shops get rebuilt: find spots fresh each day
            Scavenger.Reset();
            Lands.Reset();
            Travel.Reset(); // everyone wakes up at the cabins
        }
        if (this.state != null)
            Workers.Refresh(this.state, this.CrewTexture, newDay);
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        if (this.state != null && Context.IsMainPlayer)
            this.Helper.Data.WriteSaveData(SaveKey, this.state);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsPlayerFree || !e.Button.IsActionButton())
            return;

        // the sprite is two tiles tall, so clicking the monitor half lands on the tile above the object
        if (Travel.At(e.Cursor.AbsolutePixels) is { } traveler)
        {
            this.Helper.Input.Suppress(e.Button);
            traveler.Talk();
            return;
        }
        if (Lands.At(e.Cursor.AbsolutePixels) is { } landHand)
        {
            this.Helper.Input.Suppress(e.Button);
            landHand.Talk();
            return;
        }
        if (Outposts.At(e.Cursor.AbsolutePixels) is { } visitor)
        {
            this.Helper.Input.Suppress(e.Button);
            visitor.Talk();
            return;
        }
        if (Workers.At(e.Cursor.AbsolutePixels) is { } worker)
        {
            this.Helper.Input.Suppress(e.Button);
            worker.Talk();
            return;
        }

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
        Game1.activeClickableMenu = new StockMenu(this.state)
        {
            exitFunction = () =>
            {
                this.UpdateValleyTalk();
                this.RefreshCrew(newDay: false); // hires/fires take effect right away
            },
        };
    }
}
