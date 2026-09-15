using UnityEngine;
using UnityEngine.InputSystem;

public class AudioQuickTest : MonoBehaviour
{
    public AudioCue uiCue;
    public AudioCue fireCue;
    public AudioCue impactCue;

    private Keyboard _kb;

    private void Awake()
    {
        _kb = Keyboard.current;
    }

    private void Update()
    {
        _kb ??= Keyboard.current;
        if (_kb == null) return;

        if (_kb.digit1Key.wasPressedThisFrame)
            AudioManager.Instance?.PlayUI(uiCue);

        if (_kb.digit2Key.wasPressedThisFrame)
            AudioManager.Instance?.PlayFire(fireCue, transform.position, 123);

        if (_kb.digit3Key.wasPressedThisFrame)
            AudioManager.Instance?.PlayImpact(impactCue, transform.position);
    }
}