# Coffee Sim — Design Document

**Version:** 0.6 (concept)
**Status:** Pre-prototype. Systems spine established, setting locked, scope boundaries drawn, prototype order locked (§14), technical direction locked (§15).

**Changes since 0.1:** Setting locked to Vietnam's Central Highlands (§3). Two-crop Robusta/Arabica system added (§4). Intercropping cut (see §7.2). Café menu expanded around Vietnamese drink culture (§5).

**Changes since 0.2:** Geography locked to a single sloped property near Da Lat with an Arabica/Robusta elevation band (§3). Café located on the farm, not in town (§3). Tourist/local customer split added (§3).

**Changes since 0.3:** Prototype order locked — **roaster first** (§14). Previous owner given a working name, the Old Man (§11). Both removed from open questions (§13).

**Changes since 0.4:** Technical direction added and locked (§15): Godot 4, C# simulation core with an engine-free sim boundary, 2D pixel art in an oblique projection, terracing as the elevation device, PC only.

**Changes since 0.5:** Sprite layer system specified (sprite-layers.md), closing the §15 item that blocked final art. Fruit palettes exempted from global tinting (§15).

---

## 1. Pitch

A farming sim about coffee, from tree to cup, set in Vietnam's Central Highlands. You inherit a mature, healthy, thoroughly mediocre Robusta farm from its retiring owner and spend years converting it into something worth drinking — grafting, replanting, and learning to ferment — while running the café that turns your harvest into money and feedback.

The hook: **you drink your own mistakes for twelve months.** Last year's fermentation call is sitting in the cup you're serving today.

### Market gap

The coffee game space is saturated at the shop end and near-empty at the farm end.

| Game | What it covers | Gap |
|---|---|---|
| Coffee Shop Tycoon, The Roast, Coffie Simulator, Cup and Counter, Beans | Café management, some roasting | Beans arrive in a bag |
| Coffee Inc 2 (Side Labs) | Global plantations + blending + retail chain | Corporate spreadsheet tycoon, not a farming sim |
| Brewtopia (mobile) | Growing coffee varieties, harvest → roast | Tap-to-idle, no depth |
| Farming Simulator coffee mods | greenhouse → roaster → shop | Shallow, unbalanced; proves the chain is appealing |

Nobody has modeled the **agronomy**: altitude and shade affecting cup quality, ripeness at harvest, washed/natural/honey processing, fermentation windows, drying, defect sorting, roast curves.

### Reference point

**Sakuna: Of Rice and Ruin.** One crop, one annual cycle, ~12 hands-on stages, output as a stat vector. Sustains dozens of hours on a single crop because the cycle repeats and player knowledge compounds. That's the target shape.

**Key structural difference:** rice is an annual, coffee is a perennial. Sakuna gets a clean reset each year. Coffee trees live 20–30 years and accumulate condition — stress, rust damage, pruning history. Richer and meaner: mistakes compound instead of clearing.

---

## 2. Core design thesis

> **Agronomy sets the ceiling. Craft sets the recovery. Nothing downstream ever adds quality.**

Every stage of the pipeline either preserves or destroys. This is the rule that keeps the whole system legible.

### The quality vector

Quality is not one number. Four axes plus a separate counter:

- **Acidity**
- **Body**
- **Sweetness**
- **Aroma**
- **Defects** (separate — additive, permanent)

**Farm stage** (altitude, varietal, shade, soil, tree age) sets a *potential* per axis.
**Every downstream stage** applies a recovery fraction — you realize some percentage of the ceiling, never more.

Great processing can't save mediocre cherries. Sloppy processing wastes exceptional ones.

### Two investment classes

| Class | Example | Horizon | Effect |
|---|---|---|---|
| Raise the cap | Plant a high-altitude Geisha plot | ~4 years | Permanently raises ceiling |
| Stop the leak | Buy raised drying beds | Immediate | Raises recovery rate |

The player is always balancing these against each other. That tension is the strategic core.

### Defects

Additive, permanent, never decay. Sources: overripe cherries, over-fermentation, mold during drying, scorched roast.

The only cure is **sorting**, which costs yield. Every stage gate is therefore the same decision in a new costume: *how much mass do I throw away to protect the score?* The right answer changes depending on the lot's destination.

**Not all defects are visible.** Physical defects sort out at the mill. Over-fermentation defects (phenolic, vinegary) look perfectly normal and only appear in the cup. Quakers — underripe beans — look normal in green and only show up pale and papery *after roasting*.

