using System.Collections.Generic;
using BeachLife.Core;
using UnityEngine;

namespace BeachLife.Unity
{
    public enum HudPanel
    {
        None,
        Staff,
        Marketing,
        Goals,
        SaveLoad,
    }

    /// <summary>
    /// IMGUI heads-up display: money, clock and weather, build palette, inspector, staff and
    /// marketing management, goals, save slots and the bankruptcy screen.
    /// Deliberately code-only so the game is playable straight from an empty scene.
    /// </summary>
    public sealed class HudGui : MonoBehaviour
    {
        public static bool PointerOverHud { get; private set; }

        private const float PanelWidth = 240f;
        private const float TopBarHeight = 34f;
        private const float FloatingWidth = 320f;
        private const float FloatingHeight = 300f;

        private ResortRunner _runner;
        private Vector2 _paletteScroll;
        private Vector2 _panelScroll;
        private Vector2 _logScroll;
        private HudPanel _openPanel = HudPanel.None;
        private bool _confirmExit;
        private int _speedBeforeDialog = 1;
        private readonly List<Rect> _hudRects = new List<Rect>();
        private GUIStyle _topStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _bigStyle;

        private void Start()
        {
            _runner = ResortRunner.Instance;
        }

        private void Update()
        {
            var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            PointerOverHud = false;
            foreach (var rect in _hudRects)
            {
                if (!rect.Contains(mouse)) continue;
                PointerOverHud = true;
                break;
            }
        }

        private void OnGUI()
        {
            var resort = CurrentResort;
            if (resort == null) return;
            _hudRects.Clear();
            EnsureStyles();

            if (MainMenu.Visible)
            {
                _confirmExit = false; // the menu supersedes the exit prompt
                return;
            }

            if (resort.Bankrupt) { DrawGameOver(resort); return; }

            DrawTopBar(resort);
            DrawPalette(resort);
            DrawInspector(resort);
            DrawLog(resort);
            DrawFloatingPanel(resort);
            if (_confirmExit) DrawExitDialog(resort);
        }

        private Resort CurrentResort => _runner != null ? _runner.Resort : null;

        private void EnsureStyles()
        {
            if (_topStyle != null) return;
            _topStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.BoldAndItalic };
            _bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        // ---------------------------------------------------------------- top bar

        private void DrawTopBar(Resort resort)
        {
            var rect = new Rect(0f, 0f, Screen.width, TopBarHeight);
            _hudRects.Add(rect);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(8f, 6f, Screen.width - 16f, TopBarHeight));
            GUILayout.BeginHorizontal();

            GUILayout.Label("BEACH LIFE", _titleStyle, GUILayout.Width(96f));
            GUILayout.Label($"${resort.Economy.Cash:n0}", _topStyle, GUILayout.Width(100f));
            var weather = resort.Weather;
            GUILayout.Label($"{resort.Clock.TimeLabel}  {weather.DisplayName}", GUILayout.Width(230f));
            GUILayout.Label($"next: {WeatherTable.Get(resort.ForecastWeather).DisplayName}", GUILayout.Width(130f));
            GUILayout.Label($"Guests {resort.Tourists.Count}/{resort.TouristCapacity}", GUILayout.Width(130f));
            GUILayout.Label($"Rep {resort.Reputation:0}", GUILayout.Width(70f));
            GUILayout.Label($"Walkouts {resort.TotalWalkouts}", GUILayout.Width(100f));

            GUILayout.FlexibleSpace();
            for (int i = 0; i < _runner.SpeedSteps.Length; i++)
            {
                string label = i == 0 ? "||" : new string('>', i);
                bool active = _runner.SpeedIndex == i;
                if (GUILayout.Toggle(active, label, GUI.skin.button, GUILayout.Width(34f)) && !active)
                    _runner.SetSpeed(i);
            }

