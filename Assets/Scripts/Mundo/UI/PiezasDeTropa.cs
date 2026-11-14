// Las piezas de tropa de Conquest en las pantallas del mundo (UI Toolkit). Son gemelas de las UGUI del juego, sin tocarlas:
//   - la miniatura de `Squad_Icon` (SquadIconController): fondo, icono y una línea del color de la rareza;
//   - la tarjeta de `Squad_option` (SquadOptionUI): fondo, imagen de la tropa, franja inferior con la línea de la rareza, nivel, coste de
//     Liderazgo, hombres y la marca de elegida;
//   - las estrellas de `SquadDetailPanel`.
// Las imágenes salen del SquadData de cada tropa del motor (Resources/Squads/BronzeAge/<tropaId>, lo crea `TropasDelMotor`). El color y
// las estrellas de la rareza salen de `SquadUtils.RarityInfo`: una sola tabla. Decisión de Víctor (2026-11-13): en las tropas, estas
// piezas sustituyen al icono dibujado del prototipo.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public static class PiezasDeTropa
    {
        static readonly Dictionary<string, SquadData> datos = new Dictionary<string, SquadData>();

        /// <summary>El SquadData de una tropa del motor, o null si no lo hay (lo dice la prueba de cobertura; aquí no se rellena con nada).</summary>
        public static SquadData Datos(string tropaId)
        {
            if (string.IsNullOrEmpty(tropaId)) return null;
            if (!datos.TryGetValue(tropaId, out var d)) datos[tropaId] = d = Resources.Load<SquadData>($"Squads/BronzeAge/{tropaId}/{tropaId}");
            return d;
        }

        /// <summary>El color de la rareza (`SquadUtils.RarityInfo`).</summary>
        public static Color ColorDeRareza(SquadData d) => SquadUtils.GetRarityInfo(d.rarity).color;

        static void Imagen(VisualElement v, Sprite s)
        {
            if (s == null) return;
            v.style.backgroundImage = new StyleBackground(s);
        }

        static VisualElement Capa(string clase)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.AddToClassList(clase);
            return v;
        }

        /// <summary>La miniatura (`Squad_Icon`): 40 px en Conquest, con la línea de la rareza de 1 px al pie. Sin SquadData, null.</summary>
        public static VisualElement Miniatura(string tropaId, float lado = 40, string titulo = null)
        {
            var d = Datos(tropaId);
            if (d == null) return null;
            var v = new VisualElement { name = "mini-" + tropaId, tooltip = titulo ?? d.squadName };
            v.AddToClassList("tropa-mini");
            v.style.width = v.style.height = lado;
            var fondo = Capa("tropa-mini-fondo");
            Imagen(fondo, d.background);
            v.Add(fondo);
            var icono = Capa("tropa-mini-icono");
            Imagen(icono, d.icon);
            v.Add(icono);
            var linea = Capa("tropa-mini-linea");
            linea.style.height = Mathf.Max(1, lado / 40f);
            linea.style.backgroundColor = ColorDeRareza(d);
            v.Add(linea);
            return v;
        }

        /// <summary>La tarjeta (`Squad_option`, 150×236,75 en Conquest): la imagen de la tropa a todo el alto y, abajo, la franja oscura con la
        /// línea de la rareza, el coste de Liderazgo («14» y su icono) a la izquierda y los hombres («25/30») a la derecha, como la original: sin el
        /// nivel (con los tres datos juntos no se sabía cuál era cuál; Víctor, 2026-11-13). Lo que no se sabe no sale. `elegida`: con la marca de elegida; null, sin marca. Sin SquadData, null.</summary>
        public static VisualElement Tarjeta(string tropaId, int? liderazgo, int? vivos, int? total, bool? elegida = null, float ancho = 150)
        {
            var d = Datos(tropaId);
            if (d == null) return null;
            var escala = ancho / 150f;
            var v = new VisualElement { name = "tarjeta-" + tropaId, tooltip = d.squadName };
            v.AddToClassList("tropa-tarjeta");
            v.style.width = ancho;
            v.style.height = 236.75f * escala;
            // La marca de elegida (`outline` de Squad_option: blanco al 11 %, 1 px más grande, detrás), solo si está elegida.
            v.Add(Capa("tropa-tarjeta-marca"));
            var fondo = Capa("tropa-tarjeta-capa");
            Imagen(fondo, d.background);
            v.Add(fondo);
            var imagen = Capa("tropa-tarjeta-capa");
            imagen.AddToClassList("tropa-tarjeta-imagen");
            Imagen(imagen, d.unitImage);
            v.Add(imagen);

            var franja = Capa("tropa-tarjeta-franja");
            franja.style.height = 28.566f * escala;
            var izquierda = Capa("tropa-tarjeta-datos");
            if (liderazgo != null)
            {
                // Como en Squad_option: la cifra y, detrás, el icono de Liderazgo de Conquest (Art/UI/units/leadership, por la hoja de estilo).
                var lid = Capa("tropa-tarjeta-liderazgo");
                lid.tooltip = "Liderazgo";
                lid.Add(Texto(liderazgo.ToString(), "tropa-tarjeta-cifra"));
                var icono = Capa("tropa-tarjeta-liderazgo-icono");
                icono.style.width = icono.style.height = 13 * escala;
                lid.Add(icono);
                izquierda.Add(lid);
            }
            franja.Add(izquierda);
            if (vivos != null) franja.Add(Texto(total != null ? $"{vivos}/{total}" : vivos.ToString(), "tropa-tarjeta-hombres"));
            v.Add(franja);
            var linea = Capa("tropa-tarjeta-linea");
            linea.style.height = 4 * escala;
            linea.style.backgroundColor = ColorDeRareza(d);
            v.Add(linea);
            if (elegida != null) Elegir(v, elegida.Value);
            return v;
        }

        /// <summary>La marca de elegida de una tarjeta (`selectedImage` de Squad_option, que es su `outline`).</summary>
        public static void Elegir(VisualElement tarjeta, bool si) => tarjeta.EnableInClassList("tropa-tarjeta--elegida", si);

        /// <summary>Las estrellas de la rareza (`SquadDetailPanel.buildRarityUI`): enteras y, si toca, una media; del color de la rareza.</summary>
        public static VisualElement Estrellas(string tropaId, float lado = 16)
        {
            var d = Datos(tropaId);
            if (d == null) return null;
            var (color, estrellas) = SquadUtils.GetRarityInfo(d.rarity);
            var fila = new VisualElement { name = "estrellas-" + tropaId, pickingMode = PickingMode.Ignore };
            fila.AddToClassList("tropa-estrellas");
            int enteras = Mathf.FloorToInt(estrellas);
            float parte = estrellas - enteras;
            for (int i = 0; i < enteras + (parte > 0 ? 1 : 0); i++)
            {
                // La parcial, recortada como el `fillAmount` de Conquest.
                var caja = new VisualElement { pickingMode = PickingMode.Ignore };
                caja.style.width = i < enteras ? lado : lado * parte;
                caja.style.height = lado;
                caja.style.overflow = Overflow.Hidden;
                var estrella = Texto("★", "tropa-estrella");
                estrella.style.fontSize = lado;
                estrella.style.width = lado;
                estrella.style.color = color;
                caja.Add(estrella);
                fila.Add(caja);
            }
            return fila;
        }

        static Label Texto(string t, string clase)
        {
            var l = new Label(t) { pickingMode = PickingMode.Ignore };
            l.AddToClassList(clase);
            return l;
        }
    }
}
