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
    public sealed class MultiHitImpactPlayModeTests : InputTestFixture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float Frame = .002f, Tolerance = .004f, Middle = .65f;
        private const int MaximumFrames = 30000;
        private const string Tuning = "\"animationPlaybackSpeed\":1,\"attackInterval\":0.12,\"skillInterval\":0.28," +
            "\"hitStopDuration\":0.04,\"stepAnticipationDuration\":0.24,\"stepTimingWindow\":0.1," +
            "\"showDirectorEnabled\":false,\"combatTempoEnabled\":false,\"unopposedStreakEnabled\":false," +
            "\"bladeExchangeEnabled\":false,\"decisiveHealthDamagePercent\":25,\"multiHitMiddleStrength\":0.65";

        [UnityTest]
        public IEnumerator Campaign_EmphasisesEachTechniquesOwnFinish_WithoutChangingContactsHitStopsOrDamage()
        {
            yield return null;
            Trace on = CampaignTurn(true), off = CampaignTurn(false);
            AssertStrengths(on, new[] { Middle, Middle, 1f }, new[] { Middle, 1f, 1f });
            AssertStrengths(off, new[] { 1f, 1f, 1f }, new[] { 1f, 1f, 1f });
            AssertSameCombat(on, off);
            Assert.That(on.State, Is.EqualTo((1000, 1000, 994, 994)));
        }

        [UnityTest]
        public IEnumerator EarlyBreakHeavyHitAndFinishingBlow_KeepTheirExistingWeightBeforeTheLastAuthoredStrike()
        {
            yield return null;
            // All three are the first contact of a three-hit technique, which would normally be quiet.
            foreach (var scenario in new[] {
                (Name: "break", EnemyHealth: 1000, EnemyResistance: 1, Power: 6),
                (Name: "heavy", EnemyHealth: 20, EnemyResistance: 0, Power: 12),
                (Name: "finish", EnemyHealth: 1, EnemyResistance: 0, Power: 6) })
            {
                Trace on = CampaignTurn(true, firstContactOnly: true, scenario.EnemyHealth,
                    scenario.EnemyResistance, scenario.Power);
                Trace off = CampaignTurn(false, firstContactOnly: true, scenario.EnemyHealth,
                    scenario.EnemyResistance, scenario.Power);
                Assert.That(on.PlayerStrengths[0], Is.EqualTo(1f), scenario.Name);
                Assert.That(on.PlayerStrengths[0], Is.EqualTo(off.PlayerStrengths[0]), scenario.Name);
                AssertSameCombat(on, off);
            }
        }

        [UnityTest]
        public IEnumerator HostAndMirroredGuest_UseTheirOwnHitCounts_AndKeepTheSameClockDamageAndOutcome()
        {
            yield return null;
            Trace on = VersusTurn(true), off = VersusTurn(false);
            AssertStrengths(on, new[] { Middle, Middle, 1f }, new[] { Middle, 1f, 1f });
            AssertStrengths(off, new[] { 1f, 1f, 1f }, new[] { 1f, 1f, 1f });
            AssertSameCombat(on, off);
            Assert.That(on.State, Is.EqualTo((100, 100, 44, 44)));
        }

        private static Trace CampaignTurn(bool enabled, bool firstContactOnly = false,
            int enemyHealth = 1000, int enemyResistance = 1000, int playerPower = 6)
        {
            using (var scope = new CampaignScope(enabled, enemyHealth, enemyResistance, playerPower))
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot != null);
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                var trace = new Trace(firstContactOnly ? 1 : 3) { SlotDuration = controller.ActiveSlotDuration };
                for (int hit = 0; hit < trace.HitTimes.Length; hit++)
                {
                    int expected = hit + 1;
                    scope.Until(() => slot.HitsResolved >= expected);
                    Assert.That(slot.HitsResolved, Is.EqualTo(expected), "Each contact must be observed separately.");
                    trace.Observe(hit, scope.Elapsed, controller.HitStopTimeRemaining, controller.ArenaView);
                }
                if (!firstContactOnly) scope.Until(() => controller.CanChoose);
                trace.Elapsed = scope.Elapsed;
                trace.State = State(controller.Session.Player, controller.Session.Enemy);
                return trace;
            }
        }

        private static Trace VersusTurn(bool enabled)
        {
            using (var host = new VersusScope(enabled, 0, true))
            using (var guest = new VersusScope(enabled, 1, false))
            {
                Accepted(host, guest, 0, OnlineVersusAction.QueueLane, 0);
                Accepted(host, guest, 0, OnlineVersusAction.Pass, -1);
                Accepted(host, guest, 1, OnlineVersusAction.QueueLane, 0);
                Accepted(host, guest, 1, OnlineVersusAction.Pass, -1);
                UntilBoth(host, guest, () => host.Versus.Match.CurrentSlot != null && guest.Versus.Match.CurrentSlot != null);
                LocalVersusSlot hostSlot = host.Versus.Match.CurrentSlot, guestSlot = guest.Versus.Match.CurrentSlot;
                var trace = new Trace(3);
                for (int hit = 0; hit < 3; hit++)
                {
                    int expected = hit + 1;
                    UntilBoth(host, guest, () => hostSlot.HitsResolved >= expected && guestSlot.HitsResolved >= expected);
                    Assert.That((hostSlot.HitsResolved, guestSlot.HitsResolved), Is.EqualTo((expected, expected)));
                    Assert.That(host.Elapsed, Is.EqualTo(guest.Elapsed));
                    Assert.That(host.Versus.HitStopTimeRemaining,
                        Is.EqualTo(guest.Versus.HitStopTimeRemaining).Within(.00001f));
                    Assert.That(host.Arena.PlayerImpactStrength, Is.EqualTo(guest.Arena.EnemyImpactStrength),
                        "The host's own strike is the guest's opposing strike.");
                    Assert.That(host.Arena.EnemyImpactStrength, Is.EqualTo(guest.Arena.PlayerImpactStrength));
                    trace.Observe(hit, host.Elapsed, host.Versus.HitStopTimeRemaining, host.Arena);
                }
                UntilBoth(host, guest, () => !host.Versus.IsResolving && !guest.Versus.IsResolving);
                trace.Elapsed = host.Elapsed;
                trace.State = State(host.Versus.Match.Left.State, host.Versus.Match.Right.State);
                Assert.That(State(guest.Versus.Match.Left.State, guest.Versus.Match.Right.State), Is.EqualTo(trace.State));
                Assert.That((host.Versus.Match.RoundNumber, guest.Versus.Match.RoundNumber), Is.EqualTo((2, 2)));
                return trace;
            }
        }

        private static void Accepted(VersusScope host, VersusScope guest, int player, OnlineVersusAction action, int lane)
        {
            bool accepted = player == 0
                ? action == OnlineVersusAction.Pass ? host.Versus.TryCommit() : host.Versus.TryQueueLane(lane)
                : host.Versus.TryApplyOnlinePeerAction(action, lane, 1);
            Assert.That(accepted, Is.True);
            Assert.That(guest.Versus.ApplyOnlineAcceptedAction(player, action, lane, 1), Is.True);
        }

        private static void AssertStrengths(Trace trace, float[] player, float[] enemy)
        {
            Assert.That(trace.PlayerStrengths, Is.EqualTo(player).Within(.00001f));
            Assert.That(trace.EnemyStrengths, Is.EqualTo(enemy).Within(.00001f));
        }

        private static void AssertSameCombat(Trace on, Trace off)
        {
            Assert.That(on.HitTimes, Is.EqualTo(off.HitTimes).Within(Tolerance));
            Assert.That(on.HitStops, Is.EqualTo(off.HitStops).Within(.00001f));
            Assert.That(on.SlotDuration, Is.EqualTo(off.SlotDuration).Within(.00001f));
            Assert.That(on.Elapsed, Is.EqualTo(off.Elapsed).Within(Tolerance));
            Assert.That(on.State, Is.EqualTo(off.State));
        }

        private static (int, int, int, int) State(LegacyFighterState player, LegacyFighterState enemy)
            => (player.Health, enemy.Health, player.Resistance, enemy.Resistance);

        private static LegacySkill Attack(int id, int count, int power = 6)
            => new LegacySkill(id, "연타 끝타 검증", 0, power, power,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, count, 0, string.Empty);

        private static void Configure(DuelPresentationSettings settings, bool enabled)
            => JsonUtility.FromJsonOverwrite("{" + Tuning + ",\"multiHitEmphasisEnabled\":" +
                (enabled ? "true" : "false") + "}", settings);

        private sealed class Trace
        {
            public readonly float[] HitTimes, HitStops, PlayerStrengths, EnemyStrengths;
            public float SlotDuration, Elapsed;
            public (int, int, int, int) State;
            public Trace(int hits)
            {
                HitTimes = new float[hits]; HitStops = new float[hits];
                PlayerStrengths = new float[hits]; EnemyStrengths = new float[hits];
            }
            public void Observe(int index, float elapsed, float hitStop, LegacyArenaView arena)
            {
                HitTimes[index] = elapsed; HitStops[index] = hitStop;
                PlayerStrengths[index] = arena.PlayerImpactStrength;
                EnemyStrengths[index] = arena.EnemyImpactStrength;
            }
        }

        private sealed class CampaignScope : IDisposable
        {
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings, settings;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originalEnabled;
            private readonly FieldInfo arenaSettings;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }
            public float Elapsed { get; private set; }
            public CampaignScope(bool enabled, int enemyHealth, int enemyResistance, int playerPower)
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
                    Configure(settings, enabled);
                    Set("presentationSettings", settings);
                    arenaSettings.SetValue(Controller.ArenaView, settings);
                    Set("mission", null);
                    Set("guide", null);
                    Set("session", new LegacyQueuedDuel(1000, 1000, enemyHealth, enemyResistance,
                        new[] { Attack(99611, 3, playerPower) }, new[] { Attack(99612, 2) }, new[] { 1 }, 7, meshPercent: 0));
                    advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                        typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
                    typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", Private)
                        .Invoke(Controller, new object[] { null, null });
                    Advance(0f);
                    Assert.That(Controller.CanChoose, Is.True);
                }
                catch { Dispose(); throw; }
            }
            public void Advance(float seconds) { advance(seconds, null); Elapsed += seconds; }
            public void Until(Func<bool> condition)
            {
                for (int frame = 0; frame < MaximumFrames && !condition(); frame++) Advance(Frame);
                Assert.That(condition(), Is.True, "The campaign presentation must make progress.");
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

        private static void UntilBoth(VersusScope host, VersusScope guest, Func<bool> condition)
        {
            for (int frame = 0; frame < MaximumFrames && !condition(); frame++)
            {
                host.Advance(); guest.Advance();
            }
            Assert.That(condition(), Is.True, "Both versus perspectives must progress together.");
        }

        private sealed class VersusScope : IDisposable
        {
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            private readonly GameObject root;
            private readonly LegacyDuelArt art;
            private readonly DuelPresentationSettings settings;
            public LegacyArenaView Arena { get; }
            public LocalVersusController Versus { get; }
            public float Elapsed { get; private set; }
            public VersusScope(bool enabled, int localPlayer, bool isHost)
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                root = new GameObject("Multi Hit Emphasis Versus " + localPlayer);
                art = new LegacyDuelArt();
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                Configure(settings, enabled);
                Arena = LegacyArenaView.Create(root.transform, art, settings);
                Versus = new LocalVersusController(root.transform, art, Arena, () => { }, settings);
                Versus.StartOnline(7, localPlayer, isHost, 0, new[] { Attack(99611, 3) },
                    new[] { Attack(99612, 2) }, (action, lane) => { }, () => { });
            }
            public void Advance() { Versus.Tick(Frame, null); Elapsed += Frame; }
            public void Dispose()
            {
                Versus.Dispose(); Arena.Dispose(); art.Dispose();
                Object.Destroy(root); Object.Destroy(settings);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
