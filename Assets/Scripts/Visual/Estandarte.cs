// Los estandartes de la batalla con el sigilo de Facción (paso 8 del bloque 4, tanda 4). Capa visual: nada de esto cambia el juego.
//   - `EstandarteComponent`: el sigilo (su clave de `Sigilos.Clave`) y qué estandarte lleva la entidad: el del héroe o el de su escuadra. Lo pone
//     quien monta la batalla (`BattleSceneController`, del ticket) y, en un cliente de red, quien crea los espejos (lo que viaja en `VisualRed`).
//   - `EstandarteVisual`: sobre cada visual (`EntityVisualSync`). El héroe lleva siempre el suyo; de cada escuadra, su primer soldado vivo, si la
//     opción «Estandarte de las tropas» está encendida (`OpcionesDelJuego`, por defecto sí). Va a la espalda, de pie, siguiendo el pecho.
//   - `BanderaDeZona`: la bandera de una zona de captura, con el sigilo del bando que la tiene (sin dueño, el neutro).
//   - La tela es la bandera de Synty (`SM_Prop_Flag_13`, la de las zonas) en `Resources/Batalla/Estandarte` con su asta; su textura se compone una vez
//     por sigilo: el color primario y el sigilo en medio (`TelaDeSigilo`), en una copia del material. Sin gráficos (el servidor), nada.
using System.Collections.Generic;
using ConquestTactics.Mundo;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>El estandarte de una entidad de la batalla: la clave de su sigilo y su tipo.</summary>
public struct EstandarteComponent : IComponentData
{
    public const byte Ninguno = 0, DeHeroe = 1, DeEscuadra = 2;
    public FixedString128Bytes Clave;
    public byte Tipo;
}

/// <summary>Lo común de los estandartes de una batalla: los sigilos de los dos bandos (para las zonas) y la opción de las tropas.</summary>
public static class EstandartesDeBatalla
{
    /// <summary>Claves de los sigilos del atacante y del defensor (los del ticket; vacías, el neutro).</summary>
    public static string Atacante = "", Defensor = "";

    /// <summary>«Estandarte de las tropas», de las opciones del juego (`OpcionesDelJuego`): encendido por defecto.</summary>
    public static bool DeTropas => OpcionesDelJuego.Valor(OpcionesDelJuego.EstandarteDeTropas);

    public static bool HayGraficos => !Application.isBatchMode && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
}

/// <summary>La tela de un estandarte con un sigilo: una copia del material de la bandera con la textura compuesta, guardada por su clave.</summary>
public static class TelaDeSigilo
{
    const int Lado = 512;
    static readonly Dictionary<string, Material> materiales = new Dictionary<string, Material>();

    public static Material Para(Material original, string clave)
    {
        if (original == null) return null;
        var k = original.GetEntityId() + "/" + clave;
        if (materiales.TryGetValue(k, out var m) && m != null) return m;
        var tela = Componer(original.mainTexture, clave);
        m = new Material(original) { name = original.name + " (" + clave + ")" };
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tela);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tela);
        return materiales[k] = m;
    }

    /// <summary>La textura de la bandera de Synty con su franja de muestras abajo (los aros y el remate la leen) y, encima, el color primario del
    /// sigilo y el sigilo centrado.</summary>
    static Texture Componer(Texture base0, string clave)
    {
        TexturaDeSigilo.Asegurar();
        var sigilo = Sigilos.DeClave(clave) ?? Sigilos.Reservado(clave) ?? Sigilos.Neutro;
        // La de Synty no se puede leer: se copia a la tarjeta y se lee de vuelta (en sRGB, como se guarda).
        var tela = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false, false) { name = "Tela " + clave, wrapMode = TextureWrapMode.Clamp };
        if (base0 != null)
        {
            var rt = RenderTexture.GetTemporary(Lado, Lado, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var antes = RenderTexture.active;
            Graphics.Blit(base0, rt);
            RenderTexture.active = rt;
            tela.ReadPixels(new Rect(0, 0, Lado, Lado), 0, 0);
            RenderTexture.active = antes;
            RenderTexture.ReleaseTemporary(rt);
        }
        var px = tela.GetPixels32();
        ColorUtility.TryParseHtmlString(Sigilos.ColorHex(sigilo.ColorPrimarioId), out var primario);
        Color32 fondo = primario;
        // (la franja de muestras de Synty ocupa el 3 % de abajo, y los aros y el remate la leen: se deja)
        var franja = Mathf.CeilToInt(Lado * 0.03f);
        for (var y = franja; y < Lado; y++) for (var x = 0; x < Lado; x++) px[y * Lado + x] = fondo;
        var dibujo = TexturaDeSigilo.De(sigilo, 256);
        if (dibujo != null && dibujo.isReadable)
        {
            // El sigilo de la interfaz viene pasado a lineal (`TexturaDeSigilo.Pintar`): la tela es sRGB, se devuelve a gamma.
            var lineal = QualitySettings.activeColorSpace == ColorSpace.Linear;
            var sp = dibujo.GetPixels();
            int ancho = Lado * 7 / 10, alto = ancho * dibujo.height / dibujo.width;
            int x0 = (Lado - ancho) / 2, y0 = franja + (Lado - franja - alto) / 2;
            for (var y = 0; y < alto; y++)
                for (var x = 0; x < ancho; x++)
                {
                    var c = sp[(y * dibujo.height / alto) * dibujo.width + x * dibujo.width / ancho];
                    if (c.a <= 0) continue;
                    if (lineal) c = new Color(Mathf.LinearToGammaSpace(c.r), Mathf.LinearToGammaSpace(c.g), Mathf.LinearToGammaSpace(c.b), c.a);
                    var i = (y0 + y) * Lado + x0 + x;
                    px[i] = Color32.Lerp(px[i], (Color32)new Color(c.r, c.g, c.b, 1), c.a);
                }
        }
        tela.SetPixels32(px);
        tela.Apply(true, true);
        return tela;
    }
}

