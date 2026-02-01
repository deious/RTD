using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public sealed class ChatManager : MonoBehaviour
{
    public static ChatManager Instance { get; private set; }

    public event Action<ChatMessage> OnMessageReceived;
    public string MyNickname { get; private set; }

    [Header("History")]
    [SerializeField] private int maxHistory = 200;

    private readonly List<ChatMessage> _history = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        MyNickname = NicknameStore.Get();
        if (string.IsNullOrWhiteSpace(MyNickname))
            MyNickname = "Player";
    }

    public IReadOnlyList<ChatMessage> GetHistory() => _history;
    
    public void Send(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        text = text.Trim();
        
        if (IsNetworkChatAvailable(out var bridge))
        {
            bridge.SendToServer(text);
            return;
        }

        // 싱글/오프라인: 로컬 에코
        var msg = new ChatMessage("Me", text, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        ReceiveLocal(msg);
    }
    
    public void ReceiveLocal(ChatMessage msg)
    {
        if (string.IsNullOrWhiteSpace(msg.Text))
            return;

        _history.Add(msg);

        if (maxHistory > 0 && _history.Count > maxHistory)
        {
            int removeCount = _history.Count - maxHistory;
            _history.RemoveRange(0, removeCount);
        }

        OnMessageReceived?.Invoke(msg);
    }

    private static bool IsNetworkChatAvailable(out ChatNetworkBridge bridge)
    {
        bridge = null;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
            return false;
        
        bridge = ChatNetworkBridge.Instance;
        return bridge != null && bridge.IsSpawned;
    }
    
    public void SetNickname(string newNick)
    {
        MyNickname = newNick;
        NicknameStore.Set(newNick);

        // 이미 네트워크 연결돼 있으면 서버에 다시 등록
        if (ChatNetworkBridge.Instance != null && ChatNetworkBridge.Instance.IsSpawned)
        {
            ChatNetworkBridge.Instance.SendToServer(
                $"* 닉네임이 {newNick}(으)로 변경되었습니다 *"
            );
        }
    }
}

[Serializable]
public struct ChatMessage
{
    public string Sender;
    public string Text;
    public long UtcMs;

    public ChatMessage(string sender, string text, long utcMs)
    {
        Sender = sender;
        Text = text;
        UtcMs = utcMs;
    }

    public override string ToString() => $"[{Sender}] {Text}";
}
