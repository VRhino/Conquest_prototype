using System.Collections.Generic;
using UnityEngine;

/// <summary>Viste un personaje modular de Synty con el héroe del motor (BronzeAge): el género y las piezas que guardó al crearlo (cabeza, pelo,
/// barba, cejas) y lo que lleva puesto, pieza a pieza, con el aspecto de cada pieza del catálogo del motor (<see cref="AspectoDePiezas"/>). Lo
/// usan el selector de personaje y el interior de un campamento.</summary>
public static class VestidoDelMotor
{
    public static bool Vestir(Transform personaje, ConquestTactics.Mundo.SelectorDePersonaje.HeroeParaVer heroe)
    {
        if (personaje == null || heroe == null) return false;
        var piezas = Resources.Load<Data.Avatar.AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
        if (piezas == null) { Debug.LogError("No se pudo cargar AvatarPartDatabase."); return false; }
        var genero = heroe.Femenino ? Gender.Female : Gender.Male;
        var aspecto = new List<string>();
        foreach (var id in new[] { heroe.Cabeza, heroe.Pelo, heroe.Barba, heroe.Cejas })
            if (!string.IsNullOrEmpty(id)) aspecto.Add(id);
        Data.Avatar.AvatarVisualUtils.ResetModularDummyToBase(personaje, piezas, aspecto, genero);
        // Lo que se eligió vacío (sin barba, por ejemplo) se quita: el maniquí de Synty trae una pieza puesta en cada hueco (coleta y barba) y
        // ResetModularDummyToBase solo toca los huecos de las piezas que recibe, así que un hueco vacío se quedaba con la del maniquí.
        // Con un motor que no guarda el aspecto (anterior a 358af5ff) llegan todos vacíos: se queda sin pelo, barba ni cejas, no con unos inventados.
        QuitarHuecosVacios(personaje, piezas, heroe.Pelo, heroe.Barba, heroe.Cejas, genero);
        if (heroe.Piezas == null)
        {
            // Sin lo que lleva puesto (el selector antes de leerlo): las piezas de inicio del motor de su tipo de armadura.
            var peso = heroe.Armadura == "pesada" || heroe.Armadura == "media" ? heroe.Armadura : "ligera";
            foreach (var hueco in new[] { "torso", "guantes", "pantalones", "botas" })
                Poner(personaje, piezas, AspectoDePiezas.Parte($"{hueco}_{peso}_inicio"), genero, null);
            return true;
        }
        // Pieza a pieza: cada hueco con el aspecto de su pieza del motor (AspectoDePiezas), teñida del color de su metal; un hueco vacío, la
        // pieza desnuda de Conquest. El arma, la puesta (o la de inicio de su clase). El casco, el de su pieza (o ninguno).
        foreach (var (hueco, vacia) in new[] { ("torso", "TorsoDefault"), ("guantes", "GlovesDefault"), ("pantalones", "PantsDefault"), ("botas", "BootsDefault") })
        {
            heroe.Piezas.TryGetValue(hueco, out var delMotor);
            Poner(personaje, piezas, AspectoDePiezas.Parte(delMotor) ?? vacia, genero, delMotor == null ? (Color?)null : TinteDeMetal(Metal(delMotor)));
        }
        heroe.Piezas.TryGetValue("casco", out var casco);
        var parteCasco = AspectoDePiezas.Parte(casco);
        if (parteCasco != null) { Poner(personaje, piezas, parteCasco, genero, TinteDeMetal(Metal(casco))); Quitar(personaje, piezas.hairParts, genero); }
        else Quitar(personaje, piezas.headParts, genero);
        heroe.Piezas.TryGetValue("arma", out var arma);
        Poner(personaje, piezas, AspectoDePiezas.Parte(arma ?? $"{heroe.Clase?.Id}_inicio"), genero, null);
        return true;
    }

