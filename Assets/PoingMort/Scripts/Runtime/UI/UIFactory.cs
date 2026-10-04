using System;
using System.Collections.Generic;
using PoingMort.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PoingMort.UI
{
    /// <summary>Builds uGUI elements in code with the project's look (dark panels, light text, red accent).</summary>
    public static class UIFactory
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, int radius = 10)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (radius > 0)
            {
                img.sprite = UISprites.RoundedRect(radius);
                img.type = Image.Type.Sliced;
            }
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string text, Font font, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft, string name = "Texte")
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.text = text;
            t.font = font != null ? font : UITheme.BuiltinFont;
            t.fontSize = size;
            t.color = color;
            t.alignment = alignment;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static Shadow AddShadow(Graphic g, float distance = 2f, float alpha = 0.55f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, alpha);
            s.effectDistance = new Vector2(distance, -distance);
            return s;
        }

        /// <summary>Text button of the main menu style: no box, red bar and bright text when selected.</summary>
        public static Button MenuButton(Transform parent, UITheme theme, string label, UnityAction onClick, float width = 420f, float height = 58f, int fontSize = 34)
        {
            var rt = Rect("Bouton " + label, parent);
            rt.sizeDelta = new Vector2(width, height);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            button.onClick.AddListener(() => GameAudio.Ui(true));
            if (onClick != null) button.onClick.AddListener(onClick);

            var bar = Panel(rt, "Accent", theme.accent, 0);
            Place(bar.rectTransform, new Vector2(0f, 0.18f), new Vector2(0f, 0.82f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(5f, 0f));

            var text = Label(rt, label, theme.Body, fontSize, theme.textDim, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform, 22f, 0f, 0f, 0f);
            AddShadow(text, 2f, 0.6f);

            var visual = rt.gameObject.AddComponent<MenuButtonVisual>();
            visual.Setup(button, text, bar, theme);
            AddLayout(rt.gameObject, width, height);
            return button;
        }

        /// <summary>Boxed button used in panels (Retour, Rétablir...).</summary>
        public static Button BoxButton(Transform parent, UITheme theme, string label, UnityAction onClick, float width = 260f, float height = 52f, int fontSize = 24)
        {
            var bg = Panel(parent, "Bouton " + label, theme.panelLight, 8);
            bg.raycastTarget = true;
            var rt = bg.rectTransform;
            rt.sizeDelta = new Vector2(width, height);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.normalColor = theme.panelLight;
            colors.highlightedColor = Color.Lerp(theme.panelLight, theme.accent, 0.45f);
            colors.selectedColor = Color.Lerp(theme.panelLight, theme.accent, 0.6f);
            colors.pressedColor = theme.accent;
            colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => GameAudio.Ui(true));
            if (onClick != null) button.onClick.AddListener(onClick);
            var text = Label(rt, label, theme.Bold, fontSize, theme.text, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 8f, 8f, 0f, 0f);
            AddLayout(rt.gameObject, width, height);
            return button;
        }

        public static LayoutElement AddLayout(GameObject go, float width, float height)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.minHeight = height;
            return le;
        }

        public static RectTransform Row(Transform parent, string name, float height, float width = 760f)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = new Vector2(width, height);
            AddLayout(rt.gameObject, width, height);
            return rt;
        }

        public static VerticalLayoutGroup VerticalStack(RectTransform rt, float spacing, TextAnchor alignment = TextAnchor.UpperLeft, RectOffset padding = null)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = alignment;
            v.childControlWidth = false;
            v.childControlHeight = false;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            if (padding != null) v.padding = padding;
            return v;
        }

        public static Slider Slider(Transform parent, UITheme theme, string label, float min, float max, float value, UnityAction<float> onChange, Func<float, string> format, float width = 760f)
        {
            var row = Row(parent, "Réglage " + label, 56f, width);
            var text = Label(row, label, theme.Body, 26, theme.text, TextAnchor.MiddleLeft);
            Place(text.rectTransform, new Vector2(0f, 0f), new Vector2(0.45f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            var valueText = Label(row, format != null ? format(value) : value.ToString("0.0"), theme.Bold, 24, theme.textDim, TextAnchor.MiddleRight);
            Place(valueText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(110f, 0f));

            var sliderRt = Rect("Glissière", row);
            Place(sliderRt, new Vector2(0.46f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(-125f, 30f));
            var back = Panel(sliderRt, "Fond", theme.healthBack, 6);
            Stretch(back.rectTransform, 0f, 0f, 9f, 9f);
            back.raycastTarget = true;

            var fillArea = Rect("Zone de remplissage", sliderRt);
            Stretch(fillArea, 0f, 0f, 9f, 9f);
            var fill = Panel(fillArea, "Remplissage", theme.accent, 6);
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = Rect("Zone de poignée", sliderRt);
            Stretch(handleArea, 10f, 10f, 0f, 0f);
            var handle = Rect("Poignée", handleArea);
            handle.sizeDelta = new Vector2(26f, 26f);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.sprite = UISprites.Circle(64);
            handleImg.color = theme.text;

            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            var colors = slider.colors;
            colors.highlightedColor = new Color(1f, 0.85f, 0.85f);
            colors.selectedColor = new Color(1f, 0.75f, 0.75f);
            slider.colors = colors;
            slider.onValueChanged.AddListener(v =>
            {
                valueText.text = format != null ? format(v) : v.ToString("0.0");
                onChange?.Invoke(v);
            });
            return slider;
        }

        public static Toggle Toggle(Transform parent, UITheme theme, string label, bool value, UnityAction<bool> onChange, float width = 760f)
        {
            var row = Row(parent, "Option " + label, 56f, width);
            var text = Label(row, label, theme.Body, 26, theme.text, TextAnchor.MiddleLeft);
            Place(text.rectTransform, new Vector2(0f, 0f), new Vector2(0.8f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            var box = Panel(row, "Case", theme.healthBack, 6);
            box.raycastTarget = true;
            Place(box.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            var check = Panel(box.rectTransform, "Coche", theme.accent, 4);
            Stretch(check.rectTransform, 8f, 8f, 8f, 8f);

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = value;
            toggle.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return toggle;
        }

        /// <summary>"Label  &lt; value &gt;" selector, like the rows of the appearance screen.</summary>
        public static Selector Selector(Transform parent, UITheme theme, string label, IList<string> options, int index, UnityAction<int> onChange, float width = 760f)
        {
            var row = Row(parent, "Choix " + label, 56f, width);
            var text = Label(row, label, theme.Body, 26, theme.text, TextAnchor.MiddleLeft);
            Place(text.rectTransform, new Vector2(0f, 0f), new Vector2(0.45f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            var valueText = Label(row, "", theme.Bold, 24, theme.text, TextAnchor.MiddleCenter);
            Place(valueText.rectTransform, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-120f, 0f));

            var selector = row.gameObject.AddComponent<Selector>();
            var prev = BoxButton(row, theme, "<", selector.Previous, 52f, 44f, 26);
            Place((RectTransform)prev.transform, new Vector2(0.46f, 0.5f), new Vector2(0.46f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(52f, 44f));
            var next = BoxButton(row, theme, ">", selector.Next, 52f, 44f, 26);
            Place((RectTransform)next.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(52f, 44f));
            selector.Setup(options, index, valueText, prev, next, onChange);
            return selector;
        }
    }

    /// <summary>Highlight of the main menu text buttons (mouse hover, keyboard/gamepad selection).</summary>
    public sealed class MenuButtonVisual : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        Button m_Button;
        Text m_Text;
        Image m_Bar;
        UITheme m_Theme;
        bool m_Hover, m_Selected;

        public void Setup(Button button, Text text, Image bar, UITheme theme)
        {
            m_Button = button;
            m_Text = text;
            m_Bar = bar;
            m_Theme = theme;
            Refresh();
        }

        public void OnSelect(BaseEventData e)
        {
            if (!m_Selected) GameAudio.Ui(false);
            m_Selected = true;
            Refresh();
        }
        public void OnDeselect(BaseEventData e) { m_Selected = false; Refresh(); }
        public void OnPointerEnter(PointerEventData e)
        {
            m_Hover = true;
            if (m_Button != null && m_Button.interactable && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
            Refresh();
        }
        public void OnPointerExit(PointerEventData e) { m_Hover = false; Refresh(); }

        void OnEnable() => Refresh();

        void Update()
        {
            // Interactable state can change at runtime (e.g. "Continuer").
            if (m_Button != null && m_Text != null) Refresh();
        }

        void Refresh()
        {
            if (m_Text == null || m_Theme == null) return;
            bool enabled = m_Button == null || m_Button.interactable;
            bool active = enabled && (m_Hover || m_Selected);
            m_Text.color = !enabled ? m_Theme.textDisabled : active ? m_Theme.text : m_Theme.textDim;
            if (m_Bar != null) m_Bar.enabled = active;
            transform.localScale = active ? new Vector3(1.03f, 1.03f, 1f) : Vector3.one;
        }
    }

    /// <summary>Cycles through a list of options with two arrow buttons.</summary>
    public sealed class Selector : MonoBehaviour
    {
        IList<string> m_Options;
        Text m_Value;
        UnityAction<int> m_OnChange;
        public int Index { get; private set; }
        public Button PreviousButton { get; private set; }
        public Button NextButton { get; private set; }

        public void Setup(IList<string> options, int index, Text value, Button prev, Button next, UnityAction<int> onChange)
        {
            m_Options = options;
            m_Value = value;
            m_OnChange = onChange;
            PreviousButton = prev;
            NextButton = next;
            Index = Mathf.Clamp(index, 0, Mathf.Max(0, options.Count - 1));
            Refresh();
        }

        public void Previous() => Set(Index - 1);
        public void Next() => Set(Index + 1);

        void Set(int i)
        {
            if (m_Options == null || m_Options.Count == 0) return;
            Index = (i % m_Options.Count + m_Options.Count) % m_Options.Count;
            Refresh();
            m_OnChange?.Invoke(Index);
        }

        void Refresh()
        {
            if (m_Value != null && m_Options != null && m_Options.Count > 0) m_Value.text = m_Options[Index];
        }
    }
}
