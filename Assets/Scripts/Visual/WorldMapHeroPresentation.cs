using Conquest.WorldMap;
using ConquestTactics.Animation;
using UnityEngine;

namespace ConquestTactics.Visual
{
    /// <summary>Animates a campaign symbol from its confirmed displacement; owns no movement or input.</summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class WorldMapHeroPresentation : MonoBehaviour
    {
        public WorldMapCampaignController campaign;
        public Animator visualAnimator;
        [SerializeField] LocomotionAnimationSettings locomotion = new LocomotionAnimationSettings();
        [Tooltip("Natural speed of the source Synty walk at model scale 1.")]
        [Min(.01f)] public float walkClipSpeed = 1.457155f;
        [Tooltip("Natural speed of the source Synty run at model scale 1.")]
        [Min(.01f)] public float runClipSpeed = 2.595091f;
        [Min(.01f)] public float turnSharpness = 12;
        [Min(1)] public float teleportDistance = 25;
        [Min(0)] public float stationaryGraceSeconds = .08f;
        public float ObservedSpeed { get; private set; }
        public LocomotionGait Gait { get; private set; }
        Vector3 previousPosition;
        float stationaryTime;
        bool wasMoving;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!visualAnimator) visualAnimator=GetComponentInChildren<Animator>();
            if (!campaign || !visualAnimator) { enabled=false; return; }
            visualAnimator.applyRootMotion=false;
            ResetObservation();
        }
        void Update() => Observe(Time.deltaTime);
        public void ResetObservation()
        {
            previousPosition=transform.position;stationaryTime=0;ObservedSpeed=0;wasMoving=false;
            if(visualAnimator)Drive(false,Vector3.forward);
        }
        public void Observe(float seconds)
        {
            if (!campaign || !visualAnimator || seconds<=0) return;
            var delta=transform.position-previousPosition;previousPosition=transform.position;delta.y=0;
            if(delta.magnitude>teleportDistance) { ResetObservation();return; }
            float speed=delta.magnitude/seconds;
            stationaryTime=speed<=locomotion.stoppedSpeed?stationaryTime+seconds:0;
            bool stopped=(!campaign.authoritativePosition&&!campaign.moving)||stationaryTime>=stationaryGraceSeconds;
            ObservedSpeed=stopped?0:speed>locomotion.stoppedSpeed?speed:ObservedSpeed;
            var direction=delta.sqrMagnitude>0?delta.normalized:visualAnimator.transform.forward;
            if(!stopped&&delta.sqrMagnitude>0)
                visualAnimator.transform.rotation=Quaternion.Slerp(visualAnimator.transform.rotation,Quaternion.LookRotation(direction),1-Mathf.Exp(-turnSharpness*seconds));
            Drive(!stopped&&ObservedSpeed>locomotion.stoppedSpeed,direction);
        }
        void Drive(bool moving,Vector3 direction)
        {
            // Symbol enlargement is visual: account for it in stride cadence, never in map speed.
            float scale=Mathf.Max(.01f,Mathf.Abs(visualAnimator.transform.lossyScale.y));
            Gait=moving?locomotion.GetGait(ObservedSpeed/scale,runClipSpeed,false):LocomotionGait.Idle;
            bool started=moving&&!wasMoving;
            float blend=Gait==LocomotionGait.Walk?locomotion.walkBlendSpeed:Gait==LocomotionGait.Run?locomotion.runBlendSpeed:0;
            float naturalSpeed=Gait==LocomotionGait.Walk?walkClipSpeed:runClipSpeed;
            visualAnimator.speed=moving?Mathf.Clamp(ObservedSpeed/(naturalSpeed*scale),.15f,2):1;
            var local=visualAnimator.transform.InverseTransformDirection(direction);
            visualAnimator.SetFloat(AnimationHashes.MoveSpeed,blend);
            visualAnimator.SetInteger(AnimationHashes.CurrentGait,(int)Gait);
            visualAnimator.SetBool(AnimationHashes.IsGrounded,true);
            visualAnimator.SetBool(AnimationHashes.IsStopped,!moving);
            visualAnimator.SetBool(AnimationHashes.IsStarting,started);
            visualAnimator.SetBool(AnimationHashes.MovementInputPressed,started);
            visualAnimator.SetBool(AnimationHashes.MovementInputTapped,started);
            visualAnimator.SetBool(AnimationHashes.MovementInputHeld,moving&&!started);
            visualAnimator.SetBool(AnimationHashes.IsWalking,Gait==LocomotionGait.Walk);
            visualAnimator.SetFloat(AnimationHashes.IsStrafing,moving?1:0);
            visualAnimator.SetFloat(AnimationHashes.StrafeDirectionX,moving?local.x:0);
            visualAnimator.SetFloat(AnimationHashes.StrafeDirectionZ,moving?local.z:1);
            visualAnimator.SetFloat(AnimationHashes.ForwardStrafe,moving?1:0);
            visualAnimator.SetFloat(AnimationHashes.LocomotionStartDirection,0);
            visualAnimator.SetBool(AnimationHashes.IsTurningInPlace,false);
            visualAnimator.SetBool(AnimationHashes.IsAttacking,false);
            wasMoving=moving;
        }
        void OnDisable()
        {
            if(visualAnimator&&Application.isPlaying){ObservedSpeed=0;Drive(false,Vector3.forward);}
        }
    }
}
