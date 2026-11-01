using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// The research window (R). The research bench on top: the project being researched with its
    /// points and how many nights it has left, the daily research budget (more money, more points
    /// a night, paid every night), and the projects whose bounty is done, ready to start or switch
    /// to (switching keeps a project's points). Under it the bounty
    /// board: what to do to unlock each project, with progress, and what the project will teach.
    /// On the right, finished research and the projects that open up later. Esc closes it (handled
    /// by the store view, which owns Esc). Drawn in the #research-window (Window.uxml, styled in
    /// FishBreeder.uss).
    /// </summary>
    public class ResearchView : MonoBehaviour
    {
        static bool open;

        UiWindow window;
        UiFrame ui;

        public static bool IsOpen => open;

        public static void Open()
        {
            open = true;
            Research.Refresh();
        }

        public static void Toggle()
        {
            if (open) Close();
            else Open();
        }

        public static void Close() => open = false;

        void Awake()
        {
            open = false;
            window = new UiWindow(transform, "research-window", "Research", 10, true, Close);
            window.CloseButton.text = "Close (R / Esc)";
            ui = window.Frame;
        }

        void OnDestroy() => window?.Destroy();

        void OnDisable() => window?.Show(false);

        void Update()
        {
            if (!ViewMode.Watching && !UiPanels.Typing && Input.GetKeyDown(KeyCode.R)) Toggle();
            bool show = open && !ViewMode.Watching;
            window.Show(show);
            if (show) window.Fill(DrawHeader, DrawBody);
        }

        /// <summary>"Research (R)", with a dot while a finished bounty waits to be started or the bench is free.</summary>
        public static string ButtonLabel()
        {
            int ready = 0;
            foreach (var project in Research.Projects) if (Research.State(project) == ResearchState.Ready) ready++;
            return ready > 0 && Research.Current == null ? $"Research (R) <color={UiTheme.SunHex}>●{ready}</color>" : "Research (R)";
        }

        void DrawHeader()
        {
            ui.Label($"Research level <b>{Progression.Level(XpTrack.Research)}</b>   ·   speed <b>x{Research.Speed:0.0#}</b>   ·   <b>{Research.PointsPerNight():0}</b> points a night", "fb-text fb-header-text");
            ui.Flex();
#if UNITY_EDITOR
            if (ui.Button("Bounties done (dev)", "fb-button fb-button--small")) Research.DevCompleteBounties();
            if (ui.Button("Finish (dev)", "fb-button fb-button--small")) Research.DevFinish();
#endif
        }

        void DrawBody()
        {
            var data = Balance.Data.research;
            ui.Label($"Do a bounty to unlock its research, then start it at the bench. Every night the bench earns {data.basePointsPerDay:0} research points, plus {data.pointsPerDollar:0.##} for every dollar of your daily budget (paid each night, never while the bench is empty), times your research speed. One project at a time, with up to {Research.QueueSize} more queued behind it; switching keeps its points. Finished research makes the game tell you more about your fish.", "fb-text fb-text--small");
            ui.BeginRow("fb-halves");
            ui.BeginScroll("fb-scroll fb-half--left");
            DrawBench();
            DrawBounties();
            ui.Pop();
            ui.BeginScroll();
            DrawFinished();
            DrawLater();
            ui.Pop();
            ui.Pop();
        }

        void DrawBench()
        {
            ui.Section("AT THE RESEARCH BENCH");
            var current = Research.Current;
            if (current != null)
            {
                ui.BeginCard(true);
                ui.Label($"<b>{current.name}</b>");
                ui.Label(current.teaches, "fb-small");
                ui.Bar(Research.CurrentFraction, UiTheme.Aqua);
                int left = Research.NightsLeft;
                ui.Label($"<b>{Research.Progress(current):0} / {Research.Cost(current)}</b> points. About {left} more night{(left == 1 ? "" : "s")} at {Research.PointsPerNight():0} points a night.", "fb-small");
                ui.Pop();
            }
            else ui.Label("<i>The bench is free. Start a project once its bounty is done.</i>");
            DrawQueue();
            DrawBudget(current);

            foreach (var project in Research.Projects)
            {
                if (Research.State(project) != ResearchState.Ready) continue;
                float done = Research.Progress(project);
                ui.BeginCard();
                ui.BeginRow();
                ui.BeginColumn("fb-column fb-grow");
                string tag = done > 0f ? $"<color={UiTheme.SunHex}><size=11>on the shelf</size></color>" : $"<color={UiTheme.LeafHex}><size=11>bounty done</size></color>";
                ui.Label($"<b>{project.name}</b>  {tag}");
                ui.Label(project.teaches, "fb-small");
                ui.Pop();
                int nights = Research.NightsFor(project);
                if (current == null)
                {
                    if (ui.Button("Start", "fb-button fb-button--primary fb-button--wide", true, $"About {nights} nights")) Research.Start(project);
                }
                else
                {
                    ui.BeginColumn("fb-column fb-button-stack");
                    bool room = Research.Queue.Count < Research.QueueSize;
                    if (ui.Button("Queue", "fb-button fb-button--primary", room, room ? "Starts by itself when the projects ahead of it finish" : $"The queue is full ({Research.QueueSize})")) Research.Enqueue(project);
                    if (ui.Button("Switch", "fb-button", true, $"{current.name} goes back on the shelf, keeping its points")) Research.Start(project);
                    ui.Pop();
                }
                ui.Pop();
                if (done > 0f) ui.Bar(Research.Fraction(project), UiTheme.Sun);
                ui.Label($"{(done > 0f ? $"{done:0} / " : "")}{Research.Cost(project)} points: about {nights} night{(nights == 1 ? "" : "s")} at today's budget.", "fb-small");
                ui.Pop();
            }
        }

        /// <summary>The projects lined up behind the bench, next first, with buttons to reorder or take one out.</summary>
        void DrawQueue()
        {
            var queue = Research.Queue;
            if (Research.Current == null || Research.QueueSize <= 0) return;
            ui.Space(4f);
            ui.Label($"<b>Up next</b>  <size=11>{queue.Count} of {Research.QueueSize} in the queue</size>");
            if (queue.Count == 0)
            {
                ui.Label("<i>Queue ready projects and they start by themselves, one after another.</i>", "fb-small");
                return;
            }
            for (int i = 0; i < queue.Count; i++)
            {
                var project = queue[i];
                int nights = Research.NightsUntilDone(project);
                ui.BeginCard(false, "fb-card");
                ui.Label($"<b>{i + 1}. {project.name}</b>\n<size=11>{Research.Progress(project):0} / {Research.Cost(project)} points · done in about {nights} night{(nights == 1 ? "" : "s")}</size>", "fb-text fb-grow");
                if (ui.Button("▲", "fb-button fb-step", i > 0, "Move up")) Research.MoveUp(project);
                if (ui.Button("x", "fb-button fb-step", true, "Take it out of the queue")) Research.Dequeue(project);
                ui.Pop();
            }
        }

        /// <summary>The daily research budget: +/- buttons and a few quick amounts, with what it buys.</summary>
        void DrawBudget(ResearchProjectBalance current)
        {
            var data = Balance.Data.research;
            int budget = Research.DailyBudget;
            int step = Mathf.Max(1, data.budgetStep);
            ui.Space(4f);
            ui.BeginCard();
            ui.BeginRow();
            ui.Label("<b>Daily research budget</b>", "fb-text fb-budget-label");
            if (ui.Button("-", "fb-button fb-step")) Research.SetBudget(budget - step);
            ui.Label($"<size=16><b>{Economy.Format(budget)}</b></size>", "fb-text fb-step-value");
            if (ui.Button("+", "fb-button fb-step")) Research.SetBudget(budget + step);
            ui.Pop();
            ui.BeginRow("fb-row fb-wrap");
            foreach (int amount in new[] { 0, 25, 50, 100, 250 })
            {
                if (amount > data.maxDailyBudget) continue;
                if (ui.Button(amount == 0 ? "None" : Economy.Format(amount), amount == budget ? "fb-button fb-tab fb-tab--on" : "fb-button fb-tab")) Research.SetBudget(amount);
            }
            ui.Pop();
            float extra = Research.PointsPerNight(budget) - Research.PointsPerNight(0);
            string what = budget > 0
                ? $"+{extra:0} points a night for {Economy.Format(budget)}, taken from your money every night while something is being researched."
                : "Put money in to research faster. It's paid every night, only while a project is at the bench.";
            if (current == null && budget > 0) what += " <color=#ffd27f>Nothing is at the bench, so nothing is paid tonight.</color>";
            ui.Label(what, "fb-small");
            ui.Pop();
        }

        void DrawBounties()
        {
            ui.Section("BOUNTIES");
            int shown = 0;
            foreach (var project in Research.Projects)
            {
                if (Research.State(project) != ResearchState.Bounty) continue;
                shown++;
                int have = Research.BountyProgress(project);
                ui.BeginCard();
                ui.Line($"<b>{Research.BountyText(project)}</b>", $"<b>{have}/{project.target}</b>");
                ui.Bar(have / (float)Mathf.Max(1, project.target), UiTheme.Hibiscus);
                ui.Label($"Unlocks <b>{project.name}</b>: {project.teaches}", "fb-small");
                ui.Pop();
            }
            if (shown == 0) ui.Label("<i>No bounties up right now. More come with higher Research levels.</i>");
        }

        void DrawFinished()
        {
            ui.Section("FINISHED");
            int shown = 0;
            foreach (var project in Research.Projects)
            {
                if (Research.State(project) != ResearchState.Done) continue;
                shown++;
                ui.Label($"<color={UiTheme.LeafHex}>✔</color> <b>{project.name}</b>");
                ui.Label(project.teaches, "fb-small");
            }
            if (shown == 0) ui.Label("<i>Nothing yet.</i>");
        }

        void DrawLater()
        {
            ui.Section("LATER");
            int shown = 0;
            foreach (var project in Research.Projects)
            {
                if (Research.State(project) != ResearchState.Locked) continue;
                shown++;
                ui.Label($"<b>{project.name}</b>  <size=11><color=#9fb4bc>{Research.LockReason(project)}</color></size>");
            }
            if (shown == 0) ui.Label("<i>Every bounty is up.</i>");
        }
    }
}
