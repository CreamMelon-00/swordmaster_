using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class DamageNumberPlayModeTests
    {
        [UnityTest]
        public IEnumerator DamageNumbers_HaveReadableDefaults_AndZeroOrNegativeDamageNeverLooksCritical()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                fixture.Hud.ShowHitDamage(false, 12, Vector3.zero);
                fixture.Hud.ShowHitDamage(true, 0, Vector3.zero, true);
                fixture.Hud.ShowHitDamage(true, -50, Vector3.zero, true);
                fixture.Refresh(.22f);
                List<Text> numbers = DamageTexts(fixture.Hud, true);
                Assert.That(numbers.Count, Is.EqualTo(3));
                Text normal = FindNumber(numbers, "12");
                Assert.That(normal.fontSize, Is.EqualTo(168));
                Assert.That(normal.rectTransform.localScale.x, Is.EqualTo(.925f).Within(.001f));
                AssertNormalInk(normal);
                foreach (Text number in numbers)
                {
                    AssertReadableEffects(number);
                    Assert.That(number.color.a, Is.EqualTo(1f).Within(.001f));
                    if (number.text != "0") continue;
                    Assert.That(number.fontSize, Is.EqualTo(normal.fontSize));
                    Assert.That(number.rectTransform.localScale.x, Is.EqualTo(.8f).Within(.001f),
                        "No damage must not receive the critical size multiplier.");
                    Assert.That(number.color, Is.EqualTo(normal.color),
                        "The critical flag must not tint zero or clamped negative damage gold.");
                }
            }
        }

        [UnityTest]
        public IEnumerator PopIn_SettlesAtReadableSize_AndOnlyFadesAtTheEndOnTheRealClock()
        {
            yield return null;
            float originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                using (var fixture = new HudFixture())
                {
                    fixture.Hud.ShowHitDamage(false, 24, Vector3.zero);
                    Text number = OnlyNumber(fixture.Hud);
                    float initialScale = number.rectTransform.localScale.x;
                    Vector2 initialPosition = number.rectTransform.anchoredPosition;
                    Color initialColor = number.color;
                    fixture.Refresh(0f);
                    Assert.That(number.rectTransform.localScale.x, Is.EqualTo(initialScale));
                    Assert.That(number.rectTransform.anchoredPosition, Is.EqualTo(initialPosition));
                    Assert.That(number.color, Is.EqualTo(initialColor));
                    fixture.Refresh(.1f);
                    float peakScale = number.rectTransform.localScale.x;
                    Assert.That(peakScale, Is.GreaterThan(1.05f));
                    Assert.That(peakScale, Is.GreaterThan(initialScale));
                    Assert.That(number.color.a, Is.EqualTo(1f).Within(.001f));
                    fixture.Refresh(.12f);
                    float settledScale = number.rectTransform.localScale.x;
                    Assert.That(settledScale, Is.EqualTo(1.05f).Within(.001f));
                    Assert.That(number.rectTransform.anchoredPosition.y, Is.GreaterThan(initialPosition.y),
                        "The number must rise after the impact instead of shrinking in place.");
                    fixture.Refresh(.45f);
                    Assert.That(number.color.a, Is.EqualTo(1f).Within(.001f),
                        "The first 70 percent of the real-time lifetime remains fully readable.");
                    fixture.Refresh(.2f);
                    Assert.That(number.color.a, Is.InRange(.001f, .999f));
                    Assert.That(number.rectTransform.localScale.x, Is.EqualTo(settledScale).Within(.001f),
                        "Exit fades the ink; it must not shrink the number to an unreadable dot.");
                    fixture.Refresh(.2f);
                    Assert.That(number.gameObject.activeSelf, Is.False);
                }
            }
            finally { Time.timeScale = originalTimeScale; }
        }

        [UnityTest]
        public IEnumerator CriticalDamage_IsGoldAndLarger_WithABoundedSizeAndLongerLifetime()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                fixture.Hud.ShowHitDamage(true, 24, Vector3.zero);
                fixture.Hud.ShowHitDamage(false, int.MaxValue, Vector3.zero, true);
                fixture.Refresh(.22f);
                List<Text> numbers = DamageTexts(fixture.Hud, true);
                Text normal = FindNumber(numbers, "24");
                Text critical = FindNumber(numbers, int.MaxValue.ToString());
                Assert.That(normal.rectTransform.localScale.x, Is.EqualTo(1.05f).Within(.001f));
                Assert.That(critical.rectTransform.localScale.x, Is.EqualTo(1.05f * 1.3f).Within(.001f),
                    "Overkill values must not inflate the size beyond the bounded large-hit scale.");
                Assert.That(critical.rectTransform.localScale.x, Is.GreaterThan(normal.rectTransform.localScale.x));
                Assert.That(critical.color.r, Is.GreaterThan(critical.color.b + .15f));
                Assert.That(critical.color.g, Is.GreaterThan(critical.color.b + .1f),
                    "The emphasized number must use warm gold ink, not just a larger white glyph.");
                AssertReadableEffects(critical);
                fixture.Refresh(.86f);
                Assert.That(normal.gameObject.activeSelf, Is.False, "Ordinary damage lasts 1.05 real seconds.");
                Assert.That(critical.gameObject.activeSelf, Is.True, "Critical damage lasts 1.25 real seconds.");
                Assert.That(critical.color.a, Is.InRange(.001f, .999f));
                fixture.Refresh(.18f);
                Assert.That(critical.gameObject.activeSelf, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator BothSidesAndMultiHits_UseDirectionalStagger_AndReprojectWhenTheCameraMoves()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                Vector3 impact = new Vector3(.3f, -.2f, 0f);
                fixture.Hud.ShowHitDamage(true, 11, impact);
                fixture.Hud.ShowHitDamage(false, 12, impact);
                fixture.Hud.ShowHitDamage(false, 13, impact);
                fixture.Hud.ShowHitDamage(false, 14, impact);
                fixture.Refresh(0f);
                List<Text> numbers = DamageTexts(fixture.Hud, true);
                Assert.That(numbers.Count, Is.EqualTo(4));
                Vector2 projectionBefore = fixture.Project(impact);
                Text player = FindNumber(numbers, "11");
                Text enemy = FindNumber(numbers, "12");
                Assert.That(player.rectTransform.anchoredPosition.x, Is.LessThan(projectionBefore.x));
                Assert.That(enemy.rectTransform.anchoredPosition.x, Is.GreaterThan(projectionBefore.x));
                var positions = new Dictionary<Text, Vector2>();
                foreach (Text number in numbers)
                {
                    positions.Add(number, number.rectTransform.anchoredPosition);
                    Assert.That(Quaternion.Angle(number.rectTransform.localRotation, Quaternion.identity), Is.LessThan(.001f));
                    AssertReadableEffects(number);
                }
                Text second = FindNumber(numbers, "13"), third = FindNumber(numbers, "14");
                Assert.That(Vector2.Distance(positions[enemy], positions[second]), Is.GreaterThan(1f));
                Assert.That(Vector2.Distance(positions[second], positions[third]), Is.GreaterThan(1f));
                Assert.That(Vector2.Distance(positions[enemy], positions[third]), Is.GreaterThan(1f),
                    "Three simultaneous hits need distinct screen offsets.");
                fixture.Camera.transform.position += new Vector3(1.25f, .4f, 0f);
                fixture.Camera.orthographicSize = 4f;
                fixture.Refresh(0f);
                Vector2 projectedTravel = fixture.Project(impact) - projectionBefore;
                Assert.That(projectedTravel.magnitude, Is.GreaterThan(1f));
                foreach (Text number in numbers)
                    Assert.That(Vector2.Distance(number.rectTransform.anchoredPosition - positions[number], projectedTravel),
                        Is.LessThan(.05f), "At delta zero, only the impact's camera projection should move the number.");
            }
        }

        [UnityTest]
        public IEnumerator DamagePool_StaysWithin32Views_AndReusesTheSameObjectsAcrossBurstsAndReset()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                for (int hit = 0; hit < 80; hit++)
                    fixture.Hud.ShowHitDamage(hit % 2 == 0, hit + 1, Vector3.zero, hit % 3 == 0);
                List<Text> pooled = DamageTexts(fixture.Hud, false);
                Assert.That(pooled.Count, Is.EqualTo(32));
                Assert.That(DamageTexts(fixture.Hud, true).Count, Is.LessThanOrEqualTo(32));
                var originalIds = new HashSet<Text>();
                foreach (Text number in pooled)
                {
                    originalIds.Add(number);
                    AssertReadableEffects(number);
                }
                fixture.Refresh(2f);
                Assert.That(DamageTexts(fixture.Hud, true), Is.Empty);
                for (int hit = 0; hit < 80; hit++)
                    fixture.Hud.ShowHitDamage(hit % 2 != 0, hit, Vector3.one, hit % 2 == 0);
                AssertSamePool(fixture.Hud, originalIds);
                fixture.Hud.Reset();
                Assert.That(DamageTexts(fixture.Hud, true), Is.Empty);
                fixture.Refresh(0f);
                fixture.Hud.ShowHitDamage(true, 5, Vector3.zero);
                Assert.That(DamageTexts(fixture.Hud, true).Count, Is.EqualTo(1));
                AssertSamePool(fixture.Hud, originalIds);
            }
        }

        [UnityTest]
        public IEnumerator ReusedCriticalView_ResetsTintOpacitySizeAndOffsetToFreshNormalDamage()
        {
            yield return null;
            using (var reused = new HudFixture())
            using (var fresh = new HudFixture())
            {
                reused.Hud.ShowHitDamage(false, 999, Vector3.one, true);
                Text original = OnlyNumber(reused.Hud);
                reused.Refresh(1.1f);
                Assert.That(original.color.a, Is.LessThan(1f));
                reused.Refresh(.2f);
                Assert.That(original.gameObject.activeSelf, Is.False);
                Vector3 impact = new Vector3(-.3f, .25f, 0f);
                reused.Hud.ShowHitDamage(true, -1, impact, true);
                fresh.Hud.ShowHitDamage(true, 0, impact);
                Text recycled = OnlyNumber(reused.Hud), reference = OnlyNumber(fresh.Hud);
                Assert.That(recycled, Is.SameAs(original), "An expired number must reuse the existing view.");
                Assert.That(recycled.text, Is.EqualTo("0"));
                Assert.That(recycled.color, Is.EqualTo(reference.color));
                Assert.That(recycled.color.a, Is.EqualTo(1f).Within(.001f));
                Assert.That(recycled.fontSize, Is.EqualTo(reference.fontSize));
                Assert.That(Vector3.Distance(recycled.rectTransform.localScale, reference.rectTransform.localScale), Is.LessThan(.001f));
                Assert.That(Vector2.Distance(recycled.rectTransform.anchoredPosition, reference.rectTransform.anchoredPosition), Is.LessThan(.05f));
                Assert.That(recycled.rectTransform.localRotation, Is.EqualTo(Quaternion.identity));
                AssertReadableEffects(recycled);
                reused.Hud.Reset();
                fresh.Hud.Reset();
                reused.Refresh(0f);
                fresh.Refresh(0f);
                reused.Hud.ShowHitDamage(true, 0, impact);
                fresh.Hud.ShowHitDamage(true, 0, impact);
                Assert.That(OnlyNumber(reused.Hud), Is.SameAs(original));
                Assert.That(Vector2.Distance(original.rectTransform.anchoredPosition, reference.rectTransform.anchoredPosition),
                    Is.LessThan(.05f), "Reset must also clear the three-hit stagger sequence.");
            }
        }

        [UnityTest]
        public IEnumerator Settings_DefaultClampAndLiveBinding_ApplyWithoutChangingTheResourcesAsset()
        {
            yield return null;
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            DuelPresentationSettings source = Resources.Load<DuelPresentationSettings>("DuelPresentationSettings");
            Assert.That(source, Is.Not.Null);
            string assetBefore = JsonUtility.ToJson(source);
            try
            {
                Assert.That(settings.DamageTextFontSize, Is.EqualTo(168));
                Assert.That(settings.CriticalDamageScale, Is.EqualTo(1.3f));
                JsonUtility.FromJsonOverwrite("{\"damageTextFontSize\":-9,\"criticalDamageScale\":-5}", settings);
                Assert.That(settings.DamageTextFontSize, Is.EqualTo(80));
                Assert.That(settings.CriticalDamageScale, Is.EqualTo(1f));
                JsonUtility.FromJsonOverwrite("{\"damageTextFontSize\":999,\"criticalDamageScale\":999}", settings);
                Assert.That(settings.DamageTextFontSize, Is.EqualTo(240));
                Assert.That(settings.CriticalDamageScale, Is.EqualTo(1.8f));
                FieldInfo criticalScale = typeof(DuelPresentationSettings).GetField("criticalDamageScale", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(criticalScale, Is.Not.Null);
                foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    criticalScale.SetValue(settings, invalid);
                    Assert.That(settings.CriticalDamageScale, Is.EqualTo(1.3f));
                }
                JsonUtility.FromJsonOverwrite("{\"damageTextFontSize\":196,\"criticalDamageScale\":1.5}", settings);
                using (var fixture = new HudFixture(settings))
                {
                    fixture.Hud.ShowHitDamage(false, 24, Vector3.zero, true);
                    fixture.Refresh(.22f);
                    Text first = OnlyNumber(fixture.Hud);
                    Assert.That(first.fontSize, Is.EqualTo(196));
                    Assert.That(first.rectTransform.localScale.x, Is.EqualTo(1.05f * 1.5f).Within(.001f));
                    fixture.Hud.Reset();
                    JsonUtility.FromJsonOverwrite("{\"damageTextFontSize\":224,\"criticalDamageScale\":1.65}", settings);
                    fixture.Refresh(0f);
                    fixture.Hud.ShowHitDamage(false, 24, Vector3.zero, true);
                    fixture.Refresh(.22f);
                    Text second = OnlyNumber(fixture.Hud);
                    Assert.That(second, Is.SameAs(first));
                    Assert.That(second.fontSize, Is.EqualTo(224), "New impacts must read the bound live tuning asset.");
                    Assert.That(second.rectTransform.localScale.x, Is.EqualTo(1.05f * 1.65f).Within(.001f));
                }
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(assetBefore));
            }
            finally { Object.Destroy(settings); }
        }

        [UnityTest]
        public IEnumerator MissingCameraAndDisposedHud_DoNotAllocateOrThrow_AndDamageDisplayNeverChangesCombatState()
        {
            yield return null;
            using (var fixture = new HudFixture(null, false))
            {
                Assert.DoesNotThrow(() => fixture.Hud.ShowHitDamage(false, 7, Vector3.zero));
                Assert.That(DamageTexts(fixture.Hud, false), Is.Empty);
                fixture.Refresh(0f);
                int playerHealth = fixture.Duel.Player.Health, enemyHealth = fixture.Duel.Enemy.Health;
                int act = fixture.Duel.Act, round = fixture.Duel.RoundNumber;
                fixture.Hud.ShowHitDamage(false, 200, Vector3.zero, true);
                fixture.Hud.ShowHitDamage(true, 50, Vector3.one);
                fixture.Refresh(.4f);
                Assert.That(DamageTexts(fixture.Hud, true).Count, Is.EqualTo(2));
                Assert.That(fixture.Duel.Player.Health, Is.EqualTo(playerHealth));
                Assert.That(fixture.Duel.Enemy.Health, Is.EqualTo(enemyHealth));
                Assert.That(fixture.Duel.Act, Is.EqualTo(act));
                Assert.That(fixture.Duel.RoundNumber, Is.EqualTo(round));
                Assert.That(fixture.Duel.PlayerQueue, Is.Empty);
                fixture.Hud.Dispose();
                Assert.DoesNotThrow(() => fixture.Hud.ShowHitDamage(false, 99, Vector3.zero, true));
                Assert.DoesNotThrow(() => fixture.Refresh(1f));
                Assert.DoesNotThrow(() => fixture.Hud.Reset());
            }
        }

        private static List<Text> DamageTexts(LegacyCombatHud hud, bool activeOnly)
        {
            var result = new List<Text>();
            foreach (Text text in hud.Root.GetComponentsInChildren<Text>(true))
                if (text.gameObject.name == "Damage" && text.transform.parent == hud.Root.transform &&
                    (!activeOnly || text.gameObject.activeSelf)) result.Add(text);
            return result;
        }

        private static Text OnlyNumber(LegacyCombatHud hud)
        {
            List<Text> numbers = DamageTexts(hud, true);
            Assert.That(numbers.Count, Is.EqualTo(1));
            return numbers[0];
        }

        private static Text FindNumber(List<Text> numbers, string amount)
        {
            foreach (Text number in numbers)
                if (number.text == amount) return number;
            Assert.Fail("Missing visible damage amount " + amount);
            return null;
        }

        private static void AssertNormalInk(Text number)
        {
            Assert.That(number.color.r, Is.GreaterThanOrEqualTo(.8f));
            Assert.That(number.color.g, Is.GreaterThanOrEqualTo(.8f));
            Assert.That(number.color.b, Is.GreaterThanOrEqualTo(.7f));
        }

        private static void AssertReadableEffects(Text number)
        {
            Assert.That(number.raycastTarget, Is.False, "Floating feedback must not block combat HUD input.");
            Outline outline = number.GetComponent<Outline>();
            Assert.That(outline, Is.Not.Null);
            Assert.That(outline.enabled, Is.True);
            Assert.That(outline.effectColor.r, Is.GreaterThan(outline.effectColor.g + .1f));
            Assert.That(outline.effectColor.r, Is.GreaterThan(outline.effectColor.b + .1f));
            Assert.That(outline.effectDistance.sqrMagnitude, Is.GreaterThan(1f));
            Shadow shadow = null;
            foreach (Shadow candidate in number.GetComponents<Shadow>())
                if (!(candidate is Outline)) shadow = candidate;
            Assert.That(shadow, Is.Not.Null, "Red edging also needs a separate dark backdrop shadow.");
            Assert.That(shadow.enabled, Is.True);
            Assert.That(Mathf.Max(shadow.effectColor.r, Mathf.Max(shadow.effectColor.g, shadow.effectColor.b)), Is.LessThan(.2f));
            Assert.That(shadow.effectDistance.sqrMagnitude, Is.GreaterThan(1f));
            Assert.That(number.GetComponents<Outline>().Length, Is.EqualTo(1), "Reuse must not add duplicate outline components.");
            Assert.That(number.GetComponents<Shadow>().Length, Is.EqualTo(2), "Reuse must not add duplicate shadows.");
        }

        private static void AssertSamePool(LegacyCombatHud hud, HashSet<Text> originalIds)
        {
            List<Text> numbers = DamageTexts(hud, false);
            Assert.That(numbers.Count, Is.EqualTo(32));
            var ids = new HashSet<Text>();
            foreach (Text number in numbers)
            {
                ids.Add(number);
                AssertReadableEffects(number);
            }
            CollectionAssert.AreEquivalent(originalIds, ids);
        }

        private sealed class HudFixture : System.IDisposable
        {
            private readonly GameObject owner = new GameObject("Damage Number Fixture");
            private readonly Transform player, enemy;
            public readonly Camera Camera;
            public readonly LegacyQueuedDuel Duel = new LegacyQueuedDuel();
            public readonly LegacyCombatHud Hud;

            public HudFixture(DuelPresentationSettings settings = null, bool bindCamera = true)
            {
                Camera = new GameObject("Damage Fixture Camera").AddComponent<Camera>();
                Camera.transform.SetParent(owner.transform, false);
                Camera.transform.localPosition = new Vector3(0f, 0f, -10f);
                Camera.orthographic = true;
                Camera.orthographicSize = 6f;
                Camera.aspect = 16f / 9f;
                Camera.enabled = false;
                player = new GameObject("Damage Fixture Player").transform;
                enemy = new GameObject("Damage Fixture Enemy").transform;
                player.SetParent(owner.transform, false);
                enemy.SetParent(owner.transform, false);
                player.localPosition = new Vector3(-2f, -.5f, 0f);
                enemy.localPosition = new Vector3(2f, -.5f, 0f);
                Hud = new LegacyCombatHud(owner.transform, new LegacyDuelArt(), _ => { }, () => { }, () => { }, settings);
                Canvas.ForceUpdateCanvases();
                if (bindCamera) Refresh(0f);
            }

            public void Refresh(float realDelta)
            {
                // The combat clock is deliberately stopped. Damage feedback must
                // still complete on its own explicit real-time presentation clock.
                Hud.Refresh(Duel, 10f, true, -1, Camera, player, enemy, 0f, realDelta);
                Canvas.ForceUpdateCanvases();
            }

            public Vector2 Project(Vector3 world)
            {
                Vector3 screen = Camera.WorldToScreenPoint(world);
                return new Vector2(screen.x, screen.y) / Hud.Root.GetComponent<Canvas>().scaleFactor;
            }

            public void Dispose()
            {
                Hud.Dispose();
                Object.Destroy(owner);
            }
        }
    }
}
