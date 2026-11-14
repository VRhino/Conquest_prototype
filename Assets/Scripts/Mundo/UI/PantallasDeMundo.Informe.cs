// Después de la batalla (paso 5, tanda 2, del bloque 4). Al acabar la partida, el cartel de victoria o derrota de Conquest unos segundos y
// luego el informe de combate, en su escena (InformeScene), con la sesión viva. También al abandonar (con confirmación) y si la conexión
// con la batalla se pierde y no vuelve. El informe calca el de BronzeAgeClient (`ui/informeCombate.ts`, `htmlInforme`): «Informe de
// combate» con la fecha, «Victoria» o «Derrota» y lo que pasó, los dos bandos, las escuadras de cada uno con sus bajas (con las piezas de
// tropa de Conquest), los héroes, y lo que ganas o pierdes. Solo lo que se sabe con certeza:
//   - del resultado del motor (`Batalla.resultado`, por la sesión viva): ganador y razón, por escuadra los desplegados, los que vuelven y los
//     muertos, y por héroe si participó y si cayó. Hasta que el motor lo aplica, «calculando…»;
//   - comparando el héroe y su columna de antes y de después: las monedas, el carro, los objetos nuevos, el nivel y la experiencia, y la
//     herida (`herido_hasta_ms`). Si no hay «antes» (el informe abierto desde el mundo en otra partida del cliente), eso no sale.
// «Salir» vuelve a MundoScene (la vuelta de la tanda 1), con la columna donde la deja el motor: en el sitio de la batalla. El mismo informe
// se abre desde el aviso del final de la batalla («Ver informe») mientras el motor la guarde (una hora). Docs/Arquitectura/28.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Net;
using Newtonsoft.Json.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed partial class PantallasDeMundo
    {
        public const string EscenaInforme = "InformeScene";
        const string MotivoAbandono = "Abandonaste la batalla.";
        /// <summary>Lo que se deja el cartel de victoria o derrota de Conquest antes del informe.</summary>
        const float SegundosDelCartel = 5f;

        /// <summary>El héroe y su columna al empezar la batalla, para comparar con lo de después.</summary>
        sealed class Antes
        {
            public ulong Monedas;
            public uint Nivel;
            public ulong HaciaSiguiente;
            public Dictionary<string, long> Carro = new Dictionary<string, long>();
            public HashSet<string> Objetos = new HashSet<string>();
        }

        readonly Dictionary<string, Antes> antesDeLaBatalla = new Dictionary<string, Antes>();
        readonly HashSet<string> informesAnotados = new HashSet<string>();
        VisualElement panelInforme, capaDeBatalla;
        float finVistoEn = -1f;
        bool yendoAlInforme;

        /// <summary>Al empezar la batalla: lo de antes, y el botón «Abandonar la batalla» encima de BattleScene.</summary>
        void AlEntrarEnLaBatalla(string batallaId)
        {
            var h = HeroeAhora();
            if (h != null)
            {
                var antes = new Antes { Monedas = h.Monedas, Nivel = h.Nivel, HaciaSiguiente = h.ExperienciaHaciaSiguienteNivel };
                foreach (var o in h.Inventario) if (!string.IsNullOrEmpty(o.ItemInstanceId)) antes.Objetos.Add(o.ItemInstanceId);
                var col = h.Ubicacion?.EjercitoId == null ? null : c.Sesion.Almacen.Buscar(TipoEntidad.Ejercito, h.Ubicacion.EjercitoId)?.Ejercito;
                if (col?.Suministro != null) foreach (var kv in col.Suministro.PorId) antes.Carro[kv.Key] = kv.Value;
                antesDeLaBatalla[batallaId] = antes;
            }
            finVistoEn = -1f;
            yendoAlInforme = false;
            capaDeBatalla?.RemoveFromHierarchy();
            capaDeBatalla = Dialogo.Caja("batalla-capa");
            capaDeBatalla.pickingMode = PickingMode.Ignore;
            var abandonar = Dialogo.Boton("Abandonar la batalla", "boton-secundario batalla-abandonar", async () =>
            {
                if (!await dialogo.Abrir("Tu héroe sale de la batalla con las bajas que lleve y queda herido un rato.", "¿Abandonar la batalla?", "Abandonar", "Seguir", true)) return;
                var cliente = MundoDeRed("ClientWorld");
                if (cliente != null) ReconexionClienteSystem.Abandonar(cliente.EntityManager);
            });
            abandonar.name = "batalla-abandonar";
            capaDeBatalla.Add(abandonar);
            capaEncima.Add(capaDeBatalla);
        }

        static World MundoDeRed(string nombre)
        {
            foreach (var w in World.All) if (w.IsCreated && w.Name == nombre) return w;
            return null;
        }

        /// <summary>Cada fotograma con una batalla en curso: cuándo se pasa al informe (fin de la partida, abandono o conexión perdida).</summary>
        void VigilarFinDeBatalla()
        {
            if (BatallaEnCurso == null || yendoAlInforme) return;
            if (ReconexionClienteSystem.Abandonada) _ = IrAlInforme(MotivoAbandono);
            else if (ReconexionClienteSystem.Perdida) _ = IrAlInforme("Perdiste la conexión con la batalla.");
            else if (ReconexionClienteSystem.Acabada)
            {
                if (finVistoEn < 0) finVistoEn = Time.realtimeSinceStartup;
                else if (Time.realtimeSinceStartup - finVistoEn >= SegundosDelCartel) _ = IrAlInforme(null);
            }
        }

        /// <summary>De la batalla al informe: se deja el servidor de batalla (vuelve el mundo ECS de siempre) y se carga su escena.</summary>
        async System.Threading.Tasks.Task IrAlInforme(string motivo)
        {
            yendoAlInforme = true;
            var id = BatallaEnCurso;
            Debug.Log($"[Batalla] {id}: al informe{(motivo != null ? $" ({motivo})" : "")}");
            capaDeBatalla?.RemoveFromHierarchy();
            capaDeBatalla = null;
            // Primero la escena del informe (que quita BattleScene y sus figuras) y después el mundo de cliente: las figuras leen su EntityManager.
            var carga = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(EscenaInforme);
            while (carga != null && !carga.isDone) await System.Threading.Tasks.Task.Yield();
            if (this == null) return;
            EntradaEnBatalla.Salir();
            BatallaEnCurso = null;
            pantalla.Clear();
            pantalla.parent.style.display = DisplayStyle.Flex;
            AbrirInforme(id, motivo, true);
        }

        /// <summary>El informe de una batalla propia, en su escena (con «Salir» al mapa) o encima del mapa (con «Cerrar»).</summary>
        public void AbrirInforme(string batallaId, string motivo, bool enSuEscena)
        {
            panelInforme?.RemoveFromHierarchy();
            panelInforme = Dialogo.Caja("dialogo-velo informe-velo");
            panelInforme.name = "panel-informe";
            capaEncima.Add(panelInforme);
            string pintado = null;
            void Refrescar()
            {
                var b = c.Sesion?.Almacen.Buscar(TipoEntidad.Batalla, batallaId)?.Batalla;
                var yo = HeroeAhora();
                var col = yo?.Ubicacion?.EjercitoId == null ? null : c.Sesion?.Almacen.Buscar(TipoEntidad.Ejercito, yo.Ubicacion.EjercitoId)?.Ejercito;
                var firma = (b == null ? "-" : $"{b.Version}|{b.Estado}|{b.Resultado != null}") + $"|{yo?.Version}|{col?.Version}|{col?.HasBatallaId}";
                if (firma == pintado) return;
                pintado = firma;
                PintarInforme(batallaId, b, motivo, enSuEscena);
                if (b != null && b.Estado == EstadoBatalla.BatallaAplicada) AnotarInforme(b);
            }
            Refrescar();
            panelInforme.schedule.Execute(Refrescar).Every(500);
        }

        void CerrarInforme()
        {
            panelInforme?.RemoveFromHierarchy();
            panelInforme = null;
        }

        /// <summary>El aviso del final de una batalla propia, con «Ver informe» (una vez por batalla).</summary>
        void AnotarInforme(Batalla b)
        {
            if (!informesAnotados.Add(b.Id) || b.Resultado == null || !b.HasLadoPropio) return;
            bool victoria = b.Resultado.Ganador == b.LadoPropio;
            c.Avisos.Anotar(new EntradaAviso
            {
                Titulo = "Informe de combate", Texto = $"{(victoria ? "Victoria" : "Derrota")}: {CosasDelMapa.NombreDeBatalla(b).ToLowerInvariant()}.",
                Clase = victoria ? ClaseAviso.Combate : ClaseAviso.Baja, Categoria = CategoriaAviso.Combate, BatallaId = b.Id,
                EnMs = b.HasAplicadaEnMs ? b.AplicadaEnMs : 0,
            });
        }

        /// <summary>Desde el mapa: las batallas propias que el motor aplica dejan su aviso, aunque no se haya visto el informe.</summary>
        void AnotarInformesDelMapa()
        {
            foreach (var e in c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Batalla))
                if (e.Batalla.Estado == EstadoBatalla.BatallaAplicada && e.Batalla.HasLadoPropio) AnotarInforme(e.Batalla);
        }

        static readonly Dictionary<RazonVictoria, string> Razones = new Dictionary<RazonVictoria, string>
        {
            [RazonVictoria.ObjetivosCapturados] = "por los objetivos capturados",
            [RazonVictoria.Aniquilacion] = "por aniquilación",
            [RazonVictoria.TiempoAgotado] = "por tiempo: se agotó y gana quien defiende",
        };

        /// <summary>El informe (`htmlInforme` del prototipo), con lo que hay ahora de la batalla.</summary>
        void PintarInforme(string batallaId, Batalla b, string motivo, bool enSuEscena)
        {
            panelInforme.Clear();
            var caja = Dialogo.Caja("informe");
            caja.name = "informe";
            panelInforme.Add(caja);
            var cuerpo = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            cuerpo.AddToClassList("informe-cuerpo");
            caja.Add(cuerpo);
            var momento = b != null && b.HasAplicadaEnMs ? " · " + Instante.AFecha(b.AplicadaEnMs).ToString("dd/MM/yyyy HH:mm") : "";
            cuerpo.Add(Dialogo.Texto("INFORME DE COMBATE" + momento, "kicker"));
            if (motivo != null && motivo != MotivoAbandono) cuerpo.Add(Dialogo.Texto(motivo, "informe-motivo"));

            var r = b?.Resultado;
            // Quien abandona deja de combatir en ella y el motor ya no le enseña la batalla ni su resultado: solo lo suyo, cuando su columna queda libre.
            if (motivo == MotivoAbandono && (b == null || !b.HasLadoPropio))
            {
                var yo = HeroeAhora();
                var columna = yo?.Ubicacion?.EjercitoId == null ? null : c.Sesion.Almacen.Buscar(TipoEntidad.Ejercito, yo.Ubicacion.EjercitoId)?.Ejercito;
                var tituloAbandono = Dialogo.Texto("Derrota · Abandonaste la batalla", "informe-titulo informe-titulo--derrota");
                tituloAbandono.name = "informe-titulo";
                cuerpo.Add(tituloAbandono);
                cuerpo.Add(Dialogo.Texto("Al salir antes de que acabe, el motor ya no te cuenta cómo terminó: solo lo que te pasa a ti.", "campo-ayuda"));
                var filaAbandono = Dialogo.Caja("informe-recuadros");
                cuerpo.Add(filaAbandono);
                if (yo != null && columna != null && !columna.HasBatallaId && antesDeLaBatalla.TryGetValue(batallaId, out var antesA)) LoQueCambia(filaAbandono, yo, antesA, false);
                else cuerpo.Add(Dialogo.Texto("Esperando a que tu columna quede libre…", "campo-ayuda"));
                Botonera(caja, enSuEscena);
                return;
            }
            if (b == null || r == null || b.Estado != EstadoBatalla.BatallaAplicada)
            {
                var calculando = Dialogo.Texto(b == null ? "El motor ya no guarda esta batalla." : "Calculando el resultado…", "informe-titulo");
                calculando.name = "informe-calculando";
                cuerpo.Add(calculando);
                if (b != null) cuerpo.Add(Dialogo.Texto("El servidor de batalla manda el resultado y el motor lo aplica: tus bajas, la experiencia y lo que ganes o pierdas.", "campo-ayuda"));
                Botonera(caja, enSuEscena);
                return;
            }

            bool propioAtaca = b.LadoPropio == LadoId.Atacante;
            bool victoria = r.Ganador == b.LadoPropio;
            var bandidos = BandidosDe(b);
            var rival = bandidos != null ? (bandidos.Nivel > 0 ? $"Bandidos de nivel {bandidos.Nivel}" : "Bandidos") : "Bando rival";
            string que = bandidos != null
                ? (victoria ? "Campamento de bandidos destruido" : "Ataque fallido al campamento de bandidos")
                : propioAtaca ? (victoria ? "Atacaste y ganaste" : "Atacaste y perdiste") : (victoria ? "Te atacaron y resististe" : "Te atacaron y perdiste");
            var titulo = Dialogo.Texto($"{(victoria ? "Victoria" : "Derrota")} · {que}", "informe-titulo " + (victoria ? "informe-titulo--victoria" : "informe-titulo--derrota"));
            titulo.name = "informe-titulo";
            cuerpo.Add(titulo);
            if (Razones.TryGetValue(r.Razon, out var razon))
                cuerpo.Add(Dialogo.Texto($"{(r.Ganador == LadoId.Atacante ? "Gana quien ataca" : "Gana quien defiende")}, {razon}.", "campo-ayuda"));

            // A pantalla completa: tu bando a la izquierda y el rival a la derecha, cada uno con su recuadro (`briefing-bandos`: quién iba y
            // cuántas bajas), sus escuadras y sus héroes; debajo, en una fila, lo que ganas o pierdes.
            var propio = propioAtaca ? b.Ticket.Atacante : b.Ticket.Defensor;
            var otro = propioAtaca ? b.Ticket.Defensor : b.Ticket.Atacante;
            var escuadras = r.PorEscuadra.ToDictionary(e => e.SquadId);
            int Bajas(BattleSide l) => Escuadras(b, l).Sum(s => escuadras.TryGetValue(s.SquadId, out var e) ? (int)e.Muertos : 0);
            var columnas = Dialogo.Caja("informe-columnas");
            cuerpo.Add(columnas);
            var izquierda = Dialogo.Caja("informe-columna");
            var derecha = Dialogo.Caja("informe-columna");
            columnas.Add(izquierda);
            columnas.Add(derecha);
            izquierda.Add(Bando("Tu bando", $"{Bajas(propio)} bajas", Heroes(propio, "sin héroes")));
            derecha.Add(Bando(rival, $"{Bajas(otro)} bajas", otro.Participantes.Count > 0 ? Heroes(otro, "") : bandidos != null ? "los defiende el campamento" : "sin héroes"));
            izquierda.Add(Dialogo.Texto("Tus escuadras", "informe-sub"));
            izquierda.Add(TablaDeBajas(Escuadras(b, propio), escuadras, "informe-propias"));
            derecha.Add(Dialogo.Texto("Escuadras rivales", "informe-sub"));
            derecha.Add(TablaDeBajas(Escuadras(b, otro), escuadras, "informe-rivales"));

            // Los héroes de cada bando (los que se unieron, en el suyo), con si cayeron.
            var ladoPropio = b.LadoPropio;
            var otroLado = ladoPropio == LadoId.Atacante ? LadoId.Defensor : LadoId.Atacante;
            List<BattleParticipantSnapshot> HeroesDe(BattleSide l, LadoId lado) =>
                l.Participantes.Concat(b.Incorporaciones.Where(i => i.Lado == lado).Select(i => i.Participante)).ToList();
            ListaDeHeroes(izquierda, HeroesDe(propio, ladoPropio), r, "informe-heroes");
            ListaDeHeroes(derecha, HeroesDe(otro, otroLado), r, "informe-heroes-rivales");

            // Lo que ganas o pierdes, comparado con lo de antes de la batalla (solo si se tiene).
            var recuadros = Dialogo.Caja("informe-recuadros");
            recuadros.name = "informe-recuadros";
            cuerpo.Add(recuadros);
            var h = HeroeAhora();
            if (h != null && antesDeLaBatalla.TryGetValue(batallaId, out var antes)) LoQueCambia(recuadros, h, antes, victoria);
            else if (h != null && h.HasHeridoHastaMs && h.HeridoHastaMs > c.Sesion.AhoraMundo)
                recuadros.Add(Recuadro("Quedas herido", $"Hasta las {Instante.AFecha(h.HeridoHastaMs):HH:mm} (tiempo del mundo)", "Un herido no ataca ni persigue durante un rato.", false, "informe-herido"));
            Botonera(caja, enSuEscena);
        }

        void ListaDeHeroes(VisualElement donde, List<BattleParticipantSnapshot> heroes, BattleResult r, string nombre)
        {
            if (heroes.Count == 0) return;
            donde.Add(Dialogo.Texto("Héroes", "informe-sub"));
            var lista = Dialogo.Caja("informe-heroes");
            lista.name = nombre;
            foreach (var p in heroes)
            {
                var rh = r.PorHeroe.FirstOrDefault(x => x.HeroeId == p.HeroeId);
                var estado = rh == null || !rh.Participo ? "no llegó a entrar" : rh.SobrevivioAlCierre ? "en pie al acabar" : "cayó";
                var fila = Dialogo.Caja("informe-heroe");
                fila.Add(Dialogo.Texto(NombreDe(p) + (p.HeroeId == c.HeroeId ? " (tú)" : ""), "aviso-titulo"));
                fila.Add(Dialogo.Texto(estado, rh != null && rh.Participo && !rh.SobrevivioAlCierre ? "informe-baja" : "campo-ayuda"));
                lista.Add(fila);
            }
            donde.Add(lista);
        }

        void Botonera(VisualElement caja, bool enSuEscena)
        {
            var botones = Dialogo.Caja("dialogo-botones informe-botones");
            var boton = enSuEscena
                ? Dialogo.Boton("Salir", "boton-primario", () => { CerrarInforme(); _ = VolverAlMundo(); })
                : Dialogo.Boton("Cerrar", "boton-secundario", CerrarInforme);
            boton.name = "informe-salir";
            botones.Add(boton);
            caja.Add(botones);
        }

        string NombreDe(BattleParticipantSnapshot p) => c.Sesion.Almacen.NombresHeroes.TryGetValue(p.HeroeId, out var n) ? n : p.Heroe?.DisplayName ?? p.HeroeId;

        string Heroes(BattleSide l, string ninguno) => l.Participantes.Count == 0 ? ninguno : string.Join(", ", l.Participantes.Select(NombreDe));

        /// <summary>Las escuadras de un bando: las de sus héroes, las sin héroe y las de quien se unió a ese bando, con el dueño de cada una.</summary>
        List<(BronzeAge.Contract.V1.SquadSnapshot escuadra, string dueno)> EscuadrasConDueno(Batalla b, BattleSide l)
        {
            var r = new List<(BronzeAge.Contract.V1.SquadSnapshot, string)>();
            foreach (var p in l.Participantes) foreach (var s in p.Escuadras) r.Add((s, NombreDe(p)));
            foreach (var s in l.EscuadrasSinHeroe) r.Add((s, BandidosDe(b) != null ? "Bandidos" : "Sin héroe"));
            var lado = l == b.Ticket.Atacante ? LadoId.Atacante : LadoId.Defensor;
            foreach (var i in b.Incorporaciones.Where(i => i.Lado == lado)) foreach (var s in i.Participante.Escuadras) r.Add((s, NombreDe(i.Participante)));
            return r;
        }

        List<BronzeAge.Contract.V1.SquadSnapshot> Escuadras(Batalla b, BattleSide l) => EscuadrasConDueno(b, l).Select(x => x.escuadra).ToList();

        static VisualElement Bando(string titulo, string fuerte, string pie)
        {
            var c = Dialogo.Caja("informe-bando");
            c.Add(Dialogo.Texto(titulo.ToUpperInvariant(), "informe-bando-titulo"));
            c.Add(Dialogo.Texto(fuerte, "informe-bando-fuerte"));
            if (!string.IsNullOrEmpty(pie)) c.Add(Dialogo.Texto(pie, "informe-bando-pie"));
            return c;
        }

        /// <summary>La tabla de bajas (`briefing-bajas`): la pieza de tropa con su nombre, su dueño, los que entraron al campo (la escuadra en reserva
        /// que no llegó a salir, 0), los que vuelven y las bajas.</summary>
        VisualElement TablaDeBajas(List<BronzeAge.Contract.V1.SquadSnapshot> lista, Dictionary<string, ResultadoEscuadra> resultado, string nombre)
        {
            var tabla = Dialogo.Caja("informe-tabla");
            tabla.name = nombre;
            if (lista.Count == 0) { tabla.Add(Dialogo.Texto("Sin escuadras.", "campo-ayuda")); return tabla; }
            var cab = Dialogo.Caja("informe-fila informe-fila--cab");
            foreach (var (t, cl) in new[] { ("Escuadra", "informe-col-nombre"), ("Dueño", "informe-col-dueno"), ("Entraron", "informe-col-num"), ("Vuelven", "informe-col-num"), ("Bajas", "informe-col-num") })
                cab.Add(Dialogo.Texto(t, cl));
            tabla.Add(cab);
            foreach (var s in lista)
            {
                resultado.TryGetValue(s.SquadId, out var e);
                var fila = Dialogo.Caja("informe-fila");
                fila.name = "fila-" + s.SquadId;
                var celda = Dialogo.Caja("informe-col-nombre informe-tropa");
                if (PiezasDeTropa.Miniatura(s.TropaId, 26) is VisualElement mini) celda.Add(mini);
                celda.Add(Dialogo.Texto(NombreDeTropa(s.TropaId), "informe-tropa-nombre"));
                fila.Add(celda);
                fila.Add(Dialogo.Texto(DuenoDe(s), "informe-col-dueno"));
                var antes = e?.Desplegados ?? 0;
                var despues = e?.SupervivientesAlCierre ?? 0;
                var muertos = e?.Muertos ?? 0;
                fila.Add(Dialogo.Texto(e == null ? "—" : antes.ToString(), "informe-col-num"));
                fila.Add(Dialogo.Texto(e == null ? "—" : despues.ToString(), "informe-col-num"));
                fila.Add(Dialogo.Texto(e == null ? "—" : muertos > 0 ? $"−{muertos}" : "0", "informe-col-num" + (muertos > 0 ? " informe-baja" : "")));
                tabla.Add(fila);
            }
            return tabla;
        }

        /// <summary>El dueño de una escuadra del ticket (su héroe), o «Bandidos» / «Sin héroe».</summary>
        string DuenoDe(BronzeAge.Contract.V1.SquadSnapshot s)
        {
            if (!s.HasHeroeId) return "—";
            return c.Sesion.Almacen.NombresHeroes.TryGetValue(s.HeroeId, out var n) ? n : s.HeroeId;
        }

        /// <summary>Lo que entra y sale (`briefing-botin`): el carro, las monedas, los objetos nuevos, la experiencia y la herida.</summary>
        void LoQueCambia(VisualElement donde, Heroe h, Antes antes, bool victoria)
        {
            var col = h.Ubicacion?.EjercitoId == null ? null : c.Sesion.Almacen.Buscar(TipoEntidad.Ejercito, h.Ubicacion.EjercitoId)?.Ejercito;
            var ahora = col?.Suministro?.PorId ?? new Google.Protobuf.Collections.MapField<string, long>();
            var gana = new List<string>();
            var pierde = new List<string>();
            foreach (var r in antes.Carro.Keys.Union(ahora.Keys).OrderBy(x => x))
            {
                long d = (ahora.TryGetValue(r, out var n) ? n : 0) - (antes.Carro.TryGetValue(r, out var m) ? m : 0);
                if (d >= 1000) gana.Add($"{d / 1000} de {CosasDelMapa.NombreDeRecurso(r).ToLowerInvariant()}");
                else if (d <= -1000) pierde.Add($"{-d / 1000} {CosasDelMapa.NombreDeRecurso(r).ToLowerInvariant()}");
            }
            long monedas = (long)h.Monedas - (long)antes.Monedas;
            if (monedas > 0) gana.Add($"{monedas} monedas");
            List<JObject> objetos;
            try { objetos = c.Datos.Bloque("Objetos_Del_Heroe", "OBJETOS")["objetos"].OfType<JObject>().ToList(); }
            catch (FaltaDato) { objetos = new List<JObject>(); }
            foreach (var o in h.Inventario.Where(o => !string.IsNullOrEmpty(o.ItemInstanceId) && !antes.Objetos.Contains(o.ItemInstanceId)))
                gana.Add((string)objetos.FirstOrDefault(x => (string)x["id"] == o.ItemDefinitionId)?["nombre"] ?? o.ItemDefinitionId);
            if (gana.Count > 0) donde.Add(Recuadro("Botín", "+" + Enumerar(gana), null, true, "informe-botin"));
            else if (victoria) donde.Add(Recuadro("Botín", "Sin botín", null, true, "informe-botin"));
            if (pierde.Count > 0) donde.Add(Recuadro("Pierdes del carro", Enumerar(pierde), "La mitad de lo que llevabas.", false, "informe-carro"));

            if (h.Nivel > antes.Nivel) donde.Add(Recuadro("Experiencia", $"Subes al nivel {h.Nivel}", null, true, "informe-experiencia"));
            else if (h.Nivel == antes.Nivel && antes.HaciaSiguiente > h.ExperienciaHaciaSiguienteNivel)
                donde.Add(Recuadro("Experiencia", $"+{(antes.HaciaSiguiente - h.ExperienciaHaciaSiguienteNivel) / 1000.0:0.#} de experiencia", null, true, "informe-experiencia"));

            if (h.HasHeridoHastaMs && h.HeridoHastaMs > c.Sesion.AhoraMundo)
                donde.Add(Recuadro("Quedas herido", $"Hasta las {Instante.AFecha(h.HeridoHastaMs):HH:mm} (tiempo del mundo)", "Un herido no ataca ni persigue durante un rato.", false, "informe-herido"));
        }

        static string Enumerar(List<string> xs) => xs.Count <= 1 ? string.Join("", xs) : string.Join(", ", xs.Take(xs.Count - 1)) + " y " + xs.Last();

        static VisualElement Recuadro(string titulo, string fuerte, string pie, bool bueno, string nombre = null)
        {
            var c = Dialogo.Caja("informe-recuadro " + (bueno ? "informe-recuadro--bueno" : "informe-recuadro--malo"));
            c.name = nombre;
            c.Add(Dialogo.Texto(titulo.ToUpperInvariant(), "informe-bando-titulo"));
            c.Add(Dialogo.Texto(fuerte, "informe-recuadro-fuerte"));
            if (pie != null) c.Add(Dialogo.Texto(pie, "informe-bando-pie"));
            return c;
        }
    }
}
