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
        public IEnumerator TitleOffersLocalAndOnlineVersusSeparately()
        {
            yield return null;
            var host = new GameObject("Online Title Test Host");
            int local = 0, online = 0;
            using (var art = new LegacyDuelArt())
            using (var title = new TitleHud(host.transform, art, () => { }, () => { },
                null, () => local++, () => online++))
            {
                title.Show(null, null, false);
                Assert.That(title.LocalVersusButton.interactable, Is.True);
                Assert.That(title.OnlineVersusButton.interactable, Is.True);
                title.LocalVersusButton.onClick.Invoke();
                title.OnlineVersusButton.onClick.Invoke();
                Assert.That(local, Is.EqualTo(1));
                Assert.That(online, Is.EqualTo(1));
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

        [UnityTest]
        public IEnumerator OnlineDuelDesk_KeepsOwnSkillsAndWaitsForOpponent()
        {
            yield return null;
            var host = new GameObject("Online Duel Desk Test Host");
            using (var art = new LegacyDuelArt())
            using (var hud = new LocalVersusHud(host.transform, art, _ => { }, () => { },
                () => { }, () => { }, () => { }, () => { }))
            {
                var match = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All);
                hud.Refresh(match, 20f, 20f, 1);
                Assert.That(Label(hud.Root, "Versus Active Player").text, Is.EqualTo("상대 차례"));
                Assert.That(Button(hud.Root, "Versus Lane Q").interactable, Is.False);
                Assert.That(Button(hud.Root, "Versus Pass").interactable, Is.False);
                Assert.That(Label(hud.Root, "Versus Command Heading").text, Does.StartWith("내 기술"));
                hud.ShowSkillDetail(0);
                Assert.That(Named(hud.Root, "Versus Skill Detail").activeSelf, Is.True);

                Assert.That(match.TryQueueLane(0, 0), Is.True);
                hud.Refresh(match, 19f, 20f, 1);
                Assert.That(Label(hud.Root, "Versus Active Player").text, Is.EqualTo("내 차례"));
                Assert.That(Button(hud.Root, "Versus Lane Q").interactable, Is.True);
                hud.SetRematchPending(true);
                Assert.That(Button(hud.Root, "Versus Rematch").interactable, Is.False);
                Assert.That(Label(hud.Root, "Button Label",
                    Button(hud.Root, "Versus Rematch").transform).text, Is.EqualTo("상대 기다리는 중"));
                hud.SetRematchPending(false);
                Assert.That(Button(hud.Root, "Versus Rematch").interactable, Is.True);
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
