using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class EscPauseBinder : MonoBehaviour
{
    private bool _prevDown;
    private int _lastToggleFrame = -1;

    private void Update()
    {
        if (SceneManager.GetActiveScene().name != "InGame")
            return;

        var kb = Keyboard.current;
        if (kb == null) return;

        bool down = kb.escapeKey.isPressed;

        if (down && !_prevDown)
        {
            if (_lastToggleFrame != Time.frameCount)
            {
                _lastToggleFrame = Time.frameCount;
                UIManager.Instance?.TogglePausePanel();
            }
        }

        _prevDown = down;
    }
}