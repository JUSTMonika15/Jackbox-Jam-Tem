using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Lightweight overlay for both the landing page and ready lobby, which share MainMenu.
public sealed class MainMenuRulesOverlay : MonoBehaviour
{
    readonly MainMenuRulesLayout layout = new MainMenuRulesLayout();
    bool open;
    GUIStyle buttonStyle;
    GUIStyle titleStyle;
    GUIStyle bodyStyle;

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
        if (!scene.IsValid() || scene.name != "MainMenu") return;
        ConfigureBackdrop(scene);
        if (Object.FindAnyObjectByType<MainMenuRulesOverlay>() != null) return;
        GameObject overlay = new GameObject("MainMenuRulesOverlay");
        SceneManager.MoveGameObjectToScene(overlay, scene);
        overlay.AddComponent<MainMenuRulesOverlay>();
    }

    static void ConfigureBackdrop(Scene scene)
    {
        GameObject background = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "Grain") continue;
            Transform child = root.transform.Find("Image");
            if (child != null) background = child.gameObject;
            break;
        }

        Image image = background != null ? background.GetComponent<Image>() : null;
        if (image == null || image.sprite == null
            || image.sprite.name != MainMenuVisualRules.BackgroundSpriteName) return;

        image.color = Color.white;
        image.raycastTarget = false;
        image.rectTransform.localScale = Vector3.one;
        AspectRatioFitter fitter = background.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = background.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;

        if (GameObject.Find("MainMenuControlBackdrop") != null) return;
        GameObject canvasObject = new GameObject("MainMenuControlBackdrop",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -500;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = .5f;

        GameObject panelObject = new GameObject("ControlPanel", typeof(RectTransform), typeof(Image));
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.SetParent(canvasObject.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
        panel.anchoredPosition = new Vector2(0f, MainMenuVisualRules.ControlPanelY);
        panel.sizeDelta = new Vector2(MainMenuVisualRules.ControlPanelWidth,
            MainMenuVisualRules.ControlPanelHeight);
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(.035f, .045f, .075f, MainMenuVisualRules.ControlPanelOpacity);
        panelImage.raycastTarget = false;
    }

    void OnGUI()
    {
        EnsureStyles();
        float scale = Mathf.Max(.5f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        int oldDepth = GUI.depth;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        GUI.depth = -1000;

        if (!open)
        {
            if (GUI.Button(new Rect(width - 152f, 70f, 128f, 38f), "RULES", buttonStyle)) open = true;
        }
        else
        {
            GUI.color = new Color(0f, 0f, 0f, .58f);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
            GUI.color = oldColor;

            float panelWidth = layout.PanelWidth;
            float panelHeight = layout.PanelHeight;
            float x = (width - panelWidth) * .5f;
            float y = (height - panelHeight) * .5f;
            GUI.color = new Color(.06f, .075f, .12f, .98f);
            GUI.DrawTexture(new Rect(x, y, panelWidth, panelHeight), Texture2D.whiteTexture);
            GUI.color = new Color(.98f, .78f, .12f, 1f);
            GUI.DrawTexture(new Rect(x, y, panelWidth, 62f), Texture2D.whiteTexture);
            GUI.color = oldColor;

            GUI.Label(new Rect(x + 24f, y + 8f, panelWidth - 48f, 48f), MainMenuRulesContent.Title, titleStyle);
            GUI.Label(new Rect(x + 38f, y + layout.BodyTop, panelWidth - 76f, layout.BodyHeight),
                MainMenuRulesContent.Body, bodyStyle);
            if (GUI.Button(new Rect(x + panelWidth * .5f - 90f, y + layout.CloseButtonY,
                180f, layout.CloseButtonHeight), "CLOSE", buttonStyle))
                open = false;
        }

        GUI.depth = oldDepth;
        GUI.color = oldColor;
        GUI.matrix = oldMatrix;
    }

    void EnsureStyles()
    {
        if (buttonStyle != null) return;
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(.08f, .08f, .12f);
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = layout.BodyFontSize,
            wordWrap = true,
            richText = false,
            alignment = TextAnchor.UpperLeft
        };
        bodyStyle.normal.textColor = Color.white;
    }
}
