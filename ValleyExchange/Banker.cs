using StardewValley;

namespace ValleyExchange;

/// <summary>
/// The banker: a savings account that earns interest every morning, loans paid back a bit each morning, and a wallet floor
/// (he tops your wallet up from savings before payroll). Fire him and he pays your savings out; a loan is still collected.
/// </summary>
internal static class Banker
{
    private static readonly int[] Amounts = { 1_000, 10_000, 100_000 };
    private static readonly int[] Floors = { 0, 1_000, 5_000, 20_000, 50_000 };

    /// <summary>Before payroll: interest, the loan payment, then the wallet top-up.</summary>
    public static void Morning(MarketState state)
    {
        var player = Game1.player;
        bool open = Market.IsHired(state, Role.Banker);
        if (!open && state.Savings > 0)
        {
            player.Money += (int)state.Savings;
            Game1.addHUDMessage(new HUDMessage($"No banker on the payroll: your savings ({state.Savings:N0}g) were paid out.", HUDMessage.newQuest_type));
            state.Savings = 0;
        }
        var notes = new List<string>();
        if (open && state.Savings > 0)
        {
            long interest = Market.SavingsInterest(state.Savings, CrewSkills.Total(Role.Banker, PerkKind.Yield));
            state.Savings += interest;
            CrewReport.Earned(Role.Banker, interest);
            CrewReport.Count(Role.Banker, "g interest earned", (int)Math.Min(interest, int.MaxValue));
            notes.Add($"Your savings earned {interest:N0}g overnight.");
        }
        if (state.Loan > 0)
        {
            long interest = Market.LoanInterest(state.Loan, CrewSkills.Total(Role.Banker, PerkKind.LowRate));
            state.Loan += interest;
            CrewReport.Paid(Role.Banker, interest);
            long due = Market.LoanPayment(state.Loan), fromWallet = Math.Min(due, player.Money), fromSavings = Math.Min(due - fromWallet, state.Savings);
            player.Money -= (int)fromWallet;
            state.Savings -= fromSavings;
            state.Loan -= fromWallet + fromSavings;
            notes.Add(state.Loan == 0 ? "Loan's paid off. Pleasure doin' business." : $"Took {fromWallet + fromSavings:N0}g toward the loan; {state.Loan:N0}g to go.");
        }
        if (open && state.WalletFloor > player.Money && state.Savings > 0)
        {
            long topUp = Math.Min(state.WalletFloor - player.Money, state.Savings);
            state.Savings -= topUp;
            player.Money += (int)topUp;
            notes.Add($"Moved {topUp:N0}g to your wallet for payroll.");
        }
        if (open)
            Staff.Reports[Role.Banker] = notes.Count > 0 ? string.Join(" ", notes) : "Books are balanced, boss.";
    }

    /// <summary>Talk to the banker: his statement, and the account options.</summary>
    public static void Talk(MarketState state, string name, Action<string> say)
    {
        long limit = Market.LoanLimit - state.Loan;
        string statement = $"Savings: {state.Savings:N0}g, earnin' {Skills.Pct(Market.SavingsRate * (1 + CrewSkills.Total(Role.Banker, PerkKind.Yield)))} a day."
            + (state.Loan > 0 ? $" You owe {state.Loan:N0}g." : "")
            + (state.WalletFloor > 0 ? $" Keepin' {state.WalletFloor:N0}g in your wallet." : "");
        var options = new List<Response>();
        if (Game1.player.Money > 0)
            options.Add(new Response("deposit", "Make a deposit"));
        if (state.Savings > 0)
            options.Add(new Response("withdraw", "Make a withdrawal"));
        if (limit > 0)
            options.Add(new Response("borrow", $"Take out a loan ({Skills.Pct(Market.LoanRate * (1 - CrewSkills.Total(Role.Banker, PerkKind.LowRate)))} a day)"));
        if (state.Loan > 0 && Game1.player.Money + state.Savings > 0)
            options.Add(new Response("repay", "Pay off the loan"));
        int next = Floors[(Array.IndexOf(Floors, state.WalletFloor) + 1) % Floors.Length];
        options.Add(new Response("floor", next == 0 ? "Stop toppin' up my wallet" : $"Keep {next:N0}g in my wallet each morning"));

        Personas.Ask(name, Role.Banker, $"{name}: \"{statement}\"", options, answer =>
        {
            switch (answer)
            {
                case "floor":
                    state.WalletFloor = next;
                    say(next == 0 ? "Your wallet, your problem." : $"Every mornin', {next:N0}g in your pocket.");
                    Game1.playSound("purchaseClick");
                    break;
                case "deposit":
                    AskAmount(name, "How much are we depositin'?", () => Game1.player.Money, amount =>
                    {
                        Game1.player.Money -= amount;
                        state.Savings += amount;
                        say($"{amount:N0}g, safe and sound.");
                    });
                    break;
                case "withdraw":
                    AskAmount(name, "How much ya need?", () => state.Savings, amount =>
                    {
                        state.Savings -= amount;
                        Game1.player.Money += amount;
                        say($"Here's {amount:N0}g. Spend it wise.");
                    });
                    break;
                case "borrow":
                    AskAmount(name, $"I can lend ya up to {limit:N0}g. A little back every mornin'.", () => Market.LoanLimit - state.Loan, amount =>
                    {
                        state.Loan += amount;
                        Game1.player.Money += amount;
                        say($"{amount:N0}g. Don't make me send Hank.");
                    });
                    break;
                case "repay":
                    long pay = Math.Min(state.Loan, Game1.player.Money + state.Savings), fromWallet = Math.Min(pay, Game1.player.Money);
                    Game1.player.Money -= (int)fromWallet;
                    state.Savings -= pay - fromWallet;
                    state.Loan -= pay;
                    say(state.Loan == 0 ? "Paid in full. Ah like you, boss." : $"Paid {pay:N0}g. {state.Loan:N0}g left.");
                    Game1.playSound("purchaseClick");
                    break;
            }
        });
    }

    /// <summary>Pick an amount; it's re-checked against what's there when you answer.</summary>
    private static void AskAmount(string name, string prompt, Func<long> available, Action<int> done)
    {
        long max = available();
        var options = Amounts.Where(a => a < max).Select(a => new Response(a.ToString(), $"{a:N0}g"))
            .Append(new Response(Math.Min(max, int.MaxValue).ToString(), $"All of it ({max:N0}g)"))
            .Append(new Response("bye", "Never mind"))
            .ToArray();
        DelayedAction.functionAfterDelay(() => Game1.currentLocation.createQuestionDialogue($"{name}: \"{prompt}\"", options, (_, answer) =>
        {
            if (!int.TryParse(answer, out int amount) || (amount = (int)Math.Min(amount, available())) <= 0)
                return;
            done(amount);
            Game1.playSound("money");
        }, null), 50);
    }
}
