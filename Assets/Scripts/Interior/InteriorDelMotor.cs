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

    /// <summary>Los de lo que está montado: una plaza va con su arte (<see cref="CiudadPorDentro"/>) a 3 m por unidad, la escala del héroe
    /// (Víctor, 2026-11-25); el campamento sigue en cuadros a 1 m hasta que le toque.</summary>
    static float metros = MetrosPorUnidadLocal;

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

    /// <summary>La muralla de una plaza por nivel (`ciudad3d/murallas.ts`): empalizada de madera, adobe y ciclópea; lo comprometido sin levantar, la zanja.</summary>
    static readonly string[] ColorMuralla = { "#6b4a29", "#6b4a29", "#c4a57a", "#9a958c" };
    const string Zanja = "#5a4630", EnCola = "#8f8a80", EnObra = "#b5b0a5";
    const float AltoMuro = 4f, AltoTorre = 6f;

    Scene escena;
    GameObject recinto, raiz, cuadros;
    CiudadPorDentro.Montada ciudad;
    bool montado, panelAbierto;
    int versionMontada;
    /// <summary>Dónde quedó la puerta en el primer montaje: el héroe y la planta en unidades locales. Al volver a montar (cambió la planta) no se mueve.</summary>
    Vector3 origen;
    double puertaX, puertaY;
    /// <summary>Lo alto de cada edificio por su id, para los carteles de obra.</summary>
    readonly Dictionary<string, Vector3> altoDe = new Dictionary<string, Vector3>();
    /// <summary>La pieza de cada edificio por su id, y la resaltada ahora (la de la ficha abierta).</summary>
    readonly Dictionary<string, Renderer> piezaDe = new Dictionary<string, Renderer>();
    /// <summary>Las piezas de cada edificio montado con su arte, y las que están ahora en oro.</summary>
    Dictionary<string, List<Renderer>> arteDe = new Dictionary<string, List<Renderer>>();
    List<Renderer> enOro;
    LuzDeLaPlaza luz;
    string resaltado;
    static readonly Color Oro = new Color32(0xff, 0xd2, 0x4a, 0xff);


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
        if (montado)
        {
            // La planta cambió (una obra acaba, otro edificio entra en la cola): se vuelve a montar en el mismo sitio.
            if (interior.Version != versionMontada) { versionMontada = interior.Version; Montar(interior); resaltado = null; }
            if (interior.Elegido != resaltado) Resaltar(interior.Elegido);
            return;
        }
        var heroe = FindObjectsByType<EntityVisualSync>(FindObjectsSortMode.None).FirstOrDefault(v => v.IsLocalHero);
        if (heroe == null) return;
        origen = heroe.transform.position;
        versionMontada = interior.Version;
        Montar(interior);
        interior.EnPantalla = EnPantalla;
        montado = true;
        interior.HeroeDentro = true;
    }

    /// <summary>Lo alto de un edificio en la pantalla (de 0 a 1, desde arriba a la izquierda), si está delante de la cámara.</summary>
    (float x, float y)? EnPantalla(string id)
    {
        var cam = Camera.main;
        if (cam == null || id == null || !altoDe.TryGetValue(id, out var p)) return null;
        var s = cam.WorldToScreenPoint(p);
        if (s.z <= 0) return null;
        return (s.x / cam.pixelWidth, 1 - s.y / cam.pixelHeight);
    }

    /// <summary>El edificio elegido, en oro (el prototipo lo rodea con su contorno dorado): en su copia, con un bloque de propiedades; el material
    /// compartido no se toca.</summary>
    void Resaltar(string id)
    {
        if (resaltado != null && piezaDe.TryGetValue(resaltado, out var antes) && antes != null) antes.SetPropertyBlock(null);
        if (enOro != null) foreach (var x in enOro) if (x != null) x.SetPropertyBlock(null);
        enOro = null;
        resaltado = id;
        // con su arte: todas sus piezas, con el tono de su material hacia el oro
        if (id != null && arteDe.TryGetValue(id, out enOro))
        {
            foreach (var x in enOro)
            {
                if (x == null || x.sharedMaterial == null || !x.sharedMaterial.HasProperty("_Tono")) continue;
                var b = new MaterialPropertyBlock();
                b.SetColor("_Tono", Color.Lerp(x.sharedMaterial.GetColor("_Tono"), Oro * 1.6f, 0.45f));
                x.SetPropertyBlock(b);
            }
            return;
        }
        if (id == null || !piezaDe.TryGetValue(id, out var r) || r == null) return;
        var bloque = new MaterialPropertyBlock();
        bloque.SetColor("_BaseColor", Color.Lerp(r.sharedMaterial.GetColor("_BaseColor"), Oro, 0.55f));
        r.SetPropertyBlock(bloque);
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

    /// <summary>El interior con cuadros desde la planta: el suelo, las calles, cada edificio con su huella y su color (los del plano de
    /// BronzeAgeClient), la empalizada del campamento o la muralla de la plaza celda a celda, y el hueco de la puerta. Todo con colisión salvo lo
    /// plano. La puerta queda donde estaba el héroe al entrar. En una plaza (paso 7), lo que está en cola es una losa gris y lo que está en obra, una
    /// caja gris más baja; las torres, más altas que el muro; lo comprometido sin levantar, la zanja. Al cambiar la planta se rehace igual.</summary>
    void Montar(SelectorDePersonaje.InteriorParaVer interior)
    {
        altoDe.Clear();
        piezaDe.Clear();
        bool plaza = interior.Edificios.Any(e => e.Id != null);
        metros = plaza ? CiudadPorDentro.Metros : MetrosPorUnidadLocal;
        // La plaza con su arte no se rehace al cambiar la planta (Víctor, 2026-11-26): su raíz y su ciudad se quedan y `CiudadPorDentro` las
        // pone al día; solo se rehacen los cuadros (lo que aún no tiene modelo, la muralla, las zonas), que son pocos y van aparte. El
        // campamento, todo de cuadros, se rehace entero como siempre.
        if (raiz != null && (!plaza || ciudad == null)) { Destroy(raiz); raiz = null; cuadros = null; ciudad = null; }
        if (raiz == null)
        {
            raiz = new GameObject(plaza ? "Plaza del motor" : "Campamento del motor");
            SceneManager.MoveGameObjectToScene(raiz, escena);
        }
        if (cuadros != null && cuadros != raiz) Destroy(cuadros);
        cuadros = raiz;
        if (plaza)
        {
            cuadros = new GameObject("Cuadros");
            cuadros.transform.SetParent(raiz.transform, false);
        }
        var todo = interior.Edificios.Concat(interior.Calles).Concat(interior.Empalizada).Concat(interior.Puerta).Concat(interior.Muralla).ToList();
        if (todo.Count == 0) { Debug.LogWarning("[Interior] el sitio no trae planta: queda el suelo del feudo."); return; }
        double minX = todo.Min(r => r.X - r.Ancho / 2), maxX = todo.Max(r => r.X + r.Ancho / 2);
        double minY = todo.Min(r => r.Y - r.Alto / 2), maxY = todo.Max(r => r.Y + r.Alto / 2);
        if (!montado)
        {
            puertaX = interior.Puerta.Count > 0 ? interior.Puerta.Average(r => r.X) : 0;
            puertaY = interior.Puerta.Count > 0 ? interior.Puerta.Average(r => r.Y) : maxY;
        }
        Vector3 A(double x, double y, float alto) =>
            origen + new Vector3((float)(x - puertaX) * metros, alto, -(float)(y - puertaY) * metros);

        // La plaza, con su arte: el suelo, las calles, lo que tiene receta y el relleno de lo libre. Lo demás (lo que está en cola o en obra,
        // lo que aún no tiene modelo, la muralla) sigue en cuadros.
        if (plaza && ciudad == null) ciudad = CiudadPorDentro.Montar(interior, A, raiz.transform);
        else if (plaza) CiudadPorDentro.Actualizar(ciudad, interior, A);
        arteDe = ciudad?.PiezasDe ?? new Dictionary<string, List<Renderer>>();
        // la luz de la plaza, con la hora del mundo: una vez (cuelga de este objeto, no de lo que se vuelve a montar)
        if (ciudad != null && luz == null) luz = LuzDeLaPlaza.Poner(transform, interior.AhoraMundo);
        // el suelo, hijo directo del interior, como el de cuadros: por él se orientan las pruebas que llevan al héroe andando
        if (ciudad != null) ciudad.Suelo.transform.SetParent(raiz.transform, true);
        enOro = null;
        const double margen = 30;
        if (ciudad == null)
        {
            Caja(cuadros, "Suelo", A((minX + maxX) / 2, (minY + maxY) / 2, -0.25f),
                new Vector3((float)(maxX - minX + 2 * margen), 0.5f, (float)(maxY - minY + 2 * margen)) * metros, Suelo, true);
            foreach (var c in interior.Calles) Plano(cuadros, "Calle", c, A, 0.02f, Calle);
        }
        for (int indice = 0; indice < interior.Edificios.Count; indice++)
        {
            var e = interior.Edificios[indice];
            if (ciudad != null && ciudad.Hechos.Contains(indice))
            {
                if (e.Id != null) altoDe[e.Id] = A(e.X, e.Y, 7f);
                continue;
            }
            var (color, alto) = Piezas.TryGetValue(e.Tipo, out var p) ? p : (Edificios.Color(e.Tipo), Edificios.Alto(e.Tipo));
            GameObject pieza;
            if (e.Estado == "cola") { pieza = Plano(cuadros, e.Tipo + " (en cola)", e, A, 0.08f, EnCola); alto = 0.08f; }
            else if (e.Estado == "obra" && alto > 0) { alto *= 0.45f; pieza = Caja(cuadros, e.Tipo + " (en obra)", A(e.X, e.Y, alto / 2), Tamano(e, alto), EnObra, true); }
            else if (alto <= 0) pieza = Plano(cuadros, e.Tipo, e, A, 0.04f, e.Estado == "obra" ? EnObra : color);
            else pieza = Caja(cuadros, e.Tipo, A(e.X, e.Y, alto / 2), Tamano(e, alto), color, true);
            if (e.Id == null) continue;
            altoDe[e.Id] = A(e.X, e.Y, Mathf.Max(alto, 0.5f) + 1f);
            piezaDe[e.Id] = pieza.GetComponent<Renderer>();
        }
        foreach (var r in interior.Empalizada)
            Caja(cuadros, "Empalizada", A(r.X, r.Y, AltoEmpalizada / 2), Tamano(r, AltoEmpalizada), Empalizada, true);
        foreach (var r in interior.Muralla)
        {
            if (ciudad != null && r.Nivel <= CiudadPorDentro.NivelDeMurallaConArte) continue;   // la empalizada ya va con su arte
            var color = ColorMuralla[Mathf.Clamp(r.Nivel, 0, ColorMuralla.Length - 1)];
            if (r.Estado == "plan") Plano(cuadros, "Muralla (sin levantar)", r, A, 0.05f, Zanja);
            else if (r.Tipo == "puerta") Plano(cuadros, "Puerta de la muralla", r, A, 0.03f, Calle);
            else if (r.Tipo == "torre")
                Caja(cuadros, "Torre", A(r.X, r.Y, AltoTorre / 2), new Vector3((float)r.Ancho * 1.2f * metros, AltoTorre, (float)r.Alto * 1.2f * metros), color, true);
            else Caja(cuadros, "Muralla", A(r.X, r.Y, AltoMuro / 2), Tamano(r, AltoMuro), color, true);
        }
        // La puerta, sin colisión: por ahí se sale (la salida de verdad es la ventana «Salir al mundo»).
        foreach (var r in interior.Puerta) Plano(cuadros, "Puerta", r, A, 0.03f, Calle);
        // Las zonas donde se interactúa, como el herrero de Conquest: un collider disparador alrededor de la pieza, la tecla F y un cartel.
        foreach (var e in interior.Edificios.Where(x => x.Zona != null))
            Zona(cuadros, e.Zona, A(e.X, e.Y, 1.5f), new Vector3((float)e.Ancho + 5, 3, (float)e.Alto + 5) * metros, new Vector3((float)e.Ancho, 3, (float)e.Alto) * metros);
        // La de la puerta, justo fuera (el héroe aparece en el hueco: hay que salir para que se abra). Fuera es hacia donde cae la puerta desde
        // el centro de todo, por el lado que más pesa.
        double dx = puertaX - (minX + maxX) / 2, dy = puertaY - (minY + maxY) / 2;
        var (fx, fy) = System.Math.Abs(dx) > System.Math.Abs(dy) ? (System.Math.Sign(dx), 0) : (0, dy == 0 ? 1 : System.Math.Sign(dy));
        foreach (var r in interior.Puerta)
        {
            var fuera = (fx != 0 ? r.Ancho : r.Alto) / 2 + 3;
            var ancho = (fx != 0 ? r.Alto : r.Ancho) + 2;
            Zona(cuadros, "puerta", A(r.X + fx * fuera, r.Y + fy * fuera, 1.5f), (fx != 0 ? new Vector3(4, 3, (float)ancho) : new Vector3((float)ancho, 3, 4)) * metros);
        }
        if (recinto != null && !ConRecintoDelFeudo) recinto.SetActive(false);
        Debug.Log($"[Interior] montado: {interior.Edificios.Count} piezas, {interior.Calles.Count} calles, {interior.Empalizada.Count} tramos de empalizada, {interior.Muralla.Count} celdas de muralla; puerta en ({puertaX:0.#}, {puertaY:0.#}) u.l.");
    }

    static Vector3 Tamano(SelectorDePersonaje.PiezaDePlanta r, float alto) => new Vector3((float)r.Ancho * metros, alto, (float)r.Alto * metros);

    static GameObject Plano(GameObject raiz, string nombre, SelectorDePersonaje.PiezaDePlanta r, System.Func<double, double, float, Vector3> A, float alto, string color)
    {
        var go = Caja(raiz, nombre, A(r.X, r.Y, alto / 2), new Vector3((float)r.Ancho * metros, alto, (float)r.Alto * metros), color, false);
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    /// <summary>Una zona: el disparador (`tamano`) y la huella de lo que la da (`huella`; sin ella, la propia zona), que decide cuál manda si
    /// el héroe está en varias.</summary>
    static void Zona(GameObject raiz, string id, Vector3 centro, Vector3 tamano, Vector3? huella = null)
    {
        var go = new GameObject("Zona " + id);
        go.transform.SetParent(raiz.transform, false);
        go.transform.position = centro;
        var caja = go.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = tamano;
        var zona = go.AddComponent<ZonaDelInterior>();
        zona.Id = id;
        zona.Huella = new Bounds(centro, huella ?? tamano);
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
    /// <summary>La huella de lo que da la zona (el edificio): manda la zona de la huella más cercana al héroe.</summary>
    public Bounds Huella;
    EntityVisualSync heroe;

    static EntityVisualSync HeroeLocal(Collider otro)
    {
        var vs = otro.GetComponent<EntityVisualSync>();
        return vs != null && vs.IsLocalHero ? vs : null;
    }

    /// <summary>Las zonas donde está ahora el héroe. En una plaza los edificios están juntos y sus zonas se pisan: manda la más cercana.</summary>
    static readonly List<ZonaDelInterior> conHeroe = new List<ZonaDelInterior>();

    void OnTriggerEnter(Collider otro)
    {
        var vs = HeroeLocal(otro);
        if (vs == null || SelectorDePersonaje.Interior == null) return;
        heroe = vs;
        if (!conHeroe.Contains(this)) conHeroe.Add(this);
    }

    void OnTriggerExit(Collider otro)
    {
        if (HeroeLocal(otro) == null) return;
        Salir();
    }

    // Al rehacer la planta las zonas se destruyen sin avisar de la salida.
    void OnDestroy() => Salir();

    void Salir()
    {
        heroe = null;
        conHeroe.Remove(this);
        if (SelectorDePersonaje.Interior != null && SelectorDePersonaje.Interior.ZonaCercana == Id) SelectorDePersonaje.Interior.ZonaCercana = null;
    }

    void Update()
    {
        var interior = SelectorDePersonaje.Interior;
        if (heroe == null || interior == null) return;
        var donde = heroe.transform.position;
        var cercana = conHeroe.Where(z => z != null).OrderBy(z => z.Huella.SqrDistance(donde)).ThenBy(z => (z.transform.position - donde).sqrMagnitude).FirstOrDefault();
        if (cercana == this) interior.ZonaCercana = Id;
        if (interior.PanelAbierto || interior.ZonaCercana != Id) return;
        var mundo = World.DefaultGameObjectInjectionWorld;
        if (mundo == null || !mundo.IsCreated) return;
        var entidad = heroe.GetHeroEntity();
        var em = mundo.EntityManager;
        if (!em.Exists(entidad) || !em.HasComponent<PlayerInteractionComponent>(entidad)) return;
        if (em.GetComponentData<PlayerInteractionComponent>(entidad).interactPressed) interior.AlInteractuar?.Invoke(Id);
    }
}
