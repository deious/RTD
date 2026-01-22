using UnityEngine;

public class MiniMapMonsterReporter : MonoBehaviour
{
    private MiniMapMonsterUIRenderer _renderer;
    private Transform _root;
    private bool _inited;
    private bool _done;

    public void Init(MiniMapMonsterUIRenderer renderer, Transform monsterRoot)
    {
        _renderer = renderer;
        _root = monsterRoot;
        _inited = true;

        _renderer?.Register(_root);
    }

    private void OnDestroy()
    {
        if (_done) return;
        _done = true;

        if (_inited)
            _renderer?.Unregister(_root);
    }
}