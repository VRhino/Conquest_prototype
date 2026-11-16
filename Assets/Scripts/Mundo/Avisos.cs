// Los avisos: los hechos de las deltas que le tocan al jugador, guardados en un historial consultable (puerto del mecanismo de
// `BronzeAgeClient/src/ui/avisos.ts`). Cada aviso tiene categoría y urgencia. Los urgentes (te atacan, te echan, piden una decisión) avisan en
// pantalla, no se agrupan y salen arriba. Los demás solo suman a la insignia, y los repetidos (misma `Clave`: tipo y origen) se funden en una
// línea con contador y rango de horas. La insignia cuenta líneas sin leer, no hechos.
//
// Qué hecho da qué aviso se decide al migrar cada pantalla (inventario §3.19): aquí están los que ya se entienden sin ellas. Uno nuevo es una
// rama en `Procesar`. El historial se guarda por héroe (lo hace quien lo usa con `AJson`/`DesdeJson`) y con él la última versión avisada, para
// que lo que vuelve a llegar al reanudar no se avise dos veces.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using Newtonsoft.Json;

namespace ConquestTactics.Mundo
{
    public enum CategoriaAviso { Combate, Diplomacia, Movimientos, Inteligencia, Economia, Aedas }

    /// <summary>`Peligro` = te atacan o te echan; `Combate` = un combate tuyo; `Baja` = pierdes algo; `Mirada` = informativo.</summary>
    public enum ClaseAviso { Peligro, Combate, Baja, Mirada }

    public sealed class EntradaAviso
    {
        public ulong Version;
        /// <summary>Instante de mundo del hecho (ms del contrato).</summary>
        public long EnMs;
        public string Titulo, Texto;
        public ClaseAviso Clase;
        public CategoriaAviso Categoria;
        public bool Urgente, Leido;
        /// <summary>Cuántos avisos iguales (misma `Clave`) se han fundido en esta línea.</summary>
        public int Veces = 1;
        /// <summary>Tipo y origen: solo se agrupan los no urgentes con clave.</summary>
        public string Clave;
        /// <summary>Primera y última llegada, en hora real (ms de época Unix).</summary>
        public long Desde, Hasta;
        /// <summary>El aviso del final de una batalla propia: su id, para abrir su informe mientras el motor la guarde.</summary>
        public string BatallaId;
    }

    public sealed class Avisos
    {
        public const int MaxHistorial = 80;

        /// <summary>Orden y nombre de las categorías en el panel.</summary>
        public static readonly (CategoriaAviso categoria, string nombre)[] Categorias =
        {
            (CategoriaAviso.Combate, "Combate"), (CategoriaAviso.Diplomacia, "Diplomacia"), (CategoriaAviso.Movimientos, "Movimientos"),
            (CategoriaAviso.Inteligencia, "Inteligencia"), (CategoriaAviso.Economia, "Economía y tropas"), (CategoriaAviso.Aedas, "Aedas y tecnología"),
        };

        /// <summary>Lo más reciente primero.</summary>
        public List<EntradaAviso> Historial { get; private set; } = new List<EntradaAviso>();
        /// <summary>La versión de mundo hasta la que ya se avisó.</summary>
        public ulong UltimaVersion { get; private set; }

        /// <summary>Algo cambió en el historial (para repintar el panel y la insignia).</summary>
        public event Action AlCambiar;
        /// <summary>Un aviso urgente que llega en vivo: hay que enseñarlo.</summary>
        public event Action<EntradaAviso> AlUrgente;

        /// <summary>Para el reloj real; las pruebas lo fijan.</summary>
        public Func<long> AhoraReal = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public int NoLeidos => Historial.Count(a => !a.Leido);
        public bool HayPeligroSinLeer => Historial.Any(a => !a.Leido && a.Clase == ClaseAviso.Peligro);

