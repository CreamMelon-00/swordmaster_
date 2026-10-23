using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>Turn progression and an actually exhausted queue accelerate presentation without changing the duel.
    /// Step timing and a pending counter keep their first-hit opportunity; both versus perspectives use the same clock.</summary>
    public sealed class ProgressiveCombatTempoPlayModeTests : InputTestFixture
    {
        private const float Frame = .002f, Tolerance = Frame * 4f;
        private const int MaximumFrames = 40000;
        private const string TempoOff = "\"combatTempoEnabled\":false,\"unopposedStreakEnabled\":false";
        private static readonly CombatFeature NoSteps = CombatFeature.All & ~CombatFeature.Dodge & ~CombatFeature.Pressure;

        [UnityTest]
        public IEnumerator PairedSkills_AccelerateToTheCap_ResetNextTurn_AndResolveTheSameDuelFaster()
        {
            yield return null;
            SlotTiming[] enabled, disabled;
            float enabledTime, disabledTime;
            (int, int, int, int) enabledState, disabledState;
            using (var scope = new CampaignScope(Duel(10, Attack(88002, 9), NoSteps)))
            {
                scope.Queue(10);
                float started = scope.Elapsed;
                enabled = scope.PlayTurn(10);
                enabledTime = scope.Elapsed - started;
                enabledState = scope.State;

                scope.Queue(1);
                scope.Controller.CommitTurn();
                scope.Until(() => scope.Controller.Session.CurrentSlot != null);
                Assert.That(scope.Controller.ResolutionPlaybackSpeed, Is.EqualTo(1f).Within(1e-5f),
                    "The next turn starts from the opening tempo, even after the previous turn reached its cap.");
            }
            using (var scope = new CampaignScope(Duel(10, Attack(88002, 9), NoSteps), TempoOff))
            {
                scope.Queue(10);
                float started = scope.Elapsed;
                disabled = scope.PlayTurn(10);
                disabledTime = scope.Elapsed - started;
                disabledState = scope.State;
            }
            for (int index = 0; index < enabled.Length; index++)
            {
                float expected = Mathf.Min(1f + index * .1f, 1.8f);
                AssertSpeed(enabled[index], expected, "paired slot " + index);
                AssertSpeed(disabled[index], 1f, "disabled slot " + index);
                Assert.That(enabled[index].ToFirstHit,
                    Is.EqualTo(disabled[index].ToFirstHit / expected).Within(Tolerance), "The accelerated clock reaches the impact sooner.");
            }
            Assert.That(enabledTime, Is.LessThan(disabledTime - .5f));
            Assert.That(enabledState, Is.EqualTo(disabledState), "Only presentation changes: health and resistance agree.");
        }

        [UnityTest]
        public IEnumerator AQueuedBreath_IsStillAnAction_AndOnlyTheActuallyEmptyTailGetsDoubled()
        {
            yield return null;
            using (var scope = new CampaignScope(Duel(3, LegacyCommonActions.Breathe, NoSteps)))
            {
                scope.Queue(5);
                SlotTiming[] slots = scope.PlayTurn(5);
                float[] expected = { 1f, 1.1f, 1.2f, 2.6f, 2.8f };
                for (int index = 0; index < slots.Length; index++)
                    AssertSpeed(slots[index], expected[index], "breaths then empty, slot " + index);
                Assert.That(slots[2].LegacyStreak, Is.GreaterThan(1),
                    "The old unopposed streak exists, but must not multiply the new progressive tempo.");
            }
            using (var scope = new CampaignScope(Duel(0, null, NoSteps), "\"showDirectorEnabled\":false"))
            {
                scope.Queue(3);
                foreach (SlotTiming slot in scope.PlayTurn(3)) AssertSpeed(slot, 1f, "director disabled");
            }
        }

        [UnityTest]
        public IEnumerator EitherFighterCanFinishAnEmptyQueue_StartingAtDoubleSpeedOnItsFirstAttack()
        {
            yield return null;
            using (var scope = new CampaignScope(Duel(0, null, NoSteps)))
            {
                scope.Queue(3);
                SlotTiming[] player = scope.PlayTurn(3);
                for (int index = 0; index < player.Length; index++)
                    AssertSpeed(player[index], 2f * (1f + index * .1f), "player alone, slot " + index);
            }
            using (var scope = new CampaignScope(Duel(3, Attack(88002, 9), NoSteps)))
            {
                SlotTiming[] enemy = scope.PlayTurn(3);
                for (int index = 0; index < enemy.Length; index++)
                    AssertSpeed(enemy[index], 2f * (1f + index * .1f), "enemy alone, slot " + index);
            }
        }

        [UnityTest]
        public IEnumerator StepWindowsAndPendingCounters_KeepTheirFirstHitTime_ThenUseTheSettledSlotTempo()
        {
            yield return null;
            SlotTiming[] on, off;
            using (var scope = new CampaignScope(Duel(2, Attack(88002, 10), CombatFeature.All,
                new LegacyCounter(Attack(88003, 7))))) on = scope.PlayTurn(2);
            using (var scope = new CampaignScope(Duel(2, Attack(88002, 10), CombatFeature.All,
                new LegacyCounter(Attack(88003, 7))), TempoOff)) off = scope.PlayTurn(2);

            for (int index = 0; index < 2; index++)
            {
                Assert.That(on[index].SpeedBeforeHit, Is.EqualTo(1f), "An available step keeps its real-time opportunity.");
                Assert.That(on[index].WindowOpens, Is.GreaterThan(0f));
                Assert.That(on[index].WindowOpens, Is.EqualTo(off[index].WindowOpens).Within(Tolerance));
                Assert.That(on[index].WindowCloses, Is.EqualTo(off[index].WindowCloses).Within(Tolerance));
                Assert.That(on[index].ToFirstHit, Is.EqualTo(off[index].ToFirstHit).Within(Tolerance));
            }
            Assert.That(on[0].CounterSettled, Is.True, "The pending player counter became an actual action at its first hit.");
            Assert.That(on[0].SpeedAfterHit, Is.EqualTo(1f), "A settled counter fills the queue slot, so it is not doubled.");
            Assert.That(on[1].CounterSettled, Is.False);
            Assert.That(on[1].SpeedAfterHit, Is.EqualTo(2.2f).Within(1e-5f), "After the counter is spent, the empty tail is doubled.");
            Assert.That(on[1].GapSpeed, Is.EqualTo(1f), "The last slot has no gap to accelerate.");
        }

        [UnityTest]
        public IEnumerator MirroredVersusScreens_UseTheSameProgressiveAndExhaustedTempo()
        {
            yield return null;
            using (var host = new VersusScope(0))
            using (var guest = new VersusScope(1))
            {
                for (int index = 0; index < 4; index++)
                    Accepted(host, guest, 0, OnlineVersusAction.QueueLane, index / LegacyLaneLimit.MaxReservations);
                Accepted(host, guest, 0, OnlineVersusAction.Pass, -1);
                for (int index = 0; index < 2; index++)
                    Accepted(host, guest, 1, OnlineVersusAction.QueueLane, 0);
                Accepted(host, guest, 1, OnlineVersusAction.Pass, -1);
                float[] expected = { 1f, 1.1f, 2.4f, 2.6f };
                for (int index = 0; index < expected.Length; index++)
                {
                    int slotIndex = index;
                    UntilBoth(host, guest, () => host.Versus.Match.CurrentSlot?.SlotIndex == slotIndex &&
                        guest.Versus.Match.CurrentSlot?.SlotIndex == slotIndex);
                    Assert.That(host.Versus.ResolutionPlaybackSpeed, Is.EqualTo(expected[index]).Within(1e-5f), "host slot " + index);
                    Assert.That(guest.Versus.ResolutionPlaybackSpeed, Is.EqualTo(expected[index]).Within(1e-5f), "guest slot " + index);
                    UntilBoth(host, guest, () => host.Versus.Match.CurrentSlot?.HitsResolved > 0 &&
                        guest.Versus.Match.CurrentSlot?.HitsResolved > 0);
                    Assert.That(host.Versus.ResolutionPlaybackSpeed, Is.EqualTo(guest.Versus.ResolutionPlaybackSpeed));
                    UntilBoth(host, guest, () => host.Versus.Match.CurrentSlot == null && guest.Versus.Match.CurrentSlot == null);
                }
                UntilBoth(host, guest, () => !host.Versus.IsResolving && !guest.Versus.IsResolving);
                Assert.That(host.Versus.Match.RoundNumber, Is.EqualTo(2));
                Assert.That(guest.Versus.Match.Left.State.Health, Is.EqualTo(host.Versus.Match.Left.State.Health));
                Assert.That(guest.Versus.Match.Right.State.Health, Is.EqualTo(host.Versus.Match.Right.State.Health));
            }
        }

        private static LegacySkill Attack(int id, int power, int lane = 0) => new LegacySkill(id, "템포 검증", 0, power, power,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, lane, string.Empty);

        private static LegacyQueuedDuel Duel(int enemyCount, LegacySkill enemy, CombatFeature features, LegacyCounter counter = null)
            => new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                new[] { Attack(88001, 10), Attack(88004, 10, 1), Attack(88005, 10, 2) },
                enemy == null ? new LegacySkill[0] : new[] { enemy }, new[] { enemyCount }, 7,
                playerCounter: counter, features: features, meshPercent: 0);

        private static void AssertSpeed(SlotTiming timing, float expected, string message)
        {
            Assert.That(timing.SpeedBeforeHit, Is.EqualTo(expected).Within(1e-5f), message + " before impact");
            Assert.That(timing.SpeedAfterHit, Is.EqualTo(expected).Within(1e-5f), message + " after impact");
            if (timing.HasGap) Assert.That(timing.GapSpeed, Is.EqualTo(expected).Within(1e-5f), message + " gap");
        }

        private struct SlotTiming
        {
            public float ToFirstHit, SpeedBeforeHit, SpeedAfterHit, GapSpeed, WindowOpens, WindowCloses;
            public bool HasGap, CounterSettled;
            public int LegacyStreak;
        }

        private const string Tuning = "\"animationPlaybackSpeed\":1,\"attackInterval\":0.1,\"skillInterval\":0.28," +
            "\"hitStopDuration\":0.04,\"stepAnticipationDuration\":0.24,\"stepTimingWindow\":0.1," +
            "\"showDirectorEnabled\":true,\"minorSlotSpeed\":1,\"majorHitStopScale\":1,\"bladeBindSeconds\":0," +
            "\"combatTempoEnabled\":true,\"combatTempoStep\":0.1,\"combatTempoMaxSpeed\":1.8,\"exhaustedSideSpeed\":2,\"exhaustedGapScale\":1," +
            "\"unopposedStreakEnabled\":true,\"unopposedStreakStartLength\":2,\"unopposedStreakStep\":0.15," +
            "\"unopposedStreakMaxSpeed\":1.6,\"bladeExchangeEnabled\":false";

        private sealed class CampaignScope : IDisposable
        {
            private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originalEnabled;
            private readonly FieldInfo arenaSettings;
            private readonly Action<float, Keyboard> advance;
            private readonly DuelPresentationSettings settings;
            public DuelPrototypeController Controller { get; }
            public float Elapsed { get; private set; }
            public (int, int, int, int) State => (Controller.PlayerHealth, Controller.EnemyHealth,
                Controller.Session.Player.Resistance, Controller.Session.Enemy.Resistance);

            public CampaignScope(LegacyQueuedDuel duel, string extraTuning = null)
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalSession = Controller.Session;
                arenaSettings = typeof(LegacyArenaView).GetField("settings", Private);
                originalArenaSettings = (DuelPresentationSettings)arenaSettings.GetValue(Controller.ArenaView);
                settings = Object.Instantiate(originalSettings);
                try
                {
                    JsonUtility.FromJsonOverwrite("{" + Tuning + "}", settings);
                    if (extraTuning != null) JsonUtility.FromJsonOverwrite("{" + extraTuning + "}", settings);
                    Set("presentationSettings", settings);
                    arenaSettings.SetValue(Controller.ArenaView, settings);
                    Set("mission", null);
                    Set("guide", null);
                    Set("session", duel);
                    advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                        typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
                    typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", Private)
                        .Invoke(Controller, new object[] { null, null });
                    Advance(0f);
                    Assert.That(Controller.CanChoose, Is.True);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void Queue(int count)
            {
                if (count > 3 * LegacyLaneLimit.MaxReservations)
                {
                    // Ten authored exchanges reach the tempo cap; manual planning is tested separately.
                    LegacyQueuedDuel duel = Controller.Session;
                    Assert.That(duel.TrySetFixedQueues(Enumerable.Repeat(duel.GetLane(0)[0], count).ToArray(), duel.EnemyQueue), Is.True);
                    Advance(0f);
                    return;
                }
                for (int index = 0; index < count; index++)
                    Assert.That(Controller.QueueLane(index / LegacyLaneLimit.MaxReservations), Is.True);
            }

            public void Advance(float seconds)
            {
                advance(seconds, null);
                Elapsed += seconds;
            }

            public void Until(Func<bool> condition)
            {
                for (int index = 0; index < MaximumFrames && !condition(); index++) Advance(Frame);
                Assert.That(condition(), Is.True, "The battle presentation must make progress.");
            }

            public SlotTiming[] PlayTurn(int count)
            {
                LegacyQueuedDuel session = Controller.Session;
                int nextRound = session.RoundNumber + 1;
                var timings = new SlotTiming[count];
                Controller.CommitTurn();
                for (int index = 0; index < count; index++)
                {
                    int slotIndex = index;
                    Until(() => session.CurrentSlot?.SlotIndex == slotIndex);
                    LegacyCurrentSlot slot = session.CurrentSlot;
                    var timing = new SlotTiming { WindowOpens = -1f, WindowCloses = -1f,
                        SpeedBeforeHit = Controller.ResolutionPlaybackSpeed, GapSpeed = 1f };
                    float started = Elapsed;
                    for (int frame = 0; frame < MaximumFrames && slot.HitsResolved == 0; frame++)
                    {
                        Advance(Frame);
                        if (slot.HitsResolved > 0) break;
                        timing.SpeedBeforeHit = Mathf.Max(timing.SpeedBeforeHit, Controller.ResolutionPlaybackSpeed);
                        bool window = Controller.IsStepTimingWindow;
                        if (window && timing.WindowOpens < 0f) timing.WindowOpens = Elapsed - started;
                        if (!window && timing.WindowOpens >= 0f && timing.WindowCloses < 0f)
                            timing.WindowCloses = Elapsed - started;
                    }
                    Assert.That(slot.HitsResolved, Is.GreaterThan(0), "Slot " + index + " reaches its impact.");
                    timing.ToFirstHit = Elapsed - started;
                    if (timing.WindowOpens >= 0f && timing.WindowCloses < 0f) timing.WindowCloses = timing.ToFirstHit;
                    timing.SpeedAfterHit = Controller.ResolutionPlaybackSpeed;
                    timing.CounterSettled = slot.PlayerCountered;
                    timing.LegacyStreak = Controller.ShowDirector.StreakLength;
                    Until(() => session.LastResolvedSlot >= slotIndex);
                    if (Controller.IsBetweenSlots)
                    {
                        timing.HasGap = true;
                        timing.GapSpeed = Controller.ResolutionPlaybackSpeed;
                        Until(() => !Controller.IsBetweenSlots);
                    }
                    timings[index] = timing;
                }
                Until(() => Controller.CanChoose);
                Assert.That(session.RoundNumber, Is.EqualTo(nextRound));
                return timings;
            }

            private void Set(string name, object value)
                => typeof(DuelPrototypeController).GetField(name, Private).SetValue(Controller, value);

            public void Dispose()
            {
                Set("presentationSettings", originalSettings);
                arenaSettings.SetValue(Controller.ArenaView, originalArenaSettings);
                Set("session", originalSession);
                Controller.RestartMatch();
                Controller.enabled = originalEnabled;
                Object.Destroy(settings);
            }
        }

        private static void Accepted(VersusScope host, VersusScope guest, int player, OnlineVersusAction action, int lane)
        {
            Assert.That(host.Versus.ApplyOnlineAcceptedAction(player, action, lane, 1), Is.True);
            Assert.That(guest.Versus.ApplyOnlineAcceptedAction(player, action, lane, 1), Is.True);
        }

        private static void UntilBoth(VersusScope host, VersusScope guest, Func<bool> condition)
        {
            for (int index = 0; index < MaximumFrames && !condition(); index++)
            {
                host.Versus.Tick(Frame, null);
                guest.Versus.Tick(Frame, null);
            }
            Assert.That(condition(), Is.True, "Both mirrored screens must complete the same beat.");
        }

        private sealed class VersusScope : IDisposable
        {
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            private readonly GameObject root;
            private readonly LegacyDuelArt art;
            private readonly DuelPresentationSettings settings;
            private readonly LegacyArenaView arena;
            public LocalVersusController Versus { get; }

            public VersusScope(int localPlayer)
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                root = new GameObject("Progressive Tempo Versus " + localPlayer);
                art = new LegacyDuelArt();
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                JsonUtility.FromJsonOverwrite("{" + Tuning + "}", settings);
                arena = LegacyArenaView.Create(root.transform, art, settings);
                Versus = new LocalVersusController(root.transform, art, arena, () => { }, settings);
                Versus.StartOnline(7, localPlayer, false, 0,
                    new[] { Attack(88011, 3), Attack(88013, 3, 1) },
                    new[] { Attack(88012, 2), Attack(88014, 2, 1) },
                    (action, lane) => { }, () => { });
            }

            public void Dispose()
            {
                Versus.Dispose();
                arena.Dispose();
                art.Dispose();
                Object.Destroy(root);
                Object.Destroy(settings);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
