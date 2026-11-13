"""Turns the pacing simulator's runs.jsonl into one self-contained HTML report.

    python report.py runs.jsonl report.html targets.json

Charts show each bot's median run as a line with the middle half of its runs as a band.
The milestone table gives the median first day each bot reached a milestone, against the targets.
"""
import html
import json
import statistics
import sys

import rules as rulebook

BOT_COLORS = {"Cautious": "#2a78d6", "Greedy": "#d6542a", "Breeder": "#2a9d6a", "Careless": "#9a5ad6", "Mixer": "#c79a1e"}
FALLBACK = ["#3aa0b8", "#b8476e"]

CHARTS = [
    ("money", "Money", "$"),
    ("fishkeeping", "Fishkeeping level", ""),
    ("shopkeeping", "Shopkeeping level", ""),
    ("reputation", "Reputation (0 to 5 stars)", ""),
    ("bestFishValue", "Most valuable fish owned (appraised)", "$"),
    ("soldTotal", "Fish sold (running total)", ""),
    ("fish", "Living fish", ""),
    ("deaths", "Deaths (running total)", ""),
    ("shareHealthy", "Tanks Healthy green (share of tanks with fish)", ""),
    ("shareOvergrown", "Tanks Overgrown (share)", ""),
    ("shareBloom", "Tanks in an algal bloom (share)", ""),
]

STAGES = [("tanksClear", "Clear"), ("tanksHealthy", "Healthy green"), ("tanksOvergrown", "Overgrown"), ("blooms", "Algal bloom")]
STAGE_WINDOWS = [(1, 10), (11, 20), (21, 30), (31, 60), (61, 9999)]


def stage_shares(runs, a, b):
    """Share of tank-days (tanks with fish) in each algae stage between days a and b, over all the runs."""
    totals = [0] * len(STAGES)
    for r in runs:
        for d in r["days"]:
            if a <= d["day"] <= b:
                for i, (key, _) in enumerate(STAGES):
                    totals[i] += d.get(key, 0)
    n = sum(totals)
    return [t / n for t in totals] if n else None


def load(path):
    runs = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                runs.append(json.loads(line))
    for run in runs:
        sold = 0
        for d in run["days"]:
            sold += d.get("sold", 0)
            d["soldTotal"] = sold
            d["reputation"] = round(d.get("reputation", 0) * 5, 2)
        rulebook.enrich(run)
    return runs


def quantile(values, q):
    values = sorted(values)
    if not values:
        return 0
    pos = (len(values) - 1) * q
    lo = int(pos)
    hi = min(lo + 1, len(values) - 1)
    return values[lo] + (values[hi] - values[lo]) * (pos - lo)


def by_bot(runs):
    bots = {}
    for run in runs:
        bots.setdefault(run["bot"], []).append(run)
    return bots


def series(runs, metric):
    """Per day index: (day, q25, median, q75) over the runs that got that far."""
    length = max(len(r["days"]) for r in runs)
    out = []
    for i in range(length):
        values = [r["days"][i][metric] for r in runs if i < len(r["days"])]
        day = next(r["days"][i]["day"] for r in runs if i < len(r["days"]))
        out.append((day, quantile(values, 0.25), quantile(values, 0.5), quantile(values, 0.75)))
    return out


def first_day(run, metric, at_least):
    for d in run["days"]:
        if d.get(metric, 0) >= at_least:
            return d["day"]
    return None


def fmt(v, unit):
    if unit == "$":
        return f"${v:,.0f}"
    return f"{v:,.0f}" if abs(v) >= 10 or v == int(v) else f"{v:.1f}"