            GUILayout.Space(8f);
            TogglePanelButton(HudPanel.Staff, $"Staff {resort.StaffMembers.Count}");
            TogglePanelButton(HudPanel.Marketing, resort.Campaign == CampaignLevel.None ? "Ads" : "Ads *");
            bool goalsOpen = _openPanel == HudPanel.Goals;
            if (GUILayout.Toggle(goalsOpen, "Goals", GUI.skin.button) && !goalsOpen) _openPanel = HudPanel.Goals;
            TogglePanelButton(HudPanel.SaveLoad, "Save");
            if (GUILayout.Button("Menu", GUILayout.Width(58f)))
            {
                _confirmExit = true;
                _speedBeforeDialog = _runner.SpeedIndex;
                _runner.SetSpeed(0); // hold the world while the player decides
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- exit dialog

        private void DrawExitDialog(Resort resort)
        {
            const float width = 400f, height = 190f;
            var rect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            _hudRects.Add(rect);
            GUI.Box(rect, "Leave the resort?");

            GUILayout.BeginArea(new Rect(rect.x + 16f, rect.y + 26f, width - 32f, height - 40f));

            int slot = FirstFreeSlot();
            string slotLabel = SaveManager.HasSave(slot) ? SaveManager.SlotLabel(slot) : "empty";
            if (GUILayout.Button($"Save and leave  (slot {slot + 1}: {slotLabel})", GUILayout.Height(34f)))
            {
                if (SaveManager.Save(resort, slot))
                    resort.EventLog.Add($"[{resort.Clock.TimeLabel}] Game saved to slot {slot + 1}");
                ReturnToMenu();
            }
            if (GUILayout.Button("Leave without saving", GUILayout.Height(34f)))
                ReturnToMenu();
            if (GUILayout.Button("Cancel", GUILayout.Height(34f)))
            {
                _confirmExit = false;
                _runner.SetSpeed(_speedBeforeDialog);
            }

            GUILayout.EndArea();
        }

        private static int FirstFreeSlot()
        {
            for (int slot = 0; slot < SaveManager.SlotCount; slot++)
                if (!SaveManager.HasSave(slot)) return slot;
            return 0;
        }

        private void ReturnToMenu()
        {
            _confirmExit = false;
            _runner.SetSpeed(Mathf.Max(1, _speedBeforeDialog));
            var menu = FindObjectOfType<MainMenu>();
            if (menu != null) menu.Open();
        }

        private void TogglePanelButton(HudPanel panel, string label)
        {
            bool open = _openPanel == panel;
            if (GUILayout.Toggle(open, label, GUI.skin.button) && !open) _openPanel = panel;
        }

        // ---------------------------------------------------------------- floating panels

        private void DrawFloatingPanel(Resort resort)
        {
            if (_openPanel == HudPanel.None) return;
            var rect = new Rect((Screen.width - FloatingWidth) / 2f, TopBarHeight + 40f, FloatingWidth, FloatingHeight);
            _hudRects.Add(rect);
            string title = _openPanel == HudPanel.Staff ? "Staff"
                : _openPanel == HudPanel.Marketing ? "Marketing"
                : _openPanel == HudPanel.Goals ? "Goals"
                : "Save / Load";
            GUI.Box(rect, title);
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 22f, rect.width - 16f, rect.height - 30f));
            _panelScroll = GUILayout.BeginScrollView(_panelScroll);

            if (_openPanel == HudPanel.Staff) DrawStaffPanel(resort);
            else if (_openPanel == HudPanel.Marketing) DrawMarketingPanel(resort);
            else if (_openPanel == HudPanel.Goals) DrawGoalsPanel(resort);
            else DrawSavePanel(resort);

            GUILayout.EndScrollView();
            if (GUILayout.Button("Close", GUILayout.Width(70f))) _openPanel = HudPanel.None;
            GUILayout.EndArea();
        }

        private void DrawStaffPanel(Resort resort)
        {
            foreach (var info in StaffTable.All)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{info.DisplayName} - ${info.HireFee:n0} + ${info.WagePerDay}/day");
                GUI.enabled = resort.CanHire(info.Type);
                if (GUILayout.Button("Hire", GUILayout.Width(60f))) resort.Hire(info.Type);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8f);
            if (resort.StaffMembers.Count == 0)
            {
                GUILayout.Label("No staff yet. Cleaners fight litter, attendants boost a venue.");
                return;
            }

