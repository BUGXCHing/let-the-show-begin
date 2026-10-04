using NUnit.Framework;
using UnityEngine;

namespace HaoxiKaiyan.Tests
{
    public sealed class HitImpactTests
    {
        [TestCase(2f, HitTier.Light)]
        [TestCase(4.5f, HitTier.Heavy)]
        [TestCase(8f, HitTier.Launch)]
        public void MeasuredSwingSelectsTheExpectedReaction(float speed, HitTier expected)
        {
            HitImpact hit = Impact(speed, WeaponKind.WoodenSaber);
            Assert.That(hit.tier, Is.EqualTo(expected));
            if (expected == HitTier.Launch) Assert.That(hit.lift, Is.GreaterThan(0f));
            else Assert.That(hit.lift, Is.EqualTo(0f));
            Assert.That(hit.point, Is.EqualTo(Vector3.up));
            Assert.That(hit.direction, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void FasterSwingsAndHeavierWeaponsIncreaseDamage()
        {
            float light = Impact(2f, WeaponKind.WoodenSaber).damage;
            float heavy = Impact(4.5f, WeaponKind.WoodenSaber).damage;
            float launch = Impact(8f, WeaponKind.WoodenSaber).damage;
            Assert.That(heavy, Is.GreaterThan(light));
            Assert.That(launch, Is.GreaterThan(heavy));
            Assert.That(Impact(4.5f, WeaponKind.WarHammer).damage, Is.GreaterThan(heavy));
        }

        [Test]
        public void ExtremeMeasuredSpeedStillHasBoundedDamageAndLaunch()
        {
            HitImpact hit = Impact(1000f, WeaponKind.WarHammer);
            Assert.That(hit.damage, Is.LessThanOrEqualTo(48f));
            Assert.That(hit.push, Is.LessThanOrEqualTo(6f));
            Assert.That(hit.lift, Is.LessThanOrEqualTo(6f));
        }

        private static HitImpact Impact(float speed, WeaponKind kind)
        {
            WeaponSpec weapon = WeaponSpec.Get(kind);
            return HitImpact.FromCollision(speed, weapon.mass, .35f, .75f, Vector3.up, Vector3.right);
        }
    }
}
