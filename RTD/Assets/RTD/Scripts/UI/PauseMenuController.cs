using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject root;

    private bool _open;

    public void OnClickGoTitle()
    {
        AppFlowManager.Instance.GoTitle();
    }

    public void OnClickQuit()
    {
        Application.Quit();
    }
}