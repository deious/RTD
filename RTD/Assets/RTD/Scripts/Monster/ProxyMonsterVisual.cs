using UnityEngine;

public class ProxyMonsterVisual : MonoBehaviour
{
    [SerializeField] private Transform shieldVfxPrefab;
    private Transform _shieldVfxInstance;
    
    public void SetShieldActive(bool active)
    {
        if (shieldVfxPrefab == null) return;

        if (active)
        {
            if (_shieldVfxInstance == null)
                _shieldVfxInstance = Instantiate(shieldVfxPrefab, transform);

            _shieldVfxInstance.gameObject.SetActive(true);
        }
        else
        {
            if (_shieldVfxInstance != null)
                _shieldVfxInstance.gameObject.SetActive(false);
        }
    }
}