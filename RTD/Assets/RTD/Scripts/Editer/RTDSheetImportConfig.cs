using UnityEngine;

[CreateAssetMenu(menuName = "RTD/Tools/Sheet Import Config", fileName = "RTD_SheetImportConfig")]
public class RTDSheetImportConfig : ScriptableObject
{
    [Header("Google Sheet CSV URLs (Publish to web -> CSV)")]
    public string monstersArchetypeCsvUrl;
    public string monstersColorCsvUrl;
    public string bossCsvUrl;
    public string wavesCsvUrl;
    public string towersCsvUrl;
    public string augmentsCsvUrl;
    public string traitsCsvUrl;

    [Header("Output Folders")]
    public string archetypeFolder = "Assets/RTD/Data/Monster/Archetype";
    public string colorFolder = "Assets/RTD/Data/Monster/Color";
    public string bossFolder = "Assets/RTD/Data/Monster/Boss";
    public string wavesFolder = "Assets/RTD/Data/Waves";
    
    public string towersFolder = "Assets/RTD/Data/Towers";
    public string augmentsFolder = "Assets/RTD/Data/Augment";
    public string traitsFolder = "Assets/RTD/Data/Trait";

    [Header("Optional: Auto-fill GameRuntime.wavePatterns")]
    public bool autoFillGameRuntimeWavePatterns = true;
}