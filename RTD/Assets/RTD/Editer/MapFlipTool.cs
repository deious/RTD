#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MapFlipTool
{
    [MenuItem("RTD/Map/Flip Map Vertically (Y)")]
    public static void FlipY()
    {
        GameObject mapRoot = Selection.activeGameObject;
        if (mapRoot == null)
        {
            Debug.LogError("MapRoot 오브젝트를 선택한 상태에서 실행하세요.");
            return;
        }

        Transform tiles = mapRoot.transform.Find("Tiles");
        Transform waypoints = mapRoot.transform.Find("Waypoints");

        if (tiles == null)
        {
            Debug.LogError("MapRoot 아래에 'Tiles' 오브젝트가 없습니다.");
            return;
        }

        // GridManager에서 값을 가져오거나, 여기서 직접 설정
        GridManager gm = Object.FindFirstObjectByType<GridManager>();
        if (gm == null)
        {
            Debug.LogError("씬에 GridManager가 없습니다. width/height/cellSize 계산을 위해 필요합니다.");
            return;
        }

        int height = gm.height;
        float cell = gm.cellSize;

        // Z축 반전 기준선: 0 ~ (height-1)*cell 범위에서 대칭
        float zMax = (height - 1) * cell;

        Undo.RegisterFullObjectHierarchyUndo(mapRoot, "Flip Map Y");

        // 1) Tiles 뒤집기
        for (int i = 0; i < tiles.childCount; i++)
        {
            Transform t = tiles.GetChild(i);
            Vector3 p = t.localPosition;
            p.z = zMax - p.z;
            t.localPosition = p;

            // GridTile이 있다면 GridPos도 같이 갱신(권장)
            GridTile tile = t.GetComponent<GridTile>();
            if (tile != null)
            {
                Vector2Int gp = tile.GridPos;
                int newY = (height - 1) - gp.y;
                tile.Init(new Vector2Int(gp.x, newY), tile.TileType);
                EditorUtility.SetDirty(tile);
            }
        }

        // 2) Waypoints도 같이 뒤집기 (있으면)
        if (waypoints != null)
        {
            for (int i = 0; i < waypoints.childCount; i++)
            {
                Transform w = waypoints.GetChild(i);
                Vector3 p = w.localPosition;
                p.z = zMax - p.z;
                w.localPosition = p;
            }
        }

        Debug.Log("Flip Map Vertically (Y) 완료: Tiles/Waypoints가 Z축 기준으로 반전되었습니다.");
    }
}
#endif
