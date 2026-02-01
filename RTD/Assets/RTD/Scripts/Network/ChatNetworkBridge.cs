using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public sealed class ChatNetworkBridge : NetworkBehaviour
{
    public static ChatNetworkBridge Instance { get; private set; }
    
    private readonly Dictionary<ulong, string> _nameByClientId = new();
    
    private string _lastRegisteredNick;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            DontDestroyOnLoad(gameObject);
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            Debug.Log("[ChatNetworkBridge] DontDestroyOnLoad applied");
        }
        
        if (IsClient)
        {
            RegisterMyNicknameNow();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager != null)
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        _nameByClientId.Remove(clientId);
    }
    
    public void RegisterMyNicknameNow()
    {
        if (!IsClient) return;
        if (!IsSpawned) return;

        string nick = GetMyNicknameLocal();
        
        if (!string.IsNullOrEmpty(_lastRegisteredNick) && _lastRegisteredNick == nick)
            return;

        _lastRegisteredNick = nick;
        RegisterNicknameServerRpc(nick);
    }

    private string GetMyNicknameLocal()
    {
        string nick = NicknameStore.Get();
        if (!string.IsNullOrWhiteSpace(nick))
            return NicknameStore.Sanitize(nick, $"Player-{NetworkManager.LocalClientId}");

        if (ChatManager.Instance != null && !string.IsNullOrWhiteSpace(ChatManager.Instance.MyNickname))
            return NicknameStore.Sanitize(ChatManager.Instance.MyNickname, $"Player-{NetworkManager.LocalClientId}");

        return $"Player-{NetworkManager.LocalClientId}";
    }

    [ServerRpc(RequireOwnership = false)]
    private void RegisterNicknameServerRpc(string nickname, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        // 서버에서도 sanitize
        nickname = NicknameStore.Sanitize(nickname, $"Player-{senderId}");

        _nameByClientId[senderId] = nickname;

        Debug.Log($"[ChatBridge] Nickname registered: {senderId} -> {nickname}");
    }
    
    public void SendToServer(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        SendChatServerRpc(text.Trim());
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendChatServerRpc(string text, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (!_nameByClientId.TryGetValue(senderId, out var senderName))
            senderName = $"Player-{senderId}";

        long utcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        BroadcastChatClientRpc(senderName, text, utcMs);
    }

    [ClientRpc]
    private void BroadcastChatClientRpc(string senderName, string text, long utcMs)
    {
        if (ChatManager.Instance == null)
        {
            var go = new GameObject("ChatManager");
            go.AddComponent<ChatManager>();
        }

        ChatManager.Instance.ReceiveLocal(new ChatMessage(senderName, text, utcMs));
    }
}
