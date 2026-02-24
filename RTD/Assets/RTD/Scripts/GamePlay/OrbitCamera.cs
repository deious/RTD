using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    [SerializeField] private bool enableKeyboardPan = false;
    [SerializeField] private bool enableEdgePan = true;
    [SerializeField, Min(1f)] private float edgePanThresholdPx = 24f;
    
    [Header("Mode")]
    [SerializeField] private bool useTransformAsInitialView = false;
    
    [Header("UI Block")]
    [SerializeField] private ScrollRect blockZoomWhenPointerOver;
    
    [SerializeField] private TMP_InputField chatInput;

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
        if (UIState.BlockWorldInput) 
            return;
        
        if (IsTyping())
            return;
        
        if (target == null) return;

        if (!_lockInput)
        {
            HandleRotation();
            HandleZoom();
            HandlePan();
        }

        UpdateCameraPosition();
    }

    private bool IsTyping()
    {
        if (chatInput != null && chatInput.isFocused) 
            return true;
        
        var go = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (go == null)
            return false;
        return go.GetComponent<TMP_InputField>() != null;
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
        
        if (IsPointerOver(blockZoomWhenPointerOver))
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
        
        if (enableKeyboardPan && Keyboard.current != null)
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
        
        if (enableEdgePan && Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector3 right = transform.right;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            float edge = Mathf.Max(1f, edgePanThresholdPx);

            if (mousePos.x <= edge)
                panDir += -right;
            else if (mousePos.x >= Screen.width - edge)
                panDir += right;

            if (mousePos.y <= edge)
                panDir += -forward;
            else if (mousePos.y >= Screen.height - edge)
                panDir += forward;
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
    
    private static bool IsPointerOver(ScrollRect sr)
    {
        if (sr == null) return false;
        if (EventSystem.current == null) return false;
        if (Mouse.current == null) return false;

        var pointer = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);

        for (int i = 0; i < results.Count; i++)
        {
            var go = results[i].gameObject;
            if (go == null) continue;
            
            if (go == sr.gameObject) return true;
            if (go.transform.IsChildOf(sr.transform)) return true;
        }

        return false;
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
