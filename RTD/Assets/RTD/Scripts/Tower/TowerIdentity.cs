using UnityEngine;

public class TowerIdentity : MonoBehaviour
{
    public int OwnerLaneId { get; private set; }
    public int TowerId { get; private set; }
    
    public Vector2Int GridPos { get; private set; }

    public void Init(int ownerLaneId, int towerId, Vector2Int gridPos)
    {
        OwnerLaneId = ownerLaneId;
        TowerId = towerId;
        GridPos = gridPos;
    }
}