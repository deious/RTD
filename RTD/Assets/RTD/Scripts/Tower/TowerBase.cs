using UnityEngine;

public abstract class TowerBase : MonoBehaviour
{
    [Header("Stats")]
    public float range = 3f;
    public float attackInterval = 1f;
    public int damage = 5;

    [Header("Visual")]
    [SerializeField] private GameObject rangeVisual;
    
    [Header("Data")]
    [SerializeField] private TowerData data;

    private Renderer[] renderers;
    protected float attackTimer;
    
    public GridTile CurrentTile { get; private set; }

    protected virtual void Start()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        
        ApplyDataIfAny();
        ApplyRangeVisual();
        ApplyVisual();
    }

    protected virtual void Update()
    {
        attackTimer += Time.deltaTime;

        if (attackTimer >= attackInterval)
        {
            attackTimer = 0f;
            Attack();
        }
    }
    
    protected abstract void Attack();
    
    protected virtual MonsterAI FindTarget()
    {
        MonsterAI[] monsters = FindObjectsOfType<MonsterAI>();

        MonsterAI closest = null;
        float closestDist = Mathf.Infinity;
        Vector3 myPos = transform.position;

        foreach (var m in monsters)
        {
            if (!m.isActiveAndEnabled) continue;

            float dist = Vector3.Distance(myPos, m.transform.position);
            if (dist <= range && dist < closestDist)
            {
                closest = m;
                closestDist = dist;
            }
        }

        return closest;
    }
    
    public void SetSelected(bool selected)
    {
        if (rangeVisual != null)
            rangeVisual.SetActive(selected);
    }
    
    private void ApplyDataIfAny()
    {
        if (data == null) 
            return;
        
        range = data.range;
        attackInterval = 1f / Mathf.Max(0.0001f, data.attackSpeed);
        damage = Mathf.RoundToInt(data.damage);
    }

    private void ApplyRangeVisual()
    {
        if (rangeVisual == null) 
            return;

        Vector3 s = rangeVisual.transform.localScale;
        rangeVisual.transform.localScale = new Vector3(range * 2f, s.y, range * 2f);
        rangeVisual.SetActive(false);
    }
    
    private void ApplyVisual()
    {
        if (data == null || renderers == null)
            return;

        foreach (var r in renderers)
        {
            if (r.material.HasProperty("_Color"))
                r.material.color = data.gradeColor;
        }
    }

    public void SetTile(GridTile tile)
    {
        if (CurrentTile == tile)
            return;
        
        if (CurrentTile != null)
            CurrentTile.ClearTower(this);

        CurrentTile = tile;
        
        if (CurrentTile != null)
            CurrentTile.SetTower(this);
    }
    
    public TowerData GetData()
    {
        return data;
    }
    
    public void SetData(TowerData newData)
    {
        if (newData == null)
            return;

        data = newData;
        ApplyDataIfAny();
        ApplyRangeVisual();
        ApplyVisual();
    }

    private void OnDestroy()
    {
        if (CurrentTile != null)
        {
            CurrentTile.ClearTower(this);
            CurrentTile = null;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}