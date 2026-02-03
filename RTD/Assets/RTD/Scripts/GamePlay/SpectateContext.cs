public static class SpectateContext
{
    public static int ViewLaneId = 0;

    public static bool IsViewingMyLane(int myLaneId) => ViewLaneId == myLaneId;
}