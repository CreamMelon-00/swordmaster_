namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>The real technique selected by the optional F1 thrust sample. Shared combat presentation does not depend on this ID.</summary>
    public static class LegacyFeaturedTechnique
    {
        public const int SkillId = 3;

        public static bool IsFeatured(LegacySkill skill) =>
            skill != null && skill.Id == SkillId && skill.Kind == LegacySkillKind.Attack;
    }
}
