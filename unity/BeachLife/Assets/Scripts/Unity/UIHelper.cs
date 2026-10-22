using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BeachLife.Unity
{
    /// <summary>
    /// Factory for the code-built uGUI hierarchy. Every node it creates is a named,
    /// editor-editable GameObject: the saved Resort.unity scene contains the whole UI, so
    /// buttons, panels and labels can be restyled in the Unity editor. Runtime code finds
    /// them by name and only rebinds behaviour.
    /// </summary>
    internal static class UI
    {
        public static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.13f, 0.88f);
        public static readonly Color ButtonColor = new Color(0.16f, 0.19f, 0.25f, 0.95f);
        public static readonly Color AccentColor = new Color(1f, 0.85f, 0.4f);
        public static readonly Color MutedColor = new Color(0.82f, 0.85f, 0.9f);

        private static Sprite _whiteSprite;
        private static Font _font;

        public static Font Font =>
            _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite != null) return _whiteSprite;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "UIWhite" };
                var pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply();
                _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                return _whiteSprite;
            }
        }

        // ---------------------------------------------------------------- layout

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Creates a positioned, image-backed node. Anchors are normalized; offsets in pixels.</summary>
        public static RectTransform Panel(GameObject parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent.transform, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var image = go.GetComponent<Image>();
            image.sprite = WhiteSprite;
            image.color = color;
            image.raycastTarget = true;
            return rt;
        }

        public static Text Label(GameObject parent, string name, string content, int size,
            TextAnchor align = TextAnchor.MiddleLeft, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent.transform, false);
            Stretch(rt);
            var text = go.AddComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = size;
            text.alignment = align;
            text.color = color ?? Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button Button(GameObject parent, string name, string label, UnityAction onClick,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, int fontSize = 14)
        {
            var rt = Panel(parent, name, ButtonColor, anchorMin, anchorMax, offsetMin, offsetMax);
            var button = rt.gameObject.GetComponent<Button>();
            if (button == null) button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var text = Label(rt.gameObject, "Label", label, fontSize, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        public static Toggle Toggle(GameObject parent, string name, string label, bool value, UnityAction<bool> onChanged,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, int fontSize = 14)
        {
            var rt = Panel(parent, name, new Color(0f, 0f, 0f, 0f), anchorMin, anchorMax, offsetMin, offsetMax);
            rt.GetComponent<Image>().raycastTarget = true;
            var toggle = rt.gameObject.AddComponent<Toggle>();
            toggle.transition = Selectable.Transition.ColorTint;

            var checkArea = Panel(rt.gameObject, "CheckArea", new Color(0f, 0f, 0f, 0f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, -11f), new Vector2(28f, 11f));
            var checkImage = checkArea.GetComponent<Image>();
            checkImage.sprite = WhiteSprite;
            checkImage.color = new Color(0.35f, 0.55f, 0.35f, 0f);
            var checkMark = Panel(checkArea.gameObject, "CheckMark", AccentColor,
                Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            var text = Label(rt.gameObject, "Label", label, fontSize, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.offsetMin = new Vector2(34f, 0f);
            text.rectTransform.offsetMax = Vector2.zero;

            toggle.graphic = checkMark.GetComponent<Image>();
            toggle.isOn = value;
            if (onChanged != null) toggle.onValueChanged.AddListener(onChanged);
            return toggle;
        }

        public static Slider Slider(GameObject parent, string name, float min, float max, float value, UnityAction<float> onChanged,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = Panel(parent, name, new Color(0f, 0f, 0f, 0f), anchorMin, anchorMax, offsetMin, offsetMax);
            rt.GetComponent<Image>().raycastTarget = true;
            var slider = rt.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.ColorTint;

            var track = Panel(rt.gameObject, "Track", new Color(0.25f, 0.28f, 0.34f, 0.95f),
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(4f, -3f), new Vector2(-4f, 3f));
            var fillArea = Panel(rt.gameObject, "FillArea", new Color(0f, 0f, 0f, 0f),
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(4f, -3f), new Vector2(-4f, 3f));
            var fill = Panel(fillArea.gameObject, "Fill", AccentColor,
                new Vector2(0f, 0f), new Vector2(0.6f, 1f), Vector2.zero, Vector2.zero);
            var handleArea = Panel(rt.gameObject, "HandleArea", new Color(0f, 0f, 0f, 0f),
                new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var handle = Panel(handleArea.gameObject, "Handle", Color.white,
                new Vector2(0.6f, 0f), new Vector2(0.6f, 1f), new Vector2(-8f, 0f), new Vector2(8f, 0f));

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        /// <summary>Scroll view with a vertical-layout content; returns the content to fill.</summary>
        public static RectTransform Scroll(GameObject parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var root = Panel(parent, name, new Color(0.05f, 0.06f, 0.09f, 0.7f), anchorMin, anchorMax, offsetMin, offsetMax);
            root.gameObject.AddComponent<RectMask2D>();
            var scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 18f;

            var viewport = Panel(root.gameObject, "Viewport", new Color(0f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-6f, 0f));
            var content = Panel(viewport.gameObject, "Content", new Color(0f, 0f, 0f, 0f),
                new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero);
            content.GetComponent<Image>().raycastTarget = false;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 3f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            return content;
        }

        /// <summary>Fixed-height row inside a vertical layout, with an optional label.</summary>
        public static RectTransform Row(GameObject parent, string name, float height, string label = null, int fontSize = 13)
        {
            var rt = Panel(parent, name, new Color(0f, 0f, 0f, 0f),
                new Vector2(0f, 1f), Vector2.one, Vector2.zero, new Vector2(0f, -height));
            rt.pivot = new Vector2(0.5f, 1f);
            var element = rt.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            if (label != null) Label(rt.gameObject, "Label", label, fontSize, TextAnchor.MiddleLeft);
            return rt;
        }

        /// <summary>Finds a child by path; creates it through <paramref name="build"/> when missing,
        /// so editor-made customizations survive while renamed/deleted nodes are rebuilt.</summary>
        public static RectTransform EnsureNode(GameObject root, string path, System.Func<RectTransform> build)
        {
            var existing = root.transform.Find(path) as RectTransform;
            if (existing != null) return existing;
            build();
            // The builder may return an inner node (e.g. a scroll's content); always hand
            // back the node that actually carries the path name.
            return root.transform.Find(path) as RectTransform;
        }

        /// <summary>
        /// Scroll with a vertical-layout content, reusing a saved scene's tree. Returns the
        /// content node rows must be added to - never the clipping root.
        /// </summary>
        public static RectTransform EnsureScroll(GameObject parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var root = parent.transform.Find(name) as RectTransform;
            if (root == null)
            {
                Scroll(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
                root = parent.transform.Find(name) as RectTransform;
            }
            var content = root.Find("Viewport/Content") as RectTransform;
            return content != null ? content : root;
        }

        /// <summary>
        /// Restores runtime-generated graphics references after loading a saved scene: the
        /// white sprite is a runtime object and cannot be serialized, so every Image saved
        /// in edit mode comes back with a missing sprite and stops rendering.
        /// </summary>
        public static void RepairGraphics(GameObject root)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (image.sprite == null) image.sprite = WhiteSprite;
            foreach (var text in root.GetComponentsInChildren<Text>(true))
                if (text.font == null) text.font = Font;
        }
    }
}
