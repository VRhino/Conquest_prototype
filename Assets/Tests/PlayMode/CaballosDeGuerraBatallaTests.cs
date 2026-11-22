#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using ConquestTactics.Visual;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

/// <summary>
/// Los caballos de guerra en la escena de batalla (tema 7): el héroe monta con cada pelaje (0, el caballo de antes; 1 a 5, las cinco monturas del
/// motor) y se fotografía parado y galopando, de lado («batalla_caballo_{pelaje}_parado|galope.png» en `MUNDO_CAPTURAS`). Solo lo visual: el
/// montar, el galope y el asiento son los de siempre.
/// </summary>
public partial class BattleSceneIntegrationTests
{
    [UnityTest, Explicit("tema 7: los caballos de guerra del héroe en batalla")]
    public IEnumerator CaballosDeGuerraEnBatalla()
    {
        var previousInput = UseUnfocusedEditorInput();
        var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
        var camaraGo = new GameObject("CamaraCaballos");
        var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
        try
        {
            yield return PrepareBaseline();
            Assert.That(CaballosDeGuerra.Cargar(), Is.Not.Null, "falta Resources/Mounts/CaballosDeGuerra");
            Assert.That(VisualSyncUtility.TryGetVisual(testWorld, hero, out var visual), Is.True);
            var presenter = visual.GetComponent<HeroMountPresenter>();
            var camara = camaraGo.AddComponent<Camera>();
            camara.targetTexture = rt;
            camara.fieldOfView = 35;
            IEnumerator Foto(string nombre)
            {
                var t = visual.transform;
                var centro = t.position + Vector3.up * 1.3f;
                // (de lado y algo desde arriba, para ver por encima de la tropa que lo rodea)
                camara.transform.position = centro + t.right * 6.5f + Vector3.up * 2.4f;
                camara.transform.LookAt(centro);
                yield return null;
                camara.Render();
                if (string.IsNullOrEmpty(carpeta)) yield break;
                var antes = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = antes;
                File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
            }
            for (byte pelaje = 0; pelaje <= 5; pelaje++)
            {
                var m = HeroMount;
                m.coat = pelaje;
                em.SetComponentData(hero, m);
                yield return Press(Key.G);
                yield return Until(() => HeroMount.state == HeroMountState.Mounted, 4f, $"no montó con el pelaje {pelaje}");
                yield return new WaitForSeconds(0.6f);
                Debug.Log($"[Caballos] pelaje {pelaje} ({VestidoDelMotor.MonturaDePelaje(pelaje) ?? "el de antes"}): malla {presenter.Horse.GetComponentsInChildren<SkinnedMeshRenderer>()[0].sharedMesh.name}");
                yield return Foto($"batalla_caballo_{pelaje}_parado");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(MarchKey, Key.LeftShift));
                yield return new WaitForSeconds(1.0f);
                yield return Foto($"batalla_caballo_{pelaje}_galope");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(2.5f);
                yield return Press(Key.G);
                yield return Until(() => HeroMount.state == HeroMountState.OnFoot, 3f, "no desmontó");
                yield return new WaitForSeconds(1.8f);
            }
        }
        finally
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            UnityEngine.Object.Destroy(camaraGo);
            rt.Release();
            RestoreInputSettings(previousInput);
        }
    }
}
#endif
