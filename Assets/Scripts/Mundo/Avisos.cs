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
            if (e.Codigo != null && e.Codigo.StartsWith("mercenarios.") && TextoDeMercenario(e.Codigo, Texto, Numero, almacen) is string merc)
                return new EntradaAviso
                {
                    Titulo = "Mercenarios", Texto = merc, Clase = ClaseAviso.Mirada,
                    Categoria = e.Codigo.StartsWith("mercenarios.orden") ? CategoriaAviso.Movimientos : CategoriaAviso.Economia,
                    Clave = e.Codigo + "|" + Texto("mercenario"),
                };
            if (e.Codigo != null && e.Codigo.StartsWith("aedas.") && TextoDeAeda(e, Texto, heroeId, almacen, datos) is { } deAeda)
                return new EntradaAviso
                {
                    Titulo = deAeda.Item1, Texto = deAeda.Item2, Clase = ClaseAviso.Mirada, Categoria = CategoriaAviso.Aedas, Urgente = deAeda.Item3,
                    Clave = deAeda.Item3 ? null : e.Codigo + "|" + e.Version,
                };
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

        static readonly string[] OrdenesDeMercenario = { "Guardar", "Ir a", "Atacar", "Unirse" };
        static readonly Dictionary<string, string> FinDeContrato = new Dictionary<string, string>
        {
            ["plazo"] = "se cumplió el plazo", ["despido"] = "despedido", ["plaza_perdida"] = "se perdió la plaza que lo firmó",
            ["sin_ciudadanos"] = "no quedan ciudadanos sin marca", ["faccion_perdida"] = "dejó de ser ciudadano",
        };

        /// <summary>El texto de un hecho `mercenarios.*` (`textoDeHechoMercenario`; los ids de héroe llegan como números). Los del héroe bot (ofrecerse,
        /// rango, medallas, premio) no le tocan a un jugador: null.</summary>
        static string TextoDeMercenario(string codigo, Func<string, string> texto, Func<string, double> numero, Almacen almacen)
        {
            var quien = texto("mercenario") is string m ? Textos.NombreDeHeroe(almacen, "heroe-" + m) : "Un mercenario";
            var monedas = numero("monedas");
            var oro = numero("oro") / 1000;
            var pagado = monedas > 0 ? $" por {monedas:N0} monedas" : oro > 0 ? $" por {Math.Round(oro, 1)} de oro" : "";
            var t = (int)numero("tipo");
            var orden = texto("tipo") != null && t >= 0 && t < OrdenesDeMercenario.Length ? $"«{OrdenesDeMercenario[t]}»" : "";
            switch (codigo)
            {
                case "mercenarios.contratado": return $"{quien} entra a sueldo de tu Facción{pagado}.";
                case "mercenarios.prorrogado": return $"Se prorroga el contrato de {quien}{pagado}.";
                case "mercenarios.terminado":
                    var motivo = texto("motivo") ?? "";
                    return $"Acaba el contrato de {quien}: {(FinDeContrato.TryGetValue(motivo, out var fin) ? fin : motivo)}. Puedes valorarlo en Facción › Mercenarios.";
                case "mercenarios.valorado": return $"{quien} recibe su valoración.";
                case "mercenarios.ordenado": return $"{quien} recibe la orden {orden}.";
                case "mercenarios.orden_cumplida": return $"{quien} cumple la orden {orden}.";
                case "mercenarios.orden_caducada":
                    var porque = texto("motivo") switch { "plazo" => "venció el plazo", "objetivo" => "el objetivo ya no vale", "paz" => "llegó la paz", "salida" => "salió de lo que acompañaba", var x => x ?? "" };
                    return $"Caduca la orden {orden} de {quien}: {porque}. Sin orden, solo se defiende.";
                case "mercenarios.mudado": return $"{quien} se muda a otra plaza.";
                default: return null;
            }
        }

        /// <summary>El texto de un hecho de los Aedas para tu Facción (`textoDeHechoAeda`), o null si no le toca. `importante`: sale en pantalla (una
        /// tecnología comprada o cumplida, un residente que se va, una épica perdida por conquista).</summary>
        static (string, string, bool)? TextoDeAeda(EventoDominio e, Func<string, string> texto, string heroeId, Almacen almacen, DatosDeJuego datos)
        {
            var faccion = almacen.Buscar(TipoEntidad.Heroe, heroeId)?.Heroe is Heroe h && h.HasFaccionId ? h.FaccionId : null;
            if (faccion == null || "f-" + texto("faccion") != faccion) return null;
            string NombreDeTecnologia(string id)
            {
                if (id == null) return null;
                var tecs = (datos?.Raiz?["documentos"] as Newtonsoft.Json.Linq.JObject)?.Properties().Select(p => (p.Value as Newtonsoft.Json.Linq.JObject)?["TECNOLOGIAS"]).FirstOrDefault(x => x != null);
                return (string)tecs?[id]?["nombre"] ?? id.Replace('_', ' ');
            }
            string Plaza(string id) => id == null ? "una plaza" : almacen.Buscar(TipoEntidad.Asentamiento, id)?.Asentamiento is Asentamiento a && a.HasNombre ? a.Nombre : "una plaza";
            string Faccion(string n) => n == null ? "otra Facción" : "f-" + n == faccion ? "tu Facción" : almacen.Buscar(TipoEntidad.Faccion, "f-" + n)?.Faccion?.Nombre ?? "otra Facción";
            var tec = texto("tecnologia") is string t ? $"«{NombreDeTecnologia(t)}»" : "una tecnología";
            var plaza = Plaza(e.HasAsentamientoId ? e.AsentamientoId : texto("plaza") is string p ? "as-" + p : null);
            var itinerante = texto("aeda") is string n0 ? CosasDelMapa.NombreDeAeda("aeda-" + n0) : null;
            var residente = texto("aedaResidente") is string r ? almacen.Buscar(TipoEntidad.Aeda, "aeda-r" + r)?.AedaResidente?.Nombre ?? "Un Aeda" : "Un Aeda";
            var capitulos = texto("capitulos");
            switch (e.Codigo)
            {
                case "aedas.revela":
                    return ("Aedas", $"{itinerante ?? "Un Aeda itinerante"} canta en {plaza} {tec}, que descubrió {Faccion(texto("descubridora"))}. Os falta su hito; mientras siga allí os la vende.", false);
                case "aedas.venta":
                    return ("Aedas", $"{plaza} le compra {tec} a {itinerante ?? "un Aeda"}{(texto("precio") is string pr ? $" por {pr} de oro" : "")}: ya es de tu Facción, falta adoptarla.", true);
                case "aedas.residente_llega":
                    return ("Aedas", $"{residente} se queda a vivir en {plaza}: un Aeda residente que puede cantar una épica.", false);
                case "aedas.residente_se_va":
                    return ("Aedas", $"Un Aeda residente se va de {plaza}: ya no hay sitio para él{(texto("cupo") is string cu ? $" (caben {cu})" : "")}.", true);
                case "aedas.epica_empieza":
                    return ("Épica", $"{residente} empieza en {plaza} la épica de {tec}{(capitulos != null ? $" ({capitulos} capítulos)" : "")}.", false);
                case "aedas.epica_capitulo":
                    return ("Épica", $"{residente} cierra en {plaza} el capítulo {texto("capitulo") ?? "?"}{(capitulos != null ? $" de {capitulos}" : "")} de la épica de {tec}.", false);
                case "aedas.epica_tecnologia":
                    return ("Épica cumplida", $"{residente} termina en {plaza} la épica de {tec}: la tecnología ya es de tu Facción, falta adoptarla.", true);
                case "aedas.epica_abandonada":
                    var motivo = texto("motivo");
                    var por = motivo == "conquista" ? $"porque {plaza} cayó en otras manos" : motivo == "aparecida" ? "porque la tecnología os llegó por otra vía"
                        : motivo == "unificada" ? "porque otro Aeda de tu Facción ya la canta" : "por orden de la plaza";
                    return ("Épica", $"Se abandona la épica de {tec} {por}; lo avanzado se pierde.", motivo == "conquista");
                default: return null;
            }
        }

        /// <summary>El título de una misión de `Misiones_Textos.TEXTOS_MISIONES`; sin él, su id.</summary>
        static string TituloDeMision(DatosDeJuego datos, string id)
        {
            try { return (string)datos?.Bloque("Misiones_Textos", "TEXTOS_MISIONES")["misiones"]?[id]?["titulo"] ?? id; }
            catch (FaltaDato) { return id; }
        }
    }
}
