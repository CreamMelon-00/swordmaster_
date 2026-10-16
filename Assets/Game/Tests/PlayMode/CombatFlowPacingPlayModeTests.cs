using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class CombatFlowPacingPlayModeTests : InputTestFixture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float Frame = .002f, Tolerance = Frame * 4f;
        private const int MaximumFrames = 30000;
        private const float Playback = .6f;

        [UnityTest]
        public IEnumerator ShorterGapsAndTravel_FinishRepresentativeTurnsSooner_WithTheSameClipsDamageAndFairStepWindows()
        {
            yield return null;
            var report = new MeasurementReport();
            foreach (int hits in new[] { 1, 3 })
            {
                int count = hits == 1 ? 5 : 3;
                report.comparisons.Add(new Comparison
                {
                    mode = "campaign", slots = count, hitsPerSkill = hits,
                    before = CampaignTurn(false, count, hits), after = CampaignTurn(true, count, hits)
                });
                report.comparisons.Add(new Comparison
                {
                    mode = "mirrored-versus", slots = count, hitsPerSkill = hits,
                    before = VersusTurn(false, count, hits), after = VersusTurn(true, count, hits)
                });
            }

            foreach (Comparison comparison in report.comparisons)
            {
                comparison.speedRatio = comparison.before.elapsed / comparison.after.elapsed;
                TestContext.WriteLine($"Flow pacing {comparison.mode}, {comparison.slots} x {comparison.hitsPerSkill}: " +
                    $"{comparison.before.elapsed:F3}s -> {comparison.after.elapsed:F3}s ({comparison.speedRatio:F3}x)");
                foreach (PhaseTime phase in comparison.before.phases)
                    TestContext.WriteLine($"  {phase.name}: {phase.seconds:F3}s -> {PhaseSeconds(comparison.after, phase.name):F3}s");
            }
            string path = Environment.GetEnvironmentVariable("SWORDMASTER_FLOW_MEASURE_PATH");
            if (!string.IsNullOrEmpty(path))
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }

            foreach (Comparison comparison in report.comparisons)
            {
                TurnTrace before = comparison.before, after = comparison.after;
                string at = comparison.mode + " " + comparison.slots + " x " + comparison.hitsPerSkill;
                Assert.That(after.elapsed, Is.LessThan(before.elapsed - .1f), at + " removes a meaningful amount of waiting without a global playback multiplier.");
                Assert.That(after.finalState, Is.EqualTo(before.finalState), at + " health and resistance");
                Assert.That(after.slots.Count, Is.EqualTo(comparison.slots));
                Assert.That(before.slots.Count, Is.EqualTo(comparison.slots));
                Assert.That(after.totalHits, Is.EqualTo(comparison.slots * comparison.hitsPerSkill));
                Assert.That(after.totalHits, Is.EqualTo(before.totalHits));
                for (int index = 0; index < comparison.slots; index++)
                {
                    SlotTrace oldSlot = before.slots[index], newSlot = after.slots[index];
                    string slotAt = at + " / slot " + index;
                    Assert.That(newSlot.hits.Count, Is.EqualTo(comparison.hitsPerSkill));
                    Assert.That(newSlot.playback, Is.EqualTo(Playback).Within(.00001f), slotAt + " keeps the authored attack clip speed.");
                    Assert.That(newSlot.playback, Is.EqualTo(oldSlot.playback));
                    Assert.That(newSlot.clipSeconds, Is.EqualTo(oldSlot.clipSeconds).Within(.00001f));
                    Assert.That(newSlot.clipSeconds, Is.EqualTo(LegacyArenaView.OriginalClipDuration / Playback).Within(.00001f));
                    Assert.That(newSlot.impactOffset, Is.EqualTo(oldSlot.impactOffset).Within(.00001f));
                    Assert.That(newSlot.impactOffset, Is.EqualTo(LegacyArenaView.OriginalImpactTime / Playback).Within(.00001f));
                    Assert.That(newSlot.hitStops, Is.EqualTo(oldSlot.hitStops).Within(Tolerance), slotAt + " preserves contact stops.");
                    Assert.That(newSlot.progressiveSpeed, Is.EqualTo(oldSlot.progressiveSpeed).Within(.00001f), slotAt + " retains the existing progression, rather than increasing every playback clock.");
                    Assert.That(newSlot.attackInterval, Is.EqualTo(.025f).Within(.00001f));
                    Assert.That(oldSlot.attackInterval, Is.EqualTo(.1f).Within(.00001f));
                    if (comparison.mode != "campaign")
                    {
                        Assert.That(newSlot.hits[0], Is.EqualTo(oldSlot.hits[0]).Within(Tolerance), slotAt + " keeps the real slot-start-to-contact time.");
                        continue;
                    }
                    Assert.That(oldSlot.hits[0] - newSlot.hits[0], Is.EqualTo(.04f).Within(Tolerance), slotAt + " changes only the planned warning lead-in before first contact.");
                    Assert.That(oldSlot.windowOpened, Is.GreaterThan(0f), slotAt + " still offers a timed step.");
                    Assert.That(newSlot.windowOpened, Is.GreaterThan(0f));
                    Assert.That(newSlot.windowClosed - newSlot.windowOpened,
                        Is.EqualTo(oldSlot.windowClosed - oldSlot.windowOpened).Within(Tolerance), slotAt + " keeps the real-time success window width.");
                    Assert.That(newSlot.windowClosed - newSlot.windowOpened, Is.EqualTo(.1f).Within(Tolerance));
                    Assert.That(oldSlot.windowOpened - newSlot.windowOpened, Is.EqualTo(.04f).Within(Tolerance), slotAt + " begins the warning earlier by the intentionally shorter windup.");
                }
            }
        }

        private static TurnTrace CampaignTurn(bool compact, int count, int hits)
        {
            using (var scope = new CampaignScope(compact, count, hits))
            {
                DuelPrototypeController controller = scope.Controller;
                for (int index = 0; index < count; index++) Assert.That(controller.QueueLane(0), Is.True);
                var trace = new TurnTrace();
                controller.CommitTurn();
                LegacyCurrentSlot tracked = null;
                SlotTrace timing = null;
                for (int frame = 0; frame < MaximumFrames && !controller.CanChoose; frame++)
                {
                    AddPhase(trace, Phase(controller), Frame);
                    scope.Advance(Frame);
                    trace.elapsed += Frame;
                    LegacyCurrentSlot current = controller.Session.CurrentSlot;
                    if (current != null && !ReferenceEquals(current, tracked))
                    {
                        tracked = current;
                        timing = NewSlot(controller, current.SlotIndex, current.HitCount, trace.elapsed);
                        trace.slots.Add(timing);
                    }
                    if (tracked == null) continue;
                    ObserveHits(trace, timing, tracked.HitsResolved, trace.elapsed, controller.HitStopTimeRemaining,
                        controller.ResolutionPlaybackSpeed);
                    if (timing.windowOpened < 0f && controller.IsStepTimingWindow)
                        timing.windowOpened = trace.elapsed - timing.started;
                    if (timing.windowOpened >= 0f && timing.windowClosed < 0f && !controller.IsStepTimingWindow)
                        timing.windowClosed = trace.elapsed - timing.started;
                }
                Assert.That(controller.CanChoose, Is.True, "The campaign must return to planning.");
                Assert.That(controller.Session.RoundNumber, Is.EqualTo(2));
                trace.finalState = State(controller.Session.Player, controller.Session.Enemy);
                return trace;
            }
        }

        private static TurnTrace VersusTurn(bool compact, int count, int hits)
        {
            using (var host = new VersusScope(compact, hits, 0))
            using (var guest = new VersusScope(compact, hits, 1))
            {
                for (int index = 0; index < count; index++)
                {
                    Accepted(host, guest, 0, OnlineVersusAction.QueueLane, 0);
                    Accepted(host, guest, 1, OnlineVersusAction.QueueLane, 0);
                }
                Accepted(host, guest, 0, OnlineVersusAction.Pass, -1);
                Accepted(host, guest, 1, OnlineVersusAction.Pass, -1);
                Assert.That(host.Versus.IsResolving && guest.Versus.IsResolving, Is.True);
                var trace = new TurnTrace();
                LocalVersusSlot tracked = null;
                SlotTrace timing = null;
                for (int frame = 0; frame < MaximumFrames && (host.Versus.IsResolving || guest.Versus.IsResolving); frame++)
                {
                    Assert.That(Phase(host.Versus), Is.EqualTo(Phase(guest.Versus)), "Mirroring cannot change which part of a beat is playing.");
                    AddPhase(trace, Phase(host.Versus), Frame);
                    host.Versus.Tick(Frame, null);
                    guest.Versus.Tick(Frame, null);
                    trace.elapsed += Frame;
                    LocalVersusSlot current = host.Versus.Match.CurrentSlot;
                    LocalVersusSlot mirrored = guest.Versus.Match.CurrentSlot;
                    Assert.That(current == null, Is.EqualTo(mirrored == null));
                    Assert.That(host.Versus.HitStopTimeRemaining, Is.EqualTo(guest.Versus.HitStopTimeRemaining).Within(.00001f));
                    if (current != null)
                    {
                        Assert.That(current.SlotIndex, Is.EqualTo(mirrored.SlotIndex));
                        Assert.That(current.HitsResolved, Is.EqualTo(mirrored.HitsResolved));
                        Assert.That(host.Versus.ResolutionPlaybackSpeed, Is.EqualTo(guest.Versus.ResolutionPlaybackSpeed).Within(.00001f));
                        Assert.That(Read<float>(host.Versus, "slotImpactTime"), Is.EqualTo(Read<float>(guest.Versus, "slotImpactTime")));
                        if (!ReferenceEquals(current, tracked))
                        {
                            tracked = current;
                            timing = NewSlot(host.Versus, current.SlotIndex, current.HitCount, trace.elapsed);
                            trace.slots.Add(timing);
                        }
                    }
                    if (tracked != null)
                        ObserveHits(trace, timing, tracked.HitsResolved, trace.elapsed, host.Versus.HitStopTimeRemaining,
                            host.Versus.ResolutionPlaybackSpeed);
                }
                Assert.That(host.Versus.IsResolving || guest.Versus.IsResolving, Is.False, "Both mirrored screens must finish the same turn.");
                Assert.That(host.Versus.Match.RoundNumber, Is.EqualTo(2));
                Assert.That(guest.Versus.Match.RoundNumber, Is.EqualTo(2));
                trace.finalState = State(host.Versus.Match.Left.State, host.Versus.Match.Right.State);
                Assert.That(State(guest.Versus.Match.Left.State, guest.Versus.Match.Right.State), Is.EqualTo(trace.finalState));
                trace.mirrored = true;
                return trace;
            }
        }

        private static SlotTrace NewSlot(object controller, int index, int hits, float elapsed)
        {
            float playback = Read<float>(controller, "slotPlaybackSpeed");
            float interval = Read<float>(controller, "slotAttackInterval");
            float duration = Read<float>(controller, "slotDuration");
            return new SlotTrace
            {
                index = index, started = elapsed, playback = playback, attackInterval = interval,
                clipSeconds = (duration - interval * (hits - 1) - .01f) / hits,
                impactOffset = Read<float>(controller, "slotImpactTime")
            };
        }
        private static void ObserveHits(TurnTrace turn, SlotTrace slot, int hits, float elapsed, float hitStop, float progressive)
        {
            if (hits <= slot.hits.Count) return;
            Assert.That(hits, Is.EqualTo(slot.hits.Count + 1), "Small measured frames must present each impact separately.");
            slot.hits.Add(elapsed - slot.started);
            slot.hitStops.Add(hitStop);
            if (hits == 1) slot.progressiveSpeed = progressive;
            turn.totalHits++;
        }
        private static void AddPhase(TurnTrace turn, string name, float delta)
        {
            foreach (PhaseTime phase in turn.phases)
                if (phase.name == name) { phase.seconds += delta; return; }
            turn.phases.Add(new PhaseTime { name = name, seconds = delta });
        }
        private static float PhaseSeconds(TurnTrace turn, string name)
        {
            foreach (PhaseTime phase in turn.phases) if (phase.name == name) return phase.seconds;
            return 0f;
        }
        private static string Phase(object controller) => Read<object>(controller, "viewPhase").ToString();
        private static T Read<T>(object source, string name) => (T)source.GetType().GetField(name, Private).GetValue(source);
        private static int[] State(LegacyFighterState player, LegacyFighterState enemy)
            => new[] { player.Health, enemy.Health, player.Resistance, enemy.Resistance };
        private static LegacySkill Attack(int id, int hits, int perHit)
            => new LegacySkill(id, "전투 흐름 검증", 0, hits * perHit, hits * perHit,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, 0, string.Empty);
        private static void Configure(DuelPresentationSettings settings, bool compact)
        {
            // Clone the current asset first: emphasis, progressive tempo, hit stops and attack art remain the same.
            JsonUtility.FromJsonOverwrite("{\"animationPlaybackSpeed\":0.6,\"movementDistanceMultiplier\":1.4," +
                "\"movementSpeedMultiplier\":1.2,\"hitStopDuration\":0.04,\"stepTimingWindow\":0.1}", settings);
            JsonUtility.FromJsonOverwrite(compact
                ? "{\"attackInterval\":0.025,\"skillInterval\":0.06,\"stepAnticipationDuration\":0.20," +
                    "\"approachSettleSeconds\":0.04,\"returnSettleSeconds\":0.04,\"turnCleanupSeconds\":0.04," +
                    "\"bladeRecoilScale\":0.35,\"battleStagingSeparation\":5.4}"
                : "{\"attackInterval\":0.1,\"skillInterval\":0.28,\"stepAnticipationDuration\":0.24," +
                    "\"approachSettleSeconds\":0.18,\"returnSettleSeconds\":0.18,\"turnCleanupSeconds\":0.12," +
                    "\"bladeRecoilScale\":1,\"battleStagingSeparation\":7}", settings);
        }

        [Serializable] private sealed class MeasurementReport { public List<Comparison> comparisons = new List<Comparison>(); }
        [Serializable] private sealed class Comparison
        {
            public string mode;
            public int slots, hitsPerSkill;
            public float speedRatio;
            public TurnTrace before, after;
        }
        [Serializable] private sealed class TurnTrace
        {
            public float elapsed;
            public int totalHits;
            public bool mirrored;
            public int[] finalState;
            public List<PhaseTime> phases = new List<PhaseTime>();
            public List<SlotTrace> slots = new List<SlotTrace>();
        }
        [Serializable] private sealed class PhaseTime { public string name; public float seconds; }
        [Serializable] private sealed class SlotTrace
        {
            public int index;
            public float started, playback, clipSeconds, attackInterval, impactOffset, progressiveSpeed;
            public float windowOpened = -1f, windowClosed = -1f;
            public List<float> hits = new List<float>(), hitStops = new List<float>();
        }

        private sealed class CampaignScope : IDisposable
        {
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings, settings;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originalEnabled;
            private readonly FieldInfo arenaSettings = typeof(LegacyArenaView).GetField("settings", Private);
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }
            public CampaignScope(bool compact, int count, int hits)
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalArenaSettings = (DuelPresentationSettings)arenaSettings.GetValue(Controller.ArenaView);
                originalSession = Controller.Session;
                settings = Object.Instantiate(originalSettings);
                try
                {
                    Configure(settings, compact);
                    Set("presentationSettings", settings);
                    arenaSettings.SetValue(Controller.ArenaView, settings);
                    Set("mission", null);
                    Set("guide", null);
                    Set("session", new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                        new[] { Attack(88701, hits, 3) }, new[] { Attack(88702, hits, 2) }, new[] { count }, 7,
                        features: CombatFeature.All, meshPercent: 0));
                    advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                        typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
                    typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", Private).Invoke(Controller, new object[] { null, null });
                    Advance(0f);
                    Assert.That(Controller.CanChoose, Is.True);
                }
                catch { Dispose(); throw; }
            }
            public void Advance(float seconds) => advance(seconds, null);
            private void Set(string name, object value) => typeof(DuelPrototypeController).GetField(name, Private).SetValue(Controller, value);
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
        private sealed class VersusScope : IDisposable
        {
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            private readonly GameObject root;
            private readonly LegacyDuelArt art;
            private readonly DuelPresentationSettings settings;
            private readonly LegacyArenaView arena;
            public LocalVersusController Versus { get; }
            public VersusScope(bool compact, int hits, int localPlayer)
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                root = new GameObject("Flow Pacing Versus " + localPlayer);
                art = new LegacyDuelArt();
                settings = Object.Instantiate(Resources.Load<DuelPresentationSettings>("DuelPresentationSettings"));
                Configure(settings, compact);
                arena = LegacyArenaView.Create(root.transform, art, settings);
                Versus = new LocalVersusController(root.transform, art, arena, () => { }, settings);
                Versus.StartOnline(7, localPlayer, false, 0, new[] { Attack(88701, hits, 3) }, new[] { Attack(88702, hits, 2) },
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
