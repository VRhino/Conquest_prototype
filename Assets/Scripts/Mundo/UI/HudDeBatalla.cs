// El HUD de la batalla en «Bronce y arcilla» (pedido del autor, 2026-11-27, opción B): UI Toolkit con los estilos de fuera (MundoUI/Mundo.uss)
// y los suyos (HudBatalla/HudBatalla.uss), con la placa de bronce batido de los carteles del mapa y la tarjeta de tropa del juego
// (`PiezasDeTropa.Tarjeta`, la misma de la preparación y el campamento). Sustituye a las secciones uGUI del héroe, las habilidades, la escuadra
// y el tiempo de la escena, que se apagan al cargar.
// - El tiempo, arriba en el centro (`BattleClockComponent`, el reloj de la batalla en local y en red).
// - El héroe, abajo en el centro: vida y aguante con sus cifras y, debajo, Q, E, R y la definitiva T (con su combo), y las ventajas 4 y 5.
// - La escuadra, abajo a la izquierda, solo si el héroe lleva tropa: su tarjeta (la imagen entera, con «vivos/total» en la franja), y su ficha:
//   nombre, rareza, vivos, la vida de cada soldado (en local; en red el servidor no la manda), las formaciones con su tecla (la activa, en la
//   placa de oro) y sus habilidades 1-3 (con el modo puntero, un clic las lanza).
// Solo lee: `UIHeroBattleDataComponent` (lo escriben UIBattleDataSystem o, en red, el espejo), el contenedor de la batalla y, en red, los
// soldados propios. Los rombos de las zonas no salen en las batallas con el campo del lugar, que no tienen zonas (autor, 2026-11-27).
using System.Collections.Generic;
using System.Reflection;
using ConquestTactics.Net;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public class HudDeBatalla : MonoBehaviour
    {
        static readonly string[] TeclasHeroe = { "Q", "E", "R", "T", "4", "5" };
        const float AnchoTarjeta = 112f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Enganchar()
        {
            SceneManager.sceneLoaded -= AlCargar;
            SceneManager.sceneLoaded += AlCargar;
        }

        static void AlCargar(Scene escena, LoadSceneMode modo)
        {
            if (escena.name != SceneTransitionService.SceneNames.Battle || Application.isBatchMode) return;
            var go = new GameObject("HudDeBatalla");
            SceneManager.MoveGameObjectToScene(go, escena);
            go.AddComponent<HudDeBatalla>();
        }

        sealed class Hueco
        {
            public VisualElement Raiz, Icono, Espera;
            public Label Segundos, Combo;
            public int Mostrada = -1;
        }

        EntityManager em;
        EntityQuery datosQ, contenedorQ, relojQ, escuadraQ;
        readonly SkillIconCatalog iconos = new SkillIconCatalog();
        bool iconosDeEscuadras, viejoApagado;
        VisualElement raizHud, tiempo, vida, aguante, escuadra, ficha, tarjetaHueco, barraEscuadra, soldados, formaciones, habilidades, habilidadesKicker;
        Label vidaTexto, aguanteTexto, nombre, efectivos, tiempoTexto;
        VisualElement estrellasHueco;
        readonly List<Hueco> heroe = new List<Hueco>(), deEscuadra = new List<Hueco>();
        readonly List<(VisualElement raiz, FormationType tipo)> iconosFormacion = new List<(VisualElement, FormationType)>();
        readonly List<(VisualElement raiz, VisualElement vida)> pips = new List<(VisualElement, VisualElement)>();
        string escuadraMostrada;
        readonly Dictionary<string, int> maximoVisto = new Dictionary<string, int>();
        int vivosEnTarjeta = -1, totalEnTarjeta = -1, segundosMostrados = -1;
        UIHeroBattleDataComponent datos;

        void Start()
        {
            em = World.DefaultGameObjectInjectionWorld.EntityManager;
            datosQ = em.CreateEntityQuery(ComponentType.ReadOnly<UIHeroBattleDataComponent>());
            contenedorQ = em.CreateEntityQuery(ComponentType.ReadOnly<DataContainerComponent>());
            relojQ = em.CreateEntityQuery(ComponentType.ReadOnly<BattleClockComponent>());
            escuadraQ = em.CreateEntityQuery(ComponentType.ReadOnly<SquadStatusComponent>(), ComponentType.ReadOnly<IsLocalSquadActive>());
            var clase = PlayerSessionService.SelectedHero != null ? HeroClassManager.GetClassDefinition(PlayerSessionService.SelectedHero.classId) : null;
            if (clase != null) iconos.AddClass(clase);

            // (el asset, no uno creado aquí: uno creado en tiempo de ejecución no lleva los datos ICU del texto y en un ejecutable no pinta letras)
            var ajustes = Instantiate(Resources.Load<PanelSettings>("HudBatalla/PanelDeBatalla"));
            var hijo = new GameObject("Interfaz");
            hijo.SetActive(false);
            hijo.transform.SetParent(transform, false);
            var doc = hijo.AddComponent<UIDocument>();
            doc.panelSettings = ajustes;
            hijo.SetActive(true);
            var raiz = doc.rootVisualElement;
            raiz.pickingMode = PickingMode.Ignore;
            raiz.styleSheets.Add(Resources.Load<StyleSheet>("MundoUI/Mundo"));
            raiz.styleSheets.Add(Resources.Load<StyleSheet>("HudBatalla/HudBatalla"));
            raizHud = Caja(raiz, "hud");

            tiempo = Caja(raizHud, "hud-tiempo", "hud-placa");
            tiempoTexto = Texto(tiempo, "", "hud-tiempo-texto", "hud-cinzel");

            var h = Caja(raizHud, "hud-heroe", "hud-placa");
            vida = Barra(h, "hud-vida", out vidaTexto);
            aguante = Barra(h, "hud-aguante", out aguanteTexto);
            var fila = Caja(h, "hud-fila");
            for (int i = 0; i < TeclasHeroe.Length; i++)
            {
                if (i == 4) Caja(fila, "hud-separa");
                var hueco = NuevoHueco(fila, TeclasHeroe[i], i == 3);
                if (i >= 4) hueco.Raiz.AddToClassList("hud-perk");
                heroe.Add(hueco);
            }

            escuadra = Caja(raizHud, "hud-escuadra");
            tarjetaHueco = Caja(escuadra, "hud-tarjeta-hueco");
            ficha = Caja(escuadra, "hud-ficha", "hud-placa");
            var cab = Caja(ficha, "hud-escuadra-cab");
            nombre = Texto(cab, "", "hud-escuadra-nombre", "hud-cinzel");
            estrellasHueco = Caja(cab, "hud-estrellas");
            efectivos = Texto(ficha, "", "hud-escuadra-efectivos", "hud-inter");
            barraEscuadra = Caja(Caja(ficha, "hud-escuadra-barra"), "hud-barra-relleno");
            soldados = Caja(ficha, "hud-soldados");
            Texto(ficha, "FORMACIÓN", "hud-kicker", "hud-cinzel");
            formaciones = Caja(ficha, "hud-fila");
            habilidadesKicker = Texto(ficha, "HABILIDADES", "hud-kicker", "hud-cinzel");
            habilidades = Caja(ficha, "hud-fila");
            for (int i = 0; i < 3; i++)
            {
                var hueco = NuevoHueco(habilidades, (i + 1).ToString(), false);
                var ranura = (SkillSlot)((int)SkillSlot.Squad1 + i);
                hueco.Raiz.RegisterCallback<ClickEvent>(_ => BattleCommandInput.TryRequestSquadSkill(ranura, datos.Skill(ranura)));
                deEscuadra.Add(hueco);
            }
            escuadra.style.display = DisplayStyle.None;
            tiempo.style.display = DisplayStyle.None;
        }

        void OnDestroy()
        {
            if (World.DefaultGameObjectInjectionWorld == null || !World.DefaultGameObjectInjectionWorld.IsCreated) return;
            datosQ.Dispose();
            contenedorQ.Dispose();
            relojQ.Dispose();
            escuadraQ.Dispose();
        }

        static VisualElement Caja(VisualElement padre, params string[] clases)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (var c in clases) e.AddToClassList(c);
            padre.Add(e);
            return e;
        }

        static Label Texto(VisualElement padre, string texto, params string[] clases)
        {
            var l = new Label(texto) { pickingMode = PickingMode.Ignore };
            foreach (var c in clases) l.AddToClassList(c);
            padre.Add(l);
            return l;
        }

        static VisualElement Barra(VisualElement padre, string clase, out Label texto)
        {
            var b = Caja(padre, "hud-barra", clase);
            var relleno = Caja(b, "hud-barra-relleno");
            Caja(b, "hud-barra-brillo");
            // marcas cada quinto, como las de la barra de antes
            for (int i = 1; i < 5; i++) Caja(b, "hud-barra-marca").style.left = Length.Percent(i * 20);
            texto = Texto(b, "", "hud-barra-texto", "hud-inter");
            return relleno;
        }

        static Hueco NuevoHueco(VisualElement padre, string tecla, bool conCombo)
        {
            var r = Caja(padre, "hud-hueco", "hud-placa");
            var hueco = new Hueco { Raiz = r, Icono = Caja(r, "hud-hueco-icono"), Espera = Caja(r, "hud-hueco-espera") };
            hueco.Segundos = Texto(r, "", "hud-hueco-segundos", "hud-inter");
            Texto(r, tecla, "hud-hueco-tecla", "hud-inter");
            if (conCombo) hueco.Combo = Texto(r, "", "hud-hueco-combo", "hud-inter");
            r.pickingMode = PickingMode.Position;
            return hueco;
        }

        // Las secciones viejas de la escena (uGUI), fuera, una vez: el héroe, las habilidades, la escuadra y el tiempo; y los rombos, en una batalla con
        // el campo del lugar. La barra de captura se queda: es también la de canalizar (el cambio de escuadra en el suministro).
        void ApagarLoViejo()
        {
            if (viejoApagado) return;
            var hud = FindFirstObjectByType<HUDController>();
            if (hud == null) return;
            viejoApagado = true;
            var campos = new List<string> { "_heroSection", "_abilitiesSection", "_squadSection" };
            if (MontajeDelCampo.Actual != null) campos.Add("_capturePointIconsContainer");
            foreach (var nombreCampo in campos)
            {
                var valor = typeof(HUDController).GetField(nombreCampo, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(hud);
                var go = valor is Transform t ? t.gameObject : valor as GameObject;
                if (go != null) go.SetActive(false);
            }
            var ficha = FindFirstObjectByType<SquadSectionController>(FindObjectsInactive.Include);
            if (ficha != null) ficha.gameObject.SetActive(false);
            var barra = FindFirstObjectByType<SkillBarController>();
            if (barra != null) barra.gameObject.SetActive(false);
            var reloj = GameObject.Find("Timer");
            if (reloj != null) reloj.SetActive(false);
        }

        void Update()
        {
            if (raizHud == null) return;
            ApagarLoViejo();
            Tiempo();
            if (datosQ.IsEmptyIgnoreFilter) { raizHud.style.display = DisplayStyle.None; return; }
            raizHud.style.display = DisplayStyle.Flex;
            datos = em.GetComponentData<UIHeroBattleDataComponent>(datosQ.GetSingletonEntity());

            vida.style.width = Length.Percent(datos.maxHealth > 0 ? 100f * Mathf.Clamp01(datos.currentHealth / datos.maxHealth) : 0);
            aguante.style.width = Length.Percent(datos.maxStamina > 0 ? 100f * Mathf.Clamp01(datos.currentStamina / datos.maxStamina) : 0);
            vidaTexto.text = $"{Mathf.CeilToInt(datos.currentHealth)} / {Mathf.CeilToInt(datos.maxHealth)}";
            aguanteTexto.text = $"{Mathf.CeilToInt(datos.currentStamina)} / {Mathf.CeilToInt(datos.maxStamina)}";
            for (int i = 0; i < heroe.Count; i++)
            {
                var ranura = (SkillSlot)((int)SkillSlot.Q + i);
                Pintar(heroe[i], datos.Skill(ranura), datos.CanAfford(ranura));
            }
            var definitiva = heroe[3].Combo;
            definitiva.style.display = datos.ultimateComboCost > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            definitiva.text = $"{Mathf.FloorToInt(datos.combo)}/{Mathf.CeilToInt(datos.ultimateComboCost)}";
            Escuadra();
        }

        void Tiempo()
        {
            if (relojQ.IsEmptyIgnoreFilter) { tiempo.style.display = DisplayStyle.None; return; }
            var reloj = relojQ.GetSingleton<BattleClockComponent>();
            int s = Mathf.Max(0, Mathf.CeilToInt(reloj.remainingSeconds));
            tiempo.style.display = DisplayStyle.Flex;
            if (s == segundosMostrados) return;
            segundosMostrados = s;
            tiempoTexto.text = $"{s / 60:00}:{s % 60:00}";
            tiempo.EnableInClassList("hud-tiempo-ultimos", s <= 60);
        }

        void Pintar(Hueco h, UISkillSlotData s, bool alcanza)
        {
            if (s.skillId != 0 && !iconos.Contains(s.skillId)) CargarIconosDeEscuadras();
            if (h.Mostrada != s.skillId)
            {
                h.Mostrada = s.skillId;
                var icono = iconos.Find(s.skillId);
                h.Icono.style.backgroundImage = icono != null ? new StyleBackground(icono) : new StyleBackground(StyleKeyword.None);
                h.Raiz.tooltip = iconos.FindName(s.skillId) ?? "";
            }
            h.Raiz.EnableInClassList("hud-hueco-vacio", s.skillId == 0);
            h.Raiz.EnableInClassList("hud-hueco-sin-coste", !alcanza);
            h.Espera.style.height = Length.Percent(s.skillId != 0 ? Mathf.Clamp01(s.cooldown) * 100f : 0);
            h.Segundos.text = s.skillId != 0 && s.remaining > 0.05f ? (s.remaining >= 10 ? Mathf.CeilToInt(s.remaining).ToString() : s.remaining.ToString("0.0")) : "";
        }

        void CargarIconosDeEscuadras()
        {
            if (iconosDeEscuadras) return;
            iconosDeEscuadras = true;
            var todas = SquadDataService.GetAllSquads();
            if (todas != null) foreach (var s in todas) iconos.AddSquad(s);
        }

        // La tropa del héroe: la del contenedor de la batalla (o la del último cambio), si existe en la base de escuadras.
        string TropaActual()
        {
            if (datos.squadChangedThisFrame && !datos.newSquadBaseID.IsEmpty) return datos.newSquadBaseID.ToString();
            return contenedorQ.IsEmptyIgnoreFilter ? null : em.GetComponentData<DataContainerComponent>(contenedorQ.GetSingletonEntity()).selectedSquadBaseID.ToString();
        }

        void Escuadra()
        {
            string id = TropaActual();
            var tropa = string.IsNullOrEmpty(id) ? null : SquadDataService.GetSquadById(id);
            escuadra.style.display = tropa != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (tropa == null) return;
            if (id != escuadraMostrada) MontarEscuadra(id, tropa);

            // vivos y total: los del juego; en red, los vivos que manda el servidor y el total de la tropa
            int vivos = datos.aliveUnits, total = datos.totalUnits;
            var vidas = new List<float>();
            // la vida de cada soldado: en local, de sus entidades; en red, la que manda el servidor
            if (!escuadraQ.IsEmptyIgnoreFilter) VidasEnLocal(vidas);
            else VistaDeBatalla.VidasDeMiEscuadra(vidas);
            if (total <= 0 && vidas.Count > 0)
            {
                // en red, los vivos y el total salen de la vida de cada soldado (los muertos siguen en la lista mientras estén en el búfer)
                vivos = 0;
                foreach (var v in vidas) if (v > 0) vivos++;
            }
            if (total <= 0)
            {
                // en red el servidor manda los vivos y no el total (los muertos salen del búfer): el total, los más que se han visto (los que
                // entraron: los autorizados del ticket, que pueden ser menos que los de la tropa)
                if (vivos <= 0) vivos = VistaDeBatalla.SoldadosPropios();
                if (!maximoVisto.TryGetValue(id, out int visto) || vivos > visto) maximoVisto[id] = visto = vivos;
                total = visto > 0 ? visto : tropa.unitCount;
            }
            efectivos.text = total > 0 ? $"{vivos} de {total} soldados" : "";
            barraEscuadra.parent.style.display = total > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (total > 0) barraEscuadra.style.width = Length.Percent(100f * Mathf.Clamp01((float)vivos / total));
            if (vivos != vivosEnTarjeta || total != totalEnTarjeta) PonerTarjeta(id, vivos, total);
            PonerSoldados(vidas);

            foreach (var (r, tipo) in iconosFormacion) r.EnableInClassList("hud-placa-oro", tipo == datos.currentFormation);
            bool algunaHabilidad = false;
            for (int i = 0; i < deEscuadra.Count; i++)
            {
                var ranura = (SkillSlot)((int)SkillSlot.Squad1 + i);
                var s = datos.Skill(ranura);
                deEscuadra[i].Raiz.style.display = s.skillId != 0 ? DisplayStyle.Flex : DisplayStyle.None;
                algunaHabilidad |= s.skillId != 0;
                Pintar(deEscuadra[i], s, true);
            }
            habilidadesKicker.style.display = habilidades.style.display = algunaHabilidad ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void MontarEscuadra(string id, SquadData tropa)
        {
            escuadraMostrada = id;
            vivosEnTarjeta = totalEnTarjeta = -1;
            nombre.text = tropa.squadName;
            estrellasHueco.Clear();
            var estrellas = PiezasDeTropa.Estrellas(id, 13);
            if (estrellas != null) estrellasHueco.Add(estrellas);
            formaciones.Clear();
            iconosFormacion.Clear();
            for (int i = 0; i < (tropa.formations?.Length ?? 0) && i < 4; i++)
            {
                var f = tropa.formations[i];
                if (f == null) continue;
                var r = Caja(formaciones, "hud-formacion", "hud-placa");
                r.tooltip = f.formationType.ToString();
                var ic = Caja(r, "hud-hueco-icono");
                if (f.formationIcon != null) ic.style.backgroundImage = new StyleBackground(f.formationIcon);
                Texto(r, "F" + (i + 1), "hud-hueco-tecla", "hud-inter");
                iconosFormacion.Add((r, f.formationType));
            }
        }

        // La tarjeta del juego, con «vivos/total» en su franja; sin SquadData del motor, la imagen de la tropa de Conquest.
        void PonerTarjeta(string id, int vivos, int total)
        {
            vivosEnTarjeta = vivos;
            totalEnTarjeta = total;
            tarjetaHueco.Clear();
            var t = PiezasDeTropa.Tarjeta(id, null, total > 0 ? vivos : (int?)null, total > 0 ? total : (int?)null, null, AnchoTarjeta);
            if (t != null) tarjetaHueco.Add(t);
        }

        // La vida de cada soldado de la escuadra activa (batalla en local), de más a menos.
        void VidasEnLocal(List<float> vidas)
        {
            var e = escuadraQ.GetSingletonEntity();
            if (!em.HasBuffer<SquadUnitElement>(e)) return;
            var unidades = em.GetBuffer<SquadUnitElement>(e);
            for (int i = 0; i < unidades.Length; i++)
            {
                var u = unidades[i].Value;
                if (!em.Exists(u) || !em.HasComponent<HealthComponent>(u)) { vidas.Add(0); continue; }
                var hp = em.GetComponentData<HealthComponent>(u);
                vidas.Add(hp.maxHealth > 0 ? Mathf.Clamp01(hp.currentHealth / hp.maxHealth) : 0);
            }
            vidas.Sort((a, b) => b.CompareTo(a));
        }

        void PonerSoldados(List<float> vidas)
        {
            soldados.style.display = vidas.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            while (pips.Count < vidas.Count)
            {
                var r = Caja(soldados, "hud-soldado");
                pips.Add((r, Caja(r, "hud-soldado-vida")));
            }
            for (int i = 0; i < pips.Count; i++)
            {
                var (r, v) = pips[i];
                r.style.display = i < vidas.Count ? DisplayStyle.Flex : DisplayStyle.None;
                if (i >= vidas.Count) continue;
                v.style.height = Length.Percent(vidas[i] * 100f);
                r.EnableInClassList("hud-soldado-mal", vidas[i] <= 0.35f);
                r.EnableInClassList("hud-soldado-muerto", vidas[i] <= 0f);
            }
        }
    }
}
