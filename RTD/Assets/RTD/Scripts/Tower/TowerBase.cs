using TMPro;
using UnityEngine;

public abstract class TowerBase : MonoBehaviour
{
    [Header("Stats")]
    public float range = 3f;
    public float attackInterval = 1f;
    public int damage = 5;

    [Header("Visual")]
    [SerializeField] private GameObject rangeVisual;
    [SerializeField] private Renderer gradeRingRenderer;   // GradeRing의 Renderer
    [SerializeField] private TMP_Text typeLabel;
    [SerializeField, Range(0f, 1f)] private float gradeRingAlpha = 0.75f;
    
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
        if (data == null)
            return;
        
        if (gradeRingRenderer != null)
        {
            var mat = gradeRingRenderer.material;
            if (mat != null)
            {
                Color c = data.gradeColor;
                c.a = gradeRingAlpha;
                
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", c);
                else if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", c);
            }
        }
        
        if (typeLabel != null)
        {
            typeLabel.text = GetTypeShortLabel(data.towerId);
            typeLabel.color = Color.white;
        }
        
        /*
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r != null && r.material != null && r.material.HasProperty("_Color"))
                    r.material.color = Color.gray;
            }
        }
        */
    }
    
    private string GetTypeShortLabel(string towerId)
    {
        if (string.IsNullOrEmpty(towerId))
            return "?";

        int idx = towerId.IndexOf('_');
        string key = (idx >= 0) ? towerId.Substring(0, idx) : towerId;

        key = key.ToLower();
        
        if (key.Contains("basic")) 
            return "B";
        if (key.Contains("cannon")) 
            return "C";
        if (key.Contains("magic")) 
            return "M";
        
        if (key.Length >= 2) 
            return key.Substring(0, 2).ToUpper();
        return key.ToUpper();
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