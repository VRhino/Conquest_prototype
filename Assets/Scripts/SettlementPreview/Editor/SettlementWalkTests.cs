using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Conquest.SettlementPreview.Editor
{
    // Play-mode smoke test using a temporary input device. Does not read/write player saves.
    public static class SettlementWalkTests
    {
        public static async Task<string> Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Requiere Play en SettlementPreview.");
            var walk = SettlementWalkMode.Instance;
            if (!walk) throw new InvalidOperationException("Falta SettlementWalkMode.");
            var geometry = walk.GetComponent<SettlementPreview>();
            foreach (Transform street in geometry.generated.Find("calles"))
                if (Mathf.Min(street.localScale.x, street.localScale.z) < 10.49f)
                    throw new Exception("Calle por debajo de los 10.5 metros de referencia.");
            Keyboard keyboard = null;
            var disabledKeyboards = new List<Keyboard>();
            bool previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            try
            {
                walk.Exit(); await Task.Delay(250);
                walk.Enter(); await Until(() => walk.Ready);
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                using var heroes = em.CreateEntityQuery(typeof(IsLocalPlayer));
                if (heroes.CalculateEntityCount() != 1) throw new Exception("Debe haber un solo héroe local.");
                Entity hero = heroes.GetSingletonEntity();
                keyboard = InputSystem.AddDevice<Keyboard>("Settlement smoke keyboard");
                foreach (var other in InputSystem.devices.OfType<Keyboard>())
                    if (other != keyboard && other.enabled) { InputSystem.DisableDevice(other); disabledKeyboards.Add(other); }
                keyboard.MakeCurrent();
                Camera.main.GetComponent<HeroCameraController>().staticCamera = true;
                Camera.main.transform.rotation = Quaternion.LookRotation(Vector3.right);
                Vector3 start = em.GetComponentData<LocalTransform>(hero).Position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                InputSystem.Update();
                await Task.Delay(1200);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                await Task.Delay(100);
                Vector3 end = em.GetComponentData<LocalTransform>(hero).Position;
                float moved = Vector3.Distance(start, end);
                if (moved < 1 || moved > 8) throw new Exception("Movimiento WASD inesperado: " + moved + "; input=" + em.GetComponentData<HeroInputComponent>(hero).MoveInput);
                if (Mathf.Abs(end.y) > .3f) throw new Exception("El héroe no permanece sobre el suelo.");
                if (UnityEngine.Object.FindObjectsByType<UnityEngine.AI.NavMeshAgent>().Length != 0) throw new Exception("No debe haber NavMeshAgents.");

                // Return to the entrance; left relative to east-facing camera heads into the town centre.
                walk.Exit(); await Task.Delay(250); walk.Enter(); await Until(() => walk.Ready); await Task.Delay(300);
                hero = heroes.GetSingletonEntity();
                keyboard.MakeCurrent();
                Camera.main.GetComponent<HeroCameraController>().staticCamera = true;
                Camera.main.transform.rotation = Quaternion.LookRotation(Vector3.right);
                var preview = walk.GetComponent<SettlementPreview>();
                var origin = (Vector3)em.GetComponentData<LocalTransform>(hero).Position + Vector3.up * .9f;
                var blocker = Physics.RaycastAll(origin, Vector3.forward, 100, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => h.collider.transform.IsChildOf(preview.generated)).OrderBy(h => h.distance).FirstOrDefault();
                if (!blocker.collider) throw new Exception("No hay bloqueador generado para probar colisión.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A, Key.Q, Key.E, Key.R));
                InputSystem.Update();
                await Task.Delay(3200);
                var input = em.GetComponentData<HeroInputComponent>(hero);
                var wallPosition = em.GetComponentData<LocalTransform>(hero).Position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                if (input.UseSkill1 || input.UseSkill2 || input.UseUltimate || input.IsAttackPressed) throw new Exception("Se filtró input de combate.");
                float wallZ = blocker.collider.bounds.min.z;
                if (wallPosition.z > wallZ-.20f || wallPosition.z < wallZ-.65f) throw new Exception("Colisión con edificio incorrecta: " + wallPosition);
                walk.Exit(); await Task.Delay(300);
                if (heroes.CalculateEntityCount() != 0) throw new Exception("Héroe residual tras salir.");
                if (Camera.main == null || Camera.main.name != "Settlement Camera") throw new Exception("No volvió la cámara del visor.");
                if (UnityEngine.Object.FindObjectsByType<ConquestTactics.Visual.EntityVisualSync>().Length != 0) throw new Exception("Visual residual.");
                var result = "PASS: WASD " + moved.ToString("F2") + " u; suelo estable; pared bloquea en " + wallPosition + "; sin NavMesh; habilidades desactivadas; reentrada y limpieza correctas.";
                Debug.Log(result);
                return result;
            }
            finally
            {
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                foreach (var disabled in disabledKeyboards) if (disabled != null && disabled.added) InputSystem.EnableDevice(disabled);
                if (walk) walk.Exit();
                Application.runInBackground = previousBackground;
            }
        }
        private static async Task Until(Func<bool> ready)
        {
            for (int i=0; i<150; i++) { if (ready()) return; await Task.Delay(100); }
            throw new TimeoutException("El héroe no estuvo listo en 15 segundos.");
        }
    }
}