def chart(title, unit, data, colors):
    w, h, left, right, top, bottom = 560, 240, 56, 12, 12, 26
    days = [p[0] for s in data.values() for p in s]
    vals = [v for s in data.values() for p in s for v in p[1:]]
    if not days:
        return ""
    x0, x1 = min(days), max(days)
    y1 = max(vals) if vals else 1
    y1 = y1 * 1.05 if y1 > 0 else 1
    sx = lambda d: left + (d - x0) / max(1, x1 - x0) * (w - left - right)
    sy = lambda v: top + (1 - v / y1) * (h - top - bottom)
    parts = [f'<svg viewBox="0 0 {w} {h}" role="img" aria-label="{html.escape(title)}">']
    for i in range(5):
        v = y1 * i / 4
        y = sy(v)
        parts.append(f'<line class="grid" x1="{left}" x2="{w - right}" y1="{y:.1f}" y2="{y:.1f}"/>')
        parts.append(f'<text class="tick" x="{left - 6}" y="{y + 4:.1f}" text-anchor="end">{fmt(v, unit)}</text>')
    step = max(1, round((x1 - x0) / 6 / 10) * 10) if x1 - x0 > 30 else 5
    d = x0 - x0 % step + step if x0 % step else x0
    while d <= x1:
        parts.append(f'<text class="tick" x="{sx(d):.1f}" y="{h - 8}" text-anchor="middle">day {d}</text>')
        d += step
    for bot, s in data.items():
        c = colors[bot]
        band = " ".join(f"{sx(p[0]):.1f},{sy(p[3]):.1f}" for p in s) + " " + " ".join(f"{sx(p[0]):.1f},{sy(p[1]):.1f}" for p in reversed(s))
        parts.append(f'<polygon points="{band}" fill="{c}" opacity="0.13"/>')
        line = " ".join(f"{sx(p[0]):.1f},{sy(p[2]):.1f}" for p in s)
        parts.append(f'<polyline points="{line}" fill="none" stroke="{c}" stroke-width="2"><title>{html.escape(bot)}</title></polyline>')
    parts.append("</svg>")
    return f'<figure class="chart"><figcaption>{html.escape(title)}</figcaption>{"".join(parts)}</figure>'


