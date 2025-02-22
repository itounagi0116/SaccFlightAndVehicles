
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

namespace SaccFlightAndVehicles
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SAV_HUDController : UdonSharpBehaviour
    {
        [Tooltip("Transform of the pilot seat's target eye position, HUDController is automatically moved to this position in Start() to ensure perfect alignment. Not required")]
        public Transform PilotSeatAdjusterTarget;
        public UdonSharpBehaviour SAVControl;
        public Text HUDText_G;
        public Text HUDText_mach;
        public Text HUDText_altitude;
        public Text HUDText_radaraltitude;
        [Tooltip("Subtract this many meters from radar altitude to match 0 to the bottom of the vehicle")]
        public float RadarAltitudeOffset = 1.5f;
        [Tooltip("Meters * (default=feet)")]
        public float AltitudeConversion = 3.28084f;
        public Text HUDText_knots;
        [Tooltip("Meters * (default=knots)")]
        public float SpeedConversion = 1.9438445f;
        public Text HUDText_knotsairspeed;
        public Text HUDText_angleofattack;
        [Tooltip("Hud element that points toward the gruond")]
        public Transform DownIndicator;
        [Tooltip("Hud element that shows pitch angle")]
        public Transform ElevationIndicator;
        [Tooltip("Hud element that shows yaw angle")]
        public Transform HeadingIndicator;
        [Tooltip("Hud element that shows vehicle's direction of movement")]
        public Transform VelocityIndicator;
        private SaccEntity EntityControl;
        [Tooltip("Local distance projected forward for objects that move dynamically, only adjust if the hud is moved forward in order to make it appear smaller")]
        public float distance_from_head = 1.333f;
        public float updateInterval_vectors = 0f;
        public float updateInterval_text = 0.3f;
        public bool OnlyUpdateTextOnVectorUpdateFrame = true;
        private float maxGs = 0f;
        private float check_vectors = 0;
        private float check_text = 0;
        private float SeaLevel;
        private Transform CenterOfMass;
        private Vector3 Vel_Lerper;
        private float Vel_UpdateInterval;
        private float Vel_UpdateTime;
        private Vector3 Vel_PredictedCurVel;
        private Vector3 Vel_LastCurVel;
        private Vector3 Vel_NormalizedExtrapDir;
        private void Start()
        {
            EntityControl = (SaccEntity)SAVControl.GetProgramVariable("EntityControl");

            if (PilotSeatAdjusterTarget) { transform.position = PilotSeatAdjusterTarget.position; }

            SeaLevel = (float)SAVControl.GetProgramVariable("SeaLevel");
            CenterOfMass = EntityControl.CenterOfMass;
        }
        private void OnEnable()
        {
            maxGs = 0f;
        }
        private void LateUpdate()
        {
            float SmoothDeltaTime = Time.smoothDeltaTime;
            bool updatedVectors = false;
            if (check_vectors > updateInterval_vectors)
            {
                updatedVectors = true;
                //Velocity indicator
                if (VelocityIndicator)
                {
                    Vector3 currentvel = (Vector3)SAVControl.GetProgramVariable("CurrentVel");
                    if (currentvel.magnitude < 2)
                    { currentvel = -Vector3.up * 2; }//straight down instead of spazzing out when moving very slow
                    if (EntityControl.IsOwner)
                    {
                        VelocityIndicator.position = transform.position + currentvel;
                    }
                    else
                    {
                        //extrapolate CurrentVel and lerp towards it to smooth out the velocity indicator of non-owners
                        if (currentvel != Vel_LastCurVel)
                        {
                            float tim = Time.time;
                            Vel_UpdateInterval = tim - Vel_UpdateTime;
                            Vel_NormalizedExtrapDir = (currentvel - Vel_LastCurVel) * (1 / Vel_UpdateInterval);
                            Vel_LastCurVel = currentvel;
                            Vel_UpdateTime = tim;
                        }
                        Vel_PredictedCurVel = currentvel + (Vel_NormalizedExtrapDir * (Time.time - Vel_UpdateTime));
                        Vel_Lerper = Vector3.Lerp(Vel_Lerper, Vel_PredictedCurVel, 9f * Time.smoothDeltaTime);
                        VelocityIndicator.position = transform.position + Vel_Lerper;
                    }
                    VelocityIndicator.localPosition = VelocityIndicator.localPosition.normalized * distance_from_head;
                    VelocityIndicator.rotation = Quaternion.LookRotation(VelocityIndicator.position - gameObject.transform.position, gameObject.transform.up);//This makes it face the pilot.
                }
                /////////////////


                //Heading indicator
                Vector3 VehicleEuler = EntityControl.transform.rotation.eulerAngles;
                if (HeadingIndicator) { HeadingIndicator.localRotation = Quaternion.Euler(new Vector3(0, -VehicleEuler.y, 0)); }
                /////////////////

                //Elevation indicator
                if (ElevationIndicator) { ElevationIndicator.rotation = Quaternion.Euler(new Vector3(0, VehicleEuler.y, 0)); }
                /////////////////

                //Down indicator
                if (DownIndicator) { DownIndicator.localRotation = Quaternion.Euler(new Vector3(0, 0, -VehicleEuler.z)); }
                /////////////////
                check_vectors = 0;
            }

            if (check_text > updateInterval_text && (!OnlyUpdateTextOnVectorUpdateFrame || updatedVectors))//update text
            {
                float speed = (float)SAVControl.GetProgramVariable("Speed");
                float vertGs = (float)SAVControl.GetProgramVariable("VertGs");
                Vector3 CurrentVel = (Vector3)SAVControl.GetProgramVariable("CurrentVel");
                if (HUDText_G)
                {
                    if (Mathf.Abs(maxGs) < Mathf.Abs(vertGs))
                    { maxGs = vertGs; }
                    HUDText_G.text = vertGs.ToString("F1") + "\n" + maxGs.ToString("F1");
                }
                if (HUDText_mach) { HUDText_mach.text = (speed / 343f).ToString("F2"); }
                if (HUDText_altitude)
                {
                    HUDText_altitude.text = (CurrentVel.y * 60 * AltitudeConversion).ToString("F0") +
                    "\n" + ((CenterOfMass.position.y - SeaLevel) * AltitudeConversion).ToString("F0");
                }
                if (HUDText_radaraltitude)
                {
                    RaycastHit alt;
                    if (Physics.Raycast(CenterOfMass.position, Vector3.down, out alt, Mathf.Infinity, 2065 /* Default, Water and Environment */, QueryTriggerInteraction.Collide))
                    { HUDText_radaraltitude.text = ((alt.distance - RadarAltitudeOffset) * AltitudeConversion).ToString("F0"); }
                    else
                    { HUDText_radaraltitude.text = string.Empty; }
                }
                if (HUDText_knots) { HUDText_knots.text = (speed * SpeedConversion).ToString("F0"); }
                if (HUDText_knotsairspeed) { HUDText_knotsairspeed.text = (((float)SAVControl.GetProgramVariable("AirSpeed")) * SpeedConversion).ToString("F0"); }

                if (HUDText_angleofattack)
                {
                    if (speed < 2)
                    { HUDText_angleofattack.text = System.String.Empty; }
                    else
                    {
                        if (EntityControl.IsOwner)
                            HUDText_angleofattack.text = ((float)SAVControl.GetProgramVariable("AngleOfAttackPitch")).ToString("F0");
                        else
                        {
                            // recalculate because it's not synced
                            float _ZeroLiftAoA = (float)SAVControl.GetProgramVariable("ZeroLiftAoA");
                            float _AngleOfAttackPitch = Vector3.SignedAngle(EntityControl.transform.forward, Vector3.ProjectOnPlane(CurrentVel, EntityControl.transform.right), EntityControl.transform.right) - _ZeroLiftAoA;
                            HUDText_angleofattack.text = _AngleOfAttackPitch.ToString("F0");
                        }
                    }
                }
                check_text = 0;
            }
            check_text += SmoothDeltaTime;
            check_vectors += SmoothDeltaTime;
        }
    }
}
