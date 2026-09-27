using TurnLimbo.Presentation;
using UnityEditor;
using UnityEngine;

namespace TurnLimbo.EditorTools
{
    public static class DuelPresentationSettingsMenu
    {
        private const string SettingsPath = "Assets/Game/Resources/DuelPresentationSettings.asset";

        [MenuItem("Turn Limbo/연출 튜닝 열기", false, 10)]
        public static void OpenSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DuelPresentationSettings>(SettingsPath);
            if (settings == null)
            {
                Debug.LogError("연출 설정을 찾을 수 없습니다: " + SettingsPath);
                return;
            }
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
