// Run with: unity command eval_file ArtSource/PikemenLine/render-review.cs
var s = PikemenFormationSetup.ReadSettings();
var preview = new UnityEditor.PreviewRenderUtility();
try
{
    var clone = UnityEngine.Object.Instantiate(GameObject.Find(s.previewName));
    clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    preview.AddSingleGO(clone);
    foreach (var animator in clone.GetComponentsInChildren<Animator>())
    {
        var position = animator.transform.localPosition;
        animator.transform.SetParent(null);
        animator.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var rank = animator.name.Contains("Front") ? "Front" : "Rear";
        AssetDatabase.LoadAssetAtPath<AnimationClip>(s.folder + "/Animations/Pike_" + rank + ".anim")
            .SampleAnimation(animator.gameObject, 0);
        animator.transform.SetParent(clone.transform);
        animator.transform.localPosition = position;
    }
    preview.camera.nearClipPlane = 0.01f;
    preview.camera.farClipPlane = 100;
    preview.camera.orthographic = true;
    preview.camera.orthographicSize = 2.6f;
    preview.camera.clearFlags = CameraClearFlags.SolidColor;
    preview.camera.backgroundColor = new Color(0.28f, 0.30f, 0.34f);
    preview.lights[0].intensity = 2;
    preview.lights[0].transform.rotation = Quaternion.Euler(40, 30, 0);
    preview.lights[1].intensity = 1.5f;
    preview.lights[1].transform.rotation = Quaternion.Euler(30, 210, 0);
    foreach (var view in new[] {
        ("side", new Vector3(6, 1.6f, 1.4f)),
        ("front", new Vector3(3, 4, 7)),
        ("opposite", new Vector3(-6, 2, 2)) })
    {
        preview.camera.transform.position = view.Item2;
        preview.camera.transform.LookAt(new Vector3(0, 0.7f, 1));
        preview.BeginStaticPreview(new Rect(0, 0, 1200, 900));
        preview.Render(true);
        var texture = preview.EndStaticPreview();
        System.IO.File.WriteAllBytes("ArtSource/PikemenLine/review-" + view.Item1 + ".png", texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }
    return "Rendered side, front and opposite views.";
}
finally { preview.Cleanup(); }
