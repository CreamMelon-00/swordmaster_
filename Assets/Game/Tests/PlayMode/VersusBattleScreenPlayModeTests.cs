using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The versus battle screen (<see cref="LocalVersusController"/>) is the campaign's duel HUD seen from the
    /// viewer's side: the guest's match mirrored so its own fighter is the player, input only on the viewer's turn and
    /// never slowing time, the timer on the current planner's clock, the PvP overlay, the start card ('나' / '상대'), the
    /// finishing blow and the single-play result card with 다시 겨루기 / 타이틀로. Driven host-only without a network, as
    /// <see cref="OnlineVersusController"/> drives it.</summary>
    public sealed class VersusBattleScreenPlayModeTests : InputTestFixture
    {
        private const float Seconds = LocalVersusController.PlanningSeconds;
        // One blow that ends a match from full health against an empty slot.
        private static readonly LegacySkill Finisher = new LegacySkill(90201, "검증 일격", 0, 999, 999,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "검증");

        [UnityTest]
        public IEnumerator HostScreen_IsTheDuelHud_OwnTurnActs_OpponentsTurnWaits_AndTwoPassesResolve()
        {
            yield return null;
            using (var scope = new VersusScope())
            {
                LocalVersusController versus = scope.Versus;
                scope.StartHost(LegacyInitialSkills.All, LegacyInitialSkills.All);
                LocalVersusMatch match = versus.Match;
                versus.Tick(0f, null);
                Assert.That(versus.Perspective.LocalPlayer, Is.Zero);
                Assert.That(versus.BattleHud.Root.activeSelf, Is.True, "Versus plays on the campaign's duel HUD.");
                Assert.That(versus.Hud.IsBattleVisible, Is.True);
                Assert.That(versus.BattleHud.CommitLabelText, Is.EqualTo(LocalVersusController.PassLabel));
                Assert.That(versus.BattleHud.StageText, Does.Contain(LocalVersusController.MatchTitle));
                Assert.That(versus.BattleHud.EnemyName, Is.EqualTo(LocalVersusController.OpponentName));
                Assert.That(scope.Arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.Elisa));
                Assert.That(scope.Arena.BackdropKind, Is.EqualTo(ArenaBackdropKind.SchoolCorridor));
                Assert.That(versus.Hud.TurnText, Is.EqualTo(LocalVersusHud.MyTurnText));
                Assert.That(versus.BattleHud.InputsHeld, Is.False);
                Assert.That(scope.HudText("Input/Keys/Current Q/Skill Name").text,
                    Is.EqualTo(match.Left.GetLane(0)[0].Name), "The host's own desk.");
                Button laneQ = scope.HudButton("Input/Keys/Current Q");
                Assert.That(laneQ.interactable, Is.True);

                laneQ.onClick.Invoke();
                Assert.That(match.Left.Queue.Count, Is.EqualTo(1));
                Assert.That(match.CurrentPlanner, Is.EqualTo(1));
                versus.Tick(0f, null);
                Assert.That(versus.BattleHud.GetQueuedSkillAnchor(true, 0), Is.Not.Null,
                    "The host's reservation rides its own queue row.");
                Assert.That(versus.Hud.TurnText, Is.EqualTo(LocalVersusHud.OpponentTurnText));
                Assert.That(versus.Hud.HintText, Is.EqualTo(LocalVersusHud.OpponentChoosingHint));
                Assert.That(versus.BattleHud.InputsHeld, Is.True);
                Assert.That(laneQ.interactable, Is.False, "The dock waits through the opponent's turn.");
                Assert.That(scope.HudButton("Input/Keys/AButton").interactable, Is.False);
                Assert.That(versus.TryPass(), Is.False, "Only the viewer's own turn acts.");

                // The timer shows the current planner's chess clock.
                versus.Tick(1.5f, null);
                Assert.That(versus.RightClock, Is.EqualTo(Seconds - 1.5f).Within(1e-4f));
                Assert.That(versus.LeftClock, Is.EqualTo(Seconds));
                Assert.That(versus.ShownClock, Is.EqualTo(versus.RightClock));
                Assert.That(scope.HudText("Input/Keys/TimerBG/Time Remaining").text, Is.EqualTo("19s"));

                Assert.That(versus.TryApplyOnlinePeerAction(OnlineVersusAction.Pass, -1, 1), Is.True);
                versus.Tick(0f, null);
                Assert.That(versus.Hud.TurnText, Is.EqualTo(LocalVersusHud.MyTurnText));
                Assert.That(versus.Hud.HintText, Is.EqualTo(LocalVersusHud.PassToResolveHint));
                Button seal = scope.HudButton("Input/Keys/AButton");
                Assert.That(seal.interactable, Is.True);
                seal.onClick.Invoke();
                Assert.That(match.Phase, Is.EqualTo(LegacyDuelPhase.Resolving), "The seal is 패스: the second in a row resolves.");
                Assert.That(versus.IsResolving, Is.True);
                versus.Tick(0f, null);
                Assert.That(versus.Hud.TurnText, Is.EqualTo(LocalVersusHud.ResolvingText));

                Assert.That(scope.TickUntil(() => match.RoundNumber == 2 && !versus.IsResolving), Is.True,
                    "A committed turn plays out in finite time.");
                Assert.That(versus.BattleHud.LogCount, Is.EqualTo(1), "The resolved slot is in the combat log.");
                Assert.That(match.OpeningPlayer, Is.EqualTo(1));
                Assert.That(versus.LeftClock, Is.EqualTo(Seconds));
                Assert.That(versus.RightClock, Is.EqualTo(Seconds));
                Assert.That(versus.Hud.TurnText, Is.EqualTo(LocalVersusHud.OpponentTurnText));
            }
        }

        [UnityTest]
        public IEnumerator GuestScreen_MirrorsTheMatch_AndItsRequestsWaitForTheHost()
        {
            yield return null;
            using (var scope = new VersusScope())
            {
                LocalVersusController versus = scope.Versus;
                LegacySkill[] guestSkills =
                {
                    Skill(90101, "검증 베기", 0, LegacySkillKind.Attack, LegacySkillProperty.Slash),
                    Skill(90102, "검증 막기", 1, LegacySkillKind.Defence, LegacySkillProperty.Defence),
                    Skill(90103, "검증 받아치기", 2, LegacySkillKind.Attack, LegacySkillProperty.Hit),
                };
                scope.StartGuest(LegacyInitialSkills.All, guestSkills, opening: 1);
                LocalVersusMatch match = versus.Match;
                versus.Tick(0f, null);
                Assert.That(versus.Perspective.LocalPlayer, Is.EqualTo(1));
                Assert.That(scope.HudText("Input/Keys/Current Q/Skill Name").text, Is.EqualTo("검증 베기"),
                    "The guest's desk is its own (Right's) lanes.");
                Assert.That(versus.Hud.TurnText, Is.EqualTo(LocalVersusHud.MyTurnText));
                Assert.That(versus.BattleHud.InputsHeld, Is.False);

                scope.HudButton("Input/Keys/Current Q").onClick.Invoke();
                Assert.That(scope.RequestedActions, Is.EqualTo(new[] { OnlineVersusAction.QueueLane }));
                Assert.That(scope.RequestedLanes, Is.EqualTo(new[] { 0 }));
                Assert.That(match.Right.Queue, Is.Empty, "The guest's command waits for the host's approval.");
                versus.Tick(0f, null);
                Assert.That(versus.Hud.HintText, Is.EqualTo(LocalVersusHud.RequestPendingHint));
                Assert.That(versus.BattleHud.InputsHeld, Is.True);
                Assert.That(versus.TryPass(), Is.False, "A second command waits for the first.");

                Assert.That(versus.ApplyOnlineAcceptedAction(1, OnlineVersusAction.QueueLane, 0, 1), Is.True);
                versus.Tick(0f, null);
                Assert.That(match.Right.Queue.Count, Is.EqualTo(1));
                Assert.That(versus.BattleHud.GetQueuedSkillAnchor(true, 0), Is.Not.Null,
                    "Right's queue is the guest's own row.");
                Assert.That(versus.BattleHud.GetQueuedSkillAnchor(false, 0), Is.Null);
                Assert.That(versus.Hud.TurnText, Is.EqualTo(LocalVersusHud.OpponentTurnText));

                Assert.That(versus.ApplyOnlineAcceptedAction(0, OnlineVersusAction.Breath, -1, 1), Is.True);
                versus.Tick(0f, null);
                Assert.That(versus.BattleHud.GetQueuedSkillAnchor(false, 0), Is.Not.Null,
                    "Left's queue is the opponent's row.");
                Assert.That(versus.Hud.TurnText, Is.EqualTo(LocalVersusHud.MyTurnText));

                // The host's clocks arrive by match side; the timer shows the planner's (the guest's own now).
                versus.SyncOnlineClocks(12.5f, 7.2f);
                Assert.That(versus.ShownClock, Is.EqualTo(7.2f).Within(1e-4f));
                Assert.That(scope.HudText("Input/Keys/TimerBG/Time Remaining").text, Is.EqualTo("8s"));
                // Only the host passes for a player whose clock ran out.
                versus.SyncOnlineClocks(12.5f, 0f);
                versus.Tick(.5f, null);
                Assert.That(match.CurrentPlanner, Is.EqualTo(1));
                Assert.That(match.ConsecutivePasses, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator Guest_NeverAsksTheHostForACommandItsOwnMatchRefuses()
        {
            yield return null;
            using (var scope = new VersusScope())
            {
                LocalVersusController versus = scope.Versus;
                LegacySkill[] guestSkills =
                {
                    new LegacySkill(90104, "검증 큰 베기", 4, 5, 5, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "검증"),
                    Skill(90102, "검증 막기", 1, LegacySkillKind.Defence, LegacySkillProperty.Defence),
                    Skill(90103, "검증 받아치기", 2, LegacySkillKind.Attack, LegacySkillProperty.Hit),
                };
                scope.StartGuest(LegacyInitialSkills.All, guestSkills, opening: 1);
                LocalVersusMatch match = versus.Match;
                versus.Tick(0f, null);
                Assert.That(versus.Hud.TurnText, Is.EqualTo(LocalVersusHud.MyTurnText));
                Assert.That(match.Right.Act, Is.EqualTo(3));

                // As on the host and in single play, a lane ACT cannot pay and 넘기기 with nothing to turn do nothing.
                Assert.That(versus.TryQueueLane(0), Is.False);
                Assert.That(versus.TryCycle(), Is.False);
                Assert.That(scope.RequestedActions, Is.Empty, "Nothing the host would refuse is sent.");
                versus.Tick(0f, null);
                Assert.That(versus.Hud.HintText, Is.Not.EqualTo(LocalVersusHud.RequestPendingHint));
                Assert.That(versus.BattleHud.InputsHeld, Is.False, "The dock stays open for the next command.");

                // Three 숨고르기 a turn: the fourth is never asked for.
                for (int breath = 0; breath < LocalVersusMatch.MaximumBreathsPerTurn; breath++)
                {
                    Assert.That(versus.TryBreath(), Is.True);
                    Assert.That(versus.ApplyOnlineAcceptedAction(1, OnlineVersusAction.Breath, -1, 1), Is.True);
                    Assert.That(versus.ApplyOnlineAcceptedAction(0, OnlineVersusAction.Pass, -1, 1), Is.True);
                }
                Assert.That(versus.TryBreath(), Is.False);
                Assert.That(scope.RequestedActions.Count, Is.EqualTo(LocalVersusMatch.MaximumBreathsPerTurn));

                // A command the match takes still waits for the host.
                Assert.That(versus.TryQueueLane(1), Is.True);
                Assert.That(scope.RequestedActions[scope.RequestedActions.Count - 1],
                    Is.EqualTo(OnlineVersusAction.QueueLane));
                Assert.That(scope.RequestedLanes[scope.RequestedLanes.Count - 1], Is.EqualTo(1));
                versus.Tick(0f, null);
                Assert.That(versus.Hud.HintText, Is.EqualTo(LocalVersusHud.RequestPendingHint));
            }
        }

        [UnityTest]
        public IEnumerator TabCard_AndTheOpponentsQueueCard_CountTheOpponentsCycleUses()
        {
            yield return null;
            using (var scope = new VersusScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                LocalVersusController versus = scope.Versus;
                LegacySkill hone = LegacySkillDefinitions.Skill(46);
                scope.StartHost(LegacyInitialSkills.All, new[] { hone });
                LocalVersusMatch match = versus.Match;
                // The opponent uses its 순환 skill once.
                Assert.That(versus.TryPass(), Is.True);
                Assert.That(versus.TryApplyOnlinePeerAction(OnlineVersusAction.QueueLane, hone.LaneIndex, 1), Is.True);
                Assert.That(versus.TryPass(), Is.True);
                Assert.That(versus.TryApplyOnlinePeerAction(OnlineVersusAction.Pass, -1, 1), Is.True);
                Assert.That(scope.TickUntil(() => match.RoundNumber == 2 && !versus.IsResolving), Is.True);
                Assert.That(match.Right.CycleUses(hone.Id), Is.EqualTo(1));

                // It opens the next turn with the same skill: its card reads its own count, cost and power.
                Assert.That(versus.TryApplyOnlinePeerAction(OnlineVersusAction.QueueLane, hone.LaneIndex, 2), Is.True);
                versus.Tick(0f, null);
                Transform card = scope.HudPart("Enemy Requests").GetChild(0);
                Assert.That(TextUnder(card, "Cycle Count").text, Is.EqualTo("1/9"));
                Assert.That(TextUnder(card, "Cycle Count").transform.parent.gameObject.activeSelf, Is.True);

                Press(keyboard.tabKey);
                yield return null;
                versus.Tick(0f, keyboard);
                Assert.That(versus.IsInspecting, Is.True);
                Transform explanation = scope.HudPart("Enemy Skill Explain");
                Assert.That(explanation.gameObject.activeSelf, Is.True);
                Assert.That(TextUnder(explanation, "Keyword 1 Text").text, Is.EqualTo("순환 1/9"));
                Assert.That(TextUnder(explanation, "ACT Value").text,
                    Is.EqualTo(match.Right.EffectiveCost(hone).ToString()));
                Assert.That(TextUnder(explanation, "ACT Value").text, Is.EqualTo((hone.Cost + 1).ToString()));
                match.Right.EffectivePowerRange(hone, out int min, out int max);
                Assert.That(TextUnder(explanation, "Enemy Detail Values").text, Is.EqualTo(min + "–" + max));
                Release(keyboard.tabKey);
                yield return null;
                versus.Tick(0f, keyboard);
                Assert.That(versus.IsInspecting, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator HeldLane_ExplainsWithoutSlowingTheClock_TapQueues_TabReadsTheOpponentsQueue_SpacePasses()
        {
            yield return null;
            using (var scope = new VersusScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                LocalVersusController versus = scope.Versus;
                scope.StartHost(LegacyInitialSkills.All, LegacyInitialSkills.All);
                LocalVersusMatch match = versus.Match;
                float tap = scope.Settings.LaneTapSeconds, explain = scope.Settings.ExplanationHoldSeconds;

                // The frame a press is first seen counts no time held; the held frames after it do.
                Press(keyboard.qKey);
                yield return null;
                versus.Tick(0f, keyboard);
                yield return null;
                float before = versus.LeftClock;
                versus.Tick(tap + explain, keyboard);
                Assert.That(versus.ExplainedSkill, Is.SameAs(match.Left.GetLane(0)[0]));
                Assert.That(scope.HudPart("Skill Explain").gameObject.activeSelf, Is.True,
                    "A held lane opens the single-play explanation card.");
                Assert.That(versus.LeftClock, Is.EqualTo(before - (tap + explain)).Within(1e-4f),
                    "Reading never slows the clock: it is the host's.");
                Release(keyboard.qKey);
                yield return null;
                versus.Tick(0f, keyboard);
                Assert.That(versus.ExplainedSkill, Is.Null);
                Assert.That(match.Left.Queue, Is.Empty, "A hold's release queues nothing.");

                Press(keyboard.qKey);
                yield return null;
                versus.Tick(0f, keyboard);
                yield return null;
                versus.Tick(tap * .5f, keyboard);
                Release(keyboard.qKey);
                yield return null;
                versus.Tick(0f, keyboard);
                Assert.That(match.Left.Queue.Count, Is.EqualTo(1), "A tap queues on release.");
                Assert.That(match.CurrentPlanner, Is.EqualTo(1));

                Assert.That(versus.TryApplyOnlinePeerAction(OnlineVersusAction.QueueLane, 0, 1), Is.True);
                Press(keyboard.tabKey);
                yield return null;
                before = versus.LeftClock;
                versus.Tick(.5f, keyboard);
                Assert.That(versus.IsInspecting, Is.True);
                Assert.That(scope.HudPart("Enemy Skill Explain").gameObject.activeSelf, Is.True);
                Assert.That(scope.HudText("Enemy Skill Explain/Name").text, Is.EqualTo(match.Right.Queue[0].Name));
                Assert.That(versus.LeftClock, Is.EqualTo(before - .5f).Within(1e-4f), "Tab never slows the clock.");
                Assert.That(versus.TryQueueLane(1), Is.False, "Tab holds every command but 패스.");
                Release(keyboard.tabKey);
                yield return null;
                versus.Tick(0f, keyboard);
                Assert.That(versus.IsInspecting, Is.False);

                Press(keyboard.spaceKey);
                yield return null;
                versus.Tick(0f, keyboard);
                Release(keyboard.spaceKey);
                Assert.That(match.ConsecutivePasses, Is.EqualTo(1), "Space is the seal: 패스.");
                Assert.That(match.CurrentPlanner, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator StartCard_NamesMeAndTheOpponent_HoldsTheClocks_AndCannotBeSkipped()
        {
            yield return null;
            using (var scope = new VersusScope())
            {
                LocalVersusController versus = scope.Versus;
                versus.StartCardsEnabled = true;
                scope.StartHost(LegacyInitialSkills.All, LegacyInitialSkills.All);
                DuelStartCard card = versus.StartCard;
                Assert.That(versus.IsShowingStartCard, Is.True);
                Assert.That(card.Timeline.Seconds, Is.EqualTo(scope.Settings.StartCardSeconds).Within(1e-4f));
                Assert.That(card.PlayerLabel.text, Is.EqualTo(LocalVersusController.LocalName));
                Assert.That(card.OpponentLabel.text, Is.EqualTo(LocalVersusController.OpponentName));
                Assert.That(card.OpponentFigure.sprite, Is.SameAs(card.PlayerFigure.sprite), "Both fighters are 엘리사.");
                Assert.That(card.OpponentFigure.rectTransform.localScale.x, Is.LessThan(0f),
                    "The opponent's 엘리사 faces her.");
                Assert.That(versus.BattleHud.Root.activeSelf, Is.False, "The duel HUD waits under the card.");
                Assert.That(versus.Hud.IsBattleVisible, Is.False);
                Assert.That(versus.TryQueueLane(0), Is.False);

                card.SkipButton.onClick.Invoke();
                Assert.That(versus.IsShowingStartCard, Is.True, "Neither peer can cut its card short.");
                versus.Tick(scope.Settings.StartCardSeconds * .5f, null);
                Assert.That(versus.LeftClock, Is.EqualTo(Seconds), "No clock runs under the card.");
                versus.Tick(scope.Settings.StartCardSeconds * .5f + .01f, null);
                Assert.That(versus.IsShowingStartCard, Is.False);
                Assert.That(versus.BattleHud.Root.activeSelf, Is.True);
                Assert.That(versus.Hud.IsBattleVisible, Is.True);
                versus.Tick(1f, null);
                Assert.That(versus.LeftClock, Is.EqualTo(Seconds - 1f).Within(1e-4f));
            }
        }

        [UnityTest]
        public IEnumerator HostKnockout_PlaysTheFinishingBlow_ThenTheResultCard_AndRematchWaitsForTheOpponent()
        {
            yield return null;
            using (var scope = new VersusScope())
            {
                LocalVersusController versus = scope.Versus;
                scope.StartHost(new[] { Finisher }, LegacyInitialSkills.All);
                LocalVersusMatch match = versus.Match;
                Assert.That(versus.TryQueueLane(0), Is.True);
                Assert.That(versus.TryApplyOnlinePeerAction(OnlineVersusAction.Pass, -1, 1), Is.True);
                Assert.That(versus.TryPass(), Is.True);
                bool sawFinale = false;
                Assert.That(scope.TickUntil(() =>
                {
                    sawFinale |= versus.Finale.IsRunning;
                    return versus.IsShowingResult;
                }), Is.True);
                Assert.That(sawFinale, Is.True, "The finishing blow plays before the result.");
                Assert.That(match.Outcome, Is.EqualTo(LocalVersusOutcome.LeftVictory));
                Assert.That(scope.ResultText("Result Heading").text, Is.EqualTo("승리"));
                Assert.That(scope.ResultText("Result Stage").text, Is.EqualTo(LocalVersusController.MatchTitle));
                Assert.That(scope.ResultText("Result Rounds").text, Is.EqualTo("1"));
                Assert.That(scope.ResultText("Result Player HP").text, Is.EqualTo("100"));
                Assert.That(scope.ResultText("Result Enemy HP").text, Is.EqualTo("0"));
                Button retry = scope.ResultButton("Result Retry"), lobby = scope.ResultButton("Result Lobby");
                Assert.That(Caption(retry), Is.EqualTo(BattleResultHud.VersusRematchCaption));
                Assert.That(Caption(lobby), Is.EqualTo(BattleResultHud.VersusExitCaption));
                Assert.That(scope.ResultButton("Result Next Stage").gameObject.activeSelf, Is.False);
                Assert.That(versus.BattleHud.Root.activeSelf, Is.False);
                Assert.That(versus.Hud.IsBattleVisible, Is.False);
                Assert.That(versus.Finale.Letterbox.Amount, Is.Zero, "The bars go as the result comes.");

                retry.onClick.Invoke();
                Assert.That(scope.Rematches, Is.EqualTo(1));
                Assert.That(Caption(retry), Is.EqualTo(BattleResultHud.VersusWaitingCaption));
                Assert.That(retry.interactable, Is.False);

                // Both agreed: the host starts the rematch on the same screens, with the short start card.
                versus.StartCardsEnabled = true;
                scope.StartHost(new[] { Finisher }, LegacyInitialSkills.All, opening: 1);
                Assert.That(versus.Match, Is.Not.SameAs(match));
                Assert.That(versus.IsShowingResult, Is.False);
                Assert.That(versus.StartCard.Timeline.Seconds,
                    Is.EqualTo(scope.Settings.StartCardRetrySeconds).Within(1e-4f));
            }
        }

        [UnityTest]
        public IEnumerator GuestKnockedOut_ReadsTheResultFromItsOwnSide_AndOnlySendsItsRematchVote()
        {
            yield return null;
            using (var scope = new VersusScope())
            {
                LocalVersusController versus = scope.Versus;
                scope.StartGuest(new[] { Finisher }, LegacyInitialSkills.All, opening: 0);
                LocalVersusMatch match = versus.Match;
                Assert.That(versus.ApplyOnlineAcceptedAction(0, OnlineVersusAction.QueueLane, 0, 1), Is.True);
                Assert.That(versus.ApplyOnlineAcceptedAction(1, OnlineVersusAction.Pass, -1, 1), Is.True);
                Assert.That(versus.ApplyOnlineAcceptedAction(0, OnlineVersusAction.Pass, -1, 1), Is.True);
                Assert.That(scope.TickUntil(() => versus.IsShowingResult), Is.True);
                Assert.That(versus.Perspective.Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
                Assert.That(scope.ResultText("Result Heading").text, Is.EqualTo("패배"));
                Assert.That(scope.ResultText("Result Player HP").text, Is.EqualTo("0"), "내 남은 HP is the guest's.");
                Assert.That(scope.ResultText("Result Enemy HP").text, Is.EqualTo("100"));

                scope.ResultButton("Result Retry").onClick.Invoke();
                Assert.That(scope.Rematches, Is.EqualTo(1));
                Assert.That(versus.Match, Is.SameAs(match), "The host starts the rematch, never the guest.");
                scope.ResultButton("Result Lobby").onClick.Invoke();
                Assert.That(scope.Exits, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator HostAndGuest_SpendTheSameTimeFromCommitToResult()
        {
            yield return null;
            using (var host = new VersusScope())
            using (var guest = new VersusScope())
            {
                host.StartHost(new[] { Finisher }, LegacyInitialSkills.All);
                guest.StartGuest(new[] { Finisher }, LegacyInitialSkills.All, opening: 0);
                // The same commands reach both screens.
                Assert.That(host.Versus.TryQueueLane(0), Is.True);
                Assert.That(guest.Versus.ApplyOnlineAcceptedAction(0, OnlineVersusAction.QueueLane, 0, 1), Is.True);
                Assert.That(host.Versus.TryApplyOnlinePeerAction(OnlineVersusAction.Pass, -1, 1), Is.True);
                Assert.That(guest.Versus.ApplyOnlineAcceptedAction(1, OnlineVersusAction.Pass, -1, 1), Is.True);
                Assert.That(host.Versus.TryPass(), Is.True);
                Assert.That(guest.Versus.ApplyOnlineAcceptedAction(0, OnlineVersusAction.Pass, -1, 1), Is.True);
                int hostFrames = FramesToResult(host, out int hostFinale);
                int guestFrames = FramesToResult(guest, out int guestFinale);
                Assert.That(hostFinale, Is.GreaterThan(0));
                Assert.That(guestFinale, Is.EqualTo(hostFinale),
                    "The finishing blow lasts as long on the screen that won as on the one that lost.");
                Assert.That(guestFrames, Is.EqualTo(hostFrames),
                    "The mirrored screen plays the same presentation for as long.");
            }
        }

        [UnityTest]
        public IEnumerator ExitQuestion_NeverPausesAnOnlineMatch_AndSitsOverTheResultCard()
        {
            yield return null;
            using (var scope = new VersusScope())
            {
                LocalVersusController versus = scope.Versus;
                scope.StartHost(LegacyInitialSkills.All, LegacyInitialSkills.All);
                scope.OverlayButton("Versus Exit").onClick.Invoke();
                Assert.That(versus.Hud.IsExitConfirming, Is.True);
                versus.Tick(1f, null);
                Assert.That(versus.LeftClock, Is.EqualTo(Seconds - 1f).Within(1e-4f), "The host's clock keeps running.");
                Assert.That(versus.BattleHud.InputsHeld, Is.True);
                Assert.That(versus.TryQueueLane(0), Is.False);
                Canvas question = scope.OverlayPart("Versus Exit Confirmation").GetComponent<Canvas>();
                Assert.That(question, Is.Not.Null);
                Assert.That(question.overrideSorting, Is.True);
                Assert.That(question.sortingOrder,
                    Is.GreaterThan(versus.ResultHud.Root.GetComponent<Canvas>().sortingOrder));
                scope.OverlayButton("Versus Cancel Exit").onClick.Invoke();
                Assert.That(versus.Hud.IsExitConfirming, Is.False);
                versus.Tick(0f, null);
                Assert.That(versus.BattleHud.InputsHeld, Is.False);
                Assert.That(scope.Exits, Is.Zero);

                scope.OverlayButton("Versus Exit").onClick.Invoke();
                scope.OverlayButton("Versus Confirm Exit").onClick.Invoke();
                Assert.That(scope.Exits, Is.EqualTo(1));
            }
        }

        private static int FramesToResult(VersusScope scope, out int finaleFrames)
        {
            int frames = 0;
            finaleFrames = 0;
            while (!scope.Versus.IsShowingResult && frames < 4000)
            {
                scope.Versus.Tick(.05f, null);
                frames++;
                if (scope.Versus.Finale.IsRunning) finaleFrames++;
            }
            Assert.That(scope.Versus.IsShowingResult, Is.True);
            return frames;
        }

        private static LegacySkill Skill(int id, string name, int lane, LegacySkillKind kind, LegacySkillProperty property)
            => new LegacySkill(id, name, 0, 5, 5, kind, property, 1, lane, "검증");

        private static string Caption(Button button) => button.GetComponentInChildren<Text>(true).text;

        private static Text TextUnder(Transform root, string name)
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
                if (text.name == name) return text;
            Assert.Fail("Missing text: " + name);
            return null;
        }

        /// <summary>A versus battle on its own arena, with the campaign's controller (if the scene has one) held still.</summary>
        private sealed class VersusScope : IDisposable
        {
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            public readonly GameObject Host;
            public readonly LegacyDuelArt Art;
            public readonly DuelPresentationSettings Settings;
            public readonly LegacyArenaView Arena;
            public readonly LocalVersusController Versus;
            public readonly List<OnlineVersusAction> RequestedActions = new List<OnlineVersusAction>();
            public readonly List<int> RequestedLanes = new List<int>();
            public int Rematches, Exits;

            public VersusScope()
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null)
                {
                    sceneEnabled = scene.enabled;
                    scene.enabled = false;
                }
                Host = new GameObject("Versus Battle Screen Test");
                Art = new LegacyDuelArt();
                Settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                Arena = LegacyArenaView.Create(Host.transform, Art, Settings);
                Versus = new LocalVersusController(Host.transform, Art, Arena, () => Exits++, Settings);
            }

            public void StartHost(IReadOnlyList<LegacySkill> left, IReadOnlyList<LegacySkill> right, int opening = 0)
                => Versus.StartOnline(7, 0, true, opening, left, right, Request, () => Rematches++);

            public void StartGuest(IReadOnlyList<LegacySkill> left, IReadOnlyList<LegacySkill> right, int opening = 0)
                => Versus.StartOnline(7, 1, false, opening, left, right, Request, () => Rematches++);

            private void Request(OnlineVersusAction action, int lane)
            {
                RequestedActions.Add(action);
                RequestedLanes.Add(lane);
            }

            public bool TickUntil(Func<bool> done, int frames = 2000)
            {
                for (int frame = 0; frame < frames && !done(); frame++) Versus.Tick(.05f, null);
                return done();
            }

            public Transform HudPart(string path)
            {
                Transform part = Versus.BattleHud.Root.transform.Find(path);
                Assert.That(part, Is.Not.Null, "Missing duel HUD part: " + path);
                return part;
            }

            public Button HudButton(string path) => HudPart(path).GetComponent<Button>();
            public Text HudText(string path) => HudPart(path).GetComponent<Text>();
            public Transform OverlayPart(string name) => Named(Versus.Hud.Root, name);
            public Button OverlayButton(string name) => OverlayPart(name).GetComponent<Button>();
            public Text ResultText(string name) => Named(Versus.ResultHud.Root, name).GetComponent<Text>();
            public Button ResultButton(string name) => Named(Versus.ResultHud.Root, name).GetComponent<Button>();

            private static Transform Named(GameObject root, string name)
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name) return child;
                Assert.Fail("Missing UI object: " + name);
                return null;
            }

            public void Dispose()
            {
                Versus.Dispose();
                Arena.Dispose();
                Art.Dispose();
                Object.Destroy(Host);
                Object.Destroy(Settings);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