def main():
    runs_path, out_path = sys.argv[1], sys.argv[2]
    spec = json.load(open(sys.argv[3], encoding="utf-8")) if len(sys.argv) > 3 else {}
    targets = spec.get("targets", [])
    runs = [r for r in load(runs_path) if r["days"]]
    bots = by_bot(runs)
    order = [b for b in BOT_COLORS if b in bots] + [b for b in bots if b not in BOT_COLORS]
    colors = {b: BOT_COLORS.get(b, FALLBACK[i % len(FALLBACK)]) for i, b in enumerate(order)}
    days = max(len(r["days"]) for r in runs)

    legend = "".join(f'<span class="key"><i style="background:{colors[b]}"></i>{html.escape(b)} ({len(bots[b])} runs)</span>' for b in order)
    charts = "".join(chart(t, u, {b: series(bots[b], m) for b in order}, colors) for m, t, u in CHARTS)

    # Milestones: median first day per bot, how many runs got there, and the target.
    head = "".join(f"<th>{html.escape(b)}</th>" for b in order)
    rows = []
    for t in targets:
        cells = []
        for b in order:
            firsts = [first_day(r, t["metric"], t["atLeast"]) for r in bots[b]]
            got = [f for f in firsts if f is not None]
            if not got:
                cells.append('<td class="none">not reached</td>')
                continue
            med = statistics.median(got)
            cls = ""
            if b == "Cautious":
                cls = "early" if med < t["from"] else "late" if med > t["to"] else "ok"
            share = "" if len(got) == len(firsts) else f' <small>({len(got)}/{len(firsts)} runs)</small>'
            cells.append(f'<td class="{cls}">day {med:g}{share}</td>')
        rows.append(f'<tr><th>{html.escape(t["label"])}</th><td class="target">days {t["from"]} to {t["to"]}</td>{"".join(cells)}</tr>')

    # Kristian's rules, pass or fail.
    rule_rows = []
    for rule in spec.get("rules", []):
        ok, text = rulebook.check(rule, bots)
        cls, mark = ("ok", "Pass") if ok else ("none", "n/a") if ok is None else ("late", "Fail")
        rule_rows.append(f'<tr><td>{rule["rule"]}</td><th style="white-space:normal">{html.escape(rule["label"])}</th><td>{html.escape(rule.get("bot", "Cautious"))}</td><td class="{cls}">{mark}</td><td style="white-space:normal">{html.escape(text)}</td></tr>')
    rules_html = ""
    if rule_rows:
        passed = sum(1 for r in rule_rows if ">Pass<" in r)
        rules_html = (f'<h2>Pacing rules: {passed} of {len(rule_rows)} pass</h2><div class="table"><table><thead><tr><th>Rule</th><th>Check</th><th>Bot</th><th>Result</th><th>Measured</th></tr></thead>'
                      f'<tbody>{"".join(rule_rows)}</tbody></table></div>')

    # Algae stages: share of tank-days in each stage, by bot and stretch of days.
    stage_rows = []
    for b in order:
        for a, z in STAGE_WINDOWS:
            if a > days:
                continue
            shares = stage_shares(bots[b], a, z)
            if shares is None:
                continue
            label = f"days {a}-{min(z, days)}"
            cells = "".join(f"<td>{s * 100:.0f}%</td>" for s in shares)
            stage_rows.append(f"<tr><th>{html.escape(b)}</th><td>{label}</td>{cells}</tr>")
    stages_html = ""
    if stage_rows:
        stages_html = ('<h2>Algae stages: share of tank-days</h2><p class="sub">Tanks with fish, counted each morning before any cleaning (what the fish lived in overnight). Healthy green is good for fish; Overgrown and blooms hurt them.</p>'
                       '<div class="table"><table><thead><tr><th>Bot</th><th>Days</th>' + "".join(f"<th>{n}</th>" for _, n in STAGES) + "</tr></thead>"
                       f'<tbody>{"".join(stage_rows)}</tbody></table></div>')

    # Trouble: rescues, errors, money at the end.
    trouble = []
    for b in order:
        rs = bots[b]
        last = [r["days"][-1] for r in rs]
        rescues = [d["rescues"] for d in last]
        broke = sum(1 for d in last if d["money"] < 100)
        errors = sum(1 for r in rs if r.get("error"))
        secs = statistics.mean(r["seconds"] for r in rs) / max(1, len(rs[0]["days"]))
        trouble.append(f'<tr><th>{html.escape(b)}</th><td>${statistics.median(d["money"] for d in last):,.0f}</td>'
                       f'<td>{statistics.median(d["fishkeeping"] for d in last):g} / {statistics.median(d["shopkeeping"] for d in last):g}</td>'
                       f'<td>{statistics.median(d["deaths"] for d in last):g}</td><td>{sum(rescues)} in {sum(1 for x in rescues if x)} runs</td>'
                       f'<td>{broke}</td><td>{errors}</td><td>{secs * 1000:.0f} ms</td></tr>')

    notes = ""
    if len(sys.argv) > 4:
        notes = '<h2>What the bots found</h2><div class="notes">' + open(sys.argv[4], encoding="utf-8").read() + "</div>"
    page = f"""<title>Pacing Simulator Report</title>
<meta name="claude:viewport-fit-sides" content="cover">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&family=Space+Grotesk:wght@500;600&display=swap">
<style>
/* One column of findings, a milestone table, then a grid of small-multiple charts. */
:root {{ --bg:#f4f7f8; --panel:#ffffff; --text:#14232b; --muted:#566a74; --line:#dde5e9; --ok:#1f8a4c; --late:#c2412d; --early:#a8740c;
  --display:"Space Grotesk", "Segoe UI", sans-serif; --body:Inter, "Segoe UI", system-ui, sans-serif; }}
@media (prefers-color-scheme: dark) {{ :root:not([data-theme="light"]) {{ --bg:#0f171b; --panel:#162228; --text:#e3edf1; --muted:#93a8b2; --line:#26363e; --ok:#4cc17e; --late:#f07a62; --early:#e2b24a; color-scheme:dark; }} }}
:root[data-theme="dark"] {{ --bg:#0f171b; --panel:#162228; --text:#e3edf1; --muted:#93a8b2; --line:#26363e; --ok:#4cc17e; --late:#f07a62; --early:#e2b24a; color-scheme:dark; }}
body {{ background:var(--bg); color:var(--text); font:15px/1.5 var(--body); }}
main {{ max-width:1180px; margin:0 auto; padding-block:24px 48px; padding-left:max(16px, env(safe-area-inset-left, 0px)); padding-right:max(16px, env(safe-area-inset-right, 0px)); }}
h1, h2 {{ font-family:var(--display); text-wrap:balance; }}
h1 {{ font-size:26px; margin:0 0 4px; }} h2 {{ font-size:18px; margin:32px 0 10px; }}
p.sub {{ color:var(--muted); margin:0 0 16px; max-width:70ch; }}
.notes {{ max-width:72ch; }} .notes li {{ margin-bottom:8px; }}
.legend {{ display:flex; flex-wrap:wrap; gap:14px; margin:8px 0 4px; }} .key i {{ display:inline-block; width:12px; height:12px; border-radius:3px; margin-right:6px; vertical-align:-1px; }}
.charts {{ display:grid; grid-template-columns:repeat(auto-fit,minmax(min(100%,320px),1fr)); gap:14px; }}
.chart {{ margin:0; min-width:0; background:var(--panel); border:1px solid var(--line); border-radius:8px; padding:10px 12px 4px; }}
.chart figcaption {{ font-weight:600; font-size:14px; margin-bottom:4px; }}
svg {{ width:100%; height:auto; display:block; }} .grid {{ stroke:var(--line); }} .tick {{ fill:var(--muted); font-size:11px; font-family:var(--body); }}
.table {{ overflow-x:auto; background:var(--panel); border:1px solid var(--line); border-radius:8px; }}
table {{ border-collapse:collapse; width:100%; font-size:14px; font-variant-numeric:tabular-nums; }} th, td {{ padding:8px 10px; border-bottom:1px solid var(--line); text-align:left; white-space:nowrap; }}
thead th {{ color:var(--muted); font-weight:600; font-size:12px; letter-spacing:.04em; text-transform:uppercase; }} tbody th {{ font-weight:500; }}
td.ok {{ color:var(--ok); font-weight:600; }} td.late {{ color:var(--late); font-weight:600; }} td.early {{ color:var(--early); font-weight:600; }} td.none, td.target {{ color:var(--muted); }}
</style>
<main>
<h1>Pacing simulator</h1>
<p class="sub">{len(runs)} games played by bots through the real game code, {days} days each. Lines are each bot's median run; the shaded band is the middle half of its runs.</p>
{notes}
{rules_html}
<h2>Milestones: median first day</h2>
<p class="sub">Targets are for the Cautious bot: <span style="color:var(--ok)">on target</span>, <span style="color:var(--early)">early</span>, <span style="color:var(--late)">late</span>. "Not reached" means no run got there in {days} days.</p>
<div class="table"><table><thead><tr><th>Milestone</th><th>Target</th>{head}</tr></thead><tbody>{''.join(rows)}</tbody></table></div>
{stages_html}
<h2>Curves</h2>
<div class="legend">{legend}</div>
<div class="charts">{charts}</div>
<h2>End of the run (medians)</h2>
<div class="table"><table><thead><tr><th>Bot</th><th>Money</th><th>Fishkeeping / Shopkeeping</th><th>Deaths</th><th>Grandma rescues</th><th>Runs ending broke</th><th>Errors</th><th>Time per day</th></tr></thead><tbody>{''.join(trouble)}</tbody></table></div>
</main>"""
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(page)


if __name__ == "__main__":
    main()
