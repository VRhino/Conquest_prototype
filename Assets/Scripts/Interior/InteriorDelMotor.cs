using System.Collections.Generic;
using System.Linq;
using ConquestTactics.Mundo;
using ConquestTactics.Visual;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// FeudoScene en modo «desde el motor» (paso 4 del bloque 4): el interior de un campamento de mercenarios de BronzeAge. Se engancha al cargar
/// la escena si hay <see cref="SelectorDePersonaje.Interior"/>; sin él, FeudoScene hace lo de siempre. No toca el fichero de la escena:
/// apaga al cargar lo que vive de datos de relleno (HUD, tienda, barracas, minimapa, matchmaking, el herrero, TestEnv) y sustituye el recinto
/// del feudo por el campamento montado con cuadros desde la planta que manda el motor. Quedan el héroe (ECS, con su aspecto del motor), la
/// cámara que lo sigue, la luz y la SubScene. Alt libera el ratón para la interfaz, como en Conquest.
/// </summary>
public sealed class InteriorDelMotor : MonoBehaviour
{
    public const string Escena = "FeudoScene";

    /// <summary>Metros de Unity por unidad local de la planta. La planta vive en su propio espacio, sin relación con el mapa (Trazado_Urbano:
    /// «la ciudad no se mide contra el exterior»), así que no sale de EscalaDelMapa; con 1 m una vivienda de 2×2 celdas es una tienda de
    /// 6×6 m y el campamento (radio de 60 u.l.) mide unos 120 m de lado a lado: a la escala del héroe de Synty.</summary>
    public const float MetrosPorUnidadLocal = 1f;

    /// <summary>Encender el recinto del feudo hecho a mano (`Map`) en lugar de quitarlo. Para verlo, a mano: no se usa en el flujo.</summary>
    public static bool ConRecintoDelFeudo;

    /// <summary>Lo que se apaga al cargar: vive de `player_save.json` o de datos de relleno sin equivalente del motor todavía.</summary>
    static readonly string[] Apagados = { "Matchmaking", "TestEnv", "UICanvas", "MinimapCanvas", "NPCs", "Squad_Preview", "Hero_Preview" };

    static readonly Dictionary<string, (string color, float alto)> Piezas = new Dictionary<string, (string, float)>
    {
        ["taberna"] = ("#a0672d", 5f), ["vivienda"] = ("#c9b27c", 3f), ["mercado"] = ("#d6a437", 4f), ["puestoMercado"] = ("#e0c070", 2.2f),
        ["barracon"] = ("#7a2f2f", 4.5f), ["galeriaDeTiro"] = ("#566b3a", 4.5f), ["caballerizas"] = ("#6b5638", 4.5f),
        ["plazaDeArmas"] = ("#9a8f78", 0f), ["plaza"] = ("#b9ae94", 0f), ["pozo"] = ("#7d8f9a", 1f), ["parque"] = ("#8fb070", 0f),
    };
    const string Suelo = "#93c26b", Calle = "#b39b72", Empalizada = "#6b4a29";
    const float AltoEmpalizada = 3f;

    Scene escena;
    GameObject recinto;
    bool montado, panelAbierto;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Registrar()
    {
        SceneManager.sceneLoaded -= AlCargar;
        SceneManager.sceneLoaded += AlCargar;
    }

    static void AlCargar(Scene cargada, LoadSceneMode modo)
    {
        if (cargada.name != Escena || SelectorDePersonaje.Interior == null) return;
        GameObject mapa = null;
        foreach (var raiz in cargada.GetRootGameObjects())
        {
            if (Apagados.Contains(raiz.name)) raiz.SetActive(false);
            if (raiz.name == "Map") mapa = raiz;
        }
        SceneTransitionService.UpdateGamePhase(GamePhase.Feudo);
        var go = new GameObject("Interior del motor");
        SceneManager.MoveGameObjectToScene(go, cargada);
        var interior = go.AddComponent<InteriorDelMotor>();
        interior.escena = cargada;
        interior.recinto = mapa;
        // Hasta que el campamento tenga su suelo, solo queda el del feudo (el héroe aparece encima de él).
        if (mapa != null && !ConRecintoDelFeudo)
            foreach (Transform hijo in mapa.transform) hijo.gameObject.SetActive(hijo.name == "Ground");
    }

