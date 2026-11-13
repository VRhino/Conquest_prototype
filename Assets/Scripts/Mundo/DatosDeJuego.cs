// Las cifras de las reglas del motor (`GET /v1/datos/<versión>`, `Bienvenida.mundo.url_datos_de_juego`): `documentos` (cada bloque
// `<!-- parametros:NOMBRE -->` de `Docs/Mechanics` del motor, por documento y nombre, en unidades del juego), `reglas` y `derivados` (en
// milésimas) y `huella_reglas`. Son las mismas con las que juega el núcleo: aquí no se copia ninguna a mano ni hay valores de reserva. Si falta
// una, `FaltaDato` lo dice; eso es un cambio del motor que hay que mirar, no algo que tapar.
//
// Los lectores con nombre son los que ya usa Conquest; cada pantalla migrada añade los suyos (tabla «Dónde está cada cifra» de 08_Consumo).
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ConquestTactics.Mundo
{
    public sealed class FaltaDato : Exception
    {
        public FaltaDato(string ruta) : base($"/v1/datos no trae «{ruta}»") { }
    }

    public sealed class DatosDeJuego
    {
        public readonly JObject Raiz;
        public DatosDeJuego(JObject raiz) { Raiz = raiz; }

        public static DatosDeJuego Leer(byte[] json) => new DatosDeJuego(JObject.Parse(Encoding.UTF8.GetString(json)));

        /// <summary>Los datos de la `Bienvenida` (por versión, con caché en disco: cambian solo cuando cambian las reglas).</summary>
        public static async Task<DatosDeJuego> Cargar(Acceso acceso, BronzeAge.Contract.V1.InfoMundo mundo, CacheDeUno cache)
        {
            var version = mundo.VersionDatosDeJuego;
            var datos = string.IsNullOrEmpty(version) ? null : cache?.Leer(version);
            if (datos == null)
            {
                datos = await acceso.Bajar(string.IsNullOrEmpty(mundo.UrlDatosDeJuego) ? "/v1/datos" : mundo.UrlDatosDeJuego).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(version)) cache?.Guardar(version, datos);
            }
            return Leer(datos);
        }

        public string Huella => (string)Raiz["huella_reglas"];

        /// <summary>Lo que hay en una ruta con puntos (`documentos.Movimiento_Ejercitos.VISION.jugadorSolo`).</summary>
        public JToken En(string ruta)
        {
            JToken t = Raiz;
            foreach (var parte in ruta.Split('.'))
            {
                t = (t as JObject)?[parte];
                if (t == null) throw new FaltaDato(ruta);
            }
            return t;
        }

        public double Numero(string ruta) => En(ruta).Value<double>();

        /// <summary>Un bloque de parámetros de un documento (`Bloque("Heroe_Y_Tropa", "TROPAS_RECLUTABLES")`).</summary>
        public JToken Bloque(string documento, string nombre) => En($"documentos.{documento}.{nombre}");

        /// <summary>Unas cantidades por recurso, en milésimas (las de `derivados`).</summary>
        public Dictionary<string, long> Cantidades(string ruta) => En(ruta).ToObject<Dictionary<string, long>>();

        // ── Lo que ya se usa ──

        /// <summary>Visión de un héroe solo, en unidades.</summary>
        public double VisionJugadorSolo => Numero("documentos.Movimiento_Ejercitos.VISION.jugadorSolo");
        public double RadioEncuentro => Numero("documentos.Movimiento_Ejercitos.LOGISTICA.radioEncuentro");
        public double RadioPuerta => Numero("documentos.Movimiento_Ejercitos.MOVIMIENTO.radioPuerta");
        public double VelocidadJugador => Numero("documentos.Movimiento_Ejercitos.MOVIMIENTO.velocidadJugador");
        public bool BatallasEnUnity => En("reglas.batallas_en_unity").Value<bool>();
        public Dictionary<string, long> CosteCaravanaFundacion => Cantidades("derivados.costo_caravana_fundacion");
        public Dictionary<string, long> CosteRefundacion => Cantidades("derivados.costo_refundacion");
        /// <summary>El catálogo de tropas (poder, velocidad, unidades, coste, edificio...).</summary>
        public JToken TropasReclutables => Bloque("Heroe_Y_Tropa", "TROPAS_RECLUTABLES");
    }
}
