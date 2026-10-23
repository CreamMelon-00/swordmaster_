using System;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>Direct controller SetActive calls must also discard any unfinished reservation entrance.</summary>
    public sealed class LegacyQueueInsertionVisibility : MonoBehaviour
    {
        private Action reset;
        public void Configure(Action callback) => reset = callback;
        private void OnDisable() => reset?.Invoke();
    }
}