    void Update()
    {
        var interior = SelectorDePersonaje.Interior;
        if (interior == null) return;
        var teclado = UnityEngine.InputSystem.Keyboard.current;
        if (teclado != null && teclado.leftAltKey.wasPressedThisFrame) BattleCommandInput.SetPointerMode(!BattleCommandInput.PointerMode);
        // Un panel abierto bloquea como un diálogo de Conquest: sin andar ni girar la cámara, con el ratón libre.
        if (interior.PanelAbierto != panelAbierto)
        {
            panelAbierto = interior.PanelAbierto;
            DialogueUIState.IsDialogueOpen = panelAbierto;
            SceneTransitionService.ApplyCursorState(panelAbierto || BattleCommandInput.PointerMode);
        }
        if (montado) return;
        var heroe = FindObjectsByType<EntityVisualSync>(FindObjectsSortMode.None).FirstOrDefault(v => v.IsLocalHero);
        if (heroe == null) return;
        Montar(interior, heroe.transform.position);
        montado = true;
        interior.HeroeDentro = true;
    }

    void OnDestroy()
    {
        // Al descargar la escena: fuera el héroe del World de ECS (si no, al volver a entrar no aparecería otro) y el ratón suelto.
        BattleCommandInput.SetPointerMode(false);
        DialogueUIState.IsDialogueOpen = false;
        SceneTransitionService.ApplyCursorState(true);
        if (SelectorDePersonaje.Interior != null) { SelectorDePersonaje.Interior.HeroeDentro = false; SelectorDePersonaje.Interior.ZonaCercana = null; }
        var mundo = World.DefaultGameObjectInjectionWorld;
        if (mundo == null || !mundo.IsCreated) return;
        using var heroes = mundo.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<IsLocalPlayer>());
        mundo.EntityManager.DestroyEntity(heroes);
    }

    /// <summary>El campamento con cuadros desde la planta: el suelo, las calles, cada edificio con su huella y su color (los del plano de
    /// BronzeAgeClient) y la empalizada con su hueco en la puerta. Todo con colisión salvo lo plano. La puerta queda donde está el héroe.</summary>
    void Montar(SelectorDePersonaje.InteriorParaVer interior, Vector3 heroe)
    {
        var raiz = new GameObject("Campamento del motor");
        SceneManager.MoveGameObjectToScene(raiz, escena);
        var todo = interior.Edificios.Concat(interior.Calles).Concat(interior.Empalizada).Concat(interior.Puerta).ToList();
        if (todo.Count == 0) { Debug.LogWarning("[Interior] el campamento no trae planta: queda el suelo del feudo."); return; }
        double puertaX = interior.Puerta.Count > 0 ? interior.Puerta.Average(r => r.X) : 0;
        double puertaY = interior.Puerta.Count > 0 ? interior.Puerta.Average(r => r.Y) : todo.Max(r => r.Y + r.Alto / 2);
        Vector3 A(double x, double y, float alto) =>
            heroe + new Vector3((float)(x - puertaX) * MetrosPorUnidadLocal, alto, -(float)(y - puertaY) * MetrosPorUnidadLocal);

        double minX = todo.Min(r => r.X - r.Ancho / 2), maxX = todo.Max(r => r.X + r.Ancho / 2);
        double minY = todo.Min(r => r.Y - r.Alto / 2), maxY = todo.Max(r => r.Y + r.Alto / 2);
        const double margen = 30;
        Caja(raiz, "Suelo", A((minX + maxX) / 2, (minY + maxY) / 2, -0.25f),
            new Vector3((float)(maxX - minX + 2 * margen), 0.5f, (float)(maxY - minY + 2 * margen)) * MetrosPorUnidadLocal, Suelo, true);
        foreach (var c in interior.Calles) Plano(raiz, "Calle", c, A, 0.02f, Calle);
        foreach (var e in interior.Edificios)
        {
            var (color, alto) = Piezas.TryGetValue(e.Tipo, out var p) ? p : ("#a89a80", 3f);
            if (alto <= 0) Plano(raiz, e.Tipo, e, A, 0.04f, color);
            else Caja(raiz, e.Tipo, A(e.X, e.Y, alto / 2), new Vector3((float)e.Ancho * MetrosPorUnidadLocal, alto, (float)e.Alto * MetrosPorUnidadLocal), color, true);
        }
        foreach (var r in interior.Empalizada)
            Caja(raiz, "Empalizada", A(r.X, r.Y, AltoEmpalizada / 2), new Vector3((float)r.Ancho * MetrosPorUnidadLocal, AltoEmpalizada, (float)r.Alto * MetrosPorUnidadLocal), Empalizada, true);
        // La puerta, sin colisión: por ahí se sale (la salida de verdad es la ventana «Salir al mundo»).
        foreach (var r in interior.Puerta) Plano(raiz, "Puerta", r, A, 0.03f, Calle);
        // Las zonas donde se interactúa, como el herrero de Conquest: un collider disparador alrededor de la pieza, la tecla F y un cartel.
        foreach (var e in interior.Edificios.Where(x => x.Zona != null))
            Zona(raiz, e.Zona, A(e.X, e.Y, 1.5f), new Vector3((float)e.Ancho + 5, 3, (float)e.Alto + 5) * MetrosPorUnidadLocal);
        // La de la puerta, justo fuera de la empalizada (el héroe aparece en el hueco: hay que salir para que se abra).
        foreach (var r in interior.Puerta)
            Zona(raiz, "puerta", A(r.X, r.Y + r.Alto / 2 + 3, 1.5f), new Vector3((float)r.Ancho + 2, 3, 4) * MetrosPorUnidadLocal);
        if (recinto != null && !ConRecintoDelFeudo) recinto.SetActive(false);
        Debug.Log($"[Interior] campamento montado: {interior.Edificios.Count} piezas, {interior.Calles.Count} calles, {interior.Empalizada.Count} tramos de empalizada; puerta en ({puertaX:0.#}, {puertaY:0.#}) u.l.");
    }

    static void Plano(GameObject raiz, string nombre, SelectorDePersonaje.PiezaDePlanta r, System.Func<double, double, float, Vector3> A, float alto, string color)
    {
        var go = Caja(raiz, nombre, A(r.X, r.Y, alto / 2), new Vector3((float)r.Ancho * MetrosPorUnidadLocal, alto, (float)r.Alto * MetrosPorUnidadLocal), color, false);
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void Zona(GameObject raiz, string id, Vector3 centro, Vector3 tamano)
    {
        var go = new GameObject("Zona " + id);
        go.transform.SetParent(raiz.transform, false);
        go.transform.position = centro;
        var caja = go.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = tamano;
        go.AddComponent<ZonaDelInterior>().Id = id;
    }

    static readonly Dictionary<string, Material> materiales = new Dictionary<string, Material>();

    static GameObject Caja(GameObject raiz, string nombre, Vector3 centro, Vector3 tamano, string color, bool choca)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetParent(raiz.transform, false);
        go.transform.position = centro;
        go.transform.localScale = tamano;
        if (!choca) Destroy(go.GetComponent<Collider>());
        if (!materiales.TryGetValue(color, out var m) || m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ColorUtility.TryParseHtmlString(color, out var c);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.1f);
            materiales[color] = m;
        }
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }
}

