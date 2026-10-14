using System;
using System.Collections.Generic;
using System.IO;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.EditorTools
{
    /// <summary>Renders the real HUD builders in an isolated preview scene, without changing the open scene or save.
    /// The fixed project-local request file lets asset tools request the same review as the Editor menu.</summary>
    [InitializeOnLoad]
    public static class AcademyUiPreview
    {
        private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static readonly string RequestPath = Path.Combine(ProjectRoot, "Logs/academy-ui-request.json");
        private static readonly string ResultPath = Path.Combine(ProjectRoot, "Logs/academy-ui-result.json");
        private static double nextCheck;
        private static readonly string[] Pages = { "title", "home", "stages", "loadout", "curriculum", "briefing", "combat", "loading" };

        static AcademyUiPreview()
        {
            EditorApplication.update += Poll;
            TestRunnerApi.RegisterTestCallback(new PreviewTestCallbacks());
        }

        [MenuItem("Turn Limbo/UI/PixelLab UI 미리보기 내보내기", false, 30)]
        public static void ExportAll()
        {
            try
            {
                string[] files = Export("all");
                Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
                File.WriteAllText(ResultPath, JsonUtility.ToJson(new Report { success = true,
                    utc = DateTime.UtcNow.ToString("O"), command = "render", playing = EditorApplication.isPlaying,
                    panelReady = AcademyUiSkin.Panel != null, buttonReady = AcademyUiSkin.Button != null,
                    crestReady = AcademyUiSkin.Crest != null, files = files }, true));
                Debug.Log("UI previews: Logs/AcademyUI");
            }
            catch (Exception error) { Debug.LogException(error); }
        }

        private static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            if (!File.Exists(RequestPath)) return;
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), playing = EditorApplication.isPlaying,
                scene = SceneManager.GetActiveScene().name };
            try
            {
                Request request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
                File.Delete(RequestPath);
                report.command = request.command;
                if (request.command == "render") report.files = Export(request.page ?? "all");
                else if (request.command == "refresh") AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                else if (request.command == "tests" && !EditorApplication.isPlaying)
                {
                    SessionState.SetBool("AcademyUI.TargetedTests", true);
                    var runner = ScriptableObject.CreateInstance<TestRunnerApi>();
                    runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode,
                        groupNames = new[] { "^TurnLimbo.Presentation.Tests.(SkillGearDockPlayModeTests|BreathingHudPlayModeTests|DuelVisualThemePlayModeTests|DuelLoadingHudPlayModeTests)\\." } }));
                }
                else if (request.command == "capture" && EditorApplication.isPlaying)
                {
                    string path = Path.Combine(ProjectRoot, "Logs/AcademyUI/live-game.png");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    ScreenCapture.CaptureScreenshot(path);
                    report.files = new[] { path };
                }
                else if (request.command != "status") throw new ArgumentException("Expected status, refresh, render, tests, or capture in Play Mode.");
                report.panelReady = AcademyUiSkin.Panel != null;
                report.buttonReady = AcademyUiSkin.Button != null;
                report.crestReady = AcademyUiSkin.Crest != null;
                var controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (controller != null)
                    report.gameState = controller.IsInTitle ? "title" : controller.IsInBriefing ? "briefing" :
                        controller.IsInAcademyField ? "academy field" : controller.IsInLobby ? "lobby" : "battle/story";
                report.success = true;
            }
            catch (Exception error) { report.error = error.ToString(); }
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, JsonUtility.ToJson(report, true));
        }

        private static string[] Export(string page)
        {
            foreach (string asset in new[] { "panel-frame", "button-frame", "academy-crest", "title-academy",
                "cycle-lever", "breath-valve", "commit-seal", "gauge-bezel", "gauge-fill", "menu-plaque",
                "gear-wheel", "gear-pinion", "gear-clock", "lever-base", "lever-arm", "valve-base", "valve-wheel" })
                AssetDatabase.ImportAsset("Assets/Game/Resources/AcademyUI/" + asset + ".png", ImportAssetOptions.ForceSynchronousImport);
            AcademyUiSkin.Reload();
            var paths = new List<string>();
            if (page == "motion") for (int frame = 0; frame <= 14; frame++) paths.Add(Render("motion-" + frame.ToString("00")));
            else if (page == "all") foreach (string name in Pages) paths.Add(Render(name));
            else
            {
                if (Array.IndexOf(Pages, page) < 0) throw new ArgumentException("Unknown preview page.");
                paths.Add(Render(page));
            }
            return paths.ToArray();
        }

        private static string Render(string page)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewPreviewScene();
            var host = new GameObject("Academy UI Preview") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(host, scene);
            var cameraHost = new GameObject("UI Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(cameraHost, scene);
            var camera = cameraHost.AddComponent<Camera>();
            camera.scene = scene;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 540f;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = DuelVisualTheme.Track;
            camera.cullingMask = 1 << 5;
            camera.enabled = false;
            cameraHost.AddComponent<UniversalAdditionalCameraData>();
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            var art = new LegacyDuelArt();
            IDisposable hud = null;
            RenderTexture previous = RenderTexture.active;
            Texture2D picture = null;
            try
            {
                if (page == "title")
                {
                    int continued = 0, restarted = 0, online = 0;
                    var title = new TitleHud(host.transform, art, () => continued++, () => restarted++, null, () => online++);
                    title.Show("기사학교 · UI 미리보기", null, false);
                    title.ContinueButton.onClick.Invoke();
                    title.OnlineVersusButton.onClick.Invoke();
                    title.Show("기사학교 · UI 미리보기", null, true);
                    title.PressNewGame();
                    if (!title.IsConfirming || restarted != 0) throw new InvalidOperationException("New game must still ask before overwriting a save.");
                    title.CancelConfirm();
                    if (title.IsConfirming || continued != 1 || online != 1) throw new InvalidOperationException("Title action callback regression.");
                    title.Show(null, null, false);
                    if (title.CanContinue || !title.NewGameButton.transform.Find("Primary Marker").gameObject.activeSelf)
                        throw new InvalidOperationException("A new player's primary action must be New Game.");
                    title.Show("기사학교 · UI 미리보기", null, true);
                    Image background = title.Root.transform.Find("Title Background").GetComponent<Image>();
                    if (background.sprite != Resources.Load<Sprite>("AcademyUI/title-academy"))
                        throw new InvalidOperationException("The title must load its academy background.");
                    hud = title;
                }
                else if (page == "loading")
                {
                    var loading = DuelLoadingHud.Create(host.transform, art.UIFont);
                    loading.Play("기사학교로 돌아가는 중", 1f);
                    loading.Advance(.5f);
                    hud = loading;
                }
                else if (page == "briefing")
                {
                    var briefing = new MissionBriefingHud(host.transform, art, null, () => { });
                    briefing.Show(LobbyMissions.All[0], LobbyMissions.All.Count, true);
                    hud = briefing;
                }
                else if (page == "combat" || page.StartsWith("motion-", StringComparison.Ordinal))
                {
                    var combat = new LegacyCombatHud(host.transform, art, null, null, null, null, () => { }, () => { });
                    combat.SetStage(1, 8, "기사학교 · 훈련 결투");
                    var duel = new LegacyQueuedDuel();
                    combat.Refresh(duel, 8f, false, -1, null, null, null);
                    if (page.StartsWith("motion-", StringComparison.Ordinal))
                    {
                        duel.TryCycleLanes();
                        combat.PlayLaneTurn(new[] { true, true, true });
                        duel.TryQueueBreath();
                        combat.Refresh(duel, 8f, false, -1, null, null, null);
                        combat.Refresh(duel, 8f, false, -1, null, null, null, 0f, int.Parse(page.Substring(7)) * .05f);
                    }
                    hud = combat;
                }
                else
                {
                    var lobby = new CampaignLobbyHud(host.transform, art, null, null, null, null, null, null, null);
                    var run = new CampaignRun();
                    run.TryAbandonBattle();
                    lobby.SetNextMission(LobbyMissions.All[0], true);
                    lobby.Show(run);
                    lobby.ShowTab(page == "home" ? LobbyTab.Home : page == "stages" ? LobbyTab.Stages :
                        page == "loadout" ? LobbyTab.Loadout : LobbyTab.Curriculum);
                    lobby.Root.GetComponent<LobbyScreenTransition>().Finish();
                    hud = lobby;
                }
                foreach (Canvas canvas in host.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1f;
                }
                Canvas.ForceUpdateCanvases();
                foreach (RectTransform rect in host.GetComponentsInChildren<RectTransform>(true))
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Canvas.ForceUpdateCanvases();
                ValidateFrames(host);
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                else camera.Render();
                if (page == "loading") ValidateLoadingFill(host);
                RenderTexture.active = target;
                picture = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
                picture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                picture.Apply();
                string output = Path.Combine(ProjectRoot, "Logs/AcademyUI/" + page + ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllBytes(output, picture.EncodeToPNG());
                return output;
            }
            finally
            {
                RenderTexture.active = previous;
                hud?.Dispose();
                art.Dispose();
                if (picture != null) Object.DestroyImmediate(picture);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cameraHost);
                target.Release();
                Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
                if (activeScene.IsValid() && activeScene.isLoaded) SceneManager.SetActiveScene(activeScene);
            }
        }

        private static void ValidateFrames(GameObject host)
        {
            foreach (AcademyCommandHousing housing in host.GetComponentsInChildren<AcademyCommandHousing>())
            {
                Mesh mesh = housing.canvasRenderer != null ? housing.canvasRenderer.GetMesh() : null;
                if (housing.raycastTarget || mesh == null || mesh.vertexCount < 4 || mesh.bounds.size.x < 1200f)
                    throw new InvalidOperationException("The command housing must render a full-width, non-interactive mesh.");
            }
            foreach (Image image in host.GetComponentsInChildren<Image>(true))
            {
                if (image.name != "PixelLab Frame") continue;
                if (image.raycastTarget || image.type != Image.Type.Sliced || image.fillCenter || image.sprite.border == Vector4.zero)
                    throw new InvalidOperationException("Invalid sliced UI frame: " + image.transform.parent.name);
                if (image.sprite.texture.filterMode != FilterMode.Point)
                    throw new InvalidOperationException("PixelLab art must use point filtering.");
            }
        }

        private static void ValidateLoadingFill(GameObject host)
        {
            Transform fill = host.transform.Find("Transition Loading HUD/Loading Assembly/Loading Fill");
            Image image = fill.GetComponent<Image>();
            if (image.sprite == null || Mathf.Abs(image.fillAmount - .5f) > .001f)
                throw new InvalidOperationException("Loading fill requires a sprite and 50 percent progress.");
            Mesh mesh = image.canvasRenderer.GetMesh();
            float width = mesh != null ? mesh.bounds.size.x : 0f;
            if (mesh == null || mesh.vertexCount < 4 || Mathf.Abs(width - image.rectTransform.rect.width * .5f) > 1f)
                throw new InvalidOperationException("Loading bar rendered width differs from 50 percent progress.");
        }

        [Serializable] private sealed class Request { public string command; public string page; }
        private sealed class PreviewTestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool("AcademyUI.TargetedTests", false)) return;
                SessionState.SetBool("AcademyUI.TargetedTests", false);
                string output = Path.Combine(ProjectRoot, "Logs/AcademyUI/v3-playmode.xml");
                TestRunnerApi.SaveResultToFile(result, output);
                Debug.Log("Academy UI targeted tests: " + result.TestStatus + ", passed=" + result.PassCount + ", failed=" + result.FailCount);
            }
        }
        [Serializable] private sealed class Report
        {
            public bool success, playing, panelReady, buttonReady, crestReady;
            public string utc, command, scene, gameState, error;
            public string[] files;
        }
    }
}
