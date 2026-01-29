using UnityEngine;
using UnityEngine.InputSystem;

public class TileSelector : MonoBehaviour
{
    [Header("Raycast Settings")]
    public LayerMask tileLayerMask;
    public float raycastDistance = 200f;

    private Camera _mainCamera;
    private GridTile _hoveredTile;

    private void Awake()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogError("Main Camera not found! 카메라에 'MainCamera' 태그가 붙어 있는지 확인하세요.");
        }
    }

    private void Update()
    {
        if (_mainCamera == null) return;

        HandleHover();
    }

    private void HandleHover()
    {
        Vector2 mousePos = Vector2.zero;

        if (Mouse.current != null)
        {
            mousePos = Mouse.current.position.ReadValue();
        }
        else
        {
            return;
        }

        Ray ray = _mainCamera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, tileLayerMask))
        {
            GridTile tile = hit.collider.GetComponent<GridTile>();

            if (tile != _hoveredTile)
            {
                // 이전 타일 하이라이트 해제
                if (_hoveredTile != null)
                    _hoveredTile.SetHighlight(false);

                // 새 타일 하이라이트
                _hoveredTile = tile;
                if (_hoveredTile != null)
                    _hoveredTile.SetHighlight(true);
            }
        }
        else
        {
            // 아무 것도 가리키지 않으면 하이라이트 제거
            if (_hoveredTile != null)
            {
                _hoveredTile.SetHighlight(false);
                _hoveredTile = null;
            }
        }
    }
}