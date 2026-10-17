using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class QueueComparisonPlayModeTests : InputTestFixture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator TabAndArrows_CompareTheSameReservedIndex_KeepSideSpecificFacts_AndHideOnlyMissingSlots()
        {
            yield return null;
            using (var scope = new CampaignScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                LegacyCombatHud hud = scope.Controller.Hud;
                scope.QueuePair();
                int act = scope.Controller.Session.Act;
                Press(keyboard.tabKey);
                yield return null;
                scope.Advance(0f, keyboard);
                AssertPair(hud, scope.Duel.EnemyQueue[0], scope.Duel.PlayerQueue[0]);
                Assert.That(CardText(hud.ExplanationRoot, "Comparison Caption"), Is.EqualTo("상대 기술"));
                Assert.That(CardText(hud.ComparisonPlayerRoot, "Comparison Caption"), Is.EqualTo("대응 기술"));
                Assert.That(hud.Root.transform.Find("Skill Explain").gameObject.activeSelf, Is.False);

                Press(keyboard.rightArrowKey);
                yield return null;
                scope.Advance(0f, keyboard);
                AssertPair(hud, scope.Duel.EnemyQueue[1], scope.Duel.PlayerQueue[1]);
                Assert.That(CardText(hud.ComparisonPlayerRoot, "Keyword 1 Text"), Is.EqualTo("순환 2/9"));
                Assert.That(CardText(hud.ExplanationRoot, "Keyword 1 Text"), Is.EqualTo("순환 0/9"));
                Assert.That(CardText(hud.ComparisonPlayerRoot, "ACT Value"), Is.EqualTo("2"));
                Assert.That(CardText(hud.ComparisonPlayerRoot, "Comparison Player Detail Values"), Is.EqualTo("15–20"));
                Assert.That(hud.EnemyForecastNote, Is.EqualTo(scope.Duel.ForecastTurn()[1].Summary));
                foreach (Text text in hud.ComparisonPlayerRoot.GetComponentsInChildren<Text>())
                    Assert.That(text.text, Does.Not.Contain("예약할 수").And.Not.Contain("지금 예약"), "The player card reads an existing reservation, not the lane's next possible reservation.");
                Release(keyboard.rightArrowKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Press(keyboard.rightArrowKey);
                yield return null;
                scope.Advance(0f, keyboard);
                AssertPair(hud, scope.Duel.EnemyQueue[2], null);
                Assert.That(scope.Duel.ForecastTurn()[2].PlayerSkill, Is.Not.Null,
                    "The fixture has an available automatic counter: it must not be presented as a manually reserved matching card.");
                Release(keyboard.rightArrowKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Press(keyboard.rightArrowKey);
                yield return null;
                scope.Advance(0f, keyboard);
                AssertPair(hud, scope.Duel.EnemyQueue[2], null);
                Release(keyboard.rightArrowKey);
                yield return null;
                scope.Advance(0f, keyboard);
                for (int index = 0; index < 3; index++)
                {
                    Press(keyboard.leftArrowKey);
                    yield return null;
                    scope.Advance(0f, keyboard);
                    Release(keyboard.leftArrowKey);
                    yield return null;
                    scope.Advance(0f, keyboard);
                }
                AssertPair(hud, scope.Duel.EnemyQueue[0], scope.Duel.PlayerQueue[0]);
                Assert.That(scope.Duel.PlayerQueue.Count, Is.EqualTo(2));
                Assert.That(scope.Duel.Act, Is.EqualTo(act));

                using (var render = new ComparisonRender(scope.Controller))
                {
                    string output = Environment.GetEnvironmentVariable("SWORDMASTER_QUEUE_COMPARE_CAPTURE_DIR");
                    if (!string.IsNullOrEmpty(output)) Directory.CreateDirectory(output);
                    foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1280, 960), new Vector2Int(1920, 810) })
                    {
                        render.Resize(size);
                        yield return null;
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                        Assert.That(render.CanvasRect.width * render.Scale, Is.EqualTo(size.x).Within(1f));
                        Assert.That(render.CanvasRect.height * render.Scale, Is.EqualTo(size.y).Within(1f));
                        foreach (int selected in new[] { 1, 2 })
                        {
                            scope.Select(selected);
                            scope.Advance(0f, keyboard);
                            Canvas.ForceUpdateCanvases();
                            AssertComparisonLayout(hud, render.Camera, size, selected == 1);
                            yield return null;
                            if (!string.IsNullOrEmpty(output))
                                render.Save(Path.Combine(output, size.x + "x" + size.y + (selected == 1 ? "-pair-hone.png" : "-enemy-only.png")));
                        }
                    }
                }

                Release(keyboard.tabKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(scope.Controller.QueueBreath(), Is.True);
                scope.Select(2);
                Press(keyboard.tabKey);
                yield return null;
                scope.Advance(0f, keyboard);
                AssertPair(hud, scope.Duel.EnemyQueue[2], LegacyCommonActions.Breathe);
                Assert.That(CardText(hud.ComparisonPlayerRoot, "Effect"), Is.EqualTo(LegacyCommonActions.Breathe.Description));
                Assert.That(hud.ComparisonPlayerRoot.Find("Skill Summary/Skill Attachments").gameObject.activeSelf, Is.False,
                    "A reserved breath has its real description and no invented weapon statistics.");
                Release(keyboard.tabKey);
            }
        }

        [UnityTest]
        public IEnumerator ClosingTab_RestoresOrdinaryLaneCardsAndInputs_WhileTheClockSlowsAndTheCameraKeepsItsPlanningView()
        {
            yield return null;
            using (var scope = new CampaignScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                scope.QueuePair();
                LegacyCombatHud hud = scope.Controller.Hud;
                float before = scope.Controller.TurnTimeRemaining;
                Press(keyboard.tabKey);
                yield return null;
                scope.Advance(0f, keyboard);
                scope.Advance(1f, keyboard);
                Assert.That(scope.Controller.TurnTimeRemaining, Is.EqualTo(before - LegacyLaneHold.InspectionTimeScale).Within(.0001f));
                Assert.That(scope.Controller.PlanningTimeScale, Is.EqualTo(LegacyLaneHold.InspectionTimeScale));
                float planningTime = scope.Controller.TurnTimeRemaining;
                SettlePlanning(scope.Controller.ArenaView, planningTime, true);
                Camera camera = scope.Controller.ArenaView.ArenaCamera;
                Vector3 position = camera.transform.localPosition;
                Quaternion rotation = camera.transform.localRotation;
                float zoom = camera.orthographicSize;
                SettlePlanning(scope.Controller.ArenaView, planningTime, false);
                Assert.That(Vector3.Distance(camera.transform.localPosition, position), Is.LessThan(.001f), "At the same planning time Tab cannot retarget the camera to the enemy.");
                Assert.That(Quaternion.Angle(camera.transform.localRotation, rotation), Is.LessThan(.001f));
                Assert.That(camera.orthographicSize, Is.EqualTo(zoom).Within(.001f));
                Release(keyboard.tabKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(hud.IsQueueComparisonVisible, Is.False);

                KeyControl[] keys = { keyboard.qKey, keyboard.wKey, keyboard.eKey };
                RectTransform ordinary = (RectTransform)hud.Root.transform.Find("Skill Explain");
                for (int lane = 0; lane < keys.Length; lane++)
                {
                    Press(keys[lane]);
                    yield return null;
                    scope.Advance(0f, keyboard);
                    yield return null;
                    scope.Advance(scope.HoldSeconds, keyboard);
                    Assert.That(ordinary.gameObject.activeSelf, Is.True);
                    string name = CardText(ordinary, "Name");
                    Vector2 size = ordinary.sizeDelta;
                    Vector2 home = ordinary.anchoredPosition;
                    Assert.That(size.x, Is.EqualTo(460f * LegacyCombatHud.PlayerExplanationScale).Within(.01f));
                    Press(keyboard.tabKey);
                    yield return null;
                    scope.Advance(0f, keyboard);
                    Assert.That(hud.IsQueueComparisonVisible, Is.True);
                    Assert.That(ordinary.gameObject.activeSelf, Is.False);
                    Release(keyboard.tabKey);
                    yield return null;
                    scope.Advance(0f, keyboard);
                    Release(keys[lane]);
                    yield return null;
                    scope.Advance(0f, keyboard);
                    Assert.That(scope.Duel.PlayerQueue.Count, Is.EqualTo(2), "Releasing a lane consumed by Tab must not accidentally reserve it.");
                    Press(keys[lane]);
                    yield return null;
                    scope.Advance(0f, keyboard);
                    yield return null;
                    scope.Advance(scope.HoldSeconds, keyboard);
                    Assert.That(hud.IsQueueComparisonVisible, Is.False);
                    Assert.That(hud.ComparisonPlayerRoot.gameObject.activeSelf, Is.False);
                    Assert.That(ordinary.gameObject.activeSelf, Is.True);
                    Assert.That(CardText(ordinary, "Name"), Is.EqualTo(name));
                    Assert.That(ordinary.sizeDelta, Is.EqualTo(size));
                    Assert.That(Vector2.Distance(ordinary.anchoredPosition, home), Is.LessThan(.01f), "The regular held card returns to its lane, not the comparison's edge.");
                    Assert.That(ordinary.localScale, Is.EqualTo(Vector3.one));
                    Release(keys[lane]);
                    yield return null;
                    scope.Advance(0f, keyboard);
                }
                Assert.That(scope.Duel.PlayerQueue.Count, Is.EqualTo(2));
                Assert.That(scope.Controller.QueueLane(2), Is.True, "The normal reservation commands work again after Tab closes.");
            }
        }

        [UnityTest]
        public IEnumerator MirroredVersus_UsesEachViewersEnemyAndOwnReservedSlots_WithoutSlowingEitherClock()
        {
            yield return null;
            using (var host = new VersusScope(0))
            using (var guest = new VersusScope(1))
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                for (int index = 0; index < 2; index++)
                {
                    Accepted(host, guest, 0, OnlineVersusAction.QueueLane, index);
                    Accepted(host, guest, 1, OnlineVersusAction.QueueLane, index);
                }
                Accepted(host, guest, 0, OnlineVersusAction.Pass, -1);
                Accepted(host, guest, 1, OnlineVersusAction.QueueLane, 2);
                Press(keyboard.tabKey);
                yield return null;
                host.Tick(0f, keyboard); guest.Tick(0f, keyboard);
                AssertMirror(host, guest, 0);
                float hostClock = host.Versus.LeftClock, guestClock = guest.Versus.LeftClock;
                host.Tick(.5f, keyboard); guest.Tick(.5f, keyboard);
                Assert.That(host.Versus.LeftClock, Is.EqualTo(hostClock - .5f).Within(.0001f));
                Assert.That(guest.Versus.LeftClock, Is.EqualTo(guestClock - .5f).Within(.0001f));
                Assert.That(host.Versus.TryQueueLane(0), Is.False);
                Press(keyboard.rightArrowKey);
                yield return null;
                host.Tick(0f, keyboard); guest.Tick(0f, keyboard);
                AssertMirror(host, guest, 1);
                Release(keyboard.rightArrowKey);
                yield return null;
                host.Tick(0f, keyboard); guest.Tick(0f, keyboard);
                Press(keyboard.rightArrowKey);
                yield return null;
                host.Tick(0f, keyboard); guest.Tick(0f, keyboard);
                AssertPair(host.Versus.BattleHud, host.Versus.Match.Right.Queue[2], null);
                AssertPair(guest.Versus.BattleHud, guest.Versus.Match.Left.Queue[1], guest.Versus.Match.Right.Queue[1]);
                Assert.That(host.Versus.Match.Left.Queue.Count, Is.EqualTo(2));
                Assert.That(host.Versus.Match.Right.Queue.Count, Is.EqualTo(3));
                Assert.That(guest.Versus.Match.Left.Queue.Count, Is.EqualTo(2));
                Assert.That(guest.Versus.Match.Right.Queue.Count, Is.EqualTo(3));
                Release(keyboard.rightArrowKey);
                Release(keyboard.tabKey);
                yield return null;
                host.Tick(0f, keyboard); guest.Tick(0f, keyboard);
                Assert.That(host.Versus.BattleHud.IsQueueComparisonVisible, Is.False);
                Assert.That(guest.Versus.BattleHud.IsQueueComparisonVisible, Is.False);
            }
        }

        private static void AssertPair(LegacyCombatHud hud, LegacySkill enemy, LegacySkill own)
        {
            Assert.That(hud.IsQueueComparisonVisible, Is.True);
            Assert.That(hud.ExplanationRoot.gameObject.activeInHierarchy, Is.True);
            Assert.That(CardText(hud.ExplanationRoot, "Name"), Is.EqualTo(enemy.Name));
            Assert.That(hud.ComparisonPlayerRoot.gameObject.activeSelf, Is.EqualTo(own != null));
            if (own != null) Assert.That(CardText(hud.ComparisonPlayerRoot, "Name"), Is.EqualTo(own.Name));
        }
        private static string CardText(Transform card, string name)
        {
            foreach (Text text in card.GetComponentsInChildren<Text>(true)) if (text.name == name) return text.text;
            Assert.Fail("Missing card label: " + card.name + "/" + name);
            return null;
        }
        private static void AssertMirror(VersusScope host, VersusScope guest, int index)
        {
            AssertPair(host.Versus.BattleHud, host.Versus.Match.Right.Queue[index], host.Versus.Match.Left.Queue[index]);
            AssertPair(guest.Versus.BattleHud, guest.Versus.Match.Left.Queue[index], guest.Versus.Match.Right.Queue[index]);
        }
        private static void AssertComparisonLayout(LegacyCombatHud hud, Camera camera, Vector2Int size, bool paired)
        {
            Rect right = ScreenRect(hud.ExplanationRoot, camera);
            AssertInside(right, size);
            Assert.That(right.xMin, Is.GreaterThanOrEqualTo(size.x * .6f - 1f), "The enemy card stays on the right, including when there is no matching player reservation.");
            if (!paired) { Assert.That(hud.ComparisonPlayerRoot.gameObject.activeSelf, Is.False); return; }
            Rect left = ScreenRect(hud.ComparisonPlayerRoot, camera);
            AssertInside(left, size);
            Assert.That(left.xMax, Is.LessThanOrEqualTo(size.x * .4f + 1f), "The player's reservation stays on the left.");
            Assert.That(right.yMax, Is.EqualTo(left.yMax).Within(1f));
            Assert.That(Vector2.Distance(right.size, left.size), Is.LessThan(.01f));
            Assert.That(left.Overlaps(right), Is.False);
        }
        private static Rect ScreenRect(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static void AssertInside(Rect rect, Vector2Int size)
        {
            Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(-1f));
            Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(-1f));
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(size.x + 1f));
            Assert.That(rect.yMax, Is.LessThanOrEqualTo(size.y + 1f));
        }
        private static void SettlePlanning(LegacyArenaView arena, float seconds, bool inspect)
        {
            for (int frame = 0; frame < 80; frame++)
            {
                arena.SetPlanningState(seconds, inspect);
                arena.Tick(0f, .05f);
            }
        }
        private static LegacySkill Skill(int id, string name, int lane)
            => new LegacySkill(id, name, 0, 4, 6, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, lane, string.Empty, iconId: 1);
        private static LegacySkill Hone()
            => new LegacySkill(46, "연마", 0, 5, 10, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 1, string.Empty, iconId: 1);

        private sealed class CampaignScope : IDisposable
        {
            private readonly bool wasEnabled;
            private readonly LegacyQueuedDuel original;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }
            public LegacyQueuedDuel Duel { get; }
            public float HoldSeconds => Controller.PresentationSettings.LaneTapSeconds + Controller.PresentationSettings.ExplanationHoldSeconds + .02f;
            public CampaignScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                wasEnabled = Controller.enabled; original = Controller.Session; Controller.enabled = false;
                try
                {
                    Set("mission", null); Set("guide", null);
                    var counter = Skill(88903, "자동 반격", 2);
                    Duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                        new[] { Skill(88901, "예약한 첫 기술", 0), Hone(), counter },
                        new[] { Skill(88911, "상대 첫 기술", 0), Hone(), Skill(88913, "상대 셋째 기술", 2) },
                        new[] { 3 }, 7, playerCounter: new LegacyCounter(counter), meshPercent: 0);
                    Set("session", Duel);
                    advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                        typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
                    typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", Private).Invoke(Controller, new object[] { null, null });
                    // ResetBattlePresentation starts a fresh attempt with Session.Reset(), which clears per-battle uses.
                    ((Dictionary<int, int>)typeof(LegacyQueuedDuel).GetField("cycleUses", Private).GetValue(Duel))[46] = 2;
                    Advance(0f, null);
                    Assert.That(Controller.CanChoose, Is.True);
                }
                catch { Dispose(); throw; }
            }
            public void QueuePair() { Assert.That(Controller.QueueLane(0), Is.True); Assert.That(Controller.QueueLane(1), Is.True); }
            public void Advance(float seconds, Keyboard keyboard) => advance(seconds, keyboard);
            public void Select(int index) => Set("inspectingEnemy", index);
            private void Set(string name, object value) => typeof(DuelPrototypeController).GetField(name, Private).SetValue(Controller, value);
            public void Dispose() { Set("session", original); Controller.RestartMatch(); Controller.enabled = wasEnabled; }
        }

        private static void Accepted(VersusScope host, VersusScope guest, int player, OnlineVersusAction action, int lane)
        {
            Assert.That(host.Versus.ApplyOnlineAcceptedAction(player, action, lane, 1), Is.True);
            Assert.That(guest.Versus.ApplyOnlineAcceptedAction(player, action, lane, 1), Is.True);
        }
        private sealed class VersusScope : IDisposable
        {
            private readonly DuelPrototypeController scene;
            private readonly bool wasEnabled;
            private readonly GameObject root;
            private readonly LegacyDuelArt art;
            private readonly DuelPresentationSettings settings;
            private readonly LegacyArenaView arena;
            public LocalVersusController Versus { get; }
            public VersusScope(int localPlayer)
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { wasEnabled = scene.enabled; scene.enabled = false; }
                root = new GameObject("Queue Comparison Versus " + localPlayer);
                art = new LegacyDuelArt(); settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                arena = LegacyArenaView.Create(root.transform, art, settings);
                Versus = new LocalVersusController(root.transform, art, arena, () => { }, settings);
                Versus.StartOnline(7, localPlayer, false, 0,
                    new[] { Skill(88921, "왼쪽 Q", 0), Skill(88922, "왼쪽 W", 1), Skill(88923, "왼쪽 E", 2) },
                    new[] { Skill(88931, "오른쪽 Q", 0), Skill(88932, "오른쪽 W", 1), Skill(88933, "오른쪽 E", 2) },
                    (action, lane) => { }, () => { });
            }
            public void Tick(float seconds, Keyboard keyboard) => Versus.Tick(seconds, keyboard);
            public void Dispose()
            {
                Versus.Dispose(); arena.Dispose(); art.Dispose(); Object.Destroy(settings); Object.Destroy(root);
                if (scene != null) scene.enabled = wasEnabled;
            }
        }

        private sealed class ComparisonRender : IDisposable
        {
            private readonly Canvas canvas;
            private readonly CanvasScaler scaler;
            private readonly RenderMode mode;
            private readonly Camera oldWorldCamera;
            private readonly CanvasScaler.ScaleMode scaleMode;
            private readonly float oldScale, plane, aspect;
            private readonly int culling;
            private readonly RenderTexture oldTarget;
            private readonly Dictionary<GameObject, int> layers = new Dictionary<GameObject, int>();
            private RenderTexture target;
            public Camera Camera { get; }
            public float Scale => canvas.scaleFactor;
            public Rect CanvasRect => ((RectTransform)canvas.transform).rect;
            public ComparisonRender(DuelPrototypeController controller)
            {
                canvas = controller.Hud.Root.GetComponent<Canvas>(); scaler = canvas.GetComponent<CanvasScaler>();
                Camera = controller.ArenaView.ArenaCamera;
                mode = canvas.renderMode; oldWorldCamera = canvas.worldCamera; plane = canvas.planeDistance;
                scaleMode = scaler.uiScaleMode; oldScale = scaler.scaleFactor;
                oldTarget = Camera.targetTexture; culling = Camera.cullingMask; aspect = Camera.aspect;
                foreach (Transform node in controller.Hud.Root.GetComponentsInChildren<Transform>(true))
                { layers[node.gameObject] = node.gameObject.layer; node.gameObject.layer = 31; }
                Camera.cullingMask |= 1 << 31;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera;
                canvas.planeDistance = Mathf.Max(1f, Camera.nearClipPlane + .1f);
            }
            public void Resize(Vector2Int size)
            {
                Camera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                Camera.targetTexture = target; Camera.aspect = size.x / (float)size.y;
                float logX = Mathf.Log(size.x / scaler.referenceResolution.x, 2f);
                float logY = Mathf.Log(size.y / scaler.referenceResolution.y, 2f);
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = Mathf.Pow(2f, Mathf.Lerp(logX, logY, scaler.matchWidthOrHeight));
            }
            public void Save(string path)
            {
                RenderTexture old = RenderTexture.active;
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally { RenderTexture.active = old; Object.Destroy(texture); }
            }
            public void Dispose()
            {
                Camera.targetTexture = oldTarget; Camera.cullingMask = culling; Camera.aspect = aspect;
                canvas.renderMode = mode; canvas.worldCamera = oldWorldCamera; canvas.planeDistance = plane;
                scaler.uiScaleMode = scaleMode; scaler.scaleFactor = oldScale;
                foreach (KeyValuePair<GameObject, int> layer in layers) if (layer.Key != null) layer.Key.layer = layer.Value;
                if (target != null) { target.Release(); Object.Destroy(target); }
            }
        }
    }
}
