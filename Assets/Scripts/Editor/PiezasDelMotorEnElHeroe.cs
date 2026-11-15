// Las piezas provisionales de Synty para vestir al héroe con el equipo del motor (paso 6 del bloque 4, tanda 2; Víctor, 2026-11-14: «como
// propones»): un casco por peso (cubrecabezas que dejan el pelo, de `PolygonFantasyHeroCharacters/ModularCharacters`), el hacha, el par de
// dagas y el escudo redondo de la lanza (mallas de `Weapons` sin usar). Las monta en el héroe modular de autoría (`ModularHeroVisual`; su
// variante remota las hereda), da de alta sus piezas en `AvatarPartDatabase` y sus objetos de Conquest (`ItemDataSO`, para la batalla), y
// rehace los visuales de batalla de los héroes con `BattleVisualPrefabBuilder` (solo los de héroe: los de las tropas no se tocan).
// Todo en las copias de las mallas dentro del prefab: no cambia materiales compartidos ni los prefabs de Synty. Se puede volver a correr.
// Batch: -executeMethod PiezasDelMotorEnElHeroe.Montar
using System;
using System.Collections.Generic;
using System.Linq;
using Data.Avatar;
using UnityEditor;
using UnityEngine;

public static class PiezasDelMotorEnElHeroe
{
    const string Heroe = "Assets/Prefabs/esqueletos/ModularHeroVisual.prefab";
    const string Synty = "Assets/Synty/PolygonFantasyHeroCharacters/Prefabs/ModularCharacters.prefab";
    const string Armas = "Assets/Synty/PolygonFantasyHeroCharacters/Prefabs/Weapons/";
    const string Partes = "Assets/Resources/Data/Avatar/AvatarPartDatabase.asset";
    const string BaseDeObjetos = "Assets/Resources/Items/ItemSODatabase.asset";
    const string Config = "Assets/ScriptableObjects/VisualPrefabConfig.asset";

    /// <summary>Los cascos: pieza de Conquest, objeto, malla de Synty (en el grupo `HeadCoverings_No_Hair`, que tapa el pelo): capucha de
    /// cuero, yelmo nasal con cota y yelmo con cresta.</summary>
    static readonly (string parte, string objeto, string nombre, string malla, int peso)[] Cascos =
    {
        ("HelmetLigthArmorDefault", "HelLADef", "Casco ligero", "Chr_HeadCoverings_No_Hair_13", 0),
        ("HelmetMediumArmorDefault", "HelMADef", "Casco medio", "Chr_HeadCoverings_No_Hair_02", 1),
        ("HelmetHeavyArmorDefault", "HelHADef", "Casco pesado", "Chr_HeadCoverings_No_Hair_04", 2),
    };
    const string GrupoCascos = "HeadCoverings_No_Hair";
    /// <summary>Los primeros cascos provisionales (sombreros), que se quitan del héroe.</summary>
    static readonly string[] CascosViejos = { "Chr_HeadCoverings_Base_Hair_01", "Chr_HeadCoverings_Base_Hair_05", "Chr_HeadCoverings_Base_Hair_09" };

    [MenuItem("Tools/Characters/Montar piezas del motor en el héroe")]
    public static void Montar()
    {
        var informe = new List<string>();
        MontarMallas(informe);
        DarDeAltaPartes(informe);
        CrearObjetos(informe);
        AssetDatabase.SaveAssets();
        RehacerVisualesDeBatalla(informe);
        Debug.Log("[PiezasDelMotor] " + string.Join(" · ", informe));
    }

    static Transform Buscar(Transform raiz, string nombre) =>
        raiz.name == nombre ? raiz : raiz.Cast<Transform>().Select(h => Buscar(h, nombre)).FirstOrDefault(x => x != null);

