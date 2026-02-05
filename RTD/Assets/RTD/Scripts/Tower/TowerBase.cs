using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using RTD.Scripts.Network;

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
    
    [Header("Projectile")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeed = 18f;
    [SerializeField] private float projectileLifeTime = 3f;
    
    [Header("Spawn FX")]
    [SerializeField] private bool playSpawnPop = true;
    [SerializeField] private float spawnPopDuration = 0.14f;
    [SerializeField] private float spawnPopScaleMul = 1.18f;
    [SerializeField] private AnimationCurve spawnPopCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField] private ParticleSystem spawnParticlePrefab;
    [SerializeField] private Transform spawnParticlePoint;

    private Renderer[] renderers;
    protected float attackTimer;
    private MonsterAI _focusTarget;
    private int _focusStacks;
    
    public GridTile CurrentTile { get; private set; }
    public TowerTraitSO RuntimeTrait { get; private set; }
    public System.Action OnStatsChanged;

    protected virtual void Start()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        
        ApplyDataIfAny();
        ApplyRangeVisual();
        ApplyVisual();
        PlaySpawnFeedback();
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

        int mySlot = MultiplayerContext.MyLaneId;

        MonsterAI closest = null;
        float closestDist = Mathf.Infinity;
        Vector3 myPos = transform.position;

        foreach (var m in monsters)
        {
            if (m == null) continue;
            if (!m.isActiveAndEnabled) continue;
            
            if (m.WorldSlotId != mySlot) continue;

            float dist = Vector3.Distance(myPos, m.transform.position);
            if (dist <= range && dist < closestDist)
            {
                closest = m;
                closestDist = dist;
            }
        }

        return closest;
    }
    
    protected bool TryFireProjectile(MonsterAI target)
    {
        return TryFireProjectile(target, 0f, 0f);
    }

    protected bool TryFireProjectile(MonsterAI target, float splashRadius, float splashRatio)
    {
        if (target == null) return false;
        if (projectilePrefab == null) return false;

        Vector3 spawnPos = (firePoint != null) ? firePoint.position : (transform.position + Vector3.up * 0.7f);
        if (ProjectilePool.Instance == null) return false;

        Projectile proj = ProjectilePool.Instance.Get(projectilePrefab, spawnPos, Quaternion.identity);
        if (proj == null) return false;
        
        IProjectileHitListener listener = this as IProjectileHitListener;

        proj.Init(target, projectileSpeed, damage, projectileLifeTime, this, listener, splashRadius, splashRatio);
        return true;
    }
    
    protected bool TryFireProjectile(MonsterAI target, Vector3 spawnOffset, float splashRadius = 0f, float splashRatio = 0f)
    {
        if (target == null) return false;
        if (projectilePrefab == null) return false;

        if (ProjectilePool.Instance == null) return false;

        Vector3 spawnPos = (firePoint != null) 
            ? firePoint.position 
            : (transform.position + Vector3.up * 0.7f);

        spawnPos += spawnOffset;

        Projectile proj = ProjectilePool.Instance.Get(projectilePrefab, spawnPos, Quaternion.identity);
        if (proj == null) return false;

        IProjectileHitListener listener = this as IProjectileHitListener;
        proj.Init(target, projectileSpeed, damage, projectileLifeTime, this, listener, splashRadius, splashRatio);
        return true;
    }


    public int ApplyHitAndReturnDamage(MonsterAI target, int baseDamage)
    {
        if (target == null) 
            return 0;

        if (target.IsEnded || !target.gameObject.activeInHierarchy)
            return 0;
        
        int dmg = baseDamage;
        
        if (RuntimeTrait != null && RuntimeTrait.type == TowerTraitType.Execute)
        {
            float threshold = Mathf.Clamp01(RuntimeTrait.range);
            float bonus = Mathf.Max(0f, RuntimeTrait.value);
            if (target.Hp01 <= threshold)
                dmg = Mathf.RoundToInt(dmg * (1f + bonus));
        }
        
        if (RuntimeTrait != null && RuntimeTrait.type == TowerTraitType.Focus)
        {
            if (_focusTarget == target) _focusStacks++;
            else { _focusTarget = target; _focusStacks = 1; }

            int maxStacks = Mathf.Max(1, RuntimeTrait.count);
            _focusStacks = Mathf.Min(_focusStacks, maxStacks);

            float per = Mathf.Max(0f, RuntimeTrait.value);
            float mul = 1f + per * (_focusStacks - 1);
            dmg = Mathf.RoundToInt(dmg * mul);
        }
        else
        {
            _focusTarget = null;
            _focusStacks = 0;
        }
        
        dmg = TraitProcessor.ModifyDamage(RuntimeTrait, dmg);
        
        TraitProcessor.ApplyOnHit(RuntimeTrait, this, target, dmg);

        target.TakeDamage(dmg);
        return dmg;
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
        
        if (GameRuntime.Instance != null)
        {
            range += GameRuntime.Instance.TowerRangeAdd;

            float atkMul = GameRuntime.Instance.TowerAttackSpeedMul;
            if (atkMul > 0.0001f)
                attackInterval /= atkMul;

            damage = Mathf.RoundToInt(damage * GameRuntime.Instance.TowerDamageMul);
        }
    }
    
    public void RefreshStats()
    {
        ApplyDataIfAny();
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
    
    public void PlaySpawnFeedback()
    {
        PlaySpawnFeedbackAsync().Forget();
    }
    
    private async UniTaskVoid PlaySpawnFeedbackAsync()
    {
        if (playSpawnPop)
            await SpawnPopAsync();

        if (spawnParticlePrefab != null)
        {
            Vector3 p = (spawnParticlePoint != null)
                ? spawnParticlePoint.position
                : transform.position;

            Instantiate(spawnParticlePrefab, p, Quaternion.identity);
        }
    }
    
    private async UniTask SpawnPopAsync()
    {
        var ct = this.GetCancellationTokenOnDestroy();
        Transform t = transform;

        Vector3 baseScale = t.localScale;
        float targetMul = Mathf.Max(1.0f, spawnPopScaleMul);

        float dur = Mathf.Max(0.01f, spawnPopDuration);
        float timer = 0f;

        try
        {
            while (timer < dur)
            {
                ct.ThrowIfCancellationRequested();
                
                if (t == null)
                    return;

                timer += Time.deltaTime;
                float u = Mathf.Clamp01(timer / dur);

                float k = spawnPopCurve != null
                    ? spawnPopCurve.Evaluate(u)
                    : u;

                float pop;
                if (u < 0.5f)
                    pop = Mathf.Lerp(1f, targetMul, k * 2f);
                else
                    pop = Mathf.Lerp(targetMul, 1f, (k - 0.5f) * 2f);

                t.localScale = baseScale * pop;
                
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }
        finally
        {
            if (t != null)
                t.localScale = baseScale;
        }
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
    
    public void SetTrait(TowerTraitSO trait)
    {
        RuntimeTrait = trait; // 노멀은 null
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