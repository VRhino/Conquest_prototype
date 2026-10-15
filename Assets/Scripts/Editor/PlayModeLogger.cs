using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Escribe los logs de play mode en Logs/playmode.log (raíz del proyecto).
/// El archivo se limpia al entrar a play mode para que solo tenga la sesión actual.
/// Úsalo con: PlayModeLogger.Log("mensaje") o PlayModeLogger.Log("TAG", "mensaje")
/// Claude puede leer el archivo directamente sin necesidad de buscar en Editor.log.
/// </summary>
[InitializeOnLoad]
public static class PlayModeLogger
{
    private static readonly string LogFilePath =
        Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "playmode.log");

    private static StreamWriter _writer;

    static PlayModeLogger()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);

            // Limpiar el archivo al inicio de cada sesión
            File.WriteAllText(LogFilePath, $"=== Play mode started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");

            _writer = new StreamWriter(LogFilePath, append: true) { AutoFlush = true };
            Application.logMessageReceived += OnLogReceived;

            Debug.Log($"[PlayModeLogger] Logging to: {LogFilePath}");
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Application.logMessageReceived -= OnLogReceived;

            _writer?.WriteLine($"\n=== Play mode ended: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            _writer?.Close();
            _writer = null;
        }
    }

    private static void OnLogReceived(string message, string stackTrace, LogType type)
    {
        if (_writer == null) return;

        string prefix = type switch
        {
            LogType.Error     => "[ERROR]",
            LogType.Warning   => "[WARN] ",
            LogType.Exception => "[EXCEP]",
            _                 => "[INFO] ",
        };

        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        _writer.WriteLine($"{timestamp} {prefix} {message}");

        // Incluir stack trace solo en errores y excepciones
        if ((type == LogType.Error || type == LogType.Exception) && !string.IsNullOrEmpty(stackTrace))
        {
            foreach (var line in stackTrace.Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    _writer.WriteLine($"         {line.Trim()}");
            }
        }
    }

    // --- API opcional para logs con tags ---

    public static void Log(string message) => Debug.Log(message);

    public static void Log(string tag, string message) => Debug.Log($"[{tag}] {message}");

    public static void Warn(string tag, string message) => Debug.LogWarning($"[{tag}] {message}");

    public static void Error(string tag, string message) => Debug.LogError($"[{tag}] {message}");
}
