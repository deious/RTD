using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

public class LaneMapBootstrap : MonoBehaviour
{
    [Header("Map Prefabs")]
    [Tooltip("내 lane에만 깔리는 '실제 플레이용' MapRoot 프리팹 (Tiles/WaypointPath/Environment 위주 권장)")]
    [SerializeField] private GameObject gameplayMapPrefab;

    [Tooltip("다른 lane에 깔리는 '관전용' MapRoot 프리팹 (콜라이더/상호작용 꺼짐)")]
    [SerializeField] private GameObject viewMapPrefab;

    [Header("Lane Anchors (P1~P4)")]
    [Tooltip("LaneRoot_P1~P4 (맵 기준점). 인덱스 0..3 과 매칭")]
    [SerializeField] private Transform[] laneAnchors = new Transform[4];

    [Header("Options")]
    [SerializeField] private bool autoBindAnchorsByName = true;
    [SerializeField] private string anchorNameFormat = "LaneRoot_P{0}";

    [Tooltip("NGO에서 MyLaneId가 확정될 때까지 기다릴 프레임 수(최소 2 추천)")]
    [SerializeField] private int waitFramesForLaneResolve = 2;

    [Tooltip("접속 인원수만큼만 맵을 켤지")]
    [SerializeField] private bool usePlayersCount = true;

    [Header("Debug")]
    [SerializeField] private bool log = true;

    private GameObject[] _spawnedMaps = new GameObject[4];
    private int _builtForMyLane = -1;
    private int _builtForPlayers = -1;

    private void Awake()
    {
        if (autoBindAnchorsByName)
            AutoBindAnchors();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        BootstrapAsync().Forget();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (autoBindAnchorsByName)
            AutoBindAnchors();

        BootstrapAsync().Forget();
    }

    private async UniTaskVoid BootstrapAsync()
    {
        for (int i = 0; i < Mathf.Max(0, waitFramesForLaneResolve); i++)
            await UniTask.NextFrame();

        MultiplayerContext.ResolveMyLaneIdFromNgo();
        int myLane = Mathf.Clamp(MultiplayerContext.MyLaneId, 0, 3);

        int players = Mathf.Clamp(MultiplayerContext.PlayersCount, 1, 4);
        if (!usePlayersCount) 
            players = 4;

        if (_builtForMyLane == myLane && _builtForPlayers == players)
        {
            if (log) Debug.Log($"[LaneMapBootstrap] Skip rebuild (same). myLane={myLane}, players={players}");
            return;
        }

        _builtForMyLane = myLane;
        _builtForPlayers = players;

        if (log) Debug.Log($"[LaneMapBootstrap] Rebuild maps. MyLaneId={myLane}, Players={players}");
        BuildMaps(myLane, players);
        
        var mm = FindFirstObjectByType<MiniMapUIController>(FindObjectsInactive.Include);
        if (mm != null)
            mm.SetPlayerCount(players);
    }

    private void BuildMaps(int myLaneId, int playersCount)
    {
        // 기존 제거
        for (int i = 0; i < _spawnedMaps.Length; i++)
        {
            if (_spawnedMaps[i] != null) Destroy(_spawnedMaps[i]);
            _spawnedMaps[i] = null;
        }

        playersCount = Mathf.Clamp(playersCount, 1, 4);

        // 1) 내 lane gameplay 먼저
        var gameplay = SpawnOneLane(myLaneId, isMyLane: true);

        // ✅ 핵심: gameplay map이 생성된 직후 GridManager를 그 맵으로 바인딩
        if (gameplay != null)
        {
            if (GridManager.Instance != null)
            {
                GridManager.Instance.BindToMapRoot(gameplay.transform);
            }
            else
            {
                Debug.LogError("[LaneMapBootstrap] GridManager.Instance is null. 씬에 GridManager(싱글턴)가 있어야 합니다.");
            }
        }

        // 2) 나머지 lane view
        for (int i = 0; i < playersCount; i++)
        {
            if (i == myLaneId) continue;
            SpawnOneLane(i, isMyLane: false);
        }

        if (MiniMapLaneRegistry.Instance != null)
            MiniMapLaneRegistry.Instance.RebindAllAfterMapBuild(_spawnedMaps, laneAnchors);
        
        if (log)
        {
            Debug.Log($"[LaneMapBootstrap] BuildMaps done. myLane={myLaneId} players={playersCount}");
            for (int i = 0; i < 4; i++)
            {
                var a = GetAnchor(i);
                Debug.Log($"  Anchor lane={i} => {(a ? a.name : "NULL")}");
            }
        }
    }

