using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConquestTactics.Visual
{
    /// <summary>
    /// Los caballos de guerra (tema 7): tres cuerpos (ibérico, tracio, niseo) sobre el esqueleto `HorseRig` de 34 huesos del `Horse_Mount`,
    /// cinco capas por paleta y las señas (lista y calzados) por héroe. El reparto por montura es uno solo, para el mapa (el horno de las
    /// figuras) y la batalla (`HeroHorseVisual`). Lo prepara `CaballosDeGuerraSetup` en Resources/Mounts/CaballosDeGuerra.asset.
    /// Solo cambia lo que se ve del caballo del héroe: malla, arreos y material.
    /// </summary>
    [CreateAssetMenu(menuName = "Conquest/Caballos de guerra")]
    public sealed class CaballosDeGuerra : ScriptableObject
    {
        public const string ResourcePath = "Mounts/CaballosDeGuerra";

        [Serializable]
        public sealed class Cuerpo
        {
            public string Nombre;
            public Mesh Malla, Arreos;
            [Tooltip("Los huesos de la malla y de los arreos, por nombre, en el orden de sus pesos.")]
            public string[] Huesos, HuesosArreos;
        }

        [Serializable]
        public sealed class Capa
        {
            public string Nombre;
            [Tooltip("La paleta de 256² (filtro Point, legible: las señas se pintan encima).")]
            public Texture2D Paleta;
        }

        public Cuerpo[] Cuerpos;
        public Capa[] Capas;
        [Tooltip("El material de base (URP Lit); cada caballo lleva una copia con su paleta.")]
        public Material Material;

        /// <summary>El reparto por montura del motor: qué cuerpo y qué capa lleva cada una. Null: la montura no tiene caballo nuevo.</summary>
        public static (string cuerpo, string capa)? Reparto(string montura) => montura switch
        {
            "montura_tiro" => ("tracio", "castana"),
            "montura_estepa" => ("tracio", "alazana"),
            "montura_escita" => ("iberico", "baya"),
            "montura_tesalia" => ("iberico", "negra"),
            "montura_nisea" => ("niseo", "torda"),
            _ => null,
        };

        // las celdas de las señas en la paleta (columna, fila de 32 px desde arriba), como en el modelo
        static readonly (int c, int f) Lista = (3, 2), Cabos = (4, 0), Capa_ = (0, 0), Blanco = (1, 1);
        static readonly (int c, int f)[] Calzados = { (4, 2), (5, 2), (6, 2), (7, 2) };

        static CaballosDeGuerra cargado;
        public static CaballosDeGuerra Cargar() => cargado != null ? cargado : cargado = Resources.Load<CaballosDeGuerra>(ResourcePath);

        readonly Dictionary<(string, int), Material> materiales = new Dictionary<(string, int), Material>();

        /// <summary>Viste el caballo `raiz` (una copia del `Horse_Mount`, con su `HorseRig`) con el cuerpo y la capa de `montura` y las señas de
        /// `semilla` (el id del héroe; null, sin señas). Quita las riendas viejas: las de los arreos nuevos van de la boca a la cruz. False si la
        /// montura no tiene caballo nuevo o falta algo (y el caballo se queda como estaba).</summary>
        public bool Vestir(GameObject raiz, string montura, string semilla)
        {
            if (raiz == null || !(Reparto(montura) is var (nombreCuerpo, nombreCapa))) return false;
            var cuerpo = Array.Find(Cuerpos ?? Array.Empty<Cuerpo>(), x => x.Nombre == nombreCuerpo);
            var capa = Array.Find(Capas ?? Array.Empty<Capa>(), x => x.Nombre == nombreCapa);
            if (cuerpo?.Malla == null || capa?.Paleta == null || Material == null) return false;
            SkinnedMeshRenderer caballo = null, arreos = null;
            foreach (var r in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name == "Horse") caballo = r;
                else if (r.name == "Horse_Tack") arreos = r;
                else if (r.name.StartsWith("Horse_Reins", StringComparison.Ordinal)) r.gameObject.SetActive(false);
            }
            if (caballo == null) return false;
            // los huesos, por nombre, los del caballo primero: en batalla cuelga del héroe, y el jinete tiene huesos que se llaman igual (Head,
            // Neck...); si ganaran los suyos, el cuello y la cabeza del caballo seguirían al jinete
            var huesos = new Dictionary<string, Transform>();
            foreach (var t in caballo.bones) if (t != null) huesos.TryAdd(t.name, t);
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) huesos.TryAdd(t.name, t);
            Transform[] Huesos(string[] nombres)
            {
                var b = new Transform[nombres.Length];
                for (int i = 0; i < nombres.Length; i++) if (!huesos.TryGetValue(nombres[i], out b[i])) return null;
                return b;
            }
            var bc = Huesos(cuerpo.Huesos);
            if (bc == null) return false;
            var mat = MaterialDe(capa, semilla);
            caballo.sharedMesh = cuerpo.Malla;
            caballo.bones = bc;
            caballo.sharedMaterials = new[] { mat };
            if (arreos != null)
            {
                var ba = cuerpo.Arreos != null ? Huesos(cuerpo.HuesosArreos) : null;
                if (ba != null)
                {
                    arreos.sharedMesh = cuerpo.Arreos;
                    arreos.bones = ba;
                    arreos.sharedMaterials = new[] { mat };
                    arreos.gameObject.SetActive(true);
                }
                else arreos.gameObject.SetActive(false);
            }
            return true;
        }

        /// <summary>El material de una capa con las señas de una semilla: lista (en dos de cada tres) y de cero a dos calzados. Uno por combinación.</summary>
        Material MaterialDe(Capa capa, string semilla)
        {
            int senas = Senas(semilla);
            if (materiales.TryGetValue((capa.Nombre, senas), out var m) && m != null) return m;
            var p = capa.Paleta;
            var t = new Texture2D(p.width, p.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                                                                                   name = $"{p.name} · señas {senas}" };
            t.SetPixels32(p.GetPixels32());
            int celda = p.width / 8;
            Color32 De((int c, int f) x) => p.GetPixel(x.c * celda + celda / 2, p.height - 1 - (x.f * celda + celda / 2));
            void Pintar((int c, int f) x, Color32 col)
            {
                var pix = new Color32[celda * celda];
                for (int i = 0; i < pix.Length; i++) pix[i] = col;
                t.SetPixels32(x.c * celda, p.height - (x.f + 1) * celda, celda, celda, pix);
            }
            var blanco = De(Blanco);
            Pintar(Lista, (senas & 1) != 0 ? blanco : De(Capa_));
            for (int i = 0; i < 4; i++) Pintar(Calzados[i], (senas & (2 << i)) != 0 ? blanco : De(Cabos));
            t.Apply(false, true);
            m = new Material(Material) { name = $"{Material.name} · {capa.Nombre} · señas {senas}" };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
            materiales[(capa.Nombre, senas)] = m;
            return m;
        }

        /// <summary>Las señas de una semilla: bit 0, la lista; bits 1 a 4, los calzados (mano izquierda, mano derecha, pie izquierdo, pie
        /// derecho). Estables por semilla; sin semilla, ninguna.</summary>
        public static int Senas(string semilla)
        {
            if (string.IsNullOrEmpty(semilla)) return 0;
            uint h = 2166136261;
            foreach (char ch in semilla) h = (h ^ ch) * 16777619;
            int s = (h % 3) != 0 ? 1 : 0;
            int calzados = (int)((h >> 4) % 4);   // 0, 1 o 2 calzados (el 3 cuenta como ninguno)
            int primero = (int)((h >> 8) % 4);
            if (calzados == 1 || calzados == 2) s |= 2 << primero;
            if (calzados == 2) s |= 2 << ((primero + 1 + (int)((h >> 12) % 3)) % 4);
            return s;
        }
    }
}
