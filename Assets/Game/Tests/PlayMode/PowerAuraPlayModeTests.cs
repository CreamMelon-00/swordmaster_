using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The code-drawn power effect on its own figure: the aura's fades, the charge's timing and cut, where
    /// the motes sit and draw, and cleanup.</summary>
    public sealed class PowerAuraPlayModeTests
    {
        [UnityTest]
        public IEnumerator Aura_FadesInAndOut_WithMotesAroundTheBodyInFrontAndBehind()
        {
            yield return null;
            var figure = new GameObject("Power Aura Figure");
            figure.AddComponent<SpriteRenderer>();
            var aura = new DuelPowerAura(figure.transform, null, 0, 5);
            try
            {
                Assert.That(aura.Root.parent, Is.SameAs(figure.transform));
                Assert.That(aura.ActiveMoteCount, Is.Zero);
                aura.SetAura(true, 1f);
                Assert.That(aura.IsAuraOn, Is.True);
                Assert.That(aura.AuraAmount, Is.Zero, "It fades in.");
                aura.Tick(.5f);
                Assert.That(aura.AuraAmount, Is.EqualTo(.5f).Within(1e-4f));
                for (int frame = 0; frame < 30; frame++) aura.Tick(1f / 30f);
                Assert.That(aura.AuraAmount, Is.EqualTo(1f));
                Assert.That(aura.ActiveMoteCount, Is.GreaterThan(5));
                bool front = false, back = false;
                foreach (SpriteRenderer view in aura.Root.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (!view.enabled || !view.name.StartsWith("Power Mote")) continue;
                    front |= view.sortingOrder == DuelPowerAura.FrontSortingOrder;
                    back |= view.sortingOrder == DuelPowerAura.BackSortingOrder;
                    Assert.That(Mathf.Abs(view.transform.localPosition.x), Is.LessThan(2f), "Around the body.");
                    Assert.That(view.transform.localPosition.y,
                        Is.InRange(DuelPowerAura.FeetY - .1f, DuelPowerAura.FeetY + DuelPowerAura.BodyHeight + 3f));
                    Assert.That(view.color.a, Is.InRange(0f, 1f));
                }
                Assert.That(front && back, Is.True, "Some motes pass in front of the body, some behind it.");
                SpriteRenderer glow = Named(aura, "Power Aura Glow");
                Assert.That(glow.enabled, Is.True);
                Assert.That(glow.sortingOrder, Is.EqualTo(DuelPowerAura.BackSortingOrder), "The glow sits behind the body.");

                figure.SetActive(false);
                Assert.That(aura.Root.gameObject.activeInHierarchy, Is.False, "It is hidden with its figure.");
                figure.SetActive(true);
                aura.SetAura(false, .5f);
                Assert.That(aura.IsAuraOn, Is.False);
                aura.Tick(.5f);
                Assert.That(aura.AuraAmount, Is.Zero);
                for (int frame = 0; frame < 60; frame++) aura.Tick(1f / 30f);
                Assert.That(aura.ActiveMoteCount, Is.Zero, "The last motes rise away.");
                Assert.That(glow.enabled, Is.False);
            }
            finally
            {
                aura.Dispose();
                Object.Destroy(figure);
            }
            yield return null;
            Assert.That(aura.Root == null, Is.True, "Dispose takes its objects away.");
        }

        [Test]
        public void Charge_GathersForItsTime_ThenFlaresAndFades_OrIsCutOffAtOnce()
        {
            var figure = new GameObject("Power Charge Figure");
            var aura = new DuelPowerAura(figure.transform, null, 0, 9);
            try
            {
                aura.Charge(1f);
                Assert.That(aura.IsCharging, Is.True);
                Assert.That(aura.ChargeGlow, Is.Zero);
                aura.Tick(.5f);
                Assert.That(aura.ChargeGlow, Is.EqualTo(.5f).Within(1e-4f), "The glow swells…");
                Assert.That(aura.ActiveMoteCount, Is.GreaterThan(0), "…as motes are drawn in.");
                Assert.That(Named(aura, "Power Charge Core").enabled, Is.True);
                aura.Tick(.5f);
                Assert.That(aura.IsCharging, Is.False, "It ends on time…");
                Assert.That(aura.ChargeGlow, Is.GreaterThan(1f), "…with a flare…");
                aura.Tick(DuelPowerAura.ReleaseSeconds);
                Assert.That(aura.ChargeGlow, Is.Zero, "…that fades.");
                Assert.That(Named(aura, "Power Charge Core").enabled, Is.False);

                aura.SetAura(true);
                aura.Charge(2f);
                for (int frame = 0; frame < 10; frame++) aura.Tick(.05f);
                Assert.That(aura.ActiveMoteCount, Is.GreaterThan(0));
                aura.StopCharge();
                Assert.That(aura.IsCharging, Is.False);
                Assert.That(aura.ChargeGlow, Is.Zero, "Cut off: no flare.");
                Assert.That(aura.AuraAmount, Is.EqualTo(1f), "The aura is untouched.");
                foreach (SpriteRenderer view in aura.Root.GetComponentsInChildren<SpriteRenderer>())
                    if (view.enabled && view.name.StartsWith("Power Mote"))
                        Assert.That(view.transform.localRotation, Is.EqualTo(Quaternion.identity),
                            "Only the aura's round motes remain; every streak drawn in is gone.");
                aura.Reset();
                Assert.That(aura.ActiveMoteCount, Is.Zero);
                Assert.That(aura.IsAuraOn || aura.IsCharging, Is.False);
            }
            finally
            {
                aura.Dispose();
                Object.Destroy(figure);
            }
        }

        [Test]
        public void AHeldCharge_StaysFullUntilItIsCutOff_WithoutAFlare()
        {
            var figure = new GameObject("Power Hold Figure");
            var aura = new DuelPowerAura(figure.transform, null, 0, 17);
            try
            {
                aura.Charge(2.5f, hold: true);
                Assert.That(aura.IsCharging && aura.IsChargeHeld, Is.True);
                aura.Tick(1.25f);
                Assert.That(aura.ChargeGlow, Is.EqualTo(.5f).Within(1e-4f), "It swells like any charge…");
                for (int frame = 0; frame < 600; frame++) aura.Tick(.05f);
                Assert.That(aura.IsCharging && aura.IsChargeHeld, Is.True, "…and is still gathering after half a minute…");
                Assert.That(aura.ChargeGlow, Is.EqualTo(1f), "…at full glow, without a finished charge's flare…");
                Assert.That(aura.ActiveMoteCount, Is.GreaterThan(0), "…with motes still drawn in.");
                aura.StopCharge();
                Assert.That(aura.IsCharging || aura.IsChargeHeld, Is.False);
                Assert.That(aura.ChargeGlow, Is.Zero, "Cut off: no flare.");
                aura.Tick(.1f);
                Assert.That(aura.ChargeGlow, Is.Zero);
                Assert.That(Named(aura, "Power Charge Core").enabled, Is.False);

                aura.Charge(1f, hold: true);
                aura.Charge(1f);
                Assert.That(aura.IsChargeHeld, Is.False, "A new charge starts over, held only when asked.");
                aura.Tick(1f);
                Assert.That(aura.IsCharging, Is.False);
                Assert.That(aura.ChargeGlow, Is.GreaterThan(1f), "An unheld charge still flares when it is full.");
            }
            finally
            {
                aura.Dispose();
                Object.Destroy(figure);
            }
        }

        [Test]
        public void ALongFrame_NeitherFloodsThePoolNorBreaksTheEffect()
        {
            var figure = new GameObject("Power Pool Figure");
            var aura = new DuelPowerAura(figure.transform, null, 0, 13);
            try
            {
                aura.SetAura(true);
                aura.Charge(5f);
                for (int frame = 0; frame < 200; frame++) aura.Tick(frame % 20 == 0 ? 3f : .02f);
                Assert.That(aura.ActiveMoteCount, Is.InRange(1, DuelPowerAura.Capacity));
                Assert.That(aura.Root.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(DuelPowerAura.Capacity + 2),
                    "A fixed pool: the glow, the core and the motes, never more.");
                aura.Tick(float.NaN);
                aura.Tick(-1f);
                Assert.That(aura.AuraAmount, Is.EqualTo(1f));
            }
            finally
            {
                aura.Dispose();
                Object.Destroy(figure);
            }
        }

        private static SpriteRenderer Named(DuelPowerAura aura, string name)
        {
            foreach (SpriteRenderer view in aura.Root.GetComponentsInChildren<SpriteRenderer>(true))
                if (view.name == name) return view;
            Assert.Fail("Missing renderer: " + name);
            return null;
        }
    }
}
