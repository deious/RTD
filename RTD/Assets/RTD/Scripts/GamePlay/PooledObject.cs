using UnityEngine;

public class PooledObject : MonoBehaviour
{
    public GameObject PrefabKey { get; private set; }
    public bool InPool { get; set; }
    
    public int LastReleaseFrame { get; set; }
    public string LastReleaseStack { get; set; }
    public void SetKey(GameObject key) => PrefabKey = key;
}