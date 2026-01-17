using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class UIRaycastDebugger : MonoBehaviour
{
    [SerializeField] private bool logEveryClick = true;

    private readonly List<RaycastResult> _results = new();

    void Update()
    {
        if (!logEveryClick) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            DebugUIUnderPointer();
        }
    }

    private void DebugUIUnderPointer()
    {
        if (EventSystem.current == null)
        {
            Debug.LogError("[UIRaycastDebugger] EventSystem.current is null");
            return;
        }

        _results.Clear();

        var pe = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };

        EventSystem.current.RaycastAll(pe, _results);

        if (_results.Count == 0)
        {
            Debug.Log("[UIRaycastDebugger] RaycastAll = 0 (UI 안맞음)");
            return;
        }

        // 가장 위에 맞은 UI
        var top = _results[0];
        Debug.Log($"[UIRaycastDebugger] TOP: {top.gameObject.name} / module={top.module} / depth={top.depth} / sortingOrder={top.sortingOrder}");

        // 필요하면 전체도 보고 싶을 때
        // for (int i = 0; i < _results.Count; i++)
        //     Debug.Log($"  [{i}] {_results[i].gameObject.name}");
    }
}