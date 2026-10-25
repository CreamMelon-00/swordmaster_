using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class BreakShieldPlayModeTests
    {
        [UnityTest]
        public IEnumerator ShieldFollowsItsActorAndCameraZoom_WithoutBlockingInput()
        {
            var owner = new GameObject("Shield Follow Test");
            var canvasRoot = new GameObject("Shield Test Canvas", typeof(RectTransform), typeof(Canvas));
            canvasRoot.transform.SetParent(owner.transform);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var camera = new GameObject("Shield Test Camera").AddComponent<Camera>();
            camera.transform.SetParent(owner.transform);
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 5;
            var player = new GameObject("Shield Player").transform;
            player.SetParent(owner.transform); player.position = new Vector3(-2, 0, 0);
            var enemy = new GameObject("Shield Enemy").transform;
            enemy.SetParent(owner.transform); enemy.position = new Vector3(2, 0, 0);
            var cue = new DuelBreakImpactCue(canvasRoot.transform);
            try
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
                cue.Tick(0, camera, player, enemy);
                cue.Show(true); cue.Show(false);
                Image playerImage = Shield(canvasRoot, true), enemyImage = Shield(canvasRoot, false);
                Assert.That(playerImage.sprite, Is.Not.Null);
                Assert.That(playerImage.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(playerImage.raycastTarget, Is.False);
                Assert.That(enemyImage.raycastTarget, Is.False);
                AssertPosition(playerImage, camera, player);
                AssertPosition(enemyImage, camera, enemy);
                AssertSharedCenter(canvasRoot, true);
                AssertSharedCenter(canvasRoot, false);
                float width = playerImage.rectTransform.sizeDelta.x;
                player.position += Vector3.right;
                camera.transform.position += Vector3.right * .7f;
                camera.orthographicSize = 2.5f;
                cue.Tick(0, camera, player, enemy);
                AssertPosition(playerImage, camera, player);
                AssertPosition(enemyImage, camera, enemy);
                AssertSharedCenter(canvasRoot, true);
                AssertSharedCenter(canvasRoot, false);
                Assert.That(playerImage.rectTransform.sizeDelta.x, Is.EqualTo(width * 2).Within(.1f),
                    "Camera focus must enlarge the shield with its actor, not leave a fixed screen sticker.");
                player.position += Vector3.right * 12f;
                cue.Tick(0, camera, player, enemy);
                AssertPosition(playerImage, camera, player);
                AssertSharedCenter(canvasRoot, true);
                camera.transform.position = new Vector3(0, 0, 10);
                cue.Tick(0, camera, player, enemy);
                Assert.That(playerImage.enabled, Is.False, "A target behind the camera cannot leave a visible shield at a fallback position.");
            }
            finally { cue.Dispose(); Object.Destroy(owner); }
        }

        [UnityTest]
        public IEnumerator ShieldUsesRealTime_ExpiresAndResetsBothSides()
        {
            var host = new GameObject("Shield Lifetime Test", typeof(RectTransform), typeof(Canvas));
            host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var cue = new DuelBreakImpactCue(host.transform);
            try
            {
                yield return null;
                cue.Show(true);
                Image player = Shield(host, true), enemy = Shield(host, false);
                Assert.That(player.gameObject.activeSelf, Is.True);
                Assert.That(enemy.gameObject.activeSelf, Is.False);
                Sprite first = player.sprite;
                cue.Tick(float.NaN, null, null, null);
                Assert.That(player.sprite, Is.SameAs(first));
                cue.Tick(.17f, null, null, null);
                Assert.That(player.sprite, Is.Not.SameAs(first));
                Assert.That(player.sprite.name, Is.EqualTo("shield-shatter-04"));
                cue.Show(false);
                cue.Tick(.42f, null, null, null);
                Assert.That(player.gameObject.activeSelf, Is.False);
                Assert.That(enemy.gameObject.activeSelf, Is.True, "The two break clocks are independent.");
                cue.Reset();
                Assert.That(player.gameObject.activeSelf, Is.False);
                Assert.That(enemy.gameObject.activeSelf, Is.False);
                cue.Show(true);
                Assert.That(player.sprite, Is.SameAs(first));
                cue.Tick(.7f, null, null, null);
                Assert.That(player.gameObject.activeSelf, Is.False);
            }
            finally { cue.Dispose(); Object.Destroy(host); }
        }

        [UnityTest]
        public IEnumerator WaveScreenSizeSurvivesRealHudScalingAndCameraFocus()
        {
            var owner = new GameObject("Real HUD Wave Scale Test");
            var camera = new GameObject("Wave Scale Camera").AddComponent<Camera>();
            camera.transform.SetParent(owner.transform);
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 5;
            var player = new GameObject("Wave Scale Player").transform;
            player.SetParent(owner.transform);
            using (var art = new LegacyDuelArt())
            using (var hud = new LegacyCombatHud(owner.transform, art, null, null, null))
            using (var cue = new DuelBreakImpactCue(hud.Root.transform))
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
                cue.Tick(0, camera, player, player); cue.Show(true);
                float before = WaveScreenRadius(hud.Root);
                hud.Root.GetComponent<CanvasScaler>().referenceResolution *= 2;
                yield return null;
                Canvas.ForceUpdateCanvases(); cue.Tick(0, camera, player, player);
                Assert.That(WaveScreenRadius(hud.Root), Is.EqualTo(before).Within(.5f),
                    "The real HUD's reference resolution must not shrink the wave into the character.");
                camera.orthographicSize *= .5f;
                cue.Tick(0, camera, player, player);
                Assert.That(WaveScreenRadius(hud.Root), Is.EqualTo(before * 2).Within(.5f),
                    "The wave must enlarge with the shield and actor during the break close-up.");
            }
            Object.Destroy(owner); yield return null;
        }

        [UnityTest]
        public IEnumerator RenderShieldFramesOnTheActualArenaForVisualReview()
        {
            var owner = new GameObject("Shield Arena Preview");
            var target = new RenderTexture(960, 540, 24);
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            using (var art = new LegacyDuelArt())
            using (var arena = LegacyArenaView.Create(owner.transform, art))
            using (var hud = new LegacyCombatHud(owner.transform, art, null, null, null))
            {
                var canvasRoot = hud.Root;
                var camera = arena.ArenaCamera;
                camera.cullingMask |= 1;
                camera.targetTexture = target;
                var canvas = canvasRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var cue = new DuelBreakImpactCue(canvasRoot.transform);
                var previous = RenderTexture.active;
                var hidden = new List<Renderer>();
                var hiddenCanvases = new List<Canvas>();
                try
                {
                    yield return null;
                    arena.SetResistanceBroken(true, true);
                    arena.Tick(.1f, .1f);
                    // Real attacks/recoil displace GPU vertices while keeping the actor Transform still.
                    var displacement = new MaterialPropertyBlock();
                    arena.PlayerRenderer.GetPropertyBlock(displacement);
                    displacement.SetVector("_AttackOffset", new Vector4(1.1f, .1f, 0, 0));
                    arena.PlayerRenderer.SetPropertyBlock(displacement);
                    arena.EnemyRenderer.GetPropertyBlock(displacement);
                    displacement.SetVector("_AttackOffset", new Vector4(-.75f, .1f, 0, 0));
                    arena.EnemyRenderer.SetPropertyBlock(displacement);
                    arena.PlayerBreakAura.RefreshPose();
                    arena.EnemyBreakAura.RefreshPose();
                    camera.transform.rotation = Quaternion.Euler(0, 0, -6);
                    camera.orthographicSize = 4.8f;
                    hud.Refresh(new LegacyQueuedDuel(), 8f, false, -1, camera,
                        arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
                    foreach (Renderer other in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                        if (other.enabled && !other.transform.IsChildOf(owner.transform))
                        { hidden.Add(other); other.enabled = false; }
                    foreach (Canvas other in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                        if (other.enabled && !other.transform.IsChildOf(owner.transform))
                        { hiddenCanvases.Add(other); other.enabled = false; }
                    // Isolate the preview from the bootstrapped battle already present in the test scene.
                    foreach (Transform child in owner.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
                    camera.cullingMask = 1 << 30;
                    Canvas.ForceUpdateCanvases();
                    string directory = Path.GetFullPath("Logs/ShieldBreak/frames");
                    Directory.CreateDirectory(directory);
                    for (int frame = 0; frame < 14; frame++)
                    {
                        cue.Reset();
                        cue.Tick(0, camera, arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
                        cue.Show(true);
                        cue.Show(false);
                        cue.Tick(frame / 24f, camera, arena.PlayerRenderer.transform, arena.EnemyRenderer.transform);
                        Vector3 torsoScreen = camera.WorldToScreenPoint(arena.PlayerRenderer.transform.TransformPoint(new Vector3(1.1f, -.05f, 0)));
                        var shield = Shield(canvasRoot, true);
                        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)shield.transform.parent,
                            torsoScreen, camera, out Vector2 torso);
                        Assert.That(Vector2.Distance(shield.rectTransform.anchoredPosition, torso), Is.LessThan(.1f),
                            "The glyph must follow the drawn torso including shader-only attack/recoil displacement.");
                        var enemyShield = Shield(canvasRoot, false);
                        Vector3 enemyTorsoScreen = camera.WorldToScreenPoint(arena.EnemyRenderer.transform.TransformPoint(new Vector3(-.75f, -.05f, 0)));
                        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)enemyShield.transform.parent,
                            enemyTorsoScreen, camera, out Vector2 enemyTorso);
                        Assert.That(Vector2.Distance(enemyShield.rectTransform.anchoredPosition, enemyTorso), Is.LessThan(.1f));
                        AssertSharedCenter(canvasRoot, true);
                        AssertSharedCenter(canvasRoot, false);
                        Canvas.ForceUpdateCanvases();
                        camera.Render(); RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                        File.WriteAllBytes(Path.Combine(directory, "frame-" + frame.ToString("00") + ".png"), image.EncodeToPNG());
                        if (frame == 4)
                        {
                            Color32[] withWave = image.GetPixels32();
                            var playerWave = Wave(canvasRoot, true).GetComponent<DuelSkillActivationBurst>();
                            var enemyWave = Wave(canvasRoot, false).GetComponent<DuelSkillActivationBurst>();
                            playerWave.enabled = enemyWave.enabled = false;
                            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                            image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                            Color32[] withoutWave = image.GetPixels32();
                            int visiblePixels = 0;
                            for (int i = 0; i < withWave.Length; i++)
                                if (Mathf.Abs(withWave[i].r - withoutWave[i].r) +
                                    Mathf.Abs(withWave[i].g - withoutWave[i].g) +
                                    Mathf.Abs(withWave[i].b - withoutWave[i].b) > 12) visiblePixels++;
                            playerWave.enabled = enemyWave.enabled = true;
                            Assert.That(visiblePixels, Is.GreaterThan(100),
                                "The wave must visibly render through the actual combat HUD, not only in an empty preview canvas.");
                        }
                    }
                    Assert.That(Shield(canvasRoot, true).sprite, Is.Not.Null);
                }
                finally
                {
                    cue.Dispose(); camera.targetTexture = null; RenderTexture.active = previous;
                    foreach (Renderer other in hidden) if (other != null) other.enabled = true;
                    foreach (Canvas other in hiddenCanvases) if (other != null) other.enabled = true;
                    target.Release(); Object.Destroy(target); Object.Destroy(image);
                }
            }
            Object.Destroy(owner);
            yield return null;
        }

        private static Image Shield(GameObject root, bool player) => root.transform
            .Find("Break Impact Cues/" + (player ? "Player" : "Enemy") + " Break Shield").GetComponent<Image>();

        private static void AssertSharedCenter(GameObject root, bool player)
        {
            var wave = Wave(root, player);
            Assert.That(Vector2.Distance(wave.anchoredPosition, Shield(root, player).rectTransform.anchoredPosition),
                Is.LessThan(.01f), "The wave must remain centred on the shield, including zoom and screen edges.");
        }

        private static RectTransform Wave(GameObject root, bool player) => (RectTransform)root.transform.Find(
            "Break Impact Cues/" + (player ? "Player" : "Enemy") + " Break Impact");

        private static float WaveScreenRadius(GameObject root)
        {
            RectTransform wave = Wave(root, true);
            return Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null, wave.position),
                RectTransformUtility.WorldToScreenPoint(null, wave.TransformPoint(Vector3.right * 100)));
        }

        private static void AssertPosition(Image image, Camera camera, Transform actor)
        {
            var root = (RectTransform)image.transform.parent;
            Vector3 screen = camera.WorldToScreenPoint(actor.TransformPoint(DuelStepHud.ActorBodyLocalOffset));
            Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 expected), Is.True);
            Assert.That(Vector2.Distance(image.rectTransform.anchoredPosition, expected), Is.LessThan(.1f));
        }
    }
}
