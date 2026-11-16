using System;
using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DialogueUI : UIBase
{
    [Header("대화 표시")]
    [SerializeField] private TMP_Text _speakerText;
    [SerializeField] private TMP_Text _dialogueText;
    [Header("NPC 스탠딩")]
    [SerializeField] private Image _npcImage;
    [Header("대화 입력")]
    [SerializeField] private Button _nextButton;

    private DialogueViewModel _viewModel;
    public event Action NextRequested;
    public event Action Hidden;

    private void Awake()
    {
        SetNpcSprite(null);
        if (_nextButton != null)
            _nextButton.onClick.AddListener(OnNextClicked);
    }

    public bool Bind(DialogueViewModel viewModel)
    {
        Unbind();
        if (viewModel == null || _speakerText == null || _dialogueText == null ||
            _nextButton == null)
        {
            Debug.LogError("DialogueUI의 화자, 본문, 다음 버튼을 연결해 주세요.", this);
            return false;
        }
        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnPropertyChanged;
        Refresh();
        return true;
    }

    public void Unbind()
    {
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnPropertyChanged;
        _viewModel = null;
        SetNpcSprite(null);
    }

    // 로딩과 리소스 소유권은 외부에 두고, 전달받은 이미지만 표시합니다.
    public void SetNpcSprite(Sprite sprite)
    {
        if (_npcImage == null)
            return;

        _npcImage.preserveAspect = true;
        _npcImage.raycastTarget = false;
        _npcImage.sprite = sprite;
        _npcImage.enabled = sprite != null;
    }

    private void OnPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
        // 빈 PropertyName은 화자/본문/버튼을 함께 갱신하라는 알림입니다.
        Refresh();
    }

    private void Refresh()
    {
        _speakerText.text = _viewModel.SpeakerName;
        _dialogueText.text = _viewModel.Text;
        _nextButton.interactable = _viewModel.CanAdvance;
    }

    private void OnNextClicked()
    {
        if (_viewModel != null && _viewModel.CanAdvance)
            NextRequested?.Invoke();
    }

    private void OnDisable()
    {
        bool wasBound = _viewModel != null;
        Unbind();
        if (wasBound)
            Hidden?.Invoke();
    }

    private void OnDestroy()
    {
        Unbind();
        if (_nextButton != null)
            _nextButton.onClick.RemoveListener(OnNextClicked);
    }
}
