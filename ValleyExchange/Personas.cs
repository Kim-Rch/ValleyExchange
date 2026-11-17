using StardewValley;

namespace ValleyExchange;

/// <summary>A Brawnie's personality: what kind of Brawnie, a short bio, their everyday lines, and their own take on the Junimos.</summary>
internal sealed record Persona(string Kind, string Bio, string[] Lines, string[] Junimos);

/// <summary>
/// Every hand is a Brawnie with a personality: the crew leads are one of a kind, everyone else is one of twelve Brawnie
/// archetypes (fixed per name). Southern dwarves at heart: drawls, beards, grudges, and opinions about Junimos.
/// </summary>
internal static class Personas
{
    private static readonly Dictionary<string, Persona> Leads = new()
    {
        // field crew
        ["Dusty"] = new("Laid-back drawler", "Slowest talker in the Hollow, fastest hands in the field. Reckons hurryin' is for Junimos.",
            new[] { "Easy does it. Dirt ain't goin' nowhere.", "Ah could nap under that tree till Tuesday.", "Crops grow slow. Ah respect that.", "Water, dig, get paid. Simple life.", "Mah beard's got more dirt in it than this field.", "Ain't no rush. Never was." },
            new[] { "Junimos rush everything. Bouncin' around like their tails're on fire.", "Ah don't hate 'em. Ah just don't like 'em. Big difference.", "Free labor ain't free, boss. Somebody pays. Usually us." }),
        ["Hank"] = new("Grumpy old-timer", "Two hundred years of farm work and a bad back. Hates crows, Junimos, and anything young.",
            new[] { "Back's killin' me. Again.", "Who planted these so damn close?", "Crows. Hate crows.", "In mah day we had real hoes. Iron ones.", "Hmph.", "Kids these days can't tell a turnip from a rock." },
            new[] { "Lost mah first job to a Junimo. Still got the grudge. Still sharpenin' it.", "Glowin' little bastards.", "Ah'd sooner hire a crow than a Junimo. And Ah HATE crows." }),
        ["Earl"] = new("Storyteller", "Has a story for everything, most of 'em true. Remembers the Great Undercutting like it was yesterday.",
            new[] { "Did Ah ever tell ya about '62?", "Mah pappy farmed this valley before the Junimos came.", "Corn was taller back then. Ask anybody.", "Weather's turnin'. Mah knee says so.", "You remind me of mah nephew. He was rich too. For a week." },
            new[] { "Three hundred years ago they showed up with fruit and smiles. By winter half the Hollow was outta work.", "Ah was there for the Undercuttin'. Saw it with these two eyes.", "They took our jobs and called it 'magic'. Ah call it scabbin'." }),
        // ranch crew
        ["Bubba"] = new("Big softie", "Built like a boulder, cries at calf births, names every animal. Would fight a Junimo for any of 'em.",
            new[] { "Who's a good cow? You are!", "Ah named 'em all. Don't tell nobody.", "Smells like home.", "That chicken looked at me funny. Ah love her anyway.", "Animals don't lie, boss." },
            new[] { "If a Junimo touches mah cows, Ah'll punt it clean to Zuzu City.", "Junimos don't name animals. Says everything ya need to know.", "Ah'm a gentle soul, boss. Except about Junimos." }),
        ["Waylon"] = new("Crooner", "Sings to the cows, sings to the chickens, sings at the worst possible times.",
            new[] { "Hmm hm hmmm...", "Singin' to the cows again.", "Got a song for every cow.", "Somebody get me a banjo.", "Ridin' the range, la la la..." },
            new[] { "Wrote a song about the Junimos. Can't sing it in front of Olivia.", "Junimos hum off-key. Every damn time.", "Ah'd write 'em a ballad, but they'd do it for free and steal mah gig." }),
        ["Cletus"] = new("Sweet dimwit", "Heart of gold, head of granite. Has lost his boots four times this season.",
            new[] { "Is that a cow or a big dog?", "Ah lost mah other boot.", "How do eggs get in there?", "Ah forgot what Ah was doin'.", "Hey, a bug!" },
            new[] { "Are Junimos edible? Askin' for a friend.", "Hank says Junimos are bad. Ah agree with Hank. Who's Hank?", "Ah tried talkin' to one. It just went 'boop'. Rude." }),
        // specialists
        ["Salty"] = new("Old sea dog", "Ran off from the Hollow to see the ocean and never went back. Exaggerates every fish.",
            new[] { "Caught a fish THIS big once.", "Sea's callin' me, boss.", "Fish bite better at dawn.", "Patience. That's the whole trick.", "Smell that? Fish. Beautiful." },
            new[] { "Never seen a Junimo catch a fish. Can't hold a rod with no thumbs.", "Junimos float. Ah tested it. Don't ask.", "The sea don't take scabs. That's why Ah love her." }),
        ["Marla"] = new("Tough gal", "Arms like anchor chains. Takes no lip from crabs, men, or Junimos.",
            new[] { "Crabs bite. Ah bite back.", "Don't touch mah traps.", "Hands are numb. Worth it.", "Tide waits for nobody, sugar.", "Ah'll arm wrestle ya for a raise, boss." },
            new[] { "Caught a Junimo in a crab pot once. Threw it back. Felt bad. For the pot.", "Junimos talk big for somethin' that fits in a teacup.", "Let one of 'em try and bait a pot. Go ahead. Ah'll wait." }),
        ["Buck"] = new("Gruff miner", "Born in the Hollow, raised in the dark. Speaks rarely, swears often.",
            new[] { "Rock solid, boss.", "Dark down there today. Good.", "Found a gem. Maybe two. Ain't tellin'.", "Lost mah lamp again. Damn it.", "Mines don't scare me. People do." },
            new[] { "Junimos can't dig. Soft little hands. Useless.", "Ain't a Junimo alive that's swung a pick. Says it all.", "Down in the Hollow we got a word for Junimos. Can't say it in front of the chickens." }),
        ["Rex"] = new("Braggart Guild Master", "Claims he's slain a hundred monsters. Has slain at least twelve. Now he runs the Guild and takes credit for everyone's kills.",
            new[] { "Killed a slime with one hand.", "Bats? Pfft. Easy.", "Danger's mah middle name.", "Gonna need a bigger sword.", "Ever seen a void spirit? Ah have. It ran." },
            new[] { "Ah could take ten Junimos. Twenty. With mah eyes closed.", "Junimos only show up AFTER the monsters are dead. Cowards.", "One day Ah'll find their hideout. Then we'll talk." }),
        ["Beau"] = new("Cajun cook", "Learned to cook in the bayou under the Hollow. Believes butter is a food group.",
            new[] { "Needs more butter, cher.", "Mmm, Ah smell a recipe.", "Ain't nothin' fried can't fix.", "Taste this! No, wait...", "Cookin' is love, cher." },
            new[] { "Junimos eat raw fruit. RAW. Like animals, cher.", "Ah'd cook for a Junimo if it paid. It never pays.", "Tried a Junimo recipe once. It was just a berry. A BERRY." }),
        ["Slick"] = new("Hustler", "Fastest mouth in the valley. Has never paid full price for anything.",
            new[] { "Everything's for sale, boss.", "Ah could sell sand to a crab.", "Buy low, sell high!", "Time is money, money is time.", "Got a deal for ya..." },
            new[] { "Tried sellin' to a Junimo. No wallet. No pockets either.", "Junimos give stuff away for FREE. It's an insult to commerce.", "Ah'd sell the Junimos their own forest if they had a dime." }),
        ["Duke"] = new("Oilman", "Smells like crude, laughs like thunder, loves anything that pumps.",
            new[] { "Smell that? Money.", "Black gold, boss!", "She's pumpin' good today.", "Hand me that wrench.", "Don't tell Emily about this one." },
            new[] { "If Junimos could pump oil they'd do it for a damn plum.", "The forest folk hate our rigs. Good. Hate 'em right back.", "Spilled crude on a Junimo once. Best day of mah life." }),
        ["Rosie"] = new("Sweet gossip", "Sweetest smile on the farm and the sharpest tongue behind it.",
            new[] { "Ooh, this one's ripe!", "Basket's gettin' heavy.", "Bees were friendly today.", "Nothin' beats fresh fruit.", "Heard Pam and Gus had words. Don't say Ah said." },
            new[] { "Junimos pick fruit for free. And they BRUISE it!", "Bless their little hearts. And by that Ah mean Ah hate 'em.", "A Junimo stole mah apple once. Ah'll never forget. Never." }),
        ["Hawk"] = new("Woods mystic", "Talks to trees, listens to rivers, trusts nobody who glows.",
            new[] { "Smell that pine?", "Mushrooms grow where nobody looks.", "Every tree Ah cut, Ah plant one.", "Saw a rabbit big as a dog.", "The woods talk, if ya listen." },
            new[] { "The woods whisper about Junimos. Mostly complaints.", "Junimos think they own the forest. The forest disagrees.", "Ah respect every creature in these woods. Almost every one." }),
        ["Smitty"] = new("Fire-breathin' smith", "Forged his first horseshoe at age six, set his beard on fire at age seven. Still fond of both.",
            new[] { "Hot iron waits for nobody.", "Pass the coal, boss.", "Lost mah eyebrows again. Worth it.", "Ain't nothin' a hammer can't fix.", "Clint? Good fella. Slow, though." },
            new[] { "Junimos can't stand heat. Ah keep the forge HOT.", "Never seen a Junimo hold a hammer. Never will.", "Ah'd melt down every Junimo statue in the valley. Ain't many. Shame." }),
        ["Mabel"] = new("Prim perfectionist", "Runs her machines like a church choir. Will re-do a cheese wheel three times if it ain't right.",
            new[] { "A proper cheese takes patience, sugar.", "Mind your boots in mah workshop.", "This mayonnaise is a work of art.", "Ah don't rush quality. Ah IRON it.", "Wool's a little scratchy this week." },
            new[] { "Junimos leave crumbs. EVERYWHERE.", "A Junimo once touched mah loom. Ah had to burn it. The loom, sadly.", "Bless their hearts, they wouldn't know craft if it bit 'em." }),
        ["Scoot"] = new("Motor-mouth courier", "Knows every porch, every dog, and every secret in Pelican Town. Delivers two of the three.",
            new[] { "Special delivery, boss!", "Haley's dog chased me again.", "Ah know where everybody lives. Ain't that somethin'.", "Gus tipped me a cookie!", "Mail's mah middle name. Well, Mailford." },
            new[] { "Junimos don't have mailboxes. Suspicious.", "Ah tried deliverin' to the Community Center once. Got a raisin for a tip.", "Fastest legs in the valley, and Ah STILL can't catch one of them glowy things." }),
        ["Sterling"] = new("Penny-pinchin' banker", "Wears a three-piece suit to a farm. Counts every coin twice and trusts nobody who rounds up.",
            new[] { "A penny saved is a penny Ah get to count.", "Interest never sleeps, boss. Neither do Ah.", "Ah ironed this vest for ya.", "Money talks. Mine just whispers.", "Never lend to a crow." },
            new[] { "Junimos work for fruit. No savings, no pension, no plan. Disgraceful.", "Ah ran the numbers on the Junimos. They're a net loss to this valley.", "Ah'd never give a Junimo credit. Not a single raisin." }),
        ["Doc"] = new("Mad scientist", "The only Brawnie with a doctorate. In Junimology. Self-awarded. Has blown up two labs and one eyebrow.",
            new[] { "Hypothesis: more gold makes better science.", "Don't touch that. Or that. Especially THAT.", "Ah'm THIS close to a breakthrough, boss.", "Science is just fancy guessin' with notes.", "Mah goggles are prescription." },
            new[] { "Ah've studied Junimos for forty years. Conclusion: overrated.", "Junimo magic ain't magic. It's unpaid labor with sparkles.", "Ah dissected a Junimo theory once. Turned out it was just a gumdrop." }),
        ["Axel"] = new("Lumberjack philosopher", "Has chopped ten thousand trees and has a deep thought about every one. Wears flannel in July.",
            new[] { "A cleared field is a clear mind, boss.", "Every stump's got a story.", "Weeds are just plants with bad attitudes.", "Ah sharpen mah axe every night. It's mah therapy.", "Timber! ...sorry, habit." },
            new[] { "Junimos grow stuff. Ah clear stuff. We are natural enemies.", "Saw a Junimo nappin' on a stump Ah was about to clear. Ah cleared around it. Ah'm not a monster.", "They leave the weeds, boss. Every time. Lazy glowin' gumdrops." }),
        ["Rusty"] = new("Proud trash connoisseur", "Believes every bin holds a fortune. Is right about once a week, and never lets anybody forget it.",
            new[] { "Smell that? That's opportunity.", "Gus throws out perfectly good cheese, boss.", "Found a boot. Lookin' for its brother.", "Trash is just treasure that ain't been loved yet.", "Raccoons respect me. Ah respect them." },
            new[] { "Junimos don't make trash. Ain't natural. Who don't make trash?", "A Junimo took mah sandwich outta a can. MAH sandwich. Mah CAN.", "If Ah ever find a Junimo in a bin, it's goin' to the dump." }),
        ["Barnacle"] = new("Swashbucklin' blowhard", "Claims he sailed all seven seas. There are three near the valley. He's been to one, by ferry.",
            new[] { "Arr, boss!", "Ah can smell a storm comin'. Or that's the fish.", "A pirate's word is his bond. Mostly.", "Ever seen a kraken? Ah have. Twice. Almost.", "Wind's in our favor today." },
            new[] { "Junimos can't swim. Ah checked. Personally.", "A Junimo stowed away once. Fed it to the gulls. They spat it out.", "No Junimos on mah ship. Rule one, two and three." }),
        ["Dixie"] = new("Treasure-mad prospector", "Carries three shovels and a map nobody else can read. Found a Dwarf Scroll at age four and never recovered.",
            new[] { "Ah got a good feelin' about this spot!", "Dig first, ask later.", "That rock's lookin' at me funny. Must be somethin' under it.", "Gunther owes me a museum wing, boss.", "Mah shovel's named Betsy." },
            new[] { "Junimos bury acorns. Ah dug up forty. FORTY.", "Junimos don't dig. Too precious. Too glowy.", "If a Junimo ever finds treasure before me, Ah'll retire. Ain't gonna happen." }),
        ["Shucks"] = new("Walkin' scarecrow", "Stuffed his own shirt with straw so the crows'd take him serious. They do. Ain't blinked since spring.",
            new[] { "Caw yourself.", "Ah see you, crow. Ah ALWAYS see you.", "Straw's itchy, boss. Worth it.", "Stood so still a bird nested in mah hat. Evicted him.", "Ah don't sleep. Ah wait." },
            new[] { "Junimos ain't scared of me. That's how Ah know they're evil.", "A Junimo tried to perch on mah arm. ONCE.", "Ah'd scare off Junimos for free, boss. Just say the word." }),
        ["Mae"] = new("Sweet-talkin' haggler", "Can talk a merchant down to cost and leave him thankin' her for it. Keeps every receipt in her apron, alphabetized.",
            new[] { "Never pay sticker price, sugar.", "Pierre sees me comin' and starts sweatin'.", "Ah got us a deal. Ah always get us a deal.", "Morris tried to sell me a membership. Bless his heart.", "Coupons are a lifestyle, boss." },
            new[] { "Junimos never pay for nothin'. Can't haggle with a freeloader.", "A Junimo tried to trade me a raisin for a parsnip seed. Ah said no. Rudely.", "Ah'd sell a Junimo to Joja if they'd take it. They won't. Ah asked." }),
        ["Hooch"] = new("Lovable drunk", "Brews a mean keg and samples every one of 'em. For quality.",
            new[] { "*hic*", "Just quality testin', boss.", "This one's... *hic*... perfect.", "Who moved the keg?", "Ah'm not drunk, Ah'm aging." },
            new[] { "Junimos don't drink. Never trust a fella who don't drink. Or a blob.", "*hic* Ah'll fight one. Ah'll fight two. Where are they?", "Saw three Junimos last night. Might've been one Junimo and two drinks." }),
    };

