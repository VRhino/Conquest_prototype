// Para ver los asentamientos del mapa de mundo en el editor (solo editor; nada del juego cambia):
//  * `Conquest/Mundo/Asentamientos: Mejorado`: la casilla del estilo de las maquetas (`MaquetasDelMapa.Estilo`). Se guarda en EditorPrefs y
//    se aplica al entrar en Play; apagada, «Prototipo» (el valor del código).
//  * `Conquest/Mundo/Banco de asentamientos`: abre una escena vacía sin guardar, entra en Play y monta el mapa en local, sin servidor, con el
//    mundo que ya hay en disco (el que bajó la última partida, en `persistentDataPath/mundo`) y un banco de plazas: de nivel 1 (zonas de 12 y
//    20 u), 2 (25 u), 3 (35 u), 4 (45 u) y 5 (55 u), las del 2 al 5 abiertas y amuralladas, cada una en tierra firme, llana y despejada,
//    separadas; y los pueblos extractores («Mejorado»): canteras sobre piedra y minas sobre cobre, estaño, hierro y oro, de nivel 1 a 3, cada
//    uno sobre su nodo; las aldeas (cada cultivo, con sus campos); y la parte 5 y lo menudo: avanzadas por etapa y estado (con su mejora y
//    su reparación), obras, campamentos de mercenarios y de bandidos, alijos y una batalla. La cámara es la del mapa (arrastrar gira, la rueda acerca); `Conquest/Mundo/Banco: siguiente plaza` lleva a la siguiente.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BronzeAge.Contract.V1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AmbitoPb = BronzeAge.Contract.V1.Ambito;

namespace ConquestTactics.Mundo.Mapa.Editor
{
    [InitializeOnLoad]
    static class MenuDeAsentamientos
    {
        const string ClaveMejorado = "Conquest.Mundo.AsentamientosMejorado", ClaveBanco = "Conquest.Mundo.AbrirBancoDeAsentamientos";
        const string RutaMejorado = "Conquest/Mundo/Asentamientos: Mejorado";

        static Almacen almacen;
        static readonly List<(string nombre, double x, double y)> sitios = new List<(string, double, double)>();
        static int actual;

        static MenuDeAsentamientos() { EditorApplication.playModeStateChanged += Cambio; }

        static void Cambio(PlayModeStateChange e)
        {
            if (e == PlayModeStateChange.EnteredPlayMode)
            {
                MaquetasDelMapa.Estilo = EditorPrefs.GetBool(ClaveMejorado, false) ? EstiloDeAsentamientos.Mejorado : EstiloDeAsentamientos.Prototipo;
                if (SessionState.GetBool(ClaveBanco, false))
                {
                    SessionState.SetBool(ClaveBanco, false);
                    try { Montar(); }
                    catch (Exception ex) { Debug.LogError($"[Banco de asentamientos] no se pudo montar: {ex.Message}"); }
                }
            }
            if (e == PlayModeStateChange.ExitingPlayMode) { almacen = null; sitios.Clear(); }
            // fuera de Play, el estilo vuelve al del código («Prototipo»): sin recarga de dominio, el de Play no se queda en el editor (ni en sus
            // pruebas)
            if (e == PlayModeStateChange.EnteredEditMode) MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Prototipo;
        }

        [MenuItem(RutaMejorado, false, 200)]
        static void CambiarEstilo()
        {
            bool v = !EditorPrefs.GetBool(ClaveMejorado, false);
            EditorPrefs.SetBool(ClaveMejorado, v);
            Menu.SetChecked(RutaMejorado, v);
            if (Application.isPlaying) MaquetasDelMapa.Estilo = v ? EstiloDeAsentamientos.Mejorado : EstiloDeAsentamientos.Prototipo;
        }

        [MenuItem(RutaMejorado, true)]
        static bool ValidarEstilo()
        {
            Menu.SetChecked(RutaMejorado, EditorPrefs.GetBool(ClaveMejorado, false));
            return true;
        }

        const string RutaPrevia = "Assets/Scenes/BronzeAge/PreviaDelMapa.unity";