    static void Poner(Transform personaje, Data.Avatar.AvatarPartDatabase piezas, string parte, Gender genero, Color? tinte)
    {
        if (string.IsNullOrEmpty(parte)) return;
        Data.Avatar.AvatarVisualUtils.ToggleArmorVisibilityByAvatarPartId(personaje, piezas, parte, genero);
        var def = Data.Avatar.AvatarVisualUtils.FindAvatarPartById(piezas, parte);
        if (def?.attachments == null) return;
        foreach (var a in def.attachments)
        {
            var hueso = genero == Gender.Male ? a.boneTargetMale : a.boneTargetFemale;
            var nombre = genero == Gender.Male ? a.prefabPathMale : a.prefabPathFemale;
            var h = string.IsNullOrEmpty(hueso) ? null : personaje.Find(hueso) ?? Data.Avatar.AvatarVisualUtils.FindDeepChild(personaje, hueso);
            var malla = h == null || string.IsNullOrEmpty(nombre) ? null : h.Find(nombre);
            if (malla == null) continue;
            foreach (var r in malla.GetComponentsInChildren<Renderer>(true))
            {
                if (tinte == null) { r.SetPropertyBlock(null); continue; }
                var bloque = new MaterialPropertyBlock();
                bloque.SetColor("_BaseColor", tinte.Value);
                bloque.SetColor("_Color", tinte.Value);
                r.SetPropertyBlock(bloque);
            }
        }
    }

    /// <summary>El metal de una pieza del motor (lo que sigue al hueco y al peso: `bronce_laminado`, `hierro_temprano`…).</summary>
    public static string Metal(string piezaDelMotor)
    {
        var p = (piezaDelMotor ?? "").Split('_');
        return p.Length < 3 ? "inicio" : string.Join("_", p, 2, p.Length - 2);
    }

    /// <summary>El tinte de un metal sobre la pieza (Víctor, 2026-11-14: «metales como tinte por ahora»); la de inicio, sin tinte.</summary>
    public static Color TinteDeMetal(string metal) => metal switch
    {
        "cobre" => new Color(1f, 0.74f, 0.58f),
        "bronce" => new Color(0.98f, 0.82f, 0.55f),
        "bronce_laminado" => new Color(1f, 0.88f, 0.62f),
        "hierro_temprano" => new Color(0.82f, 0.82f, 0.84f),
        "hierro" => new Color(0.76f, 0.79f, 0.84f),
        "acero" => new Color(0.86f, 0.9f, 0.96f),
        _ => Color.white,
    };

    /// <summary>El pelaje del caballo de una montura del motor (`HeroHorseVisual.SetCoat`): bayo, negro, castaño, bayo claro o pío; 0, el
    /// del prefab. Provisional (Víctor, 2026-11-14): el mismo caballo con otro material.</summary>
    public static byte PelajeDe(string montura) => montura switch
    {
        "montura_tiro" => 1, "montura_tesalia" => 2, "montura_estepa" => 3, "montura_nisea" => 4, "montura_escita" => 5, _ => 0,
    };

    /// <summary>Los huecos del aspecto que se eligieron vacíos (pelo, barba, cejas) se apagan: el maniquí de Synty trae una pieza puesta en cada uno.
    /// Lo usan el campamento y el héroe de la batalla en red (`HeroVisualAppearanceSystem`, con el aspecto que llega del servidor).</summary>
    public static void QuitarHuecosVacios(Transform personaje, Data.Avatar.AvatarPartDatabase piezas, string pelo, string barba, string cejas, Gender genero)
    {
        if (string.IsNullOrEmpty(pelo)) Quitar(personaje, piezas.hairParts, genero);
        if (string.IsNullOrEmpty(barba)) Quitar(personaje, piezas.beardParts, genero);
        if (string.IsNullOrEmpty(cejas)) Quitar(personaje, piezas.eyebrowsParts, genero);
    }

    /// <summary>Apaga lo que cuelga de los huesos de un hueco (pelo, barba, cejas): las piezas de esa lista para ese género.</summary>
    static void Quitar(Transform personaje, List<Data.Avatar.AvatarPartDefinition> lista, Gender genero)
    {
        if (lista == null) return;
        var huesos = new HashSet<string>();
        foreach (var def in lista)
            if (def?.attachments != null)
                foreach (var a in def.attachments)
                {
                    var hueso = genero == Gender.Male ? a.boneTargetMale : a.boneTargetFemale;
                    if (!string.IsNullOrEmpty(hueso)) huesos.Add(hueso);
                }
        foreach (var nombre in huesos)
        {
            var hueso = personaje.Find(nombre) ?? Data.Avatar.AvatarVisualUtils.FindDeepChild(personaje, nombre);
            if (hueso == null) continue;
            foreach (Transform hijo in hueso) hijo.gameObject.SetActive(false);
        }
    }
}