    private static readonly Persona[] Archetypes =
    {
        new("Grumbler", "Complains about everything, works like a mule anyway.",
            new[] { "This job's a damn insult. Ah love it.", "Too hot. Too cold. Too much farm.", "Don't talk to me before noon.", "Ah've seen better dirt in a tavern.", "Hmph." },
            new[] { "Junimos. Don't get me started. Too late, Ah'm started.", "Every Junimo alive owes me a day's wage. Plus interest." }),
        new("Sweet-talker", "Could charm the bark off a tree, and the pay off a boss.",
            new[] { "Boss, you're lookin' mighty fine today.", "Ain't this just the prettiest farm.", "Ah'd follow ya to the Hollow and back, boss.", "Y'all are too kind to this ol' Brawnie." },
            new[] { "Ah try to love everybody. Junimos make that real hard.", "Bless 'em. Bless their freeloadin' little hearts." }),
        new("Hothead", "Short fuse, shorter legs. Has been banned from two saloons.",
            new[] { "Ah'll fight anybody. ANYBODY!", "Hold mah hat.", "Who said that? WHO SAID THAT?", "Ah ain't mad. This is mah normal face." },
            new[] { "Next Junimo Ah see is gettin' a knuckle sandwich.", "Ah'll stuff one in a barrel and roll it to the sea." }),
        new("Preacher", "Preaches the Gospel of Honest Work every chance he gets.",
            new[] { "Hard work is holy, boss. So is payday.", "The Hollow provides.", "Repent, crows! Repent!", "Sweat is just the soul leakin' out right." },
            new[] { "The Junimos are a test, brothers and sisters. A sticky, glowin' test.", "Thou shalt not work for fruit. It's in the Brawnie Book." }),
        new("Gossip", "Knows everybody's business in Pelican Town, and half of it's true.",
            new[] { "Guess who Ah saw at the saloon last night.", "Don't tell nobody Ah told ya...", "Shane owes Gus money. Big money.", "Ooh, boss, the stories Ah could tell." },
            new[] { "Word is the Junimos got a queen. And she's MEAN.", "Heard the Wizard keeps a jar of 'em. For company." }),
        new("Conspiracy nut", "Wears a tin hat under the real hat. Just in case.",
            new[] { "Ever notice the crows always watchin'?", "The Wizard knows more than he lets on.", "Moon's lookin' suspicious tonight.", "Ah ain't paranoid. Ah'm prepared." },
            new[] { "Junimos are spies, boss. Wizard's spies. Mark mah words.", "Why do they glow? WHO are they signalin'?" }),
        new("Union firebrand", "Carries the Guild charter in his boot and quotes it from memory.",
            new[] { "Eight hours work, eight hours rest, eight hours ale!", "Fair pay, fair play!", "Ah'm one bad day from a strike, boss. Kiddin'. Mostly.", "Solidarity, boss. Look it up." },
            new[] { "Scabs! Every last bouncin' one of 'em!", "No contract, no dignity. Junimos got neither." }),
        new("Gambler", "Bets on everything: crows, weather, which cow moos first.",
            new[] { "Wanna bet on which crow lands first?", "Ah got a system, boss. It mostly works.", "Lucky hat's on. Today's the day.", "Double or nothin' on mah wages?" },
            new[] { "Bet ya fifty gold Ah can outwork three Junimos.", "Junimos don't gamble. Can't trust a critter with no vices." }),
        new("Crooner", "Can't work without a tune. Can't carry one either.",
            new[] { "Hmm hmm hmm...", "This field needs a theme song.", "Ah sing better than Ah dig. Don't tell nobody.", "La la la, harvest time..." },
            new[] { "Junimos hum outta tune. Every single time.", "Ah'm writin' a sad song about the Undercuttin'. Bring tissues." }),
        new("Big softie", "Tough beard, soft heart. Has adopted at least three baby birds.",
            new[] { "Found a baby bird today. Named him Gerald.", "Everybody needs a hug sometimes. Even crows.", "This farm's real pretty, boss.", "Ah cried at sunrise. Again." },
            new[] { "Ah don't hate Junimos. Ah just strongly, deeply dislike 'em.", "Even a softie's got limits. Junimos are mine." }),
        new("Old sage", "Older than the oldest oak, and twice as stubborn.",
            new[] { "Patience is a Brawnie's best tool.", "The soil remembers, boss.", "Ah've seen a hundred harvests. This one's alright.", "Young folk rush. Old folk last." },
            new[] { "Ah remember the Undercuttin'. A grudge keeps ya warm in winter.", "Forgive, they say. Ah say: invoice." }),
        new("Rookie", "Fresh out of the Hollow, eager as a pup, clumsy as a calf.",
            new[] { "First real job, boss! Ah won't let ya down!", "Is this how ya hold a hoe? Ah'm askin' for real.", "Mama said Ah'd never amount to nothin'. Look at me now!", "Ah'm learnin' so much!" },
            new[] { "Grandpa says Junimos are bad. So Ah booed one. Felt great.", "Are we allowed to throw rocks at 'em? Askin' the union." }),
    };