        /// <summary>La previa del mapa (`PreviaDelMapa`): abre su escena (la crea si no está) y entra en Play.</summary>
        [MenuItem("Conquest/Mundo/Previa del mapa", false, 199)]
        static void AbrirPrevia()
        {
            if (Application.isPlaying) { Debug.LogWarning("[Previa del mapa] sal de Play primero"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(RutaPrevia)) CrearPrevia();
            EditorSceneManager.OpenScene(RutaPrevia, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        /// <summary>La escena de la previa: un objeto con `PreviaDelMapa` y los prefabs de las figuras (también en lote: `-executeMethod
        /// ConquestTactics.Mundo.Mapa.Editor.MenuDeAsentamientos.CrearPrevia`).</summary>
        public static void CrearPrevia() => CrearEscena(RutaPrevia, "Previa del mapa", "ConquestTactics.Mundo.UI.PreviaDelMapa");

        const string RutaDistancias = "Assets/Scenes/BronzeAge/DistanciasDePlaza.unity";

        /// <summary>Las distancias de plaza (`DistanciasDePlaza`): abre su escena (la crea si no está) y entra en Play.</summary>
        [MenuItem("Conquest/Mundo/Distancias de plaza", false, 198)]
        static void AbrirDistancias()
        {
            if (Application.isPlaying) { Debug.LogWarning("[Distancias de plaza] sal de Play primero"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(RutaDistancias)) CrearDistancias();
            EditorSceneManager.OpenScene(RutaDistancias, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        /// <summary>La escena de las distancias de plaza (también en lote: `-executeMethod
        /// ConquestTactics.Mundo.Mapa.Editor.MenuDeAsentamientos.CrearDistancias`).</summary>
        public static void CrearDistancias() => CrearEscena(RutaDistancias, "Distancias de plaza", "ConquestTactics.Mundo.UI.DistanciasDePlaza");

        /// <summary>Una escena vacía con un objeto que lleva el componente `tipo` (de Conquest.Mundo.UI) y los prefabs de las figuras.</summary>
        static void CrearEscena(string ruta, string nombre, string tipoNombre)
        {
            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject(nombre);
            var tipo = Type.GetType(tipoNombre + ", Conquest.Mundo.UI");
            var c = go.AddComponent(tipo);
            var so = new SerializedObject(c);
            so.FindProperty("Prefabs").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/ScriptableObjects/VisualPrefabConfig.asset");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(escena, ruta);
            Debug.Log($"[{nombre}] escena creada en {ruta}");
        }

        [MenuItem("Conquest/Mundo/Banco de asentamientos", false, 201)]
        static void AbrirBanco()
        {
            if (Application.isPlaying) { Debug.LogWarning("[Banco de asentamientos] sal de Play primero"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(ClaveBanco, true);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Conquest/Mundo/Banco: siguiente plaza", false, 202)]
        static void Siguiente()
        {
            if (almacen == null || sitios.Count == 0) return;
            actual = (actual + 1) % sitios.Count;
            Ir(actual);
        }

        [MenuItem("Conquest/Mundo/Banco: siguiente plaza", true)]
        static bool ValidarSiguiente() => Application.isPlaying && almacen != null;

        static Punto Pu(double x, double y) => BancoDelMapa.Pu(x, y);

        static void Ir(int i)
        {
            var (nombre, x, y) = sitios[i];
            almacen.Buscar(TipoEntidad.Ejercito, "ej-banco").Ejercito.Posicion = Pu(x, y);
            Debug.Log($"[Banco de asentamientos] {nombre} ({x:0}, {y:0})");
        }

        static void Montar()
        {
            // el mundo en disco (el que bajó la última partida) y el banco (`BancoDelMapa.ElBanco`), con la cámara en la primera plaza
            var banco = BancoDelMapa.DelDisco("banco");
            banco.ElBanco();
            sitios.Clear();
            sitios.AddRange(banco.Sitios);
            (almacen, _, _) = banco.Montar("Banco de asentamientos", (sitios[0].x, sitios[0].y));
            actual = 0;
            Ir(0);
            Debug.Log($"[Banco de asentamientos] {sitios.Count} plazas; estilo {MaquetasDelMapa.Estilo}. «Conquest/Mundo/Banco: siguiente plaza» lleva a la siguiente.");
        }
    }
}
