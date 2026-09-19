using System.Collections.Generic;
using UnityEngine;

// Builds the jam board from deterministic rules on every peer. The original authored roads,
// checkpoints and jail anchors remain untouched; only legacy demo tiles are disabled in Play Mode.
[RequireComponent(typeof(GameManager))]
public class ContinuousBoard : MonoBehaviour
{
    const float RoadCenter = 10f;
    const float RoadWidth = 4f;
    const float CornerSize = 4f;
    const float StraightCell = 16f / 9f;
    const float TileY = .31f;
    readonly List<Material> runtimeMaterials = new List<Material>();
    readonly ToyBoardPalette palette = new ToyBoardPalette();
    readonly BoardPresentationRules presentation = new BoardPresentationRules();
    Transform boardRoot;
    Material depthTextMaterial;

    private void Awake() => Build();

    public void Build()
    {
        if (transform.Find("ContinuousBoard_Runtime") != null) return;
        DisableLegacyTiles();

        GameObject root = new GameObject("ContinuousBoard_Runtime");
        root.transform.SetParent(transform, false);
        boardRoot = root.transform;

        var rules = new ContinuousBoardRules();
        IReadOnlyList<BoardSpaceDefinition> spaces = rules.CreateSpaces();
        for (int index = 0; index < spaces.Count; index++) CreateSpace(index, spaces[index]);
        ImproveAuthoredJailLabel();
        CreateCenterDecoration();
        CreateBoundaries();
    }

    private static void DisableLegacyTiles()
    {
        GameObject core = GameObject.Find("JamCore_Demo");
        Transform oldProperties = core == null ? null : core.transform.Find("Properties");
        if (oldProperties != null) oldProperties.gameObject.SetActive(false);

        GameObject events = GameObject.Find("JamEvents_Demo");
        if (events == null) return;
        foreach (string childName in new[] { "Fortune", "Chance", "JailTrap" })
        {
            Transform child = events.transform.Find(childName);
            if (child != null) child.gameObject.SetActive(false);
        }
    }

    private void CreateSpace(int index, BoardSpaceDefinition definition)
    {
        GetPose(index, out Vector3 position, out Vector3 size);
        GameObject tile = new GameObject("Space_" + index.ToString("00") + "_" + definition.Name.Replace(' ', '_'));
        tile.transform.SetParent(boardRoot, false);
        tile.transform.position = position;

        BoxCollider trigger = tile.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        // The pawn actually walks on the scene ground at y=0; the toy board face is visual-only.
        // Keep the contact volume touching that floor while its upper edge stays low enough that
        // jumping over a space does not count as landing on it.
        trigger.size = new Vector3(size.x, .2f, size.z);
        trigger.center = new Vector3(0f, -.21f, 0f);

        CreateVisual(PrimitiveType.Cube,"ToyRim",tile.transform,new Vector3(0f,0f,0f),
            new Vector3(size.x-.035f,.12f,size.z-.035f),MaterialFromHex("Board_Rim",palette.RimHex));
        GameObject face = CreateVisual(PrimitiveType.Cube,"TileFace",tile.transform,new Vector3(0f,.072f,0f),
            new Vector3(size.x-.16f,.035f,size.z-.16f),MaterialFor(index,definition.Kind));
        Renderer renderer = face.GetComponent<Renderer>();

        TextMesh label = CreateLabel(tile.transform,index,definition,size);

        if (definition.Kind == BoardSpaceKind.Property)
        {
            PropertyZone property = tile.AddComponent<PropertyZone>();
            property.Configure(definition.Name,definition.Price,definition.Toll,renderer,label,
                ColorFromHex(palette.UnownedPropertyColor(index)));
        }
        else if (definition.Kind == BoardSpaceKind.Fortune || definition.Kind == BoardSpaceKind.Chance)
        {
            BoardEventZone zone = tile.AddComponent<BoardEventZone>();
            zone.Configure(definition.Kind == BoardSpaceKind.Fortune);
        }
        else if (definition.Kind != BoardSpaceKind.Start && definition.Kind != BoardSpaceKind.JailVisit)
        {
            SpecialBoardTile special = tile.AddComponent<SpecialBoardTile>();
            special.Configure(definition.Kind);
        }
    }

