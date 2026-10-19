#if UNITY_EDITOR
using UnityEditor;
#endif
using BeachLife.Core;
using UnityEngine;

namespace BeachLife.Unity
{
    /// <summary>
    /// Code-only main menu shown over the live resort panorama: continue, new game with a
    /// difficulty pick, persistent settings, how-to-play and quit. While it is up the
    /// simulation is paused and world input is blocked (camera slowly orbits instead).
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        public static bool Visible { get; private set; }

        private enum MenuScreen { Main, NewGame, Settings, HowTo }

        private ResortRunner _runner;
        private CameraRig _rig;
        private Light _sun;
        private MenuScreen _screen = MenuScreen.Main;
        private int _selectedDifficulty = (int)Difficulty.Classic;
        private int _lastSpeedIndex = 1;
        private Vector2 _howToScroll;

        private bool _fullscreen = true;
        private bool _vsync = true;
        private bool _shadows = true;
        private float _panSpeed = 18f;
        private float _clockPace = 0.25f;

        private const string PrefFullscreen = "beachlife.fullscreen";
        private const string PrefVsync = "beachlife.vsync";
        private const string PrefShadows = "beachlife.shadows";
        private const string PrefPanSpeed = "beachlife.panspeed";
        private const string PrefClockPace = "beachlife.clockpace";

        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _descStyle;
        private GUIStyle _headlineStyle;

        private void Awake()
        {
            LoadPrefs();
            Open(); // the menu is up whenever the game boots
        }

        private void Start()
        {
            CacheSceneRefs();
            ApplySettings();
        }

        private void CacheSceneRefs()
        {
            _runner = ResortRunner.Instance;
            var camera = Camera.main;
            if (camera != null) _rig = camera.GetComponent<CameraRig>();
            if (_sun == null) _sun = FindObjectOfType<Light>();
        }

        // ---------------------------------------------------------------- open / close

        public void Open()
        {
            Visible = true;
            _screen = MenuScreen.Main;
            _runner = ResortRunner.Instance;
            _lastSpeedIndex = _runner != null ? _runner.SpeedIndex : 1;
            if (_runner != null) _runner.SetSpeed(0); // freeze the world behind the menu
            var controller = BuildController.Instance;
            if (controller != null) controller.SetMode(PointerMode.Inspect);
            CacheSceneRefs();
        }

        public void CloseToGame()
        {
            Visible = false;
            if (_runner == null) return;
            _runner.SetSpeed(_runner.HasSession ? Mathf.Max(1, _lastSpeedIndex) : 1);
        }

        /// <summary>Programmatic entry point used by the UI and the batch smoke test.</summary>
        public static void StartNewGame(Difficulty difficulty)
        {
            var runner = ResortRunner.Instance;
            if (runner == null) return;
            runner.Difficulty = difficulty;
            runner.NewGame();
            var menu = FindObjectOfType<MainMenu>();
            if (menu != null) menu.CloseToGame();
        }

        // ---------------------------------------------------------------- settings

        private void LoadPrefs()
        {
            _fullscreen = PlayerPrefs.GetInt(PrefFullscreen, 1) == 1;
            _vsync = PlayerPrefs.GetInt(PrefVsync, 1) == 1;
            _shadows = PlayerPrefs.GetInt(PrefShadows, 1) == 1;
            _panSpeed = PlayerPrefs.GetFloat(PrefPanSpeed, 18f);
            _clockPace = PlayerPrefs.GetFloat(PrefClockPace, 0.25f);
            _selectedDifficulty = PlayerPrefs.GetInt("beachlife.difficulty", (int)Difficulty.Classic);
        }