---

## 3. Setting: Vietnam, Central Highlands

**Locked.** A single sloped farm near Da Lat, Lâm Đồng province, crossing the elevation band between Robusta country and Arabica country. Café on-site.

### Why this setting rather than the Latin American default

**The game's central conflict is literally happening there right now.** Vietnam is the world's #2 producer, with Robusta at ~95% of output, built explicitly on quantity over quality. The industry's modern foundation was laid under collective farming supplying the Soviet Bloc; the turning point was the Đổi Mới reforms in 1986.

A farmer who planted in the late-80s boom, sold green to an exporter at commodity price, and never once tasted his own crop is **not a character we have to construct.** He's the median Central Highlands smallholder. §11 (the previous owner) stops being a writing problem.

### Fine Robusta is the specialty turn, and it's real

A genuine CQI-graded category at 80+ points, scored by a trained **R-Grader** rather than a Q-Grader. Highlands farmers are already adopting natural, honey, and anaerobic fermentation on Robusta — methods normally reserved for high-end Arabica.

**This means our fermentation ladder (§9.3) is the actual mechanism of the actual movement.** The player isn't performing a designer's fiction; they're doing the thing the region is currently doing.

### Geography: one sloped property

**Locked: a single contiguous farm on a steep hillside near Da Lat**, running roughly 1,100m at the base to 1,500m at the ridge. Cầu Đất, above Da Lat, is the real analogue — genuine high-altitude Arabica country with Typica and Bourbon dating to the French period.

| Elevation | Crop | Character |
|---|---|---|
| Upper (~1,300–1,500m) | Arabica | Cooler, mistier, shade trees |
| Lower (~1,100–1,300m) | Robusta | Warmer, more open |

**On the realism:** Arabica wants ~1,200m+; Robusta does fine below that. A steep property crossing that threshold in a few hundred vertical meters is barely a stretch — Da Lat terrain is genuinely that steep. The only exceptional part is that one family owns the whole gradient. **Acceptable looseness; a deliberate call.**

**Rejected alternative:** fragmented non-contiguous parcels (the realistic Vietnamese smallholding pattern, with the Robusta land a drive away near Bảo Lộc / Di Linh). More accurate, but it bought realism at the cost of travel friction and a split map in co-op — working directly against the on-site café. Cut.

**Why the gradient earns its keep:**
- **Altitude becomes readable off the landscape**, not a stat in a menu. You walk uphill and the crop changes.
- **"Plant Arabica" becomes spatially meaningful.** There is only so much land above the line — a natural cap on how far the player can chase the aroma ceiling, with no rule written anywhere.

### The café is on the farm

**Not in town.** Coffee farm cafés and farmstays around Da Lat are real and thriving — people drive up from Saigon to drink coffee where it grew.

**Why on-site:**
- Keeps the entire game **one contiguous space**: home block, wet mill, drying beds, roaster, café.
- **No split-map problem in co-op.** One player at the mill, another at the bar, one location.

**Da Lat as the aesthetic anchor:** cool highland climate, pine and mist rather than tropical heat, French colonial architecture. A cozy game set somewhere cozy games are not usually set.

### Customer split (falls out of the location for free)

| Segment | Behavior | Program |
|---|---|---|
| **Tourists** | Volume, forgiving, want the iconic experience — cà phê sữa đá on a hillside | Robusta phin |
| **Locals & specialty pilgrims** | Repeat visitors, discerning, notice when quality drops | Arabica pour-over |

This is where the "regulars notice" mechanic and the two-menu justification (§4) both come from — one setting decision produces both.

**It also gives the commodity-buying escape valve (§5) real teeth:** tourists won't clock bought-in green. Regulars absolutely will.

### Climate

Monsoon/dry rather than four seasons. Harvest falls in the dry season (roughly Oct–Jan), which is favorable for drying — a real reason naturals are viable here.

**Flowering is triggered by irrigation after dry stress.** Real practice, and genuinely contentious (groundwater depletion). See open questions.

### Things to go in with open eyes

1. **Environmental history.** Highlands expansion involved real deforestation and aquifer depletion. Engage or sidestep, but decide deliberately — don't stumble into it.
2. **Indigenous presence.** The highlands around Da Lat are the homeland of the K'Ho and other groups, and there are established K'Ho-run coffee cooperatives in the area. A farm story set in this landscape that doesn't acknowledge them is a real gap. Not a reason to move the setting — a reason this belongs in the collaborator conversation early.
3. **Representation.** This needs Vietnamese collaborators from the concept stage, not as a late-stage authenticity pass.

