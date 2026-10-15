using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>일방 공격 가속 in the campaign's battles (2026-10-15, <see cref="LegacyUnopposedStreak"/>): a run of attacks
    /// into nothing plays faster and faster after each first hit, and the wait after each slot too, up to the cap; a slot where
    /// both act starts the run over, one where neither attacks keeps it, and a Major slot plays as ever while the run counts
    /// it; where a step can be taken the windup and the step window keep their real time, and where none can the slot plays
    /// faster from its start; switched off (on its own or with the director) every slot plays as ever, and a reset leaves
    /// nothing of a run.</summary>
    public sealed class UnopposedStreakPlayModeTests : InputTestFixture
    {
        // Small frames, so a phase's last frame overshoots its end by little.
        private const float Step = 1f / 500f, HitStop = .04f, Frames = Step * 3f;
        private const int MaximumSteps = 40000;
        private const string StreakOff = "\"unopposedStreakEnabled\":false", DirectorOff = "\"showDirectorEnabled\":false";
        private static readonly float[] Run = { 1f, 1.15f, 1.3f, 1.45f, 1.6f, 1.6f, 1.6f };

        [UnityTest]
        public IEnumerator AnUnopposedRun_PlaysFasterAndFaster_UpToTheCap_AfterEachFirstHit()
        {
            yield return null;
            SlotRun[] on = PlayerRun(null, CombatFeature.All, out float onTurn);
            SlotRun[] off = PlayerRun(StreakOff, CombatFeature.All, out float offTurn);
            SlotRun[] directorOff = PlayerRun(DirectorOff, CombatFeature.All, out float directorOffTurn);
            for (int slot = 0; slot < Run.Length; slot++)
            {
                string at = "slot " + slot;
                Assert.That(on[slot].SpeedBeforeHit, Is.EqualTo(1f), at + ": a step could be taken, so its windup is as ever…");
                Assert.That(on[slot].ToFirstHit, Is.EqualTo(off[slot].ToFirstHit).Within(Step * 1.5f), at + ": …and so is its hit.");
                Assert.That(on[slot].SpeedAfterHit, Is.EqualTo(Run[slot]).Within(1e-4f), at + ": then it plays faster…");
                Assert.That(off[slot].SpeedAfterHit, Is.EqualTo(1f));
                Assert.That(directorOff[slot].SpeedAfterHit, Is.EqualTo(1f), at + ": the streak plays only with the director.");
                Assert.That(on[slot].Rest, Is.EqualTo(HitStop + (off[slot].Rest - HitStop) / Run[slot]).Within(Frames),
                    at + ": …its rest after the hit's stop shorter by its speed…");
                if (slot < Run.Length - 1)
                {
                    Assert.That(on[slot].GapSpeed, Is.EqualTo(Run[slot]).Within(1e-4f));
                    Assert.That(on[slot].Gap, Is.EqualTo(off[slot].Gap / Run[slot]).Within(Frames), at + ": …and the wait after it.");
                }
            }
            Assert.That(onTurn, Is.LessThan(offTurn - .2f), "The run's turn is shorter…");
            Assert.That(directorOffTurn, Is.EqualTo(offTurn).Within(Frames), "…and switched off it plays as ever.");
        }

        [UnityTest]
        public IEnumerator WithTheStepsClosed_ARunPlaysFaster_FromEachSlotsStart()
        {
            yield return null;
            CombatFeature closed = CombatFeature.All & ~CombatFeature.Dodge & ~CombatFeature.Pressure;
            SlotRun[] on = PlayerRun(null, closed, out _);
            SlotRun[] off = PlayerRun(StreakOff, closed, out _);
            for (int slot = 0; slot < Run.Length; slot++)
            {
                string at = "slot " + slot;
                Assert.That(on[slot].WindowOpens, Is.LessThan(0f), at + ": no step window at all.");
                Assert.That(on[slot].SpeedBeforeHit, Is.EqualTo(Run[slot]).Within(1e-4f), at + ": nothing could change it before the hit.");
                Assert.That(on[slot].ToFirstHit, Is.EqualTo(off[slot].ToFirstHit / Run[slot]).Within(Frames));
            }
        }

        [UnityTest]
        public IEnumerator TheEnemysRun_KeepsEveryStepWindow_AndPlaysFasterOnlyAfterItsFirstHit()
        {
            yield return null;
            SlotRun[] on = EnemyRun(null);
            SlotRun[] off = EnemyRun(StreakOff);
            float[] expected = { 1f, 1.15f, 1.3f, 1.45f };
            for (int slot = 0; slot < expected.Length; slot++)
            {
                string at = "slot " + slot;
                Assert.That(on[slot].SpeedBeforeHit, Is.EqualTo(1f), at);
                Assert.That(on[slot].WindowOpens, Is.GreaterThan(0f), at + ": the dodge's window opened…");
                Assert.That(on[slot].WindowOpens, Is.EqualTo(off[slot].WindowOpens).Within(Step * 1.5f), at + ": …as ever…");
                Assert.That(on[slot].WindowCloses, Is.EqualTo(off[slot].WindowCloses).Within(Step * 1.5f), at + ": …closed as ever…");
                Assert.That(on[slot].ToFirstHit, Is.EqualTo(off[slot].ToFirstHit).Within(Step * 1.5f), at + ": …and the hit landed as ever.");
                Assert.That(on[slot].SpeedAfterHit, Is.EqualTo(expected[slot]).Within(1e-4f), at + ": the enemy's run counts the same.");
            }
        }

        [UnityTest]
        public IEnumerator AMutualSlot_StartsTheRunOver_ANeutralOneKeepsIt_AndAMajorSlotPlaysAsEver()
        {
            yield return null;
            // Player: 10, 10, 숨고르기, 10, 10 (against a guard), 10, 300 (a big blow), 10. Enemy: breaths but the guard.
            var player = new[] { Attack(990, 10, 0), Attack(991, 300, 1) };
            var breath = LegacyCommonActions.Breathe;
            var enemy = new[] { breath, breath, breath, breath, Guard(995, 5, 0), breath, breath, breath };
            using (var scope = new StreakScope(new LegacyQueuedDuel(1000, 1000, 1000, 1000, player, enemy, new[] { 8 }, 1,
                meshPercent: 0)))
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.QueueLane(0) && controller.QueueLane(0) && controller.QueueBreath(), Is.True);
                Assert.That(controller.QueueLane(0) && controller.QueueLane(0) && controller.QueueLane(0), Is.True);
                Assert.That(controller.QueueLane(1) && controller.QueueLane(0), Is.True);
                SlotRun[] runs = scope.PlayTurn(8);
                float[] expected = { 1f, 1.15f, 1f, 1.3f, 1f, 1f, 1f, 1.3f };
                int[] streaks = { 1, 2, 2, 3, 0, 1, 2, 3 };
                for (int slot = 0; slot < expected.Length; slot++)
                {
                    Assert.That(runs[slot].SpeedAfterHit, Is.EqualTo(expected[slot]).Within(1e-4f), "slot " + slot);
                    Assert.That(runs[slot].Streak, Is.EqualTo(streaks[slot]), "slot " + slot);
                    if (slot < expected.Length - 1)
                        Assert.That(runs[slot].GapSpeed, Is.EqualTo(expected[slot]).Within(1e-4f), "slot " + slot + "'s wait");
                }
                Assert.That(runs[6].Tier, Is.EqualTo(LegacySlotTier.Major), "300 of 1000 into a breath: a big blow.");
                Assert.That(runs[2].Tier, Is.EqualTo(LegacySlotTier.Minor), "A breath against a breath.");
            }
        }

        [UnityTest]
        public IEnumerator AReset_InTheMiddleOfARun_LeavesNothingOfIt()
        {
            yield return null;
            using (var scope = new StreakScope(new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { Attack(990, 10, 0) },
                new LegacySkill[0], new[] { 0 }, 1, meshPercent: 0)))
            {
                DuelPrototypeController controller = scope.Controller;
                for (int index = 0; index < 4; index++) Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                LegacyQueuedDuel session = controller.Session;
                scope.Until(() => session.CurrentSlot != null && session.CurrentSlot.SlotIndex == 2 &&
                    session.CurrentSlot.HitsResolved > 0);
                Assert.That(controller.StreakPlaybackSpeed, Is.EqualTo(1.3f).Within(1e-4f), "The third in a row.");
                controller.RestartMatch();
                Assert.That(controller.ShowDirector.StreakLength, Is.Zero);
                Assert.That((controller.ShowDirector.StreakSpeed, controller.ShowDirector.GapSpeed), Is.EqualTo((1f, 1f)));
                Assert.That(controller.StreakPlaybackSpeed, Is.EqualTo(1f));
                Assert.That(controller.CanChoose, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator TheSettings_KeepTheirDefaults_AndRanges()
        {
            yield return null;
            DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            try
            {
                LegacyStreakTuning tuning = settings.UnopposedStreak;
                Assert.That(tuning.Enabled, Is.True);
                Assert.That(tuning.StartLength, Is.EqualTo(2));
                Assert.That(tuning.StepPerSlot, Is.EqualTo(.15f).Within(1e-5f));
                Assert.That(tuning.MaximumSpeed, Is.EqualTo(1.6f).Within(1e-5f));
                JsonUtility.FromJsonOverwrite("{\"unopposedStreakEnabled\":false,\"unopposedStreakStartLength\":40," +
                    "\"unopposedStreakStep\":-2,\"unopposedStreakMaxSpeed\":9}", settings);
                tuning = settings.UnopposedStreak;
                Assert.That(tuning.Enabled, Is.False);
                Assert.That(tuning.StartLength, Is.EqualTo(LegacyStreakTuning.MaximumStartLength));
                Assert.That(tuning.StepPerSlot, Is.Zero);
                Assert.That(tuning.MaximumSpeed, Is.EqualTo(LegacyStreakTuning.MaximumSpeedLimit));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        private struct SlotRun
        {
            public float ToFirstHit, Rest, Gap, SpeedBeforeHit, SpeedAfterHit, GapSpeed, WindowOpens, WindowCloses;
            public int Streak;
            public LegacySlotTier Tier;
        }

        // Seven attacks of 10 into an empty enemy queue (Normal: 1% of the enemy's health each), with these steps open.
        private static SlotRun[] PlayerRun(string tuning, CombatFeature features, out float turn)
        {
            using (var scope = new StreakScope(new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { Attack(990, 10, 0) },
                new LegacySkill[0], new[] { 0 }, 1, features: features, meshPercent: 0), tuning))
            {
                for (int index = 0; index < Run.Length; index++) Assert.That(scope.Controller.QueueLane(0), Is.True);
                float started = scope.Elapsed;
                SlotRun[] runs = scope.PlayTurn(Run.Length);
                turn = scope.Elapsed - started;
                return runs;
            }
        }

        // Three breaths and an empty slot against four enemy attacks of 10: the enemy's run, a dodge open in every slot.
        private static SlotRun[] EnemyRun(string tuning)
        {
            var enemy = new[] { Attack(993, 10, 0), Attack(994, 10, 0), Attack(996, 10, 0), Attack(997, 10, 0) };
            using (var scope = new StreakScope(new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { Attack(990, 10, 0) },
                enemy, new[] { 4 }, 1, meshPercent: 0), tuning))
            {
                for (int index = 0; index < 3; index++) Assert.That(scope.Controller.QueueBreath(), Is.True);
                return scope.PlayTurn(4);
            }
        }

        private static LegacySkill Attack(int id, int power, int lane)
            => new LegacySkill(id, "가속 공격", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, lane, "검증");

        private static LegacySkill Guard(int id, int power, int lane)
            => new LegacySkill(id, "가속 방어", 0, power, power, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, lane, "검증");

        /// <summary>The real controller on a given duel, driven with chosen real seconds, on a copy of the project's
        /// presentation settings with fixed tempo, the director's and the streak's defaults, and <c>tuning</c> (JSON fields)
        /// over them.</summary>
        private sealed class StreakScope : IDisposable
        {
            private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            private const string Tempo = "\"animationPlaybackSpeed\":1,\"attackInterval\":0.1,\"skillInterval\":0.28," +
                "\"hitStopDuration\":0.04,\"stepAnticipationDuration\":0.24,\"stepTimingWindow\":0.1," +
                "\"decisiveHealthDamagePercent\":25,\"showDirectorEnabled\":true,\"minorSlotSpeed\":2,\"majorHitStopScale\":3," +
                "\"majorCameraPush\":0.1,\"majorClashScale\":1.35,\"turnExtraSecondsCap\":0.8,\"largeClashGap\":0.6," +
                "\"deepPierce\":0.7,\"bigUnopposedPercent\":25,\"bladeBindGap\":0.15,\"bladeBindSeconds\":0.45," +
                "\"unopposedStreakEnabled\":true,\"unopposedStreakStartLength\":2,\"unopposedStreakStep\":0.15," +
                "\"unopposedStreakMaxSpeed\":1.6,\"clashMotionEnabled\":true";
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originalEnabled;
            private readonly FieldInfo arenaSettings;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }
            public DuelPresentationSettings Settings { get; }
            /// <summary>Real seconds advanced so far.</summary>
            public float Elapsed { get; private set; }

            public StreakScope(LegacyQueuedDuel duel, string tuning = null)
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalSession = Controller.Session;
                arenaSettings = typeof(LegacyArenaView).GetField("settings", PrivateInstance);
                originalArenaSettings = (DuelPresentationSettings)arenaSettings.GetValue(Controller.ArenaView);
                Settings = Object.Instantiate(originalSettings);
                try
                {
                    JsonUtility.FromJsonOverwrite("{" + Tempo + (string.IsNullOrEmpty(tuning) ? "" : "," + tuning) + "}", Settings);
                    Set("presentationSettings", Settings);
                    arenaSettings.SetValue(Controller.ArenaView, Settings);
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
                    // A failed setup never reaches Dispose: restore the shared controller here.
                    try { Dispose(); } catch { Controller.enabled = originalEnabled; }
                    throw;
                }
            }

            public void Advance(float realDelta)
            {
                advance(realDelta, null);
                Elapsed += realDelta;
            }

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

            /// <summary>Commits the queued turn and plays its <paramref name="slots"/> slots to the next planning turn: from
            /// each slot's start to its first hit (the clock's speed meanwhile, the step window), the speed after the hit, the
            /// rest to its end, and the wait after it at its speed.</summary>
            public SlotRun[] PlayTurn(int slots)
            {
                LegacyQueuedDuel session = Controller.Session;
                var runs = new SlotRun[slots];
                Controller.CommitTurn();
                for (int index = 0; index < slots; index++)
                {
                    int slotIndex = index;
                    Until(() => session.CurrentSlot != null && session.CurrentSlot.SlotIndex == slotIndex);
                    LegacyCurrentSlot slot = session.CurrentSlot;
                    var run = new SlotRun { WindowOpens = -1f, WindowCloses = -1f, SpeedBeforeHit = Controller.StreakPlaybackSpeed };
                    float elapsed = 0f;
                    for (int step = 0; step < MaximumSteps && slot.HitsResolved == 0; step++)
                    {
                        Advance(Step);
                        elapsed += Step;
                        if (slot.HitsResolved > 0) break;
                        run.SpeedBeforeHit = Mathf.Max(run.SpeedBeforeHit, Controller.StreakPlaybackSpeed);
                        bool window = Controller.IsStepTimingWindow;
                        if (window && run.WindowOpens < 0f) run.WindowOpens = elapsed;
                        if (!window && run.WindowOpens >= 0f && run.WindowCloses < 0f) run.WindowCloses = elapsed;
                    }
                    Assert.That(slot.HitsResolved, Is.GreaterThan(0), "Slot " + index + " struck.");
                    run.ToFirstHit = elapsed;
                    if (run.WindowOpens >= 0f && run.WindowCloses < 0f) run.WindowCloses = elapsed;
                    run.SpeedAfterHit = Controller.StreakPlaybackSpeed;
                    run.Streak = Controller.ShowDirector.StreakLength;
                    run.Tier = Controller.ShowDirector.SlotRank.Tier;
                    run.Rest = Until(() => session.LastResolvedSlot >= slotIndex);
                    run.GapSpeed = 1f;
                    if (Controller.IsBetweenSlots)
                    {
                        run.GapSpeed = Controller.StreakPlaybackSpeed;
                        run.Gap = Until(() => !Controller.IsBetweenSlots);
                    }
                    runs[index] = run;
                }
                Until(() => Controller.CanChoose);
                Assert.That(session.RoundNumber, Is.EqualTo(2), "The turn ends: no stall.");
                return runs;
            }

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
                Set("session", originalSession);
                Controller.RestartMatch();
                Controller.enabled = originalEnabled;
                Object.Destroy(Settings);
            }
        }
    }
}
