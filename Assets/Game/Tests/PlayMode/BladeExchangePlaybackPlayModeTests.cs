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
    public sealed class BladeExchangePlaybackPlayModeTests : InputTestFixture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float Frame = .005f;
        private const string Tuning = "\"bladeExchangeEnabled\":true,\"bladeExchangeMinSeconds\":0.08," +
            "\"bladeExchangeMaxSeconds\":0.14,\"showDirectorEnabled\":false,\"combatTempoEnabled\":false," +
            "\"unopposedStreakEnabled\":false,\"animationPlaybackSpeed\":1,\"attackInterval\":0.1," +
            "\"skillInterval\":0.04,\"stepAnticipationDuration\":0.08,\"stepTimingWindow\":0.05," +
            "\"hitStopDuration\":0,\"autoDepthEnabled\":false,\"attackRhythmEnabled\":false";

        [UnityTest]
        public IEnumerator HarmlessContacts_HoldDamageAndSteps_ThenTheRealMultihitRunsExactlyOnce()
        {
            yield return null;
            LegacyQueuedDuel expected = Duel();
            expected.TryQueueLane(0);
            expected.Commit();
            expected.BeginNextSlot();
            expected.ResolveNextHit();
            var first = State(expected);
            while (!expected.IsCurrentSlotResolved) expected.ResolveNextHit();
            var all = State(expected);
            using (var scope = new CampaignScope(Duel()))
            {
                DuelPrototypeController controller = scope.Controller;
                scope.BeginExchange();
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                var before = State(controller.Session);
                Vector3 player = controller.ArenaView.PlayerRenderer.transform.localPosition;
                Vector3 enemy = controller.ArenaView.EnemyRenderer.transform.localPosition;
                int act = controller.Session.Act;
                Assert.That(controller.BladeExchangeContactCount, Is.InRange(1, 2));
                Assert.That(controller.BladeExchangeDuration, Is.InRange(.08f, .28f));
                for (int frame = 0; frame < 1000 && controller.IsPlayingBladeExchange; frame++)
                {
                    Assert.That(controller.CanStep, Is.False);
                    Assert.That(controller.IsStepTimingWindow, Is.False);
                    Assert.That(controller.TryStep(LegacyStepAction.Dodge, out _), Is.False);
                    Assert.That(slot.HitsResolved, Is.Zero);
                    Assert.That(State(controller.Session), Is.EqualTo(before));
                    Assert.That(controller.Session.Act, Is.EqualTo(act));
                    Assert.That(DamageNumbers(controller.Hud), Is.Zero);
                    Assert.That(Vector3.Distance(controller.ArenaView.PlayerRenderer.transform.localPosition, player), Is.LessThan(.0001f));
                    Assert.That(Vector3.Distance(controller.ArenaView.EnemyRenderer.transform.localPosition, enemy), Is.LessThan(.0001f));
                    Assert.That(controller.ArenaView.IsBladeBinding, Is.False);
                    scope.Advance(Frame);
                }
                Assert.That(controller.IsPlayingBladeExchange, Is.False);
                Assert.That(controller.BladeExchangeContactsPresented, Is.EqualTo(controller.BladeExchangeContactCount));
                Assert.That(slot.HitsResolved, Is.EqualTo(1));
                Assert.That(State(controller.Session), Is.EqualTo(first));
                Assert.That(DamageNumbers(controller.Hud), Is.EqualTo(2));
                for (int frame = 0; frame < 4; frame++) scope.Advance(0f);
                Assert.That(slot.HitsResolved, Is.EqualTo(1));
                scope.Until(() => slot.HitsResolved == 3);
                Assert.That(State(controller.Session), Is.EqualTo(all), "Visual contacts must not consume rolls or change actual hits.");
                Assert.That(controller.BladeExchangeContactsPresented, Is.EqualTo(controller.BladeExchangeContactCount));
            }
        }

        [UnityTest]
        public IEnumerator LongFrames_StillShowEveryContact_AndCannotBatchTheRealHits()
        {
            yield return null;
            using (var scope = new CampaignScope(Duel()))
            {
                DuelPrototypeController controller = scope.Controller;
                scope.BeginExchange();
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                int count = controller.BladeExchangeContactCount;
                for (int contact = 1; contact <= count; contact++)
                {
                    scope.Advance(2f);
                    Assert.That(controller.BladeExchangeContactsPresented, Is.EqualTo(contact));
                    Assert.That(slot.HitsResolved, Is.Zero);
                    Assert.That(DamageNumbers(controller.Hud), Is.Zero);
                }
                scope.Advance(2f);
                Assert.That(slot.HitsResolved, Is.Zero, "The exchange's release frame belongs to the harmless pose.");
                scope.Advance(2f);
                Assert.That(slot.HitsResolved, Is.EqualTo(1));
                scope.Advance(0f);
                Assert.That(slot.HitsResolved, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator PauseKeepsTheExchange_AndRestartDiscardsItWithoutDamage()
        {
            yield return null;
            using (var scope = new CampaignScope(Duel()))
            {
                DuelPrototypeController controller = scope.Controller;
                scope.BeginExchange();
                LegacyQueuedDuel held = controller.Session;
                var before = State(held);
                int contacts = controller.BladeExchangeContactsPresented;
                Assert.That(controller.PauseBattle(), Is.True);
                scope.Advance(10f);
                Assert.That(controller.IsPlayingBladeExchange, Is.True);
                Assert.That(controller.BladeExchangeContactsPresented, Is.EqualTo(contacts));
                Assert.That(State(held), Is.EqualTo(before));
                Assert.That(controller.ResumeBattle(), Is.True);
                controller.RestartMatch();
                Assert.That(controller.IsPlayingBladeExchange, Is.False);
                scope.Advance(.1f);
                Assert.That(held.CurrentSlot.HitsResolved, Is.Zero);
                Assert.That(State(held), Is.EqualTo(before));
                Assert.That(controller.Session, Is.Not.SameAs(held));
            }
        }

        [UnityTest]
        public IEnumerator SwitchOffAndSuccessfulDodge_SkipTheExtraContact()
        {
            yield return null;
            using (var scope = new CampaignScope(Duel(), "\"bladeExchangeEnabled\":false"))
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot?.HitsResolved > 0);
                Assert.That(controller.BladeExchangeContactCount, Is.Zero);
            }
            using (var scope = new CampaignScope(Duel()))
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.IsStepTimingWindow);
                Assert.That(controller.TryStep(LegacyStepAction.Dodge, out bool success), Is.True);
                Assert.That(success, Is.True);
                scope.Until(() => controller.Session.CurrentSlot?.HitsResolved > 0);
                Assert.That(controller.BladeExchangeContactCount, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator HostAndMirroredGuest_PlayTheSameContactsBeforeTheSameDamage()
        {
            yield return null;
            using (var host = new VersusScope(0, true))
            using (var guest = new VersusScope(1, false))
            {
                Assert.That(host.Controller.TryQueueLane(0), Is.True);
                Assert.That(guest.Controller.ApplyOnlineAcceptedAction(0, OnlineVersusAction.QueueLane, 0, 1), Is.True);
                Assert.That(host.Controller.TryCommit(), Is.True);
                Assert.That(guest.Controller.ApplyOnlineAcceptedAction(0, OnlineVersusAction.Pass, -1, 1), Is.True);
                Assert.That(host.Controller.TryApplyOnlinePeerAction(OnlineVersusAction.QueueLane, 0, 1), Is.True);
                Assert.That(guest.Controller.ApplyOnlineAcceptedAction(1, OnlineVersusAction.QueueLane, 0, 1), Is.True);
                Assert.That(host.Controller.TryApplyOnlinePeerAction(OnlineVersusAction.Pass, -1, 1), Is.True);
                Assert.That(guest.Controller.ApplyOnlineAcceptedAction(1, OnlineVersusAction.Pass, -1, 1), Is.True);
                UntilBoth(host, guest, () => host.Controller.IsPlayingBladeExchange && guest.Controller.IsPlayingBladeExchange);
                var initial = State(host.Controller.Match);
                Assert.That(host.Controller.BladeExchangeContactCount, Is.EqualTo(guest.Controller.BladeExchangeContactCount));
                Assert.That(host.Controller.BladeExchangeDuration, Is.EqualTo(guest.Controller.BladeExchangeDuration));
                int contacts = host.Controller.BladeExchangeContactCount;
                for (int contact = 1; contact <= contacts; contact++)
                {
                    host.Controller.Tick(2f, null);
                    guest.Controller.Tick(2f, null);
                    Assert.That(host.Controller.BladeExchangeContactsPresented, Is.EqualTo(contact));
                    Assert.That(guest.Controller.BladeExchangeContactsPresented, Is.EqualTo(contact));
                    Assert.That(State(host.Controller.Match), Is.EqualTo(initial));
                    Assert.That(State(guest.Controller.Match), Is.EqualTo(initial));
                    Assert.That(host.Controller.Match.CurrentSlot.HitsResolved + guest.Controller.Match.CurrentSlot.HitsResolved, Is.Zero);
                }
                host.Controller.Tick(2f, null);
                guest.Controller.Tick(2f, null);
                Assert.That(host.Controller.Match.CurrentSlot.HitsResolved + guest.Controller.Match.CurrentSlot.HitsResolved, Is.Zero);
                host.Controller.Tick(.005f, null);
                guest.Controller.Tick(.005f, null);
                Assert.That(host.Controller.Match.CurrentSlot.HitsResolved, Is.EqualTo(1));
                Assert.That(guest.Controller.Match.CurrentSlot.HitsResolved, Is.EqualTo(1));
                Assert.That(State(host.Controller.Match), Is.EqualTo(State(guest.Controller.Match)));
                Assert.That(DamageNumbers(host.Controller.BattleHud), Is.EqualTo(2));
                Assert.That(DamageNumbers(guest.Controller.BattleHud), Is.EqualTo(2));
                UntilBoth(host, guest, () => host.Controller.Match.RoundNumber == 2 && guest.Controller.Match.RoundNumber == 2);
                Assert.That(State(host.Controller.Match), Is.EqualTo(State(guest.Controller.Match)));
                Assert.That(host.Controller.IsPlayingBladeExchange || guest.Controller.IsPlayingBladeExchange, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator SettingsDefaultOn_AndInvalidIntervalsCannotCreateAnUnboundedHold()
        {
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            try
            {
                Assert.That(settings.BladeExchangeEnabled, Is.True);
                typeof(DuelPresentationSettings).GetField("bladeExchangeMinSeconds", Private).SetValue(settings, float.NaN);
                typeof(DuelPresentationSettings).GetField("bladeExchangeMaxSeconds", Private).SetValue(settings, -.5f);
                Assert.That(settings.BladeExchangeMinSeconds, Is.EqualTo(LegacyBladeExchange.DefaultMinSeconds));
                Assert.That(settings.BladeExchangeMaxSeconds, Is.GreaterThanOrEqualTo(settings.BladeExchangeMinSeconds));
                Assert.That(settings.BladeExchangeMaxSeconds, Is.LessThanOrEqualTo(.25f));
                Assert.That(settings.BladeExchangeStrength, Is.LessThan(1f));
                Assert.That(settings.BladeExchangeVolume, Is.LessThan(.4f));
            }
            finally { Object.Destroy(settings); }
            yield return null;
        }

        private static LegacySkill Attack(int id, int power) => new LegacySkill(id, "검 맞대기 검증", 0, power, power,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, 3, 0, string.Empty);
        private static LegacyQueuedDuel Duel() => new LegacyQueuedDuel(1000, 1000, 1000, 1000,
            new[] { Attack(88401, 30) }, new[] { Attack(88402, 27) }, new[] { 1 }, 7,
            features: CombatFeature.LaneQ | CombatFeature.Dodge | CombatFeature.Pressure, meshPercent: 0);
        private static (int, int, int, int) State(LegacyQueuedDuel duel)
            => (duel.Player.Health, duel.Enemy.Health, duel.Player.Resistance, duel.Enemy.Resistance);
        private static (int, int, int, int) State(LocalVersusMatch match)
            => (match.Left.State.Health, match.Right.State.Health, match.Left.State.Resistance, match.Right.State.Resistance);
        private static int DamageNumbers(LegacyCombatHud hud)
        {
            int count = 0;
            foreach (Text text in hud.Root.GetComponentsInChildren<Text>(true))
                if (text.name == "Damage" && text.gameObject.activeInHierarchy) count++;
            return count;
        }

        private sealed class CampaignScope : IDisposable
        {
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings, settings;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originalEnabled;
            private readonly FieldInfo arenaSettings;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public CampaignScope(LegacyQueuedDuel duel, string extra = null)
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
                JsonUtility.FromJsonOverwrite("{" + Tuning + "}", settings);
                if (extra != null) JsonUtility.FromJsonOverwrite("{" + extra + "}", settings);
                Set("presentationSettings", settings);
                arenaSettings.SetValue(Controller.ArenaView, settings);
                Set("mission", null);
                Set("guide", null);
                Set("session", duel);
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", Private).Invoke(Controller, new object[] { null, null });
            }

            public void Advance(float delta) => advance(delta, null);
            public void BeginExchange()
            {
                Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
                Until(() => Controller.IsPlayingBladeExchange);
            }
            public void Until(Func<bool> condition)
            {
                for (int frame = 0; frame < 20000 && !condition(); frame++) Advance(Frame);
                Assert.That(condition(), Is.True, "The normal presentation must make progress.");
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

        private static void UntilBoth(VersusScope host, VersusScope guest, Func<bool> condition)
        {
            for (int frame = 0; frame < 20000 && !condition(); frame++)
            {
                host.Controller.Tick(Frame, null);
                guest.Controller.Tick(Frame, null);
            }
            Assert.That(condition(), Is.True);
        }

        private sealed class VersusScope : IDisposable
        {
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            private readonly GameObject root;
            private readonly LegacyDuelArt art;
            private readonly DuelPresentationSettings settings;
            private readonly LegacyArenaView arena;
            public LocalVersusController Controller { get; }

            public VersusScope(int localPlayer, bool host)
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                root = new GameObject("Blade Exchange Versus " + localPlayer);
                art = new LegacyDuelArt();
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                JsonUtility.FromJsonOverwrite("{" + Tuning + "}", settings);
                arena = LegacyArenaView.Create(root.transform, art, settings);
                Controller = new LocalVersusController(root.transform, art, arena, () => { }, settings);
                Controller.StartOnline(7, localPlayer, host, 0, new[] { Attack(88401, 30) }, new[] { Attack(88402, 27) },
                    (action, lane) => { }, () => { });
            }
            public void Dispose()
            {
                Controller.Dispose();
                arena.Dispose();
                art.Dispose();
                Object.Destroy(root);
                Object.Destroy(settings);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
