using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the BronzeAge engine troop catalogue (<c>TROPAS_RECLUTABLES</c> in the engine's
/// <c>Docs/Mechanics/Heroe_Y_Tropa.md</c>, from the checkout next to this project) as <see cref="SquadData"/> assets in
/// Resources/Squads/BronzeAge/&lt;tropaId&gt;, registered in the SquadDatabase and out of recruitment. Run it again whenever
/// the engine catalogue changes: existing troops keep their assets (and GUIDs) and get the new numbers.
/// From the engine: id, name, tier (rarity), soldiers and the combat numbers. From Conquest (<see cref="Bases"/>): the base
/// each troop is copied from (look, animations, behaviour, abilities) and its formation families. Docs/Tropas_Del_Motor.md.
/// Also writes Resources/Squads/BronzeAge/catalogo.json with the catalogue version, which a battle ticket must match.
/// Batch: <c>-executeMethod TropasDelMotor.Importar</c>.
/// </summary>
public static class TropasDelMotor
{
    const string Carpeta = "Assets/Resources/Squads/BronzeAge";
    const string BaseDeEscuadras = "Assets/Resources/Squads/SquadDatabase.asset";
    const string Catalogo = "../BronzeAgeFase0/Docs/Mechanics/Heroe_Y_Tropa.md";
    const string Progresion = Carpeta + "/Progresion_Del_Motor.asset";

    /// <summary>Squad ability unlock levels (D8, the engine's <c>MILITAR.nivelesHabilidadEscuadra</c>).</summary>
    static readonly int[] NivelesDeHabilidad = { 4, 7, 10 };
    /// <summary>The engine's squad levels (1 to 10) and what each level above the first adds to health and damage.</summary>
    const int NivelMaximo = 10;
    const float PorNivel = 0.015f;

    // ---- What Conquest decides per troop (proposal pending the author: Docs/Tropas_Del_Motor.md) ----

    enum Base { Escuderos, Lanceros, Piqueros, Arqueros, CaballeriaDeLanza, ArquerosACaballo }

    static readonly Dictionary<Base, string> IdDeBase = new()
    {
        { Base.Escuderos, "sqd01" }, { Base.Lanceros, "spm01" }, { Base.Piqueros, "pkm01" },
        { Base.Arqueros, "arc01" }, { Base.CaballeriaDeLanza, "lcv01" }, { Base.ArquerosACaballo, "hac01" },
    };

    const FormationType L = FormationType.Line, D = FormationType.Dispersed, C = FormationType.Wedge,
        M = FormationType.ShieldWall, T = FormationType.Testudo;

    /// <summary>
    /// BronzeAge (rama bronzeage/tropas-en-batalla): tropas con modelo y animaciones propias. El visual de cada soldado es
    /// «visual_X», con X una de sus figuras (A, B, C…; los prefabs y su registro los monta BronzeAge &gt; Tropas), y la
    /// ventana de golpe sale del contacto real de su clip de ataque (s). La formación de una familia puede venir de otra
    /// base (la línea de los piqueros para la milicia), copiada y alargada a sus soldados.
    /// </summary>
    static readonly Dictionary<string, (string visual, int figuras, float golpeEmpieza, float golpeDura, float duracionAtaque)> Aspecto = new()
    {
        // estocada alta/baja de 40 fotogramas (1,30 s); la punta llega al contacto en el fotograma 15 (0,47 s)
        { "milicia_lanceros", ("BronzeAge_Lancero", 4, 0.47f, 0.15f, 1.30f) },
        // las levas: tajos (y estocadas de la horca) de 40 fotogramas con el primer contacto en el 15, como la milicia
        { "lenadores", ("BronzeAge_Lenador", 4, 0.47f, 0.15f, 1.30f) },
        { "mineros", ("BronzeAge_Minero", 4, 0.47f, 0.15f, 1.30f) },
        { "granjeros", ("BronzeAge_Granjero", 4, 0.47f, 0.15f, 1.30f) },
    };
    /// <summary>
    /// PROVISIONAL (Víctor, 18-11, «vamos con a para probar»): alcance de ataque forzado para las pruebas con la lanza de
    /// 3 m de la milicia (el contacto de sus estocadas queda a unos 3,05 m). El dato definitivo es el «alcance» del motor:
    /// cuando el motor lo mande, quitar esta entrada.
    /// </summary>
    static readonly Dictionary<string, float> AlcanceDePrueba = new() { { "milicia_lanceros", 3.0f } };
    /// <summary>Troops whose borrowed formation keeps the base's ranks and widens them instead of adding ranks behind
    /// (Víctor, 18-11: the militia's line like the pikemen's, a kneeling front rank and a standing rear rank).</summary>
    static readonly HashSet<string> EnsancharFilas = new() { "milicia_lanceros" };
    // (18-11) cuánto se corre la fila de detrás respecto a la de delante, en pasos. La milicia 0,35: con su lanza de pie a la
    // cadera, a 0,5 pasaba rozando a los de rodillas; a 0,35 el asta queda a 17,7 cm de cualquier parte de los de delante,
    // parados o atacando (PruebaTropasAnimator, malla real), y se conserva el tresbolillo
    static readonly Dictionary<string, float> MedioPasoFilaTrasera = new() { { "milicia_lanceros", 0.35f } };
    static readonly Dictionary<string, Dictionary<FormationType, Base>> FormacionDeOtraBase = new()
    {
        { "milicia_lanceros", new() { { FormationType.Line, Base.Piqueros } } },
    };

    /// <summary>Base and formation families (besides rest) of each troop. A troop that is not here is not imported.</summary>
    static readonly Dictionary<string, (Base baseDeLaTropa, FormationType[] familias)> Bases = new()
    {
        { "milicia_lanceros", (Base.Lanceros, new[] { L }) },          // (Víctor, 18-11: solo la línea de los piqueros)
        { "lenadores", (Base.Lanceros, new[] { D }) },
        { "granjeros", (Base.Lanceros, new[] { D }) },
        { "mineros", (Base.Lanceros, new[] { D }) },
        { "lanceros_mimbre", (Base.Lanceros, new[] { L, D, C }) },
        { "espadachines_cobre", (Base.Escuderos, new[] { L, D, C }) },
        { "hacheros_ligeros", (Base.Escuderos, new[] { L, D, C }) },
        { "espadachines_bronce", (Base.Escuderos, new[] { M, T, L, D }) },
        { "lanceros_pesados", (Base.Lanceros, new[] { L, D, C, M }) },
        { "hacheros_armados", (Base.Escuderos, new[] { L, D, C }) },
        { "honderos", (Base.Arqueros, new[] { D, L }) },
        { "escaramuzadores_jabalina", (Base.Arqueros, new[] { D, L }) },
        { "arqueros", (Base.Arqueros, new[] { D, L }) },
        { "arqueros_compuesto", (Base.Arqueros, new[] { D, L }) },
        { "exploradores_caballo", (Base.CaballeriaDeLanza, new[] { L, C, D }) },
        // carros_guerra: no chariots in Conquest yet; the engine numbers with the cavalry look until there is art (Víctor, 2026-11-16).
        { "carros_guerra", (Base.CaballeriaDeLanza, new[] { L, C, D }) },
        { "jinetes_asirios", (Base.CaballeriaDeLanza, new[] { L, C, D }) },
        { "guerreros_filisteos", (Base.Escuderos, new[] { M, T, L, D }) },
        { "shardana", (Base.Escuderos, new[] { M, T, L, D }) },
        { "hequetai", (Base.Lanceros, new[] { L, D, C, M }) },
        { "honderos_rodios", (Base.Arqueros, new[] { D, L }) },
        { "peltastas", (Base.Arqueros, new[] { D, L }) },
        { "jinetes_escitas", (Base.ArquerosACaballo, new[] { L, C, D }) },
        { "hoplitas_ciudadanos", (Base.Lanceros, new[] { L, D, C, M }) },
        { "caballeria_asiria", (Base.CaballeriaDeLanza, new[] { L, C, D }) },
        { "arqueros_escitas", (Base.Arqueros, new[] { D, L }) },
        { "espartiatas", (Base.Lanceros, new[] { L, D, C, M }) },
        { "epilektoi", (Base.Lanceros, new[] { L, D, C, M }) },
        { "inmortales", (Base.Arqueros, new[] { D, L }) },
        { "peltastas_ificrates", (Base.Arqueros, new[] { D, L }) },
        { "caballeria_tesalia", (Base.CaballeriaDeLanza, new[] { L, C, D }) },
        { "caballeria_noble_persa", (Base.CaballeriaDeLanza, new[] { L, C, D }) },
        { "prodromos", (Base.CaballeriaDeLanza, new[] { L, C, D }) },
        { "falangitas", (Base.Piqueros, new[] { L, D }) },
        { "hipaspistas", (Base.Lanceros, new[] { L, D, C, M }) },
        { "companeros", (Base.CaballeriaDeLanza, new[] { L, C, D }) },
        { "agrianos", (Base.Arqueros, new[] { D, L }) },
    };

    /// <summary>(18-11, Víctor) el reposo de las tres levas de herramienta, igual de desordenado pero más junto: la separación
    /// × <see cref="CompactarReposo"/> y el ruido el que deje a dos soldados vecinos a <see cref="ReposoSeparacionMinima"/> como
    /// poco (separación − 2·ruido). Con 0,5 y 0,6 m: 1,2 m y 0,3 m (antes 2,4 y 0,8; vecino medio 2,18 → 1,12 m, 15 → 7,5 m de lado).</summary>
    static readonly HashSet<string> ReposoCompacto = new() { "lenadores", "mineros", "granjeros" };
    const float CompactarReposo = 0.5f, ReposoSeparacionMinima = 0.6f;
    static RestFormationSetup.Profile ReposoDeLeva()
    {
        float separacion = 2.4f * CompactarReposo;
        return RestFormationSetup.Loose(separacion, Mathf.Min(0.8f * CompactarReposo, (separacion - ReposoSeparacionMinima) / 2f), 35f);
    }

    /// <summary>How each base stands at rest: levies loose, the rest in blocks.</summary>
    static RestFormationSetup.Profile Reposo(string tropaId, Base b) =>
        ReposoCompacto.Contains(tropaId) ? ReposoDeLeva()
        : b switch
        {
            Base.Escuderos => RestFormationSetup.Block(5, 1.2f, 0.15f, 8f),
            Base.Lanceros => RestFormationSetup.Block(5, 1.2f, 0.15f, 8f),
            Base.Piqueros => RestFormationSetup.Block(8, 1.0f, 0.05f, 2f),
            Base.Arqueros => RestFormationSetup.Block(6, 1.4f, 0.15f, 6f),
            _ => RestFormationSetup.Block(4, 2.2f, 0.2f, 6f),
        };

    // ---- The engine catalogue ----

    [Serializable] class Terna { public float perforante, cortante, contundente; }
    [Serializable] class Combate { public float vida; public Terna dano, penetracion, defensa, alivio; public float bloqueo, cadencia, alcance, precision, municion; }
    [Serializable] class Tropa { public string id, nombre; public int tier, unidadesPorDefecto; public Combate combate; }
    [Serializable] class Lista { public Tropa[] tropas; }
    [Serializable] class Version { public int version; }
    [Serializable] class Importado { public int version; public string[] tropas; }

    static string Bloque(string doc, string nombre)
    {
        var m = Regex.Match(doc, $@"<!-- parametros:{nombre} -->\s*```json\s*(.*?)```", RegexOptions.Singleline);
        if (!m.Success) throw new InvalidDataException($"Falta el bloque {nombre} en el catálogo del motor.");
        return m.Groups[1].Value;
    }

    /// <summary>I levy … V elite, as Conquest rarities (Trabajo_En_Conquest §2.4).</summary>
    static SquadRarity Rareza(int tier) => tier switch
    {
        1 => SquadRarity.levy_tier,
        2 => SquadRarity.trained_tier,
        3 => SquadRarity.veteran_tier,
        4 => SquadRarity.elite_tier,
        _ => SquadRarity.legendary_tier,
    };

    /// <summary>El fondo de la tarjeta por el tier, del color de su línea (SquadUtils.RarityInfo): gris, verde, azul, morado
    /// o dorado. La copia de la tropa base traía el de la base (el verde de los Spearmen) y todas salían verdes (Víctor,
    /// 18-11). Si falta el sprite, se queda el que tenga.</summary>
    static Sprite Fondo(SquadRarity rareza) => AssetDatabase.LoadAssetAtPath<Sprite>(rareza switch
    {
        SquadRarity.peasant_tier or SquadRarity.levy_tier or SquadRarity.conscript_tier => "Assets/Art/UI/units/common_background.png",
        SquadRarity.trained_tier or SquadRarity.seasoned_tier => "Assets/Art/UI/units/uncommon_background.png",
        SquadRarity.veteran_tier or SquadRarity.hardened_tier => "Assets/Art/UI/units/rare_background.png",
        SquadRarity.elite_tier or SquadRarity.master_tier => "Assets/Art/UI/units/epic_background.png",
        SquadRarity.legendary_tier => "Assets/Art/UI/units/legendary_background.png",
        _ => "",
    });

    [MenuItem("Tools/BronzeAge/Importar tropas del motor")]
    public static void Importar()
    {
        string doc = File.ReadAllText(Path.GetFullPath(Catalogo));
        var tropas = JsonUtility.FromJson<Lista>("{\"tropas\":" + Bloque(doc, "TROPAS_RECLUTABLES") + "}").tropas;
        int version = JsonUtility.FromJson<Version>(Bloque(doc, "VERSION_CATALOGO_TROPAS")).version;
        var bases = Resources.LoadAll<SquadData>("Squads").Where(s => IdDeBase.ContainsValue(s.id)).ToDictionary(s => s.id);
        var baseDeEscuadras = AssetDatabase.LoadAssetAtPath<SquadDatabase>(BaseDeEscuadras);
        var importadas = new List<string>();
        var progresion = ProgresionDelMotor();

        foreach (var t in tropas)
        {
            if (!Bases.TryGetValue(t.id, out var plan))
            {
                Debug.LogWarning($"[TropasDelMotor] {t.id}: sin base en Conquest, no se importa.");
                continue;
            }
            var origen = bases[IdDeBase[plan.baseDeLaTropa]];
            string carpeta = $"{Carpeta}/{t.id}";
            string ruta = $"{carpeta}/{t.id}.asset";
            var tropa = AssetDatabase.LoadAssetAtPath<SquadData>(ruta);
            if (tropa == null)
            {
                Directory.CreateDirectory(carpeta);
                AssetDatabase.Refresh();
                tropa = UnityEngine.Object.Instantiate(origen);
                tropa.name = t.id;
                tropa.restFormation = null;
                AssetDatabase.CreateAsset(tropa, ruta);
                if (origen.meleeData != null) tropa.meleeData = Copiar(origen.meleeData, $"{carpeta}/{t.id}_Melee.asset");
                if (origen.rangedData != null) tropa.rangedData = Copiar(origen.rangedData, $"{carpeta}/{t.id}_Ranged.asset");
            }
            tropa.id = t.id;
            tropa.squadName = t.nombre;
            tropa.unitCount = t.unidadesPorDefecto;
            tropa.rarity = Rareza(t.tier);
            tropa.background = Fondo(tropa.rarity) ?? tropa.background;
            tropa.availableForRecruitmentByDefault = false;
            tropa.progressionData = progresion;
            AplicarCombate(tropa, t.combate);
            if (Aspecto.TryGetValue(t.id, out var aspecto))
            {
                tropa.visualPrefabName = aspecto.visual;
                tropa.visualVariants = aspecto.figuras;
                if (tropa.meleeData != null)
                {
                    tropa.meleeData.strikeWindowStart = aspecto.golpeEmpieza;
                    tropa.meleeData.strikeWindowDuration = aspecto.golpeDura;
                    tropa.meleeData.attackAnimationDuration = aspecto.duracionAtaque;
                    if (AlcanceDePrueba.TryGetValue(t.id, out var alcance)) tropa.meleeData.attackRange = alcance;
                    EditorUtility.SetDirty(tropa.meleeData);
                }
            }
            tropa.formations = Formaciones(tropa, plan.familias, bases.Values, carpeta);
            if (tropa.restFormation == null)
                tropa.restFormation = RestFormationSetup.Create(tropa, ruta, Reposo(t.id, plan.baseDeLaTropa));
            else
                Ampliar(tropa.restFormation, tropa.unitCount);
            if (ReposoCompacto.Contains(t.id))             // el reposo ya creado también se compacta en cada importación
            {
                var p = ReposoDeLeva();
                tropa.restFormation.spacing = p.spacing; tropa.restFormation.noiseRadius = p.noiseRadius;
                EditorUtility.SetDirty(tropa.restFormation);
            }
            EditorUtility.SetDirty(tropa);
            Registrar(baseDeEscuadras, tropa);
            importadas.Add(t.id);
        }

        File.WriteAllText($"{Carpeta}/catalogo.json", JsonUtility.ToJson(new Importado { version = version, tropas = importadas.ToArray() }, true));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TropasDelMotor] {importadas.Count} de {tropas.Length} tropas importadas, versión {version} del catálogo.");
    }

    /// <summary>
    /// The progression every engine troop shares (the author's decision of 2026-11-13, Trabajo_En_Conquest §2.6): the level of
    /// the ticket, 1 to 10, adds 1.5 % to health and 1.5 % to damage per level above the first; defense and speed do not
    /// change. Abilities unlock at levels 4, 7 and 10 (D8).
    /// </summary>
    static SquadProgressionData ProgresionDelMotor()
    {
        var progresion = AssetDatabase.LoadAssetAtPath<SquadProgressionData>(Progresion);
        if (progresion == null)
        {
            progresion = ScriptableObject.CreateInstance<SquadProgressionData>();
            AssetDatabase.CreateAsset(progresion, Progresion);
        }
        AnimationCurve plana() => AnimationCurve.Constant(1, SquadProgressionCurves.MaxLevel, 1f);
        AnimationCurve porNivel() => AnimationCurve.Linear(1, 1f, NivelMaximo, 1f + PorNivel * (NivelMaximo - 1));
        (progresion.healthCurve, progresion.damageCurve) = (porNivel(), porNivel());
        (progresion.defenseCurve, progresion.speedCurve) = (plana(), plana());
        progresion.abilityUnlockLevels = NivelesDeHabilidad;
        EditorUtility.SetDirty(progresion);
        return progresion;
    }

    /// <summary>The engine numbers. Relief comes in percent; ranged troops shoot once per <c>cadencia</c> seconds.</summary>
    static void AplicarCombate(SquadData tropa, Combate c)
    {
        tropa.baseHealth = Mathf.RoundToInt(c.vida);
        tropa.piercingDefense = c.defensa.perforante;
        tropa.slashingDefense = c.defensa.cortante;
        tropa.bluntDefense = c.defensa.contundente;
        tropa.pierceRelief = c.alivio.perforante / 100f;
        tropa.slashRelief = c.alivio.cortante / 100f;
        tropa.bluntRelief = c.alivio.contundente / 100f;
        tropa.block = Mathf.RoundToInt(c.bloqueo);
        if (tropa.rangedData != null)
        {
            var r = tropa.rangedData;
            (r.piercingDamage, r.slashingDamage, r.bluntDamage) = (c.dano.perforante, c.dano.cortante, c.dano.contundente);
            (r.piercingPenetration, r.slashingPenetration, r.bluntPenetration) = (c.penetracion.perforante, c.penetracion.cortante, c.penetracion.contundente);
            r.range = c.alcance;
            r.accuracy = c.precision;
            r.fireRate = c.cadencia > 0f ? 1f / c.cadencia : r.fireRate;
            r.ammo = Mathf.RoundToInt(c.municion);
            EditorUtility.SetDirty(r);
        }
        if (tropa.meleeData != null)
        {
            var m = tropa.meleeData;
            // A ranged troop keeps its base melee for when it runs out of ammunition; the engine numbers are for its shot.
            if (tropa.rangedData == null)
            {
                (m.piercingDamage, m.slashingDamage, m.bluntDamage) = (c.dano.perforante, c.dano.cortante, c.dano.contundente);
                (m.piercingPenetration, m.slashingPenetration, m.bluntPenetration) = (c.penetracion.perforante, c.penetracion.cortante, c.penetracion.contundente);
                m.attackRange = Mathf.Max(m.attackRange, c.alcance);
                m.attackInterval = c.cadencia;
            }
            EditorUtility.SetDirty(m);
        }
    }

    /// <summary>
    /// The formation of each family: the base's own if it has it, else another base's. A template formation with fewer
    /// slots than the troop has soldiers gets its own copy, deepened with more rows behind (otherwise the extra soldiers
    /// never spawn).
    /// </summary>
    static FormationDefinition[] Formaciones(SquadData tropa, FormationType[] familias, IEnumerable<SquadData> bases, string carpeta)
    {
        var resultado = new List<FormationDefinition>();
        var donantes = bases.SelectMany(b => b.formations ?? Array.Empty<FormationDefinition>()).Where(f => f != null).ToList();
        foreach (var familia in familias)
        {
            var propia = (tropa.formations ?? Array.Empty<FormationDefinition>()).FirstOrDefault(f => f != null && f.formationType == familia);
            var formacion = propia ?? donantes.FirstOrDefault(f => f.formationType == familia);
            string sufijo = familia.ToString();
            if (FormacionDeOtraBase.TryGetValue(tropa.id, out var otras) && otras.TryGetValue(familia, out var otraBase))
            {
                // la de esa base (p. ej. la línea de los piqueros), en su propia copia alargada
                var deLaBase = bases.FirstOrDefault(b => b.id == IdDeBase[otraBase]);
                formacion = deLaBase?.formations?.FirstOrDefault(f => f != null && f.formationType == familia) ?? formacion;
                sufijo = $"{familia}_{otraBase}";
            }
            if (formacion == null)
            {
                Debug.LogWarning($"[TropasDelMotor] {tropa.id}: Conquest no tiene formación {familia}.");
                continue;
            }
            if (EnsancharFilas.Contains(tropa.id) && FormacionDeOtraBase.TryGetValue(tropa.id, out var anchas) && anchas.ContainsKey(familia))
            {
                // its own copy, rebuilt from the base's every import: same ranks, each one wider. Named after the formation only
                // (milicia_lanceros_Line), not its base (Víctor, 18-11: «el piqueros en el nombre no me dice nada»)
                string ruta = $"{carpeta}/Formations/{tropa.id}_{familia}.asset";
                var copia = AssetDatabase.LoadAssetAtPath<FormationDefinition>(ruta);
                if (copia == null)
                {
                    Directory.CreateDirectory($"{carpeta}/Formations");
                    AssetDatabase.Refresh();
                    copia = UnityEngine.Object.Instantiate(formacion);
                    AssetDatabase.CreateAsset(copia, ruta);
                }
                Ensanchar(formacion, copia, tropa.unitCount, MedioPasoFilaTrasera.TryGetValue(tropa.id, out var mp) ? mp : (float?)null);
                resultado.Add(copia);
                continue;
            }
            if (formacion.Capacity < tropa.unitCount)
            {
                string ruta = $"{carpeta}/Formations/{tropa.id}_{sufijo}.asset";
                var copia = AssetDatabase.LoadAssetAtPath<FormationDefinition>(ruta);
                if (copia == null)
                {
                    Directory.CreateDirectory($"{carpeta}/Formations");
                    AssetDatabase.Refresh();
                    copia = UnityEngine.Object.Instantiate(formacion);
                    AssetDatabase.CreateAsset(copia, ruta);
                }
                Ampliar(copia, tropa.unitCount);
                formacion = copia;
            }
            resultado.Add(formacion);
        }
        return resultado.ToArray();
    }

    /// <summary>Rebuilds <paramref name="copia"/> from the largest template of <paramref name="origen"/> with the same ranks
    /// (row, role, depth, the rear ranks' sideways offset) and <paramref name="soldados"/> spread over them, front ranks
    /// first (25 in 2 ranks: 13 in front, 12 behind).</summary>
    static void Ensanchar(FormationDefinition origen, FormationDefinition copia, int soldados, float? medioPaso = null)
    {
        if (origen.layout != FormationLayoutKind.Template || origen.templates == null || origen.templates.Length == 0) return;
        var plantilla = origen.templates.OrderByDescending(t => t.slots.Length).First();
        var filas = plantilla.slots.GroupBy(s => s.row).OrderBy(g => g.Key).Select(g => g.OrderBy(s => s.position.x).ToArray()).ToArray();
        var primera = filas[0];
        float paso = primera.Length > 1 ? (primera[primera.Length - 1].position.x - primera[0].position.x) / (primera.Length - 1) : 1f;
        var slots = new List<FormationSlot>();
        int enLaPrimera = soldados / filas.Length + (soldados % filas.Length > 0 ? 1 : 0);
        for (int f = 0; f < filas.Length; f++)
        {
            int enEsta = soldados / filas.Length + (f < soldados % filas.Length ? 1 : 0);
            // keeps each rank's sideways offset from the front one (the half column of the pikemen's rear rank). A rank with
            // one soldier fewer or more is already shifted half a column by being centred: that half comes off, or 13 in front
            // and 12 behind end up right behind each other (the rear lances through the front rank's heads, Víctor 18-11).
            float desfase = filas[f].Average(s => s.position.x) - primera.Average(s => s.position.x);
            if ((enLaPrimera - enEsta) % 2 != 0) desfase -= 0.5f * paso;
            // el medio paso pedido es el desfase FINAL de la fila respecto a la de delante: si esta fila tiene un soldado más o
            // menos, el centrado ya pone medio paso, que se descuenta (18-11: se ponía encima y quedaba a 0,85 = -0,15)
            if (f > 0 && medioPaso.HasValue) desfase = medioPaso.Value * paso - ((enLaPrimera - enEsta) % 2 != 0 ? 0.5f * paso : 0f);
            float centro = primera.Average(s => s.position.x) + desfase;
            for (int i = 0; i < enEsta; i++)
            {
                var nuevo = filas[f][0];
                nuevo.position = new Vector2(centro + (i - (enEsta - 1) * 0.5f) * paso, filas[f][0].position.y);
                slots.Add(nuevo);
            }
        }
        copia.layout = origen.layout;
        copia.spacing = origen.spacing;
        copia.postures = origen.postures;
        copia.templates = new[] { new FormationTemplate { minUnits = 0, slots = slots.ToArray() } };
        string problema = copia.Validate();
        if (problema != null) Debug.LogWarning($"[TropasDelMotor] {copia.name}: {problema}");
        EditorUtility.SetDirty(copia);
    }

    /// <summary>Deepens the largest template of a formation with rows like its last one until it fits <paramref name="soldados"/>.</summary>
    static void Ampliar(FormationDefinition formacion, int soldados)
    {
        if (formacion.layout != FormationLayoutKind.Template || formacion.Capacity >= soldados || formacion.templates.Length == 0)
            return;
        var plantilla = formacion.templates.OrderByDescending(t => t.slots.Length).First();
        var slots = plantilla.slots.ToList();
        int ultima = slots.Max(s => s.row);
        var filaFinal = slots.Where(s => s.row == ultima).ToArray();
        var anteriores = slots.Where(s => s.row == ultima - 1).ToArray();
        float paso = anteriores.Length > 0 ? anteriores.Average(s => s.position.y) - filaFinal.Average(s => s.position.y) : 1f;
        if (paso <= 0f) paso = 1f;
        for (int k = 1; slots.Count < soldados; k++)
            foreach (var s in filaFinal)
            {
                if (slots.Count >= soldados) break;
                var nuevo = s;
                nuevo.row = ultima + k;
                nuevo.position = new Vector2(s.position.x, s.position.y - paso * k);
                slots.Add(nuevo);
            }
        plantilla.slots = slots.ToArray();
        EditorUtility.SetDirty(formacion);
    }

    static void Registrar(SquadDatabase baseDeEscuadras, SquadData tropa)
    {
        if (baseDeEscuadras.allSquads.Contains(tropa))
            return;
        if (baseDeEscuadras.allSquads.Exists(d => d && d.id == tropa.id))
            throw new InvalidOperationException("Duplicate squad ID: " + tropa.id);
        baseDeEscuadras.allSquads.Add(tropa);
        EditorUtility.SetDirty(baseDeEscuadras);
    }

    static T Copiar<T>(T origen, string ruta) where T : ScriptableObject
    {
        var copia = UnityEngine.Object.Instantiate(origen);
        AssetDatabase.CreateAsset(copia, ruta);
        return copia;
    }
}
