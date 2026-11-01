# The public aquarium (plan, 2026-11-01)

Kristian's change: the second room (the East wing) stops being shop floor full of wall aquariums for
selling. It becomes a **public aquarium**: its own entrance and ticket booth, people pay to get in,
look at two big aquariums (temperate on the north wall, tropical on the east wall) and leave. Good
aquariums bring good ratings and more visitors; a poor one brings bad ratings and fewer. This plan
replaces the 2026-10-30 layout (four wall aquarium spots in the East wing).

Everything below is a first pass for Kristian to change. Numbers live in the balance file under
`aquarium` (AquariumBalance in Balance.cs), so they can be tuned in the console without code.

## The room

- Same footprint as before: 14 m wide, 20 m deep (reaching north past the main floor's back wall),
  bought the same way (price and stars from `store.sections[1]`). Its name in game is
  "Public aquarium".
- **Walled off from the shop.** The side the two rooms share is a full wall now: shoppers and
  aquarium visitors never mix.
- **Its own entrance** in the middle of its front (south) wall, with a door frame, a mat and a sign
  over it.
- **The ticket booth** stands on the pavement beside that entrance: a kiosk with a window, a striped
  awning, a price board and a ticket seller. Visitors queue at its window. Clicking the booth (or
  pressing U, or the Aquarium button in the top bar) opens the aquarium window.
- **Two exhibit spots**, both wall aquariums set into the wall like before (same sizes and height
  upgrades): the **Temperate aquarium** on the north (back) wall and the **Tropical aquarium** in
  the middle of the east wall.
- **Northwest: reserved for later.** The west wall north of the main floor (where the fourth wall
  aquarium used to be) gets a boarded-up archway with a "Coming soon" sign, leading towards the
  empty lot behind the main floor. Nothing is built there yet on purpose (turtles, maybe).
- Old saves: a wall aquarium on the old back wall becomes the temperate exhibit, the first one on
  the east wall becomes the tropical exhibit (the west wall one fills a free spot if there is one).
  Any left over is refunded and its fish go back to the fish seller.

## Exhibits

- A new tank type, **Exhibit** (TankRole.Exhibit). Only exhibits go in the aquarium's two spots, and
  exhibits go nowhere else. Fish in them are never for sale and shop customers never see them, so
  they don't add to the shop's foot traffic.
- **Climate**: the temperate exhibit only takes cold-water fish (FreshwaterCold, SaltwaterCold, and
  bottom feeders or shellfish whose comfortable temperature is below 22 °C); the tropical exhibit
  only takes warm-water fish. The Move to buttons say "temperate only" or "tropical only".
- They are fed, cleaned, treated and upgraded like any tank, and the after-closing chore list
  includes them.
- Price: `aquarium.exhibitPrice` ($12,000, like the old wall aquarium), Shopkeeping level
  `aquarium.exhibitLevel` (8).

## Appeal (how good the aquarium is)

Each exhibit gets an appeal score from 0 to 100:

| Part | Points |
| --- | --- |
| How full it is (living juveniles and adults against half its capacity) | up to 30 |
| Variety (different kinds, six or more for full marks) | up to 20 |
| How eye-catching the fish are (average attractiveness) | up to 25 |
| Aquarium quality (decorations, plants, background, equipment) | up to 20 |
| Clean, clear water | 5 |
| Dead fish | -8 each, up to -40 |
| Dirty water or algae | -25 |
| Sick fish (by share of the fish) | up to -30 |
| Fish fighting | -15 |

The aquarium's appeal is the better exhibit at 65% plus the other at 35%, so one great exhibit
already counts, and two count more. With no living fish in either exhibit the aquarium stays shut.

## Visitors and the rating

- The aquarium opens and closes with the shop. Visitors per hour =
  `visitorsPerHour` x appeal factor (from `appealTrafficAtZero` at 0 appeal to
  `appealTrafficAtFull` at 100) x **rating multiplier** (from `trafficAtNoStars` at 0 stars to
  `trafficAtFiveStars` at 5).
- The **rating** (0 to 5 stars, starting at `startingRating`) is the aquarium's own, separate from
  the shop's reputation. Each visitor leaves with a visit score; at closing the day's average moves
  the rating towards it (`ratingMemory` says how much of the old rating stays), less on quiet days.
- A visit score is 60% what they saw (appeal, and what they actually saw at each exhibit: dead,
  sick or fighting fish and dirty water spoil it on the spot) and 40% value for money (what they
  were willing to pay against the ticket price). People turned away by the price count as a
  disappointed visit at half weight.

## Ticket pricing

- The player sets the ticket price in the aquarium window ($1 steps, $1 to $200) while the shop is
  closed.
- The **fair price** grows with appeal (from `minFairPrice` to `maxFairPrice`) and a little with
  the rating. Each visitor is willing to pay the fair price times a random number between
  `minWillingness` and `maxWillingness`. Above that, they turn away at the booth (and grumble).
- So the better the aquarium, the more it can charge; overcharging empties the queue and costs
  rating, a bargain fills the place and earns rating.

## The estimator and booth upgrades

- The aquarium window shows an **estimate**: the fair price, and how many visitors and how much
  ticket money to expect today at the price set. The estimate is off by up to
  `estimatorError[booth level]` (25%, 15%, 7%, 0%); how far off, and which way, is rolled each
  morning and stays the same all day, so it can't be averaged out by looking twice.
- The booth can be upgraded **three times** (Shopkeeping levels and prices in
  `boothUpgrades`): a price board, a visitor counter, then computerised ticketing. After the third
  the estimate is exact. Each upgrade also lets a couple more visitors in at once.

## Supporting features

- **Day report**: an aquarium section after closing (visitors, turned away, tickets sold, ticket
  money, average visit, rating change and the day's aquarium reviews).
- **Reviews** in their own voice (praise, bargains, too expensive, dead fish, murky water, empty or
  samey tanks, sick fish), kept in the aquarium window.
- **Money**: ticket money is its own income line ("Tickets") on the finances screen.
- **XP**: Shopkeeping XP for each ticket sold and each happy visitor.
- **Visitors** are the same people as shoppers (crew models, gestures, thought bubbles): they queue
  at the booth, pay, walk round both exhibits looking at the fish, react, and leave. The developer
  "Skip day" simulates them too.
- **Saving**: rating, ticket price, booth level, the day's estimate roll, today's counts and reviews.
- **Shop side**: the shop floor and its foot traffic are unchanged; aquarium tanks don't count as
  shop tanks.

## Who lives here, the dives and the market (2026-11-01)

Kristian's second round of rules. Numbers are in the balance file under `aquarium` (and
`aquarium.scuba` for the dives).

- **No little fish.** Only two kinds of fish go in the exhibits: those that keep growing in big
  tanks (oscars, goldfish, the medium cichlids...), which are *hybrids* that can live in ordinary
  tanks or the exhibits, and those that only live here (`Species.eastWingOnly`: the Crimson Jewel
  and Silver Flamefin, which no longer come in egg boxes). A hybrid that reaches its full-grown size
  can't go back into an ordinary tank. Little fish already in an old save's exhibit stay until moved.
- **Sizes.** Each candidate is small, medium or large by the longest it grows (`mediumFishFrom`
  0.28 m, `largeFishFrom` 0.8 m): small are fantails, acaras, Siamese algae eaters, red-tailed
  sharks, silver dollars, pumpkinseeds and Jack Dempseys; medium are comets, oscars, green terrors,
  Texas cichlids, frontosas, Arctic grayling and peacock bass; large are the two wall fish. An
  exhibit holds **20/30/40 small, 5/10/15 medium and 1/3/5 large** fish: as bought, after one
  height upgrade, and after two or more (`smallPerExhibit` and so on).
- **Ordinary tanks** never hold more than **80 small fish** (`store.maxFishPerTank`, soft and hard
  limit alike). The wall tank sizes are 60/65/70/75/80, down from 200-390.
- **Variety matters.** Appeal is now fullness 20, variety 35 (8 kinds for full marks, the first
  kinds counting most), looks 20, quality 15, clean water 5; an exhibit where one kind is over 40%
  of the fish loses up to 20 as "samey", and each kind across the whole aquarium past five adds 2
  appeal (up to 16). Visitors grumble about samey or near-empty exhibits under three kinds.
- **Breeding** in the exhibits happens by chance at 35% (small), 15% (medium) and 5% (large) of an
  ordinary tank's chance.
- **Kinds and limits.** In the aquarium window the player sets the most of each kind wanted. After
  each time step, a kind over its limit sends grown fish, picked at random, to the **fish market**,
  which pays 75% of the appraised value at once (income line "Fish market"). Fish in exhibits and
  holding tanks can also be sold on the market or released from the fish's window.
- **The seller's dives** (Fish seller window, Dives tab; Fishkeeping `scuba.unlockLevel` 10): the
  player pays a fee ($250) and he's gone until the next time step (his van is empty meanwhile), then
  brings back one egg or fry he saved. He won't go while every holding tank is full (and gives the
  fee back if they filled up while he was out). "Keep diving" books the next dive after each one.
  Finds are weighted by real-world rarity (Silver Flamefin common, flowerhorn-type Crimson Jewel
  legendary); four gear upgrades (wetsuit, dive computer, scooter, rebreather) make rare finds
  likelier. The player never sees the odds: the admin console's Scuba dives tab shows each fish's
  chance per dive with each gear upgrade, and what each upgrade does to each rarity.