        /// <summary>Anota un aviso. Los no urgentes con la misma clave se funden en la línea anterior (que vuelve arriba como no leída).</summary>
        public void Anotar(EntradaAviso a)
        {
            var ahora = AhoraReal();
            var previa = a.Clave != null && !a.Urgente ? Historial.FindIndex(x => x.Clave == a.Clave) : -1;
            EntradaAviso antes = null;
            if (previa >= 0) { antes = Historial[previa]; Historial.RemoveAt(previa); }
            a.Leido = false;
            a.Veces = (antes?.Veces ?? 0) + 1;
            a.Desde = antes != null && antes.Desde != 0 ? antes.Desde : ahora;
            a.Hasta = ahora;
            Historial.Insert(0, a);
            if (Historial.Count > MaxHistorial) Historial.RemoveRange(MaxHistorial, Historial.Count - MaxHistorial);
            if (a.Version > UltimaVersion) UltimaVersion = a.Version;
            AlCambiar?.Invoke();
            if (a.Urgente) AlUrgente?.Invoke(a);
        }

        /// <summary>Marca como leído todo (null), solo lo urgente (`urgentes`) o una categoría (sin los urgentes, que van en su sección).</summary>
        public void MarcarLeidos(CategoriaAviso? categoria = null, bool urgentes = false)
        {
            var cambia = false;
            foreach (var a in Historial)
            {
                bool toca = urgentes ? a.Urgente : categoria == null || (!a.Urgente && a.Categoria == categoria);
                if (toca && !a.Leido) { a.Leido = true; cambia = true; }
            }
            if (cambia) AlCambiar?.Invoke();
        }

        public void Borrar(EntradaAviso a)
        {
            if (Historial.Remove(a)) AlCambiar?.Invoke();
        }

        public string AJson() => JsonConvert.SerializeObject(new Guardado { UltimaVersion = UltimaVersion, Historial = Historial });

        public void DesdeJson(string json)
        {
            var g = string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<Guardado>(json);
            Historial = g?.Historial ?? new List<EntradaAviso>();
            UltimaVersion = g?.UltimaVersion ?? 0;
            AlCambiar?.Invoke();
        }

        sealed class Guardado
        {
            public ulong UltimaVersion;
            public List<EntradaAviso> Historial;
        }

        /// <summary>Convierte un hecho de una delta en aviso, si le toca al jugador y ya se sabe contar. Lo de una versión ya avisada (lo que
        /// vuelve al reanudar) no se repite.</summary>
        public void Procesar(EventoDominio e, string heroeId, Almacen almacen, DatosDeJuego datos)
        {
            if (e.Version != 0 && e.Version <= UltimaVersion) return;
            var a = Aviso(e, heroeId, almacen, datos);
            if (a == null) return;
            a.Version = e.Version;
            a.EnMs = e.EnMs;
            Anotar(a);
        }