---

## 4. Two crops: Robusta and Arabica

**Design rule: these are not a tier ladder.** Arabica must never be the upgrade you graft toward — that collapses the decision into a linear progression and kills the whole strategic layer.

They are **different vectors into the same four axes** (§2).

| | Robusta | Arabica |
|---|---|---|
| Strengths | Heavy body, structured bitterness | Aroma, acidity |
| Weakness | Low acidity, limited aromatic range | Fragile, low yield |
| Hardiness | Rust-resistant, hardy | Rust-vulnerable, needs shade |
| Yield | High | Low |
| Elevation | Lower property | Upper property only |
| Path to ceiling | Fine Robusta frame: processing craft, selective picking, raised beds | Altitude + shade + varietal, then don't ruin it |

### The café forces coexistence

This is the load-bearing part. **You run two crops because you run two menus**, not because one crop is better.

- A **phin program needs Robusta body.** Condensed milk flattens a delicate Arabica into nothing. Go all-Arabica and the cà phê sữa đá is thin and wrong — and you've lost your volume business.
- The **pour-over program needs aroma** Robusta can't reach.

Neither crop can be abandoned without losing half the café.

### The Fine Robusta lot is the interesting third thing

Your best Robusta *could* go into the specialty program at high margin, or into the phin program where it's wasted but reliable.

**That's the allocation decision** — the same "which market does this lot serve" question from the original spine, made concrete by a menu instead of a buyer.

---

## 5. The café (and why it exists)

**Decision: yes, include the café. All coffee goes to it.**

Not for realism — for legibility. Selling green to a buyer collapses all your fermentation craft into a number that becomes money. Putting it in your own cups makes quality mean *which drinks you can make, who walks in, and what they'll pay*.

The scale math is nearly honest: a hectare yields ~1,000–2,000 lbs green/year; a modest café burns through roughly that. "One block feeds one café" is barely a stretch.

**No complex buying/selling agreements.** Deliberately excluded — they aren't fun and they'd dilute the loop.

### Quality shapes a menu, it doesn't gate a tier

| Vector state | Menu consequence |
|---|---|
| High aroma + acidity | Specialty program: pour-over, single origin. Low volume, high margin, attracts the customers who care. |
| Heavy body | Phin and milk drinks. Volume. |
| High defects | Dark roast, iced, sweetened. Sugar covers sins. |

**Your bad lots have a home.** A botched fermentation is a setback, not a dead loss.

**And in this setting that's culturally native, not a designer's escape hatch.** The phin drips slowly into a thick concentrate, usually cut with condensed milk to balance the bitterness — dark-roasted Robusta is *the point*, not the compromise. The light-roast specialty program is the genuinely contested thing the player is introducing.

### The menu, three programs

**Phin side — Robusta, dark roast, volume, forgiving:**
- cà phê sữa đá (iced, condensed milk)
- cà phê đen đá (iced black)
- bạc xỉu (milk-forward)
- cà phê trứng (egg coffee)
- cà phê muối (salt coffee)
- coconut coffee
- yogurt coffee

**Specialty side — Arabica or Fine Robusta, light roast, low volume, unforgiving:**
- pour-over
- single origin by lot
- cascara
- espresso program

**Non-coffee — staff-run, keeps the café breathing during harvest:**
- iced tea, lotus tea
- sinh tố (fruit smoothies)

The drink variety is a major content lever: it gives the café genuine day-to-day texture, and each drink has different tolerance for defects and different demands on the quality vector.

### Escape valve

You can buy commodity green from outside. Expensive, generic, regulars notice the drop in quality. One button, no contracts. Stops a bad harvest from becoming an unrecoverable year.

---

## 6. Time structure

### Three clocks, deliberately layered

| Clock | Tempo | Where the player lives |
|---|---|---|
| Café | Daily | Day-to-day, ~10 months of the year |
| Wet mill / ferments | Hours | Harvest season |
| Farm / trees | Annual | Strategic layer, arm's length |

These aren't competing tempos, they're stacked ones. There's always something in the mill to fuss over while the trees do nothing.

### The year

**Harvest season (~6–8 weeks):** You're on the farm. Picking passes, selection, fermentation, drying. The café runs on staff and last year's roasted stock — you just see the daily take. How well it does is a report card on last year's preparation.

**The other ~10 months:** Café is the hands-on day-to-day. Farm ticks at arm's length with occasional pruning and shade decisions.

