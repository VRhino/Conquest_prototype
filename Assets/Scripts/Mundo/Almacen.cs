// Lo que el cliente guarda de sus ámbitos (contrato 05 §5 y §7). Puerto del almacén de referencia del motor (`crates/contrato/src/almacen.rs`)
// por el de BronzeAgeClient (`src/contrato/almacen.ts`): cada `Foto` sustituye al ámbito, cada `Delta` se aplica sobre él (la plaza parcial con
// su máscara, la niebla parcial por tramos) y tiene que empezar donde acabó la anterior, cada `Revocado` lo descarta; y se guardan los hechos.
//
// A diferencia del de referencia, no se cae ante lo que el contrato no permite: lanza `Incoherencia` y la sesión reconecta para recibir fotos
// limpias. C# puro: se prueba en EditMode sin escena.
using System;
using System.Collections;
using System.Collections.Generic;
using BronzeAge.Contract.V1;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace ConquestTactics.Mundo
{
    /// <summary>Un ámbito tal como lo tiene el cliente. Las entidades van por (clase del contrato, id).</summary>
    public sealed class Ambito
    {
        public ulong Version;
        public readonly Dictionary<(TipoEntidad, string), Entidad> Entidades = new Dictionary<(TipoEntidad, string), Entidad>();
        public NieblaProyectada Niebla;
    }

    /// <summary>Lo que el almacén no pudo aplicar porque el contrato no lo permite; la sesión reconecta al verlo.</summary>
    public sealed class Incoherencia : Exception
    {
        /// <summary>El ámbito que quedó a medias, si se sabe: basta con tirar ese y pedirlo de nuevo.</summary>
        public readonly string Ambito;
        public Incoherencia(string mensaje, string ambito = null) : base(mensaje) { Ambito = ambito; }
    }

    public sealed class Almacen
    {
        /// <summary>Cuántos hechos se guardan, como mucho (los últimos).</summary>
        public const int MaxHechos = 500;

        public readonly Dictionary<string, Ambito> Ambitos = new Dictionary<string, Ambito>();
        public readonly List<(string ambito, EventoDominio evento)> Hechos = new List<(string, EventoDominio)>();

        /// <summary>Nombre de cada héroe que ha salido en algún mensaje (`MensajeDeServidor.nombres_heroes`). Los nombres no cambian: solo se añade.</summary>
        public readonly Dictionary<string, string> NombresHeroes = new Dictionary<string, string>();

        /// <summary>La clave `TIPO:id` de un ámbito (`HEROE:heroe-3`, `VISTA:f-1`, `ADMIN:`).</summary>
        public static string ClaveDeAmbito(BronzeAge.Contract.V1.Ambito a)
        {
            if (a == null) throw new Incoherencia("un mensaje de ámbito sin ámbito");
            return ClaveDeAmbito(a.Tipo, a.Id);
        }

        public static string ClaveDeAmbito(AmbitoTipo tipo, string id) =>
            // `AmbitoHeroe` → `HEROE`: todos los tipos son una palabra.
            tipo.ToString().Substring("Ambito".Length).ToUpperInvariant() + ":" + id;

        /// <summary>La clase y el id del contrato de una entidad (los mismos que usan los `Cambio`). Sin caso por defecto que calle: una clase
        /// nueva del `oneof` lanza aquí en vez de guardarse con una clave inventada.</summary>
        public static (TipoEntidad, string) Identidad(Entidad e)
        {
            switch (e.CuerpoCase)
            {
                case Entidad.CuerpoOneofCase.Ejercito: return (TipoEntidad.Ejercito, e.Ejercito.Id);
                case Entidad.CuerpoOneofCase.Asentamiento: return (TipoEntidad.Asentamiento, e.Asentamiento.Id);
                case Entidad.CuerpoOneofCase.Faccion: return (TipoEntidad.Faccion, e.Faccion.Id);
                case Entidad.CuerpoOneofCase.Tecnologias: return (TipoEntidad.TecnologiaFaccion, e.Tecnologias.FaccionId);
                case Entidad.CuerpoOneofCase.Heroe: return (TipoEntidad.Heroe, e.Heroe.Id);
                case Entidad.CuerpoOneofCase.InformeInspeccion: return (TipoEntidad.InformeInspeccion, e.InformeInspeccion.Id);
                case Entidad.CuerpoOneofCase.Escuadron: return (TipoEntidad.Escuadron, e.Escuadron.Id);
                case Entidad.CuerpoOneofCase.Caravana: return (TipoEntidad.Caravana, e.Caravana.Id);
                case Entidad.CuerpoOneofCase.Convocatoria: return (TipoEntidad.Convocatoria, e.Convocatoria.Id);
                case Entidad.CuerpoOneofCase.AcuerdoTrueque: return (TipoEntidad.AcuerdoTrueque, e.AcuerdoTrueque.Id);
                case Entidad.CuerpoOneofCase.OrdenMercado: return (TipoEntidad.OrdenMercado, e.OrdenMercado.Id);
                case Entidad.CuerpoOneofCase.Batalla: return (TipoEntidad.Batalla, e.Batalla.Id);
                case Entidad.CuerpoOneofCase.Relacion: return (TipoEntidad.RelacionPolitica, e.Relacion.Id);
                case Entidad.CuerpoOneofCase.Propuesta: return (TipoEntidad.Propuesta, e.Propuesta.Id);
                case Entidad.CuerpoOneofCase.Titulo: return (TipoEntidad.Titulo, e.Titulo.TituloId);
                case Entidad.CuerpoOneofCase.Pueblo: return (TipoEntidad.Pueblo, e.Pueblo.Id);
                case Entidad.CuerpoOneofCase.Obra: return (TipoEntidad.ObraDeMapa, e.Obra.Id);
                case Entidad.CuerpoOneofCase.Avanzada: return (TipoEntidad.Avanzada, e.Avanzada.Id);
                case Entidad.CuerpoOneofCase.CampamentoMercenarios: return (TipoEntidad.CampamentoMercenarios, e.CampamentoMercenarios.Id);
                case Entidad.CuerpoOneofCase.CampamentoBandidos: return (TipoEntidad.CampamentoBandidos, e.CampamentoBandidos.Id);
                case Entidad.CuerpoOneofCase.FichaConocida: return (TipoEntidad.FichaConocida, e.FichaConocida.AsentamientoId);
                case Entidad.CuerpoOneofCase.ZonaFaccion: return (TipoEntidad.ZonaFaccion, e.ZonaFaccion.FaccionId);
                case Entidad.CuerpoOneofCase.RedCaminos: return (TipoEntidad.RedCaminos, "red");
                case Entidad.CuerpoOneofCase.Mirada: return (TipoEntidad.MiradaIntel, e.Mirada.Id);
                case Entidad.CuerpoOneofCase.Informe: return (TipoEntidad.InformePlaza, e.Informe.AsentamientoId);
                case Entidad.CuerpoOneofCase.EstadoTecnologia: return (TipoEntidad.EstadoTecnologia, "tecnologia");
                case Entidad.CuerpoOneofCase.AedaItinerante: return (TipoEntidad.Aeda, e.AedaItinerante.Id);
                case Entidad.CuerpoOneofCase.AedaResidente: return (TipoEntidad.Aeda, e.AedaResidente.Id);
                case Entidad.CuerpoOneofCase.Alijo: return (TipoEntidad.Alijo, e.Alijo.Id);
                case Entidad.CuerpoOneofCase.Nodo: return (TipoEntidad.NodoRecurso, e.Nodo.Id);
                case Entidad.CuerpoOneofCase.Memoria: return (TipoEntidad.MemoriaFaccion, e.Memoria.FaccionId);
                case Entidad.CuerpoOneofCase.DirectorioFacciones: return (TipoEntidad.DirectorioFacciones, "facciones");
                default: throw new Incoherencia($"una entidad sin clase conocida ({e.CuerpoCase})");
            }
        }

        /// <summary>Aplica un mensaje del servidor; devuelve la clave del ámbito que cambió, o null si no era de un ámbito.</summary>
        public string Aplicar(MensajeDeServidor m)
        {
            switch (m.CuerpoCase)
            {
                case MensajeDeServidor.CuerpoOneofCase.Foto: return AplicarFoto(m.Foto);
                case MensajeDeServidor.CuerpoOneofCase.Delta: return AplicarDelta(m.Delta);
                case MensajeDeServidor.CuerpoOneofCase.Revocado:
                    var k = ClaveDeAmbito(m.Revocado.Ambito);
                    Ambitos.Remove(k);
                    return k;
                default: return null;
            }
        }

        string AplicarFoto(Foto f)
        {
            var k = ClaveDeAmbito(f.Ambito);
            var a = new Ambito { Version = f.Version, Niebla = f.Niebla };
            foreach (var e in f.Entidades)
                if (e.CuerpoCase != Entidad.CuerpoOneofCase.None) a.Entidades[Identidad(e)] = e;
            // La VISTA vieja se conserva al reconectar hasta que llega la nueva (ver `Sesion`); la nueva la sustituye aunque sea de otra perspectiva.
            if (k.StartsWith("VISTA:", StringComparison.Ordinal))
                foreach (var otra in new List<string>(Ambitos.Keys))
                    if (otra.StartsWith("VISTA:", StringComparison.Ordinal) && otra != k) Ambitos.Remove(otra);
            Ambitos[k] = a;
            return k;
        }

        string AplicarDelta(Delta d)
        {
            var k = ClaveDeAmbito(d.Ambito);
            if (!Ambitos.TryGetValue(k, out var a)) throw new Incoherencia($"una Delta de {k}, que el cliente no tiene", k);
            if (d.Desde != a.Version) throw new Incoherencia($"la Delta de {k} empieza en {d.Desde} y el cliente tiene {a.Version} (hueco)", k);
            if (d.Hasta < d.Desde) throw new Incoherencia($"la Delta de {k} va hacia atrás", k);
            foreach (var c in d.Cambios)
            {
                var e = (c.Entidad, c.Id);
                var datos = c.Datos;
                if (c.Tipo == TipoCambio.CambioQuita)
                {
                    // Lo que nació y desapareció dentro del tramo se quita sin que el cliente lo llegara a tener: no es un error.
                    a.Entidades.Remove(e);
                }
                else if (c.Reemplaza)
                {
                    if (datos == null || datos.CuerpoCase == Entidad.CuerpoOneofCase.None) throw new Incoherencia($"ACTUALIZA sin datos en {k}: {e}", k);
                    a.Entidades[e] = datos;
                }
                else
                {
                    if (!a.Entidades.TryGetValue(e, out var destino) || destino.CuerpoCase != Entidad.CuerpoOneofCase.Asentamiento
                        || datos == null || datos.CuerpoCase != Entidad.CuerpoOneofCase.Asentamiento)
                        throw new Incoherencia($"en {k} solo la plaza viaja parcial, y sobre una que el cliente tiene: {e}", k);
                    AplicarPlaza(destino.Asentamiento, datos.Asentamiento, c.Campos);
                }
            }
            if (d.Niebla != null)
            {
                if (d.Niebla.Parcial && a.Niebla == null) throw new Incoherencia($"niebla parcial en {k} sin una niebla previa", k);
                a.Niebla = AplicarNiebla(a.Niebla, d.Niebla);
            }
            a.Version = d.Hasta;
            foreach (var ev in d.Eventos) Hechos.Add((k, ev));
            if (Hechos.Count > MaxHechos) Hechos.RemoveRange(0, Hechos.Count - MaxHechos);
            return k;
        }

        /// <summary>El cambio parcial de la plaza (máscara de campos, `crates/contrato/src/plaza.rs`): copia de `datos` los campos de la máscara;
        /// un campo con su valor por defecto se pone a él, un `optional` ausente se borra. Los nombres de la máscara (snake_case del .proto) se
        /// resuelven con el descriptor, así que un campo nuevo del contrato entra solo al regenerar.</summary>
        public static void AplicarPlaza(Asentamiento destino, Asentamiento datos, IEnumerable<string> campos)
        {
            foreach (var nombre in campos)
            {
                if (nombre == "id") continue;
                var f = Asentamiento.Descriptor.FindFieldByName(nombre);
                if (f == null) continue; // un campo de un contrato más nuevo que este cliente
                var acc = f.Accessor;
                if (f.IsMap)
                {
                    var d = (IDictionary)acc.GetValue(destino);
                    d.Clear();
                    foreach (DictionaryEntry x in (IDictionary)acc.GetValue(datos)) d[x.Key] = x.Value;
                }
                else if (f.IsRepeated)
                {
                    var d = (IList)acc.GetValue(destino);
                    d.Clear();
                    foreach (var x in (IList)acc.GetValue(datos)) d.Add(x);
                }
                else if (f.HasPresence && !acc.HasValue(datos)) acc.Clear(destino);
                else acc.SetValue(destino, acc.GetValue(datos));
            }
        }

        /// <summary>La niebla parcial (`crates/contrato/src/niebla.rs`): la entera sustituye a la que hay; la parcial sobrescribe los bytes de sus tramos.</summary>
        public static NieblaProyectada AplicarNiebla(NieblaProyectada destino, NieblaProyectada llega)
        {
            if (!llega.Parcial) return llega;
            if (destino == null) throw new Incoherencia("una niebla parcial llega sin la niebla previa");
            destino.Celdas = Parchear(destino.Celdas, llega.TramosCeldas);
            destino.Visibles = Parchear(destino.Visibles, llega.TramosVisibles);
            return destino;
        }

        static ByteString Parchear(ByteString mascara, IEnumerable<TramoDeNiebla> tramos)
        {
            var b = mascara.ToByteArray();
            foreach (var t in tramos)
            {
                if (t.Desde + t.Datos.Length > b.Length) throw new Incoherencia("un tramo de niebla se sale de su máscara");
                t.Datos.CopyTo(b, (int)t.Desde);
            }
            return ByteString.CopyFrom(b);
        }

        /// <summary>Estado de una celda de la niebla: 0 nunca vista, 1 vista alguna vez, 2 a la vista ahora.</summary>
        public static int EstadoCelda(NieblaProyectada n, int columna, int fila)
        {
            int i = fila * (int)n.Columnas + columna;
            bool Bit(ByteString m) => (i >> 3) < m.Length && ((m[i >> 3] >> (i & 7)) & 1) == 1;
            if (Bit(n.Visibles)) return 2;
            return Bit(n.Celdas) ? 1 : 0;
        }

        /// <summary>Las entidades de una clase, de todos los ámbitos (la misma entidad en dos ámbitos sale una vez: son copias de lo mismo).</summary>
        public List<Entidad> Todas(Entidad.CuerpoOneofCase caso)
        {
            var vistas = new Dictionary<(TipoEntidad, string), Entidad>();
            foreach (var a in Ambitos.Values)
                foreach (var kv in a.Entidades)
                    if (kv.Value.CuerpoCase == caso) vistas[kv.Key] = kv.Value;
            return new List<Entidad>(vistas.Values);
        }

        /// <summary>Una entidad por su clase e id, de cualquier ámbito; null si no se tiene.</summary>
        public Entidad Buscar(TipoEntidad tipo, string id)
        {
            foreach (var a in Ambitos.Values)
                if (a.Entidades.TryGetValue((tipo, id), out var e)) return e;
            return null;
        }

        /// <summary>El `Hola` de quien reanuda con lo que conserva: la versión de cada ámbito (salvo `VISTA`, que no se reanuda) y la secuencia
        /// interior de cada plaza con su interior (`ASENTAMIENTO` o, en `ADMIN`, todas) y de las de su Facción vistas desde fuera (clave `PROPIA:`).</summary>
        public Hola Hola(string token, TipoCliente cliente, string versionCliente, string mundoHash, long relojClienteMs)
        {
            var h = new Hola
            {
                VersionProtocolo = 1,
                Cliente = cliente,
                VersionCliente = versionCliente ?? "",
                Token = token ?? "",
                RelojClienteMs = relojClienteMs,
            };
            if (!string.IsNullOrEmpty(mundoHash)) h.MundoHashConocido = mundoHash;
            h.Compresiones.Add("zstd");
            foreach (var kv in Ambitos)
            {
                var k = kv.Key;
                if (k.StartsWith("VISTA:", StringComparison.Ordinal)) continue;
                h.VersionesConocidas[k] = kv.Value.Version;
                bool interior = k.StartsWith("ASENTAMIENTO:", StringComparison.Ordinal) || k == "ADMIN:";
                bool propia = k.StartsWith("FACCION:", StringComparison.Ordinal);
                if (!interior && !propia) continue;
                foreach (var e in kv.Value.Entidades.Values)
                {
                    if (e.CuerpoCase != Entidad.CuerpoOneofCase.Asentamiento) continue;
                    var p = e.Asentamiento;
                    if (interior) h.VersionesInterioresConocidas["ASENTAMIENTO:" + p.Id] = p.VersionInterior;
                    else if (p.VersionInterior > 0) h.VersionesInterioresConocidas["PROPIA:" + p.Id] = p.VersionInterior;
                }
            }
            return h;
        }

        public void Vaciar()
        {
            Ambitos.Clear();
            Hechos.Clear();
        }
    }
}
