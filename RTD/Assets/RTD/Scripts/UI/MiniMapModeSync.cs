using UnityEngine;

public class MiniMapModeSync : MonoBehaviour
{
    [SerializeField] private MiniMapUIController uiController;

    private void Start()
    {
        Apply();
        AppFlowManager.OnSceneBecameActive += _ => Apply();
    }

    private void OnDestroy()
    {
        AppFlowManager.OnSceneBecameActive -= _ => Apply();
    }

    private void Apply()
    {
        if (!uiController) uiController = FindFirstObjectByType<MiniMapUIController>();

        bool isMulti = (AppFlowManager.Instance != null && AppFlowManager.Instance.IsMultiMode);

        if (MiniMapLaneRegistry.Instance != null)
            MiniMapLaneRegistry.Instance.SetForceSoloMode(!isMulti);

        int playerCount = isMulti ? Mathf.Clamp(MultiplayerContext.PlayersCount, 1, 4) : 1;
        if (uiController) uiController.SetPlayerCount(playerCount);
    }
}
