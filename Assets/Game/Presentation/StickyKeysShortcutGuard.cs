using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>Keeps the Sticky Keys shortcut off while the game has focus (<see cref="StickyKeysShortcut"/>):
    /// off when focus comes back (and rechecked every half second while focused), on again when focus leaves, the
    /// game quits, Play mode ends or scripts reload.
    /// Created on its own before the first scene on Windows; batch runs (CI) skip it.</summary>
    [DisallowMultipleComponent]
    public sealed class StickyKeysShortcutGuard : MonoBehaviour
    {
        private const float RecheckSeconds = .5f;
        private float nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Application.isBatchMode || !StickyKeysShortcut.IsSupported ||
                FindAnyObjectByType<StickyKeysShortcutGuard>() != null) return;
            var host = new GameObject("Sticky Keys Shortcut Guard");
            DontDestroyOnLoad(host);
            host.AddComponent<StickyKeysShortcutGuard>();
        }

        private void OnEnable()
        {
            if (Application.isFocused) StickyKeysShortcut.Suspend();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!isActiveAndEnabled) return;
            if (focused) StickyKeysShortcut.Suspend();
            else StickyKeysShortcut.Restore();
        }

        // Focus can arrive before another program (e.g. a second copy of the game losing focus) turns the shortcut
        // back on, so keep checking while focused and not yet holding it off.
        private void Update()
        {
            if (StickyKeysShortcut.IsSuspended || !Application.isFocused || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + RecheckSeconds;
            StickyKeysShortcut.Suspend();
        }

        private void OnApplicationQuit() => StickyKeysShortcut.Restore();

        // Also runs before a script reload and when the guard is destroyed, so the shortcut never stays off.
        private void OnDisable() => StickyKeysShortcut.Restore();
    }
}
