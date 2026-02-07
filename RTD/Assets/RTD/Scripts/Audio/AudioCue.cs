using UnityEngine;
using UnityEngine.Audio;

public enum AudioSpatialMode { TwoD, ThreeD }

[CreateAssetMenu(menuName = "RTD/Audio/AudioCue")]
public class AudioCue : ScriptableObject
{
    public AudioClip[] clips;
    public AudioMixerGroup outputGroupOverride;

    [Range(0f, 1f)] public float volume = 1f;
    public float minPitch = 1f;
    public float maxPitch = 1f;

    public AudioSpatialMode spatial = AudioSpatialMode.ThreeD;
    [Range(0f, 1f)] public float spatialBlend3D = 1f;
    public float minDistance = 2f;
    public float maxDistance = 25f;

    public bool IsValid => clips != null && clips.Length > 0;

    public AudioClip PickClip() => IsValid ? clips[Random.Range(0, clips.Length)] : null;

    public float PickPitch()
    {
        float a = Mathf.Min(minPitch, maxPitch);
        float b = Mathf.Max(minPitch, maxPitch);
        return Random.Range(a, b);
    }
}