/// <summary>Una zona del interior donde se interactúa (taberna, mercado, edificio militar, puerta), con el mismo mecanismo que la puerta y el
/// herrero del feudo (`DoorInteractable`, `NpcTriggerZone`): el disparador detecta al héroe local, la tecla F llega por
/// `PlayerInteractionComponent` y el cartel lo pone la interfaz con <see cref="SelectorDePersonaje.InteriorParaVer.ZonaCercana"/>.</summary>
[RequireComponent(typeof(Collider))]
public sealed class ZonaDelInterior : MonoBehaviour
{
    public string Id;
    EntityVisualSync heroe;

    static EntityVisualSync HeroeLocal(Collider otro)
    {
        var vs = otro.GetComponent<EntityVisualSync>();
        return vs != null && vs.IsLocalHero ? vs : null;
    }

    void OnTriggerEnter(Collider otro)
    {
        var vs = HeroeLocal(otro);
        if (vs == null || SelectorDePersonaje.Interior == null) return;
        heroe = vs;
        SelectorDePersonaje.Interior.ZonaCercana = Id;
    }

    void OnTriggerExit(Collider otro)
    {
        if (HeroeLocal(otro) == null) return;
        heroe = null;
        if (SelectorDePersonaje.Interior != null && SelectorDePersonaje.Interior.ZonaCercana == Id) SelectorDePersonaje.Interior.ZonaCercana = null;
    }

    void Update()
    {
        var interior = SelectorDePersonaje.Interior;
        if (heroe == null || interior == null || interior.PanelAbierto || interior.ZonaCercana != Id) return;
        var mundo = World.DefaultGameObjectInjectionWorld;
        if (mundo == null || !mundo.IsCreated) return;
        var entidad = heroe.GetHeroEntity();
        var em = mundo.EntityManager;
        if (!em.Exists(entidad) || !em.HasComponent<PlayerInteractionComponent>(entidad)) return;
        if (em.GetComponentData<PlayerInteractionComponent>(entidad).interactPressed) interior.AlInteractuar?.Invoke(Id);
    }
}
