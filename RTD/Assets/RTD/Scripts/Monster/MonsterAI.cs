using UnityEngine;

public class MonsterAI : MonoBehaviour
{
    [Header("Move Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float stoppingDistance = 0.05f;
    
    [Header("Stats")]
    public int maxHp = 20;
    private int _currentHp;

    private int _currentIndex = 0;
    private Transform _currentTarget;

    private void Awake()
    {
        _currentHp = maxHp;
    }
    
    private void Start()
    {
        if (GridManager.Instance == null)
        {
            Debug.LogError("GridManager 인스턴스가 없습니다.");
            enabled = false;
            return;
        }

        if (GridManager.Instance.WaypointCount == 0)
        {
            Debug.LogError("Waypoint가 하나도 없습니다. pathTiles를 설정했는지 확인하세요.");
            enabled = false;
            return;
        }
        
        Transform startPoint = GridManager.Instance.GetWaypoint(0);
        transform.position = startPoint.position;
        
        _currentIndex = 1;
        SetNextTarget();
    }

    private void Update()
    {
        MoveAlongPath();
    }

    private void MoveAlongPath()
    {
        if (_currentTarget == null)
            return;

        Vector3 targetPos = _currentTarget.position;
        Vector3 dir = targetPos - transform.position;
        
        dir.y = 0f;

        float distanceToTarget = dir.magnitude;
        float moveThisFrame = moveSpeed * Time.deltaTime;
        
        if (distanceToTarget <= moveThisFrame + stoppingDistance)
        {
            _currentIndex++;
            SetNextTarget();
            return;
        }

        Vector3 move = dir.normalized * moveThisFrame;
        transform.position += move;
        
        if (dir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    private void SetNextTarget()
    {
        if (_currentIndex >= GridManager.Instance.WaypointCount)
        {
            ReachGoal();
            return;
        }

        _currentTarget = GridManager.Instance.GetWaypoint(_currentIndex);
    }

    private void ReachGoal()
    {
        Destroy(gameObject);
    }
    
    public void TakeDamage(int amount)
    {
        _currentHp -= amount;

        if (_currentHp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddGold(5);
        }

        Destroy(gameObject);
    }
}
