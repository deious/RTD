using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatPanelController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button sendButton;

    [Header("Scroll")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform contentRoot;

    [Header("Message Item Prefab (TMP Text)")]
    [SerializeField] private TextMeshProUGUI messageTextPrefab;

    private void OnEnable()
    {
        if (AppFlowManager.Instance == null || !AppFlowManager.Instance.IsMultiMode)
        {
            gameObject.SetActive(false);
            return;
        }

        if (ChatManager.Instance == null)
        {
            var go = new GameObject("ChatManager");
            go.AddComponent<ChatManager>();
        }
        
        if (sendButton != null)
            sendButton.onClick.AddListener(OnClickSend);

        if (inputField != null)
            inputField.onSubmit.AddListener(OnSubmit);
        
        ChatManager.Instance.OnMessageReceived += HandleMessage;
        
        var history = ChatManager.Instance.GetHistory();
        for (int i = 0; i < history.Count; i++)
            AddMessageToUI(history[i]);

        ScrollToBottom();
    }

    private void OnDisable()
    {
        if (ChatManager.Instance != null)
            ChatManager.Instance.OnMessageReceived -= HandleMessage;

        if (sendButton != null)
            sendButton.onClick.RemoveListener(OnClickSend);

        if (inputField != null)
            inputField.onSubmit.RemoveListener(OnSubmit);
    }

    private void OnSubmit(string _)
    {
        TrySend();
    }

    private void OnClickSend()
    {
        TrySend();
    }

    private void TrySend()
    {
        if (ChatManager.Instance == null) return;
        if (inputField == null) return;

        string text = inputField.text;
        if (string.IsNullOrWhiteSpace(text)) return;

        ChatManager.Instance.Send(text);

        inputField.text = "";
        inputField.ActivateInputField();
    }

    private void HandleMessage(ChatMessage msg)
    {
        AddMessageToUI(msg);
        ScrollToBottom();
    }

    private void AddMessageToUI(ChatMessage msg)
    {
        if (messageTextPrefab == null || contentRoot == null) return;

        var t = Instantiate(messageTextPrefab, contentRoot);
        
        t.text = $"[{msg.Sender}] {msg.Text}";
    }

    private void ScrollToBottom()
    {
        if (scrollRect == null) return;

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
        Canvas.ForceUpdateCanvases();
    }
}
