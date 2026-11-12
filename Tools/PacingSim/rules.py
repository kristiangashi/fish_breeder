"""Pass/fail checks for Kristian's pacing rules (the "rules" list in targets.json).

Each rule has a "kind" (below) and is judged on the runs of one bot ("bot", default Cautious) or of
every bot ("bot": "all"). check(rule, bots) returns (passed, measured text).
"""
import statistics


def enrich(run):
    """Per-day numbers the rules use, worked out from the recorded ones."""
    profit = []
    for d in run["days"]:
        d.setdefault("uncommon", 0)
        d.setdefault("rare", 0)
        d["common"] = max(0, d["fish"] - d["uncommon"] - d["rare"])
        if "sessions" in d:
            d["withinFilterSessions"] = d["sessions"] - d["overloadedSessions"]
            if "wipesOverloaded" in d:
                d["wipesWithinFilter"] = d["wipes"] - d["wipesOverloaded"]
        d["commonDeaths"] =d["deaths"] - d.get("uncommonDeaths", 0) - d.get("rareDeaths", 0)
        # Running a shop of bred fish: sales against food, care and new eggs.
        d["operating"] = d.get("salesIn", 0) - d.get("foodOut", 0) - d.get("careOut", 0) - d.get("eggsOut", 0)
        profit.append(d["operating"])
        window = profit[-5:]
        d["profit5"] = sum(window) / len(window)


def first_day(run, metric, at_least=1):
    for d in run["days"]:
        if d.get(metric, 0) >= at_least:
            return d["day"]
    return None


def mean_in(run, metric, a, b):
    v = [d[metric] for d in run["days"] if a <= d["day"] <= b]
    return sum(v) / len(v) if v else None


def death_rate(runs, alive, deaths, a, b):
    """Deaths per 100 fish-days of one rarity between days a and b, over all the runs."""
    fish_days = dead = 0
    for r in runs:
        rows = [d for d in r["days"] if a <= d["day"] <= b]
        if not rows:
            continue
        fish_days += sum(d[alive] for d in rows)
        before = next((d[deaths] for d in reversed(r["days"]) if d["day"] < a), 0)
        dead += rows[-1][deaths] - before
    return (100.0 * dead / fish_days) if fish_days else None, fish_days


def med(values):
    values = [v for v in values if v is not None]
    return statistics.median(values) if values else None


