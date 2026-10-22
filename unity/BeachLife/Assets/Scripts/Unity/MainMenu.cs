#if UNITY_EDITOR
using UnityEditor;
#endif
using BeachLife.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BeachLife.Unity
{
    /// <summary>
    /// uGUI main menu shown over the live resort panorama: continue, new game with a difficulty
    /// pick, persistent settings, how-to-play and quit. The whole menu is named, editor-editable
    /// nodes under "MenuCanvas"; runtime code only binds behaviour. While it is up the simulation
    /// is paused, the HUD is hidden and the camera slowly orbits.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        public static bool Visible { get; private set; }

        private ResortRunner _runner;
        private CameraRig _rig;
        private Light _sun;
        private Canvas _canvas;

        private RectTransform _mainScreen, _newGameScreen, _settingsScreen, _howToScreen;
        private Button _continueBtn;
        private Button[] _difficultyButtons;
        private Text _difficultySummary;
        private Toggle _fullscreenToggle, _vsyncToggle, _shadowsToggle;
        private Text _panSpeedLabel, _clockLabel;
        private Slider _panSpeedSlider, _clockSlider;

        private int _selectedDifficulty = (int)Difficulty.Classic;
        private int _lastSpeedIndex = 1;

        private bool _fullscreen = true;
        private bool _vsync = true;
        private bool _shadows = true;
        private float _panSpeed = 18f;
        private float _clockPace = 1f;

        private const string PrefFullscreen = "beachlife.fullscreen";
        private const string PrefVsync = "beachlife.vsync";
        private const string PrefShadows = "beachlife.shadows";
        private const string PrefPanSpeed = "beachlife.panspeed";
        private const string PrefClockPace = "beachlife.clockpace";

        private void Awake()
        {
            LoadPrefs();
            Open(); // the menu is up whenever the game boots
        }

        private void Start()
        {
            CacheSceneRefs();
            EnsureUI();
            BindHandlers();
            ApplySettings();
            ShowScreen(_mainScreen); // Open() ran before the hierarchy existed; apply it now
        }

        private void CacheSceneRefs()
        {
            _runner = ResortRunner.Instance;
            var camera = Camera.main;
            if (camera != null) _rig = camera.GetComponent<CameraRig>();
            if (_sun == null) _sun = GameObject.Find("Sun") != null
                ? GameObject.Find("Sun").GetComponent<Light>()
                : FindObjectOfType<Light>();
        }

        // ---------------------------------------------------------------- open / close

        public void Open()
        {
            Visible = true;
            _runner = ResortRunner.Instance;
            _lastSpeedIndex = _runner != null ? _runner.SpeedIndex : 1;
            if (_runner != null) _runner.SetSpeed(0); // freeze the world behind the menu
            var controller = BuildController.Instance;
            if (controller != null) controller.SetMode(PointerMode.Inspect);
            CacheSceneRefs();
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            ShowScreen(_mainScreen);
        }

        public void CloseToGame()
        {
            Visible = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
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
            _clockPace = PlayerPrefs.GetFloat(PrefClockPace, 1f);
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

        // ---------------------------------------------------------------- hierarchy


        public void EnsureUI()
        {
            // Look for the menu's own canvas by name; a sibling HUD canvas must not be reused.
            var menuCanvasTransform = transform.Find("MenuCanvas");
            if (menuCanvasTransform != null) _canvas = menuCanvasTransform.GetComponent<Canvas>();
            if (_canvas == null)
            {
                var go = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.transform.SetParent(transform, false);
                _canvas = go.GetComponent<Canvas>();
            }
            // Overlay keeps the menu on top of the 3D panorama regardless of camera movement.
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 20; // above the HUD canvas
            _canvas.gameObject.SetActive(Visible);

            var root = _canvas.transform;

            // Idempotent build: reuse the saved scene's nodes (so editor restyling survives),
            // only create what is missing.
            var panel = root.Find("MenuPanel") as RectTransform;
            if (panel == null)
            {
                UI.Panel(root.gameObject, "Backdrop", new Color(0.03f, 0.05f, 0.08f, 0.72f),
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                panel = UI.Panel(root.gameObject, "MenuPanel", UI.PanelColor,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-260f, -280f), new Vector2(260f, 280f));
                BuildMenuContent(panel);
            }

            // Resolve references whether the tree was just built or loaded from the scene.
            _mainScreen = panel.Find("MainScreen") as RectTransform;
            _newGameScreen = panel.Find("NewGameScreen") as RectTransform;
            _settingsScreen = panel.Find("SettingsScreen") as RectTransform;
            _howToScreen = panel.Find("HowToScreen") as RectTransform;
            _continueBtn = panel.Find("MainScreen/ContinueBtn").GetComponent<Button>();
            _difficultyButtons = new Button[DifficultyTable.All.Length];
            for (int i = 0; i < _difficultyButtons.Length; i++)
                _difficultyButtons[i] = panel.Find($"NewGameScreen/Diff{i}").GetComponent<Button>();
            _difficultySummary = panel.Find("NewGameScreen/SummaryText").GetComponent<Text>();
            _fullscreenToggle = panel.Find("SettingsScreen/FullscreenToggle").GetComponent<Toggle>();
            _vsyncToggle = panel.Find("SettingsScreen/VsyncToggle").GetComponent<Toggle>();
            _shadowsToggle = panel.Find("SettingsScreen/ShadowsToggle").GetComponent<Toggle>();
            _panSpeedLabel = panel.Find("SettingsScreen/PanSpeedLabel").GetComponent<Text>();
            _clockLabel = panel.Find("SettingsScreen/ClockLabel").GetComponent<Text>();
            _panSpeedSlider = panel.Find("SettingsScreen/PanSpeedSlider").GetComponent<Slider>();
            _clockSlider = panel.Find("SettingsScreen/ClockSlider").GetComponent<Slider>();
            // A saved scene keeps the old slider range; re-apply the current tuning.
            _clockSlider.minValue = 0.5f;
            _clockSlider.maxValue = 2f;
            if (_clockPace < 0.5f || _clockPace > 2f) _clockPace = 1f;

            UI.RepairGraphics(_canvas.gameObject);
            ShowScreen(_mainScreen); // also correct for edit-mode scene creation
        }

        private void BuildMenuContent(RectTransform panel)
        {

            var title = UI.Label(panel.gameObject, "TitleText", "BEACH LIFE", 40,
                TextAnchor.MiddleCenter, UI.AccentColor);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(0f, -70f);
            titleRt.offsetMax = new Vector2(0f, -8f);

            var subtitle = UI.Label(panel.gameObject, "SubtitleText",
                "Build the resort. Keep the guests happy. Stay out of the red.", 13,
                TextAnchor.MiddleCenter, UI.MutedColor);
            var subRt = subtitle.rectTransform;
            subRt.anchorMin = new Vector2(0f, 1f);
            subRt.anchorMax = new Vector2(1f, 1f);
            subRt.offsetMin = new Vector2(0f, -92f);
            subRt.offsetMax = new Vector2(0f, -70f);

            _mainScreen = UI.Panel(panel.gameObject, "MainScreen", new Color(0f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one, new Vector2(40f, 20f), new Vector2(-40f, -100f));
            _mainScreen.GetComponent<Image>().raycastTarget = false;

            _continueBtn = UI.Button(_mainScreen.gameObject, "ContinueBtn", "Continue", null,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -60f), Vector2.zero, 15);
            UI.Button(_mainScreen.gameObject, "NewGameBtn", "New game", null,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -106f), new Vector2(0f, -66f), 15);
            UI.Button(_mainScreen.gameObject, "SettingsBtn", "Settings", null,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -152f), new Vector2(0f, -112f), 15);
            UI.Button(_mainScreen.gameObject, "HowToBtn", "How to play", null,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -198f), new Vector2(0f, -158f), 15);
            UI.Button(_mainScreen.gameObject, "QuitBtn", "Quit", null,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -252f), new Vector2(0f, -212f), 15);

            BuildNewGameScreen(panel);
            BuildSettingsScreen(panel);
            BuildHowToScreen(panel);
        }

        private void BuildNewGameScreen(RectTransform panel)
        {
            _newGameScreen = UI.Panel(panel.gameObject, "NewGameScreen", new Color(0f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one, new Vector2(30f, 16f), new Vector2(-30f, -100f));
            _newGameScreen.GetComponent<Image>().raycastTarget = false;

            UI.Label(_newGameScreen.gameObject, "Header", "Choose a level", 17, TextAnchor.MiddleLeft, UI.AccentColor)
                .rectTransform.sizeDelta = new Vector2(0f, 26f);

            if (_difficultyButtons == null || _difficultyButtons.Length != DifficultyTable.All.Length)
            {
                _difficultyButtons = new Button[DifficultyTable.All.Length];
                for (int i = 0; i < DifficultyTable.All.Length; i++)
                {
                    var preset = DifficultyTable.All[i];
                    var row = UI.Button(_newGameScreen.gameObject, $"Diff{i}", "", null,
                        new Vector2(0f, 1f), new Vector2(1f, 1f),
                        new Vector2(0f, -36f - i * 82f), new Vector2(0f, -36f - i * 82f + 76f), 14);
                    UI.Label(row.gameObject, "NameText", preset.DisplayName, 15, TextAnchor.UpperLeft, UI.AccentColor)
                        .rectTransform.sizeDelta = new Vector2(0f, 22f);
                    var nameText = row.GetComponentInChildren<Text>();
                    nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
                    nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
                    nameText.rectTransform.offsetMin = new Vector2(14f, -26f);
                    nameText.rectTransform.offsetMax = new Vector2(-14f, -4f);

                    var desc = UI.Label(row.gameObject, "DescText", preset.Description, 12,
                        TextAnchor.UpperLeft, UI.MutedColor);
                    desc.rectTransform.anchorMin = new Vector2(0f, 0f);
                    desc.rectTransform.anchorMax = new Vector2(1f, 1f);
                    desc.rectTransform.offsetMin = new Vector2(14f, 6f);
                    desc.rectTransform.offsetMax = new Vector2(-14f, -26f);

                    var captured = preset.Level;
                    row.onClick.AddListener(() => SelectDifficulty(captured));
                    _difficultyButtons[i] = row;
                }
            }

            _difficultySummary = UI.Label(_newGameScreen.gameObject, "SummaryText", "", 12,
                TextAnchor.UpperLeft, UI.MutedColor);
            var summaryRt = _difficultySummary.rectTransform;
            summaryRt.anchorMin = new Vector2(0f, 1f);
            summaryRt.anchorMax = new Vector2(1f, 1f);
            summaryRt.offsetMin = new Vector2(0f, -290f);
            summaryRt.offsetMax = new Vector2(0f, -252f);

            UI.Button(_newGameScreen.gameObject, "StartBtn", "Start", null,
                new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -352f), new Vector2(-6f, -316f), 15);
            UI.Button(_newGameScreen.gameObject, "BackBtn", "Back", null,
                new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(6f, -352f), new Vector2(0f, -316f), 15);
        }

        private void BuildSettingsScreen(RectTransform panel)
        {
            _settingsScreen = UI.Panel(panel.gameObject, "SettingsScreen", new Color(0f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one, new Vector2(40f, 16f), new Vector2(-40f, -100f));
            _settingsScreen.GetComponent<Image>().raycastTarget = false;

            UI.Label(_settingsScreen.gameObject, "Header", "Settings", 17, TextAnchor.MiddleLeft, UI.AccentColor)
                .rectTransform.sizeDelta = new Vector2(0f, 26f);

            _fullscreenToggle = UI.Toggle(_settingsScreen.gameObject, "FullscreenToggle", "  Fullscreen", _fullscreen, null,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -56f), new Vector2(0f, -28f));
            _vsyncToggle = UI.Toggle(_settingsScreen.gameObject, "VsyncToggle", "  VSync", _vsync, null,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -90f), new Vector2(0f, -62f));
            _shadowsToggle = UI.Toggle(_settingsScreen.gameObject, "ShadowsToggle", "  Shadows", _shadows, null,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -124f), new Vector2(0f, -96f));

            _panSpeedLabel = UI.Label(_settingsScreen.gameObject, "PanSpeedLabel", "", 13);
            _panSpeedLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            _panSpeedLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _panSpeedLabel.rectTransform.offsetMin = new Vector2(0f, -158f);
            _panSpeedLabel.rectTransform.offsetMax = new Vector2(0f, -132f);
            _panSpeedSlider = UI.Slider(_settingsScreen.gameObject, "PanSpeedSlider", 6f, 40f, _panSpeed, null,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -188f), new Vector2(0f, -160f));

            _clockLabel = UI.Label(_settingsScreen.gameObject, "ClockLabel", "", 13);
            _clockLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            _clockLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _clockLabel.rectTransform.offsetMin = new Vector2(0f, -224f);
            _clockLabel.rectTransform.offsetMax = new Vector2(0f, -198f);
            _clockSlider = UI.Slider(_settingsScreen.gameObject, "ClockSlider", 0.5f, 2f, _clockPace, null,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -254f), new Vector2(0f, -226f));

            UI.Label(_settingsScreen.gameObject, "Hint", "Settings apply immediately and are saved automatically.", 11,
                TextAnchor.UpperLeft, UI.MutedColor).rectTransform.sizeDelta = new Vector2(0f, 24f);

            UI.Button(_settingsScreen.gameObject, "BackBtn", "Back", null,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -352f), new Vector2(0f, -316f), 15);
        }

        private void BuildHowToScreen(RectTransform panel)
        {
            _howToScreen = UI.Panel(panel.gameObject, "HowToScreen", new Color(0f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one, new Vector2(30f, 16f), new Vector2(-30f, -100f));
            _howToScreen.GetComponent<Image>().raycastTarget = false;

            UI.Label(_howToScreen.gameObject, "Header", "How to play", 17, TextAnchor.MiddleLeft, UI.AccentColor)
                .rectTransform.sizeDelta = new Vector2(0f, 26f);

            var content = UI.Scroll(_howToScreen.gameObject, "HowToScroll",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -340f), new Vector2(0f, -30f));
            var howToText = UI.Label(content.gameObject, "HowToText", HowToText(), 12, TextAnchor.UpperLeft);
            howToText.rectTransform.offsetMin = new Vector2(6f, 6f);
            howToText.rectTransform.offsetMax = new Vector2(-6f, -6f);

            UI.Button(_howToScreen.gameObject, "BackBtn", "Back", null,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -352f), new Vector2(0f, -316f), 15);
        }

        private static string HowToText() =>
            "Guests arrive from the road at the top of the plot with seven needs: hunger, thirst, " +
            "bladder, hygiene, fun, energy and tan. Build venues that satisfy them, set prices in " +
            "the inspector, and their mood feeds your reputation - which brings more guests tomorrow.\n\n" +
            "Litter piles up where guests wander; bins nearby reduce it. Hire cleaners to fight " +
            "litter and dirty venues, and assign attendants to busy venues for extra capacity.\n\n" +
            "Some guests book a hotel stay on arrival: at night they check in, pay the nightly rate " +
            "and sleep until 07:30. Keep them fed and entertained for the days they stay.\n\n" +
            "Watch the weather - rain kills tanning and arrivals, heatwaves sell drinks. Marketing " +
            "campaigns boost tomorrow's crowds for a daily fee. Lifeguard towers prevent beach " +
            "accidents. Goals pay cash rewards.\n\n" +
            "If the books stay in the red for the bank's patience period, the resort goes bust.\n\n" +
            "Controls: WASD or middle-drag to pan, Q/E to rotate, right-drag to look around, " +
            "wheel to zoom, X for demolish mode, Esc to cancel, click a building to inspect and adjust it.";

        // ---------------------------------------------------------------- behaviour

        private void BindHandlers()
        {
            Bind(_mainScreen, "ContinueBtn", CloseToGame);
            Bind(_mainScreen, "NewGameBtn", () => ShowScreen(_newGameScreen));
            Bind(_mainScreen, "SettingsBtn", () => ShowScreen(_settingsScreen));
            Bind(_mainScreen, "HowToBtn", () => ShowScreen(_howToScreen));
            Bind(_mainScreen, "QuitBtn", Quit);

            Bind(_newGameScreen, "StartBtn", () => StartNewGame((Difficulty)_selectedDifficulty));
            Bind(_newGameScreen, "BackBtn", () => ShowScreen(_mainScreen));

            Bind(_settingsScreen, "BackBtn", () => ShowScreen(_mainScreen));
            _fullscreenToggle.onValueChanged.RemoveAllListeners();
            _fullscreenToggle.onValueChanged.AddListener(v => { _fullscreen = v; ApplySettings(); });
            _vsyncToggle.onValueChanged.RemoveAllListeners();
            _vsyncToggle.onValueChanged.AddListener(v => { _vsync = v; ApplySettings(); });
            _shadowsToggle.onValueChanged.RemoveAllListeners();
            _shadowsToggle.onValueChanged.AddListener(v => { _shadows = v; ApplySettings(); });
            _panSpeedSlider.onValueChanged.RemoveAllListeners();
            _panSpeedSlider.onValueChanged.AddListener(v => { _panSpeed = v; ApplySettings(); RefreshSettingLabels(); });
            _clockSlider.onValueChanged.RemoveAllListeners();
            _clockSlider.onValueChanged.AddListener(v => { _clockPace = v; ApplySettings(); RefreshSettingLabels(); });

            Bind(_howToScreen, "BackBtn", () => ShowScreen(_mainScreen));
            RefreshSettingLabels();
            RefreshDifficultyHighlight();
        }

        private static void Bind(RectTransform screen, string buttonName, System.Action action)
        {
            var button = screen.Find(buttonName) != null
                ? screen.Find(buttonName).GetComponent<Button>()
                : null;
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action());
        }

        private void ShowScreen(RectTransform screen)
        {
            if (_mainScreen == null || screen == null) return; // hierarchy not built yet (Awake-time Open)
            _mainScreen.gameObject.SetActive(screen == _mainScreen);
            _newGameScreen.gameObject.SetActive(screen == _newGameScreen);
            _settingsScreen.gameObject.SetActive(screen == _settingsScreen);
            _howToScreen.gameObject.SetActive(screen == _howToScreen);
            if (screen == _newGameScreen) RefreshDifficultyHighlight();
            if (screen == _settingsScreen) RefreshSettingLabels();
        }

        private void SelectDifficulty(Difficulty level)
        {
            _selectedDifficulty = (int)level;
            ApplySettings();
            RefreshDifficultyHighlight();
        }

        private void RefreshDifficultyHighlight()
        {
            var chosen = DifficultyTable.Get((Difficulty)_selectedDifficulty);
            if (_difficultySummary != null)
                _difficultySummary.text =
                    $"Start with ${chosen.StartingCash:n0}, reputation {chosen.StartingReputation}, " +
                    $"{chosen.BankruptcyGraceDays} days of bank patience.";
            if (_difficultyButtons == null) return;
            for (int i = 0; i < _difficultyButtons.Length; i++)
                _difficultyButtons[i].GetComponent<Image>().color =
                    i == _selectedDifficulty ? UI.AccentColor : UI.ButtonColor;
        }

        private void RefreshSettingLabels()
        {
            if (_panSpeedLabel != null) _panSpeedLabel.text = $"Camera pan speed: {_panSpeed:0}";
            if (_clockLabel != null)
            {
                float multiplier = 1f / _clockPace;
                _clockLabel.text = $"Game clock speed: x{multiplier:0.0}";
            }
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
