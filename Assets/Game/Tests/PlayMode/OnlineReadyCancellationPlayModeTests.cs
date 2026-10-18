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

        [UnityTest]
        public IEnumerator ActSteal_ThroughTheRealCommandStream_KeepsBothPeersAndTheirMirroredBadgesInSync()
        {
            yield return null;
            using (var pair = new Pair())
            {
                Assert.That(pair.Host.Loadout.TryPlaceSkill(62, 2, 0), Is.True);
                pair.ReadyBoth();
                pair.Host.Online.Hud.StartButton.onClick.Invoke();
                pair.DeliverHost();
                pair.Guest.Online.Tick(0f, null);
                pair.AssertSameBattle();

                // Build real carried ACT through the same finalized-queue messages used in a live match.
                for (int round = 1; round <= 3; round++)
                {
                    while (pair.Host.Duel.Match.Phase == LegacyDuelPhase.Planning)
                    {
                        if (pair.Host.Duel.Match.CurrentPlanner == 0) pair.HostAction(pair.Host.Duel.TryCommit);
                        else pair.GuestAction(OnlineVersusAction.Pass, pair.Guest.Duel.TryCommit);
                    }
                    pair.AdvanceUntil(() => pair.Host.Duel.Match.RoundNumber > round &&
                        pair.Guest.Duel.Match.RoundNumber > round);
                    pair.AssertSameBattle();
                }
                Assert.That(pair.Host.Duel.Match.Left.Act, Is.EqualTo(10));
                Assert.That(pair.Host.Duel.Match.Right.Act, Is.EqualTo(10));
                Assert.That(pair.Host.Duel.Match.CurrentPlanner, Is.EqualTo(1));
                pair.GuestAction(OnlineVersusAction.Pass, pair.Guest.Duel.TryCommit);
                pair.HostAction(() => pair.Host.Duel.TryQueueLane(2));
                Assert.That(pair.Host.Duel.Match.Left.Queue[0].Id, Is.EqualTo(62));
                Assert.That(pair.Host.Duel.Match.Left.Act, Is.EqualTo(8));
                Assert.That(pair.Guest.Duel.Match.Left.Act, Is.EqualTo(8));
                Assert.That(pair.Host.Duel.Match.Right.Act, Is.EqualTo(10), "Reserving the skill cannot steal yet.");
                pair.HostAction(pair.Host.Duel.TryCommit);

                pair.AdvanceUntil(() => pair.Host.Duel.Match.CurrentSlot != null &&
                    pair.Guest.Duel.Match.CurrentSlot != null);
                pair.AssertSameBattle();
                Assert.That(pair.Host.Duel.Match.CurrentSlot.LeftFeedback.ActStolen, Is.EqualTo(3));
                Assert.That(pair.Guest.Duel.Match.CurrentSlot.LeftFeedback.ActStolen, Is.EqualTo(3));
                Assert.That(pair.Host.Duel.Match.CurrentSlot.LeftFeedback.ActReceived, Is.EqualTo(2));
                Assert.That(pair.Guest.Duel.Match.CurrentSlot.LeftFeedback.ActReceived, Is.EqualTo(2));
                Assert.That(pair.Host.Duel.Perspective.Act, Is.EqualTo(10));
                Assert.That(pair.Guest.Duel.Perspective.Act, Is.EqualTo(7));
                Assert.That(pair.Host.Duel.Hud.OwnActText, Is.EqualTo("나  ACT 10 / 10"));
                Assert.That(pair.Host.Duel.Hud.OpponentActText, Is.EqualTo("상대  ACT 7 / 10"));
                Assert.That(pair.Guest.Duel.Hud.OwnActText, Is.EqualTo("나  ACT 7 / 10"));
                Assert.That(pair.Guest.Duel.Hud.OpponentActText, Is.EqualTo("상대  ACT 10 / 10"));

                pair.AdvanceUntil(() => pair.Host.Duel.Match.RoundNumber == 5 &&
                    pair.Guest.Duel.Match.RoundNumber == 5);
                pair.AssertSameBattle();
                Assert.That(pair.Host.Duel.Match.Left.Act, Is.EqualTo(10));
                Assert.That(pair.Host.Duel.Match.Right.Act, Is.EqualTo(10));
                Assert.That(pair.Host.Duel.Match.Left.NextActGain, Is.EqualTo(3));
                Assert.That(pair.Host.Duel.Match.Right.NextActGain, Is.EqualTo(3));
            }
        }

        [UnityTest]
        public IEnumerator CoinToss_HostChoosesEitherSide_AndBothPeersRevealTheSameResultWithoutSpendingClock()
        {
            yield return null;
            foreach (int opening in new[] { 0, 1 })
            {
                int hostDraws = 0, guestDraws = 0;
                using (var pair = new Pair(() => { hostDraws++; return opening; },
                    () => { guestDraws++; throw new InvalidOperationException("A guest never tosses the coin."); }))
                {
                    pair.EnableCoinCards();
                    pair.ReadyBoth();
                    pair.Host.Online.Hud.StartButton.onClick.Invoke();
                    OnlineVersusMessage start = Decode(pair.Host.Outgoing.Peek());
                    Assert.That(start.Kind, Is.EqualTo(OnlineVersusMessageKind.Start));
                    Assert.That(start.OpeningPlayer, Is.EqualTo(opening));
                    pair.DeliverHost();
                    pair.AssertSameBattle();
                    Assert.That(hostDraws, Is.EqualTo(1));
                    Assert.That(guestDraws, Is.Zero);
                    Assert.That(pair.Host.Duel.Match.OpeningPlayer, Is.EqualTo(opening));
                    Assert.That(pair.Guest.Duel.Match.OpeningPlayer, Is.EqualTo(opening));
                    Assert.That(pair.Host.Duel.StartCard.IsCoinToss, Is.True);
                    Assert.That(pair.Guest.Duel.StartCard.IsCoinToss, Is.True);
                    Assert.That(pair.Host.Duel.StartCard.CoinResultRevealed, Is.False);
                    Assert.That(pair.Guest.Duel.StartCard.CoinResultRevealed, Is.False);
                    Assert.That(pair.Host.Duel.StartCard.Timeline.Seconds,
                        Is.GreaterThanOrEqualTo(DuelStartCard.MinimumCoinTossSeconds));
                    Assert.That(pair.Host.Duel.TryBreath(), Is.False);
                    Assert.That(pair.Guest.Duel.TryBreath(), Is.False);
                    Assert.That(pair.Host.Duel.TryApplyOnlinePeerAction(OnlineVersusAction.Breath, -1, 1), Is.False);
                    Assert.That(pair.Host.Outgoing, Is.Empty);
                    Assert.That(pair.Guest.Outgoing, Is.Empty);

                    pair.AdvanceUntil(() => pair.Host.Duel.StartCard.CoinResultRevealed &&
                        pair.Guest.Duel.StartCard.CoinResultRevealed);
                    Assert.That(pair.Host.Duel.IsShowingStartCard && pair.Guest.Duel.IsShowingStartCard, Is.True);
                    Assert.That(pair.Host.Duel.StartCard.CoinResultText, Does.Contain("선공"));
                    Assert.That(pair.Guest.Duel.StartCard.CoinResultText, Does.Contain("선공"));
                    Assert.That(pair.Host.Duel.StartCard.CoinResultText,
                        Is.Not.EqualTo(pair.Guest.Duel.StartCard.CoinResultText), "The same winner is described from each viewer's side.");
                    Assert.That(pair.Host.Duel.LeftClock, Is.EqualTo(LocalVersusController.PlanningSeconds));
                    Assert.That(pair.Host.Duel.RightClock, Is.EqualTo(LocalVersusController.PlanningSeconds));
                    Assert.That(pair.Guest.Duel.LeftClock, Is.EqualTo(LocalVersusController.PlanningSeconds));
                    Assert.That(pair.Guest.Duel.RightClock, Is.EqualTo(LocalVersusController.PlanningSeconds));
                    pair.WaitForOpening();
                    if (opening == 0) pair.HostAction(pair.Host.Duel.TryBreath);
                    else pair.GuestAction(OnlineVersusAction.Breath, pair.Guest.Duel.TryBreath);
                    Assert.That(pair.Host.Duel.Match.GetFighter(opening).Queue.Count, Is.EqualTo(1));
                }
            }
        }

        [UnityTest]
        public IEnumerator CoinToss_RematchesDrawAgain_AllowTheSameWinner_AndIgnoreReplayedStartPackets()
        {
            yield return null;
            var choices = new Queue<int>(new[] { 0, 0, 1 });
            int hostDraws = 0, guestDraws = 0;
            using (var pair = new Pair(() => { hostDraws++; return choices.Dequeue(); },
                () => { guestDraws++; return 1; }))
            {
                pair.EnableCoinCards();
                pair.ReadyBoth();
                pair.Host.Online.Hud.StartButton.onClick.Invoke();
                var matchIds = new HashSet<string>();
                foreach (int opening in new[] { 0, 0, 1 })
                {
                    OnlineVersusMessage start = Decode(pair.Host.Outgoing.Peek());
                    Assert.That(start.Kind, Is.EqualTo(OnlineVersusMessageKind.Start));
                    Assert.That(start.OpeningPlayer, Is.EqualTo(opening));
                    Assert.That(matchIds.Add(start.MatchId), Is.True);
                    pair.DeliverHost();
                    pair.AssertSameBattle();
                    Assert.That(pair.Host.Duel.Match.OpeningPlayer, Is.EqualTo(opening));
                    Assert.That(pair.Guest.Duel.Match.OpeningPlayer, Is.EqualTo(opening));
                    Assert.That(pair.Host.Duel.StartCard.IsCoinToss && pair.Guest.Duel.StartCard.IsCoinToss, Is.True);
                    pair.WaitForOpening();
                    if (hostDraws == 3) break;

                    pair.FinishAsDraw();
                    pair.Guest.Receive(start.Encode());
                    Assert.That(pair.Guest.Duel.Match.IsFinished, Is.True, "An old Start cannot restart a completed match.");
                    Assert.That(pair.Guest.Duel.IsShowingStartCard, Is.False);
                    pair.AssertSameBattle();
                    Assert.That(pair.Host.Duel.Rematch(), Is.True);
                    Assert.That(hostDraws, Is.EqualTo(matchIds.Count), "One vote cannot toss the coin or restart the match.");
                    Assert.That(pair.Guest.Duel.Rematch(), Is.True);
                    pair.DeliverGuest();
                }
                Assert.That(hostDraws, Is.EqualTo(3));
                Assert.That(guestDraws, Is.Zero);
                Assert.That(choices, Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator CoinToss_AcceptedCommandsWaitForALateGuestsReveal_AndClocksStayFrozenUnderItsCard()
        {
            yield return null;
            using (var pair = new Pair())
            {
                pair.EnableCoinCards();
                pair.ReadyBoth();
                pair.Host.Online.Hud.StartButton.onClick.Invoke();
                pair.DeliverHost();
                pair.Host.Online.Tick(pair.Host.Duel.StartCard.Timeline.Seconds + .01f, null);
                pair.DeliverHost();
                Assert.That(pair.Host.Duel.IsShowingStartCard, Is.False);
                Assert.That(pair.Guest.Duel.IsShowingStartCard, Is.True);
                pair.Host.Online.Tick(.6f, null);
                Assert.That(pair.Host.Duel.LeftClock, Is.LessThan(LocalVersusController.PlanningSeconds));
                pair.DeliverHost();
                Assert.That(pair.Guest.Duel.LeftClock, Is.EqualTo(LocalVersusController.PlanningSeconds),
                    "A periodic clock packet cannot consume the late guest's opening presentation.");
                Assert.That(pair.Host.Duel.TryBreath(), Is.True);
                pair.DeliverHost();
                Assert.That(pair.Host.Duel.Match.Left.Queue.Count, Is.EqualTo(1));
                Assert.That(pair.Guest.Duel.Match.Left.Queue, Is.Empty);
                Assert.That(pair.Guest.Online.IsInBattle, Is.True);
                Assert.That(Get<Queue<OnlineVersusMessage>>(pair.Guest.Online, "accepted").Count, Is.EqualTo(1));

                pair.Guest.Online.Tick(pair.Guest.Duel.StartCard.Timeline.Seconds + .01f, null);
                pair.AssertSameBattle();
                Assert.That(pair.Guest.Duel.Match.Left.Queue.Count, Is.EqualTo(1));
                Assert.That(Get<Queue<OnlineVersusMessage>>(pair.Guest.Online, "accepted"), Is.Empty);
                Assert.That(pair.Guest.Duel.LeftClock, Is.EqualTo(pair.Host.Duel.LeftClock));
            }
        }

        [UnityTest]
        public IEnumerator CoinToss_InvalidHostChoice_DoesNotSendAnInvalidStartOrBeginBattle()
        {
            yield return null;
            foreach (int invalid in new[] { -1, 2 })
            using (var pair = new Pair(() => invalid))
            {
                pair.ReadyBoth();
                pair.Host.Online.Hud.StartButton.onClick.Invoke();
                Assert.That(pair.Host.Online.IsInBattle, Is.False);
                Assert.That(pair.Guest.Online.IsInBattle, Is.False);
                Assert.That(pair.Host.Duel.Match, Is.Null);
                Assert.That(pair.Host.Outgoing, Is.Empty);
                Assert.That(Get<OnlineInvitePhase>(pair.Host.Online, "phase"), Is.EqualTo(OnlineInvitePhase.Error));
                Assert.That(pair.Host.Online.Hud.IsVisible, Is.True);
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

            public Peer(bool host, LegacyDuelArt art, DuelPresentationSettings settings, Func<int> openingPlayerSource)
            {
                root = new GameObject(host ? "Ready Cancellation Host" : "Ready Cancellation Guest");
                arena = LegacyArenaView.Create(root.transform, art, settings);
                Duel = new LocalVersusController(root.transform, art, arena, () => { }, settings);
                ConstructorInfo constructor = typeof(OnlineVersusController).GetConstructor(Private, null,
                    new[] { typeof(Transform), typeof(LegacyDuelArt), typeof(LocalVersusController),
                        typeof(Action), typeof(Action<string>), typeof(Func<int>) }, null);
                Assert.That(constructor, Is.Not.Null);
                Online = (OnlineVersusController)constructor.Invoke(new object[] {
                    root.transform, art, Duel, (Action)(() => { }), (Action<string>)(json => Outgoing.Enqueue(json)),
                    openingPlayerSource
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

            public Pair(Func<int> hostOpeningSource = null, Func<int> guestOpeningSource = null)
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                runInBackground = Application.runInBackground;
                art = new LegacyDuelArt();
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                Host = new Peer(true, art, settings, hostOpeningSource ?? (() => 0));
                Guest = new Peer(false, art, settings, guestOpeningSource ?? (() => 0));
            }

            public void EnableCoinCards()
            {
                Host.Duel.StartCardsEnabled = Guest.Duel.StartCardsEnabled = true;
            }

            public void WaitForOpening()
                => AdvanceUntil(() => !Host.Duel.IsShowingStartCard && !Guest.Duel.IsShowingStartCard);

            public void FinishAsDraw()
            {
                // Empty finalized queues exercise the real command stream through the existing round limit.
                while (!Host.Duel.Match.IsFinished || !Guest.Duel.Match.IsFinished)
                {
                    if (Host.Duel.Match.Phase == LegacyDuelPhase.Planning)
                    {
                        if (Host.Duel.Match.CurrentPlanner == 0) HostAction(Host.Duel.TryCommit);
                        else GuestAction(OnlineVersusAction.Pass, Guest.Duel.TryCommit);
                    }
                    else AdvanceUntil(() => (Host.Duel.Match.IsFinished && Guest.Duel.Match.IsFinished) ||
                        (Host.Duel.Match.Phase == LegacyDuelPhase.Planning &&
                         Guest.Duel.Match.Phase == LegacyDuelPhase.Planning), .1f);
                }
                Assert.That(Host.Duel.Match.Outcome, Is.EqualTo(LocalVersusOutcome.Draw));
                Assert.That(Guest.Duel.Match.Outcome, Is.EqualTo(LocalVersusOutcome.Draw));
                AssertSameBattle();
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

            public void AdvanceUntil(Func<bool> complete, float delta = .01f)
            {
                for (int frame = 0; frame < 6000 && !complete(); frame++)
                {
                    Host.Online.Tick(delta, null);
                    Guest.Online.Tick(delta, null);
                    DeliverGuest();
                    DeliverHost();
                    Guest.Online.Tick(0f, null);
                }
                Assert.That(complete(), Is.True, "Both controllers must reach the requested battle boundary.");
            }

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
