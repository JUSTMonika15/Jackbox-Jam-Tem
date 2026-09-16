using PurrNet.Lobby;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Creates a virtual gamepad only when the WebGL player is opened on a touch device.
// Existing PlayerInput actions consume it exactly like a physical gamepad.
public sealed class TouchControlsOverlay : MonoBehaviour
{
    CanvasGroup controls;
    Texture2D circleTexture;
    Sprite circleSprite;
    bool pauseOpen;
    bool shown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        AddToScene(SceneManager.GetActiveScene());
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => AddToScene(scene);

    static void AddToScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name != "MainGame"
            || Object.FindAnyObjectByType<TouchControlsOverlay>() != null) return;
        GameObject overlay = new GameObject("TouchControlsOverlay");
        SceneManager.MoveGameObjectToScene(overlay, scene);
        overlay.AddComponent<TouchControlsOverlay>();
    }

    void Awake()
    {
        EnsureEventSystem();
        CreateCircleSprite();
        BuildCanvas();
        RefreshVisibility(true);
    }

    void OnEnable()
    {
        PauseMenuView.onOpened += HandlePauseOpened;
        PauseMenuView.onClosed += HandlePauseClosed;
    }

    void OnDisable()
    {
        PauseMenuView.onOpened -= HandlePauseOpened;
        PauseMenuView.onClosed -= HandlePauseClosed;
        pauseOpen = false;
    }

    void Update() => RefreshVisibility(false);

    void HandlePauseOpened()
    {
        pauseOpen = true;
        RefreshVisibility(true);
    }

    void HandlePauseClosed()
    {
        pauseOpen = false;
        RefreshVisibility(true);
    }

    void RefreshVisibility(bool force)
    {
        bool shouldShow = !pauseOpen && TouchControlRules.ShouldShow(
            Touchscreen.current != null, Application.isMobilePlatform);
        if (!force && shown == shouldShow) return;
        shown = shouldShow;
        if (controls == null) return;
        controls.alpha = shouldShow ? 1f : 0f;
        controls.interactable = shouldShow;
        controls.blocksRaycasts = shouldShow;
    }

    void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("TouchCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
        controls = canvasObject.GetComponent<CanvasGroup>();

        RectTransform baseRect = CreateCircle("MOVE", canvasObject.transform,
            new Vector2(140f, 150f), new Vector2(190f, 190f), new Color(.04f, .06f, .1f, .5f), false);
        RectTransform knob = CreateCircle(string.Empty, baseRect,
            Vector2.zero, new Vector2(92f, 92f), new Color(.98f, .82f, .18f, .86f), true);
        OnScreenStick stick = knob.gameObject.AddComponent<OnScreenStick>();
        stick.controlPath = TouchControlBindings.Move;
        stick.movementRange = 58f;
        stick.useIsolatedInputActions = true;

        CreateButton("BUY", canvasObject.transform, new Vector2(-110f, 114f),
            new Color(.98f, .74f, .12f, .88f), TouchControlBindings.Buy);
        CreateButton("PUSH", canvasObject.transform, new Vector2(-224f, 114f),
            new Color(.96f, .34f, .28f, .88f), TouchControlBindings.Push);
        CreateButton("JUMP", canvasObject.transform, new Vector2(-110f, 228f),
            new Color(.25f, .68f, .98f, .88f), TouchControlBindings.Jump);
    }

    RectTransform CreateCircle(string label, Transform parent, Vector2 position, Vector2 size,
        Color color, bool centered)
    {
        GameObject circle = new GameObject(string.IsNullOrEmpty(label) ? "Stick" : label,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = circle.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = centered ? new Vector2(.5f, .5f) : Vector2.zero;
        rect.anchorMax = centered ? new Vector2(.5f, .5f) : Vector2.zero;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = circle.GetComponent<Image>();
        image.sprite = circleSprite;
        image.color = color;
        image.raycastTarget = true;
        if (!string.IsNullOrEmpty(label)) CreateLabel(label, rect);
        return rect;
    }

    void CreateButton(string label, Transform parent, Vector2 position, Color color, string controlPath)
    {
        RectTransform rect = CreateCircle(label, parent, position, new Vector2(98f, 98f), color, false);
        rect.anchorMin = rect.anchorMax =
            new Vector2(TouchControlLayout.ButtonAnchorX, TouchControlLayout.ButtonAnchorY);
        rect.anchoredPosition = position;
        OnScreenButton button = rect.gameObject.AddComponent<OnScreenButton>();
        button.controlPath = controlPath;
    }

    static void CreateLabel(string value, RectTransform parent)
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(parent, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        Text text = labelObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    void CreateCircleSprite()
    {
        const int size = 64;
        circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "TouchControlCircle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2((size - 1) * .5f, (size - 1) * .5f);
        float radius = size * .49f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float edge = radius - Vector2.Distance(new Vector2(x, y), center);
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(edge + .5f) * 255f);
            pixels[y * size + x] = new Color32(255, 255, 255, alpha);
        }
        circleTexture.SetPixels32(pixels);
        circleTexture.Apply(false, true);
        circleSprite = Sprite.Create(circleTexture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
        circleSprite.name = "TouchControlCircle";
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject eventSystem = new GameObject("TouchEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        SceneManager.MoveGameObjectToScene(eventSystem, SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        if (circleSprite != null) Destroy(circleSprite);
        if (circleTexture != null) Destroy(circleTexture);
    }
}
