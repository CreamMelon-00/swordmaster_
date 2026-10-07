using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class BreakPresentationPlayModeTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator BreakAura_FollowsTheActorFadesOnTheCombatClockAndLeavesTheActorUntouched()
        {
            yield return null;
            var shader = Resources.Load<Shader>("DuelVFX/BreakSilhouette");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True, "The break outline shader must compile for this pipeline.");
            var material = new Material(shader);
            var texture = new Texture2D(8, 8) { filterMode = FilterMode.Point };
            var owner = new GameObject("Break Aura Fixture");
            try
            {
                Sprite first = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(.5f, 0f), 40f);
                Sprite second = Sprite.Create(texture, new Rect(0, 0, 4, 8), new Vector2(.5f, 0f), 40f);
                var actor = new GameObject("Actor").AddComponent<SpriteRenderer>();
                actor.transform.SetParent(owner.transform, false);
                actor.sprite = first;
                var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
                shadow.transform.SetParent(actor.transform, false);
                shadow.sprite = first;
                shadow.sortingOrder = -2;
                var aura = new DuelBreakAura(actor, null, shadow, material, 0);
                Assert.That(aura.HasRequiredAssets, Is.True);
                Assert.That(aura.OutlineRendererCount, Is.EqualTo(8));
                List<SpriteRenderer> outline = Outline(actor);
                MeshRenderer ring = shadow.GetComponentInChildren<MeshRenderer>(true);
                Assert.That(outline.Count, Is.EqualTo(8));
                Assert.That(ring, Is.Not.Null);
                Assert.That(ring.GetComponent<MeshFilter>().sharedMesh.vertexCount,
                    Is.EqualTo(DuelBreakAura.RingSegments * 2), "Every ring segment joins the next without gaps.");
                Assert.That(outline.TrueForAll(copy => !copy.enabled), Is.True);
                Assert.That(ring.enabled, Is.False);

                aura.SetBroken(true);
                aura.Tick(0f);
                Assert.That(aura.Intensity, Is.Zero, "A stopped combat clock holds the fade.");
                aura.Tick(DuelBreakAura.FadeDuration * .5f);
                Assert.That(aura.Intensity, Is.EqualTo(.5f).Within(.01f));
                Assert.That(aura.IsVisible, Is.True);
                var directions = new HashSet<Vector2Int>();
                foreach (SpriteRenderer copy in outline)
                {
                    Assert.That(copy.enabled, Is.True);
                    Assert.That(copy.sprite, Is.SameAs(first));
                    Assert.That(copy.sortingOrder, Is.EqualTo(DuelBreakAura.SortingOrder));
                    Assert.That(copy.sharedMaterial, Is.SameAs(material));
                    Color drawn = DuelBreakAura.OutlineColor(copy);
                    Assert.That(drawn.r, Is.GreaterThan(drawn.g + .3f), "The outline reads red.");
                    Assert.That(drawn.a, Is.InRange(.01f, 1f));
                    Assert.That(copy.color, Is.EqualTo(Color.white), "The tint travels in the property block, not the sprite colour.");
                    Vector3 offset = copy.transform.localPosition;
                    Assert.That(Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)),
                        Is.EqualTo(DuelBreakAura.OutlineThickness).Within(.0001f));
                    directions.Add(new Vector2Int(Mathf.RoundToInt(offset.x / DuelBreakAura.OutlineThickness),
                        Mathf.RoundToInt(offset.y / DuelBreakAura.OutlineThickness)));
                }
                Assert.That(directions.Count, Is.EqualTo(8), "The one-pixel outline fills cardinal and diagonal corners.");
                Assert.That(ring.enabled, Is.True);
                Assert.That(ring.sortingOrder, Is.EqualTo(DuelBreakAura.SortingOrder));

                actor.sprite = second;
                aura.Apply();
                Assert.That(outline.TrueForAll(copy => copy.sprite == second), Is.True, "The outline follows the actor's current frame.");
                aura.Tick(1f);
                Assert.That(aura.Intensity, Is.EqualTo(1f));
                Assert.That(actor.color, Is.EqualTo(Color.white), "The actor's own colour stays free for hit flashes.");

                aura.SetBroken(false);
                aura.Tick(DuelBreakAura.FadeDuration);
                Assert.That(aura.Intensity, Is.Zero);
                Assert.That(aura.IsVisible, Is.False);
                Assert.That(outline.TrueForAll(copy => !copy.enabled), Is.True);
                Assert.That(ring.enabled, Is.False);

                aura.SetBroken(true);
                aura.Tick(1f);
                aura.Reset();
                Assert.That(aura.IsBroken, Is.False);
                Assert.That(aura.IsVisible, Is.False);
                Assert.That(outline.TrueForAll(copy => !copy.enabled), Is.True);
                aura.Dispose();
            }
            finally
            {
                Object.Destroy(owner);
                Object.Destroy(material);
                Object.Destroy(texture);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Arena_ShowsTheAuraOnlyOnBrokenFightersAndResetClearsIt()
        {
            var host = new GameObject("Break Arena Test");
            using (var art = new LegacyDuelArt())
            using (var arena = LegacyArenaView.Create(host.transform, art, null))
            {
                Assert.That(arena.PlayerBreakAura.HasRequiredAssets, Is.True);
                Assert.That(arena.EnemyBreakAura.HasRequiredAssets, Is.True);
                arena.SetResistanceBroken(false, true);
                arena.Tick(.2f, .2f);
                Assert.That(arena.EnemyBreakAura.IsVisible, Is.True);
                Assert.That(arena.PlayerBreakAura.IsVisible, Is.False);
                Assert.That(Outline(arena.EnemyRenderer).TrueForAll(copy => copy.sprite == arena.EnemyRenderer.sprite), Is.True);
                Assert.That(arena.EnemyRenderer.color, Is.EqualTo(Color.white));
                arena.SetResistanceBroken(true, true);
                arena.Tick(.2f, .2f);
                Assert.That(arena.PlayerBreakAura.IsVisible, Is.True);
                arena.Reset();
                Assert.That(arena.PlayerBreakAura.IsVisible, Is.False);
                Assert.That(arena.EnemyBreakAura.IsVisible, Is.False);
            }
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HudNumbersAndCallouts_KeepResistanceSteelAndUseTheirOwnPool()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                fixture.Hud.ShowHitDamage(false, 7, Vector3.zero, false, true);
                fixture.Hud.ShowHitDamage(false, 20, Vector3.zero, true, true);
                fixture.Hud.ShowHitDamage(true, 9, Vector3.zero);
                fixture.Refresh(.01f);
                Text resistance = Number(fixture.Hud, "7"), emphasised = Number(fixture.Hud, "20"), health = Number(fixture.Hud, "9");
                Assert.That(resistance.color.b, Is.GreaterThan(resistance.color.r), "Resistance loss reads in steel, not ivory.");
                Assert.That(emphasised.color.b, Is.GreaterThan(emphasised.color.r), "Emphasis must not turn resistance loss gold.");
                Assert.That(emphasised.rectTransform.localScale.x, Is.GreaterThan(resistance.rectTransform.localScale.x));
                Assert.That(health.color.r, Is.GreaterThanOrEqualTo(.8f));
                Assert.That(health.color.b, Is.LessThan(health.color.r));

                fixture.Hud.ShowBreakCallout(false, Vector3.zero);
                fixture.Hud.ShowRecoveryCallout(true, Vector3.zero);
                fixture.Refresh(.01f);
                List<Text> callouts = Named(fixture.Hud.Root, "State Callout", true);
                Assert.That(callouts.ConvertAll(c => c.text), Is.EquivalentTo(new[] { LegacyCombatHud.BreakCalloutText, LegacyCombatHud.RecoveryCalloutText }));
                Assert.That(Named(fixture.Hud.Root, "Damage", true).Count, Is.EqualTo(3), "Callouts are never damage numbers.");
                foreach (Text callout in callouts) Assert.That(callout.raycastTarget, Is.False);
                Text breakCallout = callouts.Find(c => c.text == LegacyCombatHud.BreakCalloutText);
                Assert.That(breakCallout.rectTransform.anchoredPosition.y,
                    Is.LessThan(resistance.rectTransform.anchoredPosition.y - 100f),
                    "The callout sits at the waist, clear of the numbers rising from the same impact point.");
                for (int i = 0; i < 12; i++) fixture.Hud.ShowBreakCallout(i % 2 == 0, Vector3.zero);
                Assert.That(Named(fixture.Hud.Root, "State Callout", false).Count, Is.LessThanOrEqualTo(4));
                fixture.Refresh(2f);
                Assert.That(Named(fixture.Hud.Root, "State Callout", true), Is.Empty, "Callouts expire on the real clock.");
                fixture.Hud.ShowBreakCallout(false, Vector3.zero);
                fixture.Hud.Reset();
                Assert.That(Named(fixture.Hud.Root, "State Callout", true), Is.Empty);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualController_CallsOutSkillAndHitBreaksAndTheTurnStartRefill()
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
            MethodInfo startPlanning = typeof(DuelPrototypeController).GetMethod("StartPlanning", PrivateInstance);
            try
            {
                // 쿠페 breaks a guarding enemy at slot start, before any hit: a callout, but no fatal camera.
                var draw = new LegacyQueuedDuel(100, 50, 100, 15,
                    new[] { Attack(42, 1) }, new[] { Guard(900, 100) }, new[] { 1 });
                sessionField.SetValue(controller, draw);
                reset.Invoke(controller, null);
                draw.TryQueueLane(0); draw.Commit();
                begin.Invoke(controller, null);
                advance.Invoke(controller, new object[] { 0f, null });
                Assert.That(draw.Enemy.IsResistanceBroken, Is.True);
                Assert.That(ActiveTexts(controller.Hud.Root, "State Callout"), Is.EqualTo(new[] { LegacyCombatHud.BreakCalloutText }));
                Assert.That(controller.ArenaView.EnemyBreakAura.IsBroken, Is.True);
                Assert.That(controller.ArenaView.PlayerBreakAura.IsBroken, Is.False);
                Assert.That(controller.ArenaView.IsFatalFocus, Is.False);

                // A clash breaks the enemy on a hit; the enemy's weak strike is plain resistance loss.
                var clash = new LegacyQueuedDuel(100, 50, 100, 10,
                    new[] { Attack(900, 30) }, new[] { Attack(901, 1) }, new[] { 1 });
                sessionField.SetValue(controller, clash);
                reset.Invoke(controller, null);
                Assert.That(ActiveTexts(controller.Hud.Root, "State Callout"), Is.Empty);
                Assert.That(controller.ArenaView.EnemyBreakAura.IsBroken, Is.False);
                clash.TryQueueLane(0); clash.Commit();
                begin.Invoke(controller, null);
                for (int i = 0; i < 400 && !clash.Enemy.IsResistanceBroken; i++)
                    advance.Invoke(controller, new object[] { .02f, null });
                Assert.That(clash.Enemy.IsResistanceBroken, Is.True);
                Assert.That(ActiveTexts(controller.Hud.Root, "State Callout"), Is.EqualTo(new[] { LegacyCombatHud.BreakCalloutText }));
                Assert.That(controller.ArenaView.EnemyBreakAura.IsBroken, Is.True);
                Assert.That(controller.ArenaView.IsFatalFocus, Is.True, "A break is a decisive moment.");
                Text playerLoss = Number(controller.Hud, "1");
                Assert.That(playerLoss.color.b, Is.GreaterThan(playerLoss.color.r), "The player's resistance loss is steel.");
                Text enemyOverflow = Number(controller.Hud, "20");
                Assert.That(enemyOverflow.color.r, Is.GreaterThan(enemyOverflow.color.b), "The breaking overflow reached the body.");

                // Finish the turns directly: still broken through the next turn, refilled at the one after.
                while (!clash.IsTurnResolved) clash.ResolveNextSlot();
                controller.Hud.Reset();
                startPlanning.Invoke(controller, null);
                Assert.That(clash.Enemy.Resistance, Is.Zero);
                Assert.That(ActiveTexts(controller.Hud.Root, "State Callout"), Is.Empty);
                Assert.That(controller.ArenaView.EnemyBreakAura.IsBroken, Is.True);
                clash.TryQueueLane(0); clash.Commit();
                while (!clash.IsTurnResolved) clash.ResolveNextSlot();
                Assert.That(clash.IsFinished, Is.False);
                startPlanning.Invoke(controller, null);
                Assert.That(clash.Enemy.Resistance, Is.EqualTo(10));
                Assert.That(ActiveTexts(controller.Hud.Root, "State Callout"), Is.EqualTo(new[] { LegacyCombatHud.RecoveryCalloutText }));
                Assert.That(controller.ArenaView.EnemyBreakAura.IsBroken, Is.False);
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
        public IEnumerator ActualController_BreakImpactPlaysOncePerNewBreakOnEitherSideAndExpiresOnRealTime()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            bool originalEnabled = controller.enabled;
            LegacyQueuedDuel originalSession = controller.Session;
            controller.enabled = false;
            FieldInfo sessionField = typeof(DuelPrototypeController).GetField("session", PrivateInstance);
            FieldInfo hitStopField = typeof(DuelPrototypeController).GetField("hitStopRemaining", PrivateInstance);
            MethodInfo reset = typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance);
            MethodInfo begin = typeof(DuelPrototypeController).GetMethod("BeginSlotAnimation", PrivateInstance);
            MethodInfo advance = typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance);
            try
            {
                // A skill effect can break resistance before an impact. It still needs a one-shot cue.
                var direct = new LegacyQueuedDuel(100, 50, 100, 15,
                    new[] { Attack(42, 1) }, new[] { Guard(900, 100) }, new[] { 1 });
                sessionField.SetValue(controller, direct);
                reset.Invoke(controller, null);
                Assert.That(BreakImpact(controller, "Player Break Impact").activeSelf, Is.False);
                Assert.That(BreakImpact(controller, "Enemy Break Impact").activeSelf, Is.False);
                direct.TryQueueLane(0); direct.Commit();
                begin.Invoke(controller, null);
                advance.Invoke(controller, new object[] { 0f, null });
                Assert.That(direct.Enemy.IsResistanceBroken, Is.True);
                Assert.That(BreakImpact(controller, "Enemy Break Impact").activeSelf, Is.True);
                Assert.That(BreakImpact(controller, "Player Break Impact").activeSelf, Is.False);
                DuelSkillActivationBurst enemyBurst = BreakImpact(controller, "Enemy Break Impact")
                    .GetComponent<DuelSkillActivationBurst>();
                Assert.That(enemyBurst.Style, Is.EqualTo(DuelSkillBurstStyle.Break));
                Assert.That(enemyBurst.raycastTarget, Is.False);

                // The one-shot keeps its own real-time clock even while combat time is stopped.
                hitStopField.SetValue(controller, 2f);
                advance.Invoke(controller, new object[] { .7f, null });
                Assert.That((float)hitStopField.GetValue(controller), Is.GreaterThan(1f));
                Assert.That(BreakImpact(controller, "Enemy Break Impact").activeSelf, Is.False);
                hitStopField.SetValue(controller, 0f);
                for (int i = 0; i < 400 && direct.CurrentSlot.HitsResolved == 0; i++)
                    advance.Invoke(controller, new object[] { .02f, null });
                Assert.That(direct.CurrentSlot.HitsResolved, Is.GreaterThan(0));
                Assert.That(direct.Enemy.IsResistanceBroken, Is.True);
                Assert.That(BreakImpact(controller, "Enemy Break Impact").activeSelf, Is.False,
                    "A later hit against an already-broken enemy must not restart the impact cue.");

                // A normal clash can instead break the player. The two sides use separate cues.
                var clash = new LegacyQueuedDuel(100, 10, 100, 50,
                    new[] { Attack(900, 1) }, new[] { Attack(901, 30) }, new[] { 1 });
                sessionField.SetValue(controller, clash);
                reset.Invoke(controller, null);
                Assert.That(BreakImpact(controller, "Enemy Break Impact").activeSelf, Is.False);
                clash.TryQueueLane(0); clash.Commit();
                begin.Invoke(controller, null);
                for (int i = 0; i < 400 && !clash.Player.IsResistanceBroken; i++)
                    advance.Invoke(controller, new object[] { .02f, null });
                Assert.That(clash.Player.IsResistanceBroken, Is.True);
                Assert.That(BreakImpact(controller, "Player Break Impact").activeSelf, Is.True);
                Assert.That(BreakImpact(controller, "Enemy Break Impact").activeSelf, Is.False);
                DuelSkillActivationBurst playerBurst = BreakImpact(controller, "Player Break Impact")
                    .GetComponent<DuelSkillActivationBurst>();
                Assert.That(playerBurst.color.g, Is.LessThan(enemyBurst.color.g),
                    "The player's danger flash is redder than the enemy-break reward flash.");

                reset.Invoke(controller, null);
                Assert.That(BreakImpact(controller, "Player Break Impact").activeSelf, Is.False,
                    "A new battle clears a break impact still in progress.");
                Assert.That(BreakImpact(controller, "Enemy Break Impact").activeSelf, Is.False);
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
        public IEnumerator ActualController_ClosesUpOnBreaksFinishingBlowsAndQuarterHealthHitsOnly()
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
                Assert.That(controller.PresentationSettings.DecisiveHealthDamagePercent, Is.EqualTo(25));

                // A big hit into a guard, under a quarter of the enemy's health: still a gold, larger number, but no slow close-up.
                var big = new LegacyQueuedDuel(100, 50, 100, 50,
                    new[] { Attack(900, 20) }, new[] { Guard(901, 1) }, new[] { 1 });
                sessionField.SetValue(controller, big);
                reset.Invoke(controller, null);
                big.TryQueueLane(0); big.Commit();
                begin.Invoke(controller, null);
                for (int i = 0; i < 400 && big.Enemy.Health == 100; i++)
                    advance.Invoke(controller, new object[] { .02f, null });
                Assert.That(big.Enemy.Health, Is.EqualTo(81));
                Text bigNumber = Number(controller.Hud, "19");
                Assert.That(bigNumber.color.r, Is.GreaterThan(bigNumber.color.b + .3f), "Big hits keep the gold number.");
                Assert.That(controller.ArenaView.IsFatalFocus, Is.False, "A big number alone is not a decisive moment.");

                // The finishing blow gets the close-up.
                var finish = new LegacyQueuedDuel(100, 50, 10, 50,
                    new[] { Attack(900, 20) }, new[] { Guard(901, 1) }, new[] { 1 });
                sessionField.SetValue(controller, finish);
                reset.Invoke(controller, null);
                finish.TryQueueLane(0); finish.Commit();
                begin.Invoke(controller, null);
                for (int i = 0; i < 400 && finish.Enemy.Health > 0; i++)
                    advance.Invoke(controller, new object[] { .02f, null });
                Assert.That(finish.Enemy.Health, Is.Zero);
                Assert.That(controller.ArenaView.IsFatalFocus, Is.True);

                // One hit taking exactly a quarter of the enemy's health is decisive too, with nothing broken or finished.
                LegacyQueuedDuel quarter = HitOnce(controller, sessionField, reset, begin, advance,
                    new[] { Attack(900, 26) }, new[] { Guard(901, 1) }, duel => duel.Enemy.Health < 100);
                Assert.That(quarter.Enemy.Health, Is.EqualTo(75));
                Assert.That(quarter.Enemy.IsResistanceBroken || quarter.Enemy.IsDefeated, Is.False);
                Assert.That(controller.ArenaView.IsFatalFocus, Is.True, "A quarter of her health in one hit is a decisive moment.");

                // The same on the player: the enemy's 26 through the player's 1-power guard.
                LegacyQueuedDuel taken = HitOnce(controller, sessionField, reset, begin, advance,
                    new[] { Guard(901, 1) }, new[] { Attack(900, 26) }, duel => duel.Player.Health < 100);
                Assert.That(taken.Player.Health, Is.EqualTo(75));
                Assert.That(controller.ArenaView.IsFatalFocus, Is.True, "Either direction.");

                // 40 against an attacking enemy is resistance, not health: no close-up, though the number is large.
                LegacyQueuedDuel clash = HitOnce(controller, sessionField, reset, begin, advance,
                    new[] { Attack(900, 40) }, new[] { Attack(901, 1) }, duel => duel.Enemy.Resistance < 50);
                Assert.That((clash.Enemy.Resistance, clash.Enemy.Health), Is.EqualTo((10, 100)));
                Assert.That(controller.ArenaView.IsFatalFocus, Is.False, "Resistance loss never counts towards the quarter.");
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
        public IEnumerator DecisiveHealthDamagePercent_IsClampedAndZeroSwitchesTheQuarterTriggerOff()
        {
            yield return null;
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            try
            {
                Assert.That(settings.DecisiveHealthDamagePercent, Is.EqualTo(LegacyDecisiveHit.DefaultHealthDamagePercent));
                JsonUtility.FromJsonOverwrite("{\"decisiveHealthDamagePercent\":-5}", settings);
                Assert.That(settings.DecisiveHealthDamagePercent, Is.Zero);
                JsonUtility.FromJsonOverwrite("{\"decisiveHealthDamagePercent\":999}", settings);
                Assert.That(settings.DecisiveHealthDamagePercent, Is.EqualTo(100));
            }
            finally { Object.Destroy(settings); }

            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            bool originalEnabled = controller.enabled;
            LegacyQueuedDuel originalSession = controller.Session;
            controller.enabled = false;
            FieldInfo sessionField = typeof(DuelPrototypeController).GetField("session", PrivateInstance);
            FieldInfo settingsField = typeof(DuelPrototypeController).GetField("presentationSettings", PrivateInstance);
            MethodInfo reset = typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance);
            MethodInfo begin = typeof(DuelPrototypeController).GetMethod("BeginSlotAnimation", PrivateInstance);
            MethodInfo advance = typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance);
            var original = (DuelPresentationSettings)settingsField.GetValue(controller);
            string assetBefore = JsonUtility.ToJson(original);
            // A temporary clone, never the persistent Inspector asset.
            DuelPresentationSettings off = Object.Instantiate(original);
            JsonUtility.FromJsonOverwrite("{\"decisiveHealthDamagePercent\":0}", off);
            try
            {
                settingsField.SetValue(controller, off);
                LegacyQueuedDuel quarter = HitOnce(controller, sessionField, reset, begin, advance,
                    new[] { Attack(900, 26) }, new[] { Guard(901, 1) }, duel => duel.Enemy.Health < 100);
                Assert.That(quarter.Enemy.Health, Is.EqualTo(75));
                Assert.That(controller.ArenaView.IsFatalFocus, Is.False, "At 0 a quarter of her health is only a big number.");

                LegacyQueuedDuel finish = HitOnce(controller, sessionField, reset, begin, advance,
                    new[] { Attack(900, 20) }, new[] { Guard(901, 1) }, duel => duel.Enemy.Health < 10, enemyHealth: 10);
                Assert.That(finish.Enemy.Health, Is.Zero);
                Assert.That(controller.ArenaView.IsFatalFocus, Is.True, "The finishing blow keeps its close-up.");
            }
            finally
            {
                settingsField.SetValue(controller, original);
                Object.Destroy(off);
                sessionField.SetValue(controller, originalSession);
                controller.RestartMatch();
                controller.enabled = originalEnabled;
            }
            Assert.That(JsonUtility.ToJson(original), Is.EqualTo(assetBefore));
            yield return null;
        }

        /// <summary>Plays the first slot of a 100/50 against 100/50 duel (or the given enemy health) on the controller until
        /// <paramref name="landed"/> holds, so the arena still shows that hit's presentation.</summary>
        private static LegacyQueuedDuel HitOnce(DuelPrototypeController controller, FieldInfo sessionField, MethodInfo reset,
            MethodInfo begin, MethodInfo advance, LegacySkill[] player, LegacySkill[] enemy,
            System.Func<LegacyQueuedDuel, bool> landed, int enemyHealth = 100)
        {
            var duel = new LegacyQueuedDuel(100, 50, enemyHealth, 50, player, enemy, new[] { 1 });
            sessionField.SetValue(controller, duel);
            reset.Invoke(controller, null);
            Assert.That(controller.ArenaView.IsFatalFocus, Is.False, "Each case starts without a close-up.");
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            begin.Invoke(controller, null);
            for (int i = 0; i < 400 && !landed(duel); i++)
                advance.Invoke(controller, new object[] { .02f, null });
            Assert.That(landed(duel), Is.True);
            return duel;
        }

        private static List<SpriteRenderer> Outline(SpriteRenderer actor)
        {
            var copies = new List<SpriteRenderer>();
            foreach (Transform child in actor.transform)
                if (child.name.StartsWith("Break Outline")) copies.Add(child.GetComponent<SpriteRenderer>());
            return copies;
        }

        private static List<Text> Named(GameObject root, string name, bool activeOnly)
        {
            var result = new List<Text>();
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
                if (text.gameObject.name == name && text.transform.parent == root.transform &&
                    (!activeOnly || text.gameObject.activeSelf)) result.Add(text);
            return result;
        }

        private static string[] ActiveTexts(GameObject root, string name) =>
            Named(root, name, true).ConvertAll(text => text.text).ToArray();

        private static GameObject BreakImpact(DuelPrototypeController controller, string name)
        {
            foreach (Transform child in controller.Hud.Root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            Assert.Fail("Missing break impact " + name);
            return null;
        }

        private static Text Number(LegacyCombatHud hud, string amount)
        {
            foreach (Text number in Named(hud.Root, "Damage", true))
                if (number.text == amount) return number;
            Assert.Fail("Missing visible damage amount " + amount);
            return null;
        }

        private static LegacySkill Attack(int id, int power)
            => new LegacySkill(id, "attack", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "");

        private static LegacySkill Guard(int id, int power)
            => new LegacySkill(id, "guard", 0, power, power, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "");

        private sealed class HudFixture : System.IDisposable
        {
            private readonly GameObject owner = new GameObject("Break Hud Fixture");
            private readonly Transform player, enemy;
            private readonly Camera camera;
            private readonly LegacyQueuedDuel duel = new LegacyQueuedDuel();
            public readonly LegacyCombatHud Hud;

            public HudFixture()
            {
                camera = new GameObject("Break Hud Camera").AddComponent<Camera>();
                camera.transform.SetParent(owner.transform, false);
                camera.transform.localPosition = new Vector3(0f, 0f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = 6f;
                camera.enabled = false;
                player = new GameObject("Break Hud Player").transform;
                enemy = new GameObject("Break Hud Enemy").transform;
                player.SetParent(owner.transform, false);
                enemy.SetParent(owner.transform, false);
                player.localPosition = new Vector3(-2f, -.5f, 0f);
                enemy.localPosition = new Vector3(2f, -.5f, 0f);
                Hud = new LegacyCombatHud(owner.transform, new LegacyDuelArt(), _ => { }, () => { }, () => { });
                Canvas.ForceUpdateCanvases();
                Refresh(0f);
            }

            public void Refresh(float realDelta)
            {
                Hud.Refresh(duel, 10f, true, -1, camera, player, enemy, 0f, realDelta);
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                Hud.Dispose();
                Object.Destroy(owner);
            }
        }
    }
}
