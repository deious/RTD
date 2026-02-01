using UnityEngine;

public static class NicknameStore
{
    private const string KEY = "RTD_NICKNAME";
    public const int MaxLen = 16;

    public static string Get()
    {
        return PlayerPrefs.GetString(KEY, "");
    }

    public static void Set(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length > MaxLen) name = name.Substring(0, MaxLen);

        PlayerPrefs.SetString(KEY, name);
        PlayerPrefs.Save();
    }

    public static bool HasValue()
    {
        return !string.IsNullOrWhiteSpace(Get());
    }

    public static string Sanitize(string name, string fallback = "Player")
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) name = fallback;
        if (name.Length > MaxLen) name = name.Substring(0, MaxLen);
        return name;
    }
}
