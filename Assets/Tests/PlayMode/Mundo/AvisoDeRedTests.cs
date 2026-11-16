// La banda del aviso de conexión de la batalla (interfaz de Conquest, UGUI; Víctor, 2026-11-16): sale con el aviso, lleva la cuenta atrás mientras
// se reconecta y se va sin aviso. El corte de red no se puede forzar en la prueba de punta a punta: aquí se pone el aviso a mano.
using System.Collections;
using System.Reflection;
using ConquestTactics.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConquestTactics.Mundo.UI.Tests
{
    public class AvisoDeRedTests
    {
        static void Poner(string propiedad, object valor) =>
            typeof(ReconexionClienteSystem).GetProperty(propiedad, BindingFlags.Public | BindingFlags.Static).SetValue(null, valor);

        [UnityTest]
        public IEnumerator LaBandaSaleConElAvisoConSuCuentaAtrasYSeVaSinEl()
        {
            ReconexionClienteSystem.Reiniciar();
            var go = new GameObject("AvisoDeRed");
            var banda = go.AddComponent<AvisoDeRed>();
            try
            {
                yield return null;
                Assert.AreEqual("", banda.Mostrado, "sin aviso, no se ve");
                Poner("Aviso", "Conexión perdida. Reconectando…");
                Poner("QuedaParaPerderla", 42.3d);
                yield return null;
                Assert.AreEqual("Conexión perdida. Reconectando… 43 s", banda.Mostrado, "el aviso con su cuenta atrás");
                Assert.IsNotNull(go.GetComponent<Canvas>(), "en un lienzo UGUI, no en OnGUI");
                Poner("QuedaParaPerderla", -1d);
                Poner("Aviso", "Has abandonado la batalla.");
                yield return null;
                Assert.AreEqual("Has abandonado la batalla.", banda.Mostrado, "sin reconectar, sin cuenta atrás");
                Poner("Aviso", "");
                yield return null;
                Assert.AreEqual("", banda.Mostrado, "todo bien: se va");
            }
            finally
            {
                Object.Destroy(go);
                ReconexionClienteSystem.Reiniciar();
            }
        }
    }
}
