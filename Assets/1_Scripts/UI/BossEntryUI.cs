using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class BossEntryUI : UIBase
{
    [SerializeField] private Button _enterButton;
    [SerializeField] private Button _prepareButton;
    public event Action<bool> Chosen;
    private bool _submitted;
    private void Awake()
    {
        _enterButton.onClick.AddListener(Enter);
        _prepareButton.onClick.AddListener(Prepare);
    }
    private void OnEnable()
    {
        _submitted = false;
        _enterButton.interactable = true;
        _prepareButton.interactable = true;
    }
    private void Enter() { Submit(true); }
    private void Prepare() { Submit(false); }
    private void Submit(bool enter)
    {
        if (_submitted) return;
        _submitted = true;
        _enterButton.interactable = false;
        _prepareButton.interactable = false;
        Chosen?.Invoke(enter);
    }
}
