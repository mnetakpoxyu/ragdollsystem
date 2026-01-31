using UnityEngine;

/// <summary>
/// Прогревает шейдер обводки при старте сцены, чтобы при первом наведении на дверь не было микрофриза.
/// Вешать никуда не нужно — выполняется автоматически.
/// </summary>
public static class OutlineShaderWarmup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Warmup()
    {
        Shader s = Shader.Find("NewCore/Outline Contour");
        if (s == null) return;
        Material mat = new Material(s);
        mat.SetFloat("_OutlineWidth", 0.08f);
        mat.SetFloat("_RGBSpeed", 1f);
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "OutlineWarmup";
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.position = new Vector3(-10000f, -10000f, -10000f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.Destroy(go, 0.15f);
        Object.Destroy(mat, 0.2f);
    }
}