/// <summary>El estandarte de una visual de la batalla (héroe o abanderado de escuadra), a la espalda.</summary>
[DisallowMultipleComponent]
public sealed class EstandarteVisual : MonoBehaviour
{
    const float Escala = 0.3f, Atras = 0.28f, Abajo = 1.05f, Cada = 0.25f;
    World mundo;
    Entity entidad;
    GameObject estandarte;
    string clavePuesta;
    Transform pecho;
    float siguiente;

    /// <summary>El estandarte que lleva ahora (`EstandarteComponent.Tipo`; Ninguno si no lleva).</summary>
    public byte Mostrado { get; private set; }

    public void Bind(World w, Entity e) { mundo = w; entidad = e; siguiente = 0; }

    void LateUpdate()
    {
        if (estandarte != null && pecho != null)
        {
            estandarte.transform.SetPositionAndRotation(pecho.position - transform.forward * Atras - Vector3.up * Abajo, Quaternion.LookRotation(transform.forward, Vector3.up));
        }
        if (Time.unscaledTime < siguiente) return;
        siguiente = Time.unscaledTime + Cada;
        var (clave, tipo) = Lo();
        var ver = tipo == EstandarteComponent.DeHeroe || tipo == EstandarteComponent.DeEscuadra && EstandartesDeBatalla.DeTropas;
        if (!ver || string.IsNullOrEmpty(clave)) { Quitar(); return; }
        if (estandarte != null && clavePuesta == clave) { Mostrado = tipo; return; }
        Quitar();
        Poner(clave);
        if (estandarte != null) Mostrado = tipo;
    }

    /// <summary>La clave y el tipo: los de la entidad o, si es un soldado sin ellos (una batalla sin red), el de su héroe si es el primero vivo de
    /// su escuadra.</summary>
    (string, byte) Lo()
    {
        if (mundo == null || !mundo.IsCreated) return (null, 0);
        var em = mundo.EntityManager;
        if (!em.Exists(entidad) || em.HasComponent<IsDeadComponent>(entidad)) return (null, 0);
        if (em.HasComponent<EstandarteComponent>(entidad))
        {
            var e = em.GetComponentData<EstandarteComponent>(entidad);
            return (e.Clave.ToString(), e.Tipo);
        }
        if (!em.HasComponent<UnitOwnerComponent>(entidad)) return (null, 0);
        var duenos = em.GetComponentData<UnitOwnerComponent>(entidad);
        if (!em.Exists(duenos.hero) || !em.HasComponent<EstandarteComponent>(duenos.hero) || !em.Exists(duenos.squad) || !em.HasBuffer<SquadUnitElement>(duenos.squad)) return (null, 0);
        foreach (var u in em.GetBuffer<SquadUnitElement>(duenos.squad))
        {
            if (!em.Exists(u.Value) || em.HasComponent<IsDeadComponent>(u.Value)) continue;
            return u.Value == entidad ? (em.GetComponentData<EstandarteComponent>(duenos.hero).Clave.ToString(), EstandarteComponent.DeEscuadra) : (null, 0);
        }
        return (null, 0);
    }

    void Poner(string clave)
    {
        var prefab = Resources.Load<GameObject>("Batalla/Estandarte");
        if (prefab == null) return;
        estandarte = Instantiate(prefab);
        estandarte.name = "Estandarte";
        estandarte.transform.localScale = Vector3.one * Escala;
        foreach (var c in estandarte.GetComponentsInChildren<Collider>()) Destroy(c);
        var tela = estandarte.transform.Find("Tela")?.GetComponent<Renderer>();
        if (tela != null) tela.sharedMaterial = TelaDeSigilo.Para(tela.sharedMaterial, clave);
        var animator = GetComponentInChildren<Animator>();
        pecho = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine) : null;
        if (pecho == null) pecho = transform;
        clavePuesta = clave;
    }

    void Quitar()
    {
        if (estandarte != null) Destroy(estandarte);
        estandarte = null;
        clavePuesta = null;
        Mostrado = EstandarteComponent.Ninguno;
    }

    void OnDisable() => Quitar();
    void OnDestroy() => Quitar();
}

/// <summary>La bandera de una zona de captura: el sigilo del bando que la tiene (`ZoneTriggerComponent.teamOwner`; sin dueño, el neutro).</summary>
public sealed class BanderaDeZona : MonoBehaviour
{
    Renderer tela;
    Material original;
    Entity zona;
    int puesto = -1;
    string clavePuesta;
    float siguiente;

    public void Bind(Entity e, Renderer r)
    {
        zona = e;
        tela = r;
        original = r != null ? r.sharedMaterial : null;
    }

    void Update()
    {
        if (tela == null || Time.unscaledTime < siguiente) return;
        siguiente = Time.unscaledTime + 0.5f;
        var w = World.DefaultGameObjectInjectionWorld;
        if (w == null || !w.IsCreated || !w.EntityManager.Exists(zona) || !w.EntityManager.HasComponent<ZoneTriggerComponent>(zona)) return;
        var dueno = w.EntityManager.GetComponentData<ZoneTriggerComponent>(zona).teamOwner;
        var clave = dueno == (int)Team.TeamA ? EstandartesDeBatalla.Atacante : dueno == (int)Team.TeamB ? EstandartesDeBatalla.Defensor : "";
        if (string.IsNullOrEmpty(clave)) clave = "neutro";
        if (dueno == puesto && clave == clavePuesta) return;
        puesto = dueno;
        clavePuesta = clave;
        tela.sharedMaterial = TelaDeSigilo.Para(original, clave);
    }
}
