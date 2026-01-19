using System.Collections.Generic;
using UnityEngine;

public class WaypointPath : MonoBehaviour
{
    [Tooltip("몬스터가 따라갈 웨이포인트 순서.")]
    public List<Transform> points = new List<Transform>();

    public int Count => points == null ? 0 : points.Count;

    public Transform Get(int index)
    {
        if (points == null) return null;
        if (index < 0 || index >= points.Count) return null;
        return points[index];
    }

#if UNITY_EDITOR
    [Header("Gizmo")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color gizmoColor = Color.yellow;
    [SerializeField] private float sphereRadius = 0.12f;
    [SerializeField] private float yOffset = 0.3f;

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        if (points == null || points.Count == 0) return;

        Gizmos.color = gizmoColor;

        Vector3 prev = Vector3.zero;
        bool hasPrev = false;

        for (int i = 0; i < points.Count; i++)
        {
            Transform t = points[i];
            if (t == null) continue;

            Vector3 p = t.position + Vector3.up * yOffset;
            Gizmos.DrawSphere(p, sphereRadius);
            
            if (hasPrev)
            {
                Gizmos.DrawLine(prev, p);
            }

            prev = p;
            hasPrev = true;
        }
    }
#endif
}
