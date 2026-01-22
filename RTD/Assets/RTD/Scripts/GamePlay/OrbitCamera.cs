using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;            // 카메라가 바라볼 중심
    public Vector3 targetOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Distance")]
    public float distance = 15f;
    public float minDistance = 5f;
    public float maxDistance = 25f;
    public float zoomSpeed = 5f;

    [Header("Rotation")]
    public float rotationSpeed = 150f;
    public float minPitch = 20f;
    public float maxPitch = 75f;

    [Header("Panning")]
    public float panSpeed = 10f;
    
    [Header("Mode")]
    [SerializeField] private bool useTransformAsInitialView = false;

    private float _yaw = 45f;
    private float _pitch = 45f;
    private bool _lockInput;
    
    private Vector3 _externalPosOffset;
    
    public float Yaw => _yaw;
    public float Pitch => _pitch;
    public float Distance => distance;
    
    private void Start()
    {
        if (target == null)
        {
            GameObject temp = new GameObject("CameraTarget");
            temp.transform.position = Vector3.zero;
            target = temp.transform;
        }

        if (useTransformAsInitialView)
        {
            Vector3 dir = transform.position - (target.position + targetOffset);
            distance = dir.magnitude;

            if (distance > 0.01f)
            {
                Vector3 dirNorm = dir.normalized;
                _pitch = Mathf.Asin(dirNorm.y) * Mathf.Rad2Deg;
                _yaw = Mathf.Atan2(dirNorm.x, dirNorm.z) * Mathf.Rad2Deg;
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (!_lockInput)
        {
            HandleRotation();
            HandleZoom();
            HandlePan();
        }

        UpdateCameraPosition();
    }

    private void HandleRotation()
    {
        if (Mouse.current == null) 
            return;
        
        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();

            _yaw += delta.x * rotationSpeed * Time.deltaTime * 0.02f;
            _pitch -= delta.y * rotationSpeed * Time.deltaTime * 0.02f;

            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }
    }

    private void HandleZoom()
    {
        if (Mouse.current == null) 
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float zoomDelta = -scroll * zoomSpeed * 0.01f;
            distance = Mathf.Clamp(distance + zoomDelta, minDistance, maxDistance);
        }
    }

    private void HandlePan()
    {
        Vector3 panDir = Vector3.zero;
        
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) panDir += Vector3.forward;
            if (Keyboard.current.sKey.isPressed) panDir += Vector3.back;
            if (Keyboard.current.aKey.isPressed) panDir += Vector3.left;
            if (Keyboard.current.dKey.isPressed) panDir += Vector3.right;
        }
        
        if (Mouse.current != null && Mouse.current.middleButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            Vector3 right = transform.right;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            
            panDir += (-right * delta.x + -forward * delta.y) * 0.01f;
        }

        if (panDir.sqrMagnitude > 0.0001f)
        {
            Vector3 dir = panDir;

            if (dir.sqrMagnitude > 0.0001f)
                dir.Normalize();

            Vector3 pan = dir * (panSpeed * Time.deltaTime);
            pan.y = 0f;
            target.position += pan;
        }
    }

    private void UpdateCameraPosition()
    {
        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 offset = rot * new Vector3(0f, 0f, -distance);
        
        transform.position = target.position + targetOffset + offset + _externalPosOffset;
        transform.LookAt(target.position + targetOffset);
        
        _externalPosOffset = Vector3.zero;
    }
    
    [ContextMenu("Print Camera View")]
    private void PrintView()
    {
        Debug.Log($"Yaw={_yaw:F2}, Pitch={_pitch:F2}, Dist={distance:F2}, Target={target.position}");
    }
    
    public void AddPositionOffset(Vector3 offset)
    {
        _externalPosOffset += offset;
    }
    
    public void SetInitialView(
        Vector3 targetPos,
        float yaw,
        float pitch,
        float dist)
    {
        target.position = targetPos;
        _yaw = yaw;
        _pitch = pitch;
        distance = dist;

        UpdateCameraPosition();
    }
    
    public void SetView(Vector3 targetPos, float yaw, float pitch, float dist)
    {
        if (target != null) target.position = targetPos;
        _yaw = yaw;
        _pitch = pitch;
        distance = dist;
        UpdateCameraPosition();
    }
    
    public async Cysharp.Threading.Tasks.UniTask PlayIntroToView(
        Vector3 startTargetPos, float startYaw, float startPitch, float startDist,
        Vector3 endTargetPos,   float endYaw,   float endPitch,   float endDist,
        float duration)
    {
        SetView(startTargetPos, startYaw, startPitch, startDist);

        float t = 0f;
        duration = Mathf.Max(0.01f, duration);
        
        float yawFrom = startYaw;
        float yawTo = endYaw;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            float yaw = Mathf.LerpAngle(yawFrom, yawTo, s);
            float pitch = Mathf.Lerp(startPitch, endPitch, s);
            float dist = Mathf.Lerp(startDist, endDist, s);
            Vector3 tp = Vector3.Lerp(startTargetPos, endTargetPos, s);

            SetView(tp, yaw, pitch, dist);

            await Cysharp.Threading.Tasks.UniTask.Yield();
        }
        
        SetView(endTargetPos, endYaw, endPitch, endDist);
    }
    
    public void SetInputLock(bool locked) => _lockInput = locked;
}