- **Finds are a mystery** until they grow into juveniles: a wild find is generation 1, so it's a
  mystery fry (or an egg) and the seller's message doesn't say what it is. It joins the journal when
  it grows enough to tell. It can still be sold on the market or released while it's a mystery.
- **Past finds** in the Dives tab: one picture for every kind he has ever found, once the player can
  tell what it is (saved as `scuba.found`). The journal keeps the dive fish in their own **Dive
  finds** section (`Species.eastWingOnly`), so the "?" cards show there are more out there.
- **Holding tanks** (TankRole.Holding): three spots in a row along the back wall of the big breeding
  room (the west wing), $900 each. They only take the seller's finds: 1/2/3 of them at
  standard/large/grand size. Nothing breeds there. From there a find goes to an exhibit, the market
  or back to the wild.
- **No wall breeding tank.** The big breeding room only has the holding tanks now. Its old spot
  (spot 0) stays in the list so saved spot numbers line up; a save that had one is refunded
  (`store.wallBreederPrice`) and its fish and eggs move into a grand breeder in a free breeding room
  spot (or back to the seller if there is none).
- **Fights.** Some kinds won't share a tank with too many of their own (`Species.maxOfKind`: red-tailed
  black shark 1, peacock bass, oscar, green terror, Texas cichlid and Crimson Jewel 2, Jack Dempsey 3).
  Past that the losers take `tankMates.rivalInjury` (0.13) a time step until one dies: about eight
  days for a healthy fish. Bites and nips on medium and large fish do 35% of the usual wounds.

## Left for later

- The northwest expansion (turtles?), deliberately unbuilt.
- Exhibits sized to their walls (they are the standard wall aquarium for now).
- Aquarium daily goals, milestones and perks; merchandise or a gift shop; staff for the booth.
