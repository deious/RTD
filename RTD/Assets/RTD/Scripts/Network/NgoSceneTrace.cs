using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Cysharp.Threading.Tasks;

public class NgoSceneTrace : MonoBehaviour
{
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnUnitySceneLoaded;
        AttachNgoHooksAsync().Forget();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnUnitySceneLoaded;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnNgoSceneEvent;
        }
    }

    private async UniTaskVoid AttachNgoHooksAsync()
    {
        // Instantiate 직후 타이밍 보호
        await UniTask.Yield();

        var nm = NetworkManager.Singleton;
        if (nm == null || nm.SceneManager == null)
        {
            Debug.LogWarning("[TRACE] NGO not ready yet (NetworkManager/SceneManager null)");
            return;
        }

        nm.SceneManager.OnSceneEvent += OnNgoSceneEvent;
        Debug.Log("[TRACE] NGO Scene hooks attached");
    }

    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[TRACE] Unity sceneLoaded: {scene.name} mode={mode}");
    }

    private void OnNgoSceneEvent(SceneEvent sceneEvent)
    {
        Debug.Log($"[TRACE] NGO SceneEvent: {sceneEvent.SceneEventType} scene={sceneEvent.SceneName} clientId={sceneEvent.ClientId}");
    }
}