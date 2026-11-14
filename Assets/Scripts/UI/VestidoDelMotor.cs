using System.Collections.Generic;
using UnityEngine;

/// <summary>Viste un personaje modular de Synty con el héroe del motor (BronzeAge): el género y las piezas que guardó al crearlo (cabeza, pelo,
/// barba, cejas) y la armadura que lleva (ligera, media o pesada; las piezas de inicio de Conquest de ese tipo). Lo usan el selector de personaje
/// y el interior de un campamento. El equipo puesto pieza a pieza espera al paso 6 del bloque 4.</summary>
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
        if (string.IsNullOrEmpty(heroe.Pelo)) Quitar(personaje, piezas.hairParts, genero);
        if (string.IsNullOrEmpty(heroe.Barba)) Quitar(personaje, piezas.beardParts, genero);
        if (string.IsNullOrEmpty(heroe.Cejas)) Quitar(personaje, piezas.eyebrowsParts, genero);
        var sufijo = heroe.Armadura == "pesada" ? "H" : heroe.Armadura == "media" ? "M" : "L";
        foreach (var itemId in new[] { $"Tor{sufijo}ADef", $"Glo{sufijo}ADef", $"Pan{sufijo}ADef", $"Boot{sufijo}ADef" })
        {
            var item = ItemService.GetItemById(itemId);
            if (item != null && !string.IsNullOrEmpty(item.visualPartId))
                Data.Avatar.AvatarVisualUtils.ToggleArmorVisibilityByAvatarPartId(personaje, piezas, item.visualPartId, genero);
        }
        return true;
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
