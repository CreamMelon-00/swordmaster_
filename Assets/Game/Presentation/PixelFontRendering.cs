using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>Keeps the shared pixel font sharp when UI panels or animated labels are scaled.</summary>
    public static class PixelFontRendering
    {
        private const string FontResourcePath = "LegacyDuel/Font/neodgm";
        private static Font uiFont;
        private static bool listening;

        public static Font LoadUiFont()
        {
            if (uiFont == null) uiFont = Resources.Load<Font>(FontResourcePath);
            if (uiFont == null) return null;
            if (!listening)
            {
                Font.textureRebuilt += OnTextureRebuilt;
                listening = true;
            }
            ApplyFilter(uiFont);
            return uiFont;
        }

        private static void OnTextureRebuilt(Font rebuilt)
        {
            // A dynamic atlas can be replaced as new Korean glyphs, sizes or styles are requested.
            // Its replacement must use the same sampling as the initial texture.
            if (rebuilt == uiFont) ApplyFilter(rebuilt);
        }

        private static void ApplyFilter(Font font)
        {
            Texture atlas = font.material != null ? font.material.mainTexture : null;
            if (atlas != null) atlas.filterMode = FilterMode.Point;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            // Domain reload may be disabled in the editor. Do not accumulate global event handlers.
            Font.textureRebuilt -= OnTextureRebuilt;
            listening = false;
            uiFont = null;
        }
    }
}
