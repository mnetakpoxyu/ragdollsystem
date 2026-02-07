using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Менеджер для блокировки/разблокировки управления игроком.
/// Используется для заморозки управления во время UI взаимодействий.
/// </summary>
public class PlayerInputManager : MonoBehaviour
{
    static PlayerInputManager _instance;
    public static PlayerInputManager Instance => _instance;

    [Header("Настройки")]
    [Tooltip("InputActionAsset игрока для блокировки.")]
    [SerializeField] InputActionAsset playerInputAsset;

    bool _isInputLocked;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        if (playerInputAsset == null)
            playerInputAsset = InputSystemRuntimeFallback.GetDefaultAsset();
    }

    /// <summary>
    /// Заблокировать управление игроком.
    /// </summary>
    public void LockInput()
    {
        if (_isInputLocked) return;

        _isInputLocked = true;

        // Отключаем все action maps игрока
        if (playerInputAsset != null)
        {
            foreach (var actionMap in playerInputAsset.actionMaps)
            {
                actionMap.Disable();
            }
        }

        Debug.Log("PlayerInputManager: Управление заблокировано.");
    }

    /// <summary>
    /// Разблокировать управление игроком.
    /// </summary>
    public void UnlockInput()
    {
        if (!_isInputLocked) return;

        _isInputLocked = false;

        // Включаем все action maps игрока
        if (playerInputAsset != null)
        {
            foreach (var actionMap in playerInputAsset.actionMaps)
            {
                actionMap.Enable();
            }
        }

        Debug.Log("PlayerInputManager: Управление разблокировано.");
    }

    public bool IsInputLocked => _isInputLocked;
}
