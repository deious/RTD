public static class TowerGradeHelper
{
    public static bool TryGetNextGrade(TowerGrade current, out TowerGrade next)
    {
        switch (current)
        {
            case TowerGrade.Normal:
                next = TowerGrade.Rare;
                return true;
            case TowerGrade.Rare:
                next = TowerGrade.Epic;
                return true;
            case TowerGrade.Epic:
                next = TowerGrade.Legendary;
                return true;
            default:
                next = current;
                return false;
        }
    }
}

