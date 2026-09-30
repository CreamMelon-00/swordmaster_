using System.IO;
using TurnLimbo.Presentation;
using UnityEditor;
using UnityEngine;

namespace TurnLimbo.EditorTools
{
    /// <summary>Editor shortcuts for the temporary auto-save: remove it to start clean, or open its folder.</summary>
    public static class GameSaveMenu
    {
        [MenuItem("Turn Limbo/세이브 삭제", false, 30)]
        public static void DeleteSave()
        {
            var store = new GameSaveStore(GameSaveStore.DefaultPath);
            if (!store.Exists)
            {
                EditorUtility.DisplayDialog("세이브 삭제", "삭제할 세이브가 없습니다.\n" + store.Path, "확인");
                return;
            }
            if (!EditorUtility.DisplayDialog("세이브 삭제",
                    "저장된 진행을 지웁니다. 되돌릴 수 없습니다.\n" + store.Path, "삭제", "취소")) return;
            if (store.Delete()) Debug.Log("세이브를 삭제했습니다: " + store.Path);
        }

        [MenuItem("Turn Limbo/세이브 폴더 열기", false, 31)]
        public static void RevealSave()
        {
            string path = GameSaveStore.DefaultPath;
            EditorUtility.RevealInFinder(File.Exists(path) ? path : Path.GetDirectoryName(path));
        }
    }
}
