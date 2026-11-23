using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class PlayerControllerView
{
    private void HandleQuickSlotInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        // 호스트의 즉시 응답에도 한 프레임에는 낮은 번호 하나만 요청한다.
        int index;
        if (keyboard.digit1Key.wasPressedThisFrame) index = 0;
        else if (keyboard.digit2Key.wasPressedThisFrame) index = 1;
        else if (keyboard.digit3Key.wasPressedThisFrame) index = 2;
        else if (keyboard.digit4Key.wasPressedThisFrame) index = 3;
        else if (keyboard.digit5Key.wasPressedThisFrame) index = 4;
        else return;

        if (!CanReadQuickSlotInput())
            return;

        ServiceManager.Inst?.InventoryService?.RequestUseQuickSlot(index);
    }

    private bool CanReadQuickSlotInput()
    {
        if (!CanReadAttackInput() || Time.timeScale <= 0f
            || _playerInput.currentActionMap == null || _playerInput.currentActionMap.name != "Player")
            return false;

        UIManager ui = UIManager.Inst;
        if (ui != null && (ui.IsLoading(UIType.SettingUI) || ui.IsLoading(UIType.RuneInventoryUI)
            || ui.IsLoading(UIType.ScreenTransitionUI) || ui.IsOpen(UIType.ScreenTransitionUI)
            || ui.IsLoading(UIType.DialogueUI) || ui.IsOpen(UIType.DialogueUI)
            || ui.IsLoading(UIType.CraftingUI) || ui.IsOpen(UIType.CraftingUI)
            || ui.IsLoading(UIType.RestUI) || ui.IsOpen(UIType.RestUI)
            || ui.IsLoading(UIType.GameOverUI) || ui.IsOpen(UIType.GameOverUI)))
            return false;

        GameObject selected = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject : null;
        return selected == null || (selected.GetComponentInParent<TMP_InputField>() == null
            && selected.GetComponentInParent<InputField>() == null);
    }
}