        static EntradaAviso Aviso(EventoDominio e, string heroeId, Almacen almacen, DatosDeJuego datos)
        {
            string Texto(string clave) => e.Payload != null && e.Payload.Fields.TryGetValue(clave, out var v) ? (v.HasStringValue ? v.StringValue : v.NumberValue.ToString()) : null;
            double Numero(string clave) => e.Payload != null && e.Payload.Fields.TryGetValue(clave, out var v) && v.HasNumberValue ? v.NumberValue : 0;
            switch (e.Codigo)
            {
                // Intel: alguien te ha mirado de cerca. Informativo; se agrupa por tipo y origen.
                case "columna.observada":
                case "caravana.observada":
                case "asentamiento.observado":
                case "asentamiento.informe_pedido":
                    var que = e.Codigo == "columna.observada" ? "tu columna" : e.Codigo == "caravana.observada" ? "una caravana tuya" : "la defensa de tu plaza";
                    return new EntradaAviso
                    {
                        Titulo = e.Codigo == "asentamiento.informe_pedido" ? "Pidieron tu plano" : "Te observaron",
                        Texto = e.Codigo == "asentamiento.informe_pedido" ? "Alguien ha pedido el plano de tu plaza en una taberna." : $"Alguien ha mirado de cerca {que}.",
                        Clase = ClaseAviso.Mirada, Categoria = CategoriaAviso.Inteligencia, Clave = e.Codigo + "|" + (e.HasAsentamientoId ? e.AsentamientoId : ""),
                    };
                // Alguien pide unirse a tu ejército (`avisos.ts`): solo al Líder, que contesta en la ventana Ejército (⚑).
                case "columna.union_pedida":
                    if (!almacen.Todas(Entidad.CuerpoOneofCase.Ejercito).Any(c => c.Ejercito.LiderId == heroeId && c.Ejercito.Tipo == TipoColumna.ColumnaEjercito)) return null;
                    return new EntradaAviso
                    {
                        Titulo = "Piden unirse a tu ejército",
                        Texto = "Un héroe de tu Facción pide unirse a tu ejército: contesta en la ventana Ejército (⚑) del mapa antes de que caduque.",
                        Clase = ClaseAviso.Mirada, Categoria = CategoriaAviso.Movimientos, Urgente = true,
                    };
            }
            switch (e.TipadoCase)
            {
                case EventoDominio.TipadoOneofCase.ColumnaConHambre:
                    var sin = e.ColumnaConHambre.SinViveres;
                    return new EntradaAviso
                    {
                        Titulo = sin ? "Sin víveres" : "Víveres bajos",
                        Texto = sin ? "Tu columna se ha quedado sin víveres: la moral baja y, a cero, la tropa deserta. Pasa trigo del carro a los víveres." : "A tu columna le quedan pocos víveres.",
                        Clase = sin ? ClaseAviso.Peligro : ClaseAviso.Baja, Categoria = CategoriaAviso.Combate, Urgente = sin,
                        Clave = sin ? null : e.Codigo + "|" + e.ColumnaConHambre.EjercitoId,
                    };
                case EventoDominio.TipadoOneofCase.HeroeExpulsado:
                    var x = e.HeroeExpulsado;
                    if (x.HeroeId != heroeId) return null;
                    var porque = x.Motivo == "puerta_cerrada" ? "le han cerrado la puerta" : x.Motivo == "conquista" ? "la han conquistado" : x.Motivo == "ruina" ? "ha quedado en ruinas" : "te han desalojado";
                    return new EntradaAviso
                    {
                        Titulo = "Fuera de " + x.LugarId,
                        Texto = $"Has tenido que salir de {x.LugarId}: {porque}." + (x.HasEjercitoId ? " Estás en el mapa con tu columna." : ""),
                        Clase = ClaseAviso.Peligro, Categoria = CategoriaAviso.Movimientos, Urgente = true,
                    };
            }
            if (e.Codigo == "mision.cumplida")
            {
                var id = Texto("mision") ?? "";
                var titulo = TituloDeMision(datos, id);
                return new EntradaAviso
                {
                    Titulo = "Misión cumplida", Texto = $"¡Misión cumplida: {titulo}! Reclama la recompensa en Misiones.",
                    Clase = ClaseAviso.Mirada, Categoria = CategoriaAviso.Economia, Urgente = true,
                };
            }
            if (e.Codigo == "bandidos.campamento_vencido")
            {
                var nivel = (int)Numero("nivel");
                return new EntradaAviso
                {
                    Titulo = "Bandidos", Texto = $"Vencéis a los bandidos que os emboscaron{(nivel > 0 ? $" (nivel {nivel})" : "")}: el campamento cae.",
                    Clase = ClaseAviso.Combate, Categoria = CategoriaAviso.Combate, Clave = e.Codigo + "|" + e.Version,
                };
            }
            return null;
        }

        /// <summary>El título de una misión de `Misiones_Textos.TEXTOS_MISIONES`; sin él, su id.</summary>
        static string TituloDeMision(DatosDeJuego datos, string id)
        {
            try { return (string)datos?.Bloque("Misiones_Textos", "TEXTOS_MISIONES")["misiones"]?[id]?["titulo"] ?? id; }
            catch (FaltaDato) { return id; }
        }
    }
}
