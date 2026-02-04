using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("NewCore/Darken Child Renderers")]
public class DarkenChildRenderers : MonoBehaviour
{
    [Header("FILTER")]
    [Tooltip("Менять цвет у всех Renderer в дочерних объектах.")]
    public bool includeInactive = true;

    [Header("COLOR TINT")]
    [Tooltip("Тон, которым затемняется исходный цвет (тёмно-серый по умолчанию).")]
    public Color tint = new Color(0.35f, 0.35f, 0.35f, 1f);
    [Tooltip("Дополнительный множитель яркости (0.6–0.9 обычно достаточно).")]
    [Range(0.1f, 1f)] public float brightnessMultiplier = 0.8f;

    [Header("EMISSION (OPTIONAL)")]
    public bool affectEmission = false;
    [Range(0f, 1f)] public float emissionMultiplier = 0.5f;

    private readonly Dictionary<Material, Color> _originalColors = new Dictionary<Material, Color>();
    private readonly Dictionary<Material, Color> _originalEmission = new Dictionary<Material, Color>();

    private void OnEnable()
    {
        ApplyDarkening();
    }

    private void OnDisable()
    {
        RestoreOriginals();
    }

    public void ApplyDarkening()
    {
        var renderers = GetComponentsInChildren<Renderer>(includeInactive);
        foreach (var renderer in renderers)
        {
            var materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null) continue;

                var colorProperty = GetColorProperty(mat);
                if (!string.IsNullOrEmpty(colorProperty))
                {
                    if (!_originalColors.ContainsKey(mat))
                        _originalColors[mat] = mat.GetColor(colorProperty);

                    var original = _originalColors[mat];
                    var tinted = new Color(
                        original.r * tint.r,
                        original.g * tint.g,
                        original.b * tint.b,
                        original.a
                    );
                    var final = tinted * brightnessMultiplier;
                    final.a = original.a;
                    mat.SetColor(colorProperty, final);
                }

                if (affectEmission && mat.HasProperty("_EmissionColor"))
                {
                    if (!_originalEmission.ContainsKey(mat))
                        _originalEmission[mat] = mat.GetColor("_EmissionColor");

                    var originalEmission = _originalEmission[mat];
                    var finalEmission = originalEmission * emissionMultiplier;
                    mat.SetColor("_EmissionColor", finalEmission);
                }
            }
        }
    }

    private void RestoreOriginals()
    {
        foreach (var pair in _originalColors)
        {
            var mat = pair.Key;
            if (mat == null) continue;
            var colorProperty = GetColorProperty(mat);
            if (!string.IsNullOrEmpty(colorProperty))
                mat.SetColor(colorProperty, pair.Value);
        }

        foreach (var pair in _originalEmission)
        {
            var mat = pair.Key;
            if (mat == null) continue;
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", pair.Value);
        }

        _originalColors.Clear();
        _originalEmission.Clear();
    }

    private static string GetColorProperty(Material mat)
    {
        if (mat.HasProperty("_BaseColor")) return "_BaseColor";
        if (mat.HasProperty("_Color")) return "_Color";
        return null;
    }
}
