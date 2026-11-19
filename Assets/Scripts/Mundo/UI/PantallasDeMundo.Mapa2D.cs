// El mapa estratégico 2D (paso 9 del bloque 4): el botón «Mapa» de la barra del jugador en cualquier pantalla (`ui/barraJugador.ts`), «Ver en el
// mapa» centrado en un punto, Esc o ✕ para cerrarlo y, en el mundo, el clic: en el suelo la columna marcha allí y la ruta se ve en el mapa
// (Víctor, 2026-11-15: como el clic en el mundo 3D); sobre algo, se cierra el mapa, se elige y se marcha (`resolverClic`); con varias cosas,
// la lista. Desde dentro de una plaza o un pueblo, solo para mirar. El modo de elegir un punto (aldea, atalaya) lo abre Territorio.
using System;
using System.Collections.Generic;
using BronzeAge.Contract.V1;
using System.Linq;
using System.Threading.Tasks;
using ConquestTactics.Mundo.Mapa;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public partial class PantallasDeMundo
    {
        MapaEstrategico mapa2D;
        SueloDelMapa2D suelo2D;

        /// <summary>¿Está abierto el mapa 2D?</summary>
        public bool Mapa2DAbierto => mapa2D != null && mapa2D.panel != null;
        public MapaEstrategico Mapa2D => Mapa2DAbierto ? mapa2D : null;

        /// <summary>El botón «Mapa» de la barra, el último del prototipo (detrás de los avisos).</summary>
        void BotonDeMapa(VisualElement barra, VisualElement despuesDe)
        {
            var b = new Button(() => { if (Mapa2DAbierto) CerrarMapa2D(); else AbrirMapa2D(); }) { name = "mapa-2d", tooltip = "Mapa" };
            IconoDeBarra(b, "mapa");
            barra.Insert(barra.IndexOf(despuesDe) + 1, b);
        }

        /// <summary>Abre el mapa 2D encima de la pantalla de ahora: centrado en `centro` (o en el héroe) y, con `eleccion`, eligiendo un punto.</summary>
        public void AbrirMapa2D((double x, double y)? centro = null, EleccionEnMapa eleccion = null)
        {
            if (c?.Sesion == null || c.Artefacto == null) return;
            CerrarMapa2D();
            suelo2D ??= new SueloDelMapa2D(c.Artefacto);
            mapa2D = new MapaEstrategico(c.Sesion, c.Artefacto, c.Datos, () => c.HeroeId, suelo2D)
            {
                NombresDeNodos = PlayerPrefs.GetInt("mundo.capa.nodos", 1) == 1,
                Cultivos = CultivosDesbloqueados,
                FertilidadMinima = DatoDeAldea("fertilidadMinima", 0.3),
                Parcelas = PlayerPrefs.GetInt("mundo.mapa2d.parcelas", 0) == 1,
                EstadoDeParcela = ClasificadorDeParcelas(),
                Fundacion = mapa?.Fundacion,
            };
            var cultivo = PlayerPrefs.GetString("mundo.mapa2d.fertilidad", "");
            mapa2D.CultivoDeFertilidad = string.IsNullOrEmpty(cultivo) || !CultivosDesbloqueados().Contains(cultivo) ? null : cultivo;
            // Eligiendo la aldea, la capa de su cultivo (como el prototipo: `fijarCultivoDeLaCapa` al abrir el selector).
            mapa2D.AlCambiarCapas += () =>
            {
                PlayerPrefs.SetInt("mundo.capa.nodos", mapa2D.NombresDeNodos ? 1 : 0);
                PlayerPrefs.SetString("mundo.mapa2d.fertilidad", mapa2D.CultivoDeFertilidad ?? "");
                PlayerPrefs.SetInt("mundo.mapa2d.parcelas", mapa2D.Parcelas ? 1 : 0);
                mapa2D.EstadoDeParcela = ClasificadorDeParcelas();
                if (mapa != null) mapa.NombresDeNodos = mapa2D.NombresDeNodos;
            };
            mapa2D.AlCerrar += CerrarMapa2D;
            mapa2D.AlClicar = ClicEnMapa2D;
            pantalla.Add(mapa2D);
            if (mapa != null) mapa.EntradaApagada = true;
            mapa2D.Abrir(centro, eleccion);
            mapa2D.Focus();
        }

        public void CerrarMapa2D()
        {
            if (mapa2D == null) return;
            mapa2D.Destruir();
            mapa2D.RemoveFromHierarchy();
            mapa2D = null;
            if (mapa != null) mapa.EntradaApagada = BaseAbierta;
        }

        /// <summary>El clic en el mapa 2D sin elegir sitio. Solo en el mundo (con el mapa 3D debajo); desde dentro de un sitio, solo se mira.</summary>
        async Task ClicEnMapa2D(double x, double y, double pxPorUnidad)
        {
            if (mapa == null || mapa2D == null) return;
            if (mapa.ListaAbierta) { mapa.CerrarLista(); return; }
            var bajo = CosasDelMapa.BajoElClic(mapa2D.Cosas, x, y, pxPorUnidad);
            if (bajo.Count == 0)
            {
                // En el suelo: la columna marcha allí y el mapa sigue abierto con la ruta trazada.
                mapa.Seleccionar(null);
                await mapa.MarcharA(x, y);
                mapa2D?.Repintar();
                return;
            }
            CerrarMapa2D();
            if (bajo.Count == 1) await mapa.Elegir(bajo[0]);
            else mapa.AbrirLista(bajo);
        }

        /// <summary>El estado de cada parcela de bosque (`clasificadorDeParcelas`): «ocupada» si la explota una Leñera, «ajena» si cae en la zona de
        /// una plaza de otra Facción, «libre» si no.</summary>
        Func<ParcelaBosque, string> ClasificadorDeParcelas()
        {
            var ocupadas = FuentesOcupadas();
            var mia = HeroeAhora()?.FaccionId;
            var zonas = c.Sesion.Almacen.Todas(BronzeAge.Contract.V1.Entidad.CuerpoOneofCase.Asentamiento).Select(e => e.Asentamiento)
                .Where(a => a.Zona.Count > 2 && a.FaccionId != mia).Select(ZonaDe).ToList();
            var memo = new Dictionary<string, string>();
            return q =>
            {
                if (memo.TryGetValue(q.Id, out var e)) return e;
                double x = q.Centro.X / 1000.0, y = q.Centro.Y / 1000.0;
                return memo[q.Id] = ocupadas.Contains(q.Id) ? "ocupada" : zonas.Any(z => SitiosDeAldea.Dentro(x, y, z)) ? "ajena" : "libre";
            };
        }

        /// <summary>Un dato de la aldea (`PUEBLOS_EXTRACTORES.aldea`), con su valor de serie si no llega.</summary>
        double DatoDeAldea(string nombre, double deSerie)
        {
            var v = c?.Datos?.Raiz.SelectToken($"documentos.Economia_Construccion.PUEBLOS_EXTRACTORES.aldea.{nombre}");
            return v == null ? deSerie : (double)v;
        }

        static readonly string[] OrdenDeCultivos = { "trigo", "vid", "oliva", "cebada", "sesamo" };

        /// <summary>Los cultivos con capa de fertilidad (`cultivosDesbloqueados`): los que tu Facción ya desbloqueó por el nivel de sus plazas
        /// (`PUEBLOS_EXTRACTORES.aldea.cultivos[c].nivelMadre`); la ganadera no tiene capa propia.</summary>
        IReadOnlyList<string> CultivosDesbloqueados()
        {
            var a = c?.Sesion?.Almacen;
            var h = c?.HeroeId == null ? null : a?.Buscar(BronzeAge.Contract.V1.TipoEntidad.Heroe, c.HeroeId)?.Heroe;
            var nivel = 1u;
            if (h != null && h.HasFaccionId)
                foreach (var e in a.Todas(BronzeAge.Contract.V1.Entidad.CuerpoOneofCase.Asentamiento))
                    if (e.Asentamiento.FaccionId == h.FaccionId) nivel = System.Math.Max(nivel, e.Asentamiento.Nivel);
            var cultivos = c?.Datos?.Raiz.SelectToken("documentos.Economia_Construccion.PUEBLOS_EXTRACTORES.aldea.cultivos");
            return OrdenDeCultivos.Where(x => nivel >= ((uint?)cultivos?[x]?["nivelMadre"] ?? 1)).ToList();
        }
    }
}
