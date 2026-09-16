using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class JamCoreSetup
{
    const string PlayerPath = "Assets/Prefabs/FollowCamPlayer.prefab";
    [MenuItem("Game Jam/Setup Basic Gameplay")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().path != "Assets/Scenes/MainGame.unity")
        {
            Debug.LogWarning("Open MainGame before running gameplay setup.");
            return;
        }
        GameManager manager = Object.FindAnyObjectByType<GameManager>();
        if (manager == null)
        {
            GameObject go = new GameObject("GameManager");
            Undo.RegisterCreatedObjectUndo(go, "Create GameManager");
            manager = Undo.AddComponent<GameManager>(go);
        }
        if (manager.GetComponent<GameHud>() == null) Undo.AddComponent<GameHud>(manager.gameObject);
        if (manager.GetComponent<ContinuousBoard>() == null) Undo.AddComponent<ContinuousBoard>(manager.gameObject);
        GameObject prefab = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            if (prefab.GetComponent<PlayerState>() == null) prefab.AddComponent<PlayerState>();
            if (prefab.GetComponent<PlayerKnockback>() == null) prefab.AddComponent<PlayerKnockback>();
            if (prefab.GetComponent<PushAbility>() == null) prefab.AddComponent<PushAbility>();
            if (prefab.GetComponent<PlayerEffects>() == null) prefab.AddComponent<PlayerEffects>();
            PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        // Demo content is editable, has no fixed count limit, and never replaces existing roads.
        if (GameObject.Find("JamCore_Demo") == null)
        {
            Material land = GetMaterial("JamProperty", Color.gray);
            Material gold = GetMaterial("JamCoin", new Color(1f, 0.8f, 0.05f));
            GameObject root = new GameObject("JamCore_Demo");
            Undo.RegisterCreatedObjectUndo(root, "Create demo zones");
            GameObject properties = new GameObject("Properties");
            properties.transform.SetParent(root.transform);
            GameObject coins = new GameObject("Coins");
            coins.transform.SetParent(root.transform);
            int index = 1;
            foreach (float offset in new[] { -6f, 0f, 6f })
            {
                CreateProperty(properties.transform, new Vector3(offset, 0.4f, -10f), false, index++, land);
                CreateProperty(properties.transform, new Vector3(offset, 0.4f, 10f), false, index++, land);
                CreateProperty(properties.transform, new Vector3(-10f, 0.4f, offset), true, index++, land);
                CreateProperty(properties.transform, new Vector3(10f, 0.4f, offset), true, index++, land);
            }
            foreach (float offset in new[] { -9f, -3f, 3f, 9f })
            {
                CreateCoin(coins.transform, new Vector3(offset, 1f, -10f), gold);
                CreateCoin(coins.transform, new Vector3(offset, 1f, 10f), gold);
            }
            foreach (float offset in new[] { -6f, 0f, 6f })
            {
                CreateCoin(coins.transform, new Vector3(-10f, 1f, offset), gold);
                CreateCoin(coins.transform, new Vector3(10f, 1f, offset), gold);
            }
        }
        SetupEvents(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveScene(manager.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[JamCore] Setup complete: continuous 40-space board, 28 properties, ground-only events, 14 coins and 5s jail; networking unchanged.", manager);
        Selection.activeGameObject = manager.gameObject;
    }
    static Material GetMaterial(string name, Color color)
    {
        const string folder = "Assets/Settings/JamCore";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Settings", "JamCore");
        string path = folder + "/" + name + ".mat";
        Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (result != null) return result;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Standard");
        result = new Material(shader) { name = name, color = color };
        AssetDatabase.CreateAsset(result, path);
        return result;
    }
    static void SetupEvents(GameManager manager)
    {
        GameObject root = GameObject.Find("JamEvents_Demo");
        if (root == null)
        {
            root = new GameObject("JamEvents_Demo");
            Undo.RegisterCreatedObjectUndo(root, "Create jam event zones");
            Material fortune = GetMaterial("JamFortune", new Color(0.1f, 0.8f, 0.4f));
            Material chance = GetMaterial("JamChance", new Color(0.3f, 0.5f, 1f));
            Material jail = GetMaterial("JamJail", new Color(1f, 0.25f, 0.15f));
            CreateEvent(root.transform, "Fortune", new Vector3(3f, 0.32f, -10f), fortune, true);
            CreateEvent(root.transform, "Chance", new Vector3(-3f, 0.32f, 10f), chance, false);
            Transform start = Anchor(root.transform, "RaceStart", new Vector3(-10f, 1.3f, -8f));
            Transform cell = Anchor(root.transform, "JailPosition", new Vector3(5f, 1.1f, 5f));
            Transform exit = Anchor(root.transform, "JailExit", new Vector3(10f, 1.3f, 5f));
            GameObject trap = Tile(root.transform, "JailTrap", new Vector3(10f, 0.32f, 3f), new Vector3(3.4f, .15f, 1.2f), jail);
            BoxCollider box = trap.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(3.4f, .25f, 1.2f);
            trap.AddComponent<JailZone>();
            Label(trap.transform, "JUMP!", 0.2f);
            GameObject pen = Tile(root.transform, "JailPen", new Vector3(5f,.12f,5f), new Vector3(3f,.15f,3f), jail);
            Label(pen.transform, "JAIL 5s", 0.2f);
            foreach (Vector3 offset in new[] { new Vector3(-1.5f, .9f, 0), new Vector3(1.5f,.9f,0), new Vector3(0,.9f,-1.5f), new Vector3(0,.9f,1.5f) })
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "JailWall"; wall.transform.SetParent(root.transform); wall.transform.position = new Vector3(5f,0f,5f) + offset;
                wall.transform.localScale = offset.x != 0 ? new Vector3(.15f,1.8f,3f) : new Vector3(3f,1.8f,.15f);
                wall.GetComponent<Renderer>().sharedMaterial = jail;
            }
        }
        // Existing roads have a 0.25m top surface. Migrate only our newly generated buried tiles.
        foreach (string name in new[] { "Fortune", "Chance", "JailTrap" })
        {
            Transform tile = root.transform.Find(name);
            if (tile != null && tile.position.y < .25f)
            {
                Undo.RecordObject(tile, "Raise event tile above road");
                Vector3 position = tile.position; position.y = .32f; tile.position = position;
            }
        }
        foreach (string name in new[] { "RaceStart", "JailExit" })
        {
            Transform anchor = root.transform.Find(name);
            if (anchor != null && anchor.position.y < 1.25f)
            {
                Undo.RecordObject(anchor, "Raise road teleport above floor");
                Vector3 position = anchor.position; position.y = 1.3f; anchor.position = position;
            }
        }
        SerializedObject settings = new SerializedObject(manager);
        SetReferenceIfEmpty(settings, "raceStart", root.transform.Find("RaceStart"));
        SetReferenceIfEmpty(settings, "jailPosition", root.transform.Find("JailPosition"));
        SetReferenceIfEmpty(settings, "jailExit", root.transform.Find("JailExit"));
        settings.ApplyModifiedProperties();
    }
    static void SetReferenceIfEmpty(SerializedObject settings, string field, Transform target)
    {
        if (settings.FindProperty(field).objectReferenceValue == null) settings.FindProperty(field).objectReferenceValue = target;
    }
    static Transform Anchor(Transform parent, string name, Vector3 position)
    {
        GameObject go = new GameObject(name); go.transform.SetParent(parent); go.transform.position = position; return go.transform;
    }
    static GameObject Tile(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject go = new GameObject(name); go.transform.SetParent(parent); go.transform.position = position;
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube); visual.name = "TileVisual";
        visual.transform.SetParent(go.transform, false); visual.transform.localScale = size;
        visual.GetComponent<Collider>().enabled = false; visual.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }
    static void CreateEvent(Transform parent, string name, Vector3 position, Material material, bool fortune)
    {
        GameObject go = Tile(parent, name, position, new Vector3(2f,.15f,3.4f), material);
        BoxCollider box = go.AddComponent<BoxCollider>(); box.isTrigger = true;
        box.size = new Vector3(2f, 6f, 3.4f); box.center = new Vector3(0, 3f, 0);
        BoardEventZone zone = go.AddComponent<BoardEventZone>();
        SerializedObject settings = new SerializedObject(zone); settings.FindProperty("fortune").boolValue = fortune; settings.ApplyModifiedProperties();
        Label(go.transform, name.ToUpperInvariant(), 0.2f);
    }
    static void Label(Transform parent, string label, float height)
    {
        GameObject go = new GameObject("Label"); go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0,height,0); go.transform.localRotation = Quaternion.Euler(90,0,0);
        TextMesh text = go.AddComponent<TextMesh>(); text.text = label; text.anchor = TextAnchor.MiddleCenter;
        text.fontSize = 48; text.characterSize = .05f; text.color = Color.white;
    }
    static void CreateProperty(Transform parent, Vector3 position, bool vertical, int number, Material material)
    {
        GameObject zone = new GameObject("Property_" + number.ToString("00"));
        zone.transform.SetParent(parent);
        zone.transform.position = position;
        BoxCollider box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = vertical ? new Vector3(3.5f, 3f, 2.4f) : new Vector3(2.4f, 3f, 3.5f);
        box.center = new Vector3(0f, 1f, 0f);
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "OwnershipColor";
        visual.transform.SetParent(zone.transform, false);
        visual.transform.localScale = vertical ? new Vector3(3.5f, 0.1f, 2.4f) : new Vector3(2.4f, 0.1f, 3.5f);
        visual.GetComponent<Collider>().enabled = false;
        Renderer renderer = visual.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        PropertyZone property = zone.AddComponent<PropertyZone>();
        SerializedObject settings = new SerializedObject(property);
        settings.FindProperty("propertyName").stringValue = "Property " + number;
        // Race direction: south -> east -> north -> west; each road has three properties.
        int roadPrice = position.z < -9f ? 20 : position.x > 9f ? 40 : position.z > 9f ? 60 : 90;
        settings.FindProperty("price").intValue = roadPrice;
        settings.FindProperty("toll").intValue = roadPrice / 10;
        settings.FindProperty("zoneRenderer").objectReferenceValue = renderer;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }
    static void CreateCoin(Transform parent, Vector3 position, Material material)
    {
        GameObject coin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        coin.name = "Coin";
        coin.transform.SetParent(parent);
        coin.transform.position = position;
        coin.transform.localScale = Vector3.one * 0.55f;
        coin.GetComponent<SphereCollider>().isTrigger = true;
        coin.GetComponent<Renderer>().sharedMaterial = material;
        coin.AddComponent<CoinPickup>();
    }
}