    /// <summary>The persona for a hand: their own if they're a crew lead, else a Brawnie archetype fixed by their name.</summary>
    public static Persona Of(string name) =>
        Leads.TryGetValue(name, out var lead) ? lead : Archetypes[name.Sum(c => c) % Archetypes.Length]; // char sum: stable across sessions

    public static string Chat(string name) => Pick(Of(name).Lines);

    /// <summary>Their own Junimo grudge, sometimes the Guild's general view.</summary>
    public static string OnJunimos(string name) => Game1.random.NextDouble() < 0.7 ? Pick(Of(name).Junimos) : Chatter.JunimoLine();

    public static string WhoAmI(string name, Role role) =>
        $"{name} the {(Workers.State?.Guild.Members.FirstOrDefault(m => m.Name == name) is { } hero ? Guild.Title(hero)
            : Workers.State?.Fleet.Members.FirstOrDefault(m => m.Name == name) is { } pirate ? Guild.Title(pirate, fleet: true)
            : Market.RoleInfo(role).Title).ToLower()}. {Of(name).Kind}. {Of(name).Bio}";

    /// <summary>
    /// Talk to a Brawnie: their work options (if any) plus "Let's talk a spell", which opens the personal chat. With no work
    /// to offer, the chat options show right away.
    /// </summary>
    public static void Ask(string name, Role role, string prompt, IEnumerable<Response>? work = null, Action<string>? onWork = null)
    {
        var options = work?.ToList() ?? new List<Response>();
        if (options.Count == 0)
        {
            Social(name, role, prompt);
            return;
        }
        options.Add(new Response("vx_talk", "Let's talk a spell"));
        options.Add(new Response("bye", "Never mind"));
        Game1.currentLocation.createQuestionDialogue(prompt, options.ToArray(), (_, answer) =>
        {
            if (answer == "vx_talk")
                DelayedAction.functionAfterDelay(() => Social(name, role, $"{name}: \"Sure, boss. Whatcha wanna know?\""), 50);
            else if (answer != "bye")
                onWork?.Invoke(answer);
        }, null);
    }

    private static void Social(string name, Role role, string prompt)
    {
        var options = new[]
        {
            new Response("chat", "Shoot the breeze"),
            new Response("junimos", "What do you think of the Junimos?"),
            new Response("who", "Who are you, anyway?"),
            new Response("bye", "See ya"),
        };
        Game1.currentLocation.createQuestionDialogue(prompt, options, (_, answer) =>
        {
            string? reply = answer switch
            {
                "chat" => $"{name}: \"{Chat(name)}\"",
                "junimos" => $"{name}: \"{OnJunimos(name)}\"",
                "who" => WhoAmI(name, role),
                _ => null,
            };
            if (reply != null)
                DelayedAction.functionAfterDelay(() => Game1.drawObjectDialogue(reply), 50); // after the question has closed
        }, null);
    }

    private static string Pick(string[] lines) => lines[Game1.random.Next(lines.Length)];
}
