using UnityEngine;

public class AugmentSystem : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private AugmentChoiceUI augmentUI;

    [Header("Pool")]
    [SerializeField] private AugmentSO[] augmentPool;

    public bool IsChoosing { get; private set; }

    private readonly AugmentSO[] _offered = new AugmentSO[3];
    private System.Action _onChoiceComplete;

    // 누적값 (런 동안 유지)
    private float _towerDamageMul = 1f;
    private float _towerAttackSpeedMul = 1f;
    private float _towerRangeAdd = 0f;

    private float _enemySpeedMul = 1f;
    private float _enemyHpMul = 1f;

    public float TowerDamageMul => _towerDamageMul;
    public float TowerAttackSpeedMul => _towerAttackSpeedMul;
    public float TowerRangeAdd => _towerRangeAdd;

    public float EnemySpeedMul => _enemySpeedMul;
    public float EnemyHpMul => _enemyHpMul;
    
    public void BeginChoice(System.Action onChoiceComplete)
    {
        if (IsChoosing)
            return;

        if (augmentUI == null)
        {
            Debug.LogWarning("[AugmentSystem] augmentUI is null. Skip choice.");
            onChoiceComplete?.Invoke();
            return;
        }

        IsChoosing = true;
        _onChoiceComplete = onChoiceComplete;

        RollOptions(_offered);
        
        augmentUI.Show(_offered, OnPicked);
    }

    private void OnPicked(AugmentSO picked)
    {
        if (!IsChoosing)
            return;

        if (picked != null)
        {
            ApplyAugment(picked);
            RefreshAllTowers();
        }

        if (augmentUI != null)
            augmentUI.Hide();

        IsChoosing = false;

        var cb = _onChoiceComplete;
        _onChoiceComplete = null;
        cb?.Invoke();
    }
    
    private void RefreshAllTowers()
    {
        TowerBase[] towers = FindObjectsOfType<TowerBase>();
        for (int i = 0; i < towers.Length; i++)
        {
            if (towers[i] != null)
                towers[i].RefreshStats();
        }
    }

    private void RollOptions(AugmentSO[] outArr)
    {
        for (int i = 0; i < outArr.Length; i++)
            outArr[i] = null;

        if (augmentPool == null || augmentPool.Length == 0)
            return;

        int filled = 0;
        int safety = 0;

        while (filled < outArr.Length && safety < 200)
        {
            safety++;
            AugmentSO pick = augmentPool[Random.Range(0, augmentPool.Length)];
            if (pick == null) continue;

            bool dup = false;
            for (int j = 0; j < filled; j++)
            {
                if (outArr[j] == pick) { dup = true; break; }
            }
            if (dup) continue;

            outArr[filled] = pick;
            filled++;
        }
    }

    private void ApplyAugment(AugmentSO a)
    {
        if (a == null) return;

        // 타워 버프
        if (a.target == AugmentTarget.Tower)
        {
            switch (a.type)
            {
                case AugmentType.TowerDamageMul:
                    _towerDamageMul *= a.value;
                    break;

                case AugmentType.TowerAttackSpeedMul:
                    _towerAttackSpeedMul *= a.value;
                    break;

                case AugmentType.TowerRangeAdd:
                    _towerRangeAdd += a.value;
                    break;
            }
            return;
        }

        // 적 디버프
        if (a.target == AugmentTarget.Enemy)
        {
            switch (a.type)
            {
                case AugmentType.EnemySpeedMul:
                    _enemySpeedMul *= a.value;
                    break;

                case AugmentType.EnemyHpMul:
                    _enemyHpMul *= a.value;
                    break;
            }
        }
    }
    
    public void ForcePickRandomIfChoosing()
    {
        if (!IsChoosing)
            return;
        
        int count = 0;
        for (int i = 0; i < _offered.Length; i++)
        {
            if (_offered[i] != null) 
                count++;
        }

        if (count <= 0)
        {
            OnPicked(null);
            return;
        }
        
        int idx = Random.Range(0, _offered.Length);
        int safety = 0;
        while (_offered[idx] == null && safety++ < 20)
            idx = Random.Range(0, _offered.Length);

        OnPicked(_offered[idx]);
    }

}
