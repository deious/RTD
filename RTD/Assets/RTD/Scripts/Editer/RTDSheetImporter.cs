#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using UnityEditor;
using UnityEngine;
using RTD.Scripts.GamePlay.Wave;

public static class RTDSheetImporter
{
    [MenuItem("RTD/Import/Sync Sheets -> ScriptableObjects")]
    public static void SyncAll()
    {
        var config = FindConfig();
        if (config == null)
        {
            Debug.LogError("[SheetImporter] RTD_SheetImportConfig asset not found. Create it via Create > RTD > Tools > Sheet Import Config");
            return;
        }

        try
        {
            EnsureFolder(config.archetypeFolder);
            EnsureFolder(config.colorFolder);
            EnsureFolder(config.bossFolder);
            EnsureFolder(config.wavesFolder);
            EnsureFolder(config.towersFolder);
            EnsureFolder(config.augmentsFolder);
            EnsureFolder(config.traitsFolder);

            var archetypes = ImportArchetypes(config);
            var colors = ImportColors(config);
            var bosses = ImportBosses(config);

            ImportTowers(config);
            ImportAugments(config);
            ImportTraits(config);
            
            ImportWaves(config, archetypes, colors, bosses);

            if (config.autoFillGameRuntimeWavePatterns)
                AutoFillGameRuntimeWavePatterns(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SheetImporter] Sync complete.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SheetImporter] Failed: {e}");
        }
    }

    private static RTDSheetImportConfig FindConfig()
    {
        var guids = AssetDatabase.FindAssets("t:RTDSheetImportConfig");
        if (guids == null || guids.Length == 0) return null;
        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<RTDSheetImportConfig>(path);
    }

    private static Dictionary<string, MonsterArchetypeSO> ImportArchetypes(RTDSheetImportConfig config)
    {
        var dict = new Dictionary<string, MonsterArchetypeSO>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(config.monstersArchetypeCsvUrl))
        {
            Debug.LogWarning("[SheetImporter] monstersArchetypeCsvUrl is empty. Skipping archetypes.");
            return LoadAllById<MonsterArchetypeSO>(config.archetypeFolder, so => so.id, dict);
        }

        string csv = DownloadText(config.monstersArchetypeCsvUrl);
        var rows = Csv.Read(csv);

        foreach (var r in rows)
        {
            string id = r.Get("id");
            if (string.IsNullOrEmpty(id)) continue;

            string assetPath = $"{config.archetypeFolder}/MonsterArchetype_{SanitizeFileName(id)}.asset";
            var so = LoadOrCreate<MonsterArchetypeSO>(assetPath);

            so.id = id;
            so.prefab = LoadAssetAtPathSafe<GameObject>(r.Get("prefabPath"));
            so.baseHp = r.GetInt("baseHp", so.baseHp);
            so.baseMoveSpeed = r.GetFloat("baseMoveSpeed", so.baseMoveSpeed);
            so.baseShieldHp = r.GetInt("baseShieldHp", so.baseShieldHp);
            so.canBeBossCandidate = r.GetBool("canBeBossCandidate", so.canBeBossCandidate);

            EditorUtility.SetDirty(so);
            dict[id] = so;
        }

        return LoadAllById(config.archetypeFolder, so => so.id, dict);
    }
    
    private static Dictionary<string, MonsterColorSO> ImportColors(RTDSheetImportConfig config)
    {
        var dict = new Dictionary<string, MonsterColorSO>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(config.monstersColorCsvUrl))
        {
            Debug.LogWarning("[SheetImporter] monstersColorCsvUrl is empty. Skipping colors.");
            return LoadAllById<MonsterColorSO>(config.colorFolder, so => so.id, dict);
        }

        string csv = DownloadText(config.monstersColorCsvUrl);
        var rows = Csv.Read(csv);

        foreach (var r in rows)
        {
            string id = r.Get("id");
            if (string.IsNullOrEmpty(id)) continue;

            string assetPath = $"{config.colorFolder}/MonsterColor_{SanitizeFileName(id)}.asset";
            var so = LoadOrCreate<MonsterColorSO>(assetPath);

            so.id = id;
            so.material = LoadAssetAtPathSafe<Material>(r.Get("materialPath"));
            so.hpMul = r.GetFloat("hpMul", so.hpMul);
            so.speedMul = r.GetFloat("speedMul", so.speedMul);
            so.shieldMul = r.GetFloat("shieldMul", so.shieldMul);

            EditorUtility.SetDirty(so);
            dict[id] = so;
        }

        return LoadAllById(config.colorFolder, so => so.id, dict);
    }

    private static Dictionary<string, BossMonsterDataSO> ImportBosses(RTDSheetImportConfig config)
    {
        var dict = new Dictionary<string, BossMonsterDataSO>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(config.bossCsvUrl))
        {
            Debug.LogWarning("[SheetImporter] bossCsvUrl is empty. Skipping bosses.");
            return LoadAllById<BossMonsterDataSO>(config.bossFolder, so => so.bossId, dict);
        }

        string csv = DownloadText(config.bossCsvUrl);
        var rows = Csv.Read(csv);

        foreach (var r in rows)
        {
            string bossId = r.Get("bossId");
            if (string.IsNullOrEmpty(bossId)) continue;

            string assetPath = $"{config.bossFolder}/Boss_{SanitizeFileName(bossId)}.asset";
            var so = LoadOrCreate<BossMonsterDataSO>(assetPath);

            so.bossId = bossId;
            so.prefab = LoadAssetAtPathSafe<GameObject>(r.Get("prefabPath"));
            so.scale = r.GetFloat("scale", so.scale);

            so.maxHp = r.GetInt("maxHp", so.maxHp);
            so.moveSpeed = r.GetFloat("moveSpeed", so.moveSpeed);
            so.shieldHp = r.GetInt("shieldHp", so.shieldHp);

            so.rewardGold = r.GetInt("rewardGold", so.rewardGold);
            so.shakeDuration = r.GetFloat("shakeDuration", so.shakeDuration);
            so.shakeStrength = r.GetFloat("shakeStrength", so.shakeStrength);

            EditorUtility.SetDirty(so);
            dict[bossId] = so;
        }

        return LoadAllById(config.bossFolder, so => so.bossId, dict);
    }
    
    private static void ImportTowers(RTDSheetImportConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.towersCsvUrl))
        {
            Debug.LogWarning("[SheetImporter] towersCsvUrl is empty. Skipping towers.");
            return;
        }
    
        string csv = DownloadText(config.towersCsvUrl);
        var rows = Csv.Read(csv);
    
        foreach (var r in rows)
        {
            string towerId = r.Get("towerId");
            if (string.IsNullOrEmpty(towerId)) continue;
    
            string assetPath = $"{config.towersFolder}/Tower_{SanitizeFileName(towerId)}.asset";
            var so = LoadOrCreate<TowerData>(assetPath);
    
            so.towerId = towerId;
            so.grade = r.GetEnum("grade", so.grade);
            so.displayName = r.Get("displayName");
    
            so.damage = r.GetFloat("damage", so.damage);
            so.attackSpeed = r.GetFloat("attackSpeed", so.attackSpeed);
            so.range = r.GetFloat("range", so.range);
    
            so.buildCost = r.GetInt("buildCost", so.buildCost);
            so.towerPrefab = LoadAssetAtPathSafe<GameObject>(r.Get("towerPrefabPath"));
            
            if (r.Has("gradeColor"))
                so.gradeColor = r.GetColor("gradeColor", so.gradeColor);
    
            EditorUtility.SetDirty(so);
        }
    }

    private static void ImportAugments(RTDSheetImportConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.augmentsCsvUrl))
        {
            Debug.LogWarning("[SheetImporter] augmentsCsvUrl is empty. Skipping augments.");
            return;
        }
    
        string csv = DownloadText(config.augmentsCsvUrl);
        var rows = Csv.Read(csv);
    
        foreach (var r in rows)
        {
            string augmentId = r.Get("augmentId");
            if (string.IsNullOrEmpty(augmentId)) continue;
    
            string assetPath = $"{config.augmentsFolder}/Augment_{SanitizeFileName(augmentId)}.asset";
            var so = LoadOrCreate<AugmentSO>(assetPath);
    
            so.augmentId = augmentId;
            so.title = r.Get("title");
            so.desc = r.Get("desc");
    
            so.target = r.GetEnum("target", so.target);
            so.type = r.GetEnum("type", so.type);
    
            so.value = r.GetFloat("value", so.value);
    
            EditorUtility.SetDirty(so);
        }
    }


    private static void ImportTraits(RTDSheetImportConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.traitsCsvUrl))
        {
            Debug.LogWarning("[SheetImporter] traitsCsvUrl is empty. Skipping traits.");
            return;
        }
    
        string csv = DownloadText(config.traitsCsvUrl);
        var rows = Csv.Read(csv);
    
        foreach (var r in rows)
        {
            var type = r.GetEnum("type", TowerTraitType.Critical);
            var tier = r.GetEnum("tier", TraitTier.None);
    
            string key = $"{type}_{tier}";
            string assetPath = $"{config.traitsFolder}/Trait_{SanitizeFileName(key)}.asset";
            var so = LoadOrCreate<TowerTraitSO>(assetPath);
    
            so.type = type;
            so.tier = tier;
            
            if (r.Has("allowed"))
                so.allowed = r.GetFlags("allowed", so.allowed);
    
            so.traitName = r.Get("traitName");
            so.description = r.Get("description");
    
            so.value = r.GetFloat("value", so.value);
            so.duration = r.GetFloat("duration", so.duration);
            so.range = r.GetFloat("range", so.range);
            so.count = r.GetInt("count", so.count);
    
            EditorUtility.SetDirty(so);
        }
    }

    private static void ImportWaves(
        RTDSheetImportConfig config,
        Dictionary<string, MonsterArchetypeSO> archetypes,
        Dictionary<string, MonsterColorSO> colors,
        Dictionary<string, BossMonsterDataSO> bosses)
    {
        if (string.IsNullOrWhiteSpace(config.wavesCsvUrl))
        {
            Debug.LogWarning("[SheetImporter] wavesCsvUrl is empty. Skipping waves.");
            return;
        }

        string csv = DownloadText(config.wavesCsvUrl);
        var rows = Csv.Read(csv);
        
        var groups = rows
            .Where(r => r.Has("waveIndex"))
            .GroupBy(r => r.GetInt("waveIndex", -1))
            .Where(g => g.Key > 0)
            .OrderBy(g => g.Key);

        foreach (var g in groups)
        {
            int waveIndex = g.Key;

            string assetPath = $"{config.wavesFolder}/WavePattern_{waveIndex:00}.asset";
            var wave = LoadOrCreate<WavePatternSO>(assetPath);

            wave.waveIndex = waveIndex;

            var first = g.First();

            wave.spawnInterval = first.GetFloat("spawnInterval", wave.spawnInterval);
            
            string modStr = first.Get("modifiers");
            wave.modifiers = ParseEnumArray<WaveModifierType>(modStr, '|');

            wave.isBossWave = first.GetBool("isBossWave", wave.isBossWave);
            string bossId = first.Get("bossId");
            if (wave.isBossWave && !string.IsNullOrEmpty(bossId) && bosses.TryGetValue(bossId, out var bossSo))
                wave.bossData = bossSo;
            else
                wave.bossData = null;
            
            var spawnList = new List<WaveSpawnEntry>();

            foreach (var r in g)
            {
                string archetypeId = r.Get("archetypeId");
                string colorId = r.Get("colorId");
                int count = r.GetInt("count", 0);

                if (count <= 0) continue;
                if (string.IsNullOrEmpty(archetypeId)) continue;

                if (!archetypes.TryGetValue(archetypeId, out var archSo) || archSo == null)
                {
                    Debug.LogError($"[SheetImporter:Waves] Wave {waveIndex}: archetypeId '{archetypeId}' not found.");
                    continue;
                }

                MonsterColorSO colorSo = null;
                if (!string.IsNullOrEmpty(colorId))
                {
                    colors.TryGetValue(colorId, out colorSo);
                    if (colorSo == null)
                        Debug.LogWarning($"[SheetImporter:Waves] Wave {waveIndex}: colorId '{colorId}' not found (will be null).");
                }

                spawnList.Add(new WaveSpawnEntry
                {
                    archetype = archSo,
                    color = colorSo,
                    count = count
                });
            }

            wave.spawns = spawnList.ToArray();

            EditorUtility.SetDirty(wave);
        }
    }
    
    private static void AutoFillGameRuntimeWavePatterns(RTDSheetImportConfig config)
    {
        var runtimes = UnityEngine.Object.FindObjectsByType<GameRuntime>(FindObjectsSortMode.None);
        if (runtimes == null || runtimes.Length == 0)
        {
            Debug.LogWarning("[SheetImporter] GameRuntime not found in opened scene. (Auto fill skipped)");
            return;
        }

        var waves = LoadAllAssetsInFolder<WavePatternSO>(config.wavesFolder)
            .Where(w => w != null)
            .OrderBy(w => w.waveIndex)
            .ToArray();

        foreach (var rt in runtimes)
        {
            if (rt == null) continue;

            var so = new SerializedObject(rt);
            var prop = so.FindProperty("wavePatterns");
            if (prop == null || !prop.isArray)
            {
                Debug.LogWarning("[SheetImporter] GameRuntime has no serialized field 'wavePatterns' (Auto fill skipped)");
                continue;
            }

            prop.arraySize = waves.Length;
            for (int i = 0; i < waves.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = waves[i];

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rt);
        }

        Debug.Log($"[SheetImporter] Auto-filled GameRuntime.wavePatterns: {waves.Length} waves.");
    }
    
    private static string DownloadText(string url)
    {
        using var wc = new WebClient();
        wc.Encoding = System.Text.Encoding.UTF8;
        return wc.DownloadString(url);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = "Assets";
        string[] parts = folder.Split('/');

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{parent}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(parent, parts[i]);
            parent = next;
        }
    }

    private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
    {
        var so = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (so != null) return so;

        so = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(so, assetPath);
        return so;
    }

    private static T LoadAssetAtPathSafe<T>(string path) where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static string SanitizeFileName(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }

    private static T[] LoadAllAssetsInFolder<T>(string folder) where T : UnityEngine.Object
    {
        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
        var list = new List<T>(guids.Length);
        foreach (var g in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var a = AssetDatabase.LoadAssetAtPath<T>(p);
            if (a != null) list.Add(a);
        }
        return list.ToArray();
    }

    private static Dictionary<string, T> LoadAllById<T>(
        string folder,
        Func<T, string> getId,
        Dictionary<string, T> dst) where T : UnityEngine.Object
    {
        var assets = LoadAllAssetsInFolder<T>(folder);
        foreach (var a in assets)
        {
            if (a == null) continue;
            string id = getId(a);
            if (string.IsNullOrEmpty(id)) continue;
            dst[id] = a;
        }
        return dst;
    }

    private static TEnum[] ParseEnumArray<TEnum>(string raw, char sep) where TEnum : struct
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<TEnum>();

        var parts = raw.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        var list = new List<TEnum>(parts.Length);

        foreach (var p in parts)
        {
            string token = p.Trim();
            if (string.IsNullOrEmpty(token)) continue;

            if (Enum.TryParse<TEnum>(token, ignoreCase: true, out var v))
                list.Add(v);
            else if (!token.Equals("none", StringComparison.OrdinalIgnoreCase))
                Debug.LogWarning($"[SheetImporter] Enum parse failed: {typeof(TEnum).Name} <- '{token}'");
        }

        return list.ToArray();
    }
    
    private class CsvRow
    {
        private readonly Dictionary<string, string> _map;
        public CsvRow(Dictionary<string, string> map) => _map = map;

        public bool Has(string key) => _map.ContainsKey(key);

        public string Get(string key)
        {
            if (_map.TryGetValue(key, out var v)) return v?.Trim();
            return "";
        }

        public int GetInt(string key, int def)
        {
            var s = Get(key);
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        public float GetFloat(string key, float def)
        {
            var s = Get(key);
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        public bool GetBool(string key, bool def)
        {
            var s = Get(key);
            if (string.IsNullOrEmpty(s)) return def;
            if (s.Equals("1")) return true;
            if (s.Equals("0")) return false;
            return bool.TryParse(s, out var v) ? v : def;
        }
        
        public TEnum GetEnum<TEnum>(string key, TEnum def) where TEnum : struct
        {
            var s = Get(key);
            if (string.IsNullOrWhiteSpace(s)) return def;
        
            if (Enum.TryParse<TEnum>(s.Trim(), ignoreCase: true, out var v))
                return v;
        
            Debug.LogWarning($"[SheetImporter] Enum parse failed: {typeof(TEnum).Name} <- '{s}'");
            return def;
        }
        
        public TowerTraitAllowed GetFlags(string key, TowerTraitAllowed def)
        {
            var s = Get(key);
            if (string.IsNullOrWhiteSpace(s)) return def;
        
            if (s.Trim().Equals("All", StringComparison.OrdinalIgnoreCase))
                return TowerTraitAllowed.All;
        
            TowerTraitAllowed result = TowerTraitAllowed.None;
        
            var parts = s.Split('|', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var token = p.Trim();
                if (string.IsNullOrEmpty(token)) continue;
        
                if (Enum.TryParse<TowerTraitAllowed>(token, ignoreCase: true, out var v))
                    result |= v;
                else if (!token.Equals("none", StringComparison.OrdinalIgnoreCase))
                    Debug.LogWarning($"[SheetImporter] Flags parse failed: TowerTraitAllowed <- '{token}'");
            }
        
            return result;
        }
        
        public Color GetColor(string key, Color def)
        {
            var s = Get(key);
            if (string.IsNullOrWhiteSpace(s)) return def;
        
            s = s.Trim();
        
            if (s.StartsWith("#"))
            {
                if (ColorUtility.TryParseHtmlString(s, out var c))
                    return c;
        
                Debug.LogWarning($"[SheetImporter] Color parse failed (hex): '{s}'");
                return def;
            }
        
            var parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return def;
        
            float Parse01(string t)
            {
                if (!float.TryParse(t.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    return 0f;
        
                if (v > 1.0f) v /= 255f;
                return Mathf.Clamp01(v);
            }
        
            float r = Parse01(parts[0]);
            float g = Parse01(parts[1]);
            float b = Parse01(parts[2]);
            float a = (parts.Length >= 4) ? Parse01(parts[3]) : 1f;
        
            return new Color(r, g, b, a);
        }

    }

    private static class Csv
    {
        public static List<CsvRow> Read(string text)
        {
            var lines = SplitLines(text);
            var rows = new List<CsvRow>();
            if (lines.Count < 2) return rows;

            var header = SplitCsvLine(lines[0]);
            
            if (header.Count > 0 && !string.IsNullOrEmpty(header[0]))
                header[0] = header[0].TrimStart('\uFEFF');

            for (int i = 1; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var cols = SplitCsvLine(lines[i]);

                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < header.Count; c++)
                {
                    string key = header[c].Trim();
                    if (string.IsNullOrEmpty(key)) continue;

                    string val = (c < cols.Count) ? cols[c] : "";
                    map[key] = val;
                }

                rows.Add(new CsvRow(map));
            }

            return rows;
        }

        private static List<string> SplitLines(string s)
        {
            var list = new List<string>();
            using var sr = new StringReader(s);
            string line;
            while ((line = sr.ReadLine()) != null)
                list.Add(line);
            return list;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var res = new List<string>();
            if (line == null) return res;

            bool inQuotes = false;
            var cur = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];

                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cur.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (ch == ',' && !inQuotes)
                {
                    res.Add(cur.ToString());
                    cur.Clear();
                }
                else
                {
                    cur.Append(ch);
                }
            }

            res.Add(cur.ToString());
            return res;
        }
    }
}
#endif
