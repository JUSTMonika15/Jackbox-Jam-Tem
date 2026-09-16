using UnityEngine;
using UnityEngine.SceneManagement;

// Lightweight overlay for both the landing page and ready lobby, which share MainMenu.
public sealed class MainMenuRulesOverlay : MonoBehaviour
{
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
        if (!scene.IsValid() || scene.name != "MainMenu"
            || Object.FindAnyObjectByType<MainMenuRulesOverlay>() != null) return;
        GameObject overlay = new GameObject("MainMenuRulesOverlay");
        SceneManager.MoveGameObjectToScene(overlay, scene);
        overlay.AddComponent<MainMenuRulesOverlay>();
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

            const float panelWidth = 720f;
            const float panelHeight = 520f;
            float x = (width - panelWidth) * .5f;
            float y = (height - panelHeight) * .5f;
            GUI.color = new Color(.06f, .075f, .12f, .98f);
            GUI.DrawTexture(new Rect(x, y, panelWidth, panelHeight), Texture2D.whiteTexture);
            GUI.color = new Color(.98f, .78f, .12f, 1f);
            GUI.DrawTexture(new Rect(x, y, panelWidth, 62f), Texture2D.whiteTexture);
            GUI.color = oldColor;

            GUI.Label(new Rect(x + 24f, y + 8f, panelWidth - 48f, 48f), MainMenuRulesContent.Title, titleStyle);
            GUI.Label(new Rect(x + 38f, y + 78f, panelWidth - 76f, 362f), MainMenuRulesContent.Body, bodyStyle);
            if (GUI.Button(new Rect(x + panelWidth * .5f - 90f, y + 462f, 180f, 38f), "CLOSE", buttonStyle))
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
            fontSize = 18,
            wordWrap = true,
            richText = false,
            alignment = TextAnchor.UpperLeft
        };
        bodyStyle.normal.textColor = Color.white;
    }
}
