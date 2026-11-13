// Del mapa a la batalla (paso 5A del bloque 4). Con las batallas de Unity encendidas en el motor, «Atacar» un campamento de bandidos abre
// una `Batalla` en el ámbito BATALLA. Aquí la ficha de esa batalla (BronzeAgeClient: `ui/panelBatalla.ts`), con quién contra quién y su
// tropa, el estado y el plazo, y «Cancelar la batalla» mientras se pueda. Cuando llega `asignacion_propia` (servidor y token de tu héroe),
// se entra en el servidor de batalla de Conquest.
//
// La sesión con el motor sigue viva durante la batalla: este objeto (las pantallas y `ConexionDeMundo`, que la bombea cada fotograma) pasa a
// DontDestroyOnLoad antes de que la red cargue BattleScene en modo Single. Así siguen llegando el estado de la batalla y su resultado.
// El ciclo, los plazos y la desconexión están en Docs/Mechanics/Batalla_Unity_Ciclo.md del motor.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Net;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed partial class PantallasDeMundo
    {
        /// <summary>La batalla en la que ha entrado el héroe; null en el mundo. La vuelta (paso 5B) parte de aquí.</summary>
        public static string BatallaEnCurso { get; private set; }

        /// <summary>Los estados como los dice el prototipo (`panelBatalla.ts`).</summary>
        static readonly Dictionary<EstadoBatalla, string> EstadoDeBatalla = new Dictionary<EstadoBatalla, string>
        {
            [EstadoBatalla.BatallaConvocando] = "convocándose", [EstadoBatalla.BatallaAsignada] = "a punto de empezar",
            [EstadoBatalla.BatallaEnCurso] = "en curso", [EstadoBatalla.BatallaAplicada] = "terminada",
            [EstadoBatalla.BatallaCancelada] = "cancelada", [EstadoBatalla.BatallaFallida] = "fallida",
        };

        static bool Activa(Batalla b) => b.Estado == EstadoBatalla.BatallaConvocando || b.Estado == EstadoBatalla.BatallaAsignada || b.Estado == EstadoBatalla.BatallaEnCurso;

        VisualElement panelBatalla;
        /// <summary>La batalla de la ficha, y su versión y estado ya pintados.</summary>
        string batallaVista;
        string firmaBatalla;
        bool entrando;

        /// <summary>La batalla del héroe: la de su columna (`en_batalla`) o, si no, la última en la que tiene lado.</summary>
        Batalla MiBatalla()
        {
            var todas = c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Batalla).Select(e => e.Batalla).ToList();
            var col = mapa?.Columna;
            if (col != null && col.HasBatallaId && todas.FirstOrDefault(x => x.Id == col.BatallaId) is Batalla deColumna) return deColumna;
            return todas.Where(x => x.HasLadoPropio && (Activa(x) || x.Id == batallaVista)).OrderByDescending(x => x.Version).FirstOrDefault();
        }

        /// <summary>Cada vez que se refresca el mapa: la ficha de la batalla del héroe y, con su asignación, la entrada.</summary>
        void VigilarBatalla()
        {
            if (entrando || c.Sesion == null) return;
            var b = MiBatalla();
            if (b == null || !Activa(b) && b.Id != batallaVista) { CerrarFichaDeBatalla(); return; }
            batallaVista = b.Id;
            var token = b.AsignacionPropia != null && !string.IsNullOrEmpty(b.AsignacionPropia.Token);
            var firma = $"{b.Id}|{b.Version}|{b.Estado}|{token}";
            if (firma != firmaBatalla)
            {
                firmaBatalla = firma;
                PintarFichaDeBatalla(b);
            }
            PonerPlazo(b);
            if (token && (b.Estado == EstadoBatalla.BatallaAsignada || b.Estado == EstadoBatalla.BatallaEnCurso)) EntrarEnBatalla(b);
        }

        void CerrarFichaDeBatalla()
        {
            panelBatalla?.RemoveFromHierarchy();
            panelBatalla = null;
            batallaVista = null;
            firmaBatalla = null;
        }

        /// <summary>La ficha de la batalla (`panelBatalla.ts`): «Evento: campamento de bandidos · estado», «Contra bandidos», quién ataca y quién
        /// defiende con su tropa, qué pasa ahora con su plazo y, si la iniciaste y aún no ha empezado, «Cancelar la batalla».</summary>
        void PintarFichaDeBatalla(Batalla b)
        {
            if (panelBatalla == null)
            {
                panelBatalla = Dialogo.Caja("dialogo-velo");
                panelBatalla.name = "panel-batalla";
                capaEncima.Add(panelBatalla);
            }
            panelBatalla.Clear();
            var placa = Dialogo.Caja("placa dialogo panel-batalla");
            var filete = Dialogo.Caja("placa-filete");
            placa.Add(filete);
            panelBatalla.Add(placa);

            var bandidos = BandidosDe(b);
            var estado = EstadoDeBatalla.TryGetValue(b.Estado, out var e) ? e : b.Estado.ToString();
            var kicker = Dialogo.Texto($"{(bandidos != null ? "EVENTO: CAMPAMENTO DE BANDIDOS" : "BATALLA")} · {estado.ToUpperInvariant()}", "kicker");
            kicker.name = "batalla-estado";
            filete.Add(kicker);
            var titulo = IconosDelMundo.ConIcono("glifo", "atacar",
                Dialogo.Texto(bandidos != null ? $"Contra bandidos{(bandidos.Nivel > 0 ? $" · nivel {bandidos.Nivel}" : "")}" : "Batalla", "dialogo-titulo"), 26);
            titulo.AddToClassList("titulo-con-icono");
            filete.Add(titulo);

            var cuerpo = new ScrollView { mode = ScrollViewMode.Vertical, horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            cuerpo.style.flexShrink = 1;
            filete.Add(cuerpo);
            var t = b.Ticket;
            Bando(cuerpo, "ATACAN", t?.Atacante, b.LadoPropio == LadoId.Atacante);
            Bando(cuerpo, "DEFIENDEN", t?.Defensor, b.LadoPropio == LadoId.Defensor);
            if (t == null) cuerpo.Add(Dialogo.Texto("Aún no ha llegado quién combate.", "campo-ayuda"));

            var ahora = Dialogo.Texto(QuePasa(b), "aviso-texto batalla-ahora");
            ahora.name = "batalla-ahora";
            filete.Add(ahora);
            var plazo = Dialogo.Texto("", "campo-ayuda");
            plazo.name = "batalla-plazo";
            filete.Add(plazo);
            var error = Dialogo.Texto("", "error oculto");
            error.name = "error";
            filete.Add(error);

            var botones = Dialogo.Caja("dialogo-botones");
            filete.Add(botones);
            if (Activa(b))
            {
                // Solo quien la inició, y antes de que empiece (`batalla.no_puede_cancelar`).
                var h = HeroeAhora();
                if (h != null && b.IniciadaPor == h.Id && (b.Estado == EstadoBatalla.BatallaConvocando || b.Estado == EstadoBatalla.BatallaAsignada))
                {
                    Button cancelar = null;
                    cancelar = Dialogo.Boton("Cancelar la batalla", "boton-secundario", async () =>
                    {
                        cancelar.SetEnabled(false);
                        Poner(error, null);
                        try
                        {
                            var r = await c.Sesion.Ordenar(new Orden { CancelarBatalla = new OrdenCancelarBatalla { BattleId = b.Id } });
                            if (r.Resultado != Resultado.OrdenAceptada) Poner(error, Textos.Explicar(r.Rechazo));
                        }
                        catch (ErrorDeSesion x) { Poner(error, x.Message); }
                        if (cancelar.panel != null) cancelar.SetEnabled(true);
                    });
                    cancelar.name = "batalla-cancelar";
                    botones.Add(cancelar);
                }
            }
            else
            {
                var cerrar = Dialogo.Boton("Cerrar", "boton-secundario", CerrarFichaDeBatalla);
                cerrar.name = "batalla-cerrar";
                botones.Add(cerrar);
            }
            PonerPlazo(b);
        }

        /// <summary>Un bando: «Atacan» o «Defienden», de quién (la Facción, «Bandidos» sin ella) con los héroes y el tope, y su tropa con el
        /// dibujo de cada una (la del ticket, la que entra en la batalla).</summary>
        void Bando(VisualElement donde, string titulo, BattleSide lado, bool propio)
        {
            if (lado == null) return;
            // Sin Facción: los bandidos (sin héroes) o héroes que aún no tienen Facción.
            var quien = lado.HasFaccionId ? NombreDeFaccion(c.Sesion.Almacen, lado.FaccionId) : lado.Participantes.Count > 0 ? "sin Facción" : "Bandidos";
            var cabeza = Dialogo.Texto($"{titulo} · {quien}{(lado.CapacidadMaxima > 0 ? $" · {lado.Participantes.Count}/{lado.CapacidadMaxima} héroes" : "")}{(propio ? " · tu bando" : "")}", "kicker");
            donde.Add(cabeza);
            foreach (var p in lado.Participantes)
            {
                var nombre = c.Sesion.Almacen.NombresHeroes.TryGetValue(p.HeroeId, out var n) ? n : p.Heroe?.DisplayName ?? p.HeroeId;
                donde.Add(Dialogo.Texto(p.HeroeId == c.HeroeId ? $"{nombre} (tú)" : nombre, "aviso-titulo"));
                foreach (var s in p.Escuadras) donde.Add(FilaDeTropa(s));
            }
            foreach (var s in lado.EscuadrasSinHeroe) donde.Add(FilaDeTropa(s));
        }

        VisualElement FilaDeTropa(SquadSnapshot s)
        {
            var fila = Dialogo.Caja("con-icono");
            fila.name = "batalla-tropa-" + s.SquadId;
            fila.style.marginBottom = 4;
            fila.Add(IconosDelMundo.CajaDeTropa(s.TropaId));
            fila.Add(Dialogo.Texto($"{NombreDeTropa(s.TropaId)} · {s.EfectivosAutorizados} hombres", "aviso-texto"));
            return fila;
        }

        /// <summary>El campamento de bandidos de la batalla (por su contexto), o null si no es contra bandidos.</summary>
        CampamentoBandidos BandidosDe(Batalla b)
        {
            var id = b.Ticket?.ContextoEstrategico?.CampamentoBandidos?.CampamentoId ?? (b.HasBloqueoCampamentoId ? b.BloqueoCampamentoId : null);
            if (id == null) return null;
            return c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.CampamentoBandidos).Select(e => e.CampamentoBandidos).FirstOrDefault(x => x.Id == id)
                ?? new CampamentoBandidos { Id = id };
        }

        /// <summary>Qué pasa ahora, dicho para el jugador. Los plazos son del motor (Batalla_Unity_Ciclo: 5 min hasta asignarse, 5 más hasta
        /// empezar); si vencen, la batalla queda fallida sin castigo.</summary>
        static string QuePasa(Batalla b)
        {
            bool token = b.AsignacionPropia != null && !string.IsNullOrEmpty(b.AsignacionPropia.Token);
            switch (b.Estado)
            {
                case EstadoBatalla.BatallaConvocando:
                    return "Esperando un servidor de batalla. Si no llega ninguno a tiempo, la batalla se anula sin castigo y tu columna queda libre.";
                case EstadoBatalla.BatallaAsignada:
                    return token ? "Entrando en la batalla…" : "Hay servidor de batalla: preparando tu entrada. Si no empieza a tiempo, se anula sin castigo.";
                case EstadoBatalla.BatallaEnCurso:
                    return token ? "Entrando en la batalla…" : "La batalla está en curso.";
                case EstadoBatalla.BatallaCancelada:
                    return "Batalla cancelada: tu columna queda libre.";
                case EstadoBatalla.BatallaFallida:
                    return "No llegó a empezar a tiempo: la batalla se anula sin castigo y tu columna queda libre.";
                case EstadoBatalla.BatallaAplicada:
                    return "La batalla terminó.";
                default:
                    return "";
            }
        }

        /// <summary>El plazo que corre (en tiempo de mundo), mientras la batalla está activa.</summary>
        void PonerPlazo(Batalla b)
        {
            var plazo = panelBatalla?.Q<Label>("batalla-plazo");
            if (plazo == null) return;
            if (!Activa(b) || b.ExpiraEnMs <= 0) { plazo.text = ""; return; }
            var ms = Math.Max(0, b.ExpiraEnMs - c.Sesion.AhoraMundo);
            var que = b.Estado == EstadoBatalla.BatallaConvocando ? "para que llegue un servidor" : b.Estado == EstadoBatalla.BatallaAsignada ? "para que empiece" : "de batalla";
            plazo.text = $"Quedan {(int)(ms / 60000)}:{(int)(ms / 1000) % 60:00} {que} (tiempo del mundo).";
        }

        /// <summary>Entra en el servidor de batalla con la asignación del motor. La sesión sigue viva (DontDestroyOnLoad); la interfaz y el mapa
        /// se apartan, y la red de Conquest monta su mundo de cliente y carga BattleScene.</summary>
        void EntrarEnBatalla(Batalla b)
        {
            var a = b.AsignacionPropia;
            if (!System.Net.IPAddress.TryParse(a.Instancia?.Host ?? "", out _))
            {
                // La red de Conquest conecta a una IP (`NetworkEndpoint.Parse`); un nombre de máquina no vale.
                var ahora = panelBatalla?.Q<Label>("batalla-ahora");
                if (ahora != null) ahora.text = $"El servidor de batalla dio una dirección que no es una IP ({a.Instancia?.Host}): no se puede entrar.";
                return;
            }
            entrando = true;
            BatallaEnCurso = b.Id;
            Debug.Log($"[Batalla] entrando en {b.Id}: {a.Instancia.Host}:{a.Instancia.Puerto} ({a.Instancia.Protocolo})");
            DontDestroyOnLoad(transform.root.gameObject);
            pantalla.parent.style.display = DisplayStyle.None;
            CerrarFichaDeBatalla();
            QuitarMapa();
            EntradaEnBatalla.Entrar(a.Instancia.Host, (ushort)a.Instancia.Puerto, a.Token);
        }
    }

    /// <summary>El arranque del cliente de batalla de Conquest desde el mundo, por sus puntos de entrada (`OpcionesRed`, `ArranqueRed`): los
    /// mismos que con <c>-red cliente -ip -puerto -token -batallaReal 1</c>, pero sin salir del proceso. Los mundos ECS de antes se quitan y
    /// se crea el de cliente (como `BatallaRealServidorTests`); `EspejoClienteSystem` carga BattleScene.</summary>
    public static class EntradaEnBatalla
    {
        public static void Entrar(string ip, ushort puerto, string token)
        {
            // World.All no se deja enumerar con LINQ (para no copiar); se copia a mano antes de quitar.
            var mundos = new List<World>();
            foreach (var w in World.All) mundos.Add(w);
            foreach (var w in mundos)
            {
                if (!w.IsCreated) continue;
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(w);
                w.Dispose();
            }
            World.DefaultGameObjectInjectionWorld = null;
            OpcionesRed.ModoPedido = OpcionesRed.Modo.Cliente;
            OpcionesRed.Ip = ip;
            OpcionesRed.Puerto = puerto;
            OpcionesRed.Token = token;
            OpcionesRed.BatallaReal = 1;
            DefaultWorldInitialization.Initialize("Batalla");
        }
    }
}