**Weekly roast:** The bridge. Strategically the most important recurring decision in the game (see §9).

### Annual farm cycle

1. **Post-harvest pruning** — the big perennial decision. Skeletal prune, stump entirely, or leave. Sets next year's frame.
2. **Fertilization & shade management** — canopy thinning as a light/moisture tradeoff.
3. **Flowering** — triggered by first rains after dry stress. Blossoms last days, smell like jasmine. Weather-gated, high drama, low player control.
4. **Cherry fill** — the long middle. Pest and rust pressure, irrigation.
5. **Harvest** — multiple selective passes (see §9.1).
6. **Wet mill** — pulping, fermentation, washing, drying.

---

## 7. Solving the perennial time problem

Coffee takes 3–5 years to first production. Farming sim players expect weekly gratification. Four structural fixes:

### 7.1 Inherit a mature farm

Day one you have producing trees. Full loop access immediately. The glacial timer only applies to *improvement*, never to entry.

The inherited trees are old, high-yield/rust-resistant Robusta, badly pruned — call it 78-point cherries. Your first Arabica planting or Fine Robusta conversion is a bet placed from a position of stability.

**This is the main solve, and it does more work than it looks like.** Because the farm produces on day one and the café is the daily loop, a new planting is *never* something the player waits on to have gameplay. It is pure optional investment. Everything below is reinforcement, not load-bearing.

### 7.2 Intercropping — CUT

*Previously proposed as the main solve: plant durian, black pepper, avocado, macadamia between coffee rows for shade + fast income during the juvenile years, tapering as the canopy closes. Real practice in the Central Highlands (~25% of coffee area), with durian returning 2.5–3x coffee per unit area, and a live land-use tension as prices swing.*

**Cut anyway — it was belt-and-suspenders.** §7.1 already guarantees day-one gameplay, so the fast-crop layer was solving a problem that no longer exists, at the cost of a whole second economy competing with the coffee loop for attention.

**What's retained:** shade trees remain an agronomy lever (light, moisture, temperature buffering) with **no second economy attached.** Grafting (§7.3) is the real answer to "I want this block converted faster."

*Revisit only if playtesting shows the juvenile years feel dead despite the café carrying the tempo.*

### 7.3 Grafting (mid-game unlock)

Top-working existing rootstock to a new varietal is real practice and roughly halves time to maturity. Converts a mediocre inherited plot in ~2 years instead of 4.

Exciting *because* the player has already felt the four-year wait. Do not unlock early.

### 7.4 Decision-dense juvenile years

Formation pruning, shade tree density, soil amendment, nursery selection — each nudges the eventual ceiling. Four years of shaping a cap you can't cash until year five is legitimately interesting, provided you're pressing meaningful buttons throughout.

### Design guardrail

**Do not compress coffee maturity too far.** Its *relative* length gives ceiling investment its weight. If a Geisha plot pays off in twenty minutes it's just another upgrade node. Keep it the longest timer in the game by a wide margin — just never the only thing happening.

### Biennial bearing

Real phenomenon: a tree that fruits heavily exhausts itself and produces less the next year. Turns season optimization into multi-year oscillation management — deciding when to let a block rest. Sakuna structurally cannot do this; it's the payoff for choosing a perennial.

---

## 8. Scope boundary: what's hands-on

**Hands-on granularity does not survive contact with a 40-plot plantation.**

**Permanently hands-on:**
- One "home block"
- The wet mill
- The roaster
- The café

**Arm's length:** All additional plots. Managed via settings, hired crew, and reports.

Growth adds volume and variety. It never dilutes the thing you actually touch.

---

## 9. The four minigames

Each occupies a distinct register. No two share a verb.

### 9.1 Picking — flowing time pressure

**The decision is per-branch, not per-cherry.** Cherries on one branch ripen unevenly. The judgment: is this branch ripe *enough* to work, or do I come back in a week?

- Strip a 40%-ripe branch → a load of green drags the whole lot down
- Skip it → ripe cherries raisin on the tree before the next pass

Rhythm of moving through a tree making fast good-enough calls. Flow state, mild time pressure, cozy in texture with the window closing.

**Two details that give it teeth:**
- Ripeness is a window, not a binary. Overripe cherries ferment on the tree and look much closer to ripe than green does — **the dangerous confusion is on the far side.**
- Yellow-fruited varietals ripen to yellow, not red, quietly breaking the color-reading skill the player just built. (Good reason for the old man to have planted one block of Yellow Bourbon.)