        private void ApplySettings()
        {
            PlayerPrefs.SetInt(PrefFullscreen, _fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(PrefVsync, _vsync ? 1 : 0);
            PlayerPrefs.SetInt(PrefShadows, _shadows ? 1 : 0);
            PlayerPrefs.SetFloat(PrefPanSpeed, _panSpeed);
            PlayerPrefs.SetFloat(PrefClockPace, _clockPace);
            PlayerPrefs.SetInt("beachlife.difficulty", _selectedDifficulty);
            PlayerPrefs.Save();

            Screen.fullScreen = _fullscreen;
            QualitySettings.vSyncCount = _vsync ? 1 : 0;
            if (_sun != null) _sun.shadows = _shadows ? LightShadows.Soft : LightShadows.None;
            if (_rig != null) _rig.PanSpeed = _panSpeed;

            var runner = ResortRunner.Instance;
            if (runner == null) return;
            runner.RealSecondsPerGameMinute = _clockPace;
            if (runner.Resort != null) runner.Resort.Config.RealSecondsPerGameMinute = _clockPace;
        }

        private void SetScreen(MenuScreen screen)
        {
            _screen = screen;
            CacheSceneRefs();
        }

        // ---------------------------------------------------------------- drawing

        private void OnGUI()
        {
            if (!Visible) return;
            EnsureStyles();

            GUI.color = new Color(0.04f, 0.09f, 0.14f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float width = 500f;
            float height = _screen == MenuScreen.HowTo ? 560f : 480f;
            var rect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            GUI.Box(rect, GUIContent.none);

            GUILayout.BeginArea(new Rect(rect.x + 24f, rect.y + 20f, width - 48f, height - 40f));
            switch (_screen)
            {
                case MenuScreen.Main: DrawMain(); break;
                case MenuScreen.NewGame: DrawNewGame(); break;
                case MenuScreen.Settings: DrawSettings(); break;
                case MenuScreen.HowTo: DrawHowTo(); break;
            }
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.BoldAndItalic,
                alignment = TextAnchor.MiddleCenter,
            };
            _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
            _headlineStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15, fixedHeight = 34f };
            _descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
            };
            _descStyle.normal.textColor = new Color(0.85f, 0.87f, 0.9f);
        }

        private void DrawMain()
        {
            GUILayout.Label("BEACH LIFE", _titleStyle);
            GUILayout.Label("Build the resort. Keep the guests happy. Stay out of the red.",
                _descStyle);
            GUILayout.Space(18f);

            var runner = ResortRunner.Instance;
            if (runner != null && runner.HasSession
                && GUILayout.Button("Continue", _buttonStyle))
                CloseToGame();

            if (GUILayout.Button("New game", _buttonStyle)) SetScreen(MenuScreen.NewGame);
            if (GUILayout.Button("Settings", _buttonStyle)) SetScreen(MenuScreen.Settings);
            if (GUILayout.Button("How to play", _buttonStyle)) SetScreen(MenuScreen.HowTo);
            GUILayout.Space(10f);
            if (GUILayout.Button("Quit", _buttonStyle)) Quit();
        }

        private void DrawNewGame()
        {
            GUILayout.Label("Choose a level", _headlineStyle);
            GUILayout.Space(6f);

            foreach (var preset in DifficultyTable.All)
            {
                bool selected = _selectedDifficulty == (int)preset.Level;
                var rowStyle = new GUIStyle(GUI.skin.button)
                {
                    fixedHeight = 74f,
                    fontSize = 14,
                    alignment = TextAnchor.UpperLeft,
                };
                if (GUILayout.Button("", rowStyle))
                {
                    _selectedDifficulty = (int)preset.Level;
                    ApplySettings();
                }

                // Overlay the text on the row so the selection stays readable.
                var last = GUILayoutUtility.GetLastRect();
                var textRect = new Rect(last.x + 14f, last.y + 8f, last.width - 28f, last.height - 12f);
                GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, 20f),
                    preset.DisplayName + (selected ? "  <" : ""), _headlineStyle);
                GUI.Label(new Rect(textRect.x, textRect.y + 20f, textRect.width, textRect.height - 20f),
                    preset.Description, _descStyle);
            }

            var chosen = DifficultyTable.Get((Difficulty)_selectedDifficulty);
            GUILayout.Space(4f);
            GUILayout.Label(
                $"Start with ${chosen.StartingCash:n0}, reputation {chosen.StartingReputation}, "
                + $"{chosen.BankruptcyGraceDays} days of bank patience.", _descStyle);
            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start", _buttonStyle))
                StartNewGame((Difficulty)_selectedDifficulty);
            if (GUILayout.Button("Back", _buttonStyle)) SetScreen(MenuScreen.Main);
            GUILayout.EndHorizontal();
        }

        private void DrawSettings()
        {
            GUILayout.Label("Settings", _headlineStyle);
            GUILayout.Space(6f);

            bool fullscreen = GUILayout.Toggle(_fullscreen, "  Fullscreen");
            if (fullscreen != _fullscreen) { _fullscreen = fullscreen; ApplySettings(); }

            bool vsync = GUILayout.Toggle(_vsync, "  VSync");
            if (vsync != _vsync) { _vsync = vsync; ApplySettings(); }

            bool shadows = GUILayout.Toggle(_shadows, "  Shadows");
            if (shadows != _shadows) { _shadows = shadows; ApplySettings(); }

            GUILayout.Space(10f);
            GUILayout.Label($"Camera pan speed: {_panSpeed:0}");
            float pan = GUILayout.HorizontalSlider(_panSpeed, 6f, 40f);
            if ((int)(pan * 2f) != (int)(_panSpeed * 2f)) { _panSpeed = pan; ApplySettings(); }

            float multiplier = 0.25f / _clockPace;
            GUILayout.Label($"Game clock speed: x{multiplier:0.0}");
            float pace = GUILayout.HorizontalSlider(_clockPace, 0.1f, 0.6f);
            if ((int)(pace * 100f) != (int)(_clockPace * 100f)) { _clockPace = pace; ApplySettings(); }
            GUILayout.Label("Higher values run the day faster. Saved automatically.", _descStyle);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Back", _buttonStyle)) SetScreen(MenuScreen.Main);
        }

        private void DrawHowTo()
        {
            GUILayout.Label("How to play", _headlineStyle);
            GUILayout.Space(6f);
            _howToScroll = GUILayout.BeginScrollView(_howToScroll);
            GUILayout.Label(
                "Guests arrive from the road at the top of the plot with seven needs: hunger, thirst, "
                + "bladder, hygiene, fun, energy and tan. Build venues that satisfy them, set prices in "
                + "the inspector, and their mood feeds your reputation - which brings more guests tomorrow.\n\n"
                + "Litter piles up where guests wander; bins nearby reduce it. Hire cleaners to fight "
                + "litter and dirty venues, and assign attendants to busy venues for extra capacity.\n\n"
                + "Some guests book a hotel stay on arrival: at night they check in, pay the nightly rate "
                + "and sleep until 07:30. Keep them fed and entertained for the days they stay.\n\n"
                + "Watch the weather - rain kills tanning and arrivals, heatwaves sell drinks. Marketing "
                + "campaigns boost tomorrow's crowds for a daily fee. Lifeguard towers prevent beach "
                + "accidents. Goals pay cash rewards.\n\n"
                + "If the books stay in the red for the bank's patience period, the resort goes bust.\n\n"
                + "Controls: WASD or middle-drag to pan, Q/E to rotate, wheel to zoom, X for demolish "
                + "mode, Esc to cancel, click a building to inspect and adjust it.", _descStyle);
            GUILayout.EndScrollView();
            GUILayout.Space(8f);
            if (GUILayout.Button("Back", _buttonStyle)) SetScreen(MenuScreen.Main);
        }

        private void Quit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
