using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Save;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>Versus on the scene's own controller: the title's 온라인 친구 대전 (versus's only entry), then the match the
    /// room's 대전 시작 would start, run host-only without a network on the shared engine
    /// (<see cref="DuelPrototypeController.LocalVersus"/>): the guest's commands arrive as the host accepts them. Covers the
    /// arena-driven loop into the next turn, the mirrored 엘리사, each side's loadout, the 20-turn draw on the result card
    /// and the rematch, and that versus never touches the campaign save.</summary>
    public sealed class LocalVersusPlayModeTests
    {
        private const int Seed = 11;

        [UnityTest]
        public IEnumerator HostOnlyMatch_CommitsWholePublicQueues_ResolvesAndLeavesToTitleWithoutChangingSave()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                string saveBefore = scope.CreateSaveAndReadText();
                LocalVersusController versus = scope.StartHostOnly(LegacyInitialSkills.All, LegacyInitialSkills.All);
                Assert.That(controller.IsInOnlineVersus, Is.True);
                Assert.That(controller.IsInTitle, Is.False);
                Assert.That(controller.IsInLobby, Is.False);
                Assert.That(controller.AutoSaveEnabled, Is.False);
                // The title's entry opens every match with the start card, on both peers alike.
                Assert.That(versus.StartCardsEnabled, Is.True);
                Assert.That(versus.IsShowingStartCard, Is.EqualTo(versus.PresentationSettings.StartCardSeconds > 0f));
                scope.FinishStartCard();
                Assert.That(versus.Hud.IsBattleVisible, Is.True);
                Assert.That(versus.BattleHud.Root.activeSelf, Is.True);

                LocalVersusMatch match = versus.Match;
                Assert.That(match.RoundNumber, Is.EqualTo(1));
                Assert.That(match.OpeningPlayer, Is.Zero);
                Assert.That(match.CurrentPlanner, Is.Zero);
                Assert.That(match.Left.Queue, Is.Empty);
                Assert.That(match.Right.Queue, Is.Empty);

                // The host builds its entire queue before handing the planning turn to the guest.
                versus.Tick(0f, null);
                Assert.That(versus.Hud.TurnText, Is.EqualTo(LocalVersusHud.MyTurnText));
                Button lane = ButtonNamed(versus.BattleHud.Root, "Current Q");
                Assert.That(lane.interactable, Is.True);
                lane.onClick.Invoke();
                Assert.That(match.Left.Queue.Count, Is.EqualTo(1));
                Assert.That(match.Right.Queue, Is.Empty);
                Assert.That(match.CurrentPlanner, Is.Zero);
                versus.Tick(0f, null);
                Assert.That(versus.Hud.TurnText, Is.EqualTo(LocalVersusHud.MyTurnText));
                Assert.That(lane.interactable, Is.True, "The host keeps its desk while building the queue.");
                Assert.That(versus.TryBreath(), Is.True);
                Assert.That(match.Left.Queue.Count, Is.EqualTo(2));
                Button commit = ButtonNamed(versus.BattleHud.Root, "AButton");
                commit.onClick.Invoke();
                Assert.That(match.CurrentPlanner, Is.EqualTo(1));
                Assert.That(match.CommittedPlannerCount, Is.EqualTo(1));
                versus.Tick(0f, null);
                Assert.That(lane.interactable, Is.False, "The opponent's planning turn holds the host's desk.");
                lane.onClick.Invoke();
                Assert.That(match.Left.Queue.Count, Is.EqualTo(2), "A committed queue cannot be changed.");
                Assert.That(match.Right.Queue, Is.Empty, "Nobody reserves for the opponent on this screen.");

                // The guest's reservation, as the host accepts it.
                Assert.That(versus.TryApplyOnlinePeerAction(OnlineVersusAction.QueueLane, 0, 1), Is.True);
                Assert.That(match.Left.Queue.Count, Is.EqualTo(2));
                Assert.That(match.Right.Queue.Count, Is.EqualTo(1));
                Assert.That(match.CurrentPlanner, Is.EqualTo(1));
                versus.Tick(0f, null);
                Assert.That(versus.Hud.TurnText, Is.EqualTo(LocalVersusHud.OpponentTurnText));
                Assert.That(versus.BattleHud.GetQueuedSkillAnchor(false, 0), Is.Not.Null,
                    "The opponent's reservation rides its public queue row.");

                // The guest finishes its response queue; its commit resolves both complete queues.
                Assert.That(match.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
                Assert.That(versus.TryApplyOnlinePeerAction(OnlineVersusAction.Pass, -1, 1), Is.True);
                Assert.That(match.Phase, Is.EqualTo(LegacyDuelPhase.Resolving));

                int frames = 0;
                while (!match.IsFinished &&
                       !(match.RoundNumber == 2 && match.Phase == LegacyDuelPhase.Planning) &&
                       frames++ < 2000)
                    versus.Tick(.05f, null);
                Assert.That(frames, Is.LessThan(2000), "A committed turn must finish in finite presentation time.");
                Assert.That(match.IsFinished || match.RoundNumber == 2, Is.True);
                if (!match.IsFinished)
                {
                    Assert.That(match.OpeningPlayer, Is.EqualTo(1), "The first reservation alternates next turn.");
                    Assert.That(match.CurrentPlanner, Is.EqualTo(1));
                }
                Assert.That(File.ReadAllText(scope.Store.Path), Is.EqualTo(saveBefore),
                    "A versus match must not award, advance, or overwrite campaign progress.");

                // 타이틀로 leaves the match and the room together.
                ButtonNamed(versus.Hud.Root, "Versus Exit").onClick.Invoke();
                Assert.That(versus.Hud.IsExitConfirming, Is.True);
                ButtonNamed(versus.Hud.Root, "Versus Confirm Exit").onClick.Invoke();
                Assert.That(controller.IsInTitle, Is.True);
                Assert.That(controller.TitleHud.IsVisible, Is.True);
                Assert.That(controller.IsInOnlineVersus, Is.False);
                Assert.That(versus.IsActive, Is.False);
                Assert.That(versus.BattleHud.Root.activeSelf, Is.False);
                Assert.That(File.ReadAllText(scope.Store.Path), Is.EqualTo(saveBefore));
            }
        }

        [UnityTest]
        public IEnumerator VersusKeepsFinalLocalPlayerAndMirroredElisaOpponentAcrossAttackAndReactions()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                scope.StartHostOnly(LegacyInitialSkills.All, LegacyInitialSkills.All);
                LegacyArenaView arena = scope.Controller.ArenaView;
                Assert.That(arena.BackdropKind, Is.EqualTo(ArenaBackdropKind.SchoolCorridor));
                Assert.That(arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.Elisa));
                Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("final-idle-frame-00"));
                Assert.That(arena.EnemyRenderer.sprite.name, Is.EqualTo("idle-frame-01"));
                Assert.That(arena.EnemyRenderer.sprite, Is.Not.SameAs(arena.PlayerRenderer.sprite),
                    "The local FinalPlayer and the opposing Elisa use their own authored animation sets.");
                Assert.That(arena.PlayerRenderer.flipX, Is.False);
                Assert.That(arena.EnemyRenderer.flipX, Is.True);

                var attack = new LegacySkill(991, "검증", 1, 1, 1,
                    LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty);
                arena.BeginSlot(attack, attack);
                Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("final-slash-frame-00"));
                Assert.That(arena.EnemyRenderer.sprite.name, Does.Match("^slash(?:-[23])?-frame-01$"),
                    "The opposing fighter must keep Elisa's attack frames beyond the first idle frame.");

                arena.PresentHit(true, 0, 1, true, false, 1);
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("poses-block"));
                arena.PresentHit(true, 2, 0, false, false, 1);
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("poses-hurt"));
                Assert.That(arena.EnemyRenderer.flipX, Is.True);

                arena.SetEnemyAppearance(EnemyAppearance.CadetA);
                arena.Reset();
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("cadet-a-idle-frame-"));
                Assert.That(arena.EnemyRenderer.flipX, Is.False,
                    "Switching away from versus must restore the normal opposing facing.");
            }
        }

        [UnityTest]
        public IEnumerator EachSidesLoadout_ReachesItsOwnFighter_AndEachScreensDeskIsItsOwn()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                LegacySkill reward = LegacySkillDefinitions.AcquisitionSkills[0];
                int lane = reward.LaneIndex;
                string window = "Input/Keys/Current " + "QWE"[lane] + "/Skill Name";
                var hostLoadout = new VersusLoadout();
                var guestLoadout = new VersusLoadout();
                int[] guestDefault = guestLoadout.ExportIds();
                string guestLaneSkill = guestLoadout.GetSkill(lane, 0).Name;
                Assert.That(hostLoadout.TryPlaceSkill(reward.Id, lane, 0), Is.True);
                Assert.That(hostLoadout.GetSkillId(lane, 0), Is.EqualTo(reward.Id));
                Assert.That(guestLaneSkill, Is.Not.EqualTo(reward.Name));

                // The host's screen: its loadout is Left's, and its desk shows it.
                LocalVersusController versus = scope.StartHostOnly(hostLoadout.ToSkills(), guestLoadout.ToSkills());
                scope.FinishStartCard();
                Assert.That(versus.Match.Left.GetLane(lane)[0].Id, Is.EqualTo(reward.Id));
                AssertLineup(versus.Match.Right, guestDefault, "The guest keeps its own default lineup.");
                Assert.That(TextAt(versus.BattleHud.Root, window).text, Is.EqualTo(reward.Name));

                // The guest's screen of the same match: Right's lanes are its own desk; the host's reward is not on it.
                versus = scope.StartGuestOnly(hostLoadout.ToSkills(), guestLoadout.ToSkills());
                scope.FinishStartCard();
                Assert.That(versus.Perspective.LocalPlayer, Is.EqualTo(1));
                Assert.That(versus.Match.Left.GetLane(lane)[0].Id, Is.EqualTo(reward.Id));
                AssertLineup(versus.Match.Right, guestDefault, "The guest keeps its own default lineup.");
                Assert.That(TextAt(versus.BattleHud.Root, window).text, Is.EqualTo(guestLaneSkill));
            }
        }

        [UnityTest]
        public IEnumerator TwentyEmptyRounds_DrawOnTheResultCard_AndTheRematchSwitchesTheOpeningPlayer()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                string saveBefore = scope.CreateSaveAndReadText();
                int rematchVotes = 0;
                LocalVersusController versus = scope.StartHostOnly(LegacyInitialSkills.All, LegacyInitialSkills.All,
                    () => rematchVotes++);
                scope.FinishStartCard();

                LocalVersusMatch first = versus.Match;
                Assert.That(first.RoundLimit, Is.EqualTo(LocalVersusController.RoundLimit));
                Assert.That(first.RoundLimit, Is.EqualTo(20));
                for (int round = 1; round <= first.RoundLimit; round++)
                {
                    Assert.That(first.RoundNumber, Is.EqualTo(round));
                    Assert.That(first.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
                    Assert.That(first.OpeningPlayer, Is.EqualTo((round - 1) % 2));
                    Assert.That(first.CurrentPlanner, Is.EqualTo(first.OpeningPlayer));
                    // Each player confirms an empty queue, whoever opens this turn.
                    for (int planner = 0; planner < 2; planner++)
                        Assert.That(first.CurrentPlanner == 0 ? versus.TryCommit()
                            : versus.TryApplyOnlinePeerAction(OnlineVersusAction.Pass, -1, round), Is.True);
                    if (round == first.RoundLimit) break;

                    int frames = 0;
                    while (first.RoundNumber == round && frames++ < 400)
                        versus.Tick(.05f, null);
                    Assert.That(frames, Is.LessThan(400), "An empty turn should hand control to the next player.");
                    Assert.That(first.RoundNumber, Is.EqualTo(round + 1));
                }

                Assert.That(first.IsFinished, Is.True);
                Assert.That(first.Outcome, Is.EqualTo(LocalVersusOutcome.Draw));
                // The last turn's return plays out, then the single-play result card comes.
                int settle = 0;
                while (!versus.IsShowingResult && settle++ < 400) versus.Tick(.05f, null);
                Assert.That(versus.IsShowingResult, Is.True);
                Assert.That(LabelNamed(versus.ResultHud.Root, "Result Heading").text, Is.EqualTo("무승부"));
                Assert.That(LabelNamed(versus.ResultHud.Root, "Result Rounds").text, Is.EqualTo("20"));

                // 다시 겨루기 is a vote: the match stays until the room's host starts the next one.
                Button retry = ButtonNamed(versus.ResultHud.Root, "Result Retry");
                retry.onClick.Invoke();
                Assert.That(rematchVotes, Is.EqualTo(1));
                Assert.That(retry.interactable, Is.False);
                Assert.That(retry.GetComponentInChildren<Text>(true).text, Is.EqualTo(BattleResultHud.VersusWaitingCaption));
                Assert.That(versus.Match, Is.SameAs(first));

                // Both voted: the host starts the rematch with a new opening result supplied by the room.
                versus.StartOnline(Seed + 1, 0, true, 1, LegacyInitialSkills.All, LegacyInitialSkills.All,
                    (action, lane) => { }, () => rematchVotes++);
                Assert.That(versus.IsActive, Is.True);
                Assert.That(versus.Match, Is.Not.SameAs(first));
                Assert.That(versus.Match.RoundNumber, Is.EqualTo(1));
                Assert.That(versus.Match.Outcome, Is.EqualTo(LocalVersusOutcome.InProgress));
                Assert.That(versus.Match.OpeningPlayer, Is.EqualTo(1));
                Assert.That(versus.Match.CurrentPlanner, Is.EqualTo(1));
                Assert.That(versus.Match.Left.Queue, Is.Empty);
                Assert.That(versus.Match.Right.Queue, Is.Empty);
                Assert.That(versus.IsShowingResult, Is.False);
                float retryCard = versus.PresentationSettings.StartCardRetrySeconds;
                Assert.That(versus.IsShowingStartCard, Is.EqualTo(retryCard > 0f));
                if (retryCard > 0f)
                    Assert.That(versus.StartCard.Timeline.Seconds,
                        Is.EqualTo(Mathf.Max(retryCard, DuelStartCard.MinimumCoinTossSeconds)).Within(1e-4f),
                        "A rematch leaves time for its new coin result.");
                Assert.That(File.ReadAllText(scope.Store.Path), Is.EqualTo(saveBefore));
            }
        }

        private static void AssertLineup(LocalVersusFighter fighter, int[] ids, string message)
        {
            for (int lane = 0; lane < VersusLoadout.LaneCount; lane++)
                for (int slot = 0; slot < VersusLoadout.SlotsPerLane; slot++)
                    Assert.That(fighter.GetLane(lane)[slot].Id,
                        Is.EqualTo(ids[lane * VersusLoadout.SlotsPerLane + slot]), message);
        }

        private static Button ButtonNamed(GameObject root, string name)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            Assert.Fail("Missing versus button: " + name);
            return null;
        }

        private static Text LabelNamed(GameObject root, string name)
        {
            foreach (Text label in root.GetComponentsInChildren<Text>(true))
                if (label.name == name) return label;
            Assert.Fail("Missing versus label: " + name);
            return null;
        }

        private static Text TextAt(GameObject root, string path)
        {
            Transform part = root.transform.Find(path);
            Assert.That(part, Is.Not.Null, "Missing duel HUD part: " + path);
            return part.GetComponent<Text>();
        }

        private sealed class SaveScope : IDisposable
        {
            private readonly GameSaveStore originalStore;
            private readonly bool originalEnabled;
            private readonly bool originalRunInBackground;
            private readonly string directory;

            public DuelPrototypeController Controller { get; }
            public GameSaveStore Store { get; }

            public SaveScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                originalRunInBackground = Application.runInBackground;
                Controller.enabled = false;
                originalStore = Controller.SaveStore;
                directory = Path.Combine(Application.temporaryCachePath,
                    "LocalVersusTests-" + Guid.NewGuid().ToString("N"));
                Store = new GameSaveStore(Path.Combine(directory, GameSaveStore.FileName));
                Controller.SaveStore = Store;
            }

            public string CreateSaveAndReadText()
            {
                Assert.That(Store.TrySave(GameSave.Capture(new PrologueRun(), new CampaignRun()),
                    out string error), Is.True, error);
                return File.ReadAllText(Store.Path);
            }

            /// <summary>The title's 온라인 친구 대전, then the match the room's 대전 시작 starts once both are ready, here with no
            /// network: the shared engine started host-only, the invite room put away as the room does.</summary>
            public LocalVersusController StartHostOnly(IReadOnlyList<LegacySkill> left, IReadOnlyList<LegacySkill> right,
                Action rematch = null) => Start(0, true, left, right, rematch);

            /// <summary>The same match on the guest's screen: its own commands only go out as requests.</summary>
            public LocalVersusController StartGuestOnly(IReadOnlyList<LegacySkill> left, IReadOnlyList<LegacySkill> right)
                => Start(1, false, left, right, null);

            private LocalVersusController Start(int localPlayer, bool isHost,
                IReadOnlyList<LegacySkill> left, IReadOnlyList<LegacySkill> right, Action rematch)
            {
                Controller.ShowTitle();
                Assert.That(Controller.TitleHud.OnlineVersusButton.interactable, Is.True);
                Controller.TitleHud.OnlineVersusButton.onClick.Invoke();
                Assert.That(Controller.IsInOnlineVersus, Is.True);
                Assert.That(Controller.OnlineVersus.IsInBattle, Is.False, "No network: the room never starts a battle.");
                Controller.OnlineVersus.Hud.Hide();
                LocalVersusController versus = Controller.LocalVersus;
                versus.StartOnline(Seed, localPlayer, isHost, 0, left, right, (action, lane) => { },
                    rematch ?? (() => { }));
                Assert.That(versus.IsActive, Is.True);
                Assert.That(versus.OnlineLocalPlayer, Is.EqualTo(localPlayer));
                return versus;
            }

            /// <summary>Plays the start card out: it holds the clocks and the input, and nothing can cut it short.</summary>
            public void FinishStartCard()
            {
                LocalVersusController versus = Controller.LocalVersus;
                int frames = 0;
                while (versus.IsShowingStartCard && frames++ < 400) versus.Tick(.05f, null);
                Assert.That(versus.IsShowingStartCard, Is.False);
            }

            public void Dispose()
            {
                Controller.enabled = false;
                Controller.ShowTitle();
                Controller.SaveStore = originalStore;
                Controller.enabled = originalEnabled;
                Application.runInBackground = originalRunInBackground;
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
