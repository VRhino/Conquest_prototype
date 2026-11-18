using System.Collections.Generic;
using System.Linq;
using ConquestTactics.Animation;
using ConquestTactics.Visual;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Tropas de la Edad del Bronce en batalla: cada tropa con su visual y sus figuras A, B, C… (rama
/// bronzeage/tropas-en-batalla).</summary>
public class BronzeAgeTropasTests
{
    [Test]
    public void SinFigurasElVisualEsElDelTipoDeEscuadra()
    {
        var data = new SquadDataComponent();
        Assert.AreEqual("UnitVisual_Spearmen", SquadUnitSpawnBuilder.VisualName(data, SquadType.Spearmen, 7, 3).ToString());
    }

    [Test]
    public void ConFigurasCadaSoldadoTieneUnaFijaYSalenTodas()
    {
        var data = new SquadDataComponent { visualPrefabName = new FixedString64Bytes("BronzeAge_Lancero"), visualVariants = 4 };
        var vistas = new HashSet<string>();
        for (int puesto = 0; puesto < 25; puesto++)
        {
            var a = SquadUnitSpawnBuilder.VisualName(data, SquadType.Spearmen, 12, puesto).ToString();
            var b = SquadUnitSpawnBuilder.VisualName(data, SquadType.Spearmen, 12, puesto).ToString();
            Assert.AreEqual(a, b, "la figura de un soldado no puede cambiar (servidor y cliente eligen la misma)");
            StringAssert.IsMatch("^BronzeAge_Lancero_[ABCD]$", a);
            vistas.Add(a);
        }
        Assert.AreEqual(4, vistas.Count, "en una escuadra de 25 deben salir las cuatro figuras");
    }

    /// <summary>Lo que monta BronzeAge &gt; Tropas &gt; Montar milicia de lanceros: las cuatro figuras registradas y cada
    /// prefab de batalla con lo que pide la batalla (si aún no se ha montado, la prueba se salta).</summary>
    // (18-11, Víctor: la milicia en T en batalla, «No hay AnimatorController asignado») los 16 prefabs de batalla y los 4
    // GO_Squeleton de las levas llevan un Animator con controller
    [Test]
    public void CadaPrefabDeLasLevasTieneSuAnimator()
    {
        var malos = new List<string>(); int n = 0;
        foreach (var t in new[] { "milicia_lanceros", "lenadores", "mineros", "granjeros" })
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { $"Assets/Resources/Squads/BronzeAge/{t}" }))
            {
                var ruta = AssetDatabase.GUIDToAssetPath(g);
                if (!ruta.Contains("/BattleVisuals/") && !ruta.EndsWith("_GO_Squeleton.prefab")) continue;
                n++;
                var a = AssetDatabase.LoadAssetAtPath<GameObject>(ruta).GetComponent<Animator>();
                if (a == null || a.runtimeAnimatorController == null) malos.Add(ruta);
            }
        Assert.AreEqual(20, n, "16 prefabs de batalla y 4 GO_Squeleton");
        Assert.IsEmpty(malos, "sin Animator o sin controller: " + string.Join(", ", malos));
    }

    [Test]
    public void LaMiliciaTieneSusCuatroFigurasDeBatalla()
    {
        var config = AssetDatabase.LoadAssetAtPath<VisualPrefabConfiguration>("Assets/ScriptableObjects/VisualPrefabConfig.asset");
        Assert.NotNull(config);
        var entradas = config.UnitPrefabs.Where(e => e.unitId != null && e.unitId.StartsWith("BronzeAge_Lancero_")).ToList();
        if (entradas.Count == 0) Assert.Ignore("Sin montar: BronzeAge > Tropas > Montar milicia de lanceros");
        CollectionAssert.AreEquivalent(new[] { "BronzeAge_Lancero_A", "BronzeAge_Lancero_B", "BronzeAge_Lancero_C", "BronzeAge_Lancero_D" },
            entradas.Select(e => e.unitId).ToArray());
        Assert.IsFalse(entradas.Any(e => e.isDefault), "no deben ser el visual por defecto de los Spearmen");
        var primerSpearmen = config.UnitPrefabs.First(e => e.squadType == SquadType.Spearmen);
        Assert.IsFalse(primerSpearmen.unitId.StartsWith("BronzeAge_"), "el visual por defecto de los Spearmen no cambia");
        foreach (var e in entradas)
        {
            var p = e.battleVisualPrefab;
            Assert.NotNull(p, e.unitId);
            foreach (var t in new[] { typeof(Animator), typeof(EntityVisualSync), typeof(UnitAnimationAdapter), typeof(UnitAnimatorController), typeof(NavMeshAgent) })
                Assert.NotNull(p.GetComponent(t), $"{e.unitId}: falta {t.Name}");
            var a = p.GetComponent<Animator>();
            Assert.NotNull(a.avatar, $"{e.unitId}: sin avatar");
            Assert.AreEqual("AC_milicia_lanceros", a.runtimeAnimatorController.name);
            var caja = p.GetComponentInChildren<WeaponHitboxBehaviour>(true);
            Assert.NotNull(caja, $"{e.unitId}: sin caja de golpe");
            Assert.AreEqual("Weapon_Carrier", caja.transform.parent.name);
        }
    }

    /// <summary>Cada tropa nuestra tiene su carpeta de escuadra completa, como Squires: su SquadData con su Melee, sus
    /// formaciones, sus clips y su Animator en Animations/, su GO_Squeleton, su unidad ECS (con su SquadData) y su icono y
    /// miniatura; y el SquadData apunta a ellos.</summary>
    [TestCase("milicia_lanceros")]
    [TestCase("lenadores")]
    [TestCase("mineros")]
    [TestCase("granjeros")]
    public void CadaTropaTieneSuCarpetaDeEscuadraCompleta(string id)
    {
        var carpeta = $"Assets/Resources/Squads/BronzeAge/{id}";
        var datos = AssetDatabase.LoadAssetAtPath<SquadData>($"{carpeta}/{id}.asset");
        if (datos == null) Assert.Ignore("Sin importar: Tools > BronzeAge > Importar tropas del motor");
        if (AssetDatabase.LoadAssetAtPath<GameObject>($"{carpeta}/{id}_GO_Squeleton.prefab") == null)
            Assert.Ignore("Sin montar: BronzeAge > Tropas > Montar las cuatro tropas");
        Assert.NotNull(datos.meleeData, $"{id}: sin Melee");
        Assert.IsTrue(AssetDatabase.GetAssetPath(datos.meleeData).StartsWith(carpeta + "/"), $"{id}: el Melee no es suyo");
        Assert.IsTrue(AssetDatabase.FindAssets("t:FormationDefinition", new[] { $"{carpeta}/Formations" }).Length > 0, $"{id}: sin formaciones");
        Assert.IsTrue(AssetDatabase.FindAssets("t:AnimationClip", new[] { $"{carpeta}/Animations" }).Length > 0, $"{id}: sin clips en Animations/");
        Assert.IsTrue(AssetDatabase.FindAssets("t:RuntimeAnimatorController", new[] { $"{carpeta}/Animations" }).Length > 0, $"{id}: sin Animator en Animations/");
        var ecs = AssetDatabase.LoadAssetAtPath<GameObject>($"{carpeta}/UnitPrefab_ECS_{id}.prefab");
        Assert.NotNull(ecs, $"{id}: sin UnitPrefab_ECS_{id}");
        Assert.AreEqual(datos, ecs.GetComponent<UnitEntityAuthoring>().squadData, $"{id}: la unidad ECS no es de su escuadra");
        Assert.AreEqual(ecs, datos.prefab, $"{id}: el SquadData no apunta a su unidad ECS");
        Assert.AreEqual($"{carpeta}/{id}_icon.png", AssetDatabase.GetAssetPath(datos.icon), $"{id}: icono");
        Assert.AreEqual($"{carpeta}/{id}_miniature.png", AssetDatabase.GetAssetPath(datos.unitImage), $"{id}: miniatura");
        // y TODO lo de la tropa dentro: sus cuatro figuras (modelo y prefab de batalla) y su atlas
        Assert.AreEqual(4, AssetDatabase.FindAssets("t:Model", new[] { $"{carpeta}/Models" }).Length, $"{id}: los cuatro modelos A..D en Models/");
        Assert.AreEqual(4, AssetDatabase.FindAssets("t:Prefab", new[] { $"{carpeta}/BattleVisuals" }).Length, $"{id}: los cuatro prefabs de batalla en BattleVisuals/");
        Assert.IsTrue(AssetDatabase.FindAssets("t:Material", new[] { $"{carpeta}/Materials" }).Length > 0, $"{id}: sin su atlas en Materials/");
        Assert.IsFalse(AssetDatabase.IsValidFolder("Assets/Prefabs/BattleVisuals/BronzeAge"), "no deben quedar prefabs en la carpeta vieja");
    }

    /// <summary>Lo que monta BronzeAge &gt; Tropas &gt; Montar las cuatro tropas para cada leva: cuatro figuras registradas,
    /// cada una con su Animator (el de su tipo de arma o un override con sus clips) y la caja de golpe en el arma.</summary>
    [TestCase("BronzeAge_Lenador", "lenadores")]
    [TestCase("BronzeAge_Minero", "mineros")]
    [TestCase("BronzeAge_Granjero", "granjeros")]
    public void CadaLevaTieneSusCuatroFigurasDeBatalla(string clave, string id)
    {
        var config = AssetDatabase.LoadAssetAtPath<VisualPrefabConfiguration>("Assets/ScriptableObjects/VisualPrefabConfig.asset");
        Assert.NotNull(config);
        var entradas = config.UnitPrefabs.Where(e => e.unitId != null && e.unitId.StartsWith(clave + "_")).ToList();
        if (entradas.Count == 0) Assert.Ignore("Sin montar: BronzeAge > Tropas > Montar las cuatro tropas");
        CollectionAssert.AreEquivalent(new[] { "A", "B", "C", "D" }.Select(x => $"{clave}_{x}").ToArray(), entradas.Select(e => e.unitId).ToArray());
        Assert.IsFalse(entradas.Any(e => e.isDefault), "no deben ser el visual por defecto de ningún tipo");
        foreach (var e in entradas)
        {
            var p = e.battleVisualPrefab;
            Assert.NotNull(p, e.unitId);
            foreach (var t in new[] { typeof(Animator), typeof(EntityVisualSync), typeof(UnitAnimationAdapter), typeof(UnitAnimatorController), typeof(NavMeshAgent) })
                Assert.NotNull(p.GetComponent(t), $"{e.unitId}: falta {t.Name}");
            var a = p.GetComponent<Animator>();
            Assert.NotNull(a.avatar, $"{e.unitId}: sin avatar");
            var rac = a.runtimeAnimatorController;
            Assert.NotNull(rac, $"{e.unitId}: sin Animator");
            StringAssert.IsMatch($"^(AC|AOC)_{id}", rac.name, e.unitId);
            Assert.AreEqual($"Assets/Resources/Squads/BronzeAge/{id}/{id}_GO_Squeleton.prefab", AssetDatabase.GetAssetPath(e.visualPrefab), $"{e.unitId}: visualPrefab");
            Assert.IsTrue(rac.animationClips.All(c => c != null && !c.name.StartsWith("Spearmen")), $"{e.unitId}: solo clips nuestros");
            var caja = p.GetComponentInChildren<WeaponHitboxBehaviour>(true);
            Assert.NotNull(caja, $"{e.unitId}: sin caja de golpe");
            Assert.AreEqual("Weapon_Carrier", caja.transform.parent.name);
            StringAssert.StartsWith($"Assets/Resources/Squads/BronzeAge/{id}/BattleVisuals/", AssetDatabase.GetAssetPath(p), $"{e.unitId}: el prefab de batalla va en su escuadra");
        }
    }
}
