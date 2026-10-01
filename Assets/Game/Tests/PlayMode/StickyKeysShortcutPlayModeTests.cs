using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The Sticky Keys shortcut guard. Only the flag arithmetic is checked here; the tests never change the
    /// machine's setting themselves.</summary>
    public sealed class StickyKeysShortcutPlayModeTests
    {
        [Test]
        public void WithoutShortcut_ClearsOnlyTheShortcutBits_AndLeavesStickyKeysUsersAlone()
        {
            const uint other = 0x10 | 0x20 | 0x40;
            uint shortcutOn = other | StickyKeysShortcut.ShortcutFlags;
            Assert.That(StickyKeysShortcut.WithoutShortcut(shortcutOn), Is.EqualTo(other), "Only the shortcut and its prompt go.");
            Assert.That(StickyKeysShortcut.WithoutShortcut(other | StickyKeysShortcut.HotkeyActive), Is.EqualTo(other));
            Assert.That(StickyKeysShortcut.WithoutShortcut(other), Is.Null, "An already-off shortcut needs no change.");
            Assert.That(StickyKeysShortcut.WithoutShortcut(shortcutOn | StickyKeysShortcut.StickyKeysOn), Is.Null,
                "Someone using Sticky Keys keeps the shortcut that turns them off.");
        }

        [Test]
        public void WithShortcut_PutsBackOnlyTheSavedShortcutBits()
        {
            const uint changedMeanwhile = 0x10 | 0x80;
            Assert.That(StickyKeysShortcut.WithShortcut(changedMeanwhile, StickyKeysShortcut.ShortcutFlags | 0x20),
                Is.EqualTo(changedMeanwhile | StickyKeysShortcut.ShortcutFlags), "Other settings stay as they are now.");
            Assert.That(StickyKeysShortcut.WithShortcut(changedMeanwhile | StickyKeysShortcut.ConfirmHotkey,
                StickyKeysShortcut.HotkeyActive), Is.EqualTo(changedMeanwhile | StickyKeysShortcut.HotkeyActive));
        }

        [UnityTest]
        public IEnumerator Guard_IsCreatedOnceOnWindowsOutsideBatchRuns()
        {
            yield return null;
            StickyKeysShortcutGuard[] guards = Object.FindObjectsByType<StickyKeysShortcutGuard>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int expected = StickyKeysShortcut.IsSupported && !Application.isBatchMode ? 1 : 0;
            Assert.That(guards.Length, Is.EqualTo(expected));
        }
    }
}
