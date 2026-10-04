using UnityEngine;

namespace HaoxiKaiyan
{
    public enum WeaponKind { ShortKnife, WoodenSaber, LongSword, SawAxe, Wrench, BaseballBat, WarHammer }
    public enum WeaponClass { Edged, Blunt }

    // One set of physical parameters is shared by the held weapon, dropped prop and hit rules.
    public readonly struct WeaponSpec
    {
        public readonly WeaponKind kind;
        public readonly WeaponClass weaponClass;
        public readonly float mass, length, drive, angularDrive, maxAngularSpeed, damping;
        public readonly float sharpness, breakPower, movePenalty;

        public WeaponSpec(WeaponKind kind, WeaponClass weaponClass, float mass, float length,
            float drive, float angularDrive, float maxAngularSpeed, float damping,
            float sharpness, float breakPower, float movePenalty)
        {
            this.kind = kind; this.weaponClass = weaponClass; this.mass = mass; this.length = length;
            this.drive = drive; this.angularDrive = angularDrive;
            this.maxAngularSpeed = maxAngularSpeed; this.damping = damping;
            this.sharpness = sharpness; this.breakPower = breakPower; this.movePenalty = movePenalty;
        }

        public static WeaponSpec Get(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.ShortKnife: return new WeaponSpec(kind, WeaponClass.Edged, .28f, .91f, 235f, 210f, 23f, .19f, 1.15f, .75f, 0f);
                case WeaponKind.LongSword: return new WeaponSpec(kind, WeaponClass.Edged, .66f, 1.68f, 195f, 175f, 19f, .13f, 1.38f, 1.03f, .01f);
                case WeaponKind.SawAxe: return new WeaponSpec(kind, WeaponClass.Edged, 1.12f, 1.32f, 165f, 145f, 18f, .11f, 1.32f, 1.36f, .035f);
                case WeaponKind.Wrench: return new WeaponSpec(kind, WeaponClass.Blunt, .78f, 1.08f, 185f, 170f, 20f, .17f, 0f, 1.27f, .015f);
                case WeaponKind.BaseballBat: return new WeaponSpec(kind, WeaponClass.Blunt, .82f, 1.42f, 180f, 165f, 20f, .12f, 0f, 1.23f, .02f);
                case WeaponKind.WarHammer: return new WeaponSpec(kind, WeaponClass.Blunt, 1.72f, 1.56f, 160f, 150f, 19f, .085f, 0f, 2.05f, .075f);
                default: return new WeaponSpec(WeaponKind.WoodenSaber, WeaponClass.Edged, .42f, 1.49f, 245f, 230f, 22f, .14f, 1.12f, 1f, 0f);
            }
        }
    }
}
