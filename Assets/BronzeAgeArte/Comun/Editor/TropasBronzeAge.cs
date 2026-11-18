// Monta las tropas de la Edad del Bronce para la batalla (rama bronzeage/tropas-en-batalla). Menús:
//   BronzeAge > Tropas > Montar las cuatro tropas      (la milicia y las tres levas: leñadores, mineros y granjeros)
//   BronzeAge > Tropas > Montar milicia de lanceros    (solo la milicia)
// Cada tropa queda como una escuadra de Conquest (Squires), en Assets/Resources/Squads/BronzeAge/<id>/ (ver
// RutasBronzeAge): Animations/ con sus clips (FBX, Generic, solo nuestros) y su Animator, <id>_GO_Squeleton.prefab (el
// visual de menú y vista previa), UnitPrefab_ECS_<id>.prefab (la unidad ECS, copiada de la del Spearmen con su SquadData)
// y su icono y miniatura; el SquadData (<id>.asset, lo crea Tools > BronzeAge > Importar tropas del motor) apunta a ellos.
// 1. Animator: reposo, andar y correr por MoveSpeed (las coordenadas del juego: 0 / 3 / 5), los dos ataques al azar
//    cuando IsAttacking (a la velocidad AttackSpeed), golpe recibido (para los cuatro HitReactionType) y muerte. La
//    milicia, uno para sus cuatro figuras; las levas, uno por tipo de arma (una mano: tajo vertical o diagonal; horca:
//    estocada alta o baja), hecho con los clips de la primera figura de ese tipo, y un AnimatorOverrideController para cada
//    otra figura con sus clips (cada herramienta se lleva a su manera: al hombro o en la mano).
// 2. Prefabs de batalla <Tropa>_A..D en BattleVisuals/ de la carpeta de la escuadra (con sus modelos en Models/): el
//    modelo con su atlas y su tono de piel, los componentes que pide la batalla (EntityVisualSync, UnitAnimationAdapter,
//    UnitAnimatorController, NavMeshAgent y cápsula copiados del Spearmen), la caja de golpe en la parte del arma que
//    golpea y el Detector, en la capa del Spearmen.
// 3. Registro: «<clave>_A..D» en VisualPrefabConfig (al final de la lista: no cambia el visual por defecto de ningún tipo),
//    con visualPrefab = el GO_Squeleton de la escuadra y battleVisualPrefab = el prefab de batalla de la figura.
// Solo crea o rehace sus propios ficheros; del juego toca la lista de VisualPrefabConfig (añade o actualiza sus entradas) y
// en el SquadData de nuestras escuadras, solo icon, unitImage y prefab.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConquestTactics.Animation;
using ConquestTactics.Visual;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace BronzeAge.Batalla
{
    public static class TropasBronzeAge
    {
        const string SALIDA_VIEJA = "Assets/Prefabs/BattleVisuals/BronzeAge";      // (donde iban antes del 18-11)
        const string REFERENCIA = "Assets/Prefabs/BattleVisuals/Spearmen.prefab";
        const string ECS_REFERENCIA = "Assets/Resources/Squads/Spearmen/UnitPrefab_ECS_Spearmen.prefab";
        const string CONFIG = "Assets/ScriptableObjects/VisualPrefabConfig.asset";
        const string DETECTOR = "Assets/Prefabs/Detector.prefab";
        public const string VISUAL_LANCEROS = "BronzeAge_Lancero";

        /// <summary>Una figura: su modelo, su tono de piel, su tipo de arma y sus clips.</summary>
        public readonly struct Figura
        {
            public readonly string letra, modelo, arma, reposo, andar, correr, golpe, muerte;
            public readonly int piel;
            public readonly string[] ataques;
            /// <summary>Las posturas de fila (como los piqueros), o null: de rodillas (Stance 7), su estocada, de pie detrás
            /// (Stance 8) y su estocada.</summary>
            public readonly string[] fila;
            public Figura(string letra, int piel, string modelo, string arma, string reposo, string andar, string correr, string[] ataques, string golpe, string muerte, string[] fila = null)
            {
                this.letra = letra; this.piel = piel; this.modelo = modelo; this.arma = arma; this.reposo = reposo;
                this.andar = andar; this.correr = correr; this.ataques = ataques; this.golpe = golpe; this.muerte = muerte; this.fila = fila;
            }
            /// <summary>En el orden de los estados del Animator (para emparejar los clips de un override).</summary>
            public string[] Clips => new[] { reposo, andar, correr }.Concat(ataques).Concat(new[] { golpe, muerte }).Concat(fila ?? new string[0]).ToArray();
        }

        static Figura Lancero(string letra, int piel) =>
            new Figura(letra, piel, $"Lancero_{letra}", "Lanza", "Lancero_Reposo", "Lancero_Andar", "Lancero_Correr",
                new[] { "Lancero_Estocada_Alta", "Lancero_Estocada_Baja" }, "Lancero_Golpe", "Lancero_Muerte",
                new[] { "Lancero_Rodilla", "Lancero_Estocada_Rodilla", "Lancero_Fila_De_Pie", "Lancero_Estocada_Fila" });
        static Figura UnaMano(string tropa, string letra, int piel, string modelo, string reposo, string vertical, string diagonal) =>
            new Figura(letra, piel, modelo, "UnaMano", reposo, $"{tropa}_{letra}_Andar_1m", $"{tropa}_{letra}_Correr_1m",
                new[] { vertical, diagonal }, $"{tropa}_{letra}_Golpe_1m", $"{tropa}_{letra}_Muerte_1m");
        static Figura Horca(string letra, int piel) =>
            new Figura(letra, piel, $"Granjero_{letra}", "Horca", "Granjero_Reposo_Horca", $"Granjero_{letra}_Andar", $"Granjero_{letra}_Correr",
                new[] { "Granjero_Estocada_Alta", "Granjero_Estocada_Baja" }, $"Granjero_{letra}_Golpe", $"Granjero_{letra}_Muerte");

        /// <summary>Las cuatro tropas: id de la escuadra, clave del registro, nombre, prefijo de sus prefabs y figuras A..D.</summary>
        public static readonly (string id, string clave, string nombre, string prefijo, Figura[] figuras)[] TROPAS =
        {
            ("milicia_lanceros", VISUAL_LANCEROS, "Lancero (milicia)", "Lancero", "ABCD".Select((l, i) => Lancero(l.ToString(), i + 1)).ToArray()),
            ("lenadores", "BronzeAge_Lenador", "Leñador (leva)", "Lenador", "ABCD".Select((l, i) => UnaMano("Lenador", l.ToString(), i + 1, $"Lenador_{l}_1m",
                "Lenador_Reposo_1m", "Lenador_Tajo_Vertical_B_1m_v3", "Lenador_Tajo_Diagonal_1m")).ToArray()),
            ("mineros", "BronzeAge_Minero", "Minero (leva)", "Minero", "ABCD".Select((l, i) => UnaMano("Minero", l.ToString(), i + 1, $"Minero_{l}_1m",
                "Minero_Reposo_1m", "Minero_Golpe_Vertical_1m", "Minero_Golpe_Diagonal_1m")).ToArray()),
            ("granjeros", "BronzeAge_Granjero", "Granjero (leva)", "Granjero", new[] {
                Horca("A", 1),
                UnaMano("Granjero", "B", 2, "Granjero_B", "Granjero_Azada_Reposo", "Granjero_Azada_Golpe_Vertical", "Granjero_Azada_Golpe_Diagonal"),
                Horca("C", 3),
                UnaMano("Granjero", "D", 4, "Granjero_D", "Granjero_Hoz_Reposo", "Granjero_Hoz_Golpe_Vertical", "Granjero_Hoz_Golpe_Diagonal") }),
        };

        [MenuItem("BronzeAge/Tropas/Montar las cuatro tropas")]
        public static void MontarCuatro()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[TropasBronzeAge] Sal de Play antes de montar."); return; }
            foreach (var t in TROPAS) MontarTropa(t.id, t.clave, t.nombre, t.prefijo, t.figuras);
            AssetDatabase.SaveAssets();
            Debug.Log("[TropasBronzeAge] Cuatro tropas montadas. Si sus escuadras aún no existen: Tools > BronzeAge > Importar tropas del motor, y otra vez este menú.");
        }

        [MenuItem("BronzeAge/Tropas/Montar milicia de lanceros")]
        public static void MontarLanceros()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[TropasBronzeAge] Sal de Play antes de montar."); return; }
            var t = TROPAS[0];
            MontarTropa(t.id, t.clave, t.nombre, t.prefijo, t.figuras);
            AssetDatabase.SaveAssets();
        }

        public static string Carpeta(string id) => $"{RutasBronzeAge.ESCUADRAS}/{id}";

        /// <summary>Ruta del prefab de batalla de una figura, en BattleVisuals/ de su escuadra. Si ya estaba en la carpeta de
        /// antes (Assets/Prefabs/BattleVisuals/BronzeAge), lo mueve allí conservando su GUID (el registro sigue apuntando).</summary>
        static string RutaDeBatalla(string id, string nombre)
        {
            var carpeta = $"{Carpeta(id)}/BattleVisuals";
            if (!AssetDatabase.IsValidFolder(carpeta)) AssetDatabase.CreateFolder(Carpeta(id), "BattleVisuals");
            var ruta = $"{carpeta}/{nombre}.prefab";
            var vieja = $"{SALIDA_VIEJA}/{nombre}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(vieja) != null && AssetDatabase.LoadAssetAtPath<GameObject>(ruta) == null)
            {
                var error = AssetDatabase.MoveAsset(vieja, ruta);
                if (!string.IsNullOrEmpty(error)) throw new IOException($"No se pudo mover {vieja}: {error}");
            }
            return ruta;
        }

        /// <summary>Una tropa: un Animator por tipo de arma (con los clips de su primera figura), un override para cada otra
        /// figura, sus prefabs de batalla, lo que lleva su carpeta de escuadra y su registro.</summary>
        public static GameObject[] MontarTropa(string id, string clave, string nombre, string prefijo, Figura[] figuras)
        {
            // los ataques, el golpe y la muerte no van en bucle (DemoAnimacionesImportacion: solo Reposo, Andar y Correr)
            foreach (var c in figuras.SelectMany(f => f.Clips).Distinct())
                AssetDatabase.ImportAsset(Ruta(c, ".fbx"), ImportAssetOptions.ForceUpdate);
            var anim = $"{Carpeta(id)}/Animations";
            Directory.CreateDirectory(anim);
            var bases = new Dictionary<string, (AnimatorController ac, Figura fig)>();
            var prefabs = new GameObject[figuras.Length];
            var controladores = new RuntimeAnimatorController[figuras.Length];
            bool variasArmas = figuras.Select(f => f.arma).Distinct().Count() > 1;
            for (int k = 0; k < figuras.Length; k++)
            {
                var f = figuras[k];
                if (!bases.TryGetValue(f.arma, out var b))
                {
                    var ruta = variasArmas ? $"{anim}/AC_{id}_{f.arma}.controller" : $"{anim}/AC_{id}.controller";
                    b = (Controlador(ruta, f.reposo, f.andar, f.correr, f.ataques, f.golpe, f.muerte, f.fila), f);
                    bases[f.arma] = b;
                    controladores[k] = b.ac;
                }
                else
                {
                    var pares = b.fig.Clips.Zip(f.Clips, (de, a) => new KeyValuePair<AnimationClip, AnimationClip>(Clip(de), Clip(a)))
                        .Where(p => p.Key != p.Value).ToList();
                    if (pares.Count == 0) controladores[k] = b.ac;           // los mismos clips: el mismo Animator
                    else
                    {
                        var ruta = $"{anim}/AOC_{id}_{f.letra}.overrideController";
                        var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ruta);      // en su sitio: mismo GUID
                        if (aoc == null) { aoc = new AnimatorOverrideController(b.ac) { name = $"AOC_{id}_{f.letra}" }; AssetDatabase.CreateAsset(aoc, ruta); }
                        aoc.runtimeAnimatorController = b.ac;
                        aoc.ApplyOverrides(pares);
                        EditorUtility.SetDirty(aoc);
                        controladores[k] = aoc;
                    }
                }
                prefabs[k] = Prefab(f.modelo, f.piel, controladores[k], RutaDeBatalla(id, $"{prefijo}_{f.letra}"));
            }
            var esqueleto = Escuadra(id, figuras[0], controladores[0]);
            Registrar(clave, nombre, prefabs, esqueleto);
            if (AssetDatabase.IsValidFolder(SALIDA_VIEJA) && AssetDatabase.FindAssets("", new[] { SALIDA_VIEJA }).Length == 0)
                AssetDatabase.DeleteAsset(SALIDA_VIEJA);            // la carpeta vieja, ya vacía
            Debug.Log($"[TropasBronzeAge] {nombre}: {bases.Count} Animator(es) y {prefabs.Length} prefabs en {Carpeta(id)}/BattleVisuals, registrados como {clave}_A..{(char)('A' + prefabs.Length - 1)}.");
            return prefabs;
        }

        /// <summary>Lo que lleva la carpeta de la escuadra además de sus clips y su Animator: el GO_Squeleton (la primera
        /// figura), la unidad ECS y el icono y la miniatura; y el SquadData apuntando a ellos.</summary>
        static GameObject Escuadra(string id, Figura figura, RuntimeAnimatorController controlador)
        {
            var carpeta = Carpeta(id);
            var esqueleto = Prefab(figura.modelo, figura.piel, controlador, $"{carpeta}/{id}_GO_Squeleton.prefab");
            Sprite Imagen(string ruta)
            {
                if (AssetImporter.GetAtPath(ruta) is TextureImporter ti && (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
                {
                    ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.alphaIsTransparency = true;
                    ti.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
            }
            var icono = Imagen($"{carpeta}/{id}_icon.png");
            var miniatura = Imagen($"{carpeta}/{id}_miniature.png");
            var datos = AssetDatabase.LoadAssetAtPath<SquadData>($"{carpeta}/{id}.asset");
            if (datos == null)
            {
                Debug.LogWarning($"[TropasBronzeAge] {id}: aún no hay {carpeta}/{id}.asset (Tools > BronzeAge > Importar tropas del motor); sin unidad ECS ni iconos enlazados.");
                return esqueleto;
            }
            var ecs = $"{carpeta}/UnitPrefab_ECS_{id}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ecs) == null && !AssetDatabase.CopyAsset(ECS_REFERENCIA, ecs))
                throw new FileNotFoundException(ECS_REFERENCIA);
            var contenido = PrefabUtility.LoadPrefabContents(ecs);
            try
            {
                contenido.name = $"UnitPrefab_ECS_{id}";
                contenido.GetComponent<UnitEntityAuthoring>().squadData = datos;
                datos.prefab = PrefabUtility.SaveAsPrefabAsset(contenido, ecs);
            }
            finally { PrefabUtility.UnloadPrefabContents(contenido); }
            if (icono != null) datos.icon = icono;
            if (miniatura != null) datos.unitImage = miniatura;
            EditorUtility.SetDirty(datos);
            AssetDatabase.SaveAssetIfDirty(datos);
            return esqueleto;
        }

        static string Ruta(string nombre, string extension) =>
            RutasBronzeAge.Buscar(nombre, extension) ?? throw new FileNotFoundException($"Falta {nombre}{extension} (RutasBronzeAge)");

        /// <summary>El Animator de la ruta vacío, conservando su GUID: antes se borraba y se creaba otro (GUID nuevo en cada
        /// montaje), y lo que guardaba el viejo (la subescena horneada, otros assets) se quedaba apuntando a nada.</summary>
        static AnimatorController Vaciar(string ruta)
        {
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ruta);
            if (ac == null) return AnimatorController.CreateAnimatorControllerAtPath(ruta);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ruta))
                if (o != null && o != ac && !AssetDatabase.IsMainAsset(o)) { AssetDatabase.RemoveObjectFromAsset(o); Object.DestroyImmediate(o, true); }
            ac.parameters = new AnimatorControllerParameter[0];
            var sm = new AnimatorStateMachine { name = "Base Layer", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(sm, ac);
            ac.layers = new[] { new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f, stateMachine = sm } };
            return ac;
        }

        static AnimationClip Clip(string nombre) =>
            AssetDatabase.LoadAllAssetsAtPath(Ruta(nombre, ".fbx")).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"))
            ?? throw new FileNotFoundException($"El FBX {nombre} no tiene clip");

        /// <summary>El Animator de una tropa. Lleva todos los parámetros que escribe UnitAnimatorController (los que no usa,
        /// de relleno: si faltan, el Animator avisa en cada fotograma).</summary>
        public static AnimatorController Controlador(string ruta, string reposo, string andar, string correr, string[] ataques, string golpe, string muerte, string[] fila = null)
        {
            // (18-11, Víctor: la milicia en T en batalla) los clips, todos, ANTES de tocar el Animator: si falta uno, error claro
            // y el Animator de antes se queda como estaba (nunca un prefab sin controller)
            foreach (var c in new[] { reposo, andar, correr, golpe, muerte }.Concat(ataques).Concat(fila ?? new string[0])) Clip(c);
            var ac = Vaciar(ruta);
            foreach (var (n, t) in new (string, AnimatorControllerParameterType)[] {
                ("MoveSpeed", AnimatorControllerParameterType.Float), ("IsGrounded", AnimatorControllerParameterType.Bool),
                ("IsStopped", AnimatorControllerParameterType.Bool), ("MovementInputTapped", AnimatorControllerParameterType.Bool),
                ("MovementInputPressed", AnimatorControllerParameterType.Bool), ("MovementInputHeld", AnimatorControllerParameterType.Bool),
                ("CurrentGait", AnimatorControllerParameterType.Int), ("IsWalking", AnimatorControllerParameterType.Bool),
                ("IsStrafing", AnimatorControllerParameterType.Float), ("StrafeDirectionX", AnimatorControllerParameterType.Float),
                ("StrafeDirectionZ", AnimatorControllerParameterType.Float), ("ForwardStrafe", AnimatorControllerParameterType.Float),
                ("IsAttacking", AnimatorControllerParameterType.Bool), ("AttackSpeed", AnimatorControllerParameterType.Float),
                ("TriggerHitReaction", AnimatorControllerParameterType.Trigger), ("HitReactionType", AnimatorControllerParameterType.Int),
                ("TriggerDeath", AnimatorControllerParameterType.Trigger), ("IsDead", AnimatorControllerParameterType.Bool),
                (AtaqueAlAzar.Parametro, AnimatorControllerParameterType.Int), ("Stance", AnimatorControllerParameterType.Int),
                (PasoReal.VELOCIDAD, AnimatorControllerParameterType.Float), (PasoReal.PASO, AnimatorControllerParameterType.Float) })
                ac.AddParameter(n, t);
            var ps = ac.parameters;
            foreach (var p in ps) if (p.name == "AttackSpeed" || p.name == PasoReal.PASO) p.defaultFloat = 1f;
            ac.parameters = ps;
            var sm = ac.layers[0].stateMachine;
            // locomoción por la velocidad REAL del soldado (PasoReal), con los clips en sus velocidades de autor y, fuera de
            // ellas, el clip más deprisa o más despacio (Paso): los pies no patinan (Víctor, 18-11). MoveSpeed (la coordenada
            // del árbol de Synty que escribe UnitAnimatorController) ya no se usa aquí.
            var arbol = new BlendTree { name = "Locomocion", blendType = BlendTreeType.Simple1D, blendParameter = PasoReal.VELOCIDAD, useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(arbol, ac);
            arbol.AddChild(Clip(reposo), 0f); arbol.AddChild(Clip(andar), PasoReal.ANDAR); arbol.AddChild(Clip(correr), PasoReal.CORRER);
            var loco = sm.AddState("Locomocion"); loco.motion = arbol;
            loco.speedParameterActive = true; loco.speedParameter = PasoReal.PASO;
            sm.defaultState = loco;
            // ataque: un estado vacío elige al azar y pasa a uno de los ataques
            var elegir = sm.AddState("Ataque (al azar)");
            elegir.AddStateMachineBehaviour<AtaqueAlAzar>().variantes = ataques.Length;
            var t0 = loco.AddTransition(elegir); t0.hasExitTime = false; t0.duration = 0.08f;
            t0.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");
            for (int i = 0; i < ataques.Length; i++)
            {
                var st = sm.AddState(ataques[i]); st.motion = Clip(ataques[i]);
                st.speedParameterActive = true; st.speedParameter = "AttackSpeed";
                var a = elegir.AddTransition(st); a.hasExitTime = false; a.duration = 0f;
                a.AddCondition(AnimatorConditionMode.Equals, i, AtaqueAlAzar.Parametro);
                var v = st.AddTransition(loco); v.hasExitTime = true; v.exitTime = 0.92f; v.duration = 0.15f;
            }
            if (fila != null) Fila(sm, loco, t0, fila);
            // golpe recibido (los cuatro tipos, de momento el tambaleo) y muerte, desde cualquier estado
            var g = sm.AddState("Golpe recibido"); g.motion = Clip(golpe);
            var tg = sm.AddAnyStateTransition(g); tg.hasExitTime = false; tg.duration = 0.08f; tg.canTransitionToSelf = true;
            tg.AddCondition(AnimatorConditionMode.If, 0, "TriggerHitReaction");
            tg.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");             // muerto no reacciona
            var vg = g.AddTransition(loco); vg.hasExitTime = true; vg.exitTime = 0.9f; vg.duration = 0.2f;
            var m = sm.AddState("Muerte"); m.motion = Clip(muerte);
            var tm = sm.AddAnyStateTransition(m); tm.hasExitTime = false; tm.duration = 0.1f; tm.canTransitionToSelf = false;
            tm.AddCondition(AnimatorConditionMode.If, 0, "TriggerDeath");
            // la muerte gana al golpe
            sm.anyStateTransitions = new[] { tm, tg };
            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssetIfDirty(ac);
            return ac;
        }

        // Stance de UnitPostureSystem (UnitStance.PikeLineFront / PikeLineRear) y fundidos de los piqueros
        // (ArtSource/PikemenLine/settings.json: transitionSeconds 0,25, attackBlendSeconds 0,05)
        public const int DE_RODILLAS = 7, DE_PIE_EN_FILA = 8;
        const float FUNDIDO = 0.25f, FUNDIDO_ATAQUE = 0.05f;

        /// <summary>Las posturas de fila, como la «Pike Formation» de los piqueros pero en la capa base: parada con Stance 7
        /// (de rodillas) u 8 (de pie detrás) va a su postura y ataca con su estocada; en movimiento o con otra Stance, todo
        /// como antes. En la capa base el golpe recibido y la muerte (desde cualquier estado) siguen por encima.</summary>
        static void Fila(AnimatorStateMachine sm, AnimatorState loco, AnimatorStateTransition alAtaque, string[] clips)
        {
            AnimatorStateTransition T(AnimatorState de, AnimatorState a, float s)
            {
                var t = de.AddTransition(a); t.hasExitTime = false; t.hasFixedDuration = true; t.duration = s;
                t.interruptionSource = TransitionInterruptionSource.SourceThenDestination; return t;
            }
            foreach (var (stance, postura, ataque) in new[] { (DE_RODILLAS, clips[0], clips[1]), (DE_PIE_EN_FILA, clips[2], clips[3]) })
            {
                var reposo = sm.AddState("Fila " + stance + ": " + postura); reposo.motion = Clip(postura);
                var estocada = sm.AddState("Fila " + stance + ": " + ataque); estocada.motion = Clip(ataque);
                estocada.speedParameterActive = true; estocada.speedParameter = "AttackSpeed";
                var e = T(loco, reposo, FUNDIDO);                                    // parado en su sitio de la fila
                e.AddCondition(AnimatorConditionMode.If, 0, "IsStopped"); e.AddCondition(AnimatorConditionMode.Equals, stance, "Stance");
                e.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
                var a = T(loco, estocada, FUNDIDO_ATAQUE);                              // ataca desde la locomoción ya en la fila
                a.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking"); a.AddCondition(AnimatorConditionMode.If, 0, "IsStopped");
                a.AddCondition(AnimatorConditionMode.Equals, stance, "Stance");
                var g = T(reposo, estocada, FUNDIDO_ATAQUE); g.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");
                var fin = estocada.AddTransition(reposo); fin.hasExitTime = true; fin.exitTime = 0.92f; fin.duration = 0.15f;
                var anda = T(reposo, loco, FUNDIDO); anda.AddCondition(AnimatorConditionMode.IfNot, 0, "IsStopped");
                var otra = T(reposo, loco, FUNDIDO); otra.AddCondition(AnimatorConditionMode.NotEqual, stance, "Stance");
            }
            // las de la fila antes que el ataque de siempre (las transiciones de un estado se miran en orden)
            loco.transitions = loco.transitions.Where(t => t != alAtaque).Concat(new[] { alAtaque }).ToArray();
        }

        static Transform Buscar(Transform t, string nombre)
        {
            if (t.name == nombre) return t;
            foreach (Transform c in t) { var r = Buscar(c, nombre); if (r) return r; }
            return null;
        }

        /// <summary>Un prefab con todo lo que pide la batalla, a partir de un modelo de nuestras tropas.</summary>
        public static GameObject Prefab(string modelo, int piel, RuntimeAnimatorController controlador, string ruta)
        {
            if (controlador == null) throw new System.InvalidOperationException($"{ruta}: sin Animator, no se guarda (el prefab de antes se queda)");
            var rutaFbx = Ruta(modelo, ".fbx");
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(rutaFbx);
            var referencia = AssetDatabase.LoadAssetAtPath<GameObject>(REFERENCIA);
            if (fbx == null || referencia == null) throw new FileNotFoundException($"Falta {rutaFbx} o {REFERENCIA}");
            var escena = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = Object.Instantiate(fbx);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, escena);
                go.name = Path.GetFileNameWithoutExtension(ruta);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var refGo = Object.Instantiate(referencia);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(refGo, escena);
                // materiales: el atlas de la tropa y el tono de piel, como en la demo
                var tono = AssetDatabase.LoadAssetAtPath<Material>(RutasBronzeAge.Buscar($"FantasyHero_Tono{piel}", ".mat") ?? "");
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var n = mats[i] != null ? mats[i].name : "";
                        int k = n.IndexOf("_Atlas");
                        if (k > 0) mats[i] = AssetDatabase.LoadAssetAtPath<Material>(RutasBronzeAge.Buscar(n.Substring(0, k + 6), ".mat") ?? "") ?? mats[i];
                        else if (tono != null && n.Contains("lambert2")) mats[i] = tono;
                    }
                    r.sharedMaterials = mats;
                }
                // Animator con el avatar del modelo y nuestro controller
                var animator = go.GetComponent<Animator>();
                if (animator == null) animator = go.AddComponent<Animator>();
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(rutaFbx).OfType<Avatar>().FirstOrDefault();
                animator.runtimeAnimatorController = controlador;
                animator.applyRootMotion = false;
                // lo que pide la batalla
                go.AddComponent<UnitAnimationAdapter>();
                go.AddComponent<UnitAnimatorController>();
                go.AddComponent<EntityVisualSync>();
                go.AddComponent<PasoReal>();
                EditorUtility.CopySerialized(refGo.GetComponent<NavMeshAgent>(), go.AddComponent<NavMeshAgent>());
                var capRef = refGo.GetComponent<CapsuleCollider>();
                if (capRef != null) EditorUtility.CopySerialized(capRef, go.AddComponent<CapsuleCollider>());
                // caja de golpe en la parte que golpea: la punta de la lanza o la cabeza de la herramienta (hija del
                // Weapon_Carrier, como en los soldados del juego)
                var carrier = Buscar(go.transform, "Weapon_Carrier");
                if (carrier == null) throw new System.InvalidOperationException($"{modelo}: sin Weapon_Carrier");
                // la punta: el punto del arma (mallas bajo el Weapon_Carrier) más lejos del agarre, en el mundo
                Vector3 agarre = carrier.position, punta = agarre;
                foreach (var r in carrier.GetComponentsInChildren<Renderer>(true))
                {
                    var b = r.localBounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var esquina = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        var p = r.transform.TransformPoint(esquina);
                        if ((p - agarre).sqrMagnitude > (punta - agarre).sqrMagnitude) punta = p;
                    }
                }
                var eje = (punta - agarre).normalized;
                var caja = new GameObject("WeaponHitbox");
                caja.transform.SetParent(carrier, false);
                caja.transform.SetPositionAndRotation(punta - eje * 0.13f, Quaternion.LookRotation(eje));
                var col = caja.AddComponent<BoxCollider>();
                col.isTrigger = true;
                var escala = caja.transform.lossyScale;
                col.size = new Vector3(0.09f / escala.x, 0.09f / escala.y, 0.26f / escala.z);       // 9 x 9 x 26 cm en el mundo
                caja.AddComponent<WeaponHitboxBehaviour>();
                var rb = caja.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
                // el Detector (color de bando), colgado de un hueso del mismo nombre que en el Spearmen
                var detRef = Buscar(refGo.transform, "Detector");
                var detPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DETECTOR);
                if (detRef != null && detPrefab != null)
                {
                    var padre = Buscar(go.transform, detRef.parent.name) ?? go.transform;
                    var det = (GameObject)PrefabUtility.InstantiatePrefab(detPrefab, padre);
                    det.name = "Detector";
                    det.transform.localPosition = detRef.localPosition;
                    det.transform.localRotation = detRef.localRotation;
                    det.transform.localScale = detRef.localScale;
                }
                // la capa del Spearmen en todo
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = referencia.layer;
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, ruta);
                Debug.Log($"[TropasBronzeAge] {ruta}: punta del arma a {(punta - agarre).magnitude:0.00} m del agarre, Detector bajo «{(detRef != null ? detRef.parent.name : "-")}»");
                return prefab;
            }
            finally { EditorSceneManager.ClosePreviewScene(escena); }
        }

        /// <summary>Añade o actualiza las entradas «clave_A..» del registro de visuales (al final: no cambia el visual
        /// por defecto de ningún tipo de escuadra).</summary>
        public static void Registrar(string clave, string nombre, GameObject[] prefabs, GameObject esqueleto)
        {
            var config = AssetDatabase.LoadAssetAtPath<VisualPrefabConfiguration>(CONFIG);
            if (config == null) throw new FileNotFoundException(CONFIG);
            var so = new SerializedObject(config);
            var lista = so.FindProperty("unitPrefabs");
            for (int k = 0; k < prefabs.Length; k++)
            {
                string id = $"{clave}_{(char)('A' + k)}";
                int i = -1;
                for (int j = 0; j < lista.arraySize; j++)
                    if (lista.GetArrayElementAtIndex(j).FindPropertyRelative("unitId").stringValue == id) { i = j; break; }
                if (i < 0) { i = lista.arraySize; lista.arraySize++; }
                var e = lista.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("squadType").intValue = (int)SquadType.Spearmen;
                e.FindPropertyRelative("unitId").stringValue = id;
                e.FindPropertyRelative("displayName").stringValue = $"{nombre} {(char)('A' + k)}";
                e.FindPropertyRelative("visualPrefab").objectReferenceValue = esqueleto;
                e.FindPropertyRelative("battleVisualPrefab").objectReferenceValue = prefabs[k];
                e.FindPropertyRelative("isDefault").boolValue = false;
                var v = e.FindPropertyRelative("variants"); if (v != null) v.arraySize = 0;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
        }
    }
}