**Scale:** You don't hand-pick a hectare. Hired crew does volume — you set selective vs. strip and the pay rate, and get quality accordingly. You personally pick the microlot. Hand-picking is *a choice to invest attention in one lot*, not a chore.

### 9.2 Sorting — calm scrutiny

Opposite register from picking. No clock, indoors. Costs yield instead of time.

- **Float tank** catches low-density defects for free
- **Hand-sorting table** for blacks, sours, insect damage

### 9.3 Fermentation — scheduling under pressure

**Not a twitch game.** A roast is twelve focused minutes; a ferment is 18–72 unattended hours. During harvest you have multiple lots overlapping in a finite number of tanks while fresh cherry keeps arriving from the picking passes. Cherry can't wait — it starts fermenting in the bag within hours.

The pressure is **triage**: tanks are full, today's pass just came in, what gets cut short and what gets rerouted?

**Method as resource routing, not just flavor:**

| Method | Tank time | Drying space | Risk | Payoff |
|---|---|---|---|---|
| Washed | Yes | Low | Low | Clean, narrow range |
| Honey | Some | Medium | Medium | Sweetness, body |
| Natural | None | Weeks | High (mold) | Big fruit, high ceiling |
| Co-ferment | Yes | Varies | Very high | Spectacular or cough syrup |

When the mill is jammed mid-harvest, "we're naturalling this lot" is sometimes a capacity decision dressed up as an artistic one. Very true to life; great source of forced-error stories.

**The axis players learn first: mucilage.** It's sugar and it's fermentation substrate. Washed strips it immediately, honey leaves some, natural leaves the whole fruit on through drying. More mucilage = more sweetness and body = proportionally more risk of going boozy or moldy. **Reward and danger scale together on one continuum.**

**Instrumentation (progression):**
- Early: traditional rub test (gritty parchment = mucilage broken down; slimy = keep waiting) + smell, reported as vague prose (*sweet and winey* vs *sharp, solventy*)
- Later: pH meter. Starts ~5.5, falls through the ferment, done around 4.0–4.2, past that you're pickling. Converts intuition into a graph.

**Exothermic:** the mass heats itself, heat accelerates the ferment, which makes more heat. Deep tanks run away faster than shallow ones. Same momentum problem as the dial roaster, stretched across days.

**Presets are safe but capped.** Follow the recommended washed protocol exactly → solid, clean, unexceptional. Never a disaster, never a standout. Generous floor, all upside in deviation. The player who pulls a natural two hours early because the tank's running hot is the one who gets the 88.

**Visibility rule: expose state, hide outcome.** Always show pH, tank temp, elapsed time, smell, rub. Never show cup score — genuinely unknowable until dried, rested, roasted. The tension is "I could see exactly what was happening and still had to guess what it would taste like," not "I couldn't tell what was happening."

### 9.4 Roasting — momentum control

**Fidelity target: the dial roaster, not the modern profile machine.** Presets plus a temperature dial.

**Lag is the entire skill ceiling.** You turn the dial and nothing happens for ~20 seconds, then bean temp responds to what you did back then. You're not controlling temperature, you're steering something with momentum. Same input model as a good driving game — which is why one knob stays interesting for a hundred hours.

**Instrument panel is curve-first:** bean temp rising, and above it **rate-of-rise**, where the skill actually reads.

| Curve shape | Result |
|---|---|
| RoR gliding steadily downward | Healthy roast |
| RoR flatlines through drying phase | Baked — flat, papery, unrecoverable |
| Crash-and-flick after first crack | Harsh edge |

Legible failures the player can *see* coming in the curve before tasting. Punishable but telegraphed.

**First crack is audio, not UI.** Scattered pops building. The player learns to listen to the roaster instead of reading a HUD.

**Development time ratio** is the one number to surface: time from first crack to drop as a percentage of total roast. Too early → sour, grassy. Too long → flat, generic roastiness, origin character gone.

**Progression:** start with presets and a dial. Later unlock profile saving and automation — dial a lot in by hand, save the curve, apply to future batches. Mastery converted into infrastructure.

**But saved profiles don't transfer cleanly between lots.** Denser high-grown beans and wetter fresh-crop beans take heat differently. A profile that was perfect last season needs re-dialing this season. **Automation handles daily volume; anything new or precious pulls you back to the dial.** The chore gets automated, the craft doesn't.

### The weekly roast as strategic heart

Green inventory is finite until next harvest. Every roast is a commitment.

