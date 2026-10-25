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
        private const string OwnRun = "TurnLimbo.ArtValidation.OwnRun";
        private const string AnyRun = "TurnLimbo.ArtValidation.AnyRun";
        static ArtValidationRunner()
        {
            Api = ScriptableObject.CreateInstance<TestRunnerApi>();
            Api.RegisterCallbacks(new Results());
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (!File.Exists(Request) || IsBusy()) return;
            File.Delete(Request);
            Run();
        }

        [MenuItem("Tools/Art/Validate Final Player Resources")]
        public static void Run()
        {
            if (IsBusy()) return;
            // SessionState survives the domain reload when PlayMode starts and ends.
            SessionState.SetBool(OwnRun, true);
            try
            {
                Api.Execute(new ExecutionSettings(new Filter
                {
                    testMode = TestMode.PlayMode,
                    groupNames = new[] { "FinalPlayerArtPlayModeTests", "AcademyFieldViewPlayModeTests", "SwordGirl", "MobStudentAnimationPlayModeTests", "WeaponTrailPlayModeTests", "AttackRhythmPlayModeTests", "CombatTempoPlayModeTests" }
                }));
            }
            catch
            {
                SessionState.EraseBool(OwnRun);
                SessionState.EraseBool(AnyRun);
                throw;
            }
        }

        private static bool IsBusy() => SessionState.GetBool(OwnRun, false) || SessionState.GetBool(AnyRun, false) ||
            EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode;

        private sealed class Results : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) => SessionState.SetBool(AnyRun, true);
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                SessionState.EraseBool(AnyRun);
                if (!SessionState.GetBool(OwnRun, false)) return;
                // Consume before writing: duplicate callbacks and a failed write cannot claim another run.
                SessionState.EraseBool(OwnRun);
                string directory = Path.GetFullPath("Docs/Art/FinalPlayer/Validation");
                Directory.CreateDirectory(directory);
                TestRunnerApi.SaveResultToFile(result, Path.Combine(directory, "playmode.xml"));
            }

            public void OnError(string message)
            {
                SessionState.EraseBool(AnyRun);
                SessionState.EraseBool(OwnRun);
            }
        }
    }
}
