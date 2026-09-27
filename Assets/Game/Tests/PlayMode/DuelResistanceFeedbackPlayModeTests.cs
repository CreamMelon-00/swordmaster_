using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class DuelResistanceFeedbackPlayModeTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Owner;
            public readonly Camera Camera;
            public readonly Transform Player, Enemy;
            public readonly DuelResistanceFeedback Feedback;
            public readonly Text PlayerLabel, EnemyLabel;

            public Fixture()
            {
                Owner = new GameObject("Resistance Feedback Fixture", typeof(RectTransform), typeof(Canvas));
                Owner.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Camera = new GameObject("Resistance Feedback Camera").AddComponent<Camera>();
                Camera.transform.SetParent(Owner.transform, false);
                Camera.transform.position = new Vector3(0f, 0f, -10f);
                Camera.orthographic = true;
                Camera.orthographicSize = 6f;
                Camera.enabled = false;
                Player = new GameObject("Player").transform;
                Enemy = new GameObject("Enemy").transform;
                Player.SetParent(Owner.transform, false);
                Enemy.SetParent(Owner.transform, false);
                Player.position = new Vector3(-2f, 0f, 0f);
                Enemy.position = new Vector3(2f, 0f, 0f);
                Feedback = new DuelResistanceFeedback(Owner.transform, new LegacyDuelArt().UIFont);
                foreach (Text label in Owner.GetComponentsInChildren<Text>(true))
                {
                    if (label.name == "Player Resistance Effect") PlayerLabel = label;
                    if (label.name == "Enemy Resistance Effect") EnemyLabel = label;
                }
                Canvas.ForceUpdateCanvases();
            }

            public void Tick(float realDelta) => Feedback.Tick(realDelta, Camera, Player, Enemy);
            public void Dispose() { Feedback.Dispose(); Object.Destroy(Owner); }
        }

        [UnityTest]
        public IEnumerator ActualCappedSkillChanges_ShowSignedResistanceWithoutDamageFeedback()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                var duel = new LegacyQueuedDuel(100, 50, 100, 50,
                    new[] { Attack(100, 1), Guard(19, 100) },
                    new[] { Attack(900, 2), Guard(901, 100) }, new[] { 2 });
                duel.TryQueueLane(0); duel.TryQueueLane(0); duel.Commit();
                duel.ResolveNextSlot();
                int before = duel.Player.Resistance;
                duel.BeginNextSlot();
                fixture.Feedback.Show(true, duel.Player.Resistance - before);
                fixture.Tick(0f);
                Assert.That(fixture.PlayerLabel.text, Is.EqualTo("저항 +2"), "A capped restoration must not promise the uncapped +5.");
                Assert.That(fixture.PlayerLabel.color, Is.EqualTo(DuelVisualTheme.Health));
                Assert.That(fixture.PlayerLabel.gameObject.activeSelf, Is.True);

                var draw = new LegacyQueuedDuel(100, 50, 100, 15,
                    new[] { Attack(42, 1) }, new[] { Guard(900, 100) }, new[] { 1 });
                draw.TryQueueLane(0); draw.Commit();
                before = draw.Enemy.Resistance;
                draw.BeginNextSlot();
                fixture.Feedback.Show(false, draw.Enemy.Resistance - before);
                fixture.Tick(0f);
                Assert.That(fixture.EnemyLabel.text, Is.EqualTo("저항 -15"), "A direct reduction cannot remove more resistance than remains.");
                Assert.That(fixture.EnemyLabel.color, Is.EqualTo(DuelVisualTheme.Danger));
                Assert.That(fixture.EnemyLabel.gameObject.activeSelf, Is.True);
                Assert.That(draw.Enemy.Health, Is.EqualTo(100));
                Assert.That(fixture.Owner.GetComponentsInChildren<Text>(true).Length, Is.EqualTo(2));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualController_ReportsCappedInitialChangesBeforeHitsWithoutPushOrHitStop()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            bool originalEnabled = controller.enabled;
            LegacyQueuedDuel originalSession = controller.Session;
            controller.enabled = false;
            FieldInfo sessionField = typeof(DuelPrototypeController).GetField("session", PrivateInstance);
            MethodInfo reset = typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance);
            MethodInfo begin = typeof(DuelPrototypeController).GetMethod("BeginSlotAnimation", PrivateInstance);
            MethodInfo advance = typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance);
            try
            {
                var draw = new LegacyQueuedDuel(100, 50, 100, 15,
                    new[] { Attack(42, 1) }, new[] { Guard(900, 100) }, new[] { 1 });
                sessionField.SetValue(controller, draw);
                reset.Invoke(controller, null);
                draw.TryQueueLane(0); draw.Commit();
                Vector3 enemyBefore = controller.ArenaView.EnemyRenderer.transform.position;
                begin.Invoke(controller, null);
                advance.Invoke(controller, new object[] { 0f, null });
                Canvas.ForceUpdateCanvases();
                Text enemyLabel = Label(controller.Hud.Root, "Enemy Resistance Effect");
                Assert.That(enemyLabel.text, Is.EqualTo("저항 -15"));
                Assert.That(enemyLabel.gameObject.activeInHierarchy, Is.True);
                Assert.That(draw.CurrentSlot.HitsResolved, Is.Zero);
                Assert.That(draw.Enemy.Health, Is.EqualTo(100));
                Assert.That(controller.HitStopTimeRemaining, Is.Zero);
                Assert.That(controller.ArenaView.HasPendingPush, Is.False);
                Assert.That(controller.ArenaView.IsFatalFocus, Is.False);
                Assert.That(controller.ArenaView.EnemyRenderer.transform.position, Is.EqualTo(enemyBefore));

                var recovery = new LegacyQueuedDuel(100, 50, 100, 50,
                    new[] { Attack(100, 1), Guard(19, 100) },
                    new[] { Attack(900, 2), Guard(901, 100) }, new[] { 2 });
                sessionField.SetValue(controller, recovery);
                reset.Invoke(controller, null);
                recovery.TryQueueLane(0); recovery.TryQueueLane(0); recovery.Commit();
                recovery.ResolveNextSlot();
                begin.Invoke(controller, null);
                advance.Invoke(controller, new object[] { 0f, null });
                Canvas.ForceUpdateCanvases();
                Text playerLabel = Label(controller.Hud.Root, "Player Resistance Effect");
                Assert.That(playerLabel.text, Is.EqualTo("저항 +2"));
                Assert.That(playerLabel.gameObject.activeInHierarchy, Is.True);
                Assert.That(recovery.Player.Resistance, Is.EqualTo(50));
                Assert.That(recovery.CurrentSlot.HitsResolved, Is.Zero);
                Assert.That(controller.HitStopTimeRemaining, Is.Zero);
                Assert.That(controller.ArenaView.HasPendingPush, Is.False);
            }
            finally
            {
                sessionField.SetValue(controller, originalSession);
                controller.RestartMatch();
                controller.enabled = originalEnabled;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ManualRealClock_ExpiresWhileGameTimeIsPausedAndResetClearsBothSides()
        {
            yield return null;
            float originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                using (var fixture = new Fixture())
                {
                    fixture.Feedback.Show(true, 5);
                    fixture.Feedback.Show(false, -20);
                    fixture.Tick(0f);
                    Assert.That(fixture.PlayerLabel.color.a, Is.EqualTo(1f));
                    fixture.Tick(.35f);
                    Assert.That(fixture.PlayerLabel.color.a, Is.EqualTo(1f));
                    fixture.Tick(.4f);
                    Assert.That(fixture.PlayerLabel.color.a, Is.InRange(.01f, .99f));
                    fixture.Tick(.3f);
                    Assert.That(fixture.PlayerLabel.gameObject.activeSelf, Is.False);
                    Assert.That(fixture.EnemyLabel.gameObject.activeSelf, Is.False);
                    fixture.Feedback.Show(true, 5);
                    fixture.Feedback.Show(false, -20);
                    fixture.Feedback.Reset();
                    fixture.Feedback.Show(true, 0);
                    fixture.Tick(0f);
                    Assert.That(fixture.PlayerLabel.text, Is.Empty);
                    Assert.That(fixture.EnemyLabel.text, Is.Empty);
                    Assert.That(fixture.PlayerLabel.gameObject.activeSelf, Is.False);
                    Assert.That(fixture.EnemyLabel.gameObject.activeSelf, Is.False);
                }
            }
            finally { Time.timeScale = originalTimeScale; }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedFeedback_ReusesTwoLabelsTracksActorsAndStaysOnScreenWithoutBlockingInput()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                int count = fixture.Owner.GetComponentsInChildren<Transform>(true).Length;
                fixture.Feedback.Show(true, 5);
                fixture.Tick(0f);
                Vector2 initial = fixture.PlayerLabel.rectTransform.anchoredPosition;
                fixture.Player.position += Vector3.right;
                fixture.Tick(0f);
                Assert.That(fixture.PlayerLabel.rectTransform.anchoredPosition.x, Is.GreaterThan(initial.x));
                fixture.Camera.transform.position += Vector3.right;
                fixture.Tick(0f);
                Assert.That(fixture.PlayerLabel.rectTransform.anchoredPosition.x, Is.EqualTo(initial.x).Within(.1f));
                fixture.Player.position = new Vector3(-100f, 100f, 0f);
                fixture.Enemy.position = new Vector3(100f, -100f, 0f);
                for (int i = 0; i < 40; i++)
                {
                    fixture.Feedback.Show(true, i + 1);
                    fixture.Feedback.Show(false, -i - 1);
                    fixture.Tick(.01f);
                }
                foreach (Text label in new[] { fixture.PlayerLabel, fixture.EnemyLabel })
                {
                    Assert.That(label.raycastTarget, Is.False);
                    var corners = new Vector3[4];
                    label.rectTransform.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corner);
                        Assert.That(screen.x, Is.InRange(-1f, Screen.width + 1f));
                        Assert.That(screen.y, Is.InRange(-1f, Screen.height + 1f));
                    }
                }
                Assert.That(fixture.Owner.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                CollectionAssert.AreEquivalent(new[] { fixture.PlayerLabel, fixture.EnemyLabel },
                    fixture.Owner.GetComponentsInChildren<Text>(true));
            }
            yield return null;
        }

        private static LegacySkill Attack(int id, int power)
            => new LegacySkill(id, "attack", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "");

        private static LegacySkill Guard(int id, int power)
            => new LegacySkill(id, "guard", 0, power, power, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "");

        private static Text Label(GameObject root, string name)
        {
            foreach (Text label in root.GetComponentsInChildren<Text>(true)) if (label.name == name) return label;
            Assert.Fail("Missing resistance feedback label " + name);
            return null;
        }
    }
}