    private static void GetPose(int index, out Vector3 position, out Vector3 size)
    {
        if (index % 10 == 0)
        {
            int corner = index / 10;
            position = corner switch
            {
                0 => new Vector3(-RoadCenter, TileY, -RoadCenter),
                1 => new Vector3(RoadCenter, TileY, -RoadCenter),
                2 => new Vector3(RoadCenter, TileY, RoadCenter),
                _ => new Vector3(-RoadCenter, TileY, RoadCenter)
            };
            size = new Vector3(CornerSize, .12f, CornerSize);
            return;
        }

        int side = index / 10;
        int slot = index % 10;
        float along = -8f + (slot - .5f) * StraightCell;
        if (side == 0)
        {
            position = new Vector3(along, TileY, -RoadCenter);
            size = new Vector3(StraightCell, .12f, RoadWidth);
        }
        else if (side == 1)
        {
            position = new Vector3(RoadCenter, TileY, along);
            size = new Vector3(RoadWidth, .12f, StraightCell);
        }
        else if (side == 2)
        {
            position = new Vector3(-along, TileY, RoadCenter);
            size = new Vector3(StraightCell, .12f, RoadWidth);
        }
        else
        {
            position = new Vector3(-RoadCenter, TileY, -along);
            size = new Vector3(RoadWidth, .12f, StraightCell);
        }
    }

    private Material MaterialFor(int boardIndex, BoardSpaceKind kind)
    {
        string variant = kind == BoardSpaceKind.Property ? "Road"+(boardIndex/10) : kind.ToString();
        return MaterialFromHex("Board_"+variant,palette.ColorFor(boardIndex,kind));
    }

    private TextMesh CreateLabel(Transform parent, int boardIndex, BoardSpaceDefinition definition, Vector3 tileSize)
    {
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = new Vector3(0f, .125f, 0f);
        labelObject.transform.localRotation = Quaternion.AngleAxis(presentation.TileLabelAngle(boardIndex),Vector3.up)
            * Quaternion.Euler(90f,0f,0f);
        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = presentation.TileLabel(definition);
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 64;
        label.characterSize = tileSize.x >= 3f && tileSize.z >= 3f ? .09f : .075f;
        label.fontStyle = FontStyle.Bold;
        label.color = ColorFromHex(palette.LabelHexFor(definition.Kind));
        ApplyDepthTestedTextMaterial(label);
        return label;
    }

    private static void ImproveAuthoredJailLabel()
    {
        GameObject jailPen = GameObject.Find("JailPen");
        TextMesh jailLabel = jailPen == null ? null : jailPen.GetComponentInChildren<TextMesh>(true);
        if (jailLabel == null) return;
        jailLabel.color = new Color(.08f,.1f,.16f);
        jailLabel.fontStyle = FontStyle.Bold;
    }

    private void CreateCenterDecoration()
    {
        Material center = MaterialFromHex("Board_Center",palette.CenterHex);
        Material accent = MaterialFromHex("Board_CenterAccent",palette.CenterAccentHex);
        CreateVisual(PrimitiveType.Cube,"CenterBoard",boardRoot,new Vector3(0f,.27f,0f),
            new Vector3(15.35f,.08f,15.35f),center);
        CreateVisual(PrimitiveType.Cylinder,"CenterMedallion",boardRoot,new Vector3(0f,.36f,0f),
            new Vector3(3.15f,.06f,3.15f),accent);

        foreach (Vector3 position in new[] {
            new Vector3(-5.2f,.36f,-5.2f), new Vector3(5.2f,.36f,-5.2f),
            new Vector3(5.2f,.36f,5.2f), new Vector3(-5.2f,.36f,5.2f) })
            CreateToyPawn(position,accent);

        GameObject titleObject = new GameObject("CenterTitle");
        titleObject.transform.SetParent(boardRoot,false);
        titleObject.transform.position = new Vector3(0f,.5f,0f);
        titleObject.transform.rotation = Quaternion.Euler(90f,0f,0f);
        TextMesh title = titleObject.AddComponent<TextMesh>();
        title.text = "MOTION\nMONEY RUSH";
        title.anchor = TextAnchor.MiddleCenter;
        title.alignment = TextAlignment.Center;
        title.fontSize = 64;
        title.characterSize = .065f;
        title.fontStyle = FontStyle.Bold;
        title.color = new Color(.21f,.16f,.34f);
        ApplyDepthTestedTextMaterial(title);
    }

    private void ApplyDepthTestedTextMaterial(TextMesh textMesh)
    {
        Shader shader = Resources.Load<Shader>("DepthTestedText");
        if (shader == null) shader = Shader.Find("Jam/DepthTestedText");
        if (shader == null || textMesh.font == null) return;

        if (depthTextMaterial == null)
        {
            depthTextMaterial = new Material(shader) { name = "Board_DepthTestedText" };
            depthTextMaterial.mainTexture = textMesh.font.material.mainTexture;
            runtimeMaterials.Add(depthTextMaterial);
        }
        textMesh.GetComponent<MeshRenderer>().sharedMaterial = depthTextMaterial;
    }

