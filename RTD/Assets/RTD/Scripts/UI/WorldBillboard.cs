using UnityEngine;

public class WorldBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool lockY = true;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            return;

        Vector3 toCam = targetCamera.transform.position - transform.position;

        if (lockY)
            toCam.y = 0f;

        if (toCam.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
    }
}