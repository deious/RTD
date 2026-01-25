using UnityEngine;

public class MiniMapMonsterReporter : MonoBehaviour, IPoolable
{
    private MiniMapMonsterUIRenderer rendererRef;
    private Transform root;
    private bool inited;
    private bool registered;

    public void Init(MiniMapMonsterUIRenderer renderer, Transform monsterRoot)
    {
        rendererRef = renderer;
        root = monsterRoot;
        inited = true;

        Register();
    }

    private void Register()
    {
        if (!inited) return;
        if (registered) return;
        if (rendererRef == null || root == null) return;

        registered = true;
        rendererRef.Register(root);
    }

    private void Unregister()
    {
        if (!registered) return;
        registered = false;

        if (rendererRef != null && root != null)
            rendererRef.Unregister(root);
    }

    public void OnSpawned()
    {
        Register();
    }

    public void OnDespawned()
    {
        Unregister();
    }

    private void OnDisable()
    {
        Unregister();
    }
}