using UnityEngine;

public static class AudioSettingsStore
{
    private const string K_Master = "opt_audio_master";
    private const string K_Bgm    = "opt_audio_bgm";
    private const string K_Sfx    = "opt_audio_sfx";

    public static float Master01 { get; private set; } = 1f;
    public static float Bgm01    { get; private set; } = 1f;
    public static float Sfx01    { get; private set; } = 1f;

    public static void Load()
    {
        Master01 = PlayerPrefs.GetFloat(K_Master, 1f);
        Bgm01    = PlayerPrefs.GetFloat(K_Bgm,    1f);
        Sfx01    = PlayerPrefs.GetFloat(K_Sfx,    1f);
    }

    public static void Save(float master01, float bgm01, float sfx01)
    {
        Master01 = Mathf.Clamp01(master01);
        Bgm01    = Mathf.Clamp01(bgm01);
        Sfx01    = Mathf.Clamp01(sfx01);

        PlayerPrefs.SetFloat(K_Master, Master01);
        PlayerPrefs.SetFloat(K_Bgm,    Bgm01);
        PlayerPrefs.SetFloat(K_Sfx,    Sfx01);
        PlayerPrefs.Save();
    }
}