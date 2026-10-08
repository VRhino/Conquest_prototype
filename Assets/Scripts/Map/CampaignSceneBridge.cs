using Conquest.WorldMap;

using UnityEngine;

using UnityEngine.SceneManagement;

// Bridges presentation requests through the existing scene service. No ECS entities are created here.

public sealed class CampaignSceneBridge : MonoBehaviour {

 static CampaignSceneBridge instance;

 public string campaignScene="WorldMapCampaign",settlementScene="SettlementScene";

 WorldMapCampaignController controller;bool returning,busy;
 TextAsset entrySource;string entryId,entryName;

 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){instance=null;}

 void Awake(){if(instance&&instance!=this){Destroy(gameObject);return;}instance=this;DontDestroyOnLoad(gameObject);}

 void OnEnable(){if(instance!=this)return;SceneManager.sceneLoaded+=Loaded;}

 void Start(){if(instance==this)Bind();}

 void OnDisable(){if(instance!=this)return;SceneManager.sceneLoaded-=Loaded;if(controller)controller.EnterRequested-=Enter;}

 void Loaded(Scene scene,LoadSceneMode mode){if(mode!=LoadSceneMode.Single)return;busy=false;Bind();if(scene.name==settlementScene&&returning){var preview=Object.FindFirstObjectByType<Conquest.SettlementPreview.SettlementPreview>();if(preview&&entrySource){preview.source=entrySource;preview.settlementId=entryId;}}}

 void Bind(){if(controller)controller.EnterRequested-=Enter;controller=Object.FindFirstObjectByType<WorldMapCampaignController>();if(controller){controller.EnterRequested+=Enter;returning=false;SceneTransitionService.UpdateGamePhase(SceneTransitionService.SceneNames.WorldMap);}}

 public void Enter(){if(busy||!controller||!controller.CanEnterSelected)return;var target=controller.selectedSettlement;entrySource=target.previewSource;entryId=target.previewSettlementId;entryName=target.displayName;WorldMapCampaignReturn.Store(controller.Capture());returning=true;busy=true;controller.moving=false;controller.streamer.Suspend();SceneTransitionService.LoadSceneAsync(settlementScene);}

 public void Return(){if(busy||!returning)return;var walk=Object.FindFirstObjectByType<Conquest.SettlementPreview.SettlementWalkMode>();if(walk)walk.Exit();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;busy=true;SceneTransitionService.LoadSceneAsync(campaignScene);}

 void Update(){if(returning&&!controller&&!busy&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==settlementScene&&UnityEngine.InputSystem.Keyboard.current!=null&&UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)Return();}
 void OnGUI(){if(returning&&!controller&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==settlementScene){var r=new Rect(Screen.width-310,18,292,92);Conquest.WorldMap.WorldMapCampaignHud.Panel(r);GUI.Label(new Rect(r.x+14,r.y+10,264,24),entryName+" · Alt: cursor / Esc: salir");GUI.enabled=!busy;if(GUI.Button(new Rect(r.x+14,r.y+40,264,38),busy?"Volviendo…":"Salir al mapa")){var walk=Object.FindFirstObjectByType<Conquest.SettlementPreview.SettlementWalkMode>();if(walk)walk.Exit();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;Return();}GUI.enabled=true;}}
}
