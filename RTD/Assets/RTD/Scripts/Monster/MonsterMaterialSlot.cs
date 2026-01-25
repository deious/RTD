using UnityEngine;

public class MonsterMaterialSlot : MonoBehaviour
{
    [SerializeField] private Renderer[] targets;

    private void Awake()
    {
        if (targets == null || targets.Length == 0)
            targets = GetComponentsInChildren<Renderer>(true);
    }
    public void Apply(Material mat)
    {
        if (mat == null) return;

        for (int i = 0; i < targets.Length; i++)
        {
            var r = targets[i];
            if (r == null) 
                continue;
            
            var mats = r.sharedMaterials;
            for (int k = 0; k < mats.Length; k++)
                mats[k] = mat;

            r.sharedMaterials = mats;
        }
    }
}