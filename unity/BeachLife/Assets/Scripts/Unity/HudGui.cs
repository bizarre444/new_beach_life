using System.Collections.Generic;
using BeachLife.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    /// uGUI heads-up display built from code into named, editor-editable nodes under
    /// "UICanvas": top bar, build palette, inspector, log, staff/marketing/goals/save panels,
    /// exit dialog and game-over screen. When the Resort.unity scene already contains the
    /// canvas, existing nodes are reused (restyled in the editor) and only behaviour is bound.
    /// </summary>
    public sealed class HudGui : MonoBehaviour
    {
        public static bool PointerOverUI =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private ResortRunner _runner;
        private Canvas _canvas;
        private readonly Dictionary<BuildingDef, Button> _paletteButtons = new Dictionary<BuildingDef, Button>();
        private readonly Dictionary<BuildingDef, Image> _paletteImages = new Dictionary<BuildingDef, Image>();

        // Node references (resolved by EnsureUI).
        private Text _cashText, _clockText, _weatherText, _guestsText, _repText;
        private Button[] _speedButtons;
        private RectTransform _paletteContent, _paletteTemplate;
        private Text _paletteInfoText;
        private Button _selectBtn, _demolishBtn;
        private Text _inspectorTitle, _inspectorInfo, _priceLabel, _messageText;
        private Slider _priceSlider;
        private Button _attendantBtn, _inspectorDemolishBtn;
        private Text _logText;
        private RectTransform _floatingPanel, _staffContent, _goalsContent, _saveContent;
        private Text _floatingTitle, _marketingInfo;
        private Button[] _campaignButtons;
        private RectTransform _exitDialog;
        private Text _saveLeaveLabel;
        private RectTransform _gameOverPanel;
        private Text _gameOverStats;

        private HudPanel _openPanel = HudPanel.None;
        private bool _confirmExit;
        private int _speedBeforeDialog = 1;
        private float _rowsRefreshTimer;
        private Building _lastSelection;

        private Resort CurrentResort => _runner != null ? _runner.Resort : null;

        private void Start()
        {
            _runner = ResortRunner.Instance;
            EnsureUI();
            BindHandlers();
        }

        private void Update()
        {
            var resort = CurrentResort;
            if (resort == null) return;

            // The main menu canvas sits on top; hide the HUD while it is up.
            if (_canvas != null) _canvas.gameObject.SetActive(!MainMenu.Visible);
            if (MainMenu.Visible) return;

            // Esc leaves the current mode first (BuildController consumes it earlier in the
            // frame); once in select mode it opens the leave-the-resort dialog.
            if (Input.GetKeyDown(KeyCode.Escape) && !_confirmExit && !resort.Bankrupt
                && BuildController.Instance != null && BuildController.Instance.Mode == PointerMode.Inspect)
                OpenExitDialog();

            RefreshTopBar(resort);
            RefreshPalette(resort);
            RefreshInspector(resort);
            RefreshLog(resort);
            RefreshFloatingPanels(resort);
            RefreshDialogs(resort);
        }

        // ---------------------------------------------------------------- hierarchy

        /// <summary>Creates the canvas tree. Safe in edit mode (menu item) and idempotent:
        /// nodes that already exist in the saved scene are reused as-is.</summary>
        public void EnsureUI()
        {
            var canvasGo = EnsureCanvas();
            var root = canvasGo.transform;

            BuildTopBar(root);
            BuildPalette(root);
            BuildInspector(root);
            BuildLog(root);
            BuildFloatingPanel(root);
            BuildExitDialog(root);
            BuildGameOver(root);
            UI.RepairGraphics(canvasGo);
        }

        /// <summary>Binds button/slider behaviour. Called at runtime only; listeners are
        /// re-created every play session, so editor-side restyling is never overwritten.</summary>
        private void BindHandlers()
        {
            var controller = BuildController.Instance;
            if (controller != null)
            {
                _selectBtn.onClick.RemoveAllListeners();
                _selectBtn.onClick.AddListener(() => controller.SetMode(PointerMode.Inspect));
                _demolishBtn.onClick.RemoveAllListeners();
                _demolishBtn.onClick.AddListener(() => controller.SetMode(PointerMode.Demolish));
                _inspectorDemolishBtn.onClick.RemoveAllListeners();
                _inspectorDemolishBtn.onClick.AddListener(() =>
                {
                    var building = controller.SelectedBuilding;
                    var resort = CurrentResort;
                    if (building != null && resort != null && resort.Buildings.ContainsKey(building.Id))
                        resort.Demolish(building.Id);
                });
                _attendantBtn.onClick.RemoveAllListeners();
                _attendantBtn.onClick.AddListener(ToggleAttendant);
            }

            for (int i = 0; i < _speedButtons.Length; i++)
            {
                var index = i;
                _speedButtons[i].onClick.RemoveAllListeners();
                _speedButtons[i].onClick.AddListener(() => _runner.SetSpeed(index));
            }
        }

        private GameObject EnsureCanvas()
        {
            var uiCanvasTransform = transform.Find("UICanvas");
            if (_canvas == null && uiCanvasTransform != null) _canvas = uiCanvasTransform.GetComponent<Canvas>();
            if (_canvas == null)
            {
                var go = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.transform.SetParent(transform, false);
                _canvas = go.GetComponent<Canvas>();
            }

            // Overlay: the interface must always sit on top of the 3D world - with
            // ScreenSpaceCamera near buildings rendered in front of the plane and hid the UI.
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;

            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;

            if (FindObjectOfType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
            return _canvas.gameObject;
        }

        private void BuildTopBar(Transform root)
        {
            var bar = UI.EnsureNode(root.gameObject, "TopBar", () => UI.Panel(
                root.gameObject, "TopBar", UI.PanelColor,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -42f), Vector2.zero));

            _cashText = EnsureLabel(bar.gameObject, "CashText", 16, UI.AccentColor);
            _clockText = EnsureLabel(bar.gameObject, "ClockText", 14);
            _weatherText = EnsureLabel(bar.gameObject, "WeatherText", 14);
            _guestsText = EnsureLabel(bar.gameObject, "GuestsText", 14);
            _repText = EnsureLabel(bar.gameObject, "RepText", 14);

            if (_speedButtons == null || _speedButtons.Length != 4)
            {
                _speedButtons = new Button[4];
                for (int i = 0; i < 4; i++)
                {
                    int captured = i;
                    string label = i == 0 ? "||" : new string('>', i);
                    var existingSpeed = bar.Find($"Speed{i}") as RectTransform;
                    _speedButtons[i] = existingSpeed != null
                        ? existingSpeed.GetComponent<Button>()
                        : UI.Button(bar.gameObject, $"Speed{i}", label, null,
                            new Vector2(1f, 1f), new Vector2(1f, 1f),
                            new Vector2(-480f + i * 40f, -36f), new Vector2(-444f + i * 40f, -4f), 13);
                }
            }

            var staffBtn = EnsureBarButton(bar.gameObject, "StaffBtn", "Staff", -310f, -242f);
            var adsBtn = EnsureBarButton(bar.gameObject, "AdsBtn", "Ads", -238f, -188f);
            var goalsBtn = EnsureBarButton(bar.gameObject, "GoalsBtn", "Goals", -184f, -124f);
            var saveBtn = EnsureBarButton(bar.gameObject, "SaveBtn", "Save", -120f, -66f);
            var menuBtn = EnsureBarButton(bar.gameObject, "MenuBtn", "Menu", -62f, -6f);

            staffBtn.onClick.RemoveAllListeners();
            staffBtn.onClick.AddListener(() => TogglePanel(HudPanel.Staff));
            adsBtn.onClick.RemoveAllListeners();
            adsBtn.onClick.AddListener(() => TogglePanel(HudPanel.Marketing));
            goalsBtn.onClick.RemoveAllListeners();
            goalsBtn.onClick.AddListener(() => TogglePanel(HudPanel.Goals));
            saveBtn.onClick.RemoveAllListeners();
            saveBtn.onClick.AddListener(() => TogglePanel(HudPanel.SaveLoad));
            menuBtn.onClick.RemoveAllListeners();
            menuBtn.onClick.AddListener(OpenExitDialog);

            PositionTopBarChildren(bar);
        }

        private void PositionTopBarChildren(Transform bar)
        {
            // Left-to-right columns; buttons hug the right edge.
            Anchor(bar, "CashText", 10f, 150f);
            Anchor(bar, "ClockText", 165f, 330f);
            Anchor(bar, "WeatherText", 335f, 530f);
            Anchor(bar, "GuestsText", 535f, 690f);
            Anchor(bar, "RepText", 695f, 790f);
        }

        private static void Anchor(Transform bar, string name, float left, float right)
        {
            var child = bar.Find(name) as RectTransform;
            if (child == null) return;
            child.anchorMin = new Vector2(0f, 0f);
            child.anchorMax = new Vector2(0f, 1f);
            child.offsetMin = new Vector2(left, 0f);
            child.offsetMax = new Vector2(right, 0f);
        }

        private void BuildPalette(Transform root)
        {
            var panel = UI.EnsureNode(root.gameObject, "PalettePanel", () => UI.Panel(
                root.gameObject, "PalettePanel", UI.PanelColor,
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(8f, 215f), new Vector2(258f, -46f)));

            _paletteInfoText = EnsureLabel(panel.gameObject, "Title", 14, UI.MutedColor);
            var title = panel.Find("Title") as RectTransform;
            if (title != null)
            {
                title.anchorMin = new Vector2(0f, 1f);
                title.anchorMax = new Vector2(1f, 1f);
                title.offsetMin = new Vector2(10f, -26f);
                title.offsetMax = new Vector2(-10f, -2f);
            }

            var modeRow = UI.EnsureNode(panel.gameObject, "ModeRow", () =>
                UI.Panel(panel.gameObject, "ModeRow", new Color(0, 0, 0, 0),
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -62f), new Vector2(-8f, -30f)));
            modeRow.GetComponent<Image>().raycastTarget = false;

            var controller = BuildController.Instance;
            _selectBtn = GetOrBuildButton(modeRow.gameObject, "SelectBtn",
                () => UI.Button(modeRow.gameObject, "SelectBtn", "Select", null,
                    new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(-4f, 0f)));
            _demolishBtn = GetOrBuildButton(modeRow.gameObject, "DemolishBtn",
                () => UI.Button(modeRow.gameObject, "DemolishBtn", "Demolish", null,
                    new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(4f, 0f), Vector2.zero));

            _paletteContent = UI.EnsureScroll(panel.gameObject, "BuildingScroll",
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 6f), new Vector2(-6f, -64f));

            _paletteTemplate = UI.EnsureNode(panel.gameObject, "RowTemplate", () =>
            {
                var template = UI.Row(panel.gameObject, "RowTemplate", 30f);
                var btn = UI.Button(template.gameObject, "Button", "Building $0", null,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 13);
                template.gameObject.SetActive(false);
                return btn.transform as RectTransform;
            });
            _paletteTemplate.gameObject.SetActive(false);

            // One real row per catalog entry, styled from the template. Rows are runtime data,
            // so stale ones from a saved scene are removed; the editor-styled template stays.
            for (int i = _paletteContent.childCount - 1; i >= 0; i--)
            {
                var child = _paletteContent.GetChild(i);
                if (child.name.StartsWith("Row_")) Destroy(child.gameObject);
            }
            _paletteButtons.Clear();
            _paletteImages.Clear();
            foreach (var def in BuildingCatalog.All)
            {
                var row = UI.Row(_paletteContent.gameObject, $"Row_{def.Id}", 30f);
                var button = UI.Button(row.gameObject, "Button", $"{def.DisplayName}  ${def.BuildCost:n0}", null,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 13);
                CopyTemplateStyle(button);
                var captured = def;
                button.onClick.AddListener(() => ChooseBuilding(captured));
                _paletteButtons[def] = button;
                _paletteImages[def] = button.GetComponent<Image>();
            }
        }

        private void CopyTemplateStyle(Button button)
        {
            var templateButton = _paletteTemplate.GetComponentInChildren<Button>(true);
            var templateLabel = _paletteTemplate.GetComponentInChildren<Text>(true);
            if (templateButton != null)
                button.GetComponent<Image>().color = templateButton.GetComponent<Image>().color;
            if (templateLabel != null)
            {
                var label = button.GetComponentInChildren<Text>();
                label.font = templateLabel.font;
                label.fontSize = templateLabel.fontSize;
                label.fontStyle = templateLabel.fontStyle;
                label.color = templateLabel.color;
            }
        }

        private void BuildInspector(Transform root)
        {
            var panel = UI.EnsureNode(root.gameObject, "InspectorPanel", () => UI.Panel(
                root.gameObject, "InspectorPanel", UI.PanelColor,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-258f, -366f), new Vector2(-8f, -46f)));

            _inspectorTitle = EnsureLabel(panel.gameObject, "TitleText", 15, UI.AccentColor);
            Anchor(panel, "TitleText", 10f, 240f);
            var title = panel.Find("TitleText") as RectTransform;
            if (title != null)
            {
                title.anchorMin = new Vector2(0f, 1f);
                title.anchorMax = new Vector2(1f, 1f);
                title.offsetMin = new Vector2(10f, -26f);
                title.offsetMax = new Vector2(-10f, -2f);
            }

            var info = UI.EnsureNode(panel.gameObject, "InfoText", () =>
            {
                var text = UI.Label(panel.gameObject, "InfoText", "", 13, TextAnchor.UpperLeft);
                text.rectTransform.anchorMin = new Vector2(0f, 0.42f);
                text.rectTransform.anchorMax = new Vector2(1f, 1f);
                text.rectTransform.offsetMin = new Vector2(10f, 4f);
                text.rectTransform.offsetMax = new Vector2(-10f, -28f);
                return text.rectTransform;
            });
            _inspectorInfo = info.GetComponent<Text>() ?? info.gameObject.GetComponent<Text>();
            if (_inspectorInfo == null) _inspectorInfo = info.GetComponentInChildren<Text>();

            _priceLabel = EnsureLabel(panel.gameObject, "PriceLabel", 13);
            var priceLabelRt = panel.Find("PriceLabel") as RectTransform;
            if (priceLabelRt != null)
            {
                priceLabelRt.anchorMin = new Vector2(0f, 0.32f);
                priceLabelRt.anchorMax = new Vector2(1f, 0.42f);
                priceLabelRt.offsetMin = new Vector2(10f, 0f);
                priceLabelRt.offsetMax = new Vector2(-10f, 0f);
            }

            _priceSlider = UI.EnsureNode(panel.gameObject, "PriceSlider", () =>
            {
                var slider = UI.Slider(panel.gameObject, "PriceSlider", 0.5f, 2f, 1f, null,
                    new Vector2(0f, 0.24f), new Vector2(1f, 0.32f),
                    new Vector2(10f, 0f), new Vector2(-10f, 0f));
                return slider.transform as RectTransform;
            }).GetComponent<Slider>();

            _attendantBtn = EnsureButton(panel.gameObject, "AttendantBtn", "Assign attendant", 240f, 32f, 44f, 13);
            _inspectorDemolishBtn = EnsureButton(panel.gameObject, "DemolishBtn", "Demolish (50% back)", 240f, 32f, 6f, 13);

            var message = UI.EnsureNode(panel.gameObject, "MessageText", () =>
            {
                var text = UI.Label(panel.gameObject, "MessageText", "", 12, TextAnchor.LowerLeft, UI.MutedColor);
                text.rectTransform.anchorMin = new Vector2(0f, 0f);
                text.rectTransform.anchorMax = new Vector2(1f, 0.06f);
                text.rectTransform.offsetMin = new Vector2(10f, 2f);
                text.rectTransform.offsetMax = new Vector2(-10f, 0f);
                return text.rectTransform;
            });
            _messageText = message.GetComponent<Text>();
            if (_messageText == null) _messageText = message.GetComponentInChildren<Text>();
        }

        private void BuildLog(Transform root)
        {
            var panel = UI.EnsureNode(root.gameObject, "LogPanel", () => UI.Panel(
                root.gameObject, "LogPanel", UI.PanelColor,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(266f, 8f), new Vector2(-266f, 178f)));

            var title = EnsureLabel(panel.gameObject, "Title", 13, UI.MutedColor);
            var titleRt = panel.Find("Title") as RectTransform;
            if (titleRt != null)
            {
                titleRt.anchorMin = new Vector2(0f, 1f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.offsetMin = new Vector2(10f, -22f);
                titleRt.offsetMax = new Vector2(-10f, 0f);
                title.text = "Resort log";
            }

            var content = UI.EnsureScroll(panel.gameObject, "LogScroll",
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 6f), new Vector2(-6f, -26f));
            var existingLog = content.Find("LogText") as RectTransform;
            _logText = existingLog != null
                ? existingLog.GetComponent<Text>()
                : UI.Label(content.gameObject, "LogText", "", 12, TextAnchor.UpperLeft);
            _logText.rectTransform.offsetMin = new Vector2(4f, 4f);
            _logText.rectTransform.offsetMax = new Vector2(-4f, -4f);
            _logText.rectTransform.offsetMin = new Vector2(4f, 4f);
            _logText.rectTransform.offsetMax = new Vector2(-4f, -4f);
        }

        private void BuildFloatingPanel(Transform root)
        {
            _floatingPanel = UI.EnsureNode(root.gameObject, "FloatingPanel", () => UI.Panel(
                root.gameObject, "FloatingPanel", UI.PanelColor,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-200f, -190f), new Vector2(200f, 190f)));
            _floatingPanel.gameObject.SetActive(false);

            _floatingTitle = EnsureLabel(_floatingPanel.gameObject, "TitleText", 15, UI.AccentColor);
            var titleRt = _floatingPanel.Find("TitleText") as RectTransform;
            if (titleRt != null)
            {
                titleRt.anchorMin = new Vector2(0f, 1f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.offsetMin = new Vector2(12f, -30f);
                titleRt.offsetMax = new Vector2(-70f, -2f);
            }

            var close = EnsureButton(_floatingPanel.gameObject, "CloseBtn", "X", 40f, 26f, 0f, 13);
            var closeRt = close.transform as RectTransform;
            if (closeRt != null)
            {
                closeRt.anchorMin = new Vector2(1f, 1f);
                closeRt.anchorMax = new Vector2(1f, 1f);
                closeRt.offsetMin = new Vector2(-44f, -30f);
                closeRt.offsetMax = new Vector2(-8f, -4f);
            }
            close.onClick.RemoveAllListeners();
            close.onClick.AddListener(() => TogglePanel(HudPanel.None));

            _staffContent = UI.EnsureScroll(_floatingPanel.gameObject, "StaffContent",
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 8f), new Vector2(-8f, -36f));
            _goalsContent = UI.EnsureScroll(_floatingPanel.gameObject, "GoalsContent",
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 8f), new Vector2(-8f, -36f));
            _saveContent = UI.EnsureScroll(_floatingPanel.gameObject, "SaveContent",
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 8f), new Vector2(-8f, -36f));

            var marketing = UI.EnsureNode(_floatingPanel.gameObject, "MarketingContent", () =>
                UI.Panel(_floatingPanel.gameObject, "MarketingContent", new Color(0f, 0f, 0f, 0f),
                    new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 8f), new Vector2(-8f, -36f)));
            marketing.GetComponent<Image>().raycastTarget = false;
            _marketingInfo = UI.Label(marketing.gameObject, "Hint", "Ads raise tomorrow's guest arrivals. Billed daily.", 12, TextAnchor.UpperLeft);
            _marketingInfo.rectTransform.anchorMin = new Vector2(0f, 1f);
            _marketingInfo.rectTransform.anchorMax = new Vector2(1f, 1f);
            _marketingInfo.rectTransform.offsetMin = new Vector2(0f, -30f);
            _marketingInfo.rectTransform.offsetMax = Vector2.zero;

            if (_campaignButtons == null || _campaignButtons.Length != 4)
            {
                _campaignButtons = new Button[4];
                for (int i = 0; i < CampaignTable.All.Length; i++)
                {
                    var level = CampaignTable.All[i].Level;
                    _campaignButtons[i] = UI.Button(marketing.gameObject, $"Campaign{i}", CampaignTable.All[i].DisplayName, null,
                        new Vector2(0f, 1f), new Vector2(1f, 1f),
                        new Vector2(0f, -70f - i * 40f), new Vector2(0f, -36f - i * 40f), 13);
                    var captured = level;
                    _campaignButtons[i].onClick.AddListener(() => CurrentResort?.SetCampaign(captured));
                }
            }
        }

        private void BuildExitDialog(Transform root)
        {
            _exitDialog = UI.EnsureNode(root.gameObject, "ExitDialog", () => UI.Panel(
                root.gameObject, "ExitDialog", UI.PanelColor,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-210f, -110f), new Vector2(210f, 110f)));
            _exitDialog.gameObject.SetActive(false);

            var title = EnsureLabel(_exitDialog.gameObject, "TitleText", 16, UI.AccentColor);
            title.text = "Leave the resort?";
            var titleRt = _exitDialog.Find("TitleText") as RectTransform;
            if (titleRt != null)
            {
                titleRt.anchorMin = new Vector2(0f, 1f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.offsetMin = new Vector2(12f, -34f);
                titleRt.offsetMax = new Vector2(-12f, 0f);
            }

            var saveLeave = EnsureButton(_exitDialog.gameObject, "SaveLeaveBtn", "Save and leave", 396f, 36f, 0f, 14);
            var saveRt = saveLeave.transform as RectTransform;
            if (saveRt != null)
            {
                saveRt.anchorMin = new Vector2(0.5f, 0.5f);
                saveRt.anchorMax = new Vector2(0.5f, 0.5f);
                saveRt.offsetMin = new Vector2(-198f, 12f);
                saveRt.offsetMax = new Vector2(198f, 48f);
            }
            _saveLeaveLabel = saveLeave.GetComponentInChildren<Text>();

            var leave = EnsureButton(_exitDialog.gameObject, "LeaveBtn", "Leave without saving", 396f, 36f, 0f, 14);
            var leaveRt = leave.transform as RectTransform;
            if (leaveRt != null)
            {
                leaveRt.anchorMin = new Vector2(0.5f, 0.5f);
                leaveRt.anchorMax = new Vector2(0.5f, 0.5f);
                leaveRt.offsetMin = new Vector2(-198f, -30f);
                leaveRt.offsetMax = new Vector2(198f, 6f);
            }
            leave.onClick.RemoveAllListeners();
            leave.onClick.AddListener(() =>
            {
                _confirmExit = false;
                ReturnToMenu();
            });

            var cancel = EnsureButton(_exitDialog.gameObject, "CancelBtn", "Cancel", 396f, 36f, 0f, 14);
            var cancelRt = cancel.transform as RectTransform;
            if (cancelRt != null)
            {
                cancelRt.anchorMin = new Vector2(0.5f, 0.5f);
                cancelRt.anchorMax = new Vector2(0.5f, 0.5f);
                cancelRt.offsetMin = new Vector2(-198f, -72f);
                cancelRt.offsetMax = new Vector2(198f, -36f);
            }
            cancel.onClick.RemoveAllListeners();
            cancel.onClick.AddListener(() =>
            {
                _confirmExit = false;
                _exitDialog.gameObject.SetActive(false);
                _runner.SetSpeed(_speedBeforeDialog);
            });
            saveLeave.onClick.RemoveAllListeners();
            saveLeave.onClick.AddListener(OnSaveAndLeave);
        }

        private void BuildGameOver(Transform root)
        {
            _gameOverPanel = UI.EnsureNode(root.gameObject, "GameOverPanel", () => UI.Panel(
                root.gameObject, "GameOverPanel", new Color(0.03f, 0.05f, 0.08f, 0.92f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero));
            _gameOverPanel.gameObject.SetActive(false);

            var title = EnsureLabel(_gameOverPanel.gameObject, "TitleText", 40, UI.AccentColor);
            title.text = "BANKRUPT";
            title.alignment = TextAnchor.MiddleCenter;
            var titleRt = _gameOverPanel.Find("TitleText") as RectTransform;
            if (titleRt != null)
            {
                titleRt.anchorMin = new Vector2(0.5f, 0.5f);
                titleRt.anchorMax = new Vector2(0.5f, 0.5f);
                titleRt.offsetMin = new Vector2(-200f, 120f);
                titleRt.offsetMax = new Vector2(200f, 180f);
            }

            var stats = UI.Label(_gameOverPanel.gameObject, "StatsText", "", 15, TextAnchor.UpperCenter);
            var statsRt = stats.rectTransform;
            statsRt.anchorMin = new Vector2(0.5f, 0.5f);
            statsRt.anchorMax = new Vector2(0.5f, 0.5f);
            statsRt.offsetMin = new Vector2(-260f, -20f);
            statsRt.offsetMax = new Vector2(260f, 100f);
            _gameOverStats = stats;

            var menu = EnsureButton(_gameOverPanel.gameObject, "MenuBtn", "Back to main menu", 320f, 44f, 0f, 16);
            var menuRt = menu.transform as RectTransform;
            if (menuRt != null)
            {
                menuRt.anchorMin = new Vector2(0.5f, 0.5f);
                menuRt.anchorMax = new Vector2(0.5f, 0.5f);
                menuRt.offsetMin = new Vector2(-160f, -90f);
                menuRt.offsetMax = new Vector2(160f, -46f);
            }
            menu.onClick.RemoveAllListeners();
            menu.onClick.AddListener(() =>
            {
                _runner.ClearSession();
                var mainMenu = FindObjectOfType<MainMenu>();
                if (mainMenu != null) mainMenu.Open();
            });
        }

        // ---------------------------------------------------------------- helpers

        private Button GetOrBuildButton(GameObject parent, string name, System.Func<Button> build)
        {
            var existing = parent.transform.Find(name) as RectTransform;
            if (existing == null) return build();
            return existing.GetComponent<Button>();
        }

        private Text EnsureLabel(GameObject parent, string name, int size, Color? color = null)
        {
            var existing = parent.transform.Find(name);
            if (existing != null) return existing.GetComponent<Text>();
            return UI.Label(parent, name, "", size, TextAnchor.MiddleLeft, color);
        }

        /// <summary>Top-bar button: full bar height, positioned by pixel left/right from the right edge.</summary>
        private Button EnsureBarButton(GameObject parent, string name, string label, float left, float right)
        {
            var existing = parent.transform.Find(name) as RectTransform;
            if (existing == null)
            {
                UI.Button(parent, name, label, null,
                    new Vector2(1f, 0f), new Vector2(1f, 1f),
                    new Vector2(left, 3f), new Vector2(right, -3f), 13);
                existing = parent.transform.Find(name) as RectTransform;
            }
            else
            {
                // Normalize layout (scene may hold an older broken rect); colors stay editable.
                existing.anchorMin = new Vector2(1f, 0f);
                existing.anchorMax = new Vector2(1f, 1f);
                existing.offsetMin = new Vector2(left, 3f);
                existing.offsetMax = new Vector2(right, -3f);
            }
            var button = existing.GetComponent<Button>();
            var text = existing.GetComponentInChildren<Text>();
            if (text != null) text.text = label;
            button.onClick.RemoveAllListeners();
            return button;
        }

        private Button EnsureButton(GameObject parent, string name, string label, float width, float height,
            float bottom, int fontSize)
        {
            var existing = parent.transform.Find(name) as RectTransform;
            if (existing == null)
            {
                var created = UI.Button(parent, name, label, null,
                    new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(12f, bottom), new Vector2(-12f, bottom + height), fontSize);
                existing = created.transform as RectTransform;
            }
            var button = existing.GetComponent<Button>();
            var text = existing.GetComponentInChildren<Text>();
            if (text != null) text.text = label;
            button.onClick.RemoveAllListeners();
            return button;
        }

        // ---------------------------------------------------------------- panels & dialogs

        private void TogglePanel(HudPanel panel)
        {
            _openPanel = _openPanel == panel ? HudPanel.None : panel;
            _floatingPanel.gameObject.SetActive(_openPanel != HudPanel.None);
            _rowsRefreshTimer = 0f;
        }

        private void OpenExitDialog()
        {
            _confirmExit = true;
            _speedBeforeDialog = _runner.SpeedIndex;
            _runner.SetSpeed(0);
        }

        private void OnSaveAndLeave()
        {
            var resort = CurrentResort;
            if (resort == null) return;
            int slot = FirstFreeSlot();
            if (SaveManager.Save(resort, slot))
                resort.EventLog.Add($"[{resort.Clock.TimeLabel}] Game saved to slot {slot + 1}");
            _confirmExit = false;
            ReturnToMenu();
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
            _exitDialog.gameObject.SetActive(false);
            _runner.SetSpeed(Mathf.Max(1, _speedBeforeDialog));
            var menu = FindObjectOfType<MainMenu>();
            if (menu != null) menu.Open();
        }

        private void ToggleAttendant()
        {
            var building = BuildController.Instance != null ? BuildController.Instance.SelectedBuilding : null;
            var resort = CurrentResort;
            if (building == null || resort == null || !resort.Buildings.ContainsKey(building.Id)) return;

            var current = resort.AttendantFor(building);
            if (current != null) { current.AssignedBuildingId = -1; return; }

            foreach (var member in resort.StaffMembers)
            {
                if (member.Type != StaffType.Attendant || member.AssignedBuildingId >= 0) continue;
                resort.AssignAttendant(member.Id, building.Id);
                return;
            }
        }

        // ---------------------------------------------------------------- per-frame refresh

        private void RefreshTopBar(Resort resort)
        {
            _cashText.text = $"${resort.Economy.Cash:n0}";
            _clockText.text = resort.Clock.TimeLabel;
            _weatherText.text = $"{resort.Weather.DisplayName}  (next: {WeatherTable.Get(resort.ForecastWeather).DisplayName})";
            _guestsText.text = $"Guests {resort.Tourists.Count}/{resort.TouristCapacity}   Walkouts {resort.TotalWalkouts}";
            _repText.text = $"Rep {resort.Reputation:0}";
            for (int i = 0; i < _speedButtons.Length; i++)
                _speedButtons[i].GetComponent<Image>().color =
                    _runner.SpeedIndex == i ? UI.AccentColor : UI.ButtonColor;
        }

        private void RefreshPalette(Resort resort)
        {
            var controller = BuildController.Instance;
            if (controller == null) return;
            _selectBtn.GetComponent<Image>().color = controller.Mode == PointerMode.Inspect ? UI.AccentColor : UI.ButtonColor;
            _demolishBtn.GetComponent<Image>().color = controller.Mode == PointerMode.Demolish ? UI.AccentColor : UI.ButtonColor;

            foreach (var def in BuildingCatalog.All)
            {
                var button = _paletteButtons[def];
                button.interactable = resort.Economy.CanAfford(def.BuildCost);
                bool selected = controller.Mode == PointerMode.Build && controller.SelectedDef == def;
                _paletteImages[def].color = selected ? UI.AccentColor : UI.ButtonColor;
            }
        }

        private void RefreshInspector(Resort resort)
        {
            var controller = BuildController.Instance;
            if (controller == null) return;
            var building = controller.SelectedBuilding;
            bool showBuilding = building != null && resort.Buildings.ContainsKey(building.Id);

            if (controller.SelectedBuilding != _lastSelection)
            {
                _lastSelection = controller.SelectedBuilding;
                if (showBuilding) _priceSlider.SetValueWithoutNotify(building.PriceMultiplier);
            }

            _priceSlider.interactable = showBuilding;
            if (showBuilding) _priceSlider.SetValueWithoutNotify(building.PriceMultiplier);

            _attendantBtn.gameObject.SetActive(showBuilding);
            _inspectorDemolishBtn.gameObject.SetActive(showBuilding);

            if (showBuilding)
            {
                _inspectorTitle.text = building.Def.DisplayName;
                var attendant = resort.AttendantFor(building);
                _inspectorInfo.text =
                    $"Guests inside: {building.Occupants.Count}/{resort.EffectiveCapacity(building)}\n" +
                    $"Queue: {building.Waiting.Count}   Hotel beds held: {building.LongStayers.Count}\n" +
                    $"Revenue: ${building.Revenue:n0}\n" +
                    $"Upkeep: ${building.Def.DailyUpkeep:n0}/day\n" +
                    $"Dirtiness: {building.Dirtiness * 100f:0}%\n" +
                    (attendant != null ? $"Attendant: {attendant.Name}" : "No attendant assigned");
                _priceLabel.text = $"Price ${building.Price} (x{building.PriceMultiplier:0.00})";
                _attendantBtn.GetComponentInChildren<Text>().text = attendant != null ? "Remove attendant" : "Assign attendant";
            }
            else if (controller.Mode == PointerMode.Build && controller.SelectedDef != null)
            {
                var def = controller.SelectedDef;
                _inspectorTitle.text = def.DisplayName;
                var info = $"Size: {def.Width}x{def.Height}   Capacity: {def.Capacity}\n" +
                           $"Cost: ${def.BuildCost:n0}   Upkeep: ${def.DailyUpkeep:n0}/day\n" +
                           $"Charges guests: ${def.TicketPrice}\n";
                foreach (var pair in def.Satisfies)
                    info += $"  {pair.Key}: +{pair.Value * 100f:0}%\n";
                if (def.AllowedTiles.Count > 0)
                    info += $"Only on: {string.Join(", ", def.AllowedTiles)}";
                _inspectorInfo.text = info;
                _priceLabel.text = "Click the ground to place it";
            }
            else
            {
                _inspectorTitle.text = "Inspector";
                _inspectorInfo.text = "Click a building to inspect it.\n" +
                                      "WASD / middle-drag: pan\nQ / E: rotate\nRight-drag: look around\nWheel: zoom\nX: demolish, Esc: cancel";
                _priceLabel.text = "";
            }

            _messageText.text = controller.LastMessage;
        }

        private void RefreshLog(Resort resort)
        {
            var log = resort.EventLog;
            var builder = new System.Text.StringBuilder();
            for (int i = log.Count - 1; i >= 0 && i > log.Count - 30; i--)
                builder.AppendLine(log[i]);
            _logText.text = builder.ToString();
        }

        private void RefreshFloatingPanels(Resort resort)
        {
            if (_openPanel == HudPanel.None) return;

            _staffContent.gameObject.SetActive(_openPanel == HudPanel.Staff);
            _goalsContent.gameObject.SetActive(_openPanel == HudPanel.Goals);
            _saveContent.gameObject.SetActive(_openPanel == HudPanel.SaveLoad);
            _floatingPanel.Find("MarketingContent").gameObject.SetActive(_openPanel == HudPanel.Marketing);

            _rowsRefreshTimer -= Time.unscaledDeltaTime;
            if (_rowsRefreshTimer > 0f) return;
            _rowsRefreshTimer = 0.5f;

            switch (_openPanel)
            {
                case HudPanel.Staff: RebuildStaffRows(resort); break;
                case HudPanel.Marketing: RebuildMarketingHighlight(); break;
                case HudPanel.Goals: RebuildGoalRows(resort); break;
                case HudPanel.SaveLoad: RebuildSaveRows(resort); break;
            }
        }

        private void RebuildStaffRows(Resort resort)
        {
            _floatingTitle.text = "Staff";
            for (int i = _staffContent.childCount - 1; i >= 0; i--)
                Destroy(_staffContent.GetChild(i).gameObject);

            foreach (var info in StaffTable.All)
            {
                var row = UI.Row(_staffContent.gameObject, $"Hire_{info.Type}", 34f);
                UI.Label(row.gameObject, "Label", $"{info.DisplayName}  ${info.HireFee:n0} + ${info.WagePerDay}/day", 12);
                var btn = UI.Button(row.gameObject, "HireBtn", "Hire", null,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, -12f), new Vector2(-8f, 12f), 12);
                btn.interactable = resort.CanHire(info.Type);
                var captured = info.Type;
                btn.onClick.AddListener(() => CurrentResort?.Hire(captured));
            }

            foreach (var member in resort.StaffMembers)
            {
                var row = UI.Row(_staffContent.gameObject, $"Staff_{member.Id}", 30f);
                string detail = member.Type == StaffType.Attendant
                    ? DescribeAssignment(resort, member)
                    : DescribeWorkState(member);
                UI.Label(row.gameObject, "Label", $"{member.Name}  ({detail})", 12);
                var btn = UI.Button(row.gameObject, "FireBtn", "Fire", null,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-56f, -11f), new Vector2(-8f, 11f), 12);
                var captured = member.Id;
                btn.onClick.AddListener(() => CurrentResort?.Fire(captured));
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

        private void RebuildMarketingHighlight()
        {
            var resort = CurrentResort;
            if (resort == null) return;
            for (int i = 0; i < CampaignTable.All.Length; i++)
            {
                bool active = resort.Campaign == CampaignTable.All[i].Level;
                string effect = CampaignTable.All[i].Level == CampaignLevel.None
                    ? "-"
                    : $"+{Mathf.RoundToInt((CampaignTable.All[i].ArrivalMultiplier - 1f) * 100)}% guests";
                _campaignButtons[i].GetComponentInChildren<Text>().text =
                    CampaignTable.All[i].Level == CampaignLevel.None
                        ? $"{CampaignTable.All[i].DisplayName}"
                        : $"{CampaignTable.All[i].DisplayName}   ${CampaignTable.All[i].CostPerDay:n0}/day   ({effect})";
                _campaignButtons[i].GetComponent<Image>().color = active ? UI.AccentColor : UI.ButtonColor;
            }
        }

        private void RebuildGoalRows(Resort resort)
        {
            _floatingTitle.text = "Goals";
            for (int i = _goalsContent.childCount - 1; i >= 0; i--)
                Destroy(_goalsContent.GetChild(i).gameObject);

            foreach (var def in GoalTable.All)
            {
                bool done = resort.Goals.IsAchieved(def.Id);
                var row = UI.Row(_goalsContent.gameObject, $"Goal_{def.Id}", 26f);
                UI.Label(row.gameObject, "Label",
                    $"{(done ? "[x] " : "[  ] ")}{def.DisplayName}  (+${def.RewardCash:n0})", 12,
                    TextAnchor.MiddleLeft, done ? new Color(0.4f, 0.85f, 0.4f) : Color.white);
            }
        }

        private void RebuildSaveRows(Resort resort)
        {
            _floatingTitle.text = "Save / Load";
            for (int i = _saveContent.childCount - 1; i >= 0; i--)
                Destroy(_saveContent.GetChild(i).gameObject);

            UI.Label(_saveContent.gameObject, "Path", $"Files: {Application.persistentDataPath}", 11, TextAnchor.UpperLeft, UI.MutedColor)
                .rectTransform.sizeDelta = new Vector2(0f, 30f);

            for (int slot = 0; slot < SaveManager.SlotCount; slot++)
            {
                int captured = slot;
                var row = UI.Row(_saveContent.gameObject, $"Slot{slot}", 34f);
                bool hasSave = SaveManager.HasSave(slot);
                UI.Label(row.gameObject, "Label",
                    $"Slot {slot + 1}: {(hasSave ? SaveManager.SlotLabel(slot) : "(empty)")}", 12);
                var saveBtn = UI.Button(row.gameObject, "SaveBtn", "Save", null,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-102f, -12f), new Vector2(-56f, 12f), 12);
                saveBtn.onClick.AddListener(() =>
                {
                    var current = CurrentResort;
                    if (current != null && SaveManager.Save(current, captured))
                        current.EventLog.Add($"[{current.Clock.TimeLabel}] Game saved to slot {captured + 1}");
                    _rowsRefreshTimer = 0f;
                });
                var loadBtn = UI.Button(row.gameObject, "LoadBtn", "Load", null,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-52f, -12f), new Vector2(-8f, 12f), 12);
                loadBtn.interactable = hasSave;
                loadBtn.onClick.AddListener(() => LoadSlot(captured));
            }
        }

        private void LoadSlot(int slot)
        {
            var snapshot = SaveManager.Load(slot);
            var resort = CurrentResort;
            if (snapshot == null || resort == null) return;
            try
            {
                resort.ApplySnapshot(snapshot);
                _runner.MarkSessionStarted();
                resort.EventLog.Add($"[{resort.Clock.TimeLabel}] Game loaded from slot {slot + 1}");
                TogglePanel(HudPanel.None);
            }
            catch (System.ArgumentException e)
            {
                Debug.LogError($"BeachLife: load failed - {e.Message}");
                resort.EventLog.Add($"Broken save in slot {slot + 1}: {e.Message}");
            }
        }

        private void RefreshDialogs(Resort resort)
        {
            _gameOverPanel.gameObject.SetActive(resort.Bankrupt);
            if (resort.Bankrupt)
            {
                _gameOverStats.text =
                    $"The books stayed in the red for {resort.Config.BankruptcyGraceDays} days and the bank called it quits.\n\n" +
                    $"Days survived: {resort.Clock.Day}      Total guests: {resort.TotalVisitors:n0}\n" +
                    $"Angry walkouts: {resort.TotalWalkouts:n0}      Final reputation: {resort.Reputation:0}";
            }

            if (_confirmExit)
            {
                _exitDialog.gameObject.SetActive(true);
                int slot = FirstFreeSlot();
                string slotLabel = SaveManager.HasSave(slot) ? SaveManager.SlotLabel(slot) : "empty";
                if (_saveLeaveLabel != null)
                    _saveLeaveLabel.text = $"Save and leave  (slot {slot + 1}: {slotLabel})";
            }
        }

        private void ChooseBuilding(BuildingDef def)
        {
            var controller = BuildController.Instance;
            if (controller == null) return;
            controller.ChooseBuilding(def);
        }
    }
}
