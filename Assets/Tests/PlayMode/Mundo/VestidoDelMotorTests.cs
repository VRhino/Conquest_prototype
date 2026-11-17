// El héroe vestido pieza a pieza con el equipo del motor (paso 6 del bloque 4, tanda 2), sin servidor: el héroe modular de Conquest con cada
// clase del motor (su arma, con las provisionales de Synty: hacha, par de dagas, lanza con escudo redondo) y un casco de cada peso, y las piezas
// que deben verse. Si `MUNDO_CAPTURAS` dice una carpeta, una foto de cada uno.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConquestTactics.Mundo.UI.Tests
{
    public class VestidoDelMotorTests
    {
        static readonly (string clase, string casco, string[] mallas)[] Casos =
        {
            ("espada_escudo_mediano", null, new[] { "SM_Wep_Sword_01", "SM_Wep_Shield_02" }),
            ("lanza_escudo_redondo", "casco_ligera_bronce", new[] { "SM_Wep_Spear_10", "SM_Wep_Shield_05", "Chr_HeadCoverings_No_Hair_13" }),
            ("hacha_dos_manos", "casco_media_hierro", new[] { "SM_Wep_Axe_01", "Chr_HeadCoverings_No_Hair_02" }),
            ("dagas", "casco_pesada_bronce_laminado", new[] { "SM_Wep_Dagger_01", "Chr_HeadCoverings_No_Hair_04" }),
            ("arco_lanza", null, new[] { "SM_Prop_Bow_02" }),
        };

        [UnityTest]
        public IEnumerator CadaClaseConSuArmaYSuCasco()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/esqueletos/ModularHeroVisual.prefab");
#else
            GameObject prefab = null;
            Assert.Ignore("solo en el editor");
#endif
            var carpeta = System.Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            foreach (var (clase, casco, mallas) in Casos)
            {
                var raiz = new GameObject("Prueba vestido");
                raiz.SetActive(false);
                raiz.transform.position = new Vector3(0, -3000, 0);
                var modelo = Object.Instantiate(prefab, raiz.transform, false);
                modelo.transform.localPosition = Vector3.zero;
                modelo.transform.localRotation = Quaternion.identity;
                foreach (var m in modelo.GetComponentsInChildren<MonoBehaviour>(true)) if (!(m is AvatarPartSelector)) Object.DestroyImmediate(m);
                var piezas = new Dictionary<string, string> { ["torso"] = "torso_media_bronce", ["guantes"] = "guantes_media_bronce", ["pantalones"] = "pantalones_media_bronce", ["botas"] = "botas_media_bronce" };
                if (casco != null) piezas["casco"] = casco;
                Assert.IsTrue(VestidoDelMotor.Vestir(modelo.transform, new SelectorDePersonaje.HeroeParaVer
                {
                    Clase = new ClaseDeHeroe { Id = clase }, Armadura = "media", Piezas = piezas, Cabeza = "head_03", Pelo = "hair_02", Cejas = "eyebrow_01",
                }), clase);
                raiz.SetActive(true);
                yield return null;
                var visibles = modelo.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).Select(r => r.name).ToList();
                foreach (var malla in mallas) CollectionAssert.Contains(visibles, malla, $"{clase}: «{malla}» a la vista ({string.Join(", ", visibles)})");
                if (casco == null) Assert.IsFalse(visibles.Any(v => v.StartsWith("Chr_HeadCoverings")), clase + ": sin casco");
                if (!string.IsNullOrEmpty(carpeta)) Fotografiar(raiz.transform, Path.Combine(carpeta, $"58_vestido_{clase}.png"));
                Object.Destroy(raiz);
                yield return null;
            }
        }

        static void Fotografiar(Transform donde, string archivo)
        {
            var rt = new RenderTexture(480, 640, 24);
            var camara = new GameObject("Cámara").AddComponent<Camera>();
            camara.transform.SetParent(donde, false);
            camara.transform.localPosition = new Vector3(0.9f, 1.2f, 3.3f);
            camara.transform.LookAt(donde.position + new Vector3(0, 0.95f, 0));
            camara.fieldOfView = 32;
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = new Color32(0x19, 0x14, 0x10, 0xff);
            camara.targetTexture = rt;
            var luz = new GameObject("Luz").AddComponent<Light>();
            luz.transform.SetParent(donde, false);
            luz.transform.localPosition = new Vector3(1.2f, 2.4f, 2.6f);
            luz.type = LightType.Point;
            luz.range = 12;
            luz.intensity = 6;
            camara.Render();
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(archivo));
            File.WriteAllBytes(archivo, t.EncodeToPNG());
            Object.Destroy(t);
            rt.Release();
        }
    }
}
