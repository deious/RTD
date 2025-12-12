using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private int startGold = 100;
    [SerializeField] private int startLife = 10;
    [SerializeField] private int startWave = 1;
    [SerializeField] private int maxWave = 20;

    private int gold;
    private int life;
    private int currentWave;

    // ✅ 외부에서 골드 읽을 수 있게 프로퍼티
    public int Gold => gold;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        
        gold = startGold;
        life = startLife;
        currentWave = startWave;
    }

    private void Start()
    {
        UIManager.Instance.UpdateGold(gold);
        UIManager.Instance.UpdateLife(life);
        UIManager.Instance.UpdateWave(currentWave, maxWave);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        // 디버그용 키
        if (keyboard.gKey.wasPressedThisFrame)
        {
            AddGold(10);
        }

        if (keyboard.hKey.wasPressedThisFrame)
        {
            ChangeLife(-1);
        }

        if (keyboard.jKey.wasPressedThisFrame)
        {
            NextWave();
        }
    }
    
    public void AddGold(int amount)
    {
        gold += amount;
        if (gold < 0) 
            gold = 0;

        UIManager.Instance.UpdateGold(gold);
    }

    public void ChangeLife(int amount)
    {
        life += amount;
        UIManager.Instance.UpdateLife(life);
    }

    public void NextWave()
    {
        currentWave++;
        if (currentWave > maxWave)
            currentWave = maxWave;

        UIManager.Instance.UpdateWave(currentWave, maxWave);
    }
}