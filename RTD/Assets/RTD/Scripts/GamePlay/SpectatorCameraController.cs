using UnityEngine;

public class SpectatorCameraController : MonoBehaviour
{
    [Header("Move Target")]
    [SerializeField] private Transform cameraTarget;

    [Header("Spectate Points (P1~P4)")]
    [SerializeField] private Transform[] points = new Transform[4];

    [Header("Move Options")]
    [SerializeField] private bool smoothMove = true;
    [SerializeField] private float smoothSpeed = 12f;

    private Vector3 _goalPos;
    private bool _moving;

    private void Awake()
    {
        if (cameraTarget != null)
            _goalPos = cameraTarget.position;
    }

    private void Update()
    {
        if (!smoothMove || !_moving || cameraTarget == null) return;

        cameraTarget.position = Vector3.Lerp(cameraTarget.position, _goalPos, Time.deltaTime * smoothSpeed);

        if ((cameraTarget.position - _goalPos).sqrMagnitude < 0.01f)
        {
            cameraTarget.position = _goalPos;
            _moving = false;
        }
    }

    public void Spectate(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= points.Length) return;
        var p = points[slotIndex];
        if (p == null) return;

        if (cameraTarget == null)
        {
            Debug.LogWarning("[SpectatorCameraController] cameraTarget not assigned.");
            return;
        }

        _goalPos = p.position;

        if (!smoothMove)
        {
            cameraTarget.position = _goalPos;
            _moving = false;
        }
        else
        {
            _moving = true;
        }
    }
}