- **Light** — showcases the acidity and aroma you fought for. Pour-over, single origin. High margin, low volume, brings in the customers who care. Exposes every defect.
- **Dark** — forgiving, hides defects, drives espresso and milk drinks at volume. Trades away origin character for body.

Consistent with the spine: **roasting only ever reveals or destroys.**

---

## 10. The logbook

Cross-cutting mechanic. Solves the fermentation feedback-delay problem, carries the tutorial, and doubles as the multiplayer portable asset.

Every batch auto-records: method, tank temp, duration, pH curve, mucilage level, roast profile. When the cup score finally lands — after drying, resting, milling, roasting, cupping — **it attaches to that entry.**

Across a season the log becomes the thing the player actually reads to figure out what their farm wants.

**It starts as the previous owner's.** The recommended protocols aren't a tutorial overlay, they're forty years of someone's handwriting — *"washed, 36 hrs, don't let it go past Thursday."* You follow the book. Then you annotate it. Eventually you cross entries out and write your own. By late game the log is yours with a few of their pages left in it.

Progression you can literally see.

---

## 11. Narrative: the Old Man

**Working name: the Old Man.** Already how this document refers to him in its own
voice (§9.1, and throughout this section), so the name and the register agree.

Kinship-neutral on purpose. It leaves his relation to the player unstated, which
retires the question a kinship term would have opened — where the middle
generation went. The one thing it leaves loose: if he isn't family, "inherit"
(§1) wants a mechanism eventually. Succession, sale, or a handover to someone he
picked.

**Living and retiring. Not dead.** The dead-relative inheritance is the most worn opening in the genre and it costs you the entire value of the character — an ongoing voice that reacts to what you're doing to their trees.

### The farm is their biography

They planted for yield and rust resistance because they sold green at commodity price to an exporter, where cup score bought them nothing. You inherit **healthy, well-tended, thoroughly mediocre trees.** Every limitation you spend the game grafting your way out of was a rational decision by someone surviving a different market.

Explains your low ceiling diegetically. No villain required.

### Conflict without a villain

Commodity-era farming vs. the specialty turn is a genuine generational split. Their risk aversion is *earned* — they lived through price crashes where an experimental lot meant not making it.

**Let them be right sometimes.** A bad year where you overreach and their conservative approach would have carried you is worth more than a mentor who's just wrong until you prove yourself.

### Visit structure

- **First harvest:** they stay and help. This is the tutorial.
- **Thereafter:** once a year, **in the quiet season, not at harvest.**

They drink last year's crop. They saw those trees in flower on their previous visit; now they're tasting what came of it. **Their feedback runs on the same delay as everything else in the game.**

Also leaves you genuinely alone for the hard part. They show up, poke around, drink a cup, leave — and months later you're in the wet mill at 2am with nobody to ask.

### Comments generated from farm state, not scripted

You need a readable state inventory anyway: pruning quality, canopy density, rust presence, which blocks are stumped or grafted, weed pressure, tank cleanliness, drying beds covered or not.

A year is exactly the right interval for a perennial to show change, so they're always commenting on something real. *"You took the whole east block down to stumps"* hits differently when the player remembers making that call in a panic.

### No approval meter

The moment there's a bar, players optimize the old man instead of the farm — and the whole point of him is that he's an opinion you can reject.

**Approval shows up as things he brings instead:** budwood from a friend's Bourbon he's been meaning to give someone, an introduction to a buyer, equipment out of his shed. Gifts as sentiment, no numbers.

### Beats to protect

- **His first taste of the café.** Forty years selling green to an exporter, never once drinking his own farm. Don't over-write it — let it be a small thing he doesn't quite have words for.
- **Late game: the visits stop**, because travel gets hard, and **you start going to him.** Packing a bag of this year's roast and driving out. Same ritual, reversed. Doesn't require killing anybody.

### Co-ferments and the customer split

Co-ferments are contentious in real specialty coffee — yeast inoculation, carbonic maceration, fruit and cascara additions. Purists say they bulldoze terroir.

Perfect late unlock: the old man disapproves, *and* a co-ferment lot splits your customer base — delighting adventurous regulars, alienating the ones who come in for a clean washed Ethiopian. Highest ceiling, highest variance, and the variance isn't just a score — it's who walks out.

---

## 12. Multiplayer

**Decision: day one, or day two after the minigames prove out.** Not for narrative reasons — cozy players want the option.

### The real day-one cost is simulation architecture, not netcode