    static void MontarMallas(List<string> informe)
    {
        var fuente = AssetDatabase.LoadAssetAtPath<GameObject>(Synty) ?? throw new InvalidOperationException("Falta " + Synty);
        var raiz = PrefabUtility.LoadPrefabContents(Heroe);
        try
        {
            // Cascos: una copia del SkinnedMeshRenderer de Synty, con los huesos del héroe por nombre (mismo esqueleto de Synty).
            var grupo = Buscar(raiz.transform, GrupoCascos) ?? throw new InvalidOperationException("Sin el grupo " + GrupoCascos);
            var conPelo = Buscar(raiz.transform, "HeadCoverings_Base_Hair");
            foreach (var viejo in CascosViejos)
                if (conPelo?.Find(viejo) is Transform t) { UnityEngine.Object.DestroyImmediate(t.gameObject); informe.Add("fuera " + viejo); }
            foreach (var casco in Cascos)
            {
                if (grupo.Find(casco.malla) != null) continue;
                var origen = Buscar(fuente.transform, casco.malla)?.GetComponent<SkinnedMeshRenderer>() ?? throw new InvalidOperationException("Sin " + casco.malla + " en Synty");
                var go = new GameObject(casco.malla);
                go.layer = grupo.gameObject.layer;
                go.transform.SetParent(grupo, false);
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = origen.sharedMesh;
                smr.sharedMaterials = origen.sharedMaterials;
                smr.bones = origen.bones.Select(b => b == null ? null : Buscar(raiz.transform, b.name) ?? throw new InvalidOperationException("Sin el hueso " + b.name)).ToArray();
                smr.rootBone = origen.rootBone == null ? null : Buscar(raiz.transform, origen.rootBone.name);
                smr.localBounds = origen.localBounds;
                go.SetActive(false);
                informe.Add("casco " + casco.malla);
            }

            // Armas: el hacha y la daga en la mano, la otra daga y el escudo redondo en la izquierda, colocados como la espada y el escudo que ya hay.
            var mano = Buscar(raiz.transform, "WeaponCarrier") ?? throw new InvalidOperationException("Sin WeaponCarrier");
            var izquierda = Buscar(raiz.transform, "OffWeaponCarrier") ?? throw new InvalidOperationException("Sin OffWeaponCarrier");
            var espada = mano.Find("SM_Wep_Sword_01");
            var escudo = izquierda.Find("SM_Wep_Shield_02");
            void Arma(Transform donde, string prefab, Transform como)
            {
                // La escala también, la de la pieza que imita: el portador viene escalado y las de Conquest lo compensan.
                if (donde.Find(prefab) is Transform ya) { if (como != null) ya.localScale = como.localScale; return; }
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(Armas + prefab + ".prefab") ?? throw new InvalidOperationException("Sin " + prefab);
                var copia = UnityEngine.Object.Instantiate(p, donde, false);
                copia.name = prefab;
                if (como != null) { copia.transform.localPosition = como.localPosition; copia.transform.localRotation = como.localRotation; copia.transform.localScale = como.localScale; }
                foreach (var t in copia.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = donde.gameObject.layer;
                copia.SetActive(false);
                informe.Add($"arma {prefab} en {donde.name}");
            }
            Arma(mano, "SM_Wep_Axe_01", espada);
            Arma(mano, "SM_Wep_Dagger_01", espada);
            Arma(izquierda, "SM_Wep_Dagger_01", espada);
            Arma(izquierda, "SM_Wep_Shield_05", escudo);
            PrefabUtility.SaveAsPrefabAsset(raiz, Heroe);
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    static VisualAttachment Pieza(string grupo, string malla) =>
        new VisualAttachment { visualID = malla, boneTargetMale = grupo, boneTargetFemale = grupo, prefabPathMale = malla, prefabPathFemale = malla };

    static void DarDeAltaPartes(List<string> informe)
    {
        var partes = AssetDatabase.LoadAssetAtPath<AvatarPartDatabase>(Partes) ?? throw new InvalidOperationException("Falta " + Partes);
        void Alta(List<AvatarPartDefinition> lista, string id, string nombre, AvatarSlot hueco, params VisualAttachment[] piezas)
        {
            lista.RemoveAll(p => p.id == id);
            lista.Add(new AvatarPartDefinition { id = id, displayName = nombre, slot = hueco, attachments = piezas.ToList() });
            informe.Add("parte " + id);
        }
        partes.headParts ??= new List<AvatarPartDefinition>();
        foreach (var c in Cascos) Alta(partes.headParts, c.parte, c.nombre, AvatarSlot.Head, Pieza(GrupoCascos, c.malla));
        Alta(partes.weaponParts, "AxeBasic", "Hacha (provisional)", AvatarSlot.Weapon, Pieza("WeaponCarrier", "SM_Wep_Axe_01"));
        Alta(partes.weaponParts, "DaggersBasic", "Par de dagas (provisional)", AvatarSlot.Weapon, Pieza("WeaponCarrier", "SM_Wep_Dagger_01"), Pieza("OffWeaponCarrier", "SM_Wep_Dagger_01"));
        Alta(partes.weaponParts, "SpearAndRoundShieldBasic", "Lanza y escudo redondo (provisional)", AvatarSlot.Weapon, Pieza("WeaponCarrier", "SM_Wep_Spear_10"), Pieza("OffWeaponCarrier", "SM_Wep_Shield_05"));
        EditorUtility.SetDirty(partes);
    }

    /// <summary>Los objetos de Conquest de las piezas nuevas, copiados de uno del mismo tipo (su icono incluido) con su id y su pieza.</summary>
    static void CrearObjetos(List<string> informe)
    {
        var baseDeObjetos = AssetDatabase.LoadAssetAtPath<ScriptableObject>(BaseDeObjetos) ?? throw new InvalidOperationException("Falta " + BaseDeObjetos);
        var lista = new SerializedObject(baseDeObjetos);
        var items = lista.FindProperty("_items");
        void Crear(string modelo, string carpeta, string id, string nombre, string parte, int? categoria)
        {
            var ruta = $"Assets/Resources/Items/{carpeta}/{id}/{id}.asset";
            if (AssetDatabase.LoadMainAssetAtPath(ruta) == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ruta));
                AssetDatabase.Refresh();
                if (!AssetDatabase.CopyAsset(modelo, ruta)) throw new InvalidOperationException("No se pudo copiar " + modelo);
            }
            var objeto = AssetDatabase.LoadMainAssetAtPath(ruta);
            var so = new SerializedObject(objeto);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_name").stringValue = nombre;
            so.FindProperty("_visualPartId").stringValue = parte;
            if (categoria != null) so.FindProperty("_itemCategory").intValue = categoria.Value;
            so.ApplyModifiedPropertiesWithoutUndo();
            objeto.name = id;
            bool esta = false;
            for (int i = 0; i < items.arraySize; i++) if (items.GetArrayElementAtIndex(i).objectReferenceValue == objeto) esta = true;
            if (!esta)
            {
                items.InsertArrayElementAtIndex(items.arraySize);
                items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = objeto;
            }
            informe.Add("objeto " + id);
        }
        Crear("Assets/Resources/Items/armors/TorLADef/TorLADef.asset", "armors", "HelLADef", "Casco ligero", "HelmetLigthArmorDefault", 0);
        Crear("Assets/Resources/Items/armors/TorMADef/TorMADef.asset", "armors", "HelMADef", "Casco medio", "HelmetMediumArmorDefault", 0);
        Crear("Assets/Resources/Items/armors/TorHADef/TorHADef.asset", "armors", "HelHADef", "Casco pesado", "HelmetHeavyArmorDefault", 0);
        Crear("Assets/Resources/Items/weapons/WeaS2HBasic/WeaS2HBasic.asset", "weapons", "WeaAxeBasic", "Hacha", "AxeBasic", null);
        Crear("Assets/Resources/Items/weapons/WeaS&SBasic/WeaS&SBasic.asset", "weapons", "WeaDagBasic", "Dagas", "DaggersBasic", null);
        Crear("Assets/Resources/Items/weapons/WeaSBasic/WeaSBasic.asset", "weapons", "WeaSRSBasic", "Lanza y escudo redondo", "SpearAndRoundShieldBasic", null);
        lista.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(baseDeObjetos);
    }

    /// <summary>Los visuales de batalla de los héroes (el local y el remoto), hechos de nuevo desde su prefab de autoría.</summary>
    static void RehacerVisualesDeBatalla(List<string> informe)
    {
        var config = AssetDatabase.LoadAssetAtPath<VisualPrefabConfiguration>(Config) ?? throw new InvalidOperationException("Falta " + Config);
        foreach (var e in config.HeroPrefabs)
        {
            if (e.visualPrefab == null || e.battleVisualPrefab == null) continue;
            var origen = AssetDatabase.GetAssetPath(e.visualPrefab);
            var destino = AssetDatabase.GetAssetPath(e.battleVisualPrefab);
            informe.Add(BattleVisualPrefabBuilder.Generate(origen, destino, e.visualPrefab.GetComponent<AvatarPartSelector>() != null));
        }
    }
}
