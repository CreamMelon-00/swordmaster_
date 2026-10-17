using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class SlowMotionFramingPlayModeTests
    {
        [UnityTest]
        public IEnumerator StepAndDecisiveSlowMotion_EnlargeAndFrameThePlayer_WithHerFeetAboveTheHud_AtBothAspects()
        {
            yield return null;
            using (var scope = new FocusScope())
            foreach (Vector2Int viewport in new[] { new Vector2Int(1280, 720), new Vector2Int(960, 720) })
            {
                scope.Resize(viewport);
                foreach (string moment in new[] { "dodge", "pressure", "decisive" })
                {
                    scope.BeginExchange();
                    scope.Advance(.8f, directed: true);
                    float baselineSize = scope.Arena.ArenaCamera.orthographicSize;
                    scope.Capture(viewport.x + "x" + viewport.y + "-" + moment + "-before");
                    if (moment == "decisive") scope.Arena.PresentDecisiveFocus(viewport.x == 1280);
                    else scope.Arena.PerformStep(moment == "dodge" ? LegacyStepAction.Dodge : LegacyStepAction.Pressure, true);
                    scope.Advance(.24f, directed: true);
                    scope.Capture(viewport.x + "x" + viewport.y + "-" + moment + "-focus");

                    Camera camera = scope.Arena.ArenaCamera;
                    Vector3 player = camera.WorldToViewportPoint(scope.Arena.PlayerRenderer.transform.position);
                    Vector3 opponent = camera.WorldToViewportPoint(scope.Arena.EnemyRenderer.transform.position);
                    Assert.That(camera.orthographicSize, Is.LessThan(baselineSize - .15f),
                        "Slow motion shows a visibly larger protagonist than the ordinary exchange. " + viewport + " " + moment);
                    Assert.That(player.x, Is.InRange(.34f, .66f), "The player owns the close-up, not the opponent who received the hit.");
                    Assert.That(Mathf.Abs(player.x - .5f), Is.LessThan(Mathf.Abs(opponent.x - .5f)),
                        "Other fighters can leave the frame rather than pull the protagonist out of her focus.");
                    Vector3 feet = camera.WorldToViewportPoint(scope.Arena.PlayerGroundPosition);
                    Vector3 head = camera.WorldToViewportPoint(scope.Arena.PlayerRenderer.transform.position + Vector3.up * 1.65f);
                    Assert.That(feet.y, Is.GreaterThan(scope.Hud.QueuePanelFloor + .02f));
                    Assert.That(head.y, Is.LessThan(.94f), "The close-up retains the head as well as the planted feet.");
                    Assert.That(scope.Arena.ActiveEnemyIndex, Is.Zero, "Camera focus cannot hand off to a waiting opponent.");
                    Assert.That(scope.Duel.CurrentSlot.HitsResolved, Is.Zero, "The presentation cannot resolve a previewed skill.");
                    Assert.That(scope.Duel.Player.Health, Is.EqualTo(1000));
                    Assert.That(scope.Duel.Enemy.Health, Is.EqualTo(1000));
                }
            }
            yield return null;
        }

        private sealed class FocusScope : IDisposable
        {
            private readonly GameObject root = new GameObject("Slow Motion Framing Test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            private readonly DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            private RenderTexture target;
            public readonly LegacyArenaView Arena;
            public readonly LegacyCombatHud Hud;
            public readonly LegacyQueuedDuel Duel;
            private static LegacySkill Attack() => new LegacySkill(98791, "촬영 검증", 0, 5, 5,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty, iconId: 1);

            public FocusScope()
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                JsonUtility.FromJsonOverwrite("{\"battleDriftSpeed\":0,\"impactCameraShake\":0}", settings);
                Arena = LegacyArenaView.Create(root.transform, art, settings);
                Arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor);
                Arena.ConfigureEnemyRoster(new[] { EnemyAppearance.CadetA, EnemyAppearance.CadetB, EnemyAppearance.Student });
                Arena.SetDepthPresentationVisible(true);
                Hud = new LegacyCombatHud(root.transform, art, _ => { }, () => { }, () => { }, settings);
                Duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { Attack() }, new[] { Attack() }, new[] { 1 }, meshPercent: 0);
                Assert.That(Duel.TryQueueLane(0), Is.True);
                Duel.Commit(); Duel.BeginNextSlot();
            }

            public void BeginExchange()
            {
                Arena.Reset();
                Arena.PlayerRenderer.transform.localPosition = new Vector3(-3f, Arena.ActorGroundY - .6f, 0f);
                Arena.GetEnemyRenderer(0).transform.localPosition = new Vector3(1f, Arena.ActorGroundY + .6f, 0f);
                Arena.GetEnemyRenderer(1).transform.localPosition = new Vector3(13f, Arena.ActorGroundY + 1.2f, 0f);
                Arena.GetEnemyRenderer(2).transform.localPosition = new Vector3(18f, Arena.ActorGroundY - 1.2f, 0f);
                Arena.BeginSlot(Attack(), Attack());
                Hud.EndTurn(); Hud.BeginCombat();
            }

            public void Advance(float duration, bool directed)
            {
                int frames = Mathf.CeilToInt(duration / .01f);
                for (int frame = 0; frame < frames; frame++)
                {
                    if (directed) Arena.SetCombatShot(.5f, true, true, false);
                    Arena.SetHudFloor(Hud.QueuePanelFloor);
                    Arena.Tick(0f, duration / frames);
                    Hud.SetCombatLetterboxAccent(Arena.CombatLetterboxAccent);
                    Hud.Refresh(Duel, 10f, true, 0, Arena.ArenaCamera,
                        Arena.PlayerRenderer.transform, Arena.EnemyRenderer.transform, .01f, duration / frames);
                    Canvas.ForceUpdateCanvases();
                }
            }

            public void Resize(Vector2Int size)
            {
                Arena.ArenaCamera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                Camera camera = Arena.ArenaCamera;
                camera.targetTexture = target; camera.aspect = size.x / (float)size.y;
                Canvas canvas = Hud.Root.GetComponent<Canvas>(); CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
                canvas.planeDistance = Mathf.Max(1f, camera.nearClipPlane + .1f);
                float logX = Mathf.Log(size.x / scaler.referenceResolution.x, 2f), logY = Mathf.Log(size.y / scaler.referenceResolution.y, 2f);
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = Mathf.Pow(2f, Mathf.Lerp(logX, logY, scaler.matchWidthOrHeight));
            }

            public void Capture(string name)
            {
                string output = Environment.GetEnvironmentVariable("SWORDMASTER_SLOW_FOCUS_CAPTURE_DIR");
                if (string.IsNullOrEmpty(output)) return;
                Directory.CreateDirectory(output);
                // Re-register this test's volume after changing its layer before a synchronous native render.
                Volume volume = root.GetComponentInChildren<Volume>(true);
                bool volumeEnabled = volume != null && volume.enabled;
                if (volume != null) volume.enabled = false;
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
                Camera camera = Arena.ArenaCamera; camera.cullingMask = 1 << 31;
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << 31;
                if (volume != null) volume.enabled = volumeEnabled;
                Canvas.ForceUpdateCanvases(); camera.Render();
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; Object.Destroy(texture); }
            }

            public void Dispose()
            {
                Arena.ArenaCamera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                Hud.Dispose(); Arena.Dispose(); art.Dispose(); Object.Destroy(settings); Object.Destroy(root);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
