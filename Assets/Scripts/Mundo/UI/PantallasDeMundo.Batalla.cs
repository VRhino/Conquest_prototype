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

        /// <summary>
        /// Desde el mapa (cada vez que se refresca) y, ya en la preparación, desde ella misma: la batalla del héroe. Una batalla activa lleva a la
        /// escena de preparación (que sustituye a la ficha de espera de la 5A); con su asignación se conecta con el servidor de batalla, y
        /// cuando él da la salida, a BattleScene. Si se cancela o se anula antes, la preparación lo dice y «Volver al mapa» vuelve a
        /// MundoScene con la sesión viva.
        /// </summary>
        void VigilarBatalla()
        {
            if (c.Sesion == null || volviendo) return;
            var b = MiBatalla();
            if (b == null || !Activa(b) && b.Id != batallaVista)
            {
                if (enPreparacion) _ = VolverAlMundo();
                else CerrarFichaDeBatalla();
                return;
            }
            if (!enPreparacion)
            {
                if (!Activa(b)) return;
                _ = IrALaPreparacion(b);
                return;
            }
            if (b.Id != batallaVista) EmpezarPreparacion(b);
            batallaVista = b.Id;
            var token = b.AsignacionPropia != null && !string.IsNullOrEmpty(b.AsignacionPropia.Token);
            if (entrando && PreparacionRed.Empezada) { AlEmpezar(); return; }
            if (entrando && !Activa(b)) SalirSinBatalla();
            var firma = $"{b.Id}|{b.Version}|{b.Estado}|{token}|{entrando}|{FirmaDelServidor()}";
            if (firma != firmaBatalla)
            {
                firmaBatalla = firma;
                PintarPreparacion(b);
            }
            PonerPlazo(b);
            if (!entrando && token && (b.Estado == EstadoBatalla.BatallaAsignada || b.Estado == EstadoBatalla.BatallaEnCurso)) EntrarEnBatalla(b);
        }

        /// <summary>En la escena de preparación (o yendo a ella), y volviendo de ella al mundo.</summary>
        bool enPreparacion, volviendo;

        public const string EscenaPreparacion = "PreparacionScene";

        /// <summary>
        /// A la escena de preparación: este objeto (las pantallas y la sesión, que se sigue bombeando) pasa a DontDestroyOnLoad, el mapa se
        /// quita y la pantalla del mundo se aparta. La preparación se pinta encima y se vigila sola.
        /// </summary>
        async System.Threading.Tasks.Task IrALaPreparacion(Batalla b)
        {
            enPreparacion = true;
            Debug.Log($"[Batalla] {b.Id}: a la preparación");
            _ = cerrarEscenaEncima?.Invoke();
            DontDestroyOnLoad(transform.root.gameObject);
            QuitarMapa();
            pantalla.Clear();
            var carga = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(EscenaPreparacion);
            while (carga != null && !carga.isDone) await System.Threading.Tasks.Task.Yield();
            if (this == null) return;
            panelBatalla?.RemoveFromHierarchy();
            panelBatalla = null;
            firmaBatalla = null;
            VigilarBatalla();
        }

        /// <summary>
        /// De la preparación (o de la batalla, en la tanda 2) a MundoScene: si había conexión con el servidor de batalla, se deja; la escena
        /// trae su propio objeto «Mundo», que se aparta (<see cref="Viva"/>), y estas pantallas montan el mapa con la sesión viva, en tu columna.
        /// </summary>
        public async System.Threading.Tasks.Task VolverAlMundo()
        {
            if (volviendo) return;
            volviendo = true;
            Debug.Log("[Batalla] de vuelta al mundo");
            if (entrando) SalirSinBatalla();
            CerrarFichaDeBatalla();
            CerrarInforme();
            capaDeBatalla?.RemoveFromHierarchy();
            capaDeBatalla = null;
            BatallaEnCurso = null;
            var carga = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(EscenaMundo);
            while (carga != null && !carga.isDone) await System.Threading.Tasks.Task.Yield();
            if (this == null) return;
            var escena = UnityEngine.SceneManagement.SceneManager.GetSceneByName(EscenaMundo);
            if (escena.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(escena);
            enPreparacion = false;
            volviendo = false;
            pantalla.parent.style.display = DisplayStyle.Flex;
            pantalla.style.display = DisplayStyle.Flex;
            Mapa();
        }

        void CerrarFichaDeBatalla()
        {
            mapaDeLaBatalla?.Quitar();
            mapaDeLaBatalla = null;
            panelBatalla?.RemoveFromHierarchy();
            panelBatalla = null;
            batallaVista = null;
            firmaBatalla = null;
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
                // Con servidor de batalla, cómo está cada uno en la preparación.
                var enPrep = PreparacionRed.Heroes.FirstOrDefault(x => x.heroe == p.HeroeId);
                var como = !PreparacionRed.Conectado || enPrep.heroe == null ? "" : enPrep.estado == PreparacionRed.ConListo ? " · listo" : enPrep.estado == PreparacionRed.ConectadoSinListo ? " · preparándose" : " · sin conectar";
                var fila = Dialogo.Texto((p.HeroeId == c.HeroeId ? $"{nombre} (tú)" : nombre) + como, "aviso-titulo");
                fila.name = "heroe-" + p.HeroeId;
                donde.Add(fila);
                foreach (var s in p.Escuadras) donde.Add(FilaDeTropa(s));
            }
            foreach (var s in lado.EscuadrasSinHeroe) donde.Add(FilaDeTropa(s));
        }

        VisualElement FilaDeTropa(BronzeAge.Contract.V1.SquadSnapshot s)
        {
            var fila = Dialogo.Caja("con-icono");
            fila.name = "batalla-tropa-" + s.SquadId;
            fila.style.marginBottom = 4;
            if (PiezasDeTropa.Miniatura(s.TropaId) is VisualElement mini) fila.Add(mini);
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
                case EstadoBatalla.BatallaEnCurso:
                    if (!token) return b.Estado == EstadoBatalla.BatallaEnCurso ? "La batalla está en curso." : "Hay servidor de batalla: preparando tu entrada. Si no empieza a tiempo, se anula sin castigo.";
                    if (!PreparacionRed.Conectado) return "Conectando con el servidor de batalla…";
                    return PreparacionRed.Listo ? "Listo: la batalla empieza en cuanto lo estén todos o se acabe la preparación."
                        : "Elige el orden y la formación y pulsa «Listo». Si se acaba la preparación, entras con lo elegido.";
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
            // La preparación del servidor de batalla: lo que le queda (empieza antes si todos están listos).
            var queda = panelBatalla.Q<Label>("prep-queda");
            if (queda != null) queda.text = entrando && PreparacionRed.Queda >= 0 ? $"{Mathf.CeilToInt(PreparacionRed.Queda) / 60:00}:{Mathf.CeilToInt(PreparacionRed.Queda) % 60:00}" : "";
        }

        /// <summary>
        /// Conecta con el servidor de batalla con la asignación del motor, desde la escena de preparación. La sesión sigue viva
        /// (DontDestroyOnLoad). Lo elegido ya va al servidor (<see cref="PreparacionRed"/>); la batalla empieza cuando él lo dice.
        /// </summary>
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
            Debug.Log($"[Batalla] conectando con {b.Id}: {a.Instancia.Host}:{a.Instancia.Puerto} ({a.Instancia.Protocolo})");
            DontDestroyOnLoad(transform.root.gameObject);
            EntradaEnBatalla.Entrar(a.Instancia.Host, (ushort)a.Instancia.Puerto, a.Token);
            LoElegidoAlServidor();
        }

        /// <summary>El servidor da la salida: se aparta la interfaz y el mapa, y la red carga BattleScene (EspejoClienteSystem).</summary>
        void AlEmpezar()
        {
            BatallaEnCurso = batallaVista;
            Debug.Log($"[Batalla] empieza {batallaVista}: a BattleScene");
            // Encima de BattleScene solo queda la capa de arriba, con «Abandonar la batalla».
            pantalla.style.display = DisplayStyle.None;
            AlEntrarEnLaBatalla(batallaVista);
            CerrarFichaDeBatalla();
            entrando = false;
            enPreparacion = false;
        }

        /// <summary>La batalla se anuló o se canceló estando ya conectado: se deja el servidor de batalla y se vuelve al mundo de siempre.</summary>
        void SalirSinBatalla()
        {
            Debug.Log($"[Batalla] {batallaVista} no empezó: se deja el servidor de batalla");
            entrando = false;
            EntradaEnBatalla.Salir();
            firmaBatalla = null;
        }
    }

    /// <summary>El arranque del cliente de batalla de Conquest desde el mundo, por sus puntos de entrada (`OpcionesRed`, `ArranqueRed`): los
    /// mismos que con <c>-red cliente -ip -puerto -token -batallaReal 1</c>, pero sin salir del proceso. Los mundos ECS de antes se quitan y
    /// se crea el de cliente (como `BatallaRealServidorTests`); `EspejoClienteSystem` carga BattleScene.</summary>
    public static class EntradaEnBatalla
    {
        /// <summary>Conecta con un servidor de batalla. Lo estático de la batalla anterior se pone como al arrancar.</summary>
        public static void Entrar(string ip, ushort puerto, string token)
        {
            QuitarMundos();
            Reiniciar();
            PreparacionRed.ConPantalla = true;
            OpcionesRed.ModoPedido = OpcionesRed.Modo.Cliente;
            OpcionesRed.Ip = ip;
            OpcionesRed.Puerto = puerto;
            OpcionesRed.Token = token;
            OpcionesRed.BatallaReal = 1;
            DefaultWorldInitialization.Initialize("Batalla");
        }

        /// <summary>Deja el servidor de batalla sin haber jugado: se quita el mundo de cliente y vuelve el mundo ECS de siempre.</summary>
        public static void Salir()
        {
            QuitarMundos();
            Reiniciar();
            OpcionesRed.ModoPedido = OpcionesRed.Modo.Ninguno;
            OpcionesRed.Token = null;
            OpcionesRed.BatallaReal = 0;
            DefaultWorldInitialization.Initialize("Default World");
        }

        /// <summary>Lo estático que deja una batalla (Docs/Arquitectura/28, «Lo estático entre batallas»).</summary>
        static void Reiniciar()
        {
            PreparacionRed.Reiniciar();
            ReconexionClienteSystem.Reiniciar();
            BattleBootstrapRequests.NoLocalHero = false;
            BattleBootstrapRequests.NetworkTeam = 0;
            BattleTransitionData.Instance.GetAndClearBattleData();
        }

        static void QuitarMundos()
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
        }
    }
}
