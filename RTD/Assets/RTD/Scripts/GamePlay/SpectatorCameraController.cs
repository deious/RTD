using UnityEngine;
using Cysharp.Threading.Tasks;

public class SpectatorCameraController : MonoBehaviour
{
    [Header("Move Target")]
    [SerializeField] private Transform cameraTarget;

    [Header("Spectate Points (P1~P4)")]
    [SerializeField] private Transform[] points = new Transform[4];

    [Header("Move Options")]
    [SerializeField] private bool smoothMove = true;
    [SerializeField] private float smoothSpeed = 12f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private Vector3 _goalPos;
    private bool _moving;

    public int CurrentSlot { get; private set; } = 0;

    private void Awake()
    {
        if (cameraTarget != null)
            _goalPos = cameraTarget.position;
    }

    private void Start()
    {
        // ✅ 코루틴 대신 UniTask
        StartSpectateMyLaneAsync().Forget();
    }

    private async UniTaskVoid StartSpectateMyLaneAsync()
    {
        // NGO 연결/ConnectedClientsList 정착까지 프레임 몇 번 대기 (너 부트스트랩이 콜백에서 갱신하니까)
        await UniTask.NextFrame();
        await UniTask.NextFrame();

        int my = MultiplayerContext.MyLaneId;

        if (debugLog)
            Debug.Log($"[Spectator] StartSpectateMyLaneAsync => MyLaneId={my}");

        Spectate(my);
    }

    private void Update()
    {
        if (!smoothMove || !_moving || cameraTarget == null) return;

        cameraTarget.position = Vector3.Lerp(cameraTarget.position, _goalPos, Time.deltaTime * smoothSpeed);

        if ((cameraTarget.position - _goalPos).sqrMagnitude < 0.01f)
        {
            cameraTarget.position = _goalPos;
            _moving = false;

            if (debugLog)
                Debug.Log($"[Spectator] MoveDone => slot={CurrentSlot} pos={_goalPos}");
        }
    }

    public void Spectate(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= points.Length)
        {
            if (debugLog) Debug.LogWarning($"[Spectator] Spectate ignored: invalid slotIndex={slotIndex}");
            return;
        }

        var p = points[slotIndex];
        if (p == null)
        {
            if (debugLog) Debug.LogWarning($"[Spectator] Spectate ignored: points[{slotIndex}] is null");
            return;
        }

        if (cameraTarget == null)
        {
            Debug.LogWarning("[Spectator] cameraTarget not assigned.");
            return;
        }

        CurrentSlot = slotIndex;
        _goalPos = p.position;
        SpectateContext.ViewLaneId = slotIndex;

        // ✅ 여기서 “관전 레인 스냅샷 요청” 같이 쏘면 제일 깔끔함
        /*if (LaneCombatBridge.Instance != null)
        {
            if (debugLog) Debug.Log($"[Spectator] RequestSyncLane => lane={slotIndex}");
            LaneCombatBridge.Instance.RequestSyncLane(slotIndex);
        }
        else
        {
            if (debugLog) Debug.LogWarning("[Spectator] LaneCombatBridge.Instance is null (cannot RequestSyncLane)");
        }*/

        if (!smoothMove)
        {
            cameraTarget.position = _goalPos;
            _moving = false;
        }
        else
        {
            _moving = true;
        }
        
        if (TowerCombatBridge.Instance != null)
            TowerCombatBridge.Instance.RequestSyncLane(SpectateContext.ViewLaneId);

        if (LaneCombatBridge.Instance != null)
            LaneCombatBridge.Instance.RequestSyncLane(SpectateContext.ViewLaneId);
    }

    public void SpectateNext()
    {
        for (int i = 1; i <= points.Length; i++)
        {
            int idx = (CurrentSlot + i) % points.Length;
            if (points[idx] != null) { Spectate(idx); return; }
        }
    }

    public void SpectatePrev()
    {
        for (int i = 1; i <= points.Length; i++)
        {
            int idx = (CurrentSlot - i + points.Length) % points.Length;
            if (points[idx] != null) { Spectate(idx); return; }
        }
    }
}
