using RTD.Scripts.Network;
using UnityEngine;
using Unity.Netcode;

public class InGameEntryHook : MonoBehaviour
{
    [SerializeField] private MiniMapUIController miniMapUI;

    private void Awake()
    {
        AppFlowManager.OnSceneBecameActive += HandleSceneActive;
    }

    private void OnDestroy()
    {
        AppFlowManager.OnSceneBecameActive -= HandleSceneActive;
    }

    private void HandleSceneActive(string sceneName)
    {
        if (sceneName != "InGame") return;

        int count = MultiplayerContext.PlayersCount;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            count = Mathf.Clamp(nm.ConnectedClientsList.Count, 1, 4);
            MultiplayerContext.SetPlayersCount(count);
            MultiplayerContext.ResolveMyLaneIdFromNgo();
        }
        else
        {
            MultiplayerContext.SetPlayersCount(1);
        }
        
        if (miniMapUI == null)
            miniMapUI = FindFirstObjectByType<MiniMapUIController>();

        if (miniMapUI != null)
            miniMapUI.SetPlayerCount(MultiplayerContext.PlayersCount);

        Debug.Log($"[InGameEntry] players={MultiplayerContext.PlayersCount} myLane={MultiplayerContext.MyLaneId}");
    }
}