using UnityEngine;

public sealed class PooledAudioSource : MonoBehaviour
{
    public AudioSource Source { get; private set; }
    private float _releaseAt;
    private bool _active;

    private void Awake()
    {
        Source = gameObject.AddComponent<AudioSource>();
        Source.playOnAwake = false;
        Source.loop = false;
    }

    public void Play(AudioClip clip, float volume, float pitch, float now)
    {
        Source.clip = clip;
        Source.volume = volume;
        Source.pitch = pitch;
        Source.Play();

        _active = true;
        _releaseAt = now + Mathf.Max(0.02f, clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch)));
    }

    public bool IsDone(float now) => !_active || now >= _releaseAt || !Source.isPlaying;

    public void Release()
    {
        _active = false;
        Source.Stop();
        Source.clip = null;
    }
}