using System.Collections.Generic;
using BeachLife.Core;
using UnityEngine;

namespace BeachLife.Unity
{
    /// <summary>
    /// IMGUI heads-up display: money, clock, build palette, inspector and event log.
    /// Deliberately code-only so the game is playable straight from an empty scene.
    /// </summary>
    public sealed class HudGui : MonoBehaviour
    {
        public static bool PointerOverHud { get; private set; }

        private const float PanelWidth = 240f;
        private const float TopBarHeight = 34f;

        private ResortRunner _runner;
        private Resort _resort;
        private Vector2 _paletteScroll;
        private Vector2 _logScroll;
        private readonly List<Rect> _hudRects = new List<Rect>();
        private GUIStyle _topStyle;

        private void Start()
        {
            _runner = ResortRunner.Instance;
            _resort = _runner.Resort;
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
            if (_resort == null) return;
            _hudRects.Clear();
            EnsureStyles();
            DrawTopBar();
            DrawPalette();
            DrawInspector();
            DrawLog();
        }

        private void EnsureStyles()
        {
            if (_topStyle != null) return;
            _topStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        }

        private void DrawTopBar()
        {
            var rect = new Rect(0f, 0f, Screen.width, TopBarHeight);
            _hudRects.Add(rect);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(8f, 6f, Screen.width - 16f, TopBarHeight));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"${_resort.Economy.Cash:n0}", _topStyle, GUILayout.Width(110f));
            GUILayout.Label(_resort.Clock.TimeLabel, _topStyle, GUILayout.Width(140f));
            GUILayout.Label($"Guests {_resort.Tourists.Count}/{_resort.TouristCapacity}", GUILayout.Width(130f));
            GUILayout.Label($"Reputation {_resort.Reputation:0}", GUILayout.Width(130f));
            GUILayout.Label($"Walkouts {_resort.TotalWalkouts}", GUILayout.Width(110f));

            GUILayout.FlexibleSpace();
            for (int i = 0; i < _runner.SpeedSteps.Length; i++)
            {
                string label = i == 0 ? "||" : new string('>', i);
                bool active = _runner.SpeedIndex == i;
                if (GUILayout.Toggle(active, label, GUI.skin.button, GUILayout.Width(38f)) && !active)
                    _runner.SetSpeed(i);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawPalette()
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

                bool affordable = _resort.Economy.CanAfford(def.BuildCost);
                bool selected = controller.Mode == PointerMode.Build && controller.SelectedDef == def;
                GUI.enabled = affordable;
                if (GUILayout.Toggle(selected, $"{def.DisplayName}  ${def.BuildCost:n0}", GUI.skin.button) && !selected)
                    controller.ChooseBuilding(def);
                GUI.enabled = true;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawInspector()
        {
            var controller = BuildController.Instance;
            var rect = new Rect(Screen.width - PanelWidth - 8f, TopBarHeight + 8f, PanelWidth, 260f);
            _hudRects.Add(rect);
            GUI.Box(rect, "Inspector");
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 22f, rect.width - 16f, rect.height - 30f));

            var building = controller.SelectedBuilding;
            if (building != null && _resort.Buildings.ContainsKey(building.Id))
            {
                GUILayout.Label(building.Def.DisplayName);
                GUILayout.Label($"Guests inside: {building.Occupants.Count}/{building.Def.Capacity}");
                GUILayout.Label($"Queue: {building.Waiting.Count}");
                GUILayout.Label($"Revenue: ${building.Revenue:n0}");
                GUILayout.Label($"Upkeep: ${building.Def.DailyUpkeep:n0}/day");
                GUILayout.Label($"Dirtiness: {building.Dirtiness * 100f:0}%");
                GUILayout.Label($"Price: ${building.Price} (x{building.PriceMultiplier:0.00})");
                building.PriceMultiplier = GUILayout.HorizontalSlider(building.PriceMultiplier, 0.5f, 2f);
                if (GUILayout.Button("Demolish (50% back)"))
                {
                    _resort.Demolish(building.Id);
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

        private void DrawLog()
        {
            var rect = new Rect(8f, Screen.height - 180f, Screen.width - 16f - PanelWidth, 172f);
            _hudRects.Add(rect);
            GUI.Box(rect, "Resort log");
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 22f, rect.width - 16f, rect.height - 30f));
            _logScroll = GUILayout.BeginScrollView(_logScroll);
            var log = _resort.EventLog;
            for (int i = log.Count - 1; i >= 0 && i > log.Count - 30; i--)
                GUILayout.Label(log[i]);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
