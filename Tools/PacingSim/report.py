"""Turns the pacing simulator's runs.jsonl into one self-contained HTML report.

    python report.py runs.jsonl report.html targets.json

Charts show each bot's median run as a line with the middle half of its runs as a band.
The milestone table gives the median first day each bot reached a milestone, against the targets.
"""
import html
import json
import statistics
import sys

BOT_COLORS = {"Cautious": "#2a78d6", "Greedy": "#d6542a", "Breeder": "#2a9d6a", "Careless": "#9a5ad6"}
FALLBACK = ["#c79a1e", "#3aa0b8", "#b8476e"]

CHARTS = [
    ("money", "Money", "$"),
    ("fishkeeping", "Fishkeeping level", ""),
    ("shopkeeping", "Shopkeeping level", ""),
    ("reputation", "Reputation (0 to 5 stars)", ""),
    ("bestFishValue", "Most valuable fish owned (appraised)", "$"),
    ("soldTotal", "Fish sold (running total)", ""),
    ("fish", "Living fish", ""),
    ("deaths", "Deaths (running total)", ""),
]


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
    targets = json.load(open(sys.argv[3], encoding="utf-8"))["targets"] if len(sys.argv) > 3 else []
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

    page = f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Pacing Simulator Report</title>
<style>
:root {{ --bg:#f7f8fa; --panel:#fff; --text:#1d2430; --muted:#5d6878; --line:#e3e7ee; --ok:#1f8a4c; --late:#c2412d; --early:#b07a10; }}
@media (prefers-color-scheme: dark) {{ :root:not([data-theme="light"]) {{ --bg:#12161d; --panel:#1a2029; --text:#e6eaf0; --muted:#98a3b3; --line:#2b3340; --ok:#4cc17e; --late:#f07a62; --early:#e2b24a; }} }}
:root[data-theme="dark"] {{ --bg:#12161d; --panel:#1a2029; --text:#e6eaf0; --muted:#98a3b3; --line:#2b3340; --ok:#4cc17e; --late:#f07a62; --early:#e2b24a; }}
body {{ margin:0; background:var(--bg); color:var(--text); font:15px/1.45 system-ui,-apple-system,Segoe UI,sans-serif; }}
main {{ max-width:1180px; margin:0 auto; padding:24px 16px 48px; }}
h1 {{ font-size:24px; margin:0 0 4px; }} h2 {{ font-size:18px; margin:32px 0 10px; }}
p.sub {{ color:var(--muted); margin:0 0 16px; }}
.legend {{ display:flex; flex-wrap:wrap; gap:14px; margin:8px 0 4px; }} .key i {{ display:inline-block; width:12px; height:12px; border-radius:3px; margin-right:6px; vertical-align:-1px; }}
.charts {{ display:grid; grid-template-columns:repeat(auto-fit,minmax(320px,1fr)); gap:14px; }}
.chart {{ margin:0; background:var(--panel); border:1px solid var(--line); border-radius:10px; padding:10px 12px 4px; }}
.chart figcaption {{ font-weight:600; font-size:14px; margin-bottom:4px; }}
svg {{ width:100%; height:auto; display:block; }} .grid {{ stroke:var(--line); }} .tick {{ fill:var(--muted); font-size:11px; }}
.table {{ overflow-x:auto; background:var(--panel); border:1px solid var(--line); border-radius:10px; }}
table {{ border-collapse:collapse; width:100%; font-size:14px; }} th, td {{ padding:8px 10px; border-bottom:1px solid var(--line); text-align:left; white-space:nowrap; }}
thead th {{ color:var(--muted); font-weight:600; }} tbody th {{ font-weight:500; }}
td.ok {{ color:var(--ok); font-weight:600; }} td.late {{ color:var(--late); font-weight:600; }} td.early {{ color:var(--early); font-weight:600; }} td.none, td.target {{ color:var(--muted); }}
</style></head><body><main>
<h1>Pacing simulator</h1>
<p class="sub">{len(runs)} games played by bots through the real game code, {days} days each. Lines are each bot's median run; the shaded band is the middle half of its runs.</p>
<div class="legend">{legend}</div>
<h2>Milestones: median first day</h2>
<p class="sub">Targets are for the Cautious bot: <span style="color:var(--ok)">on target</span>, <span style="color:var(--early)">early</span>, <span style="color:var(--late)">late</span>.</p>
<div class="table"><table><thead><tr><th>Milestone</th><th>Target</th>{head}</tr></thead><tbody>{''.join(rows)}</tbody></table></div>
<h2>Curves</h2>
<div class="charts">{charts}</div>
<h2>End of the run (medians)</h2>
<div class="table"><table><thead><tr><th>Bot</th><th>Money</th><th>Fishkeeping / Shopkeeping</th><th>Deaths</th><th>Grandma rescues</th><th>Runs ending broke</th><th>Errors</th><th>Time per day</th></tr></thead><tbody>{''.join(trouble)}</tbody></table></div>
</main></body></html>"""
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(page)


if __name__ == "__main__":
    main()
