using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class DuelLoadingHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator LoadingBridge_ShowsItsGearProgressAndMessage_ThenLeavesCleanly()
        {
            var parent = new GameObject("Loading HUD Test");
            DuelLoadingHud hud = null;
            try
            {
                using (var art = new LegacyDuelArt())
                {
                    hud = DuelLoadingHud.Create(parent.transform, art.UIFont);
                    hud.Play("전투를 준비하는 중", .1f);
                    Assert.That(hud.IsVisible, Is.True);
                    Assert.That(hud.Root.GetComponent<Canvas>().sortingOrder, Is.EqualTo(1000));
                    Assert.That(hud.Root.Find("Loading Assembly/Loading Gear")?.GetComponent<Image>().sprite, Is.Not.Null);
                    Assert.That(hud.Root.Find("Loading Assembly/Loading Fill")?.GetComponent<Image>().type,
                        Is.EqualTo(Image.Type.Filled));
                    Assert.That(hud.Root.Find("Loading Assembly/Loading Message")?.GetComponent<Text>().text,
                        Is.EqualTo("전투를 준비하는 중"));

                    yield return new WaitForSecondsRealtime(.05f);
                    Assert.That(hud.Progress, Is.GreaterThan(0f));
                    Assert.That(hud.Root.Find("Loading Assembly/Loading Gear").localRotation.eulerAngles.z,
                        Is.Not.EqualTo(0f));
                    yield return new WaitForSecondsRealtime(.3f);
                    Assert.That(hud.IsVisible, Is.False);
                }
            }
            finally
            {
                hud?.Dispose();
                Object.Destroy(parent);
            }
        }
    }
}
