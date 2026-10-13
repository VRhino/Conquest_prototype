using System;
using System.Linq;
using System.IO;
using System.Text;
using UnityEngine;
using Conquest.WorldMap;
using Conquest.WorldMap.Editor;
using ConquestTactics.Visual;
using ConquestTactics.Animation;
public static class ValidateWorldMapHero
{
 static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
 static string Clips(Animator a)=>string.Join(",",a.GetCurrentAnimatorClipInfo(0).Where(x=>x.weight>.05f).Select(x=>x.clip.name));
 public static string Run()
 {
  Require(Application.isPlaying,"Enter Play");
  var h=UnityEngine.Object.FindAnyObjectByType<WorldMapHeroPresentation>();var c=h.campaign;var a=h.visualAnimator;
  var trial=c.GetComponent<WorldMapCalibrationTrial>();Require(trial,"Run the calibration scene");
  Require(Vector3.Distance(h.transform.localScale,Vector3.one*6)<.001f&&Mathf.Abs(c.scaleContract.heroSymbolScale-6)<.001f,"Approved scale6 missing");
  Require(a.isHuman&&!a.applyRootMotion,"Humanoid / root motion incorrect");
  Require(a.runtimeAnimatorController.name=="AC_Polygon_Masculine","Different locomotion controller");
  Require(h.GetComponentsInChildren<MonoBehaviour>(true).All(b=>b==h),"Unexpected input/motor/sync behaviour on symbol");
  Require(h.GetComponentsInChildren<Collider>(true).Length==0&&h.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true).Length==0,"Competing movement/collision owner");
  float renderedHeight=h.GetComponentsInChildren<SkinnedMeshRenderer>().Max(r=>r.bounds.max.y)-h.GetComponentsInChildren<SkinnedMeshRenderer>().Min(r=>r.bounds.min.y);
  Require(renderedHeight>8&&renderedHeight<15,"Hero visual size incorrect: "+renderedHeight);
  var report=new StringBuilder();report.AppendLine("Approved root scale6; rendered idle bounds height="+renderedHeight.ToString("F3")+" u; 14 active modular skins.");
  bool cEnabled=c.enabled,hEnabled=h.enabled;
  try
  {
   c.enabled=false;h.enabled=false;c.SetTravelKind(CampaignTravelKind.Light);trial.ResetTrial();h.ResetObservation();
   var fixedRoot=c.actor.position;for(int i=0;i<10;i++)a.Update(.1f);
   Require(Vector3.Distance(fixedRoot,c.actor.position)<.001f,"Idle animation moves the map actor");
   Require(Clips(a).Contains("Idle"),"Idle clip not selected: "+Clips(a));
   report.AppendLine("Idle: "+Clips(a));
   c.moving=true;
   for(int i=0;i<30;i++){c.Advance(.1f);h.Observe(.1f);a.Update(.1f);}
   Require(h.Gait==LocomotionGait.Walk&&a.GetFloat(AnimationHashes.MoveSpeed)==3,"Walk blend incorrect");
   Require(Clips(a).Contains("Walk"),"Walk clip not selected: "+Clips(a));
   fixedRoot=c.actor.position;a.Update(.5f);Require(Vector3.Distance(fixedRoot,c.actor.position)<.001f,"Root motion alters travel");
   report.AppendLine("Walk: "+Clips(a)+"; cadence="+a.speed.ToString("F3")+"; map speed6 unchanged.");
   c.StopTravel();h.Observe(.1f);for(int i=0;i<20;i++)a.Update(.2f);
   Require(h.Gait==LocomotionGait.Idle&&a.GetFloat(AnimationHashes.MoveSpeed)==0,"Pause fails to stop animation");
   Require(!a.GetBool(AnimationHashes.MovementInputHeld)&&!a.GetBool(AnimationHashes.MovementInputPressed)&&!a.GetBool(AnimationHashes.MovementInputTapped),"Input flags left active at rest");
   Require(Clips(a).Contains("Idle"),"Paused clip not idle: "+Clips(a));
   c.moving=true;for(int i=0;i<30;i++){c.Advance(.1f);h.Observe(.1f);a.Update(.1f);}
   Require(h.Gait==LocomotionGait.Walk&&Clips(a).Contains("Walk"),"Resume failed");
   report.AppendLine("PASS: pause returns to Idle; all movement flags clear; resume returns to Walk.");
   c.demoRoute=new[]{c.actor.position,c.actor.position+Vector3.forward*90};c.segment=1;
   for(int i=0;i<10;i++){c.Advance(.1f);h.Observe(.1f);a.Update(.1f);}
   Require(Vector3.Angle(a.transform.forward,Vector3.forward)<20,"Hero heading differs from route");
   report.AppendLine("PASS: turn follows route; animation never owns root position.");
   // Exercise the run branch without changing the saved strategic speed profiles.
   c.movementSpeed=15;for(int i=0;i<30;i++){c.Advance(.1f);h.Observe(.1f);a.Update(.1f);}
   Require(h.Gait==LocomotionGait.Run&&Clips(a).Contains("Run"),"Run branch failed: "+Clips(a));
   report.AppendLine("Run branch: "+Clips(a)+"; temporary validation speed15, not saved.");
   c.actor.position+=Vector3.right*100;h.Observe(.1f);Require(h.ObservedSpeed==0&&h.Gait==LocomotionGait.Idle,"Teleport triggers false sprint");
   report.AppendLine("PASS: teleport resets velocity; no WASD/input, ECS sync, CharacterController or NavMeshAgent on hero.");
  }
  finally
  {
   c.SetTravelKind(CampaignTravelKind.Light);trial.ResetTrial();h.ResetObservation();c.enabled=cEnabled;h.enabled=hEnabled;
  }
  File.WriteAllText(WorldMapCalibrationBuilder.Folder+"/HeroValidation.txt",report.ToString());return report.ToString();
 }
}
