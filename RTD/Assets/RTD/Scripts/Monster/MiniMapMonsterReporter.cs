using UnityEngine;

public class MiniMapMonsterReporter : MonoBehaviour, IPoolable
{
    private MiniMapMonsterUIRenderer rendererRef;
    private Transform root;
    private bool inited;
    private bool registered;
    
    private void OnDisable() => Unregister();
    public void OnSpawned() => Register();
    public void OnDespawned() => Unregister();

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
    
    public void SetRenderer(MiniMapMonsterUIRenderer newRenderer)
    {
        if (rendererRef == newRenderer) return;
        
        Unregister();

        rendererRef = newRenderer;
        
        Register();
    }
    
    public void Rebind(MiniMapMonsterUIRenderer newRenderer)
    {
        Unregister();

        rendererRef = newRenderer;

        if (root == null)
            root = transform;

        inited = true;
        Register();
    }
}