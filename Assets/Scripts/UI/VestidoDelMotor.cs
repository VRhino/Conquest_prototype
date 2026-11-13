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
        var sufijo = heroe.Armadura == "pesada" ? "H" : heroe.Armadura == "media" ? "M" : "L";
        foreach (var itemId in new[] { $"Tor{sufijo}ADef", $"Glo{sufijo}ADef", $"Pan{sufijo}ADef", $"Boot{sufijo}ADef" })
        {
            var item = ItemService.GetItemById(itemId);
            if (item != null && !string.IsNullOrEmpty(item.visualPartId))
                Data.Avatar.AvatarVisualUtils.ToggleArmorVisibilityByAvatarPartId(personaje, piezas, item.visualPartId, genero);
        }
        return true;
    }
}
