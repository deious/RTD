public static class SpectatorContext
{
    public static int ViewLaneId = 0;

    public static bool IsViewingMyLane(int myLaneId) => ViewLaneId == myLaneId;
}