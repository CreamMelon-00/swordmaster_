using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>이아's yellow 수훈 afterimages, driven by her power aura: none while it is down, ghosts of her current frame
    /// left behind her (behind her body, break outline and shadow, over her aura's glow) while it is up, drawn in the set
    /// yellow (the silhouette's tint in the arena), held by a stopped clock, fading in and out with the aura, drifting back
    /// and swelling about her feet so a still figure keeps a faint echo, a pool that holds the settings' longest trail
    /// whole, and gone at once on an instant switch-off, a reset, a hidden figure or the arena's own reset.</summary>
    public sealed class AuraAfterimagePlayModeTests
    {
        private const float Frame = 1f / 30f;

        [Test]
        public void TheGhosts_FollowTheAura_StayBehindHer_AndLeaveWithIt()
        {
            var host = new GameObject("Aura Afterimage Host");
            var figure = new GameObject("Aura Afterimage Figure");
            figure.transform.SetParent(host.transform, false);
            figure.transform.localPosition = new Vector3(5f, -.5f, 0f);
            var body = figure.AddComponent<SpriteRenderer>();
            body.sprite = new LegacyDuelArt().GetEnemySprite(0f);
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            var aura = new DuelPowerAura(figure.transform, null, 0, 3);
            var ghosts = new DuelAuraAfterimages(body, host.transform, null, 0, settings);
            aura.Afterimages = ghosts;
            try
            {
                Assume.That(body.sprite, Is.Not.Null);
                Assume.That(settings.EmpowermentAfterimageAlpha, Is.GreaterThan(0f));
                float interval = settings.EmpowermentAfterimageInterval;
                Assert.That(ghosts.Root.parent, Is.SameAs(host.transform), "They are left where she was, not carried on her.");
                Assert.That(ghosts.Root.name, Is.EqualTo("Duel Aura Afterimages"));
                Assert.That(ghosts.Root.childCount, Is.EqualTo(DuelAuraAfterimages.Capacity), "A pool made up front.");
                for (int frame = 0; frame < 30; frame++) aura.Tick(Frame);
                Assert.That(ghosts.ActiveCount, Is.Zero, "No 수훈, no afterimage.");

                aura.SetAura(true);
                aura.Tick(interval);
                Assert.That(ghosts.ActiveCount, Is.EqualTo(1), "With her aura up she leaves one every interval.");
                SpriteRenderer ghost = Shown(ghosts).Single();
                Assert.That(ghost.name, Does.StartWith("Aura Afterimage"));
                Assert.That(ghost.sprite, Is.SameAs(body.sprite), "A silhouette of her current frame…");
                Assert.That(ghost.sortingOrder, Is.EqualTo(DuelAuraAfterimages.SortingOrder).And.LessThan(body.sortingOrder),
                    "…behind her…");
                Assert.That(Vector3.Distance(ghost.transform.position, body.transform.position), Is.LessThan(1e-4f), "…where she stands…");
                Assert.That(ghost.transform.localScale, Is.EqualTo(body.transform.localScale), "…as big as she is, to begin with…");
                Color yellow = settings.EmpowermentAfterimageColor;
                Assert.That(Drawn(ghost).r, Is.EqualTo(yellow.r).Within(1e-4f), "…in the set yellow…");
                Assert.That(Drawn(ghost).g, Is.EqualTo(yellow.g).Within(1e-4f));
                Assert.That(Drawn(ghost).b, Is.EqualTo(yellow.b).Within(1e-4f));
                Assert.That(Drawn(ghost).a, Is.EqualTo(settings.EmpowermentAfterimageAlpha).Within(1e-4f), "…half see-through.");
                Assert.That(yellow.r >= yellow.g && yellow.g > yellow.b, Is.True, "A warm yellow by default.");

                // Hit stop, a freeze frame: her clock stops, and her trail with it.
                for (int frame = 0; frame < 60; frame++) aura.Tick(0f);
                Assert.That(ghosts.ActiveCount, Is.EqualTo(1));
                Assert.That(Drawn(ghost).a, Is.EqualTo(settings.EmpowermentAfterimageAlpha).Within(1e-4f), "A held clock holds them.");

                // On the move: the ghosts stay where she was.
                for (int frame = 0; frame < 8; frame++)
                {
                    figure.transform.localPosition += Vector3.left * .3f;
                    aura.Tick(interval);
                }
                float x = body.transform.position.x;
                Assert.That(Shown(ghosts).Max(view => view.transform.position.x), Is.GreaterThan(x + 1f), "She leaves a trail as she moves.");
                Assert.That(ghosts.ActiveCount, Is.LessThanOrEqualTo(DuelAuraAfterimages.Capacity));

                // Standing still (facing left, unflipped): each ghost drifts back behind her and swells about her feet.
                for (int frame = 0; frame < 30; frame++) aura.Tick(Frame);
                Assert.That(ghosts.ActiveCount, Is.GreaterThan(0), "Even standing still she is never without them.");
                bool moved = false;
                foreach (SpriteRenderer view in Shown(ghosts))
                {
                    Vector3 offset = view.transform.position - body.transform.position;
                    Assert.That(offset.x, Is.GreaterThanOrEqualTo(-1e-4f), "They drift back, away from where she faces.");
                    float growth = view.transform.localScale.y - 1f;
                    Assert.That(growth, Is.GreaterThanOrEqualTo(-1e-4f));
                    Assert.That(offset.y, Is.EqualTo(-DuelPowerAura.FeetY * growth).Within(1e-3f), "Their feet stay on the ground.");
                    Assert.That(Drawn(view).a, Is.LessThanOrEqualTo(settings.EmpowermentAfterimageAlpha + 1e-4f));
                    moved |= offset.x > .01f && growth > .001f;
                }
                Assert.That(moved, Is.True, "Older ghosts have drifted and swelled.");
                body.flipX = true;
                for (int frame = 0; frame < Mathf.CeilToInt(settings.EmpowermentAfterimageSeconds / Frame) + 2; frame++) aura.Tick(Frame);
                Assert.That(ghosts.ActiveCount, Is.GreaterThan(0));
                foreach (SpriteRenderer view in Shown(ghosts))
                {
                    Assert.That(view.transform.position.x, Is.LessThanOrEqualTo(body.transform.position.x + 1e-4f),
                        "Turned the other way, they drift the other way.");
                    Assert.That(view.flipX, Is.True, "A ghost faces as she did.");
                }

                // The aura fading out takes the trail with it.
                aura.SetAura(false, .5f);
                for (int frame = 0; frame < Mathf.CeilToInt((.5f + settings.EmpowermentAfterimageSeconds) / Frame) + 3; frame++)
                    aura.Tick(Frame);
                Assert.That(ghosts.ActiveCount, Is.Zero, "Once her aura is gone, so are they.");

                // Off at once, a reset, a hidden figure: none at once.
                aura.SetAura(true);
                for (int frame = 0; frame < 10; frame++) aura.Tick(Frame);
                Assert.That(ghosts.ActiveCount, Is.GreaterThan(0));
                aura.SetAura(false);
                Assert.That(ghosts.ActiveCount, Is.Zero, "Switched off at once, the ghosts go at once.");
                aura.SetAura(true);
                for (int frame = 0; frame < 10; frame++) aura.Tick(Frame);
                Assert.That(ghosts.ActiveCount, Is.GreaterThan(0));
                aura.Reset();
                Assert.That(ghosts.ActiveCount, Is.Zero, "A reset leaves none behind.");
                aura.SetAura(true);
                figure.SetActive(false);
                for (int frame = 0; frame < 30; frame++) aura.Tick(Frame);
                Assert.That(ghosts.ActiveCount, Is.Zero, "Off stage she leaves nothing behind.");
                figure.SetActive(true);

                // An opacity of 0 switches them off; long frames never flood the pool.
                JsonUtility.FromJsonOverwrite("{\"empowermentAfterimageAlpha\":0}", settings);
                for (int frame = 0; frame < 30; frame++) aura.Tick(Frame);
                Assert.That(ghosts.ActiveCount, Is.Zero, "Tuned to nothing, nothing shows.");
                JsonUtility.FromJsonOverwrite("{\"empowermentAfterimageAlpha\":0.45,\"empowermentAfterimageInterval\":0.02}", settings);
                for (int frame = 0; frame < 200; frame++) aura.Tick(frame % 20 == 0 ? 3f : .02f);
                Assert.That(ghosts.ActiveCount, Is.InRange(1, DuelAuraAfterimages.Capacity));
                Assert.That(ghosts.Root.childCount, Is.EqualTo(DuelAuraAfterimages.Capacity), "The same pool, never more.");

                // The longest fade at the shortest interval: the pool holds the whole trail, its tail fading out, never cut.
                JsonUtility.FromJsonOverwrite("{\"empowermentAfterimageSeconds\":1.5}", settings);
                ghosts.Clear();
                for (int frame = 0; frame < 120; frame++) aura.Tick(.02f);
                Assert.That(ghosts.ActiveCount, Is.GreaterThan(70).And.LessThanOrEqualTo(DuelAuraAfterimages.Capacity),
                    "A ghost every 0.02 s for 1.5 s all show at once…");
                Assert.That(Shown(ghosts).Min(view => Drawn(view).a), Is.LessThan(.05f),
                    "…the oldest all but faded out, not taken back while it still shows.");
                aura.Tick(float.NaN);
                aura.Tick(-1f);
                ghosts.Clear();
                Assert.That(ghosts.ActiveCount, Is.Zero);
            }
            finally
            {
                ghosts.Dispose();
                aura.Dispose();
                Object.Destroy(host);
                Object.Destroy(settings);
            }
        }

        [UnityTest]
        public IEnumerator TheArenasEnemy_LeavesThemOnTheBattlesClock_AndTheArenaLeavesNoneBehind()
        {
            yield return null;
            var host = new GameObject("Arena Aura Afterimage Host");
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            LegacyArenaView arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt(), settings);
            try
            {
                Assume.That(arena.EnemyRenderer.sprite, Is.Not.Null);
                Assert.That(arena.EnemyPowerAura.Afterimages, Is.SameAs(arena.EnemyAuraAfterimages), "Her aura drives them.");
                Assert.That(arena.PlayerPowerAura.Afterimages, Is.Null, "Only 이아 leaves them.");
                Assert.That(host.transform.Find("Legacy Duel Arena/Duel Aura Afterimages"), Is.SameAs(arena.EnemyAuraAfterimages.Root),
                    "They live on the arena, apart from the steps' afterimages.");
                for (int frame = 0; frame < 20; frame++) arena.Tick(Frame, Frame);
                Assert.That(arena.ActiveAuraAfterimageCount, Is.Zero, "Before her 수훈 she leaves nothing.");

                arena.EnemyPowerAura.SetAura(true);
                for (int frame = 0; frame < 20; frame++) arena.Tick(0f, Frame);
                Assert.That(arena.ActiveAuraAfterimageCount, Is.Zero, "Hit stop holds the battle's clock, and her trail with it.");
                for (int frame = 0; frame < 20; frame++) arena.Tick(Frame, Frame);
                Assert.That(arena.ActiveAuraAfterimageCount, Is.GreaterThan(0), "On the battle's clock she leaves them.");
                // The newest ghost, so it is still showing a frame later.
                SpriteRenderer ghost = Shown(arena.EnemyAuraAfterimages).OrderByDescending(view => Drawn(view).a).First();
                Assert.That(ghost.sortingOrder, Is.LessThan(arena.EnemyRenderer.sortingOrder), "Behind her body…");
                Assert.That(ghost.sortingOrder, Is.LessThan(DuelBreakAura.SortingOrder),
                    "…behind her break outline and shadow, so the break's red cue is never washed yellow…");
                Assert.That(ghost.sortingOrder, Is.GreaterThanOrEqualTo(DuelPowerAura.BackSortingOrder), "…but not behind her aura's glow:");
                var glow = arena.EnemyRenderer.transform.Find("Power Aura/Power Aura Glow").GetComponent<SpriteRenderer>();
                Assert.That(glow.sortingOrder, Is.EqualTo(DuelPowerAura.BackSortingOrder));
                if (ghost.sortingOrder == glow.sortingOrder)
                    Assert.That(Depth(arena, ghost.transform), Is.LessThan(Depth(arena, glow.transform)),
                        "tied with it, they stand nearer the camera, so they draw over it.");
                Assert.That(ghost.gameObject.layer, Is.EqualTo(arena.EnemyRenderer.gameObject.layer), "On the arena's layer.");
                Color yellow = settings.EmpowermentAfterimageColor, drawn = Drawn(ghost);
                Assert.That(drawn.r, Is.EqualTo(yellow.r).Within(1e-4f), "Drawn in the set yellow…");
                Assert.That(drawn.g, Is.EqualTo(yellow.g).Within(1e-4f));
                Assert.That(drawn.b, Is.EqualTo(yellow.b).Within(1e-4f));
                Assert.That(drawn.a, Is.GreaterThan(0f).And.LessThanOrEqualTo(settings.EmpowermentAfterimageAlpha + 1e-4f),
                    "…see-through…");
                if (arena.EnemyBreakAura.HasRequiredAssets)
                {
                    Assert.That(ghost.sharedMaterial.shader.name, Is.EqualTo("TurnLimbo/Duel Break Silhouette"),
                        "…as a flat silhouette in the colour, not a tinted copy…");
                    // URP 17 hands the sprite colour to its own sprite shaders per draw, never to this one.
                    Assert.That(ghost.color, Is.EqualTo(Color.white), "…its colour in the silhouette's tint, not the sprite colour…");
                    Assert.That(DuelBreakAura.OutlineColor(ghost), Is.EqualTo(drawn));
                }
                arena.Tick(Frame, Frame);
                Assert.That(ghost.enabled && ghost.gameObject.activeInHierarchy, Is.True);
                Assert.That(Drawn(ghost).a, Is.LessThan(drawn.a).And.GreaterThan(0f), "…fading out as drawn.");

                arena.Reset();
                Assert.That(arena.ActiveAuraAfterimageCount, Is.Zero, "A new duel or a retry leaves none behind…");
                Assert.That(arena.EnemyPowerAura.IsAuraOn, Is.False);
                for (int frame = 0; frame < 20; frame++) arena.Tick(Frame, Frame);
                Assert.That(arena.ActiveAuraAfterimageCount, Is.Zero, "…nor any to come.");

                arena.EnemyPowerAura.SetAura(true);
                for (int frame = 0; frame < 20; frame++) arena.Tick(Frame, Frame);
                Assert.That(arena.ActiveAuraAfterimageCount, Is.GreaterThan(0));
                arena.EnemyPowerAura.SetAura(false);
                Assert.That(arena.ActiveAuraAfterimageCount, Is.Zero, "Her aura off at once takes them at once.");
            }
            finally
            {
                arena.Dispose();
                Object.Destroy(host);
                Object.Destroy(settings);
            }
            yield return null;
        }

        private static SpriteRenderer[] Shown(DuelAuraAfterimages ghosts)
            => ghosts.Root.GetComponentsInChildren<SpriteRenderer>().Where(view => view.enabled).ToArray();

        // What the ghost is drawn with: the silhouette's tint, or the sprite colour without the silhouette material.
        private static Color Drawn(SpriteRenderer ghost) => DuelStepAfterimages.DrawnColor(ghost);

        // How far along the arena camera's view an object stands; the nearer of two at the same order draws over the other.
        private static float Depth(LegacyArenaView arena, Transform target)
            => Vector3.Dot(target.position - arena.ArenaCamera.transform.position, arena.ArenaCamera.transform.forward);
    }
}
