using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public enum BgmChannel { Title, Lobby, InGame }

public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer Groups (routing only)")]
    [SerializeField] private AudioMixerGroup bgmGroup;
    [SerializeField] private AudioMixerGroup sfxUIGroup;
    [SerializeField] private AudioMixerGroup sfxFireGroup;
    [SerializeField] private AudioMixerGroup sfxImpactGroup;

    [Header("BGM Cues")]
    [SerializeField] private AudioCue bgmTitleCue;
    [SerializeField] private AudioCue bgmLobbyCue;
    [SerializeField] private AudioCue bgmInGameCue;
    [SerializeField] private AudioCue bgmBossCue;
    
    [SerializeField] private AudioCue winCue;
    [SerializeField] private AudioCue loseCue;

    [SerializeField] private AudioCue towerBuildCue;
    [SerializeField] private AudioCue towerSellCue;
    [SerializeField] private AudioCue traitChangeCue;

    [SerializeField] private AudioCue panelOpenCue;
    [SerializeField] private AudioCue panelCloseCue;
    
    [SerializeField] private AudioCue augmentOpenCue;
    [SerializeField] private AudioCue augmentPickCue;
    [SerializeField] private AudioCue augmentCloseCue;

    [Header("Trait Cues")]
    [SerializeField] private AudioCue chainCue;
    [SerializeField] private AudioCue smallExplosionCue;

    [Header("BGM Crossfade")]
    [SerializeField] private float bgmFadeSec = 1.2f;

    [Header("SFX Pool")]
    [SerializeField] private int initialPoolSize = 24;
    [SerializeField] private int maxPoolSize = 48;

    [Header("Fire Policy")]
    [SerializeField] private float fireRateLimitPerSec = 10f;
    [SerializeField] private int fireMaxSimultaneous = 6;
    [SerializeField] private float fireMaxHearDistance = 22f;
    [SerializeField] private float firePerKeyCooldown = 0.08f;

    // volumes (0..1)
    public float Master01 { get; private set; } = 1f;
    public float Bgm01    { get; private set; } = 1f;
    public float Sfx01    { get; private set; } = 1f;

    private AudioSource _bgmA, _bgmB;
    private bool _bgmAActive = true;

    // “현재 각 소스가 어떤 Cue로 재생 중인지” 저장 (cue.volume 반영용)
    private AudioCue _cueA, _cueB;

    private CancellationTokenSource _bgmFadeCts;

    private readonly Queue<PooledAudioSource> _pool = new();
    private readonly List<(PooledAudioSource src, bool isFire)> _active = new();

    private float _fireToken;
    private float _fireTokenCap;
    private float _lastTime;
    private readonly Dictionary<int, float> _fireKeyNextAllowed = new();
    private int _fireConcurrent;
    private Transform _listenerTr;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        AudioSettingsStore.Load();
        ApplyVolumes(AudioSettingsStore.Master01, AudioSettingsStore.Bgm01, AudioSettingsStore.Sfx01);

        SetupBgmSources();
        WarmPool();

        _fireTokenCap = Mathf.Max(1f, fireRateLimitPerSec);
        _fireToken = _fireTokenCap;
        _lastTime = Time.unscaledTime;

        TraitProcessor.ChainCue = chainCue;
        TraitProcessor.SmallExplosionCue = smallExplosionCue;

        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.activeSceneChanged -= OnSceneChanged;

        CancelBgmFade();
    }

    private void Update()
    {
        UpdateTokens();
        RecycleFinished();
        RefreshListener();
        // BGM 볼륨은 페이드 루프에서 매 프레임 반영됨
    }

    // ---------------- Volumes ----------------
    public void ApplyVolumes(float master01, float bgm01, float sfx01)
    {
        Master01 = Mathf.Clamp01(master01);
        Bgm01    = Mathf.Clamp01(bgm01);
        Sfx01    = Mathf.Clamp01(sfx01);

        AudioSettingsStore.Save(Master01, Bgm01, Sfx01);

        // 페이드 중이 아니면 현재 재생 중인 BGM에 즉시 반영
        ApplyBgmInstant();
    }

    private void ApplyBgmInstant()
    {
        float user = Master01 * Bgm01;

        if (_bgmA != null && _bgmA.isPlaying)
            _bgmA.volume = user * CueVol(_cueA);

        if (_bgmB != null && _bgmB.isPlaying)
            _bgmB.volume = user * CueVol(_cueB);
    }

    // ---------------- Setup ----------------
    private void SetupBgmSources()
    {
        _bgmA = gameObject.AddComponent<AudioSource>();
        _bgmB = gameObject.AddComponent<AudioSource>();

        foreach (var s in new[] { _bgmA, _bgmB })
        {
            s.playOnAwake = false;
            s.loop = true;
            s.spatialBlend = 0f;
            s.outputAudioMixerGroup = bgmGroup;
            s.volume = 0f;
        }
    }

    private void WarmPool()
    {
        for (int i = 0; i < initialPoolSize; i++)
            _pool.Enqueue(CreateOne());
    }

    private PooledAudioSource CreateOne()
    {
        var go = new GameObject("PooledAudioSource");
        go.transform.SetParent(transform, false);
        return go.AddComponent<PooledAudioSource>();
    }

    private void RefreshListener()
    {
        if (_listenerTr != null) return;
        var listener = FindFirstObjectByType<AudioListener>();
        if (listener != null) _listenerTr = listener.transform;
    }

    // ---------------- BGM ----------------
    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        string n = newScene.name.ToLowerInvariant();
        if (n.Contains("title"))
            PlayBgm(BgmChannel.Title);
        else if (n.Contains("lobby"))
            PlayBgm(BgmChannel.Lobby);
        else
            PlayBgm(BgmChannel.InGame);
    }

    public void PlayBgm(BgmChannel ch)
    {
        AudioCue cue = ch switch
        {
            BgmChannel.Title => bgmTitleCue,
            BgmChannel.Lobby => bgmLobbyCue,
            _ => bgmInGameCue
        };

        PlayBgmCue(cue);
    }

    public void PlayBossBgm()
    {
        PlayBgmCue(bgmBossCue);
    }

    private void PlayBgmCue(AudioCue cue)
    {
        if (cue == null || !cue.IsValid) return;

        var clip = cue.PickClip();
        if (clip == null) return;

        var from = _bgmAActive ? _bgmA : _bgmB;
        var to   = _bgmAActive ? _bgmB : _bgmA;

        // 같은 클립이면 무시
        if (from.clip == clip) return;

        // to 소스에 cue 기록
        if (to == _bgmA) _cueA = cue;
        else _cueB = cue;

        to.clip = clip;
        to.time = 0f;
        to.volume = 0f;
        to.outputAudioMixerGroup = cue.outputGroupOverride != null ? cue.outputGroupOverride : bgmGroup;
        to.Play();

        CancelBgmFade();
        _bgmFadeCts = new CancellationTokenSource();
        CrossfadeAsync(from, to, bgmFadeSec, _bgmFadeCts.Token).Forget();

        _bgmAActive = !_bgmAActive;
    }

    private void CancelBgmFade()
    {
        if (_bgmFadeCts == null) return;
        _bgmFadeCts.Cancel();
        _bgmFadeCts.Dispose();
        _bgmFadeCts = null;
    }

    private async UniTaskVoid CrossfadeAsync(AudioSource from, AudioSource to, float sec, CancellationToken ct)
    {
        AudioCue fromCue = (from == _bgmA) ? _cueA : _cueB;
        AudioCue toCue   = (to   == _bgmA) ? _cueA : _cueB;

        float dur = Mathf.Max(0.0001f, sec);
        float t = 0f;

        try
        {
            while (t < dur)
            {
                ct.ThrowIfCancellationRequested();

                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);

                float user = Master01 * Bgm01;
                
                float fromScale = user * CueVol(fromCue);
                float toScale   = user * CueVol(toCue);

                if (from != null) from.volume = Mathf.Lerp(fromScale, 0f, k);
                if (to   != null) to.volume   = Mathf.Lerp(0f, toScale, k);

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (from != null)
        {
            from.Stop();
            from.clip = null;
            from.volume = 0f;
        }

        if (to != null)
        {
            float user = Master01 * Bgm01;
            to.volume = user * CueVol(toCue);
        }
    }

    // AudioCue.volume이 0..1로 막혀있으면 “작은 음원”을 못 키움.
    // 일단 0..2 허용(빠른 해결). (더 좋은 건 gainDb 추가 + 리미터)
    private static float CueVol(AudioCue cue)
    {
        if (cue == null) return 1f;
        float v = cue.volume;
        if (v < 0f) v = 0f;
        if (v > 2f) v = 2f;
        return v;
    }

    // ---------------- Tokens / Pool ----------------
    private void UpdateTokens()
    {
        float now = Time.unscaledTime;
        float dt = Mathf.Max(0f, now - _lastTime);
        _lastTime = now;

        _fireToken = Mathf.Min(_fireTokenCap, _fireToken + fireRateLimitPerSec * dt);
    }

    private void RecycleFinished()
    {
        float now = Time.unscaledTime;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var (p, isFire) = _active[i];
            if (p == null || p.IsDone(now))
            {
                if (p != null) p.Release();
                if (isFire) _fireConcurrent = Mathf.Max(0, _fireConcurrent - 1);
                if (p != null) _pool.Enqueue(p);
                _active.RemoveAt(i);
            }
        }
    }

    private PooledAudioSource GetFromPool()
    {
        if (_pool.Count > 0) return _pool.Dequeue();
        if (_active.Count + _pool.Count < maxPoolSize) return CreateOne();

        if (_active.Count > 0)
        {
            var (p, isFire) = _active[0];
            if (p != null) p.Release();
            if (isFire) _fireConcurrent = Mathf.Max(0, _fireConcurrent - 1);
            _active.RemoveAt(0);
            return p;
        }
        return null;
    }

    private void ConfigureSpatial(AudioSource src, AudioCue cue)
    {
        bool is3D = cue.spatial == AudioSpatialMode.ThreeD;
        src.spatialBlend = is3D ? cue.spatialBlend3D : 0f;
        src.minDistance = cue.minDistance;
        src.maxDistance = cue.maxDistance;
        src.rolloffMode = AudioRolloffMode.Logarithmic;
    }

    // ---------------- Public SFX API ----------------
    public void PlayUI(AudioCue cue)
    {
        if (cue == null || !cue.IsValid) return;
        PlayInternal(cue, sfxUIGroup, Vector3.zero, is2D: true, isFire: false, spamKey: 0);
    }

    public void PlayImpact(AudioCue cue, Vector3 pos)
    {
        if (cue == null || !cue.IsValid) return;
        PlayInternal(cue, sfxImpactGroup, pos, is2D: false, isFire: false, spamKey: 0);
    }

    public bool PlayFire(AudioCue cue, Vector3 pos, int spamKey)
    {
        if (cue == null || !cue.IsValid) return false;

        if (_listenerTr != null && Vector3.Distance(_listenerTr.position, pos) > fireMaxHearDistance)
            return false;

        float now = Time.unscaledTime;

        if (_fireKeyNextAllowed.TryGetValue(spamKey, out var next) && now < next)
            return false;
        _fireKeyNextAllowed[spamKey] = now + firePerKeyCooldown;

        if (_fireToken < 1f) return false;
        if (_fireConcurrent >= fireMaxSimultaneous) return false;

        _fireToken -= 1f;

        bool ok = PlayInternal(cue, sfxFireGroup, pos, is2D: false, isFire: true, spamKey: spamKey);
        if (ok) _fireConcurrent++;
        return ok;
    }

    private bool PlayInternal(AudioCue cue, AudioMixerGroup fallbackGroup, Vector3 pos, bool is2D, bool isFire, int spamKey)
    {
        var clip = cue.PickClip();
        if (clip == null) return false;

        var p = GetFromPool();
        if (p == null) return false;

        var src = p.Source;
        src.outputAudioMixerGroup = cue.outputGroupOverride != null ? cue.outputGroupOverride : fallbackGroup;

        if (is2D)
        {
            src.spatialBlend = 0f;
        }
        else
        {
            p.transform.position = pos;
            ConfigureSpatial(src, cue);
        }

        float pitch = cue.PickPitch();
        
        float baseVol = CueVol(cue);

        float userVol = Master01 * Sfx01;
        float finalVol = baseVol * userVol;

        p.Play(clip, finalVol, pitch, Time.unscaledTime);
        _active.Add((p, isFire));
        return true;
    }
    
    public void SetBossBgm(bool on)
    {
        if (on)
        {
            PlayBossBgm();
        }
        else
        {
            PlayBgm(BgmChannel.InGame);
        }
    }
    
    public void PlayWin()  => PlayUI(winCue);
    public void PlayLose() => PlayUI(loseCue);
    public void PlayPanelOpen()  => PlayUI(panelOpenCue);
    public void PlayPanelClose() => PlayUI(panelCloseCue);

    public void PlayAugmentOpen() => PlayUI(augmentOpenCue);
    public void PlayAugmentPick()  => PlayUI(augmentPickCue);
    public void PlayAugmentClose() => PlayUI(augmentCloseCue);

    public void PlayTowerBuild(Vector3 pos) => PlayImpact(towerBuildCue, pos);
    public void PlayTowerSell(Vector3 pos)  => PlayImpact(towerSellCue, pos);
    public void PlayTraitChange(Vector3 pos)=> PlayImpact(traitChangeCue, pos);
}
