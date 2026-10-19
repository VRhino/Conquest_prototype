#if UNITY_EDITOR
using System.IO;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

/// <summary>
/// Records the Game view to MP4 with Unity Recorder for the movement baseline scenarios.
/// Output goes to Recordings/MovementBaseline (outside Assets and git). Constant 30 fps playback
/// fixes the simulation step, so baseline and later runs are comparable frame by frame.
/// </summary>
sealed class MovementVideoCapture : System.IDisposable
{
    const int Width = 1280;
    const int Height = 720;
    const float FrameRate = 30f;

    readonly RecorderController controller;
    readonly RecorderControllerSettings controllerSettings;
    readonly MovieRecorderSettings movie;
    public string OutputFile { get; }

    MovementVideoCapture(string label)
    {
        string directory = Path.Combine(Directory.GetCurrentDirectory(), "Recordings", "MovementBaseline");
        Directory.CreateDirectory(directory);
        OutputFile = Path.Combine(directory, $"{label}_{System.DateTime.Now:yyyyMMdd_HHmmss}");

        controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = "MovementBaseline";
        movie.Enabled = true;
        movie.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Medium
        };
        movie.CaptureAudio = false;
        movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = Width, OutputHeight = Height };
        movie.OutputFile = OutputFile;

        controllerSettings.AddRecorderSettings(movie);
        controllerSettings.SetRecordModeToManual();
        controllerSettings.FrameRatePlayback = FrameRatePlayback.Constant;
        controllerSettings.FrameRate = FrameRate;
        controllerSettings.CapFrameRate = true;
        controller = new RecorderController(controllerSettings);
    }

    public static MovementVideoCapture Start(string label)
    {
        var capture = new MovementVideoCapture(label);
        capture.controller.PrepareRecording();
        capture.controller.StartRecording();
        return capture;
    }

    public void Dispose()
    {
        if (controller.IsRecording()) controller.StopRecording();
        Object.DestroyImmediate(movie);
        Object.DestroyImmediate(controllerSettings);
    }
}
#endif
