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
                Assert.That(hud.ReadyButton.interactable, Is.True);
                Assert.That(Label(hud.Root, "Button Label", hud.ReadyButton.transform).text,
                    Is.EqualTo("준비 취소"));
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

        [UnityTest]
        public IEnumerator ReadyRoom_CancelReadinessReopensConfiguration_ForHostAndGuest()
        {
            yield return null;
            foreach (bool isHost in new[] { true, false })
            {
                var host = new GameObject("Online Ready Toggle Test Host");
                int toggles = 0, configures = 0;
                using (var art = new LegacyDuelArt())
                using (var hud = new OnlineInviteHud(host.transform, art,
                    null, null, () => toggles++, null, null))
                {
                    hud.ConfigureSkills = () => configures++;
                    hud.Show();
                    var view = new OnlineInviteView
                    {
                        Phase = OnlineInvitePhase.Lobby,
                        Host = isHost,
                        PeerConnected = true,
                        LocalReady = true,
                        RemoteReady = true
                    };
                    hud.Refresh(view);
                    Assert.That(hud.ReadyButton.interactable, Is.True);
                    Assert.That(Label(hud.Root, "Button Label", hud.ReadyButton.transform).text,
                        Is.EqualTo("준비 취소"));
                    Assert.That(hud.ConfigureButton.interactable, Is.False);
                    Assert.That(hud.StartButton.interactable, Is.EqualTo(isHost));

                    hud.ReadyButton.onClick.Invoke();
                    Assert.That(toggles, Is.EqualTo(1), "Cancelling uses the same ready callback.");
                    view.LocalReady = false;
                    hud.Refresh(view);
                    Assert.That(Label(hud.Root, "Button Label", hud.ReadyButton.transform).text,
                        Is.EqualTo("준비"));
                    Assert.That(Label(hud.Root, "Invite Local Ready").text, Is.EqualTo("나  ·  준비 전"));
                    Assert.That(Label(hud.Root, "Invite Remote Ready").text, Is.EqualTo("친구  ·  준비 완료"));
                    Assert.That(hud.ReadyButton.interactable, Is.True);
                    Assert.That(hud.ConfigureButton.interactable, Is.True);
                    Assert.That(hud.StartButton.interactable, Is.False);
                    hud.ConfigureButton.onClick.Invoke();
                    Assert.That(configures, Is.EqualTo(1));

                    hud.ReadyButton.onClick.Invoke();
                    Assert.That(toggles, Is.EqualTo(2), "The same control can ready up again after cancelling.");
                    view.LocalReady = true;
                    view.RemoteReady = false;
                    hud.Refresh(view);
                    Assert.That(hud.StartButton.interactable, Is.False,
                        "A remote cancellation also prevents the host from starting.");

                    view.Busy = true;
                    hud.Refresh(view);
                    Assert.That(hud.ReadyButton.interactable, Is.False,
                        "Readiness cannot change while a match is starting.");
                    Assert.That(hud.ConfigureButton.interactable, Is.False);
                    view.Busy = false;
                    view.PeerConnected = false;
                    hud.Refresh(view);
                    Assert.That(hud.ReadyButton.interactable, Is.False,
                        "A disconnected room cannot send readiness changes.");
                }
                Object.Destroy(host);
            }
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
