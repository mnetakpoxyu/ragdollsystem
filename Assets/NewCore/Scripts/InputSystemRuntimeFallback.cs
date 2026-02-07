using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Если InputSystem_Actions.inputactions не загружается в редакторе (ошибка импорта),
/// этот скрипт загружает те же действия из Resources/InputSystem_Actions.json в рантайме.
/// Компоненты (PlayerController, PlayerInteract, PlayerHQDVape, PlayerInputManager) подхватывают
/// asset через GetDefaultAsset() когда их поле в инспекторе пустое.
/// </summary>
public static class InputSystemRuntimeFallback
{
    static InputActionAsset _cached;

    /// <summary>
    /// Возвращает InputActionAsset с картой Player (Move, Look, Jump, Crouch, Interact, Vape и т.д.).
    /// Создаётся из Resources/InputSystem_Actions.json при первом вызове.
    /// </summary>
    public static InputActionAsset GetDefaultAsset()
    {
        if (_cached != null) return _cached;

        TextAsset json = Resources.Load<TextAsset>("InputSystem_Actions");
        if (json == null || string.IsNullOrEmpty(json.text))
        {
            Debug.LogWarning("InputSystemRuntimeFallback: Resources/InputSystem_Actions.json not found. Player input will not work.");
            return null;
        }

        try
        {
            _cached = InputActionAsset.FromJson(json.text);
            if (_cached != null)
                Debug.Log("InputSystemRuntimeFallback: Input actions loaded from Resources/InputSystem_Actions.json.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("InputSystemRuntimeFallback: Failed to parse InputSystem_Actions.json: " + e.Message);
        }

        return _cached;
    }
}
