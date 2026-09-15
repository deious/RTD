using TMPro;
using UnityEngine;

public class LobbyPlayerSlot : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject textEmpty;
    [SerializeField] private TMP_Text textPlayerName;
    [SerializeField] private GameObject hostTagRoot;
    [SerializeField] private GameObject readyIcon;

    public void SetEmpty()
    {
        if (textEmpty) 
            textEmpty.SetActive(true);

        if (textPlayerName)
        {
            textPlayerName.gameObject.SetActive(false);
            textPlayerName.text = string.Empty;
        }

        if (hostTagRoot) 
            hostTagRoot.SetActive(false);
        
        if (readyIcon) 
            readyIcon.SetActive(false);
    }

    public void SetPlayer(string displayName, bool isHost, bool isReady)
    {
        if (textEmpty) 
            textEmpty.SetActive(false);

        if (textPlayerName)
        {
            textPlayerName.gameObject.SetActive(true);
            textPlayerName.text = displayName;
        }

        if (hostTagRoot) 
            hostTagRoot.SetActive(isHost);
        
        if (readyIcon) 
            readyIcon.SetActive(isReady);
    }
}