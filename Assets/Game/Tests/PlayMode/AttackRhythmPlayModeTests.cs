using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class AttackRhythmPlayModeTests : InputTestFixture
    {
        private const float Frame = .001f, Tolerance = .003f;
        private const int MaximumFrames = 30000;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Tuning = "\"animationPlaybackSpeed\":1,\"attackInterval\":0.12,\"skillInterval\":0.28," +
            "\"hitStopDuration\":0.04,\"stepAnticipationDuration\":0.24,\"stepTimingWindow\":0.1," +
            "\"showDirectorEnabled\":false,\"combatTempoEnabled\":false,\"unopposedStreakEnabled\":false," +
            "\"attackRhythmStrength\":1";

        [UnityTest]
        public IEnumerator SheetAttacks_HoldTheirAuthoredGatherAndFinish_OnBothBodies_WithoutReplacingReactionsOrExtendingTheClip()
        {
            yield return null;
            var root = new GameObject("Shared Attack Rhythm Test");
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            using (var art = new LegacyDuelArt())
            using (var arena = LegacyArenaView.Create(root.transform, art, settings,
                attackPoseRandom: new FirstVariant(), enemyAttackPoseRandom: new FirstVariant()))
            {
                try
                {
                    foreach (LegacySkillProperty property in new[] { LegacySkillProperty.Slash, LegacySkillProperty.Penetrate, LegacySkillProperty.Hit })
                    {
                        LegacySkill attack = SheetAttack(property);
                        Configure(settings, true);
                        arena.Reset();
                        arena.SetEnemyAppearance(EnemyAppearance.CadetA);
                        typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", Private).Invoke(arena, new object[] { 1f, .12f });
                        arena.BeginSlot(attack, attack);
                        Vector3 playerPosition = arena.PlayerRenderer.transform.position, enemyPosition = arena.EnemyRenderer.transform.position;
                        Hold(arena, LegacyArenaView.OriginalClipDuration * .16f);
                        Sprite playerGather = arena.PlayerRenderer.sprite, enemyGather = arena.EnemyRenderer.sprite;
                        Hold(arena, LegacyArenaView.OriginalClipDuration * .40f);
                        Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(playerGather));
                        Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(enemyGather));
                        Assert.That(arena.PlayerAttackVisualOffset.x, Is.LessThan(0f));
                        Assert.That(arena.EnemyAttackVisualOffset.x, Is.GreaterThan(0f));
                        Assert.That(arena.PlayerRenderer.transform.position, Is.EqualTo(playerPosition));
                        Assert.That(arena.EnemyRenderer.transform.position, Is.EqualTo(enemyPosition));

                        Hold(arena, LegacyArenaView.OriginalClipDuration * .70f);
                        Sprite playerFinish = arena.PlayerRenderer.sprite, enemyFinish = arena.EnemyRenderer.sprite;
                        Hold(arena, LegacyArenaView.OriginalClipDuration * .88f);
                        Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(playerFinish));
                        Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(enemyFinish));
                        Assert.That(playerFinish, Is.Not.SameAs(playerGather));

                        Hold(arena, LegacyArenaView.OriginalClipDuration * .5f);
                        Assert.That(FrameNumber(arena.PlayerRenderer.sprite), Is.EqualTo(5), "The authored contact cel is still sampled exactly halfway through.");
                        Assert.That(FrameNumber(arena.EnemyRenderer.sprite), Is.EqualTo(5));
                        arena.PresentHit(false, 5, 0, false, false, 5);
                        Sprite hurt = arena.PlayerRenderer.sprite;
                        Assert.That(hurt.name, Does.Contain("hurt"));
                        Hold(arena, LegacyArenaView.OriginalClipDuration * .88f);
                        Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(hurt), "A held finish cannot overwrite an active hit reaction.");
                        arena.Tick(.25f, .25f);
                        Hold(arena, LegacyArenaView.OriginalClipDuration);
                        Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("idle-frame-"));
                        Assert.That(arena.EnemyRenderer.sprite.name, Does.Contain("idle-frame-"));
                        Assert.That(arena.PlayerAttackVisualOffset, Is.EqualTo(Vector3.zero));
                        Assert.That(arena.EnemyAttackVisualOffset, Is.EqualTo(Vector3.zero));

                        arena.Reset();
                        Assert.That(arena.PlayerAttackVisualOffset, Is.EqualTo(Vector3.zero));
                        Assert.That(arena.EnemyAttackVisualOffset, Is.EqualTo(Vector3.zero));
                        typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", Private).Invoke(arena, new object[] { 1f, .12f });
                        arena.BeginSlot(Attack(88501, property), attack);
                        Hold(arena, LegacyArenaView.OriginalClipDuration * .16f);
                        Sprite newAttackGather = arena.PlayerRenderer.sprite;
                        Hold(arena, LegacyArenaView.OriginalClipDuration * .40f);
                        Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(newAttackGather), "An unregistered attack automatically receives the same held preparation after reset.");
                    }
                }
                finally
                {
                    Object.Destroy(settings);
                    Object.Destroy(root);
                }
            }
        }

        [UnityTest]
        public IEnumerator Arena_RetimesVisiblePreparationAndFollowThrough_ButPinsTheActualContactAndClipEnd()
        {
            yield return null;
            var root = new GameObject("Attack Rhythm Arena Test");
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            using (var art = new LegacyDuelArt())
            using (var arena = LegacyArenaView.Create(root.transform, art, settings,
                attackPoseRandom: new FirstVariant(), enemyAttackPoseRandom: new FirstVariant()))
            {
                try
                {
                    foreach (EnemyAppearance appearance in new[] { EnemyAppearance.Elisa, EnemyAppearance.CadetA, EnemyAppearance.CadetB })
                    foreach (LegacySkillProperty property in new[] { LegacySkillProperty.Slash, LegacySkillProperty.Penetrate, LegacySkillProperty.Hit })
                    {
                        int[,] frames = new int[2, 10];
                        for (int enabled = 0; enabled < 2; enabled++)
                        {
                            Configure(settings, enabled != 0);
                            arena.Reset();
                            arena.SetEnemyAppearance(appearance);
                            typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", Private).Invoke(arena, new object[] { 1f, .12f });
                            LegacySkill attack = Attack(88501, property);
                            arena.BeginSlot(attack, attack);
                            float[] anchors = { .16f, .60f, .74f, .5f, .999f };
                            for (int index = 0; index < anchors.Length; index++)
                            {
                                // The main-branch rhythm now accelerates follow-through after .54.
                                // Sample .60 for every family; the old .54 pierce checkpoint is inside
                                // the same contact cel with both linear and remapped playback.
                                float playerPhase = anchors[index];
                                Hold(arena, LegacyArenaView.OriginalClipDuration * playerPhase);
                                frames[enabled, index * 2] = FrameNumber(arena.PlayerRenderer.sprite);
                                Hold(arena, LegacyArenaView.OriginalClipDuration * anchors[index]);
                                frames[enabled, index * 2 + 1] = FrameNumber(arena.EnemyRenderer.sprite);
                            }
                            Hold(arena, LegacyArenaView.OriginalClipDuration);
                            Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("final-idle-frame-"), "The remap cannot extend the clip past its original end.");
                            Assert.That(arena.EnemyRenderer.sprite.name, Does.Contain("idle-frame-"));
                        }
                        for (int side = 0; side < 2; side++)
                        {
                            string at = appearance + " / " + property + " / side " + side;
                            Assert.That(frames[1, side], Is.Not.EqualTo(frames[0, side]), at + " changes the preparation cel.");
                            Assert.That(frames[1, 2 + side], Is.Not.EqualTo(frames[0, 2 + side]), at + " accelerates the follow-through.");
                            Assert.That(frames[1, 4 + side], Is.Not.EqualTo(frames[0, 4 + side]), at + " advances recovery sooner.");
                            int contact = side == 1 ? 5 : property == LegacySkillProperty.Slash ? 4 : property == LegacySkillProperty.Penetrate ? 5 : 7;
                            int last = side == 1 ? 12 : property == LegacySkillProperty.Hit ? 15 : property == LegacySkillProperty.Slash ? 16 : 14;
                            Assert.That(frames[0, 6 + side], Is.EqualTo(contact));
                            Assert.That(frames[1, 6 + side], Is.EqualTo(contact), at + " retains the half-cycle contact cel.");
                            Assert.That(frames[0, 8 + side], Is.EqualTo(last));
                            Assert.That(frames[1, 8 + side], Is.EqualTo(last), at + " retains the final cel before the clip ends.");
                        }
                    }
                }
                finally
                {
                    Object.Destroy(settings);
                    Object.Destroy(root);
                }
            }
        }

        [UnityTest]
        public IEnumerator Campaign_RhythmChangesNoStepWindowHitTimestampMultihitDurationOrDamage()
        {
            yield return null;
            Trace on = CampaignTurn(true), off = CampaignTurn(false);
            Assert.That(on.WindowOpens, Is.GreaterThan(0f));
            Assert.That(on.WindowOpens, Is.EqualTo(off.WindowOpens).Within(Tolerance));
            Assert.That(on.WindowCloses, Is.EqualTo(off.WindowCloses).Within(Tolerance));
            Assert.That(on.HitTimes, Is.EqualTo(off.HitTimes).Within(Tolerance));
            Assert.That(on.SlotDuration, Is.EqualTo(off.SlotDuration));
            Assert.That(on.ClipFinished, Is.EqualTo(off.ClipFinished).Within(Tolerance));
            Assert.That(on.TurnFinished, Is.EqualTo(off.TurnFinished).Within(Tolerance));
            Assert.That(on.State, Is.EqualTo(off.State));
            Assert.That(on.State, Is.EqualTo((1000, 1000, 994, 994)), "All three hits trade the same resistance as before.");
        }

        [UnityTest]
        public IEnumerator HostAndMirroredGuest_RetainTheSameImpactCelsTimestampsAndOutcome_WithRhythmOnOrOff()
        {
            yield return null;
            Trace on = VersusTurn(true), off = VersusTurn(false);
            Assert.That(on.HitTimes, Is.EqualTo(off.HitTimes).Within(Tolerance));
            Assert.That(on.TurnFinished, Is.EqualTo(off.TurnFinished).Within(Tolerance));
            Assert.That(on.State, Is.EqualTo(off.State));
            Assert.That(on.State, Is.EqualTo((100, 100, 44, 44)));
        }

        [UnityTest]
        public IEnumerator DebugRhythmToggle_RestartsOnlyThePreview_WithoutEditingTheSharedSettingsOrOriginalBattle()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.IsCombatDebugMode, Is.False);
            bool wasEnabled = controller.enabled;
            FieldInfo choiceField = typeof(DuelPrototypeController).GetField("combatDebugAttackRhythmChoice", Private);
            object originalChoice = choiceField.GetValue(controller);
            FieldInfo trailChoiceField = typeof(DuelPrototypeController).GetField("combatDebugWeaponTrailChoice", Private);
            object originalTrailChoice = trailChoiceField.GetValue(controller);
            LegacyQueuedDuel originalSession = controller.Session;
            string originalSettings = JsonUtility.ToJson(controller.PresentationSettings);
            controller.enabled = false;
            try
            {
                Assert.That(controller.SetCombatDebugMode(true), Is.True);
                DuelPrototypeController preview = controller.CombatDebugPreview;
                LegacyQueuedDuel before = preview.Session;
                bool rhythm = controller.CombatDebugAttackRhythmEnabled;
                int playerCount = before.PlayerQueue.Count, enemyCount = before.EnemyQueue.Count;
                Button toggle = null;
                foreach (Button button in controller.GetComponentsInChildren<Button>(true))
                    if (button.name == "Debug Attack Rhythm" && button.gameObject.activeInHierarchy) toggle = button;
                Assert.That(toggle, Is.Not.Null);
                toggle.onClick.Invoke();
                Assert.That(controller.CombatDebugAttackRhythmEnabled, Is.EqualTo(!rhythm));
                Assert.That(preview.CombatDebugAttackRhythmEnabled, Is.EqualTo(!rhythm));
                Assert.That(preview.Session, Is.Not.SameAs(before));
                Assert.That((preview.Session.PlayerQueue.Count, preview.Session.EnemyQueue.Count), Is.EqualTo((playerCount, enemyCount)));
                Assert.That(preview.Session.Phase, Is.EqualTo(LegacyDuelPhase.Resolving));
                Assert.That(JsonUtility.ToJson(controller.PresentationSettings), Is.EqualTo(originalSettings));
                Assert.That(controller.Session, Is.SameAs(originalSession));

                before = preview.Session;
                bool trail = controller.CombatDebugWeaponTrailEnabled;
                Button trailToggle = null;
                foreach (Button button in controller.GetComponentsInChildren<Button>(true))
                    if (button.name == "Debug Weapon Trail" && button.gameObject.activeInHierarchy) trailToggle = button;
                Assert.That(trailToggle, Is.Not.Null);
                trailToggle.onClick.Invoke();
                Assert.That(controller.CombatDebugWeaponTrailEnabled, Is.EqualTo(!trail));
                Assert.That(preview.CombatDebugWeaponTrailEnabled, Is.EqualTo(!trail));
                Assert.That(controller.CombatDebugAttackRhythmEnabled, Is.EqualTo(!rhythm), "The two presentation choices are independent.");
                Assert.That(preview.Session, Is.Not.SameAs(before));
                Assert.That((preview.Session.PlayerQueue.Count, preview.Session.EnemyQueue.Count), Is.EqualTo((playerCount, enemyCount)));
                Assert.That(JsonUtility.ToJson(controller.PresentationSettings), Is.EqualTo(originalSettings));
                Assert.That(controller.Session, Is.SameAs(originalSession));
                controller.SetCombatDebugWeaponTrailEnabled(trail);

                before = preview.Session;
                controller.SetCombatDebugAttackRhythmEnabled(rhythm);
                Assert.That(controller.CombatDebugAttackRhythmEnabled, Is.EqualTo(rhythm));
                Assert.That(preview.Session, Is.Not.SameAs(before));
                Assert.That(JsonUtility.ToJson(controller.PresentationSettings), Is.EqualTo(originalSettings));
                Assert.That(controller.Session, Is.SameAs(originalSession));
            }
            finally
            {
                controller.SetCombatDebugMode(false);
                choiceField.SetValue(controller, originalChoice);
                trailChoiceField.SetValue(controller, originalTrailChoice);
                controller.RestartCombatDebugPreview();
                controller.enabled = wasEnabled;
            }
        }

        private static Trace CampaignTurn(bool enabled)
        {
            using (var scope = new CampaignScope(enabled))
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.PresentationSettings.AttackRhythmEnabled, Is.EqualTo(enabled));
                Assert.That(controller.QueueLane(0), Is.True);
                float committed = scope.Elapsed;
                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot != null);
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                float started = scope.Elapsed;
                var trace = new Trace { HitTimes = new float[3], WindowOpens = -1f, WindowCloses = -1f,
                    SlotDuration = controller.ActiveSlotDuration };
                for (int hit = 0; hit < 3; hit++)
                {
                    for (int frame = 0; frame < MaximumFrames && slot.HitsResolved <= hit; frame++)
                    {
                        scope.Advance(Frame);
                        if (hit != 0 || slot.HitsResolved > 0) continue;
                        bool window = controller.IsStepTimingWindow;
                        if (window && trace.WindowOpens < 0f) trace.WindowOpens = scope.Elapsed - started;
                        if (!window && trace.WindowOpens >= 0f && trace.WindowCloses < 0f)
                            trace.WindowCloses = scope.Elapsed - started;
                    }
                    Assert.That(slot.HitsResolved, Is.EqualTo(hit + 1));
                    trace.HitTimes[hit] = scope.Elapsed - started;
                    Assert.That(FrameNumber(controller.ArenaView.PlayerRenderer.sprite), Is.EqualTo(4));
                    Assert.That(FrameNumber(controller.ArenaView.EnemyRenderer.sprite), Is.EqualTo(5));
                }
                if (trace.WindowCloses < 0f) trace.WindowCloses = trace.HitTimes[0];
                scope.Until(() => controller.Session.CurrentSlot == null);
                trace.ClipFinished = scope.Elapsed - started;
                scope.Until(() => controller.CanChoose);
                trace.TurnFinished = scope.Elapsed - committed;
                trace.State = State(controller.Session.Player, controller.Session.Enemy);
                return trace;
            }
        }

        private static Trace VersusTurn(bool enabled)
        {
            using (var host = new VersusScope(enabled, 0, true))
            using (var guest = new VersusScope(enabled, 1, false))
            {
                Assert.That(host.Versus.TryQueueLane(0), Is.True);
                Assert.That(guest.Versus.ApplyOnlineAcceptedAction(0, OnlineVersusAction.QueueLane, 0, 1), Is.True);
                Assert.That(host.Versus.TryCommit(), Is.True);
                Assert.That(guest.Versus.ApplyOnlineAcceptedAction(0, OnlineVersusAction.Pass, -1, 1), Is.True);
                Assert.That(host.Versus.TryApplyOnlinePeerAction(OnlineVersusAction.QueueLane, 0, 1), Is.True);
                Assert.That(guest.Versus.ApplyOnlineAcceptedAction(1, OnlineVersusAction.QueueLane, 0, 1), Is.True);
                Assert.That(host.Versus.TryApplyOnlinePeerAction(OnlineVersusAction.Pass, -1, 1), Is.True);
                Assert.That(guest.Versus.ApplyOnlineAcceptedAction(1, OnlineVersusAction.Pass, -1, 1), Is.True);
                UntilBoth(host, guest, () => host.Versus.Match.CurrentSlot != null && guest.Versus.Match.CurrentSlot != null);
                LocalVersusSlot hostSlot = host.Versus.Match.CurrentSlot, guestSlot = guest.Versus.Match.CurrentSlot;
                var trace = new Trace { HitTimes = new float[3] };
                for (int hit = 0; hit < 3; hit++)
                {
                    int nextHit = hit + 1;
                    UntilBoth(host, guest, () => hostSlot.HitsResolved >= nextHit && guestSlot.HitsResolved >= nextHit);
                    Assert.That((hostSlot.HitsResolved, guestSlot.HitsResolved), Is.EqualTo((nextHit, nextHit)));
                    Assert.That(host.Elapsed, Is.EqualTo(guest.Elapsed));
                    trace.HitTimes[hit] = host.Elapsed;
                    Assert.That(FrameNumber(host.Arena.PlayerRenderer.sprite), Is.EqualTo(4));
                    Assert.That(FrameNumber(host.Arena.EnemyRenderer.sprite), Is.EqualTo(5));
                    Assert.That(FrameNumber(guest.Arena.PlayerRenderer.sprite), Is.EqualTo(4));
                    Assert.That(FrameNumber(guest.Arena.EnemyRenderer.sprite), Is.EqualTo(5));
                }
                UntilBoth(host, guest, () => !host.Versus.IsResolving && !guest.Versus.IsResolving);
                trace.TurnFinished = host.Elapsed;
                trace.State = State(host.Versus.Match.Left.State, host.Versus.Match.Right.State);
                Assert.That(State(guest.Versus.Match.Left.State, guest.Versus.Match.Right.State), Is.EqualTo(trace.State));
                Assert.That(host.Versus.Match.RoundNumber, Is.EqualTo(2));
                Assert.That(guest.Versus.Match.RoundNumber, Is.EqualTo(2));
                return trace;
            }
        }

        private static (int, int, int, int) State(LegacyFighterState player, LegacyFighterState enemy)
            => (player.Health, enemy.Health, player.Resistance, enemy.Resistance);
        private static int FrameNumber(Sprite sprite)
        {
            Assert.That(sprite, Is.Not.Null);
            int start = sprite.name.LastIndexOf("frame-", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "Expected an animation cel, not a hurt or guard pose: " + sprite.name);
            return int.Parse(sprite.name.Substring(start + 6));
        }
        private static void Hold(LegacyArenaView arena, float time)
            => typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", Private).Invoke(arena, new object[] { time });
        private static void Configure(DuelPresentationSettings settings, bool enabled)
        {
            JsonUtility.FromJsonOverwrite("{" + Tuning + "}", settings);
            JsonUtility.FromJsonOverwrite(enabled ? "{\"attackRhythmEnabled\":true}" : "{\"attackRhythmEnabled\":false}", settings);
        }
        private static LegacySkill Attack(int id, LegacySkillProperty property = LegacySkillProperty.Slash, int hits = 1)
            => new LegacySkill(id, "동작 리듬 검증", 0, 6, 6, LegacySkillKind.Attack, property, hits, 0, string.Empty);

        private static LegacySkill SheetAttack(LegacySkillProperty property)
        {
            foreach (LegacySkill skill in LegacySkillDefinitions.InitialSkills)
                if (skill.Kind == LegacySkillKind.Attack && skill.Property == property) return skill;
            Assert.Fail("The initial sheet must provide an attack for " + property);
            return null;
        }

        private struct Trace
        {
            public float[] HitTimes;
            public float WindowOpens, WindowCloses, SlotDuration, ClipFinished, TurnFinished;
            public (int, int, int, int) State;
        }

        private sealed class FirstVariant : System.Random
        {
            public override int Next(int maxValue) => 0;
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
            public CampaignScope(bool enabled)
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
                    Set("session", new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                        new[] { Attack(88511, hits: 3) }, new[] { Attack(88512, hits: 3) }, new[] { 1 }, 7, meshPercent: 0));
                    advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                        typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
                    typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", Private).Invoke(Controller, new object[] { null, null });
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

        private static void UntilBoth(VersusScope host, VersusScope guest, Func<bool> condition)
        {
            for (int frame = 0; frame < MaximumFrames && !condition(); frame++)
            {
                host.Advance();
                guest.Advance();
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
                root = new GameObject("Attack Rhythm Versus " + localPlayer);
                art = new LegacyDuelArt();
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                Configure(settings, enabled);
                Arena = LegacyArenaView.Create(root.transform, art, settings);
                Versus = new LocalVersusController(root.transform, art, Arena, () => { }, settings);
                Versus.StartOnline(7, localPlayer, isHost, 0, new[] { Attack(88511, hits: 3) }, new[] { Attack(88512, hits: 3) },
                    (action, lane) => { }, () => { });
            }
            public void Advance() { Versus.Tick(Frame, null); Elapsed += Frame; }
            public void Dispose()
            {
                Versus.Dispose();
                Arena.Dispose();
                art.Dispose();
                Object.Destroy(root);
                Object.Destroy(settings);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
