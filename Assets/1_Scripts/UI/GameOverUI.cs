using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class GameOverUI : UIBase
{
    [Header("사망 화면")]
    [SerializeField] private Button _respawnButton;
    [SerializeField] private TMP_Text _messageText;
    public event Action RespawnRequested;

    private void Awake()
    {
        _respawnButton.onClick.AddListener(OnRespawnClicked);
    }

    private void OnDestroy()
    {
        _respawnButton.onClick.RemoveListener(OnRespawnClicked);
    }

    public void SetState(bool waiting, string message = "")
    {
        _respawnButton.interactable = !waiting;
        _messageText.text = waiting ? "부활 중..." : message;
        if (!waiting && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_respawnButton.gameObject);
    }

    private void OnRespawnClicked()
    {
        if (!_respawnButton.interactable) return;
        RespawnRequested?.Invoke();
    }
}
