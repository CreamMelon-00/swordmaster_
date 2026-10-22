namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        /// <summary>A light, cool contact between the blades before the real strike. Only the pooled glow is emitted:
        /// no hit identity, weapon trail, body reaction, displacement, camera impulse or gameplay is consumed.</summary>
        public void PresentBladeExchangeContact(float strength)
        {
            if (disposed || suspension != null || !resolving || approaching || returning) return;
            impactGlow.Emit(BladeBindContactPosition, true, false, strength);
        }
    }
}
