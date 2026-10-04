using UnityEngine;

namespace HaoxiKaiyan
{
    // Predictive walking, running, pivot steps and bounded sliding share one foot state.
    public sealed partial class PuppetRig
    {
        private Vector3 RestFoot(int i)
        {
            float side = i == 0 ? -1f : 1f;
            return root.position + Quaternion.Euler(0, pelvisYaw, 0) * new Vector3(side * .18f, .09f, i == 0 ? -.045f : .20f);
        }

        private void PoseFeet(Vector3 velocity, float collapse, float dt)
        {
            bool airborne = root.position.y > .16f;
            Vector3 planar = new Vector3(velocity.x, 0, velocity.z);
            float travelSpeed = planar.magnitude;
            bool running = travelSpeed > (runningLastFrame ? GameplayTuning.Pose.RunExitSpeed : GameplayTuning.Pose.RunEnterSpeed) && spinBlend < .55f && collapse <= .12f && !airborne;
            if (running)
            {
                // At running speed, one-foot-at-a-time stepping leaves the other
                // shoe out of leg reach. Each foot instead has a brief planted
                // contact and a longer airborne recovery, half a cycle apart.
                if (!runningLastFrame)
                {
                    runPhase = 0f;
                    for (int i = 0; i < 2; i++) feet[i].Stepping = false;
                }
                runningLastFrame = true;
                float phaseSpeed = travelSpeed / 2.05f;
                runPhase = Mathf.Repeat(runPhase + dt * phaseSpeed, 1f);
                Quaternion facingRun = Quaternion.Euler(0, pelvisYaw, 0);
                for (int i = 0; i < 2; i++)
                {
                    Foot foot = feet[i];
                    float phase = Mathf.Repeat(runPhase + i * .5f, 1f);
                    float side = i == 0 ? -1f : 1f;
                    if (phase < .25f)
                    {
                        if (foot.Stepping)
                        {
                            foot.Position = foot.Goal;
                            foot.Stepping = false;
                            StepCount++;
                        }
                        foot.Yaw = Mathf.LerpAngle(foot.Yaw, pelvisYaw, 1f - Mathf.Exp(-13f * dt));
                    }
                    else
                    {
                        if (!foot.Stepping)
                        {
                            foot.Start = foot.Position;
                            foot.StartYaw = foot.Yaw;
                            foot.Stepping = true;
                        }
                        float t = (phase - .25f) / .75f;
                        float remaining = (1f - phase) / phaseSpeed;
                        foot.Goal = root.position + planar * remaining + planar.normalized * .27f +
                            facingRun * new Vector3(side * .18f, .09f, 0f);
                        foot.Progress = t;
                        foot.Position = Vector3.Lerp(foot.Start, foot.Goal, Mathf.SmoothStep(0f, 1f, t)) +
                            Vector3.up * Mathf.Sin(t * Mathf.PI) * .23f;
                        foot.Yaw = Mathf.LerpAngle(foot.StartYaw, pelvisYaw, t);
                    }
                    Vector3 localRun = Quaternion.Inverse(facingRun) * (foot.Position - root.position);
                    if (foot.Stepping && localRun.x * side < .055f)
                    {
                        localRun.x = side * .055f;
                        foot.Position = root.position + facingRun * localRun;
                    }
                    Limb runLeg = i == 0 ? leftLeg : rightLeg;
                    LimitFootReach(foot, runLeg);
                    runLeg.Solve(foot.Position, facingRun * Vector3.forward,
                        Quaternion.Euler(foot.Stepping ? -Mathf.Sin(foot.Progress * Mathf.PI * 2f) * 12f : 0f, foot.Yaw, 0f));
                }
                return;
            }
            if (runningLastFrame)
            {
                runningLastFrame = false;
                for (int i = 0; i < 2; i++) feet[i].Stepping = false;
                nextStep = Time.time + .04f;
            }
            for (int i = 0; i < 2; i++)
            {
                Foot foot = feet[i];
                if (collapse > .12f)
                {
                    foot.Stepping = false;
                    Vector3 fallenGoal = root.position + Quaternion.Euler(0, pelvisYaw, 0) *
                        new Vector3(i == 0 ? -.28f : .28f, .09f, -.43f);
                    foot.Position = Vector3.Lerp(foot.Position, fallenGoal, 1f - Mathf.Exp(-8f * dt));
                    foot.Yaw = Mathf.LerpAngle(foot.Yaw, pelvisYaw, 1f - Mathf.Exp(-8f * dt));
                    (i == 0 ? leftLeg : rightLeg).Solve(foot.Position,
                        Quaternion.Euler(0, pelvisYaw, 0) * Vector3.forward, Quaternion.Euler(0, foot.Yaw, 0));
                    continue;
                }
                float speed = planar.magnitude;
                bool walking = speed > .25f;
                // The body keeps moving during the swing. Plant beyond that predicted travel
                // so the shoe arrives in front of the chest instead of behind it.
                float leadDistance = Mathf.Clamp(speed * .10f + .17f, .17f, .53f);
                Vector3 lead = walking ? planar.normalized * leadDistance : Vector3.zero;
                Vector3 goal = RestFoot(i) + lead;
                if (spinBlend > .001f)
                {
                    float side = i == 0 ? -1f : 1f;
                    Quaternion stance = Quaternion.Euler(0, pelvisYaw, 0);
                    Vector3 spinGoal = root.position + stance * new Vector3(side * (i == pivotFoot ? .14f : .27f), .09f,
                        i == pivotFoot ? -.035f : .19f) + planar * .09f;
                    goal = Vector3.Lerp(goal, spinGoal, spinBlend);
                }
                // Keep each ankle on its own side of the pelvis even during a full turn
                // combined with lateral travel. The free leg may reach, but not cross.
                Quaternion facing = Quaternion.Euler(0, pelvisYaw, 0);
                Vector3 local = Quaternion.Inverse(facing) * (goal - root.position);
                float sign = i == 0 ? -1f : 1f;
                local.x = sign * Mathf.Max(.16f, local.x * sign);
                local.z = Mathf.Clamp(local.z, -.42f, .50f);
                goal = root.position + facing * local;
                if (airborne)
                {
                    foot.Position = goal + Vector3.up * .11f;
                    foot.Yaw = pelvisYaw;
                    foot.Stepping = false;
                }
                else if (spinBlend > .55f && i == pivotFoot && speed < .75f)
                {
                    // Pivot in place. Translating this foot toward a root-relative
                    // target makes the puppet look dragged across the paper stage.
                    foot.Stepping = false;
                    foot.Yaw = Mathf.LerpAngle(foot.Yaw, pelvisYaw, 1f - Mathf.Exp(-11f * dt));
                }
                else if (!foot.Stepping && !feet[1 - i].Stepping && Time.time >= nextStep)
                {
                    float distance = Vector3.ProjectOnPlane(goal - foot.Position, Vector3.up).magnitude;
                    if ((distance > (walking ? .13f : .20f) || Mathf.Abs(Mathf.DeltaAngle(foot.Yaw, pelvisYaw)) > 24f) &&
                        (i == nextFoot || distance > .40f))
                    {
                        foot.Start = foot.Position; foot.Goal = goal;
                        foot.StartYaw = foot.Yaw; foot.GoalYaw = pelvisYaw;
                        foot.Progress = 0; foot.Duration = Mathf.Lerp(.23f, .12f,
                            Mathf.Clamp01(speed / 4.8f + Mathf.Abs(pelvisRate) / 500f));
                        foot.Stepping = true; StepCount++;
                    }
                }
                if (foot.Stepping)
                {
                    if (spinBlend > .25f) foot.Goal = Vector3.Lerp(foot.Goal, goal, 1f - Mathf.Exp(-14f * dt));
                    foot.Progress = Mathf.Min(1f, foot.Progress + dt / foot.Duration);
                    float t = Mathf.SmoothStep(0, 1, foot.Progress);
                    foot.Position = Vector3.Lerp(foot.Start, foot.Goal, t) + Vector3.up * Mathf.Sin(foot.Progress * Mathf.PI) * .23f;
                    foot.Yaw = Mathf.LerpAngle(foot.StartYaw, foot.GoalYaw, t);
                    if (foot.Progress >= 1f)
                    {
                        foot.Stepping = false; nextFoot = 1 - i; nextStep = Time.time + .015f;
                        if (spinBlend > .25f) pivotFoot = i;
                    }
                }
                else if (spinBlend > .25f && walking)
                    foot.Position += planar * dt * .70f * spinBlend; // Authored glissade while the free leg circles.
                // A fast pivot can rotate the pelvis past a planted shoe before its next
                // step begins. Constrain the actual ankle as well as its goal, so a spin
                // never pulls a leg through the other leg for one visible frame.
                Vector3 plantedLocal = Quaternion.Inverse(facing) * (foot.Position - root.position);
                if (spinBlend > .25f && plantedLocal.x * sign < .055f)
                {
                    plantedLocal.x = sign * .055f;
                    foot.Position = Vector3.MoveTowards(foot.Position, root.position + facing * plantedLocal, dt * 2.8f);
                }
                Limb leg = i == 0 ? leftLeg : rightLeg;
                if (!airborne) LimitFootReach(foot, leg);
                leg.Solve(foot.Position, Quaternion.Euler(0, pelvisYaw, 0) * Vector3.forward,
                    Quaternion.Euler(foot.Stepping ? -Mathf.Sin(foot.Progress * Mathf.PI * 2f) * 12f : 0, foot.Yaw, 0));
            }
        }

        private static void LimitFootReach(Foot foot, Limb leg)
        {
            // Near full extension, allow a small floor slide instead of asking IK
            // for an impossible point and lifting/jumping the visible ankle.
            Vector3 delta = foot.Position - leg.Root.position;
            float reach = leg.Length - .018f;
            float horizontal = Mathf.Sqrt(Mathf.Max(.015f, reach * reach - delta.y * delta.y));
            Vector3 planar = Vector3.ProjectOnPlane(delta, Vector3.up);
            if (planar.sqrMagnitude > horizontal * horizontal)
                foot.Position = leg.Root.position + Vector3.up * delta.y + planar.normalized * horizontal;
        }

        private sealed class Foot
        {
            public Vector3 Position, Start, Goal;
            public float Yaw, StartYaw, GoalYaw, Progress, Duration;
            public bool Stepping;
        }

    }
}
