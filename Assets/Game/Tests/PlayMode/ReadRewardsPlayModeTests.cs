using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The read rewards in the campaign's battles (2026-10-15): the player's 간파 bursts gold on the player's seam
    /// card, stamps a small '간파' over it and lights the player's fighter, once per slot, never for the enemy's read, and
    /// switches off; a signature move gathers its school's dye in its windup, trails its strike, comes closer and stops
    /// longer, while its step window keeps its time; and the decisive-moment replay (결정적 한 수) keeps a battle's Major
    /// slot (not the finishing blow), plays it over the result, ends at a key with the result still there, releases every
    /// picture, and leaves the result as ever when switched off, unsupported or reset.</summary>
    public sealed class ReadRewardsPlayModeTests : InputTestFixture
    {
        private const float Step = 1f / 500f, HitStop = .04f;
        private const int MaximumSteps = 40000;

        [UnityTest]
        public IEnumerator APlayersRead_BurstsGoldOnItsSeamCard_StampsIt_AndLightsTheFighter_OncePerSlot()
        {
            yield return null;
            // 막기 against 기세 꺾기 (타격): the guard's 상대 속성 조건 is met.
            using (var scope = new RewardsScope(Duel(LegacySkillDefinitions.Skill(7), LegacySkillDefinitions.Skill(5))))
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.QueueLane(LegacySkillDefinitions.Skill(7).LaneIndex), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot != null);
                Assert.That(controller.ShowDirector.SlotRank.PlayerReads, Is.EqualTo(LegacyReadEvent.OpponentCondition));
                Assert.That(controller.Hud.IsReadCuePlaying, Is.False, "Not before the payoff…");
                scope.Until(() => controller.Session.CurrentSlot.HitsResolved > 0);
                Assert.That(controller.Hud.IsReadCuePlaying, Is.True, "…but at the first hit.");
                Assert.That(controller.Hud.ReadCueSlot, Is.EqualTo(0));
                Assert.That(controller.Hud.ReadSpark, Is.Not.Null, "The gold burst on the seam card.");
                Assert.That(controller.Hud.IsReadStampShown, Is.True);
                Text stamp = controller.Hud.ReadStamp.GetComponentInChildren<Text>();
                Assert.That(stamp.text, Is.EqualTo(LegacyCombatHud.ReadStampText));
                Assert.That(controller.ArenaView.PlayerPowerAura.ChargeGlow, Is.GreaterThan(0f), "The fighter's light accent.");
                Assert.That(controller.ArenaView.PlayerPowerAura.ChargeInk, Is.EqualTo(scope.Settings.ReadCueInk));
                Assert.That(controller.ShowDirector.ReadCueGiven, Is.True);
                Assert.That(controller.HitStopTimeRemaining, Is.EqualTo(HitStop * 3f).Within(1e-4f),
                    "A read is Major: its stop is the director's, the cue takes no time.");
                scope.Advance(scope.Settings.ReadCueSeconds + .6f);
                Assert.That(controller.Hud.IsReadCuePlaying || controller.Hud.IsReadStampShown, Is.False, "Then it is gone.");
                scope.Until(() => controller.CanChoose);
            }
        }

        [UnityTest]
        public IEnumerator TheEnemysRead_IsNoCue_AndTheCueAndItsStampSwitchOff()
        {
            yield return null;
            // The enemy's 기세 꺾기 meets the player's attack: the enemy read it.
            using (var scope = new RewardsScope(Duel(Attack(980, 5, 0), LegacySkillDefinitions.Skill(5))))
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot != null && controller.Session.CurrentSlot.HitsResolved > 0);
                Assert.That(controller.ShowDirector.SlotRank.EnemyReads, Is.Not.EqualTo(LegacyReadEvent.None));
                Assert.That(controller.Hud.IsReadCuePlaying, Is.False);
                scope.Until(() => controller.CanChoose);
            }
            using (var scope = new RewardsScope(Duel(LegacySkillDefinitions.Skill(7), LegacySkillDefinitions.Skill(5)),
                "\"readStampEnabled\":false"))
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.QueueLane(LegacySkillDefinitions.Skill(7).LaneIndex), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot != null && controller.Session.CurrentSlot.HitsResolved > 0);
                Assert.That(controller.Hud.IsReadCuePlaying, Is.True, "The burst and the light…");
                Assert.That(controller.Hud.IsReadStampShown, Is.False, "…without the stamp.");
                scope.Until(() => controller.CanChoose);
            }
            using (var scope = new RewardsScope(Duel(LegacySkillDefinitions.Skill(7), LegacySkillDefinitions.Skill(5)),
                "\"readCueEnabled\":false"))
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.QueueLane(LegacySkillDefinitions.Skill(7).LaneIndex), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot != null && controller.Session.CurrentSlot.HitsResolved > 0);
                Assert.That(controller.Hud.IsReadCuePlaying, Is.False, "Switched off: nothing on screen…");
                Assert.That(controller.ArenaView.PlayerPowerAura.ChargeGlow, Is.Zero);
                Assert.That(controller.ShowDirector.SlotRank.Tier, Is.EqualTo(LegacySlotTier.Major), "…the slot is still Major.");
                scope.Until(() => controller.CanChoose);
            }
        }

        [UnityTest]
        public IEnumerator ASignatureMove_GathersItsDye_TrailsItsStrike_ComesCloser_AndStopsLonger_ItsWindowAsEver()
        {
            yield return null;
            SignatureTimes off = SignatureSlot("\"signatureTreatmentEnabled\":false", out _, out _);
            SignatureTimes on = SignatureSlot(null, out RewardsScope.Snapshot during, out float stop);
            Assert.That(on.WindowOpens, Is.EqualTo(off.WindowOpens).Within(Step * 1.5f), "The step window opens as ever…");
            Assert.That(on.WindowCloses, Is.EqualTo(off.WindowCloses).Within(Step * 1.5f), "…closes as ever…");
            Assert.That(on.FirstHit, Is.EqualTo(off.FirstHit).Within(Step * 1.5f), "…and the strike lands as ever.");
            Assert.That(during.Charging, Is.True, "Its power gathers over the windup…");
            Assert.That(during.ChargeInk, Is.EqualTo(SkillLaneStyle.Edge(LegacySkillDefinitions.Skill(51).LaneIndex)),
                "…in its school's dye.");
            Assert.That(during.TrailArmed, Is.True);
            Assert.That(on.MostGhosts, Is.GreaterThan(0), "Its strike leaves a trail.");
            Assert.That(off.MostGhosts, Is.Zero);
            Assert.That(on.MostPush, Is.GreaterThan(off.MostPush), "The camera comes closer than a Major slot's.");
            Assert.That(stop, Is.EqualTo(HitStop * LegacyShowPacingTuning.DefaultSignatureHitStopScale).Within(1e-4f),
                "Its first hit stops longer.");
            Assert.That(off.Stop, Is.EqualTo(HitStop * 3f).Within(1e-4f), "Switched off: a Major slot's stop.");
        }

        [UnityTest]
        public IEnumerator TheDecisiveMoment_IsKept_PlaysOverTheResult_EndsAtAKey_AndReleasesEveryPicture()
        {
            yield return null;
            using (var scope = new RewardsScope(ReplayDuel(), stage: true))
            {
                DuelPrototypeController controller = scope.Controller;
                DuelDecisiveReplay replay = controller.DecisiveReplay;
                Assert.That(controller.QueueLane(0) && controller.QueueLane(1), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.IsShowingResult);
                Assert.That(controller.ResultHud.IsVisible, Is.True, "The result comes as ever.");
                Assert.That(controller.Result.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
                if (replay.CaptureFailed)
                {
                    // No graphics here: nothing was recorded, nothing plays, and the result works.
                    Assert.That(replay.IsPlaying || replay.HasClip, Is.False);
                    Assert.That(replay.AllocatedPictureCount, Is.Zero);
                    Assert.That(controller.DismissBattleResult(), Is.True);
                    yield break;
                }
                Assert.That(replay.IsPlaying, Is.True, "결정적 한 수 plays over the card.");
                Assert.That(replay.Recorder.ClipRank.SlotIndex, Is.EqualTo(0), "The break, not the finishing blow.");
                Assert.That(replay.Recorder.ClipRank.Tier, Is.EqualTo(LegacySlotTier.Major));
                Assert.That(replay.Recorder.ClipPlayerSkill.Id, Is.EqualTo(980));
                Assert.That(replay.Recorder.ClipEnemySkill.Id, Is.EqualTo(982));
                Assert.That(replay.AllocatedPictureCount, Is.EqualTo(replay.Recorder.ClipFrameCount),
                    "Only the kept clip's pictures are left.");
                Assert.That(FindActive<Text>(replay.Root, DuelDecisiveReplay.TitleName).text, Is.EqualTo(DuelDecisiveReplay.Title));
                int first = replay.ShownIndex;
                scope.Advance(.3f);
                Assert.That(replay.ShownIndex, Is.GreaterThan(first), "It plays.");

                // The controller's own frames read the key, as in play.
                var keyboard = InputSystem.AddDevice<Keyboard>();
                controller.enabled = true;
                Press(keyboard.enterKey);
                yield return null;
                yield return null;
                Assert.That(replay.IsPlaying, Is.False, "Enter ends it…");
                Assert.That(controller.IsShowingResult && controller.ResultHud.IsVisible, Is.True, "…and the card is there.");
                Assert.That(replay.AllocatedPictureCount, Is.Zero, "Every picture released.");
                yield return null;
                Assert.That(controller.IsShowingResult, Is.True, "The held key never reaches the card.");
                Release(keyboard.enterKey);
                yield return null;
                controller.enabled = false;
                Assert.That(controller.DismissBattleResult(), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator SwitchedOff_OrReset_TheResultStandsAsEver_AndNothingIsLeft()
        {
            yield return null;
            using (var scope = new RewardsScope(ReplayDuel(), "\"replayEnabled\":false", stage: true))
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.QueueLane(0) && controller.QueueLane(1), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.IsShowingResult);
                Assert.That(controller.DecisiveReplay.IsPlaying || controller.DecisiveReplay.HasClip, Is.False);
                Assert.That(controller.DecisiveReplay.AllocatedPictureCount, Is.Zero);
                Assert.That(controller.ResultHud.IsVisible, Is.True);
                Assert.That(controller.DismissBattleResult(), Is.True);
            }
            using (var scope = new RewardsScope(ReplayDuel(), stage: true))
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.QueueLane(0) && controller.QueueLane(1), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot != null && controller.Session.CurrentSlot.HitsResolved > 0);
                controller.RestartMatch();
                Assert.That(controller.DecisiveReplay.IsRecording || controller.DecisiveReplay.HasClip, Is.False);
                Assert.That(controller.DecisiveReplay.Recorder, Is.Null);
                Assert.That(controller.DecisiveReplay.AllocatedPictureCount, Is.Zero, "A reset releases every picture.");
                Assert.That(controller.Hud.IsReadCuePlaying, Is.False);
                Assert.That(controller.ArenaView.ActiveSignatureAfterimageCount, Is.Zero);
            }
        }

        private struct SignatureTimes
        {
            public float WindowOpens, WindowCloses, FirstHit, MostPush, Stop;
            public int MostGhosts;
        }

        // 에스토카다 (51, a capstone) against a plain attack: when the step window opens and closes and the strike lands after
        // the slot's start, the closest the camera came, the most trail ghosts, and the stop its first hit got.
        private static SignatureTimes SignatureSlot(string tuning, out RewardsScope.Snapshot windup, out float stop)
        {
            LegacySkill estocada = LegacySkillDefinitions.Skill(51);
            var times = new SignatureTimes { WindowOpens = -1f, WindowCloses = -1f, FirstHit = -1f };
            using (var scope = new RewardsScope(Duel(estocada, Attack(982, 14, 0)), tuning))
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.QueueLane(estocada.LaneIndex), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot != null);
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                Assert.That(controller.ShowDirector.IsSignature && controller.ShowDirector.IsMajor, Is.True);
                Assert.That(controller.IsSkillWindup, Is.True);
                scope.Advance(Step);
                windup = scope.Take();
                float elapsed = Step;
                for (int step = 0; step < MaximumSteps && slot.HitsResolved == 0; step++)
                {
                    scope.Advance(Step);
                    elapsed += Step;
                    if (slot.HitsResolved > 0) break;
                    times.MostPush = Mathf.Max(times.MostPush, controller.ArenaView.DirectorPushAmount);
                    bool window = controller.IsStepTimingWindow;
                    if (window && times.WindowOpens < 0f) times.WindowOpens = elapsed;
                    if (!window && times.WindowOpens >= 0f && times.WindowCloses < 0f) times.WindowCloses = elapsed;
                }
                Assert.That(slot.HitsResolved, Is.EqualTo(1));
                times.FirstHit = elapsed;
                if (times.WindowCloses < 0f) times.WindowCloses = elapsed;
                stop = times.Stop = controller.HitStopTimeRemaining;
                for (int step = 0; step < MaximumSteps && controller.Session.CurrentSlot == slot; step++)
                {
                    scope.Advance(Step);
                    times.MostGhosts = Math.Max(times.MostGhosts, controller.ArenaView.ActiveSignatureAfterimageCount);
                }
                scope.Until(() => controller.CanChoose);
            }
            return times;
        }

        // The player's skill against the enemy's, one slot, both at full health.
        private static LegacyQueuedDuel Duel(LegacySkill player, LegacySkill enemy)
            => new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { player }, new[] { enemy }, new[] { 1 }, 1, meshPercent: 0);

        // A break that leaves the enemy standing (30 against 4 with 5 resistance: 25 over, broken), then the finishing blow
        // against nothing (80 at 75 health).
        private static LegacyQueuedDuel ReplayDuel()
            => new LegacyQueuedDuel(100, 50, 100, 5, new[] { Attack(980, 30, 0), Attack(981, 80, 1) },
                new[] { Attack(982, 4, 0) }, new[] { 1 }, 1, meshPercent: 0);

        private static LegacySkill Attack(int id, int power, int lane)
            => new LegacySkill(id, "보상 공격", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, lane, "검증");

        private static T FindActive<T>(GameObject root, string name) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(false))
                if (component.name == name) return component;
            Assert.Fail(name + " is not on screen.");
            return null;
        }

        /// <summary>The real controller on a given duel, driven with chosen real seconds, on a copy of the project's
        /// presentation settings with fixed tempo and the director's and the read rewards' defaults, and <c>tuning</c> (JSON
        /// fields) over them. <c>stage</c> starts stage 1 first, so the battle ends in a result.</summary>
        private sealed class RewardsScope : IDisposable
        {
            private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            private const string Tempo = "\"animationPlaybackSpeed\":1,\"attackInterval\":0.1,\"skillInterval\":0.28," +
                "\"hitStopDuration\":0.04,\"stepAnticipationDuration\":0.24,\"stepTimingWindow\":0.1," +
                "\"decisiveHealthDamagePercent\":25,\"showDirectorEnabled\":true,\"minorSlotSpeed\":2,\"majorHitStopScale\":3," +
                "\"majorCameraPush\":0.1,\"majorClashScale\":1.35,\"turnExtraSecondsCap\":0.8,\"largeClashGap\":0.6," +
                "\"deepPierce\":0.7,\"bladeBindGap\":0.15,\"bladeBindSeconds\":0.45,\"clashMotionEnabled\":true," +
                "\"readCueEnabled\":true,\"readStampEnabled\":true,\"readCueSeconds\":0.9,\"readFighterAccent\":true," +
                "\"signatureSkillIds\":[51,53,56],\"signatureTreatmentEnabled\":true,\"signatureHitStopScale\":4.5," +
                "\"signatureCameraPush\":0.16,\"signatureWindupGlow\":true,\"signatureAfterimageSeconds\":0.3," +
                "\"signatureAfterimageAlpha\":0.55,\"replayEnabled\":true,\"replayFramesPerSecond\":15," +
                "\"replayMaxSeconds\":2.5,\"replayTailSeconds\":0.4,\"replayPlaybackSpeed\":0.6,\"replayHoldSeconds\":0.6," +
                "\"replayWidth\":320,\"replayIncludesFinishingBlow\":false,\"finishingSlowMotionSeconds\":0.2," +
                "\"finishingFreezeSeconds\":0.1";
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings;
            private readonly bool originalEnabled, stage;
            private readonly FieldInfo arenaSettings;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }
            public DuelPresentationSettings Settings { get; }

            public struct Snapshot
            {
                public bool Charging, TrailArmed;
                public Color ChargeInk;
            }

            public RewardsScope(LegacyQueuedDuel duel, string tuning = null, bool stage = false)
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                this.stage = stage;
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                arenaSettings = typeof(LegacyArenaView).GetField("settings", PrivateInstance);
                originalArenaSettings = (DuelPresentationSettings)arenaSettings.GetValue(Controller.ArenaView);
                Settings = Object.Instantiate(originalSettings);
                try
                {
                    JsonUtility.FromJsonOverwrite("{" + Tempo + "}", Settings);
                    if (!string.IsNullOrEmpty(tuning)) JsonUtility.FromJsonOverwrite("{" + tuning + "}", Settings);
                    Set("presentationSettings", Settings);
                    arenaSettings.SetValue(Controller.ArenaView, Settings);
                    // A stage battle, so the end of the duel is a result (the campaign completes it).
                    if (stage) Controller.RestartMatch();
                    Set("mission", null);
                    Set("guide", null);
                    Set("session", duel);
                    advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                        typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
                    typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance)
                        .Invoke(Controller, new object[] { null, null });
                    Advance(0f);
                    Assert.That(Controller.CanChoose, Is.True);
                }
                catch
                {
                    try { Dispose(); } catch { Controller.enabled = originalEnabled; }
                    throw;
                }
            }

            public void Advance(float realDelta, Keyboard keyboard = null) => advance(realDelta, keyboard);

            /// <summary>Small frames until <paramref name="condition"/> holds; returns the real seconds it took.</summary>
            public float Until(Func<bool> condition)
            {
                float elapsed = 0f;
                for (int step = 0; step < MaximumSteps && !condition(); step++)
                {
                    Advance(Step);
                    elapsed += Step;
                }
                Assert.That(condition(), Is.True, "The presentation must make progress (no stall).");
                return elapsed;
            }

            public Snapshot Take() => new Snapshot
            {
                Charging = Controller.ArenaView.PlayerPowerAura.IsCharging,
                ChargeInk = Controller.ArenaView.PlayerPowerAura.ChargeInk,
                TrailArmed = Controller.ArenaView.IsSignatureTrailArmed(true),
            };

            private void Set(string name, object value)
            {
                FieldInfo field = typeof(DuelPrototypeController).GetField(name, PrivateInstance);
                Assert.That(field, Is.Not.Null, name);
                field.SetValue(Controller, value);
            }

            public void Dispose()
            {
                Set("presentationSettings", originalSettings);
                arenaSettings.SetValue(Controller.ArenaView, originalArenaSettings);
                if (stage) Controller.RestartJourney();
                else Controller.RestartMatch();
                Controller.enabled = originalEnabled;
                Object.Destroy(Settings);
            }
        }
    }
}
