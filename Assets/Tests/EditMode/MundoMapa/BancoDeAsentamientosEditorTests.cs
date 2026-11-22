// El banco de asentamientos del editor (`MenuDeAsentamientos`), sin ventana: con el mismo código del menú (el aviso de la sesión que lee al
// entrar en Play), entra en Play, espera al banco y comprueba que hay una miniatura de cada nivel del 1 al 5 y que no sale ningún error en el
// registro (el marco de pruebas falla con cualquier error); que hay una muralla de cada nivel del 2 al 5; y un pueblo de cantera y otro de mina
// de cada nivel del 1 al 3; y las ocho aldeas con sus núcleos y sus campos. Necesita el mundo en disco de una partida (`persistentDataPath/mundo`).
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
                string[] pueblos = new string[0], nucleos = new string[0];
                int campos = 0;
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
                        pueblos = en.Where(e => !e.EsSuelo && e.Material.name.StartsWith("pueblo_")).Select(e => e.Material.name.Split(' ')[0]).Distinct().OrderBy(n => n).ToArray();
                        nucleos = en.Where(e => !e.EsSuelo && e.Material.name.StartsWith("nucleo_")).Select(e => e.Material.name.Split(' ')[0]).Distinct().OrderBy(n => n).ToArray();
                        campos = en.Count(e => e.EsSuelo && e.Material.name.StartsWith("campos"));
                        if (niveles.Length == 5 && conMuralla.Length == 4 && pueblos.Length == 6 && nucleos.Length == 4 && campos == 8) break;
                    }
                    yield return null;
                }
                Assert.That(go, Is.Not.Null, "se montó el banco");
                Assert.That(MaquetasDelMapa.Estilo, Is.EqualTo(EstiloDeAsentamientos.Mejorado), "la casilla, aplicada al entrar en Play");
                Assert.That(niveles, Is.EqualTo(new[] { 1, 2, 3, 4, 5 }), "una miniatura de cada nivel");
                Assert.That(conMuralla, Is.EqualTo(new[] { 2, 3, 4, 5 }), "una muralla de cada nivel del 2 al 5");
                Assert.That(pueblos, Is.EqualTo(new[] { "pueblo_cantera_1", "pueblo_cantera_2", "pueblo_cantera_3", "pueblo_mina_1", "pueblo_mina_2", "pueblo_mina_3" }),
                    "un pueblo de cantera y otro de mina de cada nivel");
                Assert.That(nucleos, Is.EqualTo(new[] { "nucleo_ganado_2", "nucleo_labor_1", "nucleo_labor_2", "nucleo_labor_3" }), "los núcleos de las aldeas");
                Assert.That(campos, Is.EqualTo(8), "los campos de las ocho aldeas");
            }
            finally { EditorPrefs.SetBool(ClaveMejorado, antes); }
            yield return new ExitPlayMode();
            Assert.That(MaquetasDelMapa.Estilo, Is.EqualTo(EstiloDeAsentamientos.Prototipo), "fuera de Play, el estilo vuelve al del código");
            Assert.That(CosasDelMapa.HuellaMejorada, Is.False, "y la huella, la del prototipo");
        }
    }
}
