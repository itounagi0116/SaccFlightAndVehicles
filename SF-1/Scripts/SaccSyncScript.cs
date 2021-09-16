﻿
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class SaccSyncScript : UdonSharpBehaviour
{
    // whispers to Zwei, "it's okay"
    [SerializeField] private EngineController EngineControl;
    [SerializeField] private Transform VehicleTransform;
    [Tooltip("In seconds")]
    [Range(0.05f, 1f)]
    [SerializeField] private float updateInterval = 0.1f;
    [Tooltip("Should never be more than update interval")]
    [Range(0.01f, 1f)]
    [SerializeField] private float SmoothingTime = 0.1f;
    //[SerializeField] private int SmoothingFrames = 5;
    private VRCPlayerApi localPlayer;
    private float nextUpdateTime = 0;
    [UdonSynced(UdonSyncMode.None)] private int O_UpdateTime;
    [UdonSynced(UdonSyncMode.None)] private Vector3 O_Position = Vector3.zero;
    [UdonSynced(UdonSyncMode.None)] private Quaternion O_Rotation = Quaternion.identity;
    [UdonSynced(UdonSyncMode.None)] private Vector3 O_CurVel = Vector3.zero;
    private Vector3 O_LastCurVel = Vector3.zero;
    private Vector3 O_LastCurVel2 = Vector3.zero;
    private Quaternion CurAngMom = Quaternion.identity;
    private Quaternion LastCurAngMom = Quaternion.identity;
    private Quaternion O_LastRotation = Quaternion.identity;
    private Quaternion O_LastRotation2 = Quaternion.identity;
    private Quaternion RotationLerper = Quaternion.identity;
    private float Ping;
    private float LastPing;
    private int L_UpdateTime;
    private int L_LastUpdateTime;
    private int O_LastUpdateTime;
    private int O_LastUpdateTime2;
    //make everyone think they're the owner for the first frame so that don't set the position to 0,0,0 before SFEXT_L_EntityStart runs
    private bool IsOwner = true;
    private Vector3 lerpedCurVel;
    private Vector3 Acceleration;
    private Vector3 LastAcceleration;
    private Vector3 O_LastPosition;
    private Vector3 O_LastPosition2;
    private int CurrentSmoothFrame;
    private float SmoothFrameDivider;

    private void Start()
    {
        localPlayer = Networking.LocalPlayer;
        if (localPlayer != null && localPlayer.isMaster)
        { IsOwner = true; }
        else { IsOwner = false; }
        SmoothFrameDivider = 1f / (float)SmoothingTime;
    }
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (player.isLocal)
        { IsOwner = true; }
        else
        { IsOwner = false; }
    }
    private void Update()
    {
        if (IsOwner)
        {
            if (Time.time > nextUpdateTime)
            {
                O_Position = VehicleTransform.position;
                O_Rotation = VehicleTransform.rotation;
                O_CurVel = EngineControl.CurrentVel;
                O_UpdateTime = Networking.GetServerTimeInMilliseconds();
                RequestSerialization();
                if (EngineControl.Piloting)
                {
                    nextUpdateTime = (Time.time + updateInterval);
                }
                else
                {
                    nextUpdateTime = (Time.time + (updateInterval * 3));
                }
            }
        }
        else
        {
            float TimeSinceUpdate = ((float)(Networking.GetServerTimeInMilliseconds() - L_UpdateTime) * .001f);
            Vector3 PredictedPosition = O_Position
                 + ((O_CurVel + Acceleration) * Ping)
                 + ((O_CurVel + Acceleration) * TimeSinceUpdate);
            Quaternion PredictedRotation =
                (Quaternion.SlerpUnclamped(Quaternion.identity, CurAngMom, Ping + TimeSinceUpdate)
                * O_Rotation);
            if (TimeSinceUpdate < updateInterval)
            {
                float TimeSincePreviousUpdate = ((float)(Networking.GetServerTimeInMilliseconds() - L_LastUpdateTime) * .001f);
                Vector3 OldPredictedPosition = O_LastPosition2
                     + ((O_LastCurVel2 + LastAcceleration) * LastPing)
                     + ((O_LastCurVel2 + LastAcceleration) * TimeSincePreviousUpdate);

                Quaternion OldPredictedRotation =
                (Quaternion.SlerpUnclamped(Quaternion.identity, LastCurAngMom, LastPing + TimeSincePreviousUpdate)
                * O_LastRotation2);
                RotationLerper = Quaternion.Slerp(RotationLerper, Quaternion.Slerp(OldPredictedRotation, PredictedRotation, (float)TimeSinceUpdate * SmoothFrameDivider), Time.smoothDeltaTime * SmoothFrameDivider);

                VehicleTransform.SetPositionAndRotation(
                    Vector3.Lerp(OldPredictedPosition, PredictedPosition, (float)TimeSinceUpdate * SmoothFrameDivider),
                     RotationLerper);
                CurrentSmoothFrame++;
            }
            else
            {
                RotationLerper = Quaternion.Slerp(RotationLerper, PredictedRotation, Time.smoothDeltaTime * SmoothFrameDivider);
                VehicleTransform.SetPositionAndRotation(PredictedPosition, RotationLerper);
            }
        }
    }
    public override void OnDeserialization()
    {
        if (O_UpdateTime != O_LastUpdateTime)//only do anything if OnDeserialization was for this script
        {
            LastAcceleration = Acceleration;
            LastPing = Ping;
            L_LastUpdateTime = L_UpdateTime;
            LastCurAngMom = CurAngMom;
            float updatedelta = (O_UpdateTime - O_LastUpdateTime) * .001f;
            float speednormalizer = 1 / updatedelta;

            L_UpdateTime = Networking.GetServerTimeInMilliseconds();
            Ping = (float)(L_UpdateTime - O_UpdateTime) * .001f;
            Acceleration = O_CurVel - O_LastCurVel;
            //Acceleration *= speednormalizer;
            Debug.Log(string.Concat("Acceleration: ", Acceleration.ToString()));

            //rotate Acceleration by the difference in rotation of plane plane between last and this update to make it match the angle for the next frame better
            Quaternion PlaneRotDif = O_Rotation * Quaternion.Inverse(O_LastRotation);
            Acceleration = PlaneRotDif * Acceleration;

            CurAngMom = Quaternion.SlerpUnclamped(Quaternion.identity, PlaneRotDif, speednormalizer);

            O_LastUpdateTime2 = O_LastUpdateTime;
            O_LastRotation2 = O_LastRotation;
            O_LastPosition2 = O_LastPosition;
            O_LastCurVel2 = O_LastCurVel;

            O_LastUpdateTime = O_UpdateTime;
            O_LastRotation = O_Rotation;
            O_LastPosition = O_Position;
            O_LastCurVel = O_CurVel;
            CurrentSmoothFrame = 1;
        }
    }
}