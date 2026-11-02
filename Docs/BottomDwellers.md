# Bottom dwellers (Corydoras)

How the corydoras catfish in `FishSwimmer` is animated, and the real behaviour each part comes from.

| Real behaviour | In the game |
| --- | --- |
| Corydoras live on the bottom, foraging in sand and gravel. | `SwimStyle.Bottom`, depth band 0–10%, rides about 14% of a body length above the sand. |
| They move over the bottom in short starts and stops rather than steady cruising. | `Hop`: 0.3 s bursts of tail beats at 1.8 body lengths/s, then a 0.6 s glide, repeated until they reach the spot 1–3 body lengths away. Higher acceleration than midwater fish so starts and stops are crisp. |
| They search with their barbels and suck food up, often burying the snout up to the eyes. | `Sift`: 1.5–4 s nose-down (about 25°, bobbing ±4° at ~2.7 Hz) with a fluttering tail. |
| They often lie still on the bottom, propped on their fins. | `Rest`: 3–8 s motionless, tail stroke almost zero. |
| They breathe air through the gut: a quick dash to the surface, a gulp, then drop back down. It happens 1–45 times an hour, more often around meals. | `AirDash` every 40–100 s (within 6–15 s after eating), near-vertical at 2.4 body lengths/s, then `Descend`, gliding back down nose first. |
| They eat food that has sunk to the bottom. | They only target pellets within two body lengths of the sand. |
| An armoured body (bony plates) bends less than a goldfish's. | Spine path-follow 0.5 (goldfish 0.85) and a small tail twist. |

Sources: [Corydoras (Wikipedia)](https://en.wikipedia.org/wiki/Corydoras),
[Why do Corydoras take gulps from the surface? (Practical Fishkeeping)](https://www.practicalfishkeeping.co.uk/features/why-do-corydoras-take-gulps-from-the-surface),
[Pineda et al. 2020, Social dynamics obscure the effect of temperature on air breathing in Corydoras catfish](https://pmc.ncbi.nlm.nih.gov/articles/PMC7673363/).
