using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class PixelFontRenderingPlayModeTests
    {
        [UnityTest]
        public IEnumerator UiFont_IsSharedDynamicAndUsesPointFiltering()
        {
            yield return null;
            Font font = PixelFontRendering.LoadUiFont();
            Assert.That(font, Is.SameAs(Resources.Load<Font>("LegacyDuel/Font/neodgm")));
            Assert.That(font.dynamic, Is.True, "Korean dialogue must continue loading glyphs on demand.");
            font.RequestCharactersInTexture("엘리사 ACT 123", 16, FontStyle.Normal);
            AssertPointFiltering(font);

            using (var art = new LegacyDuelArt())
                Assert.That(art.UIFont, Is.SameAs(font));

            for (int i = 0; i < 3; i++)
            {
                Assert.That(PixelFontRendering.LoadUiFont(), Is.SameAs(font));
                AssertPointFiltering(font);
            }
        }

        [UnityTest]
        public IEnumerator DynamicAtlasRebuild_PreservesPointFiltering()
        {
            yield return null;
            Font font = PixelFontRendering.LoadUiFont();
            font.RequestCharactersInTexture("기본 글리프", 16, FontStyle.Normal);
            int rebuilds = 0;
            bool everyRebuildWasPointFiltered = true;
            // Subscribe after LoadUiFont so its handler applies the filter before this observer.
            Action<Font> observeRebuild = rebuilt =>
            {
                if (rebuilt != font) return;
                rebuilds++;
                Texture atlas = rebuilt.material.mainTexture;
                everyRebuildWasPointFiltered &= atlas != null && atlas.filterMode == FilterMode.Point;
            };
            Font.textureRebuilt += observeRebuild;
            try
            {
                // New Hangul glyphs at successively larger sizes force real atlas growth.
                // The request count stays bounded even if the font was warmed by another test.
                int[] sizes = { 32, 48, 64 };
                for (int attempt = 0; attempt < sizes.Length && rebuilds == 0; attempt++)
                {
                    char[] characters = new char[512];
                    for (int i = 0; i < characters.Length; i++)
                        characters[i] = (char)(0xAC00 + attempt * characters.Length + i);
                    font.RequestCharactersInTexture(new string(characters), sizes[attempt], FontStyle.Normal);
                }
                Assert.That(rebuilds, Is.GreaterThan(0), "The test must observe an actual dynamic font atlas rebuild.");
                Assert.That(everyRebuildWasPointFiltered, Is.True,
                    "Recreated font textures must remain sharp before text consumers receive the rebuild event.");
                AssertPointFiltering(font);
                Assert.That(PixelFontRendering.LoadUiFont(), Is.SameAs(font));
                AssertPointFiltering(font);
            }
            finally
            {
                Font.textureRebuilt -= observeRebuild;
                PixelFontRendering.LoadUiFont();
            }
        }

        private static void AssertPointFiltering(Font font)
        {
            Assert.That(font.material, Is.Not.Null);
            Assert.That(font.material.mainTexture, Is.Not.Null);
            Assert.That(font.material.mainTexture.filterMode, Is.EqualTo(FilterMode.Point));
        }
    }
}
