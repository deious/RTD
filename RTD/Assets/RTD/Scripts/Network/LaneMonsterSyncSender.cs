using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class LaneMonsterSyncSender : MonoBehaviour
{
    [SerializeField, Tooltip("초당 전송 횟수 (10~20 권장)")]
    private float sendRate = 15f;

    [SerializeField, Tooltip("내 레인의 몬스터만 전송")]
    private bool onlyMyLane = true;

    [SerializeField, Tooltip("전송 시 최대 몬스터 수(안전장치)")]
    private int maxMonsters = 512;

    private readonly List<MonsterAI> _buffer = new List<MonsterAI>(512);

    private void OnEnable()
    {
        Loop().Forget();
    }

    private async UniTaskVoid Loop()
    {
        while (enabled)
        {
            float interval = (sendRate <= 0f) ? 0.1f : (1f / sendRate);
            await UniTask.Delay(TimeSpan.FromSeconds(interval), DelayType.DeltaTime, PlayerLoopTiming.Update);

            var bridge = LaneCombatBridge.Instance;
            if (bridge == null) continue;

            // 내 레인만 “실제 시뮬레이션”을 돌린다는 전제라면,
            // 모든 클라가 자기 레인 몬스터를 서버로 보냄(서버가 릴레이)
            int laneId = MultiplayerContext.MyLaneId;

            BuildSnapshot(laneId, out var packed);
            if (packed == null) continue;

            bridge.SyncMonstersServerRpc(laneId, packed);
        }
    }

    private void BuildSnapshot(int laneId, out byte[] packed)
    {
        packed = null;

        _buffer.Clear();

        // 성능 개선 여지: 실제 운영은 스포너가 살아있는 몬스터 리스트를 들고 있는 게 베스트.
        // 일단은 간단하게 씬에서 긁음.
        var all = FindObjectsByType<MonsterAI>(FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            var m = all[i];
            if (m == null) continue;
            if (m.IsEnded) continue;
            if (m.NetId < 0) continue;

            if (onlyMyLane && m.WorldSlotId != laneId)
                continue;

            _buffer.Add(m);
            if (_buffer.Count >= maxMonsters) break;
        }

        // 포맷:
        // [count:int]
        // 반복:
        //  netId:int
        //  x:float y:float z:float
        //  hp:int
        //  hpMax:int
        int count = _buffer.Count;
        int bytesPer = 4 + 12 + 4 + 4 + 4;
        int bytes = 4 + count * bytesPer;

        packed = new byte[bytes];
        int o = 0;

        WriteInt(packed, ref o, count);

        for (int i = 0; i < count; i++)
        {
            var m = _buffer[i];
            Vector3 p = m.transform.position;

            WriteInt(packed, ref o, m.NetId);
            WriteFloat(packed, ref o, p.x);
            WriteFloat(packed, ref o, p.y);
            WriteFloat(packed, ref o, p.z);
            WriteFloat(packed, ref o, m.CurrentHp);
            WriteFloat(packed, ref o, m.MaxHp);
            WriteFloat(packed, ref o, m.ShieldHp);
        }
    }

    private static void WriteInt(byte[] data, ref int o, int v)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(v), 0, data, o, 4);
        o += 4;
    }

    private static void WriteFloat(byte[] data, ref int o, float v)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(v), 0, data, o, 4);
        o += 4;
    }
}
