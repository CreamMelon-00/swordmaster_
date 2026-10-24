using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace TurnLimbo.Editor.ArtValidation
{
    [InitializeOnLoad]
    public static class ArtValidationRunner
    {
        private static readonly TestRunnerApi Api;
        private const string Request = "Library/ArtValidation.request";
        static ArtValidationRunner()
        {
            Api = ScriptableObject.CreateInstance<TestRunnerApi>();
            Api.RegisterCallbacks(new Results());
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            Run();
        }

        [MenuItem("Tools/Art/Validate Final Player Resources")]
        public static void Run()
        {
            Api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                groupNames = new[] { "FinalPlayerArtPlayModeTests", "SwordGirl", "MobStudentAnimationPlayModeTests", "WeaponTrailPlayModeTests", "AttackRhythmPlayModeTests", "CombatTempoPlayModeTests" }
            }));
        }

        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                string directory = Path.GetFullPath("Docs/Art/FinalPlayer/Validation");
                Directory.CreateDirectory(directory);
                TestRunnerApi.SaveResultToFile(result, Path.Combine(directory, "playmode.xml"));
            }
        }
    }
}
