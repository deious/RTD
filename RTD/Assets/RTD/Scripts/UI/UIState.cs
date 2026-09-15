public static class UIState
{
    public static bool BlockWorldInput { get; private set; }

    public static void SetBlockWorldInput(bool block)
    {
        BlockWorldInput = block;
    }
}