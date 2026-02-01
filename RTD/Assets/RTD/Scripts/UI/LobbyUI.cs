using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    [Header("Top")]
    [SerializeField] private Button btnCreate;     // 방 생성 버튼(없으면 null 가능)
    [SerializeField] private Button btnLeave;      // 타이틀로/나가기

    [Header("Room")]
    [SerializeField] private TMP_Text txtRoomCode;
    [SerializeField] private Button btnCopyCode;   // 있으면 연결, 없으면 null 가능

    [Header("Join")]
    [SerializeField] private TMP_InputField inputJoinCode;
    [SerializeField] private Button btnJoin;

    [Header("Bottom")]
    [SerializeField] private Button btnReady;
    [SerializeField] private Button btnStart;
    
    [Header("Nickname")]
    [SerializeField] private TMP_InputField inputNickname;
    [SerializeField] private Button btnSaveNickname;

    [Header("Status")]
    [SerializeField] private TMP_Text txtStatus;

    [Header("Players (fixed 4 slots)")]
    [SerializeField] private LobbyPlayerSlot[] slots = new LobbyPlayerSlot[4];

    [Header("Logic")]
    [SerializeField] private LobbySystem lobbySystem;

    private void Awake()
    {
        ApplyIdleUI();

        if (btnCreate != null) 
            btnCreate.onClick.AddListener(() => lobbySystem.CreateLobby().Forget());
        if (btnJoin != null) 
            btnJoin.onClick.AddListener(() => lobbySystem.JoinByCode(GetJoinCode()).Forget());
        if (btnReady != null) 
            btnReady.onClick.AddListener(() => lobbySystem.ToggleReady().Forget());
        if (btnStart != null) 
            btnStart.onClick.AddListener(() => lobbySystem.StartGame().Forget());
        if (btnLeave != null) 
            btnLeave.onClick.AddListener(() => lobbySystem.OnClickLeave().Forget());
        if (btnCopyCode != null) 
            btnCopyCode.onClick.AddListener(CopyRoomCodeToClipboard);
        
        if (inputNickname != null)
            inputNickname.text = NicknameStore.Get();
        
        if (btnSaveNickname != null)
            btnSaveNickname.onClick.AddListener(OnClickSaveNickname);
    }

    private string GetJoinCode()
        => inputJoinCode != null ? inputJoinCode.text.Trim() : string.Empty;

    private void CopyRoomCodeToClipboard()
    {
        if (txtRoomCode == null) return;
        var code = txtRoomCode.text?.Trim();
        if (string.IsNullOrEmpty(code) || code == "------") return;
        GUIUtility.systemCopyBuffer = code;
        SetStatus("코드가 복사됐습니다.");
    }
    
    private void OnClickSaveNickname()
    {
        string raw = inputNickname != null ? inputNickname.text : "";
        string nick = NicknameStore.Sanitize(raw, "Player");

        NicknameStore.Set(nick);

        if (inputNickname != null)
            inputNickname.text = nick;

        SetStatus($"닉네임 저장됨: {nick}");
        
        if (lobbySystem != null)
            lobbySystem.RefreshMyNameFromLocal();
        
        if (ChatManager.Instance != null)
            ChatManager.Instance.SetNickname(nick);

        if (ChatNetworkBridge.Instance != null && ChatNetworkBridge.Instance.IsSpawned)
            ChatNetworkBridge.Instance.RegisterMyNicknameNow();
    }


    public void ApplyIdleUI()
    {
        if (txtRoomCode != null) 
            txtRoomCode.text = "------";
        if (btnCopyCode != null) 
            btnCopyCode.gameObject.SetActive(false);

        if (btnCreate != null)
        {
            btnCreate.gameObject.SetActive(true); 
            btnCreate.interactable = true;
        }

        if (inputJoinCode != null)
        {
            inputJoinCode.gameObject.SetActive(true); 
            inputJoinCode.interactable = true;
        }

        if (btnJoin != null)
        {
            btnJoin.gameObject.SetActive(true);
            btnJoin.interactable = true;
        }

        if (btnReady != null)
        {
            btnReady.gameObject.SetActive(true);
            btnReady.interactable = false;
        }

        if (btnStart != null)
        {
            btnStart.gameObject.SetActive(true);
            btnStart.interactable = false;
        }

        SetStatus("방을 생성하거나 코드를 입력해 참가하세요.");

        ClearSlots();
    }

    public void ApplyInSessionUI(string roomCode, bool isHost)
    {
        if (txtRoomCode != null) 
            txtRoomCode.text = string.IsNullOrEmpty(roomCode) ? "------" : roomCode;
        if (btnCopyCode != null) 
            btnCopyCode.gameObject.SetActive(isHost && !string.IsNullOrEmpty(roomCode));
        
        if (btnCreate != null) 
            btnCreate.interactable = false;
        if (inputJoinCode != null) 
            inputJoinCode.interactable = false;
        if (btnJoin != null) 
            btnJoin.interactable = false;

        if (btnReady != null) 
            btnReady.interactable = true;
        if (btnStart != null) 
            btnStart.interactable = isHost;

        SetStatus(isHost ? "방이 생성되었습니다. 플레이어를 기다리는 중..." : "방에 참가했습니다.");
    }

    public void SetReadyButtonText(bool isReady)
    {
        if (btnReady == null) return;
        var t = btnReady.GetComponentInChildren<TMP_Text>();
        if (t != null) 
            t.text = isReady ? "준비 취소" : "준비";
    }

    public void SetStartInteractable(bool interactable)
    {
        if (btnStart != null) 
            btnStart.interactable = interactable;
    }

    public void SetStatus(string msg)
    {
        if (txtStatus != null) 
            txtStatus.text = msg;
    }

    public void ClearSlots()
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null) 
                slots[i].SetEmpty();
        }
    }

    public void SetSlot(int index, string name, bool isHost, bool isReady)
    {
        if (slots == null || index < 0 || index >= slots.Length) return;
        if (slots[index] == null) return;
        slots[index].SetPlayer(name, isHost, isReady);
    }
}