Transport can be bolted on later without much pain. What can't: a sim built on globals and singletons — one `PlayerController`, one `CurrentRoast`, UI reading directly off game state.

**Build single-player as a local client-server from the start.** Authoritative sim, everything addressable as *station N operated by player X*, presentation strictly downstream. Multiplayer becomes a transport swap rather than a rewrite.

Far more forgiving than MITTS — almost nothing needs prediction.

### Time control is the thing that will bite

Top complaint about every co-op farming sim: host sleeps and everyone's day ends, or nobody can advance because one person is mid-task.

This game is unusually exposed — **three clocks.** Solve deliberately rather than inheriting Stardew's answer.

**Proposed:** ferments run on their own wall-clock and don't care about day advance, so an unready player never blocks the calendar.

### Parallelism audit

One dial roaster = one player roasting and two watching. That's the death of co-op.

- **Already parallel:** multiple tanks, multiple drying beds, multiple blocks to pick
- **Needs adjustment:** at least two roasters, earlier than realism suggests
- **Café:** genuinely separable posts — bar, register, pastry, dish pit

### Natural asymmetric split

**Harvest:** one player runs picking passes, the other runs the wet mill as cherry arrives. Same one-does-logistics / one-does-craft structure as Solarpunk. Makes the wet mill jam a *conversation* instead of a solo triage puzzle.

**Café rush:** Overcooked-lite with no redesign at all.

### Farmhand model — with the logbook portable

Standard in the Stardew lineage. Not universal — Valheim, Core Keeper, and Terraria use portable characters, because their progression lives on the character (skills, gear, inventory) rather than in the world.

**This game leans hard toward world-state.** Trees, grafts, equipment, the wet mill, the café — nearly everything earned is the farm itself. Farmhand is the honest fit.

**Except the logbook.** Annotated protocols, roast profiles, ferment notes. That's knowledge, it lives in the player's head anyway, and it's the one asset that should travel.

Show up at a friend's farm with your own notes and they're *almost* right — different altitude, different varietals, profiles that need re-dialing. Same rule already established for saved roast curves between lots, applied to a different farm.

A guest arrives as a competent roaster with useful instincts rather than a stranger holding a hoe. Also fixes the standard farmhand complaint that guests leave with nothing: **here they leave with their notes, and better ones than they came with.**

**Light social hook, no infrastructure:** exportable protocol cards. Trade a Yellow Bourbon honey profile the way you'd trade a recipe.

---

## 13. Open questions

1. **Salvage ceiling.** How much should dark roasting rescue a bad ferment? Too much and the tank-watching layer stops mattering.
2. **Café depth.** How much Overcooked is in the café rush, vs. how much is it a passive report during harvest?
3. **Number of blocks.** How many arm's-length plots before the strategic layer gets noisy?
4. **Crop balance.** Is there a Robusta:Arabica ratio the café makes *correct*? If so it's a solved puzzle — needs the ratio to shift with menu strategy, customer mix, and harvest luck.
5. **Environmental history.** Deforestation and groundwater depletion are real to this setting. Engage, acknowledge lightly, or sidestep? Irrigation-triggered flowering sits right on this line.
6. **Drink unlock pacing.** Does the player start with the full phin menu, or earn drinks? Egg coffee and salt coffee are regional specialties with their own stories — good unlock candidates.

*Resolved in 0.4: prototype order — roaster first (§14). Working name for the previous owner — the Old Man (§11).*

---

## 14. Prototype order

**Locked: roaster first.**

1. **Roaster** — dial, lag model, RoR curve, first-crack audio. Smallest self-contained system, best feel test, and the clearest signal on whether the "momentum control" thesis works.
2. **Fermentation + tank scheduling** — the hardest system to balance and the one most likely to change the rest of the design.
3. **Picking passes** — validates the per-branch decision as a rhythm.
4. **Vertical slice:** one harvest season, one block, wet mill, weekly roast, minimal café.

---

## 15. Technical direction

**Locked: Godot 4, C# simulation core, 2D pixel art in an oblique projection. PC only.**

### Engine: Godot 4

Chosen on existing fluency more than on merits — which is usually the right basis. PC-only removes the one real strike against it, since console export was the objection and there's no console target. And it converges with the presentation call below: **Godot's 2D half is its stronger half**, so going 2D also avoids the less mature part of the engine. The art direction and the engine choice reinforce each other rather than trading off.

### Language and the sim boundary

**C# for the simulation core.** GDScript is fine for glue and UI — Godot runs both, and the boundary is what matters, not language uniformity.

