using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class EnemyVariantsPlayModeTests
    {
        private static readonly LegacySkillProperty[] Types = { LegacySkillProperty.Slash, LegacySkillProperty.Penetrate, LegacySkillProperty.Hit };

        [Test]
        public void NineMotionsHaveDistinctContactArtAndTheSameImpactPhase()
        {
            using (var set = new EnemyStudentAnimationSet())
            {
                Assert.That(set.HasRequiredAssets, Is.True, string.Join(", ", set.MissingResources));
                Assert.That(set.LoadedSpriteCount, Is.EqualTo(120));
                Assert.That(set.GetHurt().name, Is.EqualTo("enemy-poses-hurt"));
                var textures = new System.Collections.Generic.HashSet<Texture2D>();
                foreach (var type in Types) for (int variant = 0; variant < 3; variant++)
                {
                    string prefix = "enemy-" + EnemyStudentAnimationSet.AttackKey(type, variant);
                    Assert.That(set.GetAttack(type, .499f, variant).name, Is.EqualTo(prefix + "-frame-04"));
                    Sprite contact = set.GetAttack(type, .5f, variant);
                    Assert.That(contact.name, Is.EqualTo(prefix + "-frame-05"));
                    Assert.That(set.GetAttack(type, 1, variant).name, Is.EqualTo(prefix + "-frame-12"));
                    Assert.That(contact.pivot, Is.EqualTo(set.GetHurt().pivot));
                    textures.Add(contact.texture);
                }
                Assert.That(textures.Count, Is.EqualTo(9));
            }
        }

        [UnityTest]
        public IEnumerator EveryEnemyHitDrawsOnceAndRetainsItsVariantAcrossHoldsRewindsAndReactions()
        {
            var enemyRandom = new Choices(0, 1, 2, 2, 1, 0, 1, 2, 0, 0);
            using (var s = new Scope(enemyRandom))
            {
                int calls = 0;
                foreach (var type in Types)
                {
                    s.Arena.BeginSlot(null, Attack(type, 3));
                    for (int hit = 0; hit < 3; hit++)
                    {
                        Hold(s.Arena, hit * LegacyArenaView.OriginalClipDuration + LegacyArenaView.OriginalImpactTime);
                        var contact = s.Arena.EnemyRenderer.sprite;
                        Assert.That(enemyRandom.Calls, Is.EqualTo(++calls));
                        s.Arena.Tick(0, .3f);
                        Assert.That(s.Arena.EnemyRenderer.sprite, Is.SameAs(contact));
                        Hold(s.Arena, hit * LegacyArenaView.OriginalClipDuration + .001f);
                        Assert.That(s.Arena.EnemyRenderer.sprite.name.Replace("01", "05"), Is.EqualTo(contact.name));
                        Hold(s.Arena, hit * LegacyArenaView.OriginalClipDuration + LegacyArenaView.OriginalImpactTime);
                        Assert.That(s.Arena.EnemyRenderer.sprite, Is.SameAs(contact));
                        Assert.That(enemyRandom.Calls, Is.EqualTo(calls));
                    }
                    Hold(s.Arena, LegacyArenaView.OriginalImpactTime);
                    Assert.That(enemyRandom.Calls, Is.EqualTo(calls));
                }
                s.Arena.BeginSlot(null, Attack(Types[2], 1));
                Assert.That(enemyRandom.Calls, Is.EqualTo(10), "New slots reroll; repeats are allowed.");
                s.Arena.Reset();
                Assert.That(s.Arena.EnemyRenderer.sprite.name, Is.EqualTo("enemy-idle-frame-01"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerAndEnemyDrawFromIndependentStreamsAndBodyDamageOverridesGuard()
        {
            var enemyRandom = new Choices(2, 0, 1);
            var playerRandom = new Choices(1, 2);
            using (var s = new Scope(enemyRandom, playerRandom))
            {
                var a = s.Arena;
                a.BeginSlot(Attack(Types[0], 1), Attack(Types[0], 1));
                Hold(a, LegacyArenaView.OriginalImpactTime);
                Assert.That(a.PlayerRenderer.sprite.name, Is.EqualTo("slash-2-frame-05"));
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-slash-3-frame-05"));
                var contact = a.EnemyRenderer.sprite;
                a.PresentHit(true, 0, 1, false, false, 1, LegacyArenaView.HitExchange.MutualClash);
                Assert.That(a.EnemyRenderer.sprite, Is.SameAs(contact));
                a.PresentHit(true, 0, 1, true, false, 1);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-block"));
                a.PresentHit(true, 2, 1, true, false, 1);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-hurt"));
                a.Tick(0, .5f);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-hurt"));
                Assert.That(enemyRandom.Calls, Is.EqualTo(1));
                a.BeginSlot(null, null);
                Assert.That(a.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"));
                a.PresentHit(true, 0, 0, false, false, 0);
                Assert.That(a.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"));
                a.PresentHit(true, 4, 0, false, false, 1);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-hurt"));
                a.Tick(LegacyArenaView.ReactionPoseDuration + .01f, 0);
                Assert.That(a.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"));
                a.BeginSlot(Attack(Types[1], 1), Attack(Types[1], 1));
                Assert.That(playerRandom.Calls, Is.EqualTo(2));
                Assert.That(enemyRandom.Calls, Is.EqualTo(2));
                a.PresentHit(true, 1, 0, false, true, 1);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-hurt"));
                a.EndTurn(); a.Tick(0, 0);
                Assert.That(a.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraRendersAllNineEnemyContactsAndHurt()
        {
            yield return null;
            var controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            bool enabled = controller.enabled; controller.RestartMatch(); controller.enabled = false;
            var oldRoot = controller.ArenaView.PlayerRenderer.transform.parent.gameObject;
            bool active = oldRoot.activeSelf; oldRoot.SetActive(false);
            var s = new Scope(new Choices(0,1,2,0,1,2,0,1,2));
            var camera = s.Arena.ArenaCamera;var target = new RenderTexture(1600,900,24);var previous = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                foreach (var type in Types) for (int variant = 0; variant < 3; variant++)
                {
                    var a=s.Arena; a.Reset();a.CloseDistance(1f);a.BeginSlot(null,Attack(type,1));a.Tick(0,1);
                    Hold(a,LegacyArenaView.OriginalImpactTime);
                    string key=EnemyStudentAnimationSet.AttackKey(type,variant);
                    Assert.That(a.EnemyRenderer.sprite.name,Is.EqualTo("enemy-"+key+"-frame-05"));
                    yield return null;yield return null;Capture(camera,target,key);
                }
                s.Arena.PresentHit(true,5,0,false,false,1);
                Assert.That(s.Arena.EnemyRenderer.sprite.name,Is.EqualTo("enemy-poses-hurt"));
                yield return null;yield return null;Capture(camera,target,"hurt");
            }
            finally
            {
                camera.targetTexture=previous;target.Release();Object.Destroy(target);s.Dispose();
                oldRoot.SetActive(active);controller.RestartMatch();controller.enabled=enabled;
            }
        }

        private static void Capture(Camera camera, RenderTexture target, string key)
        {
            string directory=Environment.GetEnvironmentVariable("ENEMY_VARIANTS_CAPTURE_DIR");if(string.IsNullOrEmpty(directory))return;
            Directory.CreateDirectory(directory);var old=RenderTexture.active;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try { RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,"unity-enemy-"+key+".png"),image.EncodeToPNG()); }
            finally { RenderTexture.active=old;Object.Destroy(image); }
        }
        private static LegacySkill Attack(LegacySkillProperty type,int count)=>new LegacySkill(991,"Enemy variant",1,1,1,LegacySkillKind.Attack,type,count,0,string.Empty);
        private static void Hold(LegacyArenaView arena,float time)=>typeof(LegacyArenaView).GetMethod("HoldSlotAtTime",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(arena,new object[]{time});
        private sealed class Scope:IDisposable
        {
            private readonly GameObject host=new GameObject("Enemy variant test");private readonly LegacyDuelArt art=new LegacyDuelArt();
            public LegacyArenaView Arena{get;}
            public Scope(System.Random enemy,System.Random player=null){Arena=LegacyArenaView.Create(host.transform,art,null,null,player,enemy,new FirstReaction());}
            public void Dispose(){Arena.Dispose();art.Dispose();Object.Destroy(host);}
        }
        private sealed class FirstReaction : System.Random { public override int Next(int maxValue) => 0; }
        private sealed class Choices:System.Random
        {
            private readonly int[] values;public int Calls{get;private set;}
            public Choices(params int[] values){this.values=values;}
            public override int Next(int maxValue){Assert.That(maxValue,Is.EqualTo(3));Assert.That(Calls,Is.LessThan(values.Length));return values[Calls++];}
        }
    }
}
