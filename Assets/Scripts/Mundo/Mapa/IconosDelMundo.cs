// El registro de los iconos provisionales de las pantallas del mundo (UI Toolkit): los mismos dibujos que BronzeAgeClient, sacados por script
// (`Tools/IconosDelPrototipo/generar.mjs`) a `Resources/MundoUI/Iconos` e importados como VectorImage. Cinco familias:
//   edificio  los edificios dibujados a mano (`ui/iconosEdificio.ts`)        tropa   las tropas (`ui/iconosTropa.ts`)
//   parte     las partes del interior que no son edificio (`ui/hudInterior.ts`)    mapa    los del mapa (game-icons.net, `mundo/iconosMapa.json`)
//   recurso   el emoji de cada recurso (`paletas.ts`, RECURSO_ICONO), dibujado por Twemoji
//   glifo     los glifos sueltos de las pantallas (⚔ ⛺ ☠ 👁 🚪 …), dibujados por Twemoji
//   simbolo   los símbolos que no son emoji (➜ ⧗ ⚑ ⛟ ⌂ ♜ ✦ ♛), dibujados por el script (teñibles)
// Los dibujos a mano y los del mapa van en blanco y se tiñen (como el `currentColor` del prototipo); los emoji llevan su color. Lo que no
// tiene dibujo propio lleva el genérico del prototipo (`_otro`). Va en el ensamblado del mapa porque lo usan el mapa y las pantallas.
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo
{
    public static class IconosDelMundo
    {
        /// <summary>El color de los iconos sobre las placas (el `--oro` de las pantallas).</summary>
        public static readonly Color Bronce = new Color32(0xd8, 0xb8, 0x78, 0xff);
        /// <summary>El `--oro-claro` (los iconos de tropa en su caja).</summary>
        public static readonly Color OroClaro = new Color32(0xf1, 0xdc, 0xae, 0xff);

        static readonly Dictionary<string, VectorImage> cargados = new Dictionary<string, VectorImage>();
        static JObject lista;

        /// <summary>La lista que escribe el generador: los ids de cada familia, los autores de los del mapa y el emoji y color de cada recurso.</summary>
        public static JObject Lista => lista ??= JObject.Parse(Resources.Load<TextAsset>("MundoUI/Iconos/iconos")?.text ?? "{}");

        /// <summary>El dibujo propio de un icono, o null si el prototipo no lo tiene.</summary>
        public static VectorImage Imagen(string familia, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var clave = familia + "/" + id;
            if (!cargados.TryGetValue(clave, out var v)) cargados[clave] = v = Resources.Load<VectorImage>("MundoUI/Iconos/" + clave);
            return v;
        }

        /// <summary>Un icono de `lado` px, teñido con `tinte` (los emoji de recurso no se tiñen). Sin dibujo propio, el genérico del prototipo
        /// (`_otro`: 📦, una casa, una lanza con escudo); si la familia no tiene genérico, null.</summary>
        public static VisualElement Icono(string familia, string id, float lado = 18, Color? tinte = null)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var imagen = Imagen(familia, id) ?? Imagen(familia, "_otro");
            if (imagen == null) return null;
            var v = new VisualElement { pickingMode = PickingMode.Ignore, name = $"icono-{familia}-{id}" };
            v.AddToClassList("icono");
            v.style.width = lado;
            v.style.height = lado;
            v.style.flexShrink = 0;
            v.style.backgroundImage = new StyleBackground(imagen);
            if (familia != "recurso" && familia != "glifo") v.style.unityBackgroundImageTintColor = tinte ?? Bronce;
            return v;
        }

        /// <summary>El color de un recurso (`RECURSO_COLOR` del prototipo), para sus barras y chips; bronce si no lo tiene.</summary>
        public static Color ColorDeRecurso(string id)
        {
            var c = (string)Lista["recurso"]?[id]?["color"];
            return c != null && ColorUtility.TryParseHtmlString(c, out var color) ? color : Bronce;
        }

        /// <summary>Un texto con su icono delante (en fila), como las filas del prototipo: «🪵 Madera».</summary>
        public static VisualElement ConIcono(string familia, string id, VisualElement texto, float lado = 18, Color? tinte = null)
        {
            var fila = new VisualElement { pickingMode = PickingMode.Ignore };
            fila.AddToClassList("con-icono");
            var icono = Icono(familia, id, lado, tinte);
            if (icono != null) fila.Add(icono);
            fila.Add(texto);
            return fila;
        }

        /// <summary>Una pieza de recurso (`.asent-recurso` del prototipo): su emoji y la cifra.</summary>
        public static VisualElement ChipDeRecurso(string recurso, string cifra, string titulo = null)
        {
            var chip = new VisualElement { name = "recurso-" + recurso, tooltip = titulo };
            chip.AddToClassList("chip-recurso");
            chip.Add(Icono("recurso", recurso, 16));
            var l = new Label(cifra);
            l.AddToClassList("chip-recurso-cifra");
            chip.Add(l);
            return chip;
        }

        /// <summary>El icono del mapa de una cosa y su color, como los pinta el mapa 2D del prototipo (`render.ts`, `ui/pueblos.ts`); null si allí no
        /// lleva icono (plazas: su sigilo; batallas, caravanas ajenas, aedas).</summary>
        public static (string id, Color color)? DeCosa(CosaDelMapa c)
        {
            Color Suyo() => new Color32((byte)(c.Color >> 16), (byte)(c.Color >> 8), (byte)c.Color, 255);
            switch (c.Clase)
            {
                case ClaseDeCosa.Ejercito: case ClaseDeCosa.EjercitoAjeno: case ClaseDeCosa.Formacion: return ("heroe", Suyo());
                case ClaseDeCosa.Caravana: return ("caravanaFundacion", new Color32(0xe8, 0xc2, 0x7a, 0xff));
                case ClaseDeCosa.Pueblo: return ("pueblo", Suyo());
                case ClaseDeCosa.Aldea: return ("aldea", Suyo());
                case ClaseDeCosa.Avanzada: return ("avanzada", Suyo());
                case ClaseDeCosa.Obra: return ("obra", Suyo());
                case ClaseDeCosa.Alijo: return ("alijo", new Color32(0xe0, 0xb4, 0x22, 0xff));
                case ClaseDeCosa.Mercenarios: return ("mercenarios", new Color32(0x6f, 0xa3, 0xd6, 0xff));
                case ClaseDeCosa.Bandidos: return ("bandidos", new Color32(0xd9, 0x53, 0x4f, 0xff));
                case ClaseDeCosa.Nodo: return DeNodo(c.Recurso) is string n ? (n, ColorDeNodo(c.Recurso)) : ((string, Color)?)null;
                default: return null;
            }
        }

        /// <summary>El símbolo de una cosa en la lista bajo el clic (`ui/eleccionClic.ts`): ⚔ batalla, ⧗ formación, ⚑ columnas y ejércitos,
        /// ⛟ caravana, ⌂ pueblo, ♜ avanzada, ⚒ obra, 📜 aeda, ✦ alijo, ⛺ mercenarios, ☠ bandidos, ♛ plaza. Los emoji, de Twemoji
        /// (`glifo`); los que no lo son, dibujados aquí (`simbolo`).</summary>
        public static (string familia, string id)? DeLista(ClaseDeCosa clase) => clase switch
        {
            ClaseDeCosa.Batalla => ("glifo", "atacar"),
            ClaseDeCosa.Formacion => ("simbolo", "reloj"),
            ClaseDeCosa.Ejercito or ClaseDeCosa.EjercitoAjeno => ("simbolo", "bandera"),
            ClaseDeCosa.Caravana or ClaseDeCosa.CaravanaAjena => ("simbolo", "carro"),
            ClaseDeCosa.Pueblo or ClaseDeCosa.Aldea => ("simbolo", "casa"),
            ClaseDeCosa.Avanzada => ("simbolo", "torre"),
            ClaseDeCosa.Obra => ("glifo", "obra"),
            ClaseDeCosa.Aeda => ("glifo", "aeda"),
            ClaseDeCosa.Alijo => ("simbolo", "estrella"),
            ClaseDeCosa.Mercenarios => ("glifo", "mercenarios"),
            ClaseDeCosa.Bandidos => ("glifo", "bandidos"),
            ClaseDeCosa.Plaza => ("simbolo", "corona"),
            _ => null,
        };

        /// <summary>El símbolo de una acción de la ficha (`AccionPuerta.icono` de main.ts): ➜ marchar, 🚪 entrar, ⚔ atacar, 👁 inspeccionar,
        /// 🎁 abrir, 🌾 víveres. Perseguir, interceptar y reclamar no llevan.</summary>
        public static (string familia, string id)? DeAccion(string texto) => texto switch
        {
            "Marchar aquí" => ("simbolo", "flecha"),
            "Entrar" or "Entrar con el ejército" => ("glifo", "entrar"),
            "Atacar" => ("glifo", "atacar"),
            "Inspeccionar" => ("glifo", "inspeccionar"),
            "Abrir" => ("glifo", "abrir"),
            "Llenar víveres" => ("glifo", "viveres"),
            _ => null,
        };

        /// <summary>Un botón con su glifo delante del texto, como `.puerta-boton` del prototipo.</summary>
        public static Button BotonConGlifo(string texto, (string familia, string id)? icono, string clases, System.Action alPulsar)
        {
            var b = new Button(alPulsar);
            foreach (var c in clases.Split(' ')) b.AddToClassList(c);
            b.AddToClassList("boton-con-glifo");
            var dibujo = icono == null ? null : Icono(icono.Value.familia, icono.Value.id, 16, OroClaro);
            if (dibujo != null) b.Add(dibujo);
            var l = new Label(texto);
            l.AddToClassList("boton-con-glifo-texto");
            b.Add(l);
            return b;
        }

        /// <summary>El icono de un yacimiento (`ICONO_DE_NODO`): mena para los metales, ganado, piedra; null para los demás.</summary>
        public static string DeNodo(string recurso) => recurso switch
        {
            "cobre" or "estano" or "hierro" or "oro" => "mena",
            "livestock" => "ganado",
            "piedra" => "piedra",
            _ => null,
        };

        /// <summary>El color de un yacimiento (`colorNodo`): la piedra, gris; los demás, el de su recurso.</summary>
        public static Color ColorDeNodo(string recurso) => recurso == "piedra" ? new Color32(0xb9, 0xb4, 0xaa, 0xff) : ColorDeRecurso(recurso);
    }
}