**The rule §12 actually needs: the simulation compiles without the engine.** No `Godot` type anywhere in the sim namespace. That makes the architectural constraint *checkable in CI* rather than a discipline someone has to remember at 2am. Everything addressable as *station N operated by player X*; presentation strictly downstream, reading sim state and never holding it. Multiplayer then becomes a transport swap, as §12 requires.

PC-only is also what makes C# clean here — export targets were always the weak spot, never the language. **If browser builds for playtesting matter, verify current C# web export support first**, or write the throwaway roaster prototype in GDScript and sidestep the question entirely.

### Presentation: 2D, oblique — not flat top-down

**Why 2D:**

- **§9.1 is a color-discrimination mechanic.** Picking is reading ripeness off cherry color, with the deliberate trap that overripe sits nearer to ripe than green does, and Yellow Bourbon breaking the learned reading. Dynamic 3D lighting makes the same cherry a different color in sun and shade — it fights the mechanic. A controlled pixel palette makes the states exact by construction. **This cuts both ways:** a global day/night or weather tint in 2D reintroduces the same corruption, so fruit palettes are exempt from global tinting (see sprite-layers.md).
- **§8 caps hands-on scope to one home block**, so per-tree visual state is a tractable asset count rather than a plantation's worth.
- **Higher floor for charm.** Mediocre pixel art still reads as appealing; mediocre 3D reads as ugly. On a small team that's a real argument.
- **§12's café rush wants a legible room from above** — native to 2D.

**Why oblique rather than flat top-down:** §3 is locked on the elevation gradient being readable off the landscape. Flat top-down cannot express height at all.

### Elevation legibility: terracing

The device is **contour terracing.** Steps up the hillside read as height immediately in an oblique view, the Arabica/Robusta boundary becomes a literal band across the map, and parallax ridges of pine and mist behind it carry the depth cue and Da Lat's mood (§3) in the same layer. Terraced contour planting is historically correct for steep Highlands ground, so the legibility device costs nothing in realism.

### Camera: one traversal framing, N station framings

Most of this game happens at a station — the roaster is a dial and a curve, fermentation is tanks and a readout, sorting is a table. Those snap to their own framing and are effectively UI. **Only the traversal camera is a real decision; the rest constrain nothing.**

| Context | Framing |
|---|---|
| Farm traversal | Oblique three-quarter, follows the player |
| Café | Same projection, pulled back to a readable room (§12 rush) |
| Roaster / fermentation / sorting | Snap-to station view, effectively UI |
| Picking | Undecided — see below |

### Sprite layer system

**Specified in [docs/sprite-layers.md](sprite-layers.md).** Decided before final art, deliberately.

The state §11 requires to be visible — pruning quality, canopy density, rust, stumped or grafted blocks, weed pressure — plus §9.1's ripeness and varietal color, decomposes into **frame + anchors + palette** rather than one sprite per combination. Drawn naively those axes multiply out past 600 sprites; the decomposition lands near 37.

Two consequences worth surfacing at design altitude:

- **An anchor is a branch is a §9.1 picking decision.** Foliage clumps and fruit clusters both attach at anchor points on the frame sprite, which makes canopy density and per-branch ripeness *data* rather than art. It also means anchor count is a design number living in an art file — the frame defines how many decisions a tree presents.
- **The ripeness palette ramps are where the picking mechanic actually lives.** §9.1's "the dangerous confusion is on the far side" is a perceptual spacing requirement on the ramp — wide gap green-to-ripe, narrow gap ripe-to-overripe. Yellow Bourbon breaking the trained reading falls out of its ramp being inherently tighter. Neither is coded anywhere.

**Fallback if variation gets hairy:** the Dead Cells pipeline — model in 3D, render down to sprite sheets, hand-touch the pixels. Parametric variation with a pixel-art result. Sakuna (§1) does its own version of this, 3D assets presented on a 2D plane.

### Still open

1. **Pixel resolution and character scale.** Drives everything downstream in the art pipeline.
2. **Does picking get its own framing?** §9.1 is a per-branch judgment, which wants to sit closer than traversal — but that makes picking a fifth station rather than something done while walking the block.
3. **Co-op presentation.** Assumed networked clients per §12's farmhand model, each with an independent camera. Splitscreen would change this.
4. **Godot C# web export status** — only matters if browser playtesting is wanted.

---

*Systems spine: agronomy sets the ceiling, craft sets the recovery, nothing downstream ever adds quality.*