    private void CreateToyPawn(Vector3 position, Material material)
    {
        GameObject root = new GameObject("CenterToyPawn");
        root.transform.SetParent(boardRoot,false);
        root.transform.position = position;
        CreateVisual(PrimitiveType.Cylinder,"Base",root.transform,Vector3.zero,new Vector3(.72f,.12f,.72f),material);
        CreateVisual(PrimitiveType.Sphere,"Top",root.transform,new Vector3(0f,.72f,0f),new Vector3(.85f,.85f,.85f),material);
        CreateVisual(PrimitiveType.Cylinder,"Neck",root.transform,new Vector3(0f,.34f,0f),new Vector3(.42f,.27f,.42f),material);
    }

    private void CreateBoundaries()
    {
        Material curb = MaterialFromHex("Board_Curb",palette.BoundaryHex);
        Material stripe = MaterialFromHex("Board_CurbStripe",palette.RimHex);
        CreateBoundary("Outer_North", new Vector3(0f, 1.6f, 12.15f), new Vector3(24.3f, 3.2f, .3f), curb,stripe);
        CreateBoundary("Outer_South", new Vector3(0f, 1.6f, -12.15f), new Vector3(24.3f, 3.2f, .3f), curb,stripe);
        CreateBoundary("Outer_East", new Vector3(12.15f, 1.6f, 0f), new Vector3(.3f, 3.2f, 24.3f), curb,stripe);
        CreateBoundary("Outer_West", new Vector3(-12.15f, 1.6f, 0f), new Vector3(.3f, 3.2f, 24.3f), curb,stripe);
        CreateBoundary("Inner_North", new Vector3(0f, 1.6f, 7.85f), new Vector3(15.7f, 3.2f, .3f), curb,stripe);
        CreateBoundary("Inner_South", new Vector3(0f, 1.6f, -7.85f), new Vector3(15.7f, 3.2f, .3f), curb,stripe);
        CreateBoundary("Inner_East", new Vector3(7.85f, 1.6f, 0f), new Vector3(.3f, 3.2f, 15.7f), curb,stripe);
        CreateBoundary("Inner_West", new Vector3(-7.85f, 1.6f, 0f), new Vector3(.3f, 3.2f, 15.7f), curb,stripe);
    }

    private Material MaterialFromHex(string name, string htmlColor)
    {
        foreach (Material existing in runtimeMaterials)
            if (existing.name == name) return existing;
        Color color = ColorFromHex(htmlColor);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness",.32f);
        runtimeMaterials.Add(material);
        return material;
    }

    private static Color ColorFromHex(string htmlColor)
    {
        return ColorUtility.TryParseHtmlString(htmlColor,out Color color) ? color : Color.magenta;
    }

    private static GameObject CreateVisual(PrimitiveType type,string name,Transform parent,Vector3 localPosition,
        Vector3 localScale,Material material)
    {
        GameObject visual = GameObject.CreatePrimitive(type);
        visual.name = name;
        visual.transform.SetParent(parent,false);
        visual.transform.localPosition = localPosition;
        visual.transform.localScale = localScale;
        Collider visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null) visualCollider.enabled = false;
        visual.GetComponent<Renderer>().sharedMaterial = material;
        return visual;
    }

    private void CreateBoundary(string name, Vector3 position, Vector3 colliderSize, Material material,Material stripeMaterial)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(boardRoot, false);
        wall.transform.position = position;
        BoxCollider collider = wall.AddComponent<BoxCollider>();
        collider.size = colliderSize;

        GameObject curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
        curb.name = "VisibleCurb";
        curb.transform.SetParent(wall.transform, false);
        curb.transform.localPosition = new Vector3(0f, -1.35f, 0f);
        curb.transform.localScale = new Vector3(colliderSize.x, .5f, colliderSize.z);
        curb.GetComponent<Collider>().enabled = false;
        curb.GetComponent<Renderer>().sharedMaterial = material;
        GameObject stripe = CreateVisual(PrimitiveType.Cube,"ToyStripe",wall.transform,
            new Vector3(0f,-1.06f,0f),new Vector3(colliderSize.x,.07f,colliderSize.z+.035f),stripeMaterial);
        stripe.transform.localRotation = Quaternion.identity;
    }

    private void OnDestroy()
    {
        foreach (Material material in runtimeMaterials)
            if (material != null) Destroy(material);
        runtimeMaterials.Clear();
    }
}
