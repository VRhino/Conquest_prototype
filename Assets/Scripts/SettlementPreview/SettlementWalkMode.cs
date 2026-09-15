using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Conquest.SettlementPreview
{
    // UI/presentation only. ECS lifecycle is owned by SettlementHeroLifecycleSystem.
    [RequireComponent(typeof(SettlementPreview))]
    public sealed class SettlementWalkMode : MonoBehaviour
    {
        public static SettlementWalkMode Instance { get; private set; }
        public GameObject heroCameraPrefab;
        [Min(.3f)] public float clearanceRadius = .38f;
        [Min(1.8f)] public float clearanceHeight = 1.9f;
        [Min(0)] public float spawnLift = .12f;
        public bool Requested { get; private set; }
        public Vector3 SpawnPosition { get; private set; }
        public Quaternion SpawnRotation { get; private set; }
        public string Status { get; private set; } = "";
        public bool Ready { get; private set; }
        private SettlementPreview preview;
        private GameObject heroCamera;
        private string previousTag;
        private bool previousListener;
        private CursorLockMode previousCursor;
        private bool previousCursorVisible;
        private float requestedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() { Instance = null; }
        private void OnEnable() { preview = GetComponent<SettlementPreview>(); Instance = this; }
        private void OnDisable() { Exit(); if (Instance == this) Instance = null; }

        public void Enter()
        {
            if (!Application.isPlaying || Requested) return;
            if (!heroCameraPrefab || !preview.generated) { Status = "Falta cámara o geometría del asentamiento."; return; }
            Physics.SyncTransforms();
            var streets = preview.generated.Find("calles");
            if (!streets) { Status = "No hay calles para entrar."; return; }
            bool found = false;
            Vector3 spawn = default;
            // Nearest clear street centre to the city origin, not the occupied town-centre footprint.
            foreach (Transform street in streets.Cast<Transform>().OrderBy(t => (t.position-transform.position).sqrMagnitude))
            {
                Vector3 p = street.position; p.y = transform.position.y + spawnLift;
                if (Physics.CheckCapsule(p + Vector3.up * clearanceRadius,
                    p + Vector3.up * (clearanceHeight-clearanceRadius), clearanceRadius, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!Physics.Raycast(p + Vector3.up, Vector3.down, out var ground, 3, ~0, QueryTriggerInteraction.Ignore)
                    || ground.collider.gameObject.name != "Suelo") continue;
                spawn = p;
                SpawnRotation = Quaternion.LookRotation(street.localScale.x >= street.localScale.z ? transform.right : transform.forward);
                found = true; break;
            }
            if (!found) { Status = "No hay espacio libre para la cápsula en las calles."; return; }
            SpawnPosition = spawn;
            previousCursor = Cursor.lockState; previousCursorVisible = Cursor.visible;
            Requested = true; requestedAt = Time.unscaledTime;
            Status = "Preparando héroe... Escape cancela.";
        }

        // Called by the ECS lifecycle only once its visual exists.
        public void PresentHero()
        {
            if (!Requested || Ready) return;
            var overhead = preview.viewCamera;
            previousTag = overhead.tag;
            var listener = overhead.GetComponent<AudioListener>();
            previousListener = listener && listener.enabled;
            overhead.enabled = false; overhead.tag = "Untagged";
            if (listener) listener.enabled = false;
            heroCamera = Instantiate(heroCameraPrefab);
            heroCamera.name = "Settlement Hero Camera";
            heroCamera.transform.position = SpawnPosition + new Vector3(0,2,-4);
            heroCamera.transform.LookAt(SpawnPosition + Vector3.up);
            heroCamera.GetComponent<HeroCameraController>().SetOrbitHeading(SpawnRotation.eulerAngles.y);
            Ready = true;
            Status = "WASD: caminar · Shift: correr · ratón: mirar · rueda: distancia · Escape: volver";
        }
        public void Fail(string message) { Exit(); Status = message; }
        public void Exit()
        {
            bool wasRequested = Requested;
            Requested = false;
            if (heroCamera) { heroCamera.SetActive(false); Destroy(heroCamera); heroCamera = null; }
            if (Ready && preview && preview.viewCamera)
            {
                preview.viewCamera.enabled = true; preview.viewCamera.tag = previousTag;
                var listener = preview.viewCamera.GetComponent<AudioListener>(); if (listener) listener.enabled = previousListener;
            }
            Ready = false;
            if (wasRequested) { Cursor.lockState = previousCursor; Cursor.visible = previousCursorVisible; }
            Status = "";
        }
        private void Update()
        {
            if (Requested && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Exit();
            if (Requested && !Ready && Time.unscaledTime-requestedAt > 20) Fail("No se pudo cargar el héroe. Revisa la subescena SettlementHeroWorld.");
        }
    }
}
