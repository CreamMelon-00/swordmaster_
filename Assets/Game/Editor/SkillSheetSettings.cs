using UnityEditor;
using UnityEngine;

namespace TurnLimbo.EditorTools
{
    /// <summary>The Google Sheets address of the skill sheet (<see cref="SkillSheetWindow"/>). It lives in ProjectSettings
    /// so the team shares it through git. It has a file of its own because a ScriptableSingleton is saved with a
    /// reference to its script, and Unity finds a script only for the class named after its file; declared in another
    /// file, the saved address would not load again after a restart.</summary>
    [FilePath("ProjectSettings/TurnLimboSkillSheet.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class SkillSheetSettings : ScriptableSingleton<SkillSheetSettings>
    {
        [SerializeField] private string sheetUrl = string.Empty;

        public string Url
        {
            get => sheetUrl ?? string.Empty;
            set
            {
                string url = value ?? string.Empty;
                if (url == Url) return;
                sheetUrl = url;
                Save(true);
            }
        }
    }
}
