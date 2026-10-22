using System;
using System.Collections;
using System.IO;
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
    public sealed class ResistanceImpactControllerPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float Frame = .002f;
        private const string Tuning = "\"animationPlaybackSpeed\":1,\"attackInterval\":0.1,\"skillInterval\":0.1," +
            "\"hitStopDuration\":0,\"showDirectorEnabled\":false,\"combatTempoEnabled\":false," +
            "\"unopposedStreakEnabled\":false,\"bladeExchangeEnabled\":false,\"autoDepthEnabled\":false," +
            "\"attackRhythmEnabled\":false,\"stepAnticipationDuration\":0.1";

        [UnityTest]
        public IEnumerator Campaign_CappedReductionConnectsBeforeItsBreakAndQueueShatter_WithoutAnExtraHitOrActCost()
        {
            yield return null;
            using (var scope = new CampaignScope(Duel(Attack(42), Guard(), enemyResistance: 5, enemyActions: 3)))
            {
                var c = scope.Controller;
                var duel = c.Session;
                scope.Begin(3);
                scope.Until(() => c.ArenaView.IsResistanceImpactPlaying);
                LegacyCurrentSlot slot = duel.CurrentSlot;
                Assert.That(slot.PlayerFeedback.OpponentResistanceReduced, Is.EqualTo(5), "Use the capped actual loss, not the configured fifteen.");
                Assert.That(duel.Enemy.IsResistanceBroken, Is.True, "The runtime applies opening effects once, at the original boundary.");
                Assert.That(duel.Act, Is.Zero, "Three ACT 1 reservations have already been paid.");
                Assert.That(slot.HitsResolved, Is.Zero);
                Assert.That(duel.Enemy.Health, Is.EqualTo(1000));
                Assert.That(c.ArenaView.HasResistanceImpactContact, Is.False);
                Assert.That(c.ArenaView.EnemyBreakAura.IsBroken, Is.False);
                Assert.That(BreakCalls(c.Hud), Is.Zero);
                Assert.That(c.Hud.BreakFragmentEmissionCount, Is.Zero);
                Assert.That(c.Hud.GetQueuedSkillAnchor(false, 1), Is.Not.Null, "The canceled reservation remains visible until contact.");
                Capture(c.ArenaView, "01-before-contact");
                scope.Until(() => c.ArenaView.HasResistanceImpactContact);
                Assert.That(slot.HitsResolved, Is.Zero);
                Assert.That(c.ArenaView.EnemyBreakAura.IsBroken, Is.True);
                Assert.That(BreakCalls(c.Hud), Is.EqualTo(1));
                Assert.That(c.Hud.BreakFragmentEmissionCount, Is.EqualTo(2));
                Assert.That(c.Hud.GetQueuedSkillAnchor(false, 1), Is.Null);
                Capture(c.ArenaView, "02-body-check-contact");
                scope.Until(() => !c.ArenaView.IsResistanceImpactPlaying);
                Assert.That(slot.HitsResolved, Is.Zero);
                scope.Until(() => slot.HitsResolved == 1);
                Assert.That(duel.Act, Is.Zero);
                Assert.That(slot.HitsResolved, Is.EqualTo(1));
                Assert.That(duel.Enemy.Health, Is.EqualTo(1000), "The ordinary strike is fully guarded; the body check never adds HP damage.");
                Assert.That(c.Hud.BreakFragmentEmissionCount, Is.EqualTo(2), "The first ordinary hit must not replay cancellation.");
                Capture(c.ArenaView, "03-ordinary-attack");
            }
        }

        [UnityTest]
        public IEnumerator Campaign_UnmatchedConditionsAlreadyZeroResistanceAndRecoveryDoNotAddABodyCheck()
        {
            yield return null;
            foreach (int scenario in new[] { 0, 1, 2 })
            {
                LegacySkill own = scenario == 2 ? Guard(501) : Attack(42);
                LegacySkill opposing = scenario == 0 ? Attack(99002) : Guard();
                using (var scope = new CampaignScope(Duel(own, opposing, enemyResistance: scenario == 0 ? 50 : 0)))
                {
                    var c = scope.Controller;
                    if (scenario == 2) Reduce(c.Session.Player, 25);
                    scope.Begin();
                    scope.Until(() => c.Session.CurrentSlot?.HitsResolved > 0, () =>
                        Assert.That(c.ArenaView.IsResistanceImpactPlaying, Is.False,
                            "Only actual outgoing resistance removal triggers the body check, scenario " + scenario));
                    Assert.That(c.Session.CurrentSlot.PlayerFeedback.OpponentResistanceReduced, Is.Zero);
                    Assert.That(c.ArenaView.HasResistanceImpactContact, Is.False);
                    if (scenario == 2) Assert.That(c.Session.CurrentSlot.PlayerFeedback.ResistanceRestored, Is.GreaterThan(0));
                }
            }
        }

        [UnityTest]
        public IEnumerator Campaign_BothResistanceReducingCastersConnectTogetherAndStillResolveOnlyOneAttackEach()
        {
            yield return null;
            using (var scope = new CampaignScope(Duel(Attack(5), Attack(5))))
            {
                var c = scope.Controller;
                scope.Begin();
                scope.Until(() => c.ArenaView.IsResistanceImpactPlaying);
                LegacyCurrentSlot slot = c.Session.CurrentSlot;
                Assert.That((slot.PlayerFeedback.OpponentResistanceReduced, slot.EnemyFeedback.OpponentResistanceReduced), Is.EqualTo((10, 10)));
                Assert.That((c.Session.Player.Resistance, c.Session.Enemy.Resistance), Is.EqualTo((40, 40)));
                Assert.That(c.Session.Act, Is.EqualTo(2));
                scope.Until(() => c.ArenaView.HasResistanceImpactContact);
                Assert.That(slot.HitsResolved, Is.Zero);
                Assert.That((c.Session.Player.Health, c.Session.Enemy.Health), Is.EqualTo((1000, 1000)));
                Assert.That(BreakCalls(c.Hud), Is.Zero, "A partial reduction is not a break.");
                scope.Until(() => slot.HitsResolved == 1);
                Assert.That((c.Session.Player.Resistance, c.Session.Enemy.Resistance), Is.EqualTo((39, 39)));
                Assert.That(c.Session.Act, Is.EqualTo(2));
            }
        }

        [UnityTest]
        public IEnumerator Campaign_PauseHoldsTheBodyCheck_AndRestartDiscardsItsPendingContact()
        {
            yield return null;
            using (var scope = new CampaignScope(Duel(Attack(42), Guard(), enemyResistance: 5)))
            {
                var c = scope.Controller;
                scope.Begin();
                scope.Until(() => c.ArenaView.IsResistanceImpactPlaying);
                Assert.That(c.PauseBattle(), Is.True);
                Vector3 held = c.ArenaView.PlayerRenderer.transform.localPosition;
                scope.Advance(1f);
                Assert.That(c.ArenaView.PlayerRenderer.transform.localPosition, Is.EqualTo(held));
                Assert.That(c.ArenaView.HasResistanceImpactContact, Is.False);
                Assert.That(BreakCalls(c.Hud), Is.Zero);
                Assert.That(c.ResumeBattle(), Is.True);
                scope.Until(() => c.ArenaView.HasResistanceImpactContact);
                Assert.That(BreakCalls(c.Hud), Is.EqualTo(1));
            }
            using (var scope = new CampaignScope(Duel(Attack(42), Guard(), enemyResistance: 5)))
            {
                var c = scope.Controller;
                scope.Begin();
                scope.Until(() => c.ArenaView.IsResistanceImpactPlaying);
                LegacyCurrentSlot discarded = c.Session.CurrentSlot;
                c.RestartMatch();
                Assert.That(c.ArenaView.IsResistanceImpactPlaying, Is.False);
                Assert.That(c.ArenaView.HasResistanceImpactContact, Is.False);
                scope.Advance(1f);
                Assert.That(discarded.HitsResolved, Is.Zero);
                Assert.That(BreakCalls(c.Hud), Is.Zero, "A discarded contact must not announce a break in the new attempt.");
            }
        }

        [UnityTest]
        public IEnumerator HostAndGuest_DelayTheSameActualBreakUntilContact_WithEachCasterMirroredCorrectly()
        {
            yield return null;
            foreach (int caster in new[] { 0, 1 })
                using (var host = new VersusScope(0, caster == 0 ? Attack(42) : Guard(), caster == 1 ? Attack(42) : Guard()))
                using (var guest = new VersusScope(1, caster == 0 ? Attack(42) : Guard(), caster == 1 ? Attack(42) : Guard()))
                {
                    foreach (var scope in new[] { host, guest })
                    {
                        var match = scope.Controller.Match;
                        Reduce(caster == 0 ? match.Right.State : match.Left.State, 45);
                        scope.Begin();
                        scope.Until(() => scope.Arena.IsResistanceImpactPlaying);
                        Assert.That(match.CurrentSlot.HitsResolved, Is.Zero);
                        Assert.That((caster == 0 ? match.CurrentSlot.LeftFeedback : match.CurrentSlot.RightFeedback)
                            .OpponentResistanceReduced, Is.EqualTo(5));
                        Assert.That(scope.Arena.PlayerBreakAura.IsBroken || scope.Arena.EnemyBreakAura.IsBroken, Is.False);
                        Assert.That(BreakCalls(scope.Controller.BattleHud), Is.Zero);
                        scope.Until(() => scope.Arena.HasResistanceImpactContact);
                        Assert.That(scope.Arena.PlayerBreakAura.IsBroken, Is.EqualTo(scope.Viewer != caster));
                        Assert.That(scope.Arena.EnemyBreakAura.IsBroken, Is.EqualTo(scope.Viewer == caster));
                        Assert.That(BreakCalls(scope.Controller.BattleHud), Is.EqualTo(1));
                        Assert.That(match.CurrentSlot.HitsResolved, Is.Zero);
                    }
                    Assert.That(OnlineVersusProtocol.ComputeStateHash(host.Controller.Match),
                        Is.EqualTo(OnlineVersusProtocol.ComputeStateHash(guest.Controller.Match)));
                    foreach (var scope in new[] { host, guest })
                    {
                        LocalVersusSlot slot = scope.Controller.Match.CurrentSlot;
                        scope.Until(() => slot.HitsResolved == 1);
                        Assert.That((scope.Controller.Match.Left.State.Health, scope.Controller.Match.Right.State.Health), Is.EqualTo((100, 100)));
                        Assert.That((caster == 0 ? scope.Controller.Match.Left : scope.Controller.Match.Right).Act, Is.EqualTo(2));
                    }
                }
        }

        [UnityTest]
        public IEnumerator Versus_BothCastersPartialReductionRemainsSymmetricAndDoesNotDoubleTheirHits()
        {
            yield return null;
            foreach (int viewer in new[] { 0, 1 })
                using (var scope = new VersusScope(viewer, Attack(5), Attack(5)))
                {
                    scope.Begin();
                    scope.Until(() => scope.Arena.IsResistanceImpactPlaying);
                    LocalVersusSlot slot = scope.Controller.Match.CurrentSlot;
                    Assert.That((slot.LeftFeedback.OpponentResistanceReduced, slot.RightFeedback.OpponentResistanceReduced), Is.EqualTo((10, 10)));
                    scope.Until(() => scope.Arena.HasResistanceImpactContact);
                    Assert.That(slot.HitsResolved, Is.Zero);
                    Assert.That(BreakCalls(scope.Controller.BattleHud), Is.Zero);
                    scope.Until(() => slot.HitsResolved == 1);
                    var match = scope.Controller.Match;
                    Assert.That((match.Left.State.Resistance, match.Right.State.Resistance), Is.EqualTo((39, 39)));
                    Assert.That((match.Left.Act, match.Right.Act), Is.EqualTo((2, 2)));
                }
        }

        [UnityTest]
        public IEnumerator DeferredCounter_SettlesOnceBeforeTheEffectBodyCheck_AndItsActualAttackStillWaits()
        {
            yield return null;
            var duel = Duel(Attack(99001), Attack(5), playerResistance: 5,
                playerCounter: new LegacyCounter(Attack(99003)));
            using (var scope = new CampaignScope(duel))
            {
                var c = scope.Controller;
                scope.Begin(0);
                scope.Until(() => c.ArenaView.IsResistanceImpactPlaying);
                LegacyCurrentSlot slot = duel.CurrentSlot;
                Assert.That(slot.PlayerCountered, Is.True);
                Assert.That(slot.PendingPlayerCounter, Is.Null);
                Assert.That(duel.PlayerCountersRemaining, Is.Zero);
                Assert.That(slot.EnemyFeedback.OpponentResistanceReduced, Is.EqualTo(5));
                Assert.That(slot.HitsResolved, Is.Zero, "Preparing deferred effects must not consume the counter's actual attack hit.");
                Assert.That(duel.Player.Health, Is.EqualTo(1000));
                Assert.That(BreakCalls(c.Hud), Is.Zero);
                scope.Until(() => c.ArenaView.HasResistanceImpactContact);
                Assert.That(BreakCalls(c.Hud), Is.EqualTo(1));
                Assert.That(slot.HitsResolved, Is.Zero);
                scope.Until(() => slot.HitsResolved == 1);
                Assert.That(duel.Player.Health, Is.EqualTo(998));
                Assert.That(duel.Act, Is.EqualTo(3), "Counters and their visual body checks both cost no ACT.");
                Assert.That(duel.PlayerCountersRemaining, Is.Zero);
            }
        }

        private static LegacySkill Attack(int id, int power = 1)
            => new LegacySkill(id, "저항 충격 검증", 1, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty);
        private static LegacySkill Guard(int id = 99004)
            => new LegacySkill(id, "충격 방어 검증", 0, 1000, 1000, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, string.Empty);
        private static LegacyQueuedDuel Duel(LegacySkill own, LegacySkill opposing, int playerResistance = 50,
            int enemyResistance = 50, int enemyActions = 1, LegacyCounter playerCounter = null)
            => new LegacyQueuedDuel(1000, playerResistance, 1000, enemyResistance, new[] { own }, new[] { opposing },
                new[] { enemyActions }, 7, playerCounter: playerCounter, meshPercent: 0);
        private static void Reduce(LegacyFighterState fighter, int amount)
            => typeof(LegacyFighterState).GetMethod("ReduceResistance", Private).Invoke(fighter, new object[] { amount });
        private static int BreakCalls(LegacyCombatHud hud)
        {
            int count = 0;
            foreach (Text text in hud.Root.GetComponentsInChildren<Text>(true))
                if (text.name == "State Callout" && text.gameObject.activeInHierarchy && text.text == LegacyCombatHud.BreakCalloutText) count++;
            return count;
        }

        private static void Capture(LegacyArenaView arena, string name)
        {
            string directory = Environment.GetEnvironmentVariable("SWORDMASTER_RESISTANCE_IMPACT_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) return;
            Directory.CreateDirectory(directory);
            Camera camera = arena.ArenaCamera;
            RenderTexture previousTarget = camera.targetTexture, previousActive = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                target.Create(); camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory, "resistance-impact-" + name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                target.Release(); Object.Destroy(target); Object.Destroy(pixels);
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
            public CampaignScope(LegacyQueuedDuel duel)
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled; Controller.enabled = false;
                originalSettings = Controller.PresentationSettings; originalSession = Controller.Session;
                arenaSettings = typeof(LegacyArenaView).GetField("settings", Private);
                originalArenaSettings = (DuelPresentationSettings)arenaSettings.GetValue(Controller.ArenaView);
                settings = Object.Instantiate(originalSettings);
                try
                {
                    JsonUtility.FromJsonOverwrite("{" + Tuning + "}", settings);
                    Set("presentationSettings", settings); arenaSettings.SetValue(Controller.ArenaView, settings);
                    Set("mission", null); Set("guide", null); Set("session", duel);
                    advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                        typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
                    typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", Private)
                        .Invoke(Controller, new object[] { null, null });
                    Controller.ArenaView.SetEnemyAppearance(EnemyAppearance.Elisa);
                    Advance(0f);
                }
                catch { Dispose(); throw; }
            }
            public void Begin(int reservations = 1)
            {
                for (int i = 0; i < reservations; i++) Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
            }
            public void Advance(float seconds) => advance(seconds, null);
            public void Until(Func<bool> done, Action whileWaiting = null)
            {
                for (int frame = 0; frame < 20000 && !done(); frame++) { whileWaiting?.Invoke(); Advance(Frame); }
                Assert.That(done(), Is.True, "The campaign body-check presentation must make progress.");
            }
            private void Set(string name, object value) => typeof(DuelPrototypeController).GetField(name, Private).SetValue(Controller, value);
            public void Dispose()
            {
                if (Controller.IsPaused) Controller.ResumeBattle();
                Set("presentationSettings", originalSettings); arenaSettings.SetValue(Controller.ArenaView, originalArenaSettings);
                Set("session", originalSession); Controller.RestartMatch(); Controller.enabled = originalEnabled;
                Object.Destroy(settings);
            }
        }

        private sealed class VersusScope : IDisposable
        {
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            private readonly GameObject root;
            private readonly LegacyDuelArt art;
            private readonly DuelPresentationSettings settings;
            public int Viewer { get; }
            public LegacyArenaView Arena { get; }
            public LocalVersusController Controller { get; }
            public VersusScope(int viewer, LegacySkill left, LegacySkill right)
            {
                Viewer = viewer;
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                root = new GameObject("Resistance impact versus " + viewer); art = new LegacyDuelArt();
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                JsonUtility.FromJsonOverwrite("{" + Tuning + "}", settings);
                Arena = LegacyArenaView.Create(root.transform, art, settings);
                Controller = new LocalVersusController(root.transform, art, Arena, () => { }, settings);
                Controller.StartOnline(7, viewer, viewer == 0, 0, new[] { left }, new[] { right }, (action, lane) => { }, () => { });
            }
            public void Begin()
            {
                if (Viewer == 0)
                {
                    Assert.That(Controller.TryQueueLane(0), Is.True); Assert.That(Controller.TryCommit(), Is.True);
                    Assert.That(Controller.TryApplyOnlinePeerAction(OnlineVersusAction.QueueLane, 0, 1), Is.True);
                    Assert.That(Controller.TryApplyOnlinePeerAction(OnlineVersusAction.Pass, -1, 1), Is.True);
                }
                else foreach (int owner in new[] { 0, 1 })
                    {
                        Assert.That(Controller.ApplyOnlineAcceptedAction(owner, OnlineVersusAction.QueueLane, 0, 1), Is.True);
                        Assert.That(Controller.ApplyOnlineAcceptedAction(owner, OnlineVersusAction.Pass, -1, 1), Is.True);
                    }
            }
            public void Until(Func<bool> done)
            {
                for (int frame = 0; frame < 20000 && !done(); frame++) Controller.Tick(Frame, null);
                Assert.That(done(), Is.True, "The versus body-check presentation must make progress.");
            }
            public void Dispose()
            {
                Controller.Dispose(); Arena.Dispose(); art.Dispose(); Object.Destroy(root); Object.Destroy(settings);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
