// Los loadouts del héroe en la Base (paso 6 del bloque 4, tanda 2; Víctor, duda 27: con la tarjeta grande de tropas). A la izquierda, sus loadouts
// con su Liderazgo y cuál está activo; a la derecha, el elegido: su nombre, su riel de tarjetas de tropa con «✕» y «+», y «Guardar», «Activar»
// y «Borrar» (`guardarLoadout`, `borrarLoadout`; el activo es el que defiende la residencia en persona). Lo que no cabe en el Liderazgo no se
// ofrece. BronzeAgeClient: `htmlLoadouts` de `ui/panelHeroe.ts`.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed partial class PantallasDeMundo
    {
        string loadoutElegido;
        /// <summary>El loadout a medio editar: sus escuadras y su nombre (null: sin tocar, como está en el motor).</summary>
        List<string> borradorLoadout;
        string nombreLoadout;
        bool anadiendoALoadout;

        void PintarLoadouts(VisualElement v, Heroe h, Action<string> error, Label lineaError)
        {
            var lista = Dialogo.Caja("bienes-panel loadouts-lista");
            lista.name = "loadouts-lista";
            v.Add(lista);
            var editor = Dialogo.Caja("bienes-panel loadouts-editor");
            editor.name = "loadouts-editor";
            v.Add(editor);

            if (loadoutElegido != null && h.Loadouts.All(l => l.Id != loadoutElegido) && loadoutElegido != "") { loadoutElegido = null; borradorLoadout = null; }
            loadoutElegido ??= h.Loadouts.FirstOrDefault(l => l.Activo)?.Id ?? h.Loadouts.FirstOrDefault()?.Id ?? "";
            lista.Add(Dialogo.Texto("LOADOUTS", "base-kicker loadouts-titulo"));
            foreach (var l in h.Loadouts)
            {
                var id = l.Id;
                var b = new Button(() => { loadoutElegido = id; borradorLoadout = null; anadiendoALoadout = false; PintarBase(); }) { name = "loadout-" + l.Id };
                b.AddToClassList("loadouts-fila");
                b.EnableInClassList("activo", l.Id == loadoutElegido);
                b.Add(Dialogo.Texto(l.DisplayName, "loadouts-nombre"));
                var datos = Dialogo.Caja("loadouts-datos");
                datos.Add(Dialogo.Texto($"{l.SquadIds.Count} escuadras · Liderazgo {l.LiderazgoTotal}", "loadouts-detalle"));
                if (l.Activo) datos.Add(Dialogo.Texto("activo", "base-marca"));
                b.Add(datos);
                lista.Add(b);
            }
            var nuevo = Dialogo.Boton("+ Nuevo loadout", "boton-secundario base-boton-fila loadouts-nuevo", () =>
            {
                loadoutElegido = "";
                borradorLoadout = new List<string>();
                nombreLoadout = $"Loadout {h.Loadouts.Count + 1}";
                anadiendoALoadout = false;
                PintarBase();
            });
            nuevo.name = "loadout-nuevo";
            lista.Add(nuevo);

            // El editor.
            var actual = h.Loadouts.FirstOrDefault(l => l.Id == loadoutElegido);
            var escuadras = borradorLoadout ?? actual?.SquadIds.ToList() ?? new List<string>();
            var nombre = nombreLoadout ?? actual?.DisplayName ?? "";
            if (actual == null && borradorLoadout == null)
            {
                editor.Add(Dialogo.Texto("No tienes loadouts: crea uno con «+ Nuevo loadout».", "base-vacio"));
                editor.Add(lineaError);
                return;
            }
            var cab = Dialogo.Caja("loadouts-cab");
            var campo = new TextField { name = "nombre-loadout", value = nombre };
            campo.AddToClassList("entrada");
            campo.AddToClassList("loadouts-campo");
            campo.RegisterValueChangedCallback(ev => { nombreLoadout = ev.newValue; borradorLoadout ??= escuadras.ToList(); });
            cab.Add(campo);
            var todas = EscuadrasDelHeroe(h);
            int Coste(string id) => LiderazgoDe(todas.FirstOrDefault(s => s.Id == id)?.TropaId) ?? 0;
            var usado = escuadras.Sum(Coste);
            var lid = Dialogo.Texto($"Liderazgo {usado} / {h.LiderazgoBase}", "salida-liderazgo");
            lid.name = "liderazgo-loadout";
            lid.EnableInClassList("excedido", usado > h.LiderazgoBase);
            cab.Add(lid);
            if (actual?.Activo == true) cab.Add(Dialogo.Texto("activo", "base-marca"));
            editor.Add(cab);

            // El riel: las tarjetas grandes de tropa con su «✕», el «+» y los huecos hasta llenar la fila.
            var riel = Dialogo.Caja("loadouts-riel");
            riel.name = "riel-loadout";
            var candidatas = todas.Where(s => !escuadras.Contains(s.Id) && Coste(s.Id) <= h.LiderazgoBase - usado).ToList();
            var huecos = Math.Max(5, escuadras.Count + (candidatas.Count > 0 ? 1 : 0));
            for (int i = 0; i < huecos; i++)
            {
                if (i < escuadras.Count)
                {
                    var id = escuadras[i];
                    var s = todas.FirstOrDefault(x => x.Id == id);
                    var hueco = Dialogo.Caja("loadouts-hueco");
                    hueco.name = "en-loadout-" + id;
                    if (s != null && PiezasDeTropa.Tarjeta(s.TropaId, Coste(id), (int)s.Cantidad, (int)TopeDe(s), null, 132) is VisualElement tarjeta) hueco.Add(tarjeta);
                    else hueco.Add(Dialogo.Texto(s != null ? NombreDeEscuadra(s) : "escuadra que ya no es tuya", "base-vacio"));
                    var quitar = Dialogo.Boton("✕", "salida-hueco-quitar", () => { borradorLoadout = escuadras.Where(x => x != id).ToList(); nombreLoadout ??= nombre; PintarBase(); });
                    quitar.name = "quitar-de-loadout-" + id;
                    quitar.tooltip = "Quitar del loadout";
                    hueco.Add(quitar);
                    riel.Add(hueco);
                }
                else if (i == escuadras.Count && candidatas.Count > 0)
                {
                    var mas = Dialogo.Boton("+", "salida-hueco salida-hueco-mas loadouts-mas", () => { anadiendoALoadout = !anadiendoALoadout; PintarBase(); });
                    mas.name = "anadir-a-loadout";
                    mas.EnableInClassList("activo", anadiendoALoadout);
                    riel.Add(mas);
                }
                else riel.Add(Dialogo.Caja("salida-hueco salida-hueco-vacio loadouts-hueco"));
            }
            editor.Add(riel);
            if (anadiendoALoadout && candidatas.Count > 0)
            {
                var elegir = Dialogo.Caja("salida-elegir fila-envuelta");
                elegir.name = "elegir-para-loadout";
                foreach (var s in candidatas)
                {
                    var id = s.Id;
                    var b = new Button(() => { borradorLoadout = escuadras.Append(id).ToList(); nombreLoadout ??= nombre; anadiendoALoadout = false; PintarBase(); }) { name = "anadir-" + id };
                    b.AddToClassList("salida-candidata");
                    if (PiezasDeTropa.Miniatura(s.TropaId, 32) is VisualElement mini) b.Add(mini);
                    var textos = new VisualElement();
                    textos.Add(Dialogo.Texto(NombreDeEscuadra(s), "aviso-titulo"));
                    textos.Add(Dialogo.Texto($"{s.Cantidad} hombres · {Coste(id)} de Liderazgo", "campo-ayuda"));
                    b.Add(textos);
                    elegir.Add(b);
                }
                editor.Add(elegir);
            }

            // Guardar, Activar y Borrar.
            var botones = Dialogo.Caja("loadouts-botones");
            var cambiado = borradorLoadout != null || (nombreLoadout != null && nombreLoadout != actual?.DisplayName);
            Button guardar = null, activar = null, borrar = null;
            async System.Threading.Tasks.Task Mandar(bool? activo, Button boton)
            {
                var o = new OrdenGuardarLoadout { DisplayName = string.IsNullOrWhiteSpace(nombre) ? "Loadout" : nombre.Trim() };
                if (!string.IsNullOrEmpty(actual?.Id)) o.LoadoutId = actual.Id;
                if (activo != null) o.Activo = activo.Value;
                o.SquadIds.AddRange(escuadras);
                var r = await OrdenDelPanel(new Orden { GuardarLoadout = o }, error, () => { }, boton);
                if (r?.Resultado != Resultado.OrdenAceptada) return;
                borradorLoadout = null;
                nombreLoadout = null;
                if (actual == null) loadoutElegido = null; // el nuevo, con el id que le dé el motor
                PintarBase();
            }
            guardar = Dialogo.Boton("Guardar", "boton-primario base-reponer", async () => await Mandar(null, guardar));
            guardar.name = "guardar-loadout";
            guardar.SetEnabled(cambiado && usado <= h.LiderazgoBase);
            botones.Add(guardar);
            if (actual != null && !actual.Activo)
            {
                activar = Dialogo.Boton("Activar", "boton-secundario base-boton-fila", async () => await Mandar(true, activar));
                activar.name = "activar-loadout";
                activar.SetEnabled(usado <= h.LiderazgoBase);
                botones.Add(activar);
            }
            if (actual != null)
            {
                borrar = Dialogo.Boton("Borrar", "boton-secundario base-boton-fila", async () =>
                {
                    if (!await dialogo.Abrir($"Se borra el loadout «{actual.DisplayName}».", "¿Borrar el loadout?", "Borrar", peligro: true)) return;
                    var r = await OrdenDelPanel(new Orden { BorrarLoadout = new OrdenBorrarLoadout { LoadoutId = actual.Id } }, error, () => { }, borrar);
                    if (r?.Resultado == Resultado.OrdenAceptada) { loadoutElegido = null; borradorLoadout = null; nombreLoadout = null; PintarBase(); }
                });
                borrar.name = "borrar-loadout";
                botones.Add(borrar);
            }
            editor.Add(botones);
            editor.Add(Dialogo.Texto("El loadout activo es el que defiende tu residencia contigo dentro, y el que sale cargado al salir al mundo.", "base-nota"));
            editor.Add(lineaError);
        }
    }
}