            foreach (var member in resort.StaffMembers)
            {
                GUILayout.BeginHorizontal();
                string assignment = member.Type == StaffType.Attendant
                    ? DescribeAssignment(resort, member)
                    : DescribeWorkState(member);
                GUILayout.Label($"{member.Name}  ({assignment})", GUILayout.Width(230f));
                if (GUILayout.Button("Fire", GUILayout.Width(50f))) resort.Fire(member.Id);
                GUILayout.EndHorizontal();
            }
        }

        private static string DescribeAssignment(Resort resort, Staff member)
        {
            if (member.AssignedBuildingId < 0 || !resort.Buildings.TryGetValue(member.AssignedBuildingId, out var building))
                return "unassigned";
            return $"at {building.Def.DisplayName}";
        }

        private static string DescribeWorkState(Staff member)
        {
            switch (member.State)
            {
                case StaffState.Walking: return "heading to a job";
                case StaffState.Working: return "working";
                default: return "idle";
            }
        }

        private void DrawMarketingPanel(Resort resort)
        {
            GUILayout.Label("Ads raise tomorrow's guest arrivals. Billed daily.");
            GUILayout.Space(4f);
            foreach (var info in CampaignTable.All)
            {
                string effect = info.Level == CampaignLevel.None ? "-" : $"+{Mathf.RoundToInt((info.ArrivalMultiplier - 1f) * 100)}% guests";
                string label = info.Level == CampaignLevel.None
                    ? $"{info.DisplayName}"
                    : $"{info.DisplayName}   ${info.CostPerDay:n0}/day   ({effect})";
                bool active = resort.Campaign == info.Level;
                if (GUILayout.Toggle(active, label, GUI.skin.button) && !active)
                    resort.SetCampaign(info.Level);
            }
        }

        private void DrawGoalsPanel(Resort resort)
        {
            GUILayout.Label("Milestones pay a cash reward the moment they complete.");
            GUILayout.Space(4f);
            foreach (var def in GoalTable.All)
            {
                bool done = resort.Goals.IsAchieved(def.Id);
                var style = GUI.skin.label;
                string mark = done ? "[x] " : "[  ] ";
                var old = style.normal.textColor;
                if (done) style.normal.textColor = new Color(0.4f, 0.85f, 0.4f);
                GUILayout.Label(mark + def.DisplayName + $"  (+${def.RewardCash:n0})", style);
                style.normal.textColor = old;
            }
        }

        private void DrawSavePanel(Resort resort)
        {
            GUILayout.Label($"Files live in {Application.persistentDataPath}");
            GUILayout.Space(4f);
            for (int slot = 0; slot < SaveManager.SlotCount; slot++)
            {
                bool hasSave = SaveManager.HasSave(slot);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Slot {slot + 1}: {(hasSave ? SaveManager.SlotLabel(slot) : "(empty)")}", GUILayout.Width(200f));
                if (GUILayout.Button("Save", GUILayout.Width(46f)) && SaveManager.Save(resort, slot))
                    resort.EventLog.Add($"[{resort.Clock.TimeLabel}] Game saved to slot {slot + 1}");
                GUI.enabled = hasSave;
                if (GUILayout.Button("Load", GUILayout.Width(46f))) LoadSlot(slot);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private void LoadSlot(int slot)
        {
            var snapshot = SaveManager.Load(slot);
            if (snapshot == null) return;
            var resort = CurrentResort;
            if (resort == null) return;
            try
            {
                resort.ApplySnapshot(snapshot);
                _runner.MarkSessionStarted();
                resort.EventLog.Add($"[{resort.Clock.TimeLabel}] Game loaded from slot {slot + 1}");
                _openPanel = HudPanel.None;
            }
            catch (System.ArgumentException e)
            {
                Debug.LogError($"BeachLife: load failed - {e.Message}");
                CurrentResortEventLogAdd($"Broken save in slot {slot + 1}: {e.Message}");
            }
        }

        private void CurrentResortEventLogAdd(string message)
        {
            var resort = CurrentResort;
            if (resort != null) resort.EventLog.Add(message);
        }

        // ---------------------------------------------------------------- build palette

        private void DrawPalette(Resort resort)
        {
            var rect = new Rect(8f, TopBarHeight + 8f, PanelWidth, Screen.height - TopBarHeight - 200f);
            _hudRects.Add(rect);
            GUI.Box(rect, "Build");

            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 22f, rect.width - 16f, rect.height - 30f));
            GUILayout.BeginHorizontal();
            var controller = BuildController.Instance;
            if (GUILayout.Toggle(controller.Mode == PointerMode.Inspect, "Select", GUI.skin.button))
                if (controller.Mode != PointerMode.Inspect) controller.SetMode(PointerMode.Inspect);
            if (GUILayout.Toggle(controller.Mode == PointerMode.Demolish, "Demolish", GUI.skin.button))
                if (controller.Mode != PointerMode.Demolish) controller.SetMode(PointerMode.Demolish);
            GUILayout.EndHorizontal();

            _paletteScroll = GUILayout.BeginScrollView(_paletteScroll);
            BuildingCategory? lastCategory = null;
            foreach (var def in BuildingCatalog.All)
            {
                if (lastCategory != def.Category)
                {
                    GUILayout.Space(6f);
                    GUILayout.Label(def.Category.ToString());
                    lastCategory = def.Category;
                }

                bool affordable = resort.Economy.CanAfford(def.BuildCost);
                bool selected = controller.Mode == PointerMode.Build && controller.SelectedDef == def;
                GUI.enabled = affordable;
                if (GUILayout.Toggle(selected, $"{def.DisplayName}  ${def.BuildCost:n0}", GUI.skin.button) && !selected)
                    controller.ChooseBuilding(def);
                GUI.enabled = true;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- inspector

        private void DrawInspector(Resort resort)
        {
            var controller = BuildController.Instance;
            var rect = new Rect(Screen.width - PanelWidth - 8f, TopBarHeight + 8f, PanelWidth, 280f);
            _hudRects.Add(rect);
            GUI.Box(rect, "Inspector");
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 22f, rect.width - 16f, rect.height - 30f));

            var building = controller.SelectedBuilding;
            if (building != null && resort.Buildings.ContainsKey(building.Id))
            {
                GUILayout.Label(building.Def.DisplayName);
                GUILayout.Label($"Guests inside: {building.Occupants.Count}/{resort.EffectiveCapacity(building)}");
                GUILayout.Label($"Queue: {building.Waiting.Count}   Hotel beds held: {building.LongStayers.Count}");
                GUILayout.Label($"Revenue: ${building.Revenue:n0}");
                GUILayout.Label($"Upkeep: ${building.Def.DailyUpkeep:n0}/day");
                GUILayout.Label($"Dirtiness: {building.Dirtiness * 100f:0}%");
                GUILayout.Label($"Price: ${building.Price} (x{building.PriceMultiplier:0.00})");
                building.PriceMultiplier = GUILayout.HorizontalSlider(building.PriceMultiplier, 0.5f, 2f);
                DrawAttendantControls(resort, building);
                if (GUILayout.Button("Demolish (50% back)"))
                {
                    resort.Demolish(building.Id);
                    controller.SetMode(PointerMode.Inspect);
                }
            }
            else if (controller.Mode == PointerMode.Build && controller.SelectedDef != null)
            {
                var def = controller.SelectedDef;
                GUILayout.Label(def.DisplayName);
                GUILayout.Label($"Size: {def.Width}x{def.Height}   Capacity: {def.Capacity}");
                GUILayout.Label($"Cost: ${def.BuildCost:n0}   Upkeep: ${def.DailyUpkeep:n0}/day");
                GUILayout.Label($"Charges guests: ${def.TicketPrice}");
                foreach (var pair in def.Satisfies)
                    GUILayout.Label($"  {pair.Key}: +{pair.Value * 100f:0}%");
                if (def.AllowedTiles.Count > 0)
                    GUILayout.Label($"Only on: {string.Join(", ", def.AllowedTiles)}");
            }
            else
            {
                GUILayout.Label("Click a building to inspect it.");
                GUILayout.Label("WASD/middle-drag: pan\nQ/E: rotate\nWheel: zoom\nX: demolish, Esc: cancel");
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(controller.LastMessage);
            GUILayout.EndArea();
        }

        private void DrawAttendantControls(Resort resort, Building building)
        {
            var current = resort.AttendantFor(building);
            if (current != null)
            {
                GUILayout.Label($"Attendant: {current.Name}");
                if (GUILayout.Button("Remove attendant")) current.AssignedBuildingId = -1;
                return;
            }

            Staff unassigned = null;
            foreach (var member in resort.StaffMembers)
            {
                if (member.Type != StaffType.Attendant) continue;
                if (member.AssignedBuildingId < 0) { unassigned = member; break; }
            }
            if (unassigned != null && GUILayout.Button($"Assign {unassigned.Name}"))
                resort.AssignAttendant(unassigned.Id, building.Id);
        }

        // ---------------------------------------------------------------- log & game over

        private void DrawLog(Resort resort)
        {
            var rect = new Rect(8f, Screen.height - 180f, Screen.width - 16f - PanelWidth, 172f);
            _hudRects.Add(rect);
            GUI.Box(rect, "Resort log");
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 22f, rect.width - 16f, rect.height - 30f));
            _logScroll = GUILayout.BeginScrollView(_logScroll);
            var log = resort.EventLog;
            for (int i = log.Count - 1; i >= 0 && i > log.Count - 30; i--)
                GUILayout.Label(log[i]);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawGameOver(Resort resort)
        {
            var rect = new Rect(0f, 0f, Screen.width, Screen.height);
            _hudRects.Add(rect);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 220f, Screen.height / 2f - 140f, 440f, 280f));
            GUILayout.Label("BANKRUPT", _bigStyle);
            GUILayout.Space(12f);
            GUILayout.Label(
                $"The books stayed in the red for {resort.Config.BankruptcyGraceDays} days and the bank called it quits.\n\n"
                + $"Days survived: {resort.Clock.Day}      Total guests: {resort.TotalVisitors:n0}\n"
                + $"Angry walkouts: {resort.TotalWalkouts:n0}      Final reputation: {resort.Reputation:0}");
            GUILayout.Space(18f);
            if (GUILayout.Button("Back to main menu", GUILayout.Height(40f)))
            {
                _runner.ClearSession();
                var menu = FindObjectOfType<MainMenu>();
                if (menu != null) menu.Open();
            }
            GUILayout.EndArea();
        }
    }
}