def check(rule, bots):
    who = rule.get("bot", "Cautious")
    runs = [r for b in bots.values() for r in b] if who == "all" else bots.get(who, [])
    if not runs:
        return None, "no runs"
    kind = rule["kind"]

    if kind == "notBefore":
        # No run of these bots gets one before the day.
        firsts = [first_day(r, rule["metric"]) for r in runs]
        early = [f for f in firsts if f is not None and f < rule["day"]]
        got = [f for f in firsts if f is not None]
        text = f"earliest day {min(got)}" if got else "never"
        return not early, f"{text}; {len(early)}/{len(runs)} games too early"

    if kind == "shareBy":
        # Share of games that have one by the day.
        n = sum(1 for r in runs if (first_day(r, rule["metric"]) or 10 ** 6) <= rule["day"])
        share = n / len(runs)
        return rule["min"] <= share <= rule["max"], f"{n}/{len(runs)} games ({share:.0%})"

    if kind == "maxIn":
        # Median game's most at any one time between the days.
        if rule["metric"] not in runs[0]["days"][-1]:
            return None, "not recorded in this run"
        peaks = [max([d[rule["metric"]] for d in r["days"] if rule["from"] <= d["day"] <= rule["to"]] or [0]) for r in runs]
        worst = max(peaks)
        return worst <= rule["atMost"], f"most in any game {worst} (median {med(peaks):g})"

    if kind == "keeps":
        # Of the games that found one by the day, the share still keeping one alive a few days later.
        found = [r for r in runs if (first_day(r, rule["metric"]) or 10 ** 6) <= rule["foundBy"]]
        if not found:
            return False, "none found"
        kept = sum(1 for r in found if any(d["day"] == rule["aliveOn"] and d[rule["metric"]] > 0 for d in r["days"]))
        share = kept / len(found)
        return share >= rule["min"], f"{kept}/{len(found)} still alive on day {rule['aliveOn']}"

    if kind == "eases":
        # The metric's mean falls (or for "higherIsBetter", rises) from the early window to the late one.
        early = med([mean_in(r, rule["metric"], *rule["early"]) for r in runs])
        late = med([mean_in(r, rule["metric"], *rule["late"]) for r in runs])
        if early is None or late is None:
            return None, "no data"
        if rule.get("higherIsBetter"):
            ok = late >= early + rule.get("by", 0) and early <= rule.get("earlyAtMost", 1)
        else:
            ok = late <= early * rule.get("ratio", 1) and early >= rule.get("earlyAtLeast", 0)
        return ok, f"days {rule['early'][0]}-{rule['early'][1]}: {early:.2f}, days {rule['late'][0]}-{rule['late'][1]}: {late:.2f}"

    if kind == "atMostBy":
        values = [next((d[rule["metric"]] for d in reversed(r["days"]) if d["day"] <= rule["day"]), 0) for r in runs]
        m = med(values)
        return m <= rule["atMost"], f"median {m:g} by day {rule['day']}"

    if kind == "dieShare":
        # Of the fish of one rarity owned during the window (alive at its start or arriving in it), the share that died.
        a, b = rule["days"]
        alive_key, deaths_key, seen_key = rule["alive"], rule["deaths"], rule["seen"]
        owned = died = 0
        for r in runs:
            before = [d for d in r["days"] if d["day"] < a]
            rows = [d for d in r["days"] if a <= d["day"] <= b]
            if not rows or seen_key not in rows[-1]:
                continue
            start_alive = before[-1][alive_key] if before else 0
            start_seen = before[-1].get(seen_key, 0) if before else 0
            start_dead = before[-1][deaths_key] if before else 0
            owned += start_alive + rows[-1][seen_key] - start_seen
            died += rows[-1][deaths_key] - start_dead
        if owned == 0:
            return None, "no fish"
        share = 100.0 * died / owned
        ok = share >= rule.get("atLeast", 0) and share <= rule.get("atMost", 100)
        return ok, f"{share:.0f}% of {owned} fish died ({died})"

    if kind == "profitBy":
        # From the day the Fishkeeping level is reached, sales beat food, care and eggs (5-day average) on most days.
        shares, profits = [], []
        for r in runs:
            day = rule["fromDay"] if "fromDay" in rule else first_day(r, "fishkeeping", rule["level"])
            if day is None:
                continue
            rows = [d for d in r["days"] if day <= d["day"] < day + rule.get("span", 20)]
            if rows:
                shares.append(sum(1 for d in rows if d["profit5"] > 0) / len(rows))
                profits.append(statistics.mean(d["operating"] for d in rows))
        if not shares:
            return False, "never reached"
        s, p = med(shares), med(profits)
        return s >= rule.get("min", 0.6) and p > 0, f"profitable on {s:.0%} of days, ${p:,.0f}/day ({len(shares)}/{len(runs)} games)"

    if kind == "ratio":
        # Pooled over every game: sum of num / sum of den in the window. "running": the fields are
        # running totals (use the change over the window); otherwise they are per-day counts.
        a, b = rule["days"]
        num = den = 0
        for r in runs:
            rows = [d for d in r["days"] if a <= d["day"] <= b]
            if not rows or rule["den"] not in rows[-1] or rule["num"] not in rows[-1]:
                continue
            if rule.get("running"):
                before = [d for d in r["days"] if d["day"] < a]
                num += rows[-1][rule["num"]] - (before[-1].get(rule["num"], 0) if before else 0)
                den += rows[-1][rule["den"]] - (before[-1].get(rule["den"], 0) if before else 0)
            else:
                num += sum(d[rule["num"]] for d in rows)
                den += sum(d[rule["den"]] for d in rows)
        if den == 0:
            return None, "not recorded in this run" if num == 0 else "no data"
        v = num / den
        ok = rule.get("atLeast", -1e9) <= v <= rule.get("atMost", 1e9)
        shown = f"{v:.0%}" if rule.get("percent", True) else f"{v:.2f}"
        return ok, f"{shown} ({num:,} of {den:,}), days {a}-{b}"

    if kind == "meanIn":
        m = med([mean_in(r, rule["metric"], *rule["days"]) for r in runs])
        if m is None:
            return None, "no data"
        return rule.get("atLeast", -10 ** 9) <= m <= rule.get("atMost", 10 ** 9), f"{m:,.1f} on average, days {rule['days'][0]}-{rule['days'][1]}"

    if kind == "grows":
        # Sales per day in a late window are at least `ratio` times an early one (median game).
        early = med([mean_in(r, rule["metric"], *rule["early"]) for r in runs])
        late = med([mean_in(r, rule["metric"], *rule["late"]) for r in runs])
        if not early:
            return late is not None and late > 0, f"days {rule['early'][0]}-{rule['early'][1]}: {early}, later: {late}"
        ratio = late / early
        return ratio >= rule["ratio"], f"${early:,.0f}/day on days {rule['early'][0]}-{rule['early'][1]}, ${late:,.0f}/day on days {rule['late'][0]}-{rule['late'][1]} ({ratio:.1f}x)"

    if kind == "harder":
        # Deaths per 100 fish-days: the harder rarity dies more than the easier one.
        a = death_rate(runs, rule["easy"][0], rule["easy"][1], *rule["days"])
        b = death_rate(runs, rule["hard"][0], rule["hard"][1], *rule["days"])
        if a[0] is None or b[0] is None:
            return None, "not enough fish"
        return b[0] >= a[0] * rule.get("ratio", 1.5), f"{rule['easyLabel']} {a[0]:.2f}, {rule['hardLabel']} {b[0]:.2f} deaths per 100 fish-days"

    if kind == "deathRate":
        rate, fish_days = death_rate(runs, rule["alive"], rule["deaths"], *rule["days"])
        if rate is None:
            return None, "no fish"
        ok = rate >= rule.get("atLeast", 0) and rate <= rule.get("atMost", 10 ** 9)
        return ok, f"{rate:.2f} deaths per 100 fish-days ({fish_days} fish-days)"

    return None, f"unknown kind {kind}"
