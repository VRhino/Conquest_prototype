// La pantalla de carga (BronzeAgeClient: ui/pantallaCarga.ts): una imagen a pantalla completa, un consejo del juego en el cuarto inferior y
// la barra abajo. Los consejos y sus imágenes son del cliente (`Resources/MundoUI/Carga/consejos.json`, el mismo archivo que BronzeAgeClient
// y que edita su página «Consejos de carga» del admin). Dura como poco lo bastante para leer el consejo y como mucho lo que diga quien la abre.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed class PantallaCarga
    {
        const float MinimoSegundos = 2.2f;
        const int SalidaMs = 450;
        static string ultimoConsejo;

        readonly VisualElement capa;
        readonly VisualElement raiz, relleno;
        readonly Label titulo;
        readonly float desde;

        /// <summary>Abre la pantalla de carga hacia una vista (`mundo`, `asentamiento`, `campamento`, `pueblo`; null: entrar en la partida).</summary>
        public PantallaCarga(VisualElement capa, string vista, string titulo)
        {
            this.capa = capa;
            raiz = Resources.Load<VisualTreeAsset>("MundoUI/Carga").Instantiate();
            raiz.style.position = Position.Absolute;
            raiz.style.left = raiz.style.top = raiz.style.right = raiz.style.bottom = 0;
            capa.Add(raiz);
            relleno = raiz.Q("relleno");
            this.titulo = raiz.Q<Label>("titulo");
            this.titulo.text = titulo + "…";
            desde = Time.realtimeSinceStartup;
            PintarConsejo(vista);
        }

        public void Progreso(float fraccion, string texto)
        {
            relleno.style.width = Length.Percent(Mathf.Clamp01(fraccion) * 100);
            if (!string.IsNullOrEmpty(texto)) titulo.text = texto + "…";
        }

        /// <summary>La vista nueva ya está: la barra se llena y la pantalla se va (no antes del mínimo).</summary>
        public async Task Cerrar()
        {
            Progreso(1, null);
            var falta = MinimoSegundos - (Time.realtimeSinceStartup - desde);
            if (falta > 0) await Task.Delay(TimeSpan.FromSeconds(falta));
            raiz.Q("carga").AddToClassList("carga--saliendo");
            // Mientras se desvanece ya no tapa: el ratón es de lo que hay debajo (si no, un clic en ese medio segundo se perdía).
            foreach (var e in raiz.Query<VisualElement>().ToList()) e.pickingMode = PickingMode.Ignore;
            await Task.Delay(SalidaMs);
            raiz.RemoveFromHierarchy();
        }

        void PintarConsejo(string vista)
        {
            var json = Resources.Load<TextAsset>("MundoUI/Carga/consejos");
            if (json == null) return; // sin catálogo, la pantalla sale igual, con la barra y sin consejo
            var catalogo = JObject.Parse(json.text);
            var validos = ((JArray)catalogo["consejos"] ?? new JArray()).OfType<JObject>()
                .Where(c => (bool?)c["activo"] != false && !string.IsNullOrWhiteSpace((string)c["texto"]))
                .Where(c => vista == null || !(c["vistas"] is JArray v) || v.Count == 0 || v.Any(x => (string)x == vista))
                .ToList();
            var sinRepetir = validos.Count > 1 ? validos.Where(c => (string)c["id"] != ultimoConsejo).ToList() : validos;
            if (sinRepetir.Count == 0) return;
            var consejo = sinRepetir[UnityEngine.Random.Range(0, sinRepetir.Count)];
            ultimoConsejo = (string)consejo["id"];
            raiz.Q<Label>("texto").text = (string)consejo["texto"];
            raiz.Q("consejo").RemoveFromClassList("oculto");
            var archivo = (string)catalogo["imagenes"]?[(string)consejo["imagen"] ?? ""]?["archivo"];
            var imagen = archivo == null ? null : Resources.Load<Texture2D>("MundoUI/Carga/" + Path.GetFileNameWithoutExtension(archivo));
            if (imagen == null) return;
            var fondo = raiz.Q("imagen");
            fondo.style.backgroundImage = imagen;
            // Al siguiente fotograma, para que se vea la transición (aparece y se acerca despacio).
            fondo.schedule.Execute(() => fondo.AddToClassList("carga-imagen--cargada"));
        }
    }
}
