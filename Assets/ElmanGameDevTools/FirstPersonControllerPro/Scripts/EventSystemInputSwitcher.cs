using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ElmanGameDevTools.PlayerSystem
{
    /// <summary>
    /// Replaces legacy StandaloneInputModule with InputSystemUIInputModule at runtime
    /// so UI works when Project Settings use the Input System package.
    /// Add this to the same GameObject as EventSystem (or run from any Awake).
    /// </summary>
    public class EventSystemInputSwitcher : MonoBehaviour
    {
        private void Awake()
        {
            var eventSystem = GetComponent<EventSystem>();
            if (eventSystem == null) return;

            var legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                Destroy(legacy);
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }
    }
}
