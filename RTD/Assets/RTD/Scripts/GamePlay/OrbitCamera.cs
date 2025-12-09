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

    private float _yaw = 45f;
    private float _pitch = 45f;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("OrbitCamera: Target이 비었습니다. (0,0,0)에 임시 타겟을 생성합니다.");
            GameObject temp = new GameObject("CameraTarget");
            temp.transform.position = Vector3.zero;
            target = temp.transform;
        }

        // 현재 카메라 위치 기준으로 초기 yaw/pitch 계산
        Vector3 dir = transform.position - (target.position + targetOffset);
        distance = dir.magnitude;

        if (distance > 0.01f)
        {
            Vector3 dirNorm = dir.normalized;
            _pitch = Mathf.Asin(dirNorm.y) * Mathf.Rad2Deg;
            _yaw = Mathf.Atan2(dirNorm.x, dirNorm.z) * Mathf.Rad2Deg;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        HandleRotation();
        HandleZoom();
        HandlePan();

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
            float zoomDelta = -scroll * zoomSpeed * Time.deltaTime * 0.1f;
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
            /*Vector3 pan = panDir.normalized * panSpeed * Time.deltaTime;*/
            Vector3 dir = panDir;

            if (dir.sqrMagnitude > 0.0001f) // normalized보다 직접 Normalize()가 효율적이라 위 코드에서 아래 코드로 변경
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

        transform.position = target.position + targetOffset + offset;
        transform.LookAt(target.position + targetOffset);
    }
}
