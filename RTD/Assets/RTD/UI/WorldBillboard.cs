using UnityEngine;

public class WorldBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            return;

        // Look at camera
        transform.forward = (transform.position - targetCamera.transform.position).normalized;
    }
}