using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Dialogue;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>Optional project-wide portrait lookup. Story text remains plain and Unity-independent.</summary>
    public sealed class DialoguePortraitCatalog : ScriptableObject
    {
        public const string ResourcePath = "DialoguePortraitCatalog";

        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string speakerName;
            [SerializeField] private Sprite portrait;

            public string SpeakerName => speakerName;
            public Sprite Portrait => portrait;

#if UNITY_EDITOR
            public Entry(string speakerName, Sprite portrait)
            {
                this.speakerName = speakerName;
                this.portrait = portrait;
            }

            public void SetPortrait(Sprite value) => portrait = value;
#endif
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public Sprite FindPortrait(DialogueLine line)
        {
            if (line == null || !line.ShowsNameplate) return null;
            return FindPortrait(line.SpeakerName);
        }

        public Sprite FindPortrait(string speakerName)
        {
            if (string.IsNullOrWhiteSpace(speakerName) || entries == null) return null;
            for (int index = 0; index < entries.Count; index++)
            {
                Entry entry = entries[index];
                if (entry != null && string.Equals(entry.SpeakerName, speakerName, StringComparison.Ordinal))
                    return entry.Portrait;
            }
            return null;
        }

#if UNITY_EDITOR
        public void SetPortraitForEditor(string speakerName, Sprite portrait)
        {
            if (string.IsNullOrWhiteSpace(speakerName))
                throw new ArgumentException("화자 이름이 필요합니다.", nameof(speakerName));

            speakerName = speakerName.Trim();
            if (entries == null) entries = new List<Entry>();
            Entry match = null;
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                Entry entry = entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.SpeakerName))
                {
                    entries.RemoveAt(index);
                    continue;
                }
                if (!string.Equals(entry.SpeakerName, speakerName, StringComparison.Ordinal)) continue;
                if (match == null) match = entry;
                else entries.RemoveAt(index);
            }

            if (match != null)
            {
                if (portrait == null) entries.Remove(match);
                else match.SetPortrait(portrait);
            }
            else if (portrait != null) entries.Add(new Entry(speakerName, portrait));
            if (portrait == null) return;
            entries.Sort((left, right) => string.Compare(left.SpeakerName, right.SpeakerName,
                StringComparison.Ordinal));
        }
#endif
    }
}
