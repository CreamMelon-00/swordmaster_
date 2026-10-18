using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>Exercises the real lobby controllers with delayed, encoded messages, without Unity services.</summary>
    public sealed class OnlineReadyCancellationPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator Host_CanCancelEditAndReadyAgain_WithoutClearingTheGuestsReadiness()
        {
            yield return null;
            using (var pair = new Pair())
            {
                pair.ReadyBoth();
                Peer host = pair.Host, guest = pair.Guest;
                host.Toggle();
                Assert.That(host.Ready, Is.False);
                Assert.That(host.Online.Hud.ConfigureButton.interactable, Is.True);
                Assert.That(host.Online.Hud.StartButton.interactable, Is.False);
                pair.DeliverHost();
                Assert.That(guest.RemoteReady, Is.False);
                Assert.That(guest.Ready, Is.True);

                int changed = host.SwapFirstSkills();
                host.Toggle();
                pair.DeliverHost();
                Assert.That(guest.RemoteReady, Is.True);
                Assert.That(host.Online.Hud.StartButton.interactable, Is.True);
                host.Online.Hud.StartButton.onClick.Invoke();
                pair.DeliverHost();
                pair.AssertSameBattle();
                Assert.That(host.Duel.Match.Left.GetLane(0)[0].Id, Is.EqualTo(changed));
                Assert.That(guest.Duel.Match.Left.GetLane(0)[0].Id, Is.EqualTo(changed));
            }
        }

        [UnityTest]
        public IEnumerator Guest_CancelAcknowledgementUnlocksEditing_AndRereadyUploadsTheNewLoadout()
        {
            yield return null;
            using (var pair = new Pair())
            {
                pair.ReadyBoth();
                Peer guest = pair.Guest;
                guest.Toggle();
                Assert.That(guest.Pending, Is.True);
                Assert.That(guest.Ready, Is.True, "Keep the last acknowledged state until the host orders the change.");
                Assert.That(guest.Online.Hud.ReadyButton.interactable, Is.False);
                Assert.That(guest.Online.Hud.ConfigureButton.interactable, Is.False);
                guest.Online.Hud.ConfigureButton.onClick.Invoke();
                Assert.That(guest.LoadoutHud.IsVisible, Is.False, "Direct callbacks also respect the pending lock.");

                pair.DeliverGuest();
                Assert.That(pair.Host.RemoteReady, Is.False);
                Assert.That(Get<VersusLoadout>(pair.Host.Online, "remoteLoadout"), Is.Null);
                Assert.That(pair.Host.Online.Hud.StartButton.interactable, Is.False);
                pair.DeliverHost();
                Assert.That(guest.Pending, Is.False);
                Assert.That(guest.Ready, Is.False);
                Assert.That(guest.Online.Hud.ConfigureButton.interactable, Is.True);
                guest.Online.Hud.ConfigureButton.onClick.Invoke();
                Assert.That(guest.LoadoutHud.IsVisible, Is.True);
                int changed = guest.SwapFirstSkills();
                Invoke(guest.Online, "CloseLoadout");

                guest.Toggle();
                OnlineVersusMessage request = Decode(guest.Outgoing.Peek());
                Assert.That(request.Sequence, Is.EqualTo(3));
                Assert.That(request.RightReady, Is.True);
                Assert.That(request.RightSkillIds, Is.EqualTo(guest.Loadout.ExportIds()));
                Assert.That(request.RightSkillIds[0], Is.EqualTo(changed));
                Assert.That(guest.Ready, Is.False, "Readying also waits for the host acknowledgement.");
                pair.DeliverGuest();
                pair.DeliverHost();
                Assert.That(guest.Ready, Is.True);
                Assert.That(guest.Pending, Is.False);
                Assert.That(Get<VersusLoadout>(pair.Host.Online, "remoteLoadout").ExportIds(),
                    Is.EqualTo(guest.Loadout.ExportIds()));
            }
        }

        [UnityTest]
        public IEnumerator StaleReadinessMessages_CannotUndoCancellationOrUnlockAPendingChange()
        {
            yield return null;
            using (var pair = new Pair())
            {
                pair.ReadyBoth();
                Peer guest = pair.Guest;
                var staleEcho = new OnlineVersusMessage {
                    Kind = OnlineVersusMessageKind.Ready, Sequence = 1,
                    LeftReady = true, RightReady = true
                };
                var staleRequest = new OnlineVersusMessage {
                    Kind = OnlineVersusMessageKind.Ready, Sequence = 1, Player = 1,
                    RightReady = true, RightSkillIds = guest.Loadout.ExportIds()
                };
                guest.Toggle();
                guest.Receive(staleEcho.Encode());
                Assert.That(guest.Pending, Is.True);
                Assert.That(guest.Ready, Is.True);
                Assert.That(guest.Online.Hud.ConfigureButton.interactable, Is.False);
                pair.DeliverGuest();
                pair.DeliverHost();
                Assert.That(guest.Ready, Is.False);
                Assert.That(guest.Pending, Is.False);

                pair.Host.Receive(staleRequest.Encode());
                guest.Receive(staleEcho.Encode());
                Assert.That(pair.Host.RemoteReady, Is.False, "An older ready request cannot restore the discarded loadout.");
                Assert.That(Get<VersusLoadout>(pair.Host.Online, "remoteLoadout"), Is.Null);
                Assert.That(guest.Ready, Is.False, "An older host echo cannot re-ready the guest.");
                Assert.That(guest.Online.Hud.ConfigureButton.interactable, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator CancellationOrderedBeforeStart_KeepsBothPlayersInTheLobby()
        {
            yield return null;
            using (var pair = new Pair())
            {
                pair.ReadyBoth();
                pair.Guest.Toggle();
                pair.DeliverGuest();
                pair.Host.Online.Hud.StartButton.onClick.Invoke();
                pair.DeliverHost();
                Assert.That(pair.Host.Online.IsInBattle, Is.False);
                Assert.That(pair.Guest.Online.IsInBattle, Is.False);
                Assert.That(pair.Guest.Ready, Is.False);
                Assert.That(pair.Guest.Pending, Is.False);
                Assert.That(pair.Host.Online.Hud.IsVisible, Is.True);
                Assert.That(pair.Guest.Online.Hud.IsVisible, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator StartOrderedBeforeCancellation_StartsBothPlayersAndIgnoresLaterLobbyChanges()
        {
            yield return null;
            using (var pair = new Pair())
            {
                pair.ReadyBoth();
                pair.Guest.Toggle();
                Assert.That(pair.Guest.Pending, Is.True);
                pair.Host.Online.Hud.StartButton.onClick.Invoke();
                Assert.That(pair.Host.Online.IsInBattle, Is.True);
                pair.DeliverGuest();
                pair.DeliverHost();
                pair.AssertSameBattle();
                Assert.That(pair.Guest.Pending, Is.False);
                Assert.That(pair.Guest.LoadoutHud.IsVisible, Is.False);

                pair.Host.Toggle();
                pair.Guest.Toggle();
                pair.Host.Receive(new OnlineVersusMessage {
                    Kind = OnlineVersusMessageKind.Ready, Player = 1, Sequence = 3, RightReady = false
                }.Encode());
                Assert.That(pair.Host.Ready, Is.True);
                Assert.That(pair.Guest.Ready, Is.True);
                Assert.That(pair.Host.RemoteReady, Is.True);
                Assert.That(pair.Host.Outgoing.Count, Is.Zero);
                Assert.That(pair.Guest.Outgoing.Count, Is.Zero);
                pair.AssertSameBattle();
            }
        }

        [UnityTest]
        public IEnumerator LeavingAndReentering_ResetsPendingReadinessAndItsSequence()
        {
            yield return null;
            using (var pair = new Pair())
            {
                pair.ReadyBoth();
                Peer guest = pair.Guest;
                guest.Toggle();
                Assert.That(guest.Pending, Is.True);
                guest.Online.Stop();
                Assert.That(guest.Pending, Is.False);
                guest.Outgoing.Clear();
                guest.Online.Start();
                Assert.That(guest.Ready, Is.False);
                Assert.That(guest.RemoteReady, Is.False);
                Assert.That(Get<int>(guest.Online, "nextReadySequence"), Is.Zero);
                Assert.That(Get<int>(guest.Online, "lastReadySequence"), Is.Zero);
                guest.EnterLobby(false);
                guest.Toggle();
                Assert.That(Decode(guest.Outgoing.Peek()).Sequence, Is.EqualTo(1));
                Assert.That(guest.Pending, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator CompleteQueues_TravelThroughTheRealCommandStream_AndAlternateTheLeadingPlayerNextTurn()
        {
            yield return null;
            using (var pair = new Pair())
            {
                pair.ReadyBoth();
                pair.Host.Online.Hud.StartButton.onClick.Invoke();
                pair.DeliverHost();
                pair.Guest.Online.Tick(0f, null);
                pair.AssertSameBattle();
                Peer host = pair.Host, guest = pair.Guest;
                Assert.That(host.Duel.Match.CurrentPlanner, Is.Zero);
                Assert.That(guest.Duel.TryBreath(), Is.False, "The responder cannot send a reservation before confirmation.");
                Assert.That(guest.Outgoing, Is.Empty);

                // Breaths keep the exchange nonlethal while exercising multiple actual reservations per player.
                pair.HostAction(host.Duel.TryBreath);
                pair.HostAction(host.Duel.TryBreath);
                Assert.That(host.Duel.Match.Left.Queue.Count, Is.EqualTo(2));
                Assert.That(host.Duel.Match.Right.Queue, Is.Empty);
                Assert.That(host.Duel.Match.CurrentPlanner, Is.Zero, "Individual reservations never hand over planning.");
                Assert.That(guest.Duel.Match.CurrentPlanner, Is.Zero);
                pair.HostAction(host.Duel.TryCommit);
                Assert.That(host.Duel.Match.CurrentPlanner, Is.EqualTo(1));
                Assert.That(host.Duel.Match.CommittedPlannerCount, Is.EqualTo(1));
                Assert.That(host.Duel.TryBreath(), Is.False, "A confirmed leading queue is locked.");
                Assert.That(host.Duel.TryCycle(), Is.False);

                pair.GuestAction(OnlineVersusAction.Breath, guest.Duel.TryBreath);
                pair.GuestAction(OnlineVersusAction.Breath, guest.Duel.TryBreath);
                Assert.That(host.Duel.Match.Right.Queue.Count, Is.EqualTo(2));
                Assert.That(host.Duel.Match.Left.Queue.Count, Is.EqualTo(2));
                Assert.That(host.Duel.Match.CurrentPlanner, Is.EqualTo(1));
                Assert.That(guest.Duel.Match.CurrentPlanner, Is.EqualTo(1));
                Assert.That(host.Duel.Match.CommittedPlannerCount, Is.EqualTo(1), "Responding reservations preserve the first confirmation.");
                pair.GuestAction(OnlineVersusAction.Pass, guest.Duel.TryCommit);
                Assert.That(host.Duel.IsResolving && guest.Duel.IsResolving, Is.True);

                for (int frame = 0; frame < 6000 &&
                    (host.Duel.Match.RoundNumber < 2 || guest.Duel.Match.RoundNumber < 2); frame++)
                {
                    host.Online.Tick(.01f, null);
                    guest.Online.Tick(.01f, null);
                    pair.DeliverGuest();
                    pair.DeliverHost();
                    guest.Online.Tick(0f, null);
                }
                pair.AssertSameBattle();
                Assert.That(host.Duel.Match.RoundNumber, Is.EqualTo(2));
                Assert.That(guest.Duel.Match.RoundNumber, Is.EqualTo(2));
                Assert.That(host.Duel.Match.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
                Assert.That(host.Duel.Match.CurrentPlanner, Is.EqualTo(1), "The previous responder leads the next complete queue.");
                Assert.That(guest.Duel.Match.CurrentPlanner, Is.EqualTo(1));
                Assert.That(host.Duel.TryBreath(), Is.False);

                pair.GuestAction(OnlineVersusAction.Breath, guest.Duel.TryBreath);
                pair.GuestAction(OnlineVersusAction.Breath, guest.Duel.TryBreath);
                Assert.That(host.Duel.Match.CurrentPlanner, Is.EqualTo(1));
                Assert.That(host.Duel.Match.Left.Queue, Is.Empty);
                pair.GuestAction(OnlineVersusAction.Pass, guest.Duel.TryCommit);
                Assert.That(host.Duel.Match.CurrentPlanner, Is.Zero);
                Assert.That(guest.Duel.TryBreath(), Is.False, "The guest's confirmed leading queue is locked too.");
                Assert.That(guest.Outgoing, Is.Empty);
                pair.HostAction(host.Duel.TryBreath);
                pair.HostAction(host.Duel.TryBreath);
                Assert.That(host.Duel.Match.CurrentPlanner, Is.Zero);
                Assert.That(host.Duel.Match.Right.Queue.Count, Is.EqualTo(2));
                Assert.That(host.Duel.Match.CommittedPlannerCount, Is.EqualTo(1));
                pair.HostAction(host.Duel.TryCommit);
                Assert.That(host.Duel.IsResolving && guest.Duel.IsResolving, Is.True);
            }
        }

        private static OnlineVersusMessage Decode(string json)
        {
            Assert.That(OnlineVersusMessage.TryDecode(json, out OnlineVersusMessage result, out string error),
                Is.True, error);
            return result;
        }

        private static T Get<T>(object owner, string name)
            => (T)owner.GetType().GetField(name, Private).GetValue(owner);

        private static void Set(object owner, string name, object value)
            => owner.GetType().GetField(name, Private).SetValue(owner, value);

        private static void Invoke(object owner, string name, params object[] args)
            => owner.GetType().GetMethod(name, Private).Invoke(owner, args);

        private sealed class Peer : IDisposable
        {
            private readonly GameObject root;
            private readonly LegacyArenaView arena;
            public readonly LocalVersusController Duel;
            public readonly OnlineVersusController Online;
            public readonly Queue<string> Outgoing = new Queue<string>();
            public bool Ready => Get<bool>(Online, "localReady");
            public bool RemoteReady => Get<bool>(Online, "remoteReady");
            public bool Pending => Get<bool>(Online, "readyChangePending");
            public VersusLoadout Loadout => Get<VersusLoadout>(Online, "localLoadout");
            public VersusLoadoutHud LoadoutHud => Get<VersusLoadoutHud>(Online, "loadoutHud");

            public Peer(bool host, LegacyDuelArt art, DuelPresentationSettings settings)
            {
                root = new GameObject(host ? "Ready Cancellation Host" : "Ready Cancellation Guest");
                arena = LegacyArenaView.Create(root.transform, art, settings);
                Duel = new LocalVersusController(root.transform, art, arena, () => { }, settings);
                ConstructorInfo constructor = typeof(OnlineVersusController).GetConstructor(Private, null,
                    new[] { typeof(Transform), typeof(LegacyDuelArt), typeof(LocalVersusController),
                        typeof(Action), typeof(Action<string>) }, null);
                Assert.That(constructor, Is.Not.Null);
                Online = (OnlineVersusController)constructor.Invoke(new object[] {
                    root.transform, art, Duel, (Action)(() => { }), (Action<string>)(json => Outgoing.Enqueue(json))
                });
                Online.Start();
                EnterLobby(host);
            }

            public void EnterLobby(bool host)
            {
                Set(Online, "host", host);
                Set(Online, "peerConnected", true);
                Set(Online, "rulesMatched", true);
                Set(Online, "phase", OnlineInvitePhase.Lobby);
                Invoke(Online, "Refresh");
            }

            public void Toggle() => Online.Hud.ReadyButton.onClick.Invoke();
            public void Receive(string json) => Invoke(Online, "OnMessage", json);

            public int SwapFirstSkills()
            {
                int skill = Loadout.GetSkillId(0, 1);
                Assert.That(Loadout.TryPlaceSkill(skill, 0, 0), Is.True);
                return skill;
            }

            public void Dispose()
            {
                Online.Dispose();
                Duel.Dispose();
                arena.Dispose();
                Object.Destroy(root);
            }
        }

        private sealed class Pair : IDisposable
        {
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled, runInBackground;
            private readonly LegacyDuelArt art;
            private readonly DuelPresentationSettings settings;
            public readonly Peer Host, Guest;

            public Pair()
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                runInBackground = Application.runInBackground;
                art = new LegacyDuelArt();
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                Host = new Peer(true, art, settings);
                Guest = new Peer(false, art, settings);
            }

            public void ReadyBoth()
            {
                Host.Toggle();
                Guest.Toggle();
                DeliverHost();
                DeliverGuest();
                DeliverHost();
                Assert.That(Host.Ready && Guest.Ready && Host.RemoteReady && Guest.RemoteReady, Is.True);
                Assert.That(Guest.Pending, Is.False);
                Assert.That(Host.Online.Hud.StartButton.interactable, Is.True);
            }

            public void DeliverHost() => Deliver(Host, Guest);
            public void DeliverGuest() => Deliver(Guest, Host);

            public void HostAction(Func<bool> submit)
            {
                Assert.That(submit(), Is.True);
                DeliverHost();
                Guest.Online.Tick(0f, null);
                AssertSameBattle();
            }

            public void GuestAction(OnlineVersusAction action, Func<bool> submit)
            {
                int sequence = Get<int>(Host.Online, "nextAppliedSequence") + 1;
                Assert.That(Guest.Outgoing, Is.Empty);
                Assert.That(submit(), Is.True);
                Assert.That(Get<bool>(Guest.Duel, "pendingOnlineRequest"), Is.True);
                Assert.That(Guest.Outgoing.Count, Is.EqualTo(1));
                OnlineVersusMessage request = Decode(Guest.Outgoing.Peek());
                Assert.That((request.Kind, request.Player, request.Action, request.Round),
                    Is.EqualTo((OnlineVersusMessageKind.Request, 1, action, Guest.Duel.Match.RoundNumber)));
                Assert.That(Guest.Duel.TryBreath(), Is.False, "The guest waits for each command acknowledgement.");
                DeliverGuest();
                OnlineVersusMessage accepted = Decode(Host.Outgoing.Peek());
                Assert.That((accepted.Kind, accepted.Player, accepted.Action, accepted.Sequence),
                    Is.EqualTo((OnlineVersusMessageKind.Applied, 1, action, sequence)));
                DeliverHost();
                Guest.Online.Tick(0f, null);
                Assert.That(Get<bool>(Guest.Duel, "pendingOnlineRequest"), Is.False);
                AssertSameBattle();
            }

            private static void Deliver(Peer source, Peer destination)
            {
                while (source.Outgoing.Count > 0) destination.Receive(source.Outgoing.Dequeue());
            }

            public void AssertSameBattle()
            {
                Assert.That(Host.Online.IsInBattle, Is.True);
                Assert.That(Guest.Online.IsInBattle, Is.True);
                Assert.That(OnlineVersusProtocol.ComputeStateHash(Host.Duel.Match),
                    Is.EqualTo(OnlineVersusProtocol.ComputeStateHash(Guest.Duel.Match)));
            }

            public void Dispose()
            {
                Guest.Dispose();
                Host.Dispose();
                art.Dispose();
                Object.Destroy(settings);
                Application.runInBackground = runInBackground;
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
