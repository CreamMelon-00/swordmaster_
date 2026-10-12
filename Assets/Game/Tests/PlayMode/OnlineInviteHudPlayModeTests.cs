using System;
using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class OnlineInviteHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator TitleOffersOnlineVersusOnly_CentredUnderNewGame()
        {
            yield return null;
            var host = new GameObject("Online Title Test Host");
            int online = 0;
            using (var art = new LegacyDuelArt())
            using (var title = new TitleHud(host.transform, art, () => { }, () => { }, null, () => online++))
            {
                title.Show(null, null, false);
                Assert.That(title.OnlineVersusButton.interactable, Is.True);
                title.OnlineVersusButton.onClick.Invoke();
                Assert.That(online, Is.EqualTo(1));
                foreach (Transform child in title.Root.GetComponentsInChildren<Transform>(true))
                    Assert.That(child.name, Is.Not.EqualTo("Title Local Versus"), "One keyboard's two-player play is gone.");
                var onlineRect = title.OnlineVersusButton.GetComponent<RectTransform>();
                var newGameRect = title.NewGameButton.GetComponent<RectTransform>();
                Assert.That(onlineRect.anchoredPosition.x, Is.Zero, "The one versus entry sits in the title's column.");
                Assert.That(onlineRect.anchoredPosition.y, Is.LessThan(newGameRect.anchoredPosition.y));
                Assert.That(onlineRect.sizeDelta.x, Is.EqualTo(newGameRect.sizeDelta.x));
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator InviteCodeAndReadyRoom_ExposeOnlyAvailableActions()
        {
            yield return null;
            var host = new GameObject("Online Invite Test Host");
            int creates = 0, configures = 0, readies = 0, starts = 0, leaves = 0;
            string joined = null;
            using (var art = new LegacyDuelArt())
            using (var hud = new OnlineInviteHud(host.transform, art,
                () => creates++, code => joined = code, () => readies++, () => starts++, () => leaves++))
            {
                hud.ConfigureSkills = () => configures++;
                hud.Show();
                Assert.That(hud.IsVisible, Is.True);
                Assert.That(hud.CreateButton.interactable, Is.True);
                Assert.That(hud.JoinButton.interactable, Is.False);
                hud.CreateButton.onClick.Invoke();
                Assert.That(creates, Is.EqualTo(1));

                hud.Refresh(new OnlineInviteView
                {
                    Phase = OnlineInvitePhase.Connecting,
                    Host = true,
                    Busy = true,
                    Message = "방을 여는 중"
                });
                Assert.That(hud.CreateButton.interactable, Is.False);
                Assert.That(Label(hud.Root, "Invite Message").text, Is.EqualTo("방을 여는 중"));

                hud.Refresh(new OnlineInviteView
                {
                    Phase = OnlineInvitePhase.Lobby,
                    Host = true,
                    Code = "AB12CD"
                });
                Assert.That(Label(hud.Root, "Invite Code Display").text, Is.EqualTo("AB12CD"));
                Assert.That(hud.CopyButton.gameObject.activeSelf, Is.True);
                Assert.That(hud.ConfigureButton.interactable, Is.True);
                hud.ConfigureButton.onClick.Invoke();
                Assert.That(configures, Is.EqualTo(1));
                Assert.That(hud.ReadyButton.interactable, Is.False);
                Assert.That(hud.StartButton.interactable, Is.False);
                hud.CopyButton.onClick.Invoke();
                Assert.That(Label(hud.Root, "Button Label", hud.CopyButton.transform).text,
                    Is.EqualTo("복사 완료"));

                hud.Refresh(new OnlineInviteView
                {
                    Phase = OnlineInvitePhase.Lobby,
                    Host = true,
                    Code = "AB12CD",
                    PeerConnected = true
                });
                Assert.That(hud.ReadyButton.interactable, Is.True);
                hud.ReadyButton.onClick.Invoke();
                Assert.That(readies, Is.EqualTo(1));
                Assert.That(hud.StartButton.interactable, Is.False);

                hud.Refresh(new OnlineInviteView
                {
                    Phase = OnlineInvitePhase.Lobby,
                    Host = true,
                    Code = "AB12CD",
                    PeerConnected = true,
                    LocalReady = true,
                    RemoteReady = true
                });
                Assert.That(hud.ConfigureButton.interactable, Is.False);
                Assert.That(hud.ReadyButton.interactable, Is.False);
                Assert.That(hud.StartButton.interactable, Is.True);
                hud.StartButton.onClick.Invoke();
                Assert.That(starts, Is.EqualTo(1));

                hud.Refresh(new OnlineInviteView { Phase = OnlineInvitePhase.Error, Message = "방을 찾을 수 없습니다" });
                Assert.That(Label(hud.Root, "Invite Message").text, Is.EqualTo("방을 찾을 수 없습니다"));
                Assert.That(hud.CreateButton.interactable, Is.True);
                hud.CodeInput.text = " ab-12 cd ";
                Assert.That(hud.CodeInput.text, Is.EqualTo("AB12CD"));
                Assert.That(hud.JoinButton.interactable, Is.True);
                hud.JoinButton.onClick.Invoke();
                Assert.That(joined, Is.EqualTo("AB12CD"));

                hud.LeaveButton.onClick.Invoke();
                Assert.That(leaves, Is.EqualTo(1));
                hud.Hide();
                Assert.That(hud.IsVisible, Is.False);
            }
            Object.Destroy(host);
        }

        private static GameObject Named(GameObject root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            Assert.Fail("Missing UI object: " + name);
            return null;
        }

        private static Text Label(GameObject root, string name, Transform within = null)
        {
            Transform container = within != null ? within : root.transform;
            foreach (Text label in container.GetComponentsInChildren<Text>(true))
                if (label.name == name) return label;
            Assert.Fail("Missing UI label: " + name);
            return null;
        }

        private static Button Button(GameObject root, string name)
        {
            return Named(root, name).GetComponent<Button>();
        }
    }
}
