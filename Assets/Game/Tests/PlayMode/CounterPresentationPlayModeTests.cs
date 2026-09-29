using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class CounterPresentationPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator EnemyCounter_MarksTheAnsweredCardSummarizesUsesAndCallsOut()
        {
            yield return null;
            using (var scope = new CounterScope(1, null, new LegacyCounter(Guard(202, 3))))
            {
                var controller = scope.Controller;
                Transform root = controller.Hud.Root.transform;
                Assert.That(controller.QueueLane(0), Is.True);
                Assert.That(controller.QueueLane(0), Is.True);
                scope.Advance(.01f);
                Assert.That(CounterMark(root, "Player Requests", 0).activeSelf, Is.False,
                    "The first attack meets the enemy's queued action.");
                Assert.That(CounterMark(root, "Player Requests", 1).activeSelf, Is.True);
                Assert.That(StatusCounter(root, false), Is.EqualTo("반격 · Counter Guard 1/1"));
                Assert.That(StatusCounter(root, true), Is.Empty);
                Assert.That(ActiveCallouts(root), Is.Zero);

                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot?.SlotIndex == 1);
                Assert.That(controller.Session.CurrentSlot.EnemyCountered, Is.True);
                scope.Advance(0f);
                Assert.That(ActiveCallouts(root), Is.EqualTo(1));
                Assert.That(StatusCounter(root, false), Is.EqualTo("반격 · Counter Guard 0/1"));
                Assert.That(DamageNumbersReading(root, "반격"), Is.Zero, "A callout never borrows a damage number.");
            }
        }

        [UnityTest]
        public IEnumerator PlayerCounter_DodgeAttemptWithdrawsItAndShortensTheSlot()
        {
            yield return null;
            using (var scope = new CounterScope(2, new LegacyCounter(Attack(210, 3, 3)), null))
            {
                var controller = scope.Controller;
                Transform root = controller.Hud.Root.transform;
                Assert.That(controller.QueueLane(0), Is.True);
                scope.Advance(.01f);
                Assert.That(CounterMark(root, "Enemy Requests", 1).activeSelf, Is.True,
                    "The enemy's extra attack will meet the player's counter.");

                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot?.SlotIndex == 1);
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                Assert.That(slot.PendingPlayerCounter, Is.Not.Null);
                Assert.That(controller.IsSkillWindup, Is.True);
                float withCounter = controller.ActiveSlotDuration;
                scope.Advance(0f);
                Assert.That(CounterMark(root, "Enemy Requests", 1).activeSelf, Is.True, "The pending counter keeps its mark.");

                Assert.That(controller.TryStep(LegacyStepAction.Dodge, out _), Is.True);
                Assert.That(slot.PendingPlayerCounter, Is.Null);
                Assert.That(controller.ActiveSlotDuration, Is.LessThan(withCounter),
                    "The withdrawn counter's extra hits no longer lengthen the slot.");
                scope.Until(() => controller.Session.CurrentSlot != slot);
                Assert.That(slot.PlayerCountered, Is.False);
                Assert.That(ActiveCallouts(root), Is.Zero);
                Assert.That(controller.Session.PlayerCountersRemaining, Is.EqualTo(1), "Evading keeps the use.");
            }
        }

        [UnityTest]
        public IEnumerator PlayerCounter_SettlesAtItsFirstHitWithACallout()
        {
            yield return null;
            using (var scope = new CounterScope(2, new LegacyCounter(Attack(210, 3, 3)), null))
            {
                var controller = scope.Controller;
                Transform root = controller.Hud.Root.transform;
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot?.SlotIndex == 1);
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                Assert.That(ActiveCallouts(root), Is.Zero, "Nothing is called out before the counter strikes.");
                scope.Until(() => slot.HitsResolved >= 1);
                Assert.That(slot.PlayerCountered, Is.True);
                Assert.That(slot.PlayerSkill.Id, Is.EqualTo(210));
                Assert.That(ActiveCallouts(root), Is.EqualTo(1));
                Assert.That(controller.Session.PlayerCountersRemaining, Is.Zero);
            }
        }

        private static LegacySkill Attack(int id, int power, int hits) =>
            new LegacySkill(id, "Counter Attack", 1, power, power, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, hits, 0, string.Empty, "Slash");

        private static LegacySkill Guard(int id, int power) =>
            new LegacySkill(id, "Counter Guard", 1, power, power, LegacySkillKind.Defence,
                LegacySkillProperty.Defence, 1, 0, string.Empty);

        private static GameObject CounterMark(Transform root, string queue, int index)
        {
            Transform cards = root.Find(queue);
            Assert.That(cards, Is.Not.Null);
            foreach (Transform card in cards)
            {
                if (card.name != "Queued Skill" || index-- != 0) continue;
                Transform mark = card.Find("Counter Mark");
                Assert.That(mark, Is.Not.Null);
                return mark.gameObject;
            }
            Assert.Fail("Queue card not created: " + queue);
            return null;
        }

        private static string StatusCounter(Transform root, bool player) =>
            root.Find((player ? "PlayerStatus" : "EnemyStatus") + "/Counter").GetComponent<Text>().text;

        private static int ActiveCallouts(Transform root) => CountActive(root, "Counter Callout");

        private static int DamageNumbersReading(Transform root, string text)
        {
            int count = 0;
            foreach (Transform child in root)
                if (child.name == "Damage" && child.GetComponent<Text>().text == text) count++;
            return count;
        }

        private static int CountActive(Transform root, string name)
        {
            int count = 0;
            foreach (Transform child in root)
                if (child.name == name && child.gameObject.activeSelf) count++;
            return count;
        }

        /// <summary>Installs a counter-bearing duel on the real presentation clock, restoring the prototype afterwards.</summary>
        private sealed class CounterScope : IDisposable
        {
            private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly DuelPresentationSettings originalSettings;
            private readonly DuelPresentationSettings settingsClone;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originalEnabled;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public CounterScope(int enemyActionCount, LegacyCounter playerCounter, LegacyCounter enemyCounter)
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalSession = Controller.Session;
                settingsClone = Object.Instantiate(originalSettings);
                // A windup before the first hit leaves room for the dodge decision.
                JsonUtility.FromJsonOverwrite("{\"animationPlaybackSpeed\":1,\"attackInterval\":0.1,\"skillInterval\":0.1," +
                    "\"hitStopDuration\":0,\"stepAnticipationDuration\":0.24,\"stepTimingWindow\":0.1}", settingsClone);
                SetField("presentationSettings", settingsClone);
                var playerSkills = new[] { Attack(101, 1, 1) };
                var enemySkills = new[] { Attack(201, 1, 1) };
                SetField("session", new LegacyQueuedDuel(1000, 1000, 1000, 1000, playerSkills, enemySkills,
                    new[] { enemyActionCount }, 1, playerCounter, enemyCounter));
                var method = typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance);
                Assert.That(method, Is.Not.Null);
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller, method);
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance)
                    .Invoke(Controller, null);
            }

            public void Advance(float realDelta) => advance(realDelta, null);

            public void Until(Func<bool> condition)
            {
                int steps = 0;
                while (!condition() && steps++ < 5000) Advance(.001f);
                Assert.That(condition(), Is.True, "The presentation state must make progress within five real seconds.");
            }

            private void SetField(string name, object value)
            {
                var field = typeof(DuelPrototypeController).GetField(name, PrivateInstance);
                Assert.That(field, Is.Not.Null);
                field.SetValue(Controller, value);
            }

            public void Dispose()
            {
                SetField("presentationSettings", originalSettings);
                SetField("session", originalSession);
                Controller.RestartMatch();
                Controller.enabled = originalEnabled;
                Object.Destroy(settingsClone);
            }
        }
    }
}
