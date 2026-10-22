using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class HurtReactionControllerPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float Frame = .002f;
        private const int MaximumFrames = 20000;
        private const string Tuning = "\"animationPlaybackSpeed\":1,\"attackInterval\":0.1,\"skillInterval\":0.1," +
            "\"hitStopDuration\":0.02,\"showDirectorEnabled\":false,\"combatTempoEnabled\":false," +
            "\"unopposedStreakEnabled\":false,\"bladeExchangeEnabled\":false,\"autoDepthEnabled\":false," +
            "\"attackRhythmEnabled\":false";

        [UnityTest]
        public IEnumerator Campaign_OnlyTheSideDealingAtMostHalfTheResistanceDamageFlinches()
        {
            yield return null;
            foreach (var powers in new[] { (4, 4), (4, 7), (4, 8), (8, 4) })
                using (var scope = new CampaignScope(Duel(powers.Item1, powers.Item2)))
                {
                    scope.Begin();
                    scope.UntilHit(1);
                    var duel = scope.Controller.Session;
                    Assert.That((duel.Player.Health, duel.Enemy.Health), Is.EqualTo((1000, 1000)));
                    Assert.That((duel.Player.Resistance, duel.Enemy.Resistance),
                        Is.EqualTo((1000 - powers.Item2, 1000 - powers.Item1)));
                    AssertHurt(scope.Controller.ArenaView, powers.Item1 * 2 <= powers.Item2,
                        powers.Item2 * 2 <= powers.Item1);
                }
        }

        [UnityTest]
        public IEnumerator Campaign_LaterHitsAgainstAnExhaustedAttackAreUnopposed_OnEitherSide()
        {
            yield return null;
            foreach (bool playerHasTwoHits in new[] { true, false })
                using (var scope = new CampaignScope(Duel(playerHasTwoHits ? 8 : 4,
                    playerHasTwoHits ? 4 : 8, playerHasTwoHits ? 2 : 1, playerHasTwoHits ? 1 : 2)))
                {
                    scope.Begin();
                    scope.UntilHit(1);
                    AssertHurt(scope.Controller.ArenaView, false, false);
                    scope.UntilHit(2);
                    AssertHurt(scope.Controller.ArenaView, !playerHasTwoHits, playerHasTwoHits);
                    var duel = scope.Controller.Session;
                    Assert.That((duel.Player.Resistance, duel.Enemy.Resistance),
                        Is.EqualTo(playerHasTwoHits ? (996, 992) : (992, 996)));
                }
        }

        [UnityTest]
        public IEnumerator Campaign_AlreadyBrokenFightersOnlyFlinchWhenTheIncomingAttackActuallyLands()
        {
            yield return null;
            using (var scope = new CampaignScope(Duel(2, 2, resistance: 0)))
            {
                scope.Advance(.05f);
                Assert.That(scope.Controller.Session.Player.IsResistanceBroken, Is.True);
                Assert.That(scope.Controller.Session.Enemy.IsResistanceBroken, Is.True);
                AssertHurt(scope.Controller.ArenaView, false, false);
                scope.Begin();
                scope.UntilHit(1);
                Assert.That((scope.Controller.PlayerHealth, scope.Controller.EnemyHealth), Is.EqualTo((996, 996)),
                    "Equal direct damage cannot meet the disparity rule; both react because each was hit while broken.");
                AssertHurt(scope.Controller.ArenaView, true, true);
            }
        }

        [UnityTest]
        public IEnumerator Campaign_SelfBleedingCannotMakeAnEvenExchangeLookTwiceAsStrong()
        {
            yield return null;
            foreach (bool playerBleeds in new[] { true, false })
                using (var scope = new CampaignScope(Duel(2, 2)))
                {
                    var duel = scope.Controller.Session;
                    var statuses = playerBleeds
                        ? (LegacyStatusCollection)typeof(LegacyQueuedDuel).GetField("playerStatuses", Private).GetValue(duel)
                        : (LegacyStatusCollection)typeof(LegacyQueuedDuel).GetProperty("enemyStatuses", Private).GetValue(duel);
                    statuses.GrantBleeding(1);
                    scope.Begin();
                    scope.UntilHit(1);
                    Assert.That((duel.Player.Health, duel.Enemy.Health), Is.EqualTo(playerBleeds ? (998, 1000) : (1000, 998)));
                    Assert.That((duel.Player.Resistance, duel.Enemy.Resistance), Is.EqualTo((998, 998)));
                    AssertHurt(scope.Controller.ArenaView, false, false,
                        "The two points of self bleeding do not belong to the opposing attack's two resistance damage.");
                }
        }

        [UnityTest]
        public IEnumerator HostAndMirroredGuest_SelectTheSameWeakerFighterFromActualResistanceDamage()
        {
            yield return null;
            foreach (var powers in new[] { (4, 4), (4, 7), (4, 8), (8, 4) })
                using (var host = new VersusScope(0, powers.Item1, powers.Item2))
                using (var guest = new VersusScope(1, powers.Item1, powers.Item2))
                {
                    host.Begin();
                    guest.Begin();
                    host.UntilHit(1);
                    guest.UntilHit(1);
                    bool leftHurt = powers.Item1 * 2 <= powers.Item2;
                    bool rightHurt = powers.Item2 * 2 <= powers.Item1;
                    AssertHurt(host.Arena, leftHurt, rightHurt);
                    AssertHurt(guest.Arena, rightHurt, leftHurt, "The guest's own fighter is the authoritative right fighter.");
                    Assert.That((host.Controller.Match.Left.State.Resistance, host.Controller.Match.Right.State.Resistance),
                        Is.EqualTo((50 - powers.Item2, 50 - powers.Item1)));
                    Assert.That((guest.Controller.Match.Left.State.Resistance, guest.Controller.Match.Right.State.Resistance),
                        Is.EqualTo((50 - powers.Item2, 50 - powers.Item1)));
                }
        }

        [UnityTest]
        public IEnumerator Versus_LaterUnopposedHitsFlinchTheFinishedAttack_FromEitherView()
        {
            yield return null;
            foreach (int viewer in new[] { 0, 1 })
                foreach (bool leftHasTwoHits in new[] { true, false })
                    using (var scope = new VersusScope(viewer, leftHasTwoHits ? 8 : 4, leftHasTwoHits ? 4 : 8,
                        leftHasTwoHits ? 2 : 1, leftHasTwoHits ? 1 : 2))
                    {
                        scope.Begin();
                        scope.UntilHit(1);
                        AssertHurt(scope.Arena, false, false);
                        scope.UntilHit(2);
                        bool ownHurt = viewer == 0 ? !leftHasTwoHits : leftHasTwoHits;
                        AssertHurt(scope.Arena, ownHurt, !ownHurt);
                    }
        }

        [UnityTest]
        public IEnumerator Versus_BrokenStateDoesNotFlinchAtRest_ButBothEqualIncomingHitsDo()
        {
            yield return null;
            foreach (int viewer in new[] { 0, 1 })
                using (var scope = new VersusScope(viewer, 2, 2))
                {
                    var match = scope.Controller.Match;
                    foreach (var fighter in new[] { match.Left.State, match.Right.State })
                        typeof(LegacyFighterState).GetMethod("ReduceResistance", Private)
                            .Invoke(fighter, new object[] { 50 });
                    scope.Controller.Tick(.05f, null);
                    AssertHurt(scope.Arena, false, false);
                    scope.Begin();
                    scope.UntilHit(1);
                    Assert.That((match.Left.State.Health, match.Right.State.Health), Is.EqualTo((96, 96)));
                    AssertHurt(scope.Arena, true, true);
                }
        }

        [UnityTest]
        public IEnumerator Versus_SelfBleedingIsExcludedForBothOwnersAndBothViews()
        {
            yield return null;
            foreach (int viewer in new[] { 0, 1 })
                foreach (int bleedingOwner in new[] { 0, 1 })
                    using (var scope = new VersusScope(viewer, 2, 2))
                    {
                        var match = scope.Controller.Match;
                        var fighter = bleedingOwner == 0 ? match.Left : match.Right;
                        ((LegacyStatusCollection)typeof(LocalVersusFighter).GetField("StatusEffects", Private)
                            .GetValue(fighter)).GrantBleeding(1);
                        scope.Begin();
                        scope.UntilHit(1);
                        Assert.That((match.Left.State.Health, match.Right.State.Health),
                            Is.EqualTo(bleedingOwner == 0 ? (98, 100) : (100, 98)));
                        Assert.That((match.Left.State.Resistance, match.Right.State.Resistance), Is.EqualTo((48, 48)));
                        AssertHurt(scope.Arena, false, false);
                    }
        }

        private static void AssertHurt(LegacyArenaView arena, bool own, bool opposing, string message = null)
        {
            Assert.That(arena.PlayerRenderer.sprite.name.Contains("hurt"), Is.EqualTo(own), "Own pose: " + message);
            Assert.That(arena.EnemyRenderer.sprite.name.Contains("hurt"), Is.EqualTo(opposing), "Opposing pose: " + message);
        }

        private static LegacySkill Attack(int id, int power, int hits)
            => new LegacySkill(id, "피격 판정 검증", 0, power, power, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, hits, 0, string.Empty);

        private static LegacyQueuedDuel Duel(int playerPower, int enemyPower, int playerHits = 1, int enemyHits = 1,
            int resistance = 1000)
            => new LegacyQueuedDuel(1000, resistance, 1000, resistance, new[] { Attack(98901, playerPower, playerHits) },
                new[] { Attack(98902, enemyPower, enemyHits) }, new[] { 1 }, 7, meshPercent: 0);

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
                    Set("presentationSettings", settings);
                    arenaSettings.SetValue(Controller.ArenaView, settings);
                    Set("mission", null);
                    Set("guide", null);
                    Set("session", duel);
                    advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                        typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
                    typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", Private)
                        .Invoke(Controller, new object[] { null, null });
                    Controller.ArenaView.SetEnemyAppearance(EnemyAppearance.Elisa);
                    Advance(0f);
                    Assert.That(Controller.CanChoose, Is.True);
                }
                catch { Dispose(); throw; }
            }

            public void Begin()
            {
                Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
            }
            public void Advance(float seconds) => advance(seconds, null);
            public void UntilHit(int count)
            {
                for (int frame = 0; frame < MaximumFrames && !(Controller.Session.CurrentSlot?.HitsResolved >= count); frame++)
                    Advance(Frame);
                Assert.That(Controller.Session.CurrentSlot?.HitsResolved, Is.EqualTo(count), "The expected hit must be presented.");
            }
            private void Set(string name, object value) => typeof(DuelPrototypeController).GetField(name, Private).SetValue(Controller, value);
            public void Dispose()
            {
                if (Controller.IsPaused) Controller.ResumeBattle();
                Set("presentationSettings", originalSettings);
                arenaSettings.SetValue(Controller.ArenaView, originalArenaSettings);
                Set("session", originalSession);
                Controller.RestartMatch();
                Controller.enabled = originalEnabled;
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
            private readonly int viewer;
            public LegacyArenaView Arena { get; }
            public LocalVersusController Controller { get; }

            public VersusScope(int viewer, int leftPower, int rightPower, int leftHits = 1, int rightHits = 1)
            {
                this.viewer = viewer;
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                root = new GameObject("Hurt reaction versus " + viewer);
                art = new LegacyDuelArt();
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                JsonUtility.FromJsonOverwrite("{" + Tuning + "}", settings);
                Arena = LegacyArenaView.Create(root.transform, art, settings);
                Controller = new LocalVersusController(root.transform, art, Arena, () => { }, settings);
                Controller.StartOnline(7, viewer, viewer == 0, 0, new[] { Attack(98911, leftPower, leftHits) },
                    new[] { Attack(98912, rightPower, rightHits) }, (action, lane) => { }, () => { });
            }

            public void Begin()
            {
                if (viewer == 0)
                {
                    Assert.That(Controller.TryQueueLane(0), Is.True);
                    Assert.That(Controller.TryCommit(), Is.True);
                    Assert.That(Controller.TryApplyOnlinePeerAction(OnlineVersusAction.QueueLane, 0, 1), Is.True);
                    Assert.That(Controller.TryApplyOnlinePeerAction(OnlineVersusAction.Pass, -1, 1), Is.True);
                }
                else
                    foreach (int owner in new[] { 0, 1 })
                    {
                        Assert.That(Controller.ApplyOnlineAcceptedAction(owner, OnlineVersusAction.QueueLane, 0, 1), Is.True);
                        Assert.That(Controller.ApplyOnlineAcceptedAction(owner, OnlineVersusAction.Pass, -1, 1), Is.True);
                    }
            }

            public void UntilHit(int count)
            {
                for (int frame = 0; frame < MaximumFrames && !(Controller.Match.CurrentSlot?.HitsResolved >= count); frame++)
                    Controller.Tick(Frame, null);
                Assert.That(Controller.Match.CurrentSlot?.HitsResolved, Is.EqualTo(count), "The expected versus hit must be presented.");
            }

            public void Dispose()
            {
                Controller.Dispose();
                Arena.Dispose();
                art.Dispose();
                Object.Destroy(root);
                Object.Destroy(settings);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