    private GameObject SpawnOneLane(int laneId, bool isMyLane)
    {
        Transform anchor = GetAnchor(laneId);
        if (anchor == null)
        {
            if (log) Debug.LogWarning($"[LaneMapBootstrap] Missing lane anchor for laneId={laneId}");
            return null;
        }

        GameObject prefab = isMyLane ? gameplayMapPrefab : viewMapPrefab;
        if (prefab == null)
        {
            Debug.LogError($"[LaneMapBootstrap] Map prefab missing. isMyLane={isMyLane} laneId={laneId}");
            return null;
        }

        var go = Instantiate(prefab, anchor);
        go.name = isMyLane ? $"[MapGameplay]_P{laneId + 1}" : $"[MapView]_P{laneId + 1}";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        _spawnedMaps[laneId] = go;

        if (!isMyLane)
            DisableInteractionRecursive(go);

        if (log)
            Debug.Log($"[LaneMapBootstrap] Spawned {(isMyLane ? "GAMEPLAY" : "VIEW")} laneId={laneId} at {anchor.name} root={go.name}");

        return go;
    }

    private Transform GetAnchor(int laneId)
    {
        if (laneAnchors != null && laneId >= 0 && laneId < laneAnchors.Length)
            return laneAnchors[laneId];
        return null;
    }

    private void AutoBindAnchors()
    {
        if (laneAnchors == null || laneAnchors.Length != 4)
            laneAnchors = new Transform[4];

        for (int i = 0; i < 4; i++)
        {
            // ✅ 씬이 바뀌면 기존 레퍼런스가 깨질 수 있어서, null이면 다시 찾기
            if (laneAnchors[i] != null) continue;

            string n = string.Format(anchorNameFormat, i + 1);
            var go = GameObject.Find(n);
            if (go != null) laneAnchors[i] = go.transform;
        }

        if (log)
        {
            Debug.Log("[LaneMapBootstrap] AutoBindAnchors result:");
            for (int i = 0; i < 4; i++)
                Debug.Log($"  laneAnchors[{i}]={(laneAnchors[i] ? laneAnchors[i].name : "NULL")}");
        }
    }

    private void DisableInteractionRecursive(GameObject root)
    {
        // ✅ 관전용 맵에서는 "입력/상호작용"이 일어나면 안 됨

        var cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            cols[i].enabled = false;

        // 네가 올린 GridTile은 OnSelected로 TowerManager를 부르므로 view에선 반드시 꺼야 안전
        var tiles = root.GetComponentsInChildren<GridTile>(true);
        for (int i = 0; i < tiles.Length; i++)
            tiles[i].enabled = false;

        // (선택) view맵에 혹시 실수로 들어간 시스템 스크립트들 비활성
        var gm = root.GetComponentsInChildren<GridManager>(true);
        for (int i = 0; i < gm.Length; i++)
            gm[i].enabled = false;

        var sp = root.GetComponentsInChildren<MonsterSpawner>(true);
        for (int i = 0; i < sp.Length; i++)
            sp[i].enabled = false;

        var tm = root.GetComponentsInChildren<TowerManager>(true);
        for (int i = 0; i < tm.Length; i++)
            tm[i].enabled = false;
    }
}
