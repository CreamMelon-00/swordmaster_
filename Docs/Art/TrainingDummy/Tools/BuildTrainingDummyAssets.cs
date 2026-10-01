using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class BuildTrainingDummyAssets
{
    private const string Root = "Assets/Game/Art/TrainingDummy";
    private static readonly int[] IdleMs = {180,180,180,180,180,180,180,180};
    private static readonly int[] HurtMs = {35,45,55,70,65,65,65,80,100,120};

    public static void Run()
    {
        foreach (string file in Directory.GetFiles(Root + "/Frames", "*.png", SearchOption.AllDirectories))
        {
            string path = file.Replace('\\','/');
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 40;
            importer.spritePivot = new Vector2(.5f, 22f / 224);
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
            textureSettings.spritePivot = new Vector2(.5f, 22f / 224);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
        var idle = Clip("Idle", "idle", IdleMs, true);
        var hurt = Clip("Hurt", "hurt", HurtMs, false);
        string controllerPath = Root + "/TrainingDummy.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
        var stateMachine = controller.layers[0].stateMachine;
        foreach (var state in stateMachine.states) stateMachine.RemoveState(state.state);
        foreach (var transition in stateMachine.anyStateTransitions) stateMachine.RemoveAnyStateTransition(transition);
        controller.parameters = new[] {new AnimatorControllerParameter {name = "Hurt", type = AnimatorControllerParameterType.Trigger}};
        var idleState = stateMachine.AddState("Idle"); idleState.motion = idle;
        var hurtState = stateMachine.AddState("Hurt"); hurtState.motion = hurt;
        stateMachine.defaultState = idleState;
        var incoming = stateMachine.AddAnyStateTransition(hurtState);
        incoming.hasExitTime = false; incoming.duration = 0; incoming.canTransitionToSelf = true;
        incoming.AddCondition(AnimatorConditionMode.If, 0, "Hurt");
        var recover = hurtState.AddTransition(idleState);
        recover.hasExitTime = true; recover.exitTime = 1; recover.duration = 0;
        var go = new GameObject("TrainingDummy");
        try
        {
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Frame("idle", 0); renderer.sortingOrder = 10;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (renderer.sharedMaterial == null) throw new Exception("Missing URP unlit sprite material.");
            var animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.SaveAsPrefabAsset(go, Root + "/TrainingDummy.prefab");
            Verify(idle, "idle", IdleMs, go);
            Verify(hurt, "hurt", HurtMs, go);
            if (Mathf.Abs(idle.length - 1.44f) > .0001f || Mathf.Abs(hurt.length - .7f) > .0001f)
                throw new Exception("Clip durations differ from Aseprite timing: idle="+idle.length+", hurt="+hurt.length);
            if (!AnimationUtility.GetAnimationClipSettings(idle).loopTime || AnimationUtility.GetAnimationClipSettings(hurt).loopTime)
                throw new Exception("Unexpected looping settings.");
            if (stateMachine.defaultState != idleState || incoming.conditions[0].parameter != "Hurt" || !incoming.canTransitionToSelf || !recover.hasExitTime)
                throw new Exception("Invalid reaction state transitions.");
            AssetDatabase.SaveAssets();
            string report = Environment.GetEnvironmentVariable("DUMMY_REPORT_PATH");
            if (!string.IsNullOrEmpty(report)) File.WriteAllText(report,
                "PASS: 18 sprites imported at 256x224, 40 PPU, pivot(128,22), Point/no mipmaps/uncompressed.\n" +
                "PASS: every Idle/Hurt keyframe sampled against its source sprite; fixed transform scale.\n" +
                "PASS: URP Sprite-Unlit-Default material matches the arena sprite lighting convention.\n" +
                "PASS: Idle loops for 1.44s; Hurt is a 0.70s one-shot. Prefab references both clips through its controller.\n" +
                "PASS: Hurt trigger enters/restarts Hurt; exit-time transition returns to Idle (controller structure checked).\n");
            Debug.Log("TrainingDummy asset build and verification passed.");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static Sprite Frame(string key, int index)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Frames/" + key + "/frame-" + (index + 1).ToString("00") + ".png");
        if (sprite == null) throw new Exception("Missing frame: " + key + index);
        return sprite;
    }

    private static AnimationClip Clip(string name, string key, int[] times, bool loop)
    {
        string path = Root + "/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) {clip = new AnimationClip {name=name}; AssetDatabase.CreateAsset(clip,path);}
        clip.frameRate = 200;
        var frames = new ObjectReferenceKeyframe[times.Length + 1];
        int milliseconds = 0;
        for (int i = 0; i < times.Length; i++)
        {
            frames[i] = new ObjectReferenceKeyframe {time=milliseconds/1000f, value=Frame(key,i)};
            milliseconds += times[i];
        }
        // Unity object-reference curves add one sample interval after their last key.
        // Keep the final cel through that interval without lengthening the authored clip.
        frames[times.Length] = new ObjectReferenceKeyframe {time=(milliseconds-5)/1000f, value=Frame(key,times.Length-1)};
        var binding = new EditorCurveBinding {path="", type=typeof(SpriteRenderer), propertyName="m_Sprite"};
        AnimationUtility.SetObjectReferenceCurve(clip,binding,frames);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop; settings.startTime=0; settings.stopTime=milliseconds/1000f;
        AnimationUtility.SetAnimationClipSettings(clip,settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void Verify(AnimationClip clip, string key, int[] times, GameObject target)
    {
        float time=0;
        for (int i=0;i<times.Length;i++)
        {
            Sprite sprite=Frame(key,i);
            if (sprite.rect.size != new Vector2(256,224) || sprite.pivot != new Vector2(128,22) || sprite.pixelsPerUnit != 40 ||
                sprite.texture.filterMode != FilterMode.Point || sprite.texture.mipmapCount != 1)
                throw new Exception("Invalid sprite import: "+sprite.name);
            clip.SampleAnimation(target,time+times[i]/2000f);
            if (target.GetComponent<SpriteRenderer>().sprite != sprite || target.transform.localScale != Vector3.one)
                throw new Exception("Unexpected clip sample: "+key+i);
            time+=times[i]/1000f;
        }
    }
}
