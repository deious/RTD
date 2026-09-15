using Cysharp.Threading.Tasks;
using UnityEngine;

public static class MiniMapRebindUtil
{
    public static async UniTask RebindAllMonsterReportersAsync(int playerCount, int delayFrames = 2)
    {
        playerCount = Mathf.Clamp(playerCount, 1, 4);
        
        for (int i = 0; i < Mathf.Max(0, delayFrames); i++)
            await UniTask.NextFrame();

        var reporters = Object.FindObjectsByType<MiniMapMonsterReporter>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int ok = 0, missRenderer = 0;

        for (int i = 0; i < reporters.Length; i++)
        {
            var rep = reporters[i];
            if (rep == null) continue;

            int lane = 0;

            var ai = rep.GetComponentInParent<MonsterAI>();
            if (ai != null) lane = ai.PathLaneIndex;

            if (playerCount == 1) lane = 0;
            else lane = Mathf.Clamp(lane, 0, playerCount - 1);

            if (!MiniMapMonsterUIRenderer.TryGetByLane(lane, out var renderer) || renderer == null)
            {
                missRenderer++;
                continue;
            }

            rep.Rebind(renderer);
            ok++;
        }

        Debug.Log($"[MiniMap] RebindAllMonsterReporters ok={ok} missRenderer={missRenderer} total={reporters.Length} players={playerCount}");
    }
}