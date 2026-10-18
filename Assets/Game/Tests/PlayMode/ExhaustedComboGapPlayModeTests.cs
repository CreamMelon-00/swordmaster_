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
    /// <summary>An exhausted queue tightens the space between skills, preserving every skill's own timing and rules.</summary>
    public sealed class ExhaustedComboGapPlayModeTests : InputTestFixture
    {
        private const float Frame = .002f, Tolerance = Frame * 4f;
        private const int MaximumFrames = 40000;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly CombatFeature NoSteps = CombatFeature.All & ~CombatFeature.Dodge & ~CombatFeature.Pressure;
        private const string Tuning = "\"animationPlaybackSpeed\":1,\"attackInterval\":0.1,\"skillInterval\":0.28," +
            "\"hitStopDuration\":0.04,\"stepAnticipationDuration\":0.24,\"stepTimingWindow\":0.1," +
            "\"showDirectorEnabled\":true,\"minorSlotSpeed\":1,\"majorHitStopScale\":1,\"bladeBindSeconds\":0," +
            "\"combatTempoEnabled\":true,\"combatTempoStep\":0,\"combatTempoMaxSpeed\":1.8,\"exhaustedSideSpeed\":1," +
            "\"unopposedStreakEnabled\":true";

        [UnityTest]
        public IEnumerator EmptyQueue_TightensOnlyTheGap_KeepingFirstHitsMultihitsRecoveryAndTheDuelUnchanged()
        {
            yield return null;
            SlotTimes[] tight, normal;
            float tightTurn, normalTurn;
            (int, int, int, int) tightState, normalState;
            using (var scope = new CampaignScope(Duel(Attack(88101, 12, 3), new LegacySkill[0], 0, CombatFeature.All), .25f))
            {
                scope.Queue(3);
                float started = scope.Elapsed;
                tight = scope.PlayTurn(3);
                tightTurn = scope.Elapsed - started;
                tightState = scope.State;
            }
            using (var scope = new CampaignScope(Duel(Attack(88101, 12, 3), new LegacySkill[0], 0, CombatFeature.All), 1f))
            {
                scope.Queue(3);
                float started = scope.Elapsed;
                normal = scope.PlayTurn(3);
                normalTurn = scope.Elapsed - started;
                normalState = scope.State;
            }
            for (int index = 0; index < tight.Length; index++)
            {
                string at = "skill " + index;
                Assert.That(tight[index].Hits.Length, Is.EqualTo(3), at + " retains its three hits.");
                Assert.That(tight[index].Hits[0], Is.EqualTo(normal[index].Hits[0]).Within(Tolerance), at + " first impact");
                for (int hit = 1; hit < 3; hit++)
                    Assert.That(tight[index].Hits[hit] - tight[index].Hits[hit - 1],
                        Is.EqualTo(normal[index].Hits[hit] - normal[index].Hits[hit - 1]).Within(Tolerance), at + " inter-hit timing");
                Assert.That(tight[index].HitStops, Is.EqualTo(normal[index].HitStops).Within(1e-5f), at + " hit stops");
                Assert.That(tight[index].Recovery, Is.EqualTo(normal[index].Recovery).Within(Tolerance), at + " clip recovery");
                Assert.That(tight[index].WindowOpens, Is.GreaterThan(0f), at + " still offers a step window.");
                Assert.That(tight[index].WindowOpens, Is.EqualTo(normal[index].WindowOpens).Within(Tolerance));
                Assert.That(tight[index].WindowCloses, Is.EqualTo(normal[index].WindowCloses).Within(Tolerance));
                Assert.That(tight[index].PlaybackSpeed, Is.EqualTo(1f), at + " does not speed up its animation.");
                if (index < tight.Length - 1)
                {
                    Assert.That(tight[index].Gap, Is.EqualTo(.07f).Within(Tolerance), at + " shortened skill gap");
                    Assert.That(normal[index].Gap, Is.EqualTo(.28f).Within(Tolerance), at + " original skill gap");
                }
            }
            Assert.That(tightTurn, Is.LessThan(normalTurn - .3f), "Two shorter gaps make the turn visibly shorter.");
            Assert.That(tightState, Is.EqualTo(normalState), "Damage, health and resistance are identical.");
        }

        [UnityTest]
        public IEnumerator BreathAndGuard_KeepTheirGaps_OnlyTheEmptyTailTightens_AndTheNextTurnStartsNormally()
        {
            yield return null;
            var guard = new LegacySkill(88103, "간격 검증 방어", 0, 8, 8, LegacySkillKind.Defence,
                LegacySkillProperty.Defence, 1, 0, string.Empty);
            using (var scope = new CampaignScope(Duel(Attack(88101, 10),
                new[] { LegacyCommonActions.Breathe, guard }, 2, NoSteps), .25f))
            {
                scope.Queue(4);
                SlotTimes[] first = scope.PlayTurn(4);
                Assert.That(first[0].Gap, Is.EqualTo(.28f).Within(Tolerance), "A breath is a queued skill.");
                Assert.That(first[1].Gap, Is.EqualTo(.28f).Within(Tolerance), "A guard is a queued skill.");
                Assert.That(first[2].Gap, Is.EqualTo(.07f).Within(Tolerance), "Only the first truly empty slot tightens the gap.");
                Assert.That(first[3].HasGap, Is.False, "There is no artificial gap after the final skill.");

                scope.Queue(4);
                SlotTimes[] next = scope.PlayTurn(4);
                Assert.That(next[0].Gap, Is.EqualTo(.28f).Within(Tolerance), "The previous empty tail cannot shorten the next turn's breath.");
                Assert.That(next[1].Gap, Is.EqualTo(.28f).Within(Tolerance), "The next turn's guard also restores a normal gap.");
                Assert.That(next[2].Gap, Is.EqualTo(.07f).Within(Tolerance));
            }
        }

        [UnityTest]
        public IEnumerator MirroredVersusScreens_KeepPairedSkillGaps_AndTightenTheSameEmptyTail()
        {
            yield return null;
            using (var host = new VersusScope(0))
            using (var guest = new VersusScope(1))
            {
                for (int index = 0; index < 4; index++)
                    Accepted(host, guest, 0, OnlineVersusAction.QueueLane, 0);
                Accepted(host, guest, 0, OnlineVersusAction.Pass, -1);
                for (int index = 0; index < 2; index++)
                    Accepted(host, guest, 1, OnlineVersusAction.QueueLane, 0);
                Accepted(host, guest, 1, OnlineVersusAction.Pass, -1);
                for (int index = 0; index < 4; index++)
                {
                    int slotIndex = index;
                    UntilBoth(host, guest, () => host.Versus.Match.CurrentSlot?.SlotIndex == slotIndex &&
                        guest.Versus.Match.CurrentSlot?.SlotIndex == slotIndex);
                    Assert.That(host.Versus.ResolutionPlaybackSpeed, Is.EqualTo(1f));
                    Assert.That(guest.Versus.ResolutionPlaybackSpeed, Is.EqualTo(1f));
                    UntilBoth(host, guest, () => host.Versus.Match.CurrentSlot == null && guest.Versus.Match.CurrentSlot == null);
                    if (index == 3) break;
                    Assert.That(host.IsBetweenSlots && guest.IsBetweenSlots, Is.True);
                    float hostStart = host.Elapsed, guestStart = guest.Elapsed;
                    UntilBoth(host, guest, () => !host.IsBetweenSlots && !guest.IsBetweenSlots);
                    float expected = index < 2 ? .28f : .07f;
                    Assert.That(host.Elapsed - hostStart, Is.EqualTo(expected).Within(Tolerance), "host gap after slot " + index);
                    Assert.That(guest.Elapsed - guestStart, Is.EqualTo(expected).Within(Tolerance), "guest gap after slot " + index);
                }
                UntilBoth(host, guest, () => !host.Versus.IsResolving && !guest.Versus.IsResolving);
                Assert.That(host.Versus.Match.RoundNumber, Is.EqualTo(2));
                Assert.That(guest.Versus.Match.Left.State.Health, Is.EqualTo(host.Versus.Match.Left.State.Health));
                Assert.That(guest.Versus.Match.Right.State.Health, Is.EqualTo(host.Versus.Match.Right.State.Health));
            }
        }

        private static LegacySkill Attack(int id, int power, int hits = 1) => new LegacySkill(id, "간격 검증 공격", 0, power, power,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, 0, string.Empty);

        private static LegacyQueuedDuel Duel(LegacySkill player, LegacySkill[] enemy, int enemyCount, CombatFeature features)
            => new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { player }, enemy,
                new[] { enemyCount }, 7, features: features, meshPercent: 0);

        private static void Configure(DuelPresentationSettings settings, float gapScale)
        {
            JsonUtility.FromJsonOverwrite("{" + Tuning + "}", settings);
            JsonUtility.FromJsonOverwrite(gapScale < 1f ? "{\"exhaustedGapScale\":0.25}" : "{\"exhaustedGapScale\":1}", settings);
        }

        private struct SlotTimes
        {
            public float[] Hits, HitStops;
            public float Recovery, Gap, WindowOpens, WindowCloses, PlaybackSpeed;
            public bool HasGap;
        }

        private sealed class CampaignScope : IDisposable
        {
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

            public CampaignScope(LegacyQueuedDuel duel, float gapScale)
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
                    Configure(settings, gapScale);
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
                catch { Dispose(); throw; }
            }

            public void Queue(int count)
            {
                for (int index = 0; index < count; index++) Assert.That(Controller.QueueLane(0), Is.True);
            }

            public void Advance(float seconds)
            {
                advance(seconds, null);
                Elapsed += seconds;
            }

            private void Until(Func<bool> condition)
            {
                for (int index = 0; index < MaximumFrames && !condition(); index++) Advance(Frame);
                Assert.That(condition(), Is.True, "The battle presentation must make progress.");
            }

            public SlotTimes[] PlayTurn(int count)
            {
                LegacyQueuedDuel session = Controller.Session;
                int nextRound = session.RoundNumber + 1;
                var times = new SlotTimes[count];
                Controller.CommitTurn();
                for (int index = 0; index < count; index++)
                {
                    int slotIndex = index;
                    Until(() => session.CurrentSlot?.SlotIndex == slotIndex);
                    LegacyCurrentSlot slot = session.CurrentSlot;
                    var timing = new SlotTimes { Hits = new float[slot.HitCount], HitStops = new float[slot.HitCount],
                        WindowOpens = -1f, WindowCloses = -1f };
                    float started = Elapsed;
                    int recordedHits = 0;
                    while (recordedHits < slot.HitCount)
                    {
                        int hit = recordedHits;
                        for (int frame = 0; frame < MaximumFrames && slot.HitsResolved <= hit; frame++)
                        {
                            Advance(Frame);
                            if (slot.HitsResolved > hit) break;
                            if (hit != 0) continue;
                            bool window = Controller.IsStepTimingWindow;
                            if (window && timing.WindowOpens < 0f) timing.WindowOpens = Elapsed - started;
                            if (!window && timing.WindowOpens >= 0f && timing.WindowCloses < 0f)
                                timing.WindowCloses = Elapsed - started;
                        }
                        Assert.That(slot.HitsResolved, Is.EqualTo(hit + 1), "Each impact resolves separately.");
                        timing.Hits[hit] = Elapsed - started;
                        timing.HitStops[hit] = Controller.HitStopTimeRemaining;
                        if (hit == 0)
                        {
                            timing.PlaybackSpeed = Controller.ResolutionPlaybackSpeed;
                            if (timing.WindowOpens >= 0f && timing.WindowCloses < 0f) timing.WindowCloses = timing.Hits[hit];
                        }
                        recordedHits++;
                    }
                    float lastHitAt = Elapsed;
                    Until(() => session.LastResolvedSlot >= slotIndex);
                    timing.Recovery = Elapsed - lastHitAt;
                    timing.HasGap = Controller.IsBetweenSlots;
                    if (timing.HasGap)
                    {
                        float gapStarted = Elapsed;
                        Until(() => !Controller.IsBetweenSlots);
                        timing.Gap = Elapsed - gapStarted;
                    }
                    times[index] = timing;
                }
                Until(() => Controller.CanChoose);
                Assert.That(session.RoundNumber, Is.EqualTo(nextRound));
                return times;
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
                host.Advance();
                guest.Advance();
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
            private readonly FieldInfo viewPhase = typeof(LocalVersusController).GetField("viewPhase", Private);
            public LocalVersusController Versus { get; }
            public float Elapsed { get; private set; }
            public bool IsBetweenSlots => viewPhase.GetValue(Versus).ToString() == "BetweenSlots";

            public VersusScope(int localPlayer)
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                root = new GameObject("Exhausted Combo Gap Versus " + localPlayer);
                art = new LegacyDuelArt();
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                Configure(settings, .25f);
                arena = LegacyArenaView.Create(root.transform, art, settings);
                Versus = new LocalVersusController(root.transform, art, arena, () => { }, settings);
                Versus.StartOnline(7, localPlayer, false, 0, new[] { Attack(88111, 3) }, new[] { Attack(88112, 2) },
                    (action, lane) => { }, () => { });
            }

            public void Advance()
            {
                Versus.Tick(Frame, null);
                Elapsed += Frame;
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
