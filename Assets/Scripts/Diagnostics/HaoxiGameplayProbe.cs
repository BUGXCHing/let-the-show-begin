using System.Collections;
using HaoxiKaiyan;
using UnityEngine;
using UnityEngine.EventSystems;

// Editor-only, repeatable physics smoke test. Never included in the Web build.
#if UNITY_EDITOR
public sealed class HaoxiGameplayProbe : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return null;
        var actors = FindObjectsByType<MarionetteController>();
        MarionetteController player = null, enemy = null;
        foreach (var actor in actors)
        {
            if (actor.IsEnemy) enemy = actor;
            else player = actor;
        }
        var director = FindAnyObjectByType<HaoxiGameDirector>();
        if (director == null || player == null || enemy == null)
        {
            Debug.LogError("HAOXI_SMOKE_FAIL: 场景缺少导演或木偶。");
            Destroy(gameObject);
            yield break;
        }

        director.enabled = false;
        Time.timeScale = 1f;
        VirtualStick moveStick = null, weaponStick = null;
        foreach (var stick in FindObjectsByType<VirtualStick>())
        {
            if (stick.name == "身体摇杆") moveStick = stick;
            else if (stick.name == "武器摇杆") weaponStick = stick;
        }
        bool simultaneousTouch = false;
        if (moveStick != null && weaponStick != null && EventSystem.current != null)
        {
            var leftRect = (RectTransform)moveStick.transform;
            var rightRect = (RectTransform)weaponStick.transform;
            var leftTouch = new PointerEventData(EventSystem.current) { pointerId = 11,
                position = RectTransformUtility.WorldToScreenPoint(null, leftRect.TransformPoint(leftRect.rect.center + new Vector2(42f, 0f))) };
            var rightTouch = new PointerEventData(EventSystem.current) { pointerId = 12,
                position = RectTransformUtility.WorldToScreenPoint(null, rightRect.TransformPoint(rightRect.rect.center + new Vector2(42f, 0f))) };
            moveStick.OnPointerDown(leftTouch);
            weaponStick.OnPointerDown(rightTouch);
            simultaneousTouch = moveStick.Value.x > .5f && weaponStick.Value.x > .5f;
            moveStick.OnPointerUp(leftTouch);
            simultaneousTouch &= weaponStick.Value.x > .5f;
            weaponStick.OnPointerUp(rightTouch);
        }
        // Measure navigation away from the opponent so the corridor does not
        // accidentally become a root-separation/collision test.
        player.ResetActor(new Vector3(-3f, 0f, -3f), Quaternion.Euler(0f, -90f, 0f));
        enemy.ResetActor(new Vector3(3f, 0f, 3f), Quaternion.Euler(0f, -90f, 0f));
        enemy.SetEnemyMovement(Vector3.zero, false);
        yield return new WaitForFixedUpdate();
        float startX = player.Body.position.x;
        int startSteps = player.FootSteps;
        float maxFootLead = 0f;
        float maxPlantedSlip = 0f,maxPlantedGoalSlip=0f,maxFootGoalError=0f,maxPlantedFootError=0f;
        string plantedContext="";
        float maxRootY = 0f, maxPlanarSpeed = 0f;
        int interruptedFrames = 0;
        Vector3 previousLeftFoot=player.LeftFoot.position,previousRightFoot=player.RightFoot.position;
        Vector3 previousLeftGoal=player.LeftFootGoal,previousRightGoal=player.RightFootGoal;
        bool previousLeftStep=player.LeftFootStepping,previousRightStep=player.RightFootStepping;
        for (int i = 0; i < 32; i++)
        {
            player.SetControls(Vector2.right, Vector2.zero);
            yield return new WaitForFixedUpdate();
            if(i>4 && !previousLeftStep && !player.LeftFootStepping)
            {
                maxPlantedSlip=Mathf.Max(maxPlantedSlip,Vector3.ProjectOnPlane(
                    player.LeftFoot.position-previousLeftFoot,Vector3.up).magnitude);
                maxPlantedGoalSlip=Mathf.Max(maxPlantedGoalSlip,Vector3.ProjectOnPlane(
                    player.LeftFootGoal-previousLeftGoal,Vector3.up).magnitude);
                float error=Vector3.Distance(player.LeftFoot.position,player.LeftFootGoal);
                if(error>maxPlantedFootError)
                {
                    maxPlantedFootError=error;
                    plantedContext=$"L frame={i} hipToGoal={Vector3.Distance(player.LeftFoot.parent.parent.position,player.LeftFootGoal):F2} " +
                        $"rootToGoal={(player.LeftFootGoal-player.Body.position)}";
                }
            }
            if(i>4 && !previousRightStep && !player.RightFootStepping)
            {
                maxPlantedSlip=Mathf.Max(maxPlantedSlip,Vector3.ProjectOnPlane(
                    player.RightFoot.position-previousRightFoot,Vector3.up).magnitude);
                maxPlantedGoalSlip=Mathf.Max(maxPlantedGoalSlip,Vector3.ProjectOnPlane(
                    player.RightFootGoal-previousRightGoal,Vector3.up).magnitude);
                float error=Vector3.Distance(player.RightFoot.position,player.RightFootGoal);
                if(error>maxPlantedFootError)
                {
                    maxPlantedFootError=error;
                    plantedContext=$"R frame={i} hipToGoal={Vector3.Distance(player.RightFoot.parent.parent.position,player.RightFootGoal):F2} " +
                        $"rootToGoal={(player.RightFootGoal-player.Body.position)}";
                }
            }
            maxFootGoalError=Mathf.Max(maxFootGoalError,
                Vector3.Distance(player.LeftFoot.position,player.LeftFootGoal),
                Vector3.Distance(player.RightFoot.position,player.RightFootGoal));
            previousLeftFoot=player.LeftFoot.position; previousRightFoot=player.RightFoot.position;
            previousLeftGoal=player.LeftFootGoal; previousRightGoal=player.RightFootGoal;
            previousLeftStep=player.LeftFootStepping; previousRightStep=player.RightFootStepping;
            maxRootY = Mathf.Max(maxRootY, player.Body.position.y);
            maxPlanarSpeed = Mathf.Max(maxPlanarSpeed, player.MoveSpeed);
            if (player.IsInterrupted) interruptedFrames++;
            maxFootLead = Mathf.Max(maxFootLead,
                player.LeftFoot.position.x - player.Body.position.x,
                player.RightFoot.position.x - player.Body.position.x);
        }
        float travel = player.Body.position.x - startX;
        int steps = player.FootSteps - startSteps;

        player.ResetActor(new Vector3(-.85f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f));
        enemy.ResetActor(new Vector3(.85f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f));
        bool enemyColliderActiveAtSweep=enemy.BodyCollider.enabled;
        Vector3 enemyCenterAtSweep=enemy.BodyCollider.bounds.center;
        float maxSwing = 0f, maxChestPelvisDifference = 0f, minBladeDistance = 100f, maxCollapse = 0f;
        int sampleOverlapCount=0;
        bool trackBlade=true;
        bool sampleSawBody=false;
        bool colliderAtMinimum=false;
        string sampleNames="";
        Vector3 centerAtMinimum=Vector3.zero;
        Collider[] sampleBuffer=new Collider[64];
        bool sawKnockdown = false;
        int hits = 0;
        enemy.Damaged += (_, _, _) => hits++;
        Vector2[] arc = {Vector2.left, Vector2.up, Vector2.right, Vector2.down};
        for (int sweep = 0; sweep < 3; sweep++)
        {
            foreach (Vector2 direction in arc)
            {
                for (int i = 0; i < 12; i++)
                {
                    player.SetControls(Vector2.zero, direction);
                    yield return new WaitForFixedUpdate();
                    Sample();
                }
            }
        }

        for(int i=0;i<30;i++) { yield return new WaitForFixedUpdate(); Sample(); }
        int sweptHits = hits;
        float sweepHealth = enemy.Health01;
        trackBlade=false;
        MeleeWeaponHitbox bladeProbe=player.WeaponBody.GetComponent<MeleeWeaponHitbox>();
        int parries=bladeProbe.ParriedHits;
        int sweepQueries=bladeProbe.SweepQueries,targetCandidates=bladeProbe.TargetCandidates;
        int rawOverlaps=bladeProbe.RawOverlaps,rawEnemies=bladeProbe.RawEnemyOverlaps;
        int fullBuffers=bladeProbe.FullBuffers;
        int suppressedContacts=bladeProbe.SuppressedContacts,slowContacts=bladeProbe.SlowContacts;
        // Recovery is an independent case: real swept strikes are allowed to kill,
        // especially on a neck break, and a corpse must never be expected to rise.
        player.ResetActor(new Vector3(-3f,0f,-3f),Quaternion.identity);
        enemy.ResetActor(Vector3.zero,Quaternion.identity);
        enemy.ApplyWeaponHit(7.2f,enemy.Body.position+Vector3.up*1.2f,Vector3.right,
            .25f,null,.48f,.75f,false,HitZone.Body,0f,
            WeaponSpec.Get(WeaponKind.WoodenSaber),1f);
        bool bodyFell=false;
        for(int i=0;i<125;i++) { yield return new WaitForFixedUpdate(); Sample(); bodyFell |= enemy.IsKnockedDown; }
        bool recovered = !enemy.IsDefeated && !enemy.IsKnockedDown && enemy.VisualCollapse < .15f;
        enemy.ResetActor(Vector3.zero,Quaternion.identity);
        enemy.ApplyWeaponHit(7.4f,enemy.Body.position+Vector3.up*1.7f,Vector3.right,
            .25f,null,.48f,.9f,false,HitZone.Head,.8f,
            WeaponSpec.Get(WeaponKind.WoodenSaber),1f);
        for(int i=0;i<125;i++) { yield return new WaitForFixedUpdate(); Sample(); }
        bool headKnockdown=sawKnockdown && maxCollapse>.85f;
        player.ResetActor(new Vector3(-.60f,0f,0f),Quaternion.Euler(0f,90f,0f));
        enemy.ResetActor(new Vector3(.60f,0f,0f),Quaternion.Euler(0f,-90f,0f));
        MeleeWeaponHitbox guardedClub=enemy.WeaponBody.GetComponent<MeleeWeaponHitbox>();
        float guardedHealth=player.Health;
        enemy.SetEnemyFacing(player.Body.position-enemy.Body.position);
        enemy.SetEnemyMovement(Vector3.zero,true);
        for(int i=0;i<31;i++)
        { player.SetControls(Vector2.zero,Vector2.right); yield return new WaitForFixedUpdate(); }
        enemy.SetEnemyMovement(Vector3.left*2.9f,false,true);
        for(int i=0;i<12;i++)
        { player.SetControls(Vector2.zero,Vector2.right); yield return new WaitForFixedUpdate(); }
        int enemyParries=guardedClub.ParriedHits;
        float enemyGuardGap=guardedClub.NearestGuardGap;
        float guardDamage=guardedHealth-player.Health;
        enemy.SetEnemyMovement(Vector3.zero,false);
        player.ResetActor(new Vector3(-.60f,0f,0f),Quaternion.Euler(0f,90f,0f));
        enemy.ResetActor(new Vector3(.60f,0f,0f),Quaternion.Euler(0f,-90f,0f));
        // A held sword can physically parry the club. Remove it in this baseline
        // offensive test; parrying is a separate, valid combat outcome.
        WeaponPickup blockingSword=player.DropWeapon();
        if(blockingSword!=null) { blockingSword.gameObject.SetActive(false); Destroy(blockingSword.gameObject); }
        enemy.SetEnemyFacing(player.Body.position-enemy.Body.position);
        enemy.SetEnemyMovement(Vector3.zero,true);
        for(int i=0;i<43;i++) yield return new WaitForFixedUpdate();
        enemy.SetEnemyMovement(Vector3.left*2.9f,false,true);
        for(int i=0;i<12;i++) yield return new WaitForFixedUpdate();
        bool enemyClubHit=player.Health01<.99f;
        MeleeWeaponHitbox clubProbe=enemy.WeaponBody.GetComponent<MeleeWeaponHitbox>();
        int clubCandidates=clubProbe.TargetCandidates,clubHits=clubProbe.AppliedHits;
        Vector3 clubDistance=enemy.Body.position-player.Body.position;
        enemy.SetEnemyMovement(Vector3.zero,false);
        enemy.ApplyEnemyHit(200f,Vector3.right);
        for(int i=0;i<75;i++) { yield return new WaitForFixedUpdate(); Sample(); }
        bool stayedDown = enemy.IsKnockedDown && enemy.VisualCollapse > .85f && enemy.DeathPoseReady;
        float deathJointError=enemy.MaxJointError;
        enemy.ResetActor(new Vector3(.9f,0f,0f),Quaternion.Euler(0f,-90f,0f));
        for(int i=0;i<20;i++) yield return new WaitForFixedUpdate();
        float restartHealth=enemy.Health01,restartJointError=enemy.MaxJointError;
        bool restartReady=restartHealth>.99f && !enemy.IsDefeated && restartJointError<.18f;

        // The same stick magnitude must travel similarly in every world direction.
        float minTravel=float.MaxValue,maxTravel=0f;
        Vector2[] directions={Vector2.right,Vector2.left,Vector2.up,Vector2.down,
            new Vector2(.7071f,.7071f)};
        enemy.ResetActor(new Vector3(3f,0f,-3f),Quaternion.identity);
        foreach(Vector2 direction in directions)
        {
            player.ResetActor(Vector3.zero,Quaternion.Euler(0f,90f,0f));
            for(int i=0;i<32;i++)
            {
                player.SetControls(direction,Vector2.zero);
                yield return new WaitForFixedUpdate();
            }
            float distance=Vector3.Dot(player.Body.position,new Vector3(direction.x,0f,direction.y));
            minTravel=Mathf.Min(minTravel,distance);
            maxTravel=Mathf.Max(maxTravel,distance);
        }
        bool uniformMovement=minTravel>2.05f && maxTravel/minTravel<1.18f;

        // Press both kinematic roots toward one another while swiping the blade.
        // This used to put the sword inside the opponent's joint colliders and
        // send it flying back when the wrist constraint finally caught up.
        player.ResetActor(new Vector3(-1f,0f,0f),Quaternion.Euler(0f,90f,0f));
        enemy.ResetActor(new Vector3(1f,0f,0f),Quaternion.Euler(0f,-90f,0f));
        float closestActors=float.MaxValue,maxWristGap=0f,maxGoalGap=0f;
        for(int i=0;i<105;i++)
        {
            float angle=i*.17f;
            player.SetControls(Vector2.right,new Vector2(Mathf.Sin(angle),Mathf.Cos(angle)));
            enemy.SetEnemyMovement(Vector3.left*3.55f,false);
            yield return new WaitForFixedUpdate();
            closestActors=Mathf.Min(closestActors,Vector3.ProjectOnPlane(
                player.Body.position-enemy.Body.position,Vector3.up).magnitude);
            if(i>20)
            {
                maxWristGap=Mathf.Max(maxWristGap,player.WeaponWristGap);
                maxGoalGap=Mathf.Max(maxGoalGap,player.WeaponGoalGap);
            }
        }
        bool bladeContained=closestActors>.60f && maxWristGap<.36f && maxGoalGap<.78f &&
            player.WeaponCollider.isTrigger;
        enemy.SetEnemyMovement(Vector3.zero,false);

        player.ResetActor(Vector3.zero,Quaternion.identity);
        int footCrossings=0;
        for(int i=0;i<35;i++)
        {
            float angle=i*.24f;
            player.SetControls(new Vector2(.7f,.4f),new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*.96f);
            yield return new WaitForFixedUpdate();
            if(i<8) continue;
            Quaternion inverse=Quaternion.Inverse(Quaternion.Euler(0f,player.PelvisYaw,0f));
            float left=(inverse*(player.LeftFoot.position-player.Body.position)).x;
            float right=(inverse*(player.RightFoot.position-player.Body.position)).x;
            if(left>-.025f || right<.025f) footCrossings++;
        }

        player.ResetActor(new Vector3(-2.5f,0f,-2.5f),Quaternion.identity);
        enemy.ResetActor(new Vector3(3f,0f,3f),Quaternion.identity);
        float[] radii={.42f,.65f,.96f};
        float[] heights=new float[3];
        for(int p=0;p<3;p++)
        {
            for(int i=0;i<24;i++)
            {
                player.SetControls(Vector2.zero,Vector2.up*radii[p]);
                yield return new WaitForFixedUpdate();
            }
            heights[p]=player.WeaponBody.position.y-player.Body.position.y;
        }
        bool fixedHeight=Mathf.Max(heights[0],heights[1],heights[2])-
            Mathf.Min(heights[0],heights[1],heights[2])<.16f;

        bool doubleTapWorks=false,dragSafe=false;
        if(weaponStick!=null && EventSystem.current!=null)
        {
            var rect=(RectTransform)weaponStick.transform;
            Vector2 center=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var tap=new PointerEventData(EventSystem.current){pointerId=91,position=center};
            weaponStick.OnPointerDown(tap); weaponStick.OnPointerUp(tap);
            yield return null;
            weaponStick.OnPointerDown(tap); weaponStick.OnPointerUp(tap);
            doubleTapWorks=weaponStick.ConsumeDoubleTap();
            weaponStick.OnPointerDown(tap);
            tap.position=center+Vector2.right*42f;
            weaponStick.OnDrag(tap); weaponStick.OnPointerUp(tap);
            tap.position=center;
            weaponStick.OnPointerDown(tap); weaponStick.OnPointerUp(tap);
            dragSafe=!weaponStick.ConsumeDoubleTap();
        }

        player.ResetActor(new Vector3(-2f,0f,-2f),Quaternion.identity);
        foreach(WeaponPickup oldPickup in FindObjectsByType<WeaponPickup>())
        {
            if(Vector3.ProjectOnPlane(oldPickup.transform.position-player.Body.position,Vector3.up).magnitude<1.38f)
            {
                oldPickup.gameObject.SetActive(false);
                Destroy(oldPickup.gameObject);
            }
        }
        WeaponPickup testHammer=WeaponPickup.Spawn(WeaponKind.WarHammer,
            player.Body.position+Vector3.right*.48f);
        yield return new WaitForSeconds(.24f);
        player.HandleWeaponDoubleTap();
        bool pickedHammer=player.EquippedWeapon==WeaponKind.WarHammer && player.WeaponBody!=null;
        // Verify the drop operation directly; the stick's double-tap routing was
        // checked above. A previous probe may have left a pickup at any fixed
        // test coordinate, which would turn a second double-tap into a swap.
        WeaponPickup dropped=player.DropWeapon();
        bool droppedHammer=dropped!=null && dropped.Kind==WeaponKind.WarHammer && player.WeaponBody==null;
        player.ResetActor(new Vector3(-2f,0f,-2f),Quaternion.identity);

        PuppetBodyPart neck=null,wrist=null,knee=null;
        foreach(PuppetBodyPart part in FindObjectsByType<PuppetBodyPart>())
        {
            if(part.Owner!=enemy) continue;
            if(part.Index==2) neck=part;
            else if(part.Index==5) wrist=part;
            else if(part.Index==10) knee=part;
        }
        bool wristBroken=false,kneeBroken=false,neckBroken=false;
        WeaponSpec saber=WeaponSpec.Get(WeaponKind.WoodenSaber);
        PuppetBodyPart[] weakParts={wrist,knee,neck};
        HitZone[] weakZones={HitZone.Wrist,HitZone.Knee,HitZone.Neck};
        for(int test=0;test<weakParts.Length;test++)
        {
            if(weakParts[test]==null) continue;
            enemy.ResetActor(new Vector3(1.4f,0f,1.4f),Quaternion.identity);
            for(int hit=0;hit<2;hit++)
            {
                enemy.ApplyWeaponHit(5.2f,weakParts[test].Body.position,Vector3.right,.25f,
                    weakParts[test],saber.mass,1f,false,weakZones[test],1f,saber,1f);
                yield return new WaitForSeconds(.28f);
            }
            if(test==0) wristBroken=enemy.WeaponBody==null && !enemy.IsDefeated;
            else if(test==1) kneeBroken=enemy.KneeBroken && !enemy.IsDefeated;
            else neckBroken=enemy.IsDefeated && enemy.WeaponBody==null;
        }
        enemy.ResetActor(new Vector3(1.8f,0f,0f),Quaternion.Euler(0f,-90f,0f));
        player.ResetActor(new Vector3(-1.8f,0f,0f),Quaternion.Euler(0f,90f,0f));

        player.GrantBuff(MarionetteController.BuffKind.Movement);
        player.GrantBuff(MarionetteController.BuffKind.Strength);
        player.GrantBuff(MarionetteController.BuffKind.MaxHealth);
        bool buffsWork=player.MaxHealth==200f && player.StrikeStrength>1f;
        player.ClearBuffs();
        buffsWork &= player.MaxHealth==180f && player.StrikeStrength==1f;
        Vector3 contact=Vector3.up;
        HitImpact lightImpact=HitImpact.FromCollision(2f,saber.mass,.35f,.75f,contact,Vector3.right);
        HitImpact heavyImpact=HitImpact.FromCollision(4.5f,saber.mass,.35f,.75f,contact,Vector3.right);
        HitImpact launchImpact=HitImpact.FromCollision(8f,saber.mass,.35f,.75f,contact,Vector3.right);
        HitImpact hammerImpact=HitImpact.FromCollision(4.5f,WeaponSpec.Get(WeaponKind.WarHammer).mass,
            .35f,.75f,contact,Vector3.right);
        bool impactTiers=lightImpact.tier==HitTier.Light && heavyImpact.tier==HitTier.Heavy &&
            launchImpact.tier==HitTier.Launch && lightImpact.damage<heavyImpact.damage &&
            heavyImpact.damage<launchImpact.damage && hammerImpact.damage>heavyImpact.damage;
        WeaponSpec club=WeaponSpec.Get(WeaponKind.BaseballBat);
        player.ResetActor(new Vector3(-1.8f,0f,0f),Quaternion.identity);
        player.ApplyWeaponHit(5.1f,player.Body.position+Vector3.up*1.1f,Vector3.left,
            .25f,null,club.mass,.8f,true,HitZone.Body,0f,club,1f,.85f);
        bool enemyLight=player.LastImpact.tier!=HitTier.Launch && !player.IsKnockedDown;
        player.ResetActor(new Vector3(-1.8f,0f,0f),Quaternion.identity);
        player.ApplyWeaponHit(5.1f,player.Body.position+Vector3.up*1.1f,Vector3.left,
            .25f,null,club.mass,.8f,true,HitZone.Body,0f,club,1f,1.4f);
        bool enemyHeavy=player.LastImpact.tier==HitTier.Launch;
        player.ResetActor(new Vector3(-1.8f,0f,0f),Quaternion.identity);
        foreach(WeaponPickup pickup in FindObjectsByType<WeaponPickup>())
            if(pickup.name!="Ground WarHammer") Destroy(pickup.gameObject);

        bool sweepResolved=(sweptHits>0 && sweepHealth<.99f) ||
            (parries>0 && sweptHits==0 && sweepHealth>.999f);
        bool pass = simultaneousTouch && travel > .35f && steps > 0 && maxFootLead > .27f && maxSwing > HitImpact.MinimumSpeed
            && maxChestPelvisDifference > 3f && sweepResolved && enemyClubHit && headKnockdown
            && recovered && !bodyFell && stayedDown && deathJointError<.18f && restartReady && uniformMovement &&
            footCrossings<15 && fixedHeight && bladeContained && doubleTapWorks && dragSafe && pickedHammer && droppedHammer &&
            wristBroken && kneeBroken && neckBroken && buffsWork && impactTiers && enemyLight && enemyHeavy;
        Debug.Log($"HAOXI_SMOKE_{(pass ? "PASS" : "FAIL")}: travel={travel:F2}m, rootY={maxRootY:F2}m, " +
                  $"interruptFrames={interruptedFrames}, speed={maxPlanarSpeed:F2}m/s, steps={steps}, lead={maxFootLead:F2}m, " +
                  $"plantedSlip={maxPlantedSlip:F3}m/tick, goalSlip={maxPlantedGoalSlip:F3}m/tick, footError={maxFootGoalError:F3}m, plantedError={maxPlantedFootError:F3}m, " +
                  $"plantedContext=[{plantedContext}], " +
                  $"maxSwing={maxSwing:F2}m/s, torsoLag={maxChestPelvisDifference:F1}deg, " +
                  $"minBladeDistance={minBladeDistance:F2}m, sweptHits={sweptHits}, sweepHealth={sweepHealth * 100f:F0}, " +
                  $"enemyClubHit={enemyClubHit}, clubHits={clubHits}, clubCandidates={clubCandidates}, clubDistance={clubDistance.magnitude:F2}m, " +
                  $"dualPointers={simultaneousTouch}, parries={parries}, enemyParries={enemyParries}, guardGap={enemyGuardGap:F2}, guardDamage={guardDamage:F1}, bodyFell={bodyFell}, headKnockdown={headKnockdown}, collapse={maxCollapse:F2}, recovered={recovered}, " +
                  $"deathStayedDown={stayedDown}, deathJointError={deathJointError:F3}m, " +
                  $"restartReady={restartReady}, restartHealth={restartHealth:F2}, restartJointError={restartJointError:F3}m");
        Debug.Log($"HAOXI_SECOND_ROUND: movement={minTravel:F2}-{maxTravel:F2}m, footCrossings={footCrossings}/27, " +
                  $"height={heights[0]:F2}/{heights[1]:F2}/{heights[2]:F2}m, " +
                  $"actorGap={closestActors:F2}m, wristGap={maxWristGap:F2}m, goalGap={maxGoalGap:F2}m, bladeContained={bladeContained}, " +
                  $"enemyCollider={enemyColliderActiveAtSweep}, center={enemyCenterAtSweep}, size={enemy.BodyCollider.bounds.size}, " +
                  $"sampleRaw={sampleOverlapCount}, sampleBody={sampleSawBody}, " +
                  $"activeAtMin={colliderAtMinimum}, centerAtMin={centerAtMinimum}, rawNames={sampleNames}, " +
                  $"sweepQueries={sweepQueries}, raw={rawOverlaps}, enemyRaw={rawEnemies}, full={fullBuffers}, candidates={targetCandidates}, " +
                  $"suppressed={suppressedContacts}, slow={slowContacts}, " +
                  $"doubleTap={doubleTapWorks}, noDragMisfire={dragSafe}, pickup={pickedHammer}, drop={droppedHammer}, " +
                  $"wrist={wristBroken}, knee={kneeBroken}, neck={neckBroken}, buffs={buffsWork}, tiers={impactTiers}, enemyLight={enemyLight}, enemyHeavy={enemyHeavy}");
        director.enabled = true;
        Destroy(gameObject);

        void Sample()
        {
            maxSwing = Mathf.Max(maxSwing, player.WeaponSwingSpeed);
            sawKnockdown |= enemy.IsKnockedDown;
            maxCollapse = Mathf.Max(maxCollapse, enemy.VisualCollapse);
            maxChestPelvisDifference = Mathf.Max(maxChestPelvisDifference,
                Mathf.Abs(Mathf.DeltaAngle(player.ChestYaw, player.PelvisYaw)));
            if(!trackBlade) return;
            Vector3 bladeStart = player.WeaponBody.position + player.WeaponBody.rotation * Vector3.forward * MarionetteController.BladeStart;
            Vector3 bladeEnd = player.WeaponBody.position + player.WeaponBody.rotation * Vector3.forward * player.WeaponLength;
            Vector3 center = enemy.BodyCollider.bounds.center;
            float fraction = Mathf.Clamp01(Vector3.Dot(center - bladeStart, bladeEnd - bladeStart) /
                                           (bladeEnd - bladeStart).sqrMagnitude);
            Vector3 nearest = Vector3.Lerp(bladeStart, bladeEnd, fraction);
            float bladeDistance=Vector3.Distance(center,nearest);
            if(bladeDistance<minBladeDistance)
            {
                minBladeDistance=bladeDistance;
                colliderAtMinimum=enemy.BodyCollider.enabled;
                centerAtMinimum=center;
                sampleOverlapCount=Physics.OverlapCapsuleNonAlloc(bladeStart,bladeEnd,
                    player.WeaponContactRadius,sampleBuffer,(1<<MarionetteController.ActorLayer)|1,
                    QueryTriggerInteraction.Ignore);
                sampleSawBody=false;
                sampleNames="";
                for(int i=0;i<sampleOverlapCount;i++)
                {
                    if(sampleBuffer[i]==enemy.BodyCollider) sampleSawBody=true;
                    sampleNames+=sampleBuffer[i].name+":"+sampleBuffer[i].gameObject.layer+",";
                }
            }
        }
    }
}
#endif
