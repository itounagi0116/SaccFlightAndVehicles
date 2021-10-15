
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class HitDetector : UdonSharpBehaviour
{
    public EngineController EngineControl;
    [System.NonSerializedAttribute] public float LastHitTime = -100;
    [System.NonSerializedAttribute] public EngineController LastAttacker;
    VRC.SDK3.Components.VRCObjectSync VehicleObjectSync;

    private void Start()
    {
        Assert(EngineControl != null, "Start: EngineControl != null");
        if (Networking.LocalPlayer != null)

        VehicleObjectSync = (VRC.SDK3.Components.VRCObjectSync)GetComponent(typeof(VRC.SDK3.Components.VRCObjectSync));
    }
    void OnParticleCollision(GameObject other)
    {
        if (other == null || EngineControl.dead) return;//avatars can't shoot you, and you can't get hurt when you're dead
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "PlaneHit");

        GameObject EnemyObjs = other;
        HitDetector EnemyHitDetector = null;
        AAMController EnemyAAMController = null;
        while (EnemyAAMController == null && EnemyHitDetector == null && EnemyObjs.transform.parent != null)
        {
            EnemyObjs = EnemyObjs.transform.parent.gameObject;
            EnemyHitDetector = EnemyObjs.GetComponent<HitDetector>();
            EnemyAAMController = EnemyObjs.GetComponent<AAMController>();
        }
        if (EnemyHitDetector != null)
        {
            LastAttacker = EnemyHitDetector.EngineControl;
        }
        if (EnemyAAMController != null)
        {
            LastAttacker = EnemyAAMController.EngineControl;
        }
        LastHitTime = Time.time;
    }
    public void PlaneHit()
    {
        EngineControl.PlaneHit();
    }
    public void Respawn()//called by the explode animation on last frame
    {
        if (EngineControl.IsOwner)
        {
            EngineControl.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "Respawn_event");//owner broadcasts because it's more reliable than everyone doing it individually
        }
    }
    public void MoveToSpawn()//called 3 seconds before respawn by animation, to prevent a glitch where the plane will appear where it died for a second for non-owners
    {
        if (EngineControl.IsOwner)
        {
            if (EngineControl.UsingManualSync)
            {
                EngineControl.VehicleTransform.SetPositionAndRotation(EngineControl.Spawnposition, EngineControl.Spawnrotation);
                EngineControl.VehicleRigidbody.velocity = Vector3.zero;
                EngineControl.VehicleRigidbody.angularVelocity = Vector3.zero;
            }
            else
            {
                VehicleObjectSync.Respawn();
            }
        }//this works if done just locally;
    }
    public void NotDead()//called by 'respawn' animation twice because calling on the last frame of animation is unreliable for some reason
    {
        if (EngineControl.IsOwner)
        {
            EngineControl.Health = EngineControl.FullHealth;
        }
        EngineControl.dead = false;
    }
    private void Assert(bool condition, string message)
    {
        if (!condition)
        {
            Debug.LogWarning("Assertion failed : '" + GetType() + " : " + message + "'", this);
        }
    }
}
