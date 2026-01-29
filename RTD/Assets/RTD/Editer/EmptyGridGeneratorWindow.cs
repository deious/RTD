#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class EmptyGridGeneratorWindow : EditorWindow
{
    [Header("Grid")]
    private int width = 16;
    private int height = 12;
    private float cellSize = 10f;

    [Header("Target")]
    private Transform tilesRoot;

    [Header("Defaults")]
    private TileType defaultType = TileType.Buildable;
    private bool nameTiles = true;

    [MenuItem("RTD/Empty Grid Generator")]
    public static void Open()
    {
        GetWindow<EmptyGridGeneratorWindow>("Empty Grid");
    }

    private void OnGUI()
    {
        GUILayout.Label("Generate Empty Tile Slots", EditorStyles.boldLabel);

        width = EditorGUILayout.IntField("Width", width);
        height = EditorGUILayout.IntField("Height", height);
        cellSize = EditorGUILayout.FloatField("Cell Size", cellSize);

        tilesRoot = (Transform)EditorGUILayout.ObjectField("TilesRoot", tilesRoot, typeof(Transform), true);

        defaultType = (TileType)EditorGUILayout.EnumPopup("Default TileType", defaultType);
        nameTiles = EditorGUILayout.Toggle("Name Tiles (T_x_y)", nameTiles);

        GUILayout.Space(10);

        using (new EditorGUI.DisabledScope(tilesRoot == null))
        {
            if (GUILayout.Button("Generate Empty 16x12 Tiles"))
                Generate();
        }

        using (new EditorGUI.DisabledScope(tilesRoot == null))
        {
            if (GUILayout.Button("Delete All Under TilesRoot"))
            {
                if (EditorUtility.DisplayDialog("Delete", "TilesRoot 아래를 전부 삭제할까요?", "Delete", "Cancel"))
                    DeleteAll();
            }
        }
    }

    private void Generate()
    {
        if (tilesRoot == null)
        {
            Debug.LogError("TilesRoot가 비어있습니다.");
            return;
        }

        if (tilesRoot.childCount > 0)
        {
            bool cont = EditorUtility.DisplayDialog(
                "TilesRoot Not Empty",
                "TilesRoot 아래에 이미 오브젝트가 있습니다. 그래도 생성할까요?\n(겹칠 수 있어요)",
                "Generate", "Cancel");

            if (!cont) return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var go = new GameObject();
            Undo.RegisterCreatedObjectUndo(go, "Create Empty Tile");

            go.transform.SetParent(tilesRoot, false);
            go.transform.position = new Vector3(x * cellSize, 0f, y * cellSize);

            if (nameTiles)
                go.name = $"T_{x:00}_{y:00}";

            var tile = go.GetComponent<GridTile>();
            if (tile == null)
                tile = Undo.AddComponent<GridTile>(go);

            tile.Init(new Vector2Int(x, y), defaultType);
            EditorUtility.SetDirty(tile);
        }

        Undo.CollapseUndoOperations(group);

        Debug.Log($"[RTD] Generated empty grid: {width}x{height} = {width * height} tiles.");
    }

    private void DeleteAll()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        
        int count = tilesRoot.childCount;
        var list = new GameObject[count];
        for (int i = 0; i < count; i++)
            list[i] = tilesRoot.GetChild(i).gameObject;

        foreach (var go in list)
            Undo.DestroyObjectImmediate(go);

        Undo.CollapseUndoOperations(group);
    }
}
#endif
