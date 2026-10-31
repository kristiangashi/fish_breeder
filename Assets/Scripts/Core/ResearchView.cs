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
    /// by the store view, which owns Esc).
    /// </summary>
    public class ResearchView : MonoBehaviour
    {
        static bool open;

        Vector2 leftScroll, rightScroll;
        Rect window;

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

        void Awake() => open = false;

        void Update()
        {
            if (ViewMode.Watching || GUIUtility.keyboardControl != 0) return;
            if (Input.GetKeyDown(KeyCode.R)) Toggle();
        }

        /// <summary>"Research (R)", with a dot while a finished bounty waits to be started or the bench is free.</summary>
        public static string ButtonLabel()
        {
            int ready = 0;
            foreach (var project in Research.Projects) if (Research.State(project) == ResearchState.Ready) ready++;
            return ready > 0 && Research.Current == null ? $"Research (R) <color={UiTheme.SunHex}>●{ready}</color>" : "Research (R)";
        }

        void OnGUI()
        {
            if (!open || ViewMode.Watching) return;
            UiTheme.Apply();
            GUI.depth = -10;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            float w = Mathf.Min(980f, width - 40f);
            float h = Mathf.Min(700f, height - 50f);
            window = new Rect((width - w) * 0.5f, (height - h) * 0.5f, w, h);

            GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.01f, 0.06f, 0.1f, 0.45f), 0f, 0f);

            UiCheck.Begin("Research window", window, UiStyles.Window);
            DrawHeader();
            float column = (w - UiStyles.Window.padding.horizontal - 16f) * 0.5f;
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(column));
            leftScroll = UiCheck.BeginScroll("Research: bench and bounties", leftScroll);
            DrawBench();
            DrawBounties();
            UiCheck.EndScroll();
            GUILayout.EndVertical();
            GUILayout.Space(16f);
            GUILayout.BeginVertical(GUILayout.Width(column));
            rightScroll = UiCheck.BeginScroll("Research: finished and later", rightScroll);
            DrawFinished();
            DrawLater();
            UiCheck.EndScroll();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            UiCheck.End();

            // Modal: clicks outside close it instead of reaching the store behind.
            var e = Event.current;
            if (e.type == EventType.MouseDown && !UiLayout.Editing)
            {
                if (!window.Contains(e.mousePosition)) Close();
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && window.Contains(e.mousePosition)) e.Use();
        }

        void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            Ui.Label("Research", UiTheme.Title, GUILayout.ExpandWidth(false));
            GUILayout.Space(16f);
            Ui.Label($"Research level <b>{Progression.Level(XpTrack.Research)}</b>   ·   speed <b>x{Research.Speed:0.0#}</b>   ·   <b>{Research.PointsPerNight():0}</b> points a night", Rich(), GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
#if UNITY_EDITOR
            if (Ui.Button("Bounties done (dev)", GUILayout.Width(150f))) Research.DevCompleteBounties();
            if (Ui.Button("Finish (dev)", GUILayout.Width(96f))) Research.DevFinish();
#endif
            if (Ui.Button("Close (R / Esc)", GUILayout.Width(120f))) Close();
            GUILayout.EndHorizontal();
            var data = Balance.Data.research;
            Ui.Label($"<size=12>Do a bounty to unlock its research, then start it at the bench. Every night the bench earns {data.basePointsPerDay:0} research points, plus {data.pointsPerDollar:0.##} for every dollar of your daily budget (paid each night, never while the bench is empty), times your research speed. One project at a time, with up to {Research.QueueSize} more queued behind it; switching keeps its points. Finished research makes the game tell you more about your fish.</size>", Rich());
            GUILayout.Space(4f);
        }

        void DrawBench()
        {
            Heading("AT THE RESEARCH BENCH");
            var current = Research.Current;
            if (current != null)
            {
                GUILayout.BeginVertical(UiTheme.CardSelected);
                Ui.Label($"<b>{current.name}</b>", Rich());
                Ui.Label($"<size=11>{current.teaches}</size>", Rich());
                ProgressBar(Research.CurrentFraction, UiTheme.Aqua);
                int left = Research.NightsLeft;
                Ui.Label($"<size=11><b>{Research.Progress(current):0} / {Research.Cost(current)}</b> points. About {left} more night{(left == 1 ? "" : "s")} at {Research.PointsPerNight():0} points a night.</size>", Rich());
                GUILayout.EndVertical();
            }
            else Ui.Label("<i>The bench is free. Start a project once its bounty is done.</i>", Rich());
            DrawQueue();
            DrawBudget(current);

            foreach (var project in Research.Projects)
            {
                if (Research.State(project) != ResearchState.Ready) continue;
                float done = Research.Progress(project);
                GUILayout.BeginVertical(UiTheme.Card);
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                string tag = done > 0f ? $"<color={UiTheme.SunHex}><size=11>on the shelf</size></color>" : $"<color={UiTheme.LeafHex}><size=11>bounty done</size></color>";
                Ui.Label($"<b>{project.name}</b>  {tag}", Rich());
                Ui.Label($"<size=11>{project.teaches}</size>", Rich());
                GUILayout.EndVertical();
                int nights = Research.NightsFor(project);
                if (current == null)
                {
                    if (Ui.Button(new GUIContent("Start", $"About {nights} nights"), UiTheme.Primary, GUILayout.Width(96f), GUILayout.Height(32f)))
                        Research.Start(project);
                }
                else
                {
                    GUILayout.BeginVertical(GUILayout.Width(96f));
                    GUI.enabled = Research.Queue.Count < Research.QueueSize;
                    if (Ui.Button(new GUIContent("Queue", GUI.enabled ? "Starts by itself when the projects ahead of it finish" : $"The queue is full ({Research.QueueSize})"), UiTheme.Primary)) Research.Enqueue(project);
                    GUI.enabled = true;
                    if (Ui.Button(new GUIContent("Switch", $"{current.name} goes back on the shelf, keeping its points"))) Research.Start(project);
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
                if (done > 0f) ProgressBar(Research.Fraction(project), UiTheme.Sun);
                Ui.Label($"<size=11>{(done > 0f ? $"{done:0} / " : "")}{Research.Cost(project)} points: about {nights} night{(nights == 1 ? "" : "s")} at today's budget.</size>", Rich());
                GUILayout.EndVertical();
            }
        }

        /// <summary>The projects lined up behind the bench, next first, with buttons to reorder or take one out.</summary>
        static void DrawQueue()
        {
            var queue = Research.Queue;
            if (Research.Current == null || Research.QueueSize <= 0) return;
            GUILayout.Space(4f);
            Ui.Label($"<b>Up next</b>  <size=11>{queue.Count} of {Research.QueueSize} in the queue</size>", Rich());
            if (queue.Count == 0)
            {
                Ui.Label("<size=11><i>Queue ready projects and they start by themselves, one after another.</i></size>", Rich());
                return;
            }
            for (int i = 0; i < queue.Count; i++)
            {
                var project = queue[i];
                int nights = Research.NightsUntilDone(project);
                GUILayout.BeginHorizontal(UiTheme.Card);
                Ui.Label($"<b>{i + 1}. {project.name}</b>\n<size=11>{Research.Progress(project):0} / {Research.Cost(project)} points · done in about {nights} night{(nights == 1 ? "" : "s")}</size>", Rich());
                GUI.enabled = i > 0;
                if (Ui.Button(new GUIContent("▲", "Move up"), GUILayout.Width(30f))) Research.MoveUp(project);
                GUI.enabled = true;
                if (Ui.Button(new GUIContent("x", "Take it out of the queue"), GUILayout.Width(30f))) Research.Dequeue(project);
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>The daily research budget: +/- buttons and a few quick amounts, with what it buys.</summary>
        static void DrawBudget(ResearchProjectBalance current)
        {
            var data = Balance.Data.research;
            int budget = Research.DailyBudget;
            int step = Mathf.Max(1, data.budgetStep);
            GUILayout.Space(4f);
            GUILayout.BeginVertical(UiTheme.Card);
            GUILayout.BeginHorizontal();
            Ui.Label($"<b>Daily research budget</b>", Rich(), GUILayout.Width(170f));
            if (Ui.Button("-", GUILayout.Width(30f))) Research.SetBudget(budget - step);
            Ui.Label($"<size=16><b>{Economy.Format(budget)}</b></size>", Rich(), GUILayout.Width(70f));
            if (Ui.Button("+", GUILayout.Width(30f))) Research.SetBudget(budget + step);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (int amount in new[] { 0, 25, 50, 100, 250 })
            {
                if (amount > data.maxDailyBudget) continue;
                var style = amount == budget ? UiTheme.Primary : UiTheme.Skin.button;
                if (Ui.Button(amount == 0 ? "None" : Economy.Format(amount), style)) Research.SetBudget(amount);
            }
            GUILayout.EndHorizontal();
            float extra = Research.PointsPerNight(budget) - Research.PointsPerNight(0);
            string what = budget > 0
                ? $"+{extra:0} points a night for {Economy.Format(budget)}, taken from your money every night while something is being researched."
                : "Put money in to research faster. It's paid every night, only while a project is at the bench.";
            if (current == null && budget > 0) what += " <color=#ffd27f>Nothing is at the bench, so nothing is paid tonight.</color>";
            Ui.Label($"<size=11>{what}</size>", Rich());
            GUILayout.EndVertical();
        }

        void DrawBounties()
        {
            GUILayout.Space(8f);
            Heading("BOUNTIES");
            int shown = 0;
            foreach (var project in Research.Projects)
            {
                if (Research.State(project) != ResearchState.Bounty) continue;
                shown++;
                int have = Research.BountyProgress(project);
                GUILayout.BeginVertical(UiTheme.Card);
                GUILayout.BeginHorizontal();
                Ui.Label($"<b>{Research.BountyText(project)}</b>", Rich());
                Ui.Label($"<b>{have}/{project.target}</b>", Rich(), GUILayout.Width(64f));
                GUILayout.EndHorizontal();
                ProgressBar(have / (float)Mathf.Max(1, project.target), UiTheme.Hibiscus);
                Ui.Label($"<size=11>Unlocks <b>{project.name}</b>: {project.teaches}</size>", Rich());
                GUILayout.EndVertical();
            }
            if (shown == 0) Ui.Label("<i>No bounties up right now. More come with higher Research levels.</i>", Rich());
        }

        void DrawFinished()
        {
            Heading("FINISHED");
            int shown = 0;
            foreach (var project in Research.Projects)
            {
                if (Research.State(project) != ResearchState.Done) continue;
                shown++;
                Ui.Label($"<color={UiTheme.LeafHex}>✔</color> <b>{project.name}</b>", Rich());
                Ui.Label($"<size=11>{project.teaches}</size>", Rich());
            }
            if (shown == 0) Ui.Label("<i>Nothing yet.</i>", Rich());
        }

        void DrawLater()
        {
            GUILayout.Space(8f);
            Heading("LATER");
            int shown = 0;
            foreach (var project in Research.Projects)
            {
                if (Research.State(project) != ResearchState.Locked) continue;
                shown++;
                Ui.Label($"<b>{project.name}</b>  <size=11><color=#9fb4bc>{Research.LockReason(project)}</color></size>", Rich());
            }
            if (shown == 0) Ui.Label("<i>Every bounty is up.</i>", Rich());
        }

        static void Heading(string text) => Ui.Label($"<b><color={UiTheme.SunHex}>{text}</color></b>", Rich());

        static void ProgressBar(float fill, Color color)
        {
            var bar = Ui.Slot("Bar", 100f, 8f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0.05f, 0.1f, 0.6f), 0f, 4f);
            if (fill > 0f) GUI.DrawTexture(new Rect(bar.x, bar.y, Mathf.Max(8f, bar.width * Mathf.Clamp01(fill)), bar.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 4f);
            GUILayout.Space(2f);
        }

        static GUIStyle rich;
        static GUIStyle Rich() => rich != null ? rich : rich = new GUIStyle(UiTheme.Skin.label) { richText = true, wordWrap = true };
    }
}
