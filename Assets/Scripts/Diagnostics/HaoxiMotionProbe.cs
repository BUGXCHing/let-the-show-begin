#if UNITY_EDITOR
using System.Collections;
using System.IO;
using HaoxiKaiyan;
using UnityEngine;

// Observe after the controller's LateUpdate: physics-tick sampling sees the
// previous rendered pose and incorrectly reports the root's travel as foot slip.
[DefaultExecutionOrder(500)]
public sealed class HaoxiMotionProbe : MonoBehaviour
{
    private MarionetteController player, enemy;
    private HaoxiGameDirector director;
    private bool ready;
    private float elapsed, idleMin=100f, idleMax, idleExtension, maxBoneError, maxJointError;
    private float maxIdleHeadSpeed, maxTravelHeadSpeed, spinAngle;
    private int phase=-1, spinStartSteps, spinSteps, errors, frames;
    private Vector3 lastHead, lastRoot;
    private float lastYaw;
    private bool captured;
    private const string Output="Logs/MotionQA";

    private IEnumerator Start()
    {
        yield return null;
        foreach(var actor in FindObjectsByType<MarionetteController>())
            if(actor.IsEnemy) enemy=actor; else player=actor;
        director=FindAnyObjectByType<HaoxiGameDirector>();
        if(player==null || enemy==null || director==null) { Destroy(gameObject); yield break; }
        director.enabled=false;
        Time.timeScale=1f;
        Directory.CreateDirectory(Output);
        Application.logMessageReceived+=CountError;
        ready=true;
    }

    private void CountError(string message,string stack,LogType type)
    {
        if(type==LogType.Exception || type==LogType.Error) errors++;
    }

    private void Update()
    {
        if(!ready) return;
        elapsed+=Time.deltaTime;
        int next=elapsed<3f?0:elapsed<7f?1:elapsed<12f?2:elapsed<17f?3:4;
        if(next!=phase)
        {
            if(phase==2) spinSteps=player.FootSteps-spinStartSteps;
            phase=next; captured=false; frames=0;
            player.ResetActor(Vector3.zero,Quaternion.Euler(0,90,0));
            enemy.ResetActor(new Vector3(4,0,4),Quaternion.Euler(0,-90,0));
            enemy.SetEnemyMovement(Vector3.zero,false);
            lastHead=player.PoseJoints[2].position; lastRoot=player.transform.position;
            lastYaw=player.PelvisYaw;
            if(phase==2) spinStartSteps=player.FootSteps;
        }
        float t=phase==0?elapsed:phase==1?elapsed-3f:phase==2?elapsed-7f:elapsed-12f;
        Vector2 move=phase==1?Vector2.right*(Mathf.FloorToInt(t/.70f)%2==0?1f:-1f):
            phase==3?new Vector2(Mathf.Cos(t*2.1f),Mathf.Sin(t*2.1f))*.62f:Vector2.zero;
        float turn=t<2.5f?t*3.7f:(5f-t)*3.7f;
        Vector2 weapon=phase>=2?new Vector2(Mathf.Sin(turn),Mathf.Cos(turn))*.72f:Vector2.zero;
        player.SetControls(move,weapon);
        if(phase==4) Finish();
    }

    private void LateUpdate()
    {
        if(!ready || phase<0 || phase>3) return;
        var nodes=player.PoseJoints;
        frames++;
        float t=phase==0?elapsed:phase==1?elapsed-3f:phase==2?elapsed-7f:elapsed-12f;
        if(frames>15)
        {
            float dt=Mathf.Max(.001f,Time.deltaTime);
            float headSpeed=((nodes[2].position-lastHead)-(player.transform.position-lastRoot)).magnitude/dt;
            if(phase==0)
            {
                idleMin=Mathf.Min(idleMin,nodes[0].position.y);
                idleMax=Mathf.Max(idleMax,nodes[0].position.y);
                idleExtension=Mathf.Max(idleExtension,Vector3.Distance(nodes[9].position,nodes[11].position)/1.02f,
                    Vector3.Distance(nodes[12].position,nodes[14].position)/1.02f);
                maxIdleHeadSpeed=Mathf.Max(maxIdleHeadSpeed,headSpeed);
            }
            else maxTravelHeadSpeed=Mathf.Max(maxTravelHeadSpeed,headSpeed);
            if(phase==2) spinAngle+=Mathf.Abs(Mathf.DeltaAngle(lastYaw,player.PelvisYaw));
            int[] from={3,4,6,7,9,10,12,13};
            float[] lengths={.48f,.44f,.48f,.44f,.52f,.50f,.52f,.50f};
            for(int i=0;i<from.Length;i++)
                maxBoneError=Mathf.Max(maxBoneError,Mathf.Abs(Vector3.Distance(nodes[from[i]].position,
                    nodes[from[i]+1].position)-lengths[i]));
            maxJointError=Mathf.Max(maxJointError,player.MaxJointError);
        }
        if(!captured && t>1.1f)
        {
            captured=true;
            ScreenCapture.CaptureScreenshot($"{Output}/pose-{phase}.png");
        }
        lastHead=nodes[2].position; lastRoot=player.transform.position; lastYaw=player.PelvisYaw;
    }

    private void Finish()
    {
        ready=false;
        bool pass=errors==0 && idleMin>1.02f && idleMax-idleMin<.07f && idleExtension>.91f &&
            spinSteps>=4 && spinAngle>220f && maxBoneError<.025f && maxJointError<.20f &&
            maxIdleHeadSpeed<1.2f && maxTravelHeadSpeed<8f;
        string report=$"HAOXI_MOTION_{(pass?"PASS":"FAIL")}: idlePelvis={idleMin:F3}-{idleMax:F3}m, " +
            $"supportExtension={idleExtension:F3}, spinSteps={spinSteps}, spinTravel={spinAngle:F0}deg, " +
            $"boneError={maxBoneError:F4}m, jointError={maxJointError:F3}m, " +
            $"headSpeedIdle={maxIdleHeadSpeed:F2}, headSpeedMoving={maxTravelHeadSpeed:F2}m/s, errors={errors}";
        File.WriteAllText(Output+"/result.txt",report);
        Debug.Log(report);
        player.ResetActor(new Vector3(-1.8f,0,0),Quaternion.Euler(0,90,0));
        enemy.ResetActor(new Vector3(1.8f,0,0),Quaternion.Euler(0,-90,0));
        director.enabled=true;
        Destroy(gameObject);
    }

    private void OnDestroy() { Application.logMessageReceived-=CountError; }
}
#endif
