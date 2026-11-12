using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Interfaccia costruita a runtime: nessun prefab o immagine esterna da collegare.
public static class MushroomUI
{
    public static readonly Color Ink = new Color(0.10f, 0.20f, 0.17f);
    public static readonly Color Paper = new Color(0.97f, 0.95f, 0.88f);
    public static readonly Color Green = new Color(0.19f, 0.37f, 0.27f);
    public static TMP_FontAsset Font;

    public static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Fill(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
    }

    public static RectTransform Canvas(string name)
    {
        RectTransform root = Rect(name, null);
        Canvas canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();
        return root;
    }

    public static void EnsureEventSystem()
    {
        EventSystem events = Object.FindFirstObjectByType<EventSystem>();
        if (events == null) events = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        StandaloneInputModule old = events.GetComponent<StandaloneInputModule>();
        if (old != null) old.enabled = false;
        InputSystemUIInputModule module = events.GetComponent<InputSystemUIInputModule>();
        if (module == null) module = events.gameObject.AddComponent<InputSystemUIInputModule>();
        module.enabled = true;
        if (module.actionsAsset == null) module.AssignDefaultActions();
    }

    public static Image Image(string name, Transform parent, Color color)
    {
        Image image = Rect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static TMP_Text Text(string name, Transform parent, string text, float size, Color color)
    {
        TextMeshProUGUI label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (Font == null) Font = TMP_Settings.defaultFontAsset;
        if (Font != null) label.font = Font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.richText = false;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    public static TMP_Text ScrollText(Transform parent)
    {
        Image area = Image("SpeechScroll", parent, new Color(1f, 1f, 1f, 0f));
        Place(area.rectTransform, 0f, 65f, 880f, 208f);
        ScrollRect scroll = area.gameObject.AddComponent<ScrollRect>();
        RectTransform viewport = Rect("Viewport", area.transform);
        Fill(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        TMP_Text text = Text("Speech", viewport, "", 26f, Ink);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.overflowMode = TextOverflowModes.Overflow;
        text.rectTransform.anchorMin = new Vector2(0f, 1f);
        text.rectTransform.anchorMax = new Vector2(1f, 1f);
        text.rectTransform.pivot = new Vector2(0.5f, 1f);
        text.rectTransform.sizeDelta = new Vector2(0f, 208f);
        text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = text.rectTransform;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        return text;
    }

    public static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    public static Button Button(string name, Transform parent, string caption, Color color, out TMP_Text label)
    {
        Image image = Image(name, parent, color);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.86f, 0.94f, 0.85f);
        colors.pressedColor = new Color(0.74f, 0.85f, 0.74f);
        colors.disabledColor = new Color(0.80f, 0.82f, 0.77f);
        button.colors = colors;
        label = Text("Label", image.transform, caption, 22f, color == Green ? Color.white : Ink);
        label.alignment = TextAlignmentOptions.Center;
        Fill(label.rectTransform, 8f);
        return button;
    }

    public static TMP_InputField Input(Transform parent)
    {
        Image image = Image("Question", parent, Color.white);
        TMP_InputField field = image.gameObject.AddComponent<TMP_InputField>();
        field.targetGraphic = image;
        RectTransform viewport = Rect("Viewport", image.transform);
        Fill(viewport, 14f);
        viewport.gameObject.AddComponent<RectMask2D>();
        TMP_Text value = Text("Text", viewport, "", 22f, Ink);
        Fill(value.rectTransform);
        value.alignment = TextAlignmentOptions.MidlineLeft;
        TMP_Text placeholder = Text("Placeholder", viewport, "Ask the herbalist a question...", 21f, new Color(0.42f, 0.48f, 0.44f));
        Fill(placeholder.rectTransform);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        field.textViewport = viewport;
        field.textComponent = value;
        field.placeholder = placeholder;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = 500;
        return field;
    }
}
