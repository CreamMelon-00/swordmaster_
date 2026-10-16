using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using TurnLimbo.Presentation;

public static class BackgroundLoopRenderValidation
{
    [Serializable] private sealed class Shot { public string file; public string map; public float cameraX; public float aspect; public int uncoveredPixels; }
    [Serializable] private sealed class Report { public string phase; public string unity; public string graphics; public string device; public string pipeline; public string colorSpace; public List<Shot> shots = new List<Shot>(); }

    public static void Run()
    {
        string phase = "before";
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-capturePhase") phase = args[i + 1];
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", phase));
        Directory.CreateDirectory(output);
        var report = new Report { phase = phase, unity = Application.unityVersion,
            graphics = SystemInfo.graphicsDeviceType.ToString(), device = SystemInfo.graphicsDeviceName,
            pipeline = GraphicsSettings.currentRenderPipeline == null ? "Built-in" : GraphicsSettings.currentRenderPipeline.name,
            colorSpace = QualitySettings.activeColorSpace.ToString() };
        var host = new GameObject("Isolated Backdrop Validation");
        var cameraObject = new GameObject("Validation Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.magenta;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 100f;
        var material = new Material(Shader.Find("Sprites/Default"));
        try
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
                throw new InvalidOperationException("Expected Direct3D11 graphics, got " + SystemInfo.graphicsDeviceType);
            var finite = new ExplorationFieldBackdrop(host.transform, material, 0);
            foreach (ExplorationFieldMap map in new[] { ExplorationFieldMap.Corridor, ExplorationFieldMap.Forest })
            {
                finite.SetMap(map);
                if (!finite.HasRequiredAssets) throw new InvalidOperationException("Missing finite " + map + " resources.");
                foreach (float x in new[] { -24.4f, -12f, 0f, 10f, 24.4f })
                {
                    camera.aspect = 16f / 9f;
                    camera.transform.position = new Vector3(finite.ClampCameraX(camera, x), -1.5f, -10f);
                    finite.Tick(camera);
                    Capture(camera, output, report, "finite-" + map.ToString().ToLowerInvariant(), x, 16f / 9f, 1920, 1080);
                }
                camera.aspect = 32f / 9f;
                camera.transform.position = new Vector3(finite.ClampCameraX(camera, -13.7f), -1.5f, -10f);
                finite.Tick(camera);
                Capture(camera, output, report, "finite-" + map.ToString().ToLowerInvariant(), -13.7f, 32f / 9f, 2560, 720);
            }
            finite.SetVisible(false);
            var battle = new SchoolCorridorBackdrop(host.transform, material, 0);
            if (!battle.HasRequiredAssets) throw new InvalidOperationException("Missing battle corridor resources.");
            foreach (float x in new[] { -18.6f, 0f, 18.6f })
            {
                camera.aspect = 16f / 9f;
                camera.transform.position = new Vector3(x, -1.5f, -10f);
                battle.Tick(camera, false, 0f);
                Capture(camera, output, report, "battle-corridor", x, 16f / 9f, 1920, 1080);
            }
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("BACKGROUND RENDER VALIDATION PASS: " + report.shots.Count + " actual D3D11 RenderTexture captures. " + output);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString());
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(material);
        }
    }

    private static void Capture(Camera camera, string output, Report report, string map, float x, float aspect, int width, int height)
    {
        string stem = map + "_x" + x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('-', 'm').Replace('.', 'p') + (aspect > 2f ? "_wide" : "");
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        camera.targetTexture = target;
        camera.aspect = aspect;
        try
        {
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply(false, false);
            File.WriteAllBytes(Path.Combine(output, stem + ".png"), texture.EncodeToPNG());
            int uncovered = 0;
            foreach (Color32 pixel in texture.GetPixels32()) if (pixel.r >= 250 && pixel.g <= 5 && pixel.b >= 250) uncovered++;
            report.shots.Add(new Shot { file = stem + ".png", map = map, cameraX = camera.transform.position.x, aspect = aspect, uncoveredPixels = uncovered });
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
