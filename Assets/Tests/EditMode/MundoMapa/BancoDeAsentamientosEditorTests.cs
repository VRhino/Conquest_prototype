// El banco de asentamientos del editor (`MenuDeAsentamientos`), sin ventana: con el mismo código del menú (el aviso de la sesión que lee al
// entrar en Play), entra en Play, espera al banco y comprueba que hay una miniatura de cada nivel del 1 al 5 y que no sale ningún error en el
// registro (el marco de pruebas falla con cualquier error); y que hay una muralla de cada nivel del 2 al 5. Necesita el mundo en disco de una partida (`persistentDataPath/mundo`).
using System.Collections;
using System.IO;
using System.Linq;
using ConquestTactics.Mundo;
using ConquestTactics.Mundo.Mapa;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConquestTactics.Mundo.Mapa.EditorTests
{
    public sealed class BancoDeAsentamientosEditorTests
    {
        const string ClaveMejorado = "Conquest.Mundo.AsentamientosMejorado", ClaveBanco = "Conquest.Mundo.AbrirBancoDeAsentamientos";

        [UnityTest, Category("Mapa")]
        public IEnumerator ElBancoDeAsentamientosSeMonta()
        {
            if (!Directory.Exists(Path.Combine(Application.persistentDataPath, "mundo", "artefacto")))
                Assert.Ignore("no hay mundo en disco: entra antes una vez al mundo con el servidor");
            bool antes = EditorPrefs.GetBool(ClaveMejorado, false);
            EditorPrefs.SetBool(ClaveMejorado, true);
            SessionState.SetBool(ClaveBanco, true);
            yield return new EnterPlayMode();
            try
            {
                GameObject go = null;
                MapaDeMundo mapa = null;
                int[] niveles = new int[0], conMuralla = new int[0];
                var hasta = Time.realtimeSinceStartup + 120;
                while (Time.realtimeSinceStartup < hasta)
                {
                    go = go != null ? go : GameObject.Find("Banco de asentamientos");
                    mapa = go != null ? go.GetComponent<MapaDeMundo>() : null;
                    if (mapa != null && mapa.Maquetas != null)
                    {
                        var en = mapa.Maquetas.Encargos(mapa.Vista);
                        niveles = en.Where(e => !e.EsSuelo && !e.EsMuralla)
                            .Select(e => e.Material.name).Where(n => n.StartsWith("plaza")).Select(n => n[5] - '0').Distinct().OrderBy(n => n).ToArray();
                        // (la muralla del 3 va con el material de su plaza; las demás, con el suyo: muralla<nivel>_<v>)
                        conMuralla = en.Where(e => e.EsMuralla).Select(e => e.Material.name)
                            .Select(n => n.StartsWith("muralla") ? n[7] - '0' : n[5] - '0').Distinct().OrderBy(n => n).ToArray();
                        if (niveles.Length == 5 && conMuralla.Length == 4) break;
                    }
                    yield return null;
                }
                Assert.That(go, Is.Not.Null, "se montó el banco");
                Assert.That(MaquetasDelMapa.Estilo, Is.EqualTo(EstiloDeAsentamientos.Mejorado), "la casilla, aplicada al entrar en Play");
                Assert.That(niveles, Is.EqualTo(new[] { 1, 2, 3, 4, 5 }), "una miniatura de cada nivel");
                Assert.That(conMuralla, Is.EqualTo(new[] { 2, 3, 4, 5 }), "una muralla de cada nivel del 2 al 5");
            }
            finally { EditorPrefs.SetBool(ClaveMejorado, antes); }
            yield return new ExitPlayMode();
            Assert.That(MaquetasDelMapa.Estilo, Is.EqualTo(EstiloDeAsentamientos.Prototipo), "fuera de Play, el estilo vuelve al del código");
            Assert.That(CosasDelMapa.HuellaMejorada, Is.False, "y la huella, la del prototipo");
        }
    }
}
