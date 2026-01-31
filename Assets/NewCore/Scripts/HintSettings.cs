using UnityEngine;

/// <summary>
/// Единые настройки для всех подсказок в игре (шрифт и т.д.).
/// Создай asset: ПКМ в Project → Create → NewCore → Hint Settings.
/// Перетащи сюда готический шрифт, сохрани asset в папку Resources (например Assets/Resources/HintSettings.asset).
/// Тогда все подсказки будут использовать этот шрифт.
/// </summary>
[CreateAssetMenu(fileName = "HintSettings", menuName = "NewCore/Hint Settings")]
public class HintSettings : ScriptableObject
{
    [Tooltip("Шрифт для всех подсказок (готический/рукописный).")]
    public Font defaultHintFont;
}
