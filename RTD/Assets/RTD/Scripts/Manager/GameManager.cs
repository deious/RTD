using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    private int gold = 100;
    private int life = 10;
    private int currentWave = 1;
    private int maxWave = 20;

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
        
        if (keyboard.gKey.wasPressedThisFrame)
        {
            gold += 10;
            UIManager.Instance.UpdateGold(gold);
        }
        
        if (keyboard.hKey.wasPressedThisFrame)
        {
            life--;
            UIManager.Instance.UpdateLife(life);
        }
        
        if (keyboard.jKey.wasPressedThisFrame)
        {
            currentWave++;
            UIManager.Instance.UpdateWave(currentWave, maxWave);
        }
    }
}