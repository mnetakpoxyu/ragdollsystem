using UnityEngine;

/// <summary>
/// Общая настройка рендера пара/дыма (игрок и NPC): шейдер, мягкая текстура, billboard.
/// Используется в PlayerHQDVape и ClientNPC (дым кальяна).
/// </summary>
public static class VaporParticleSetup
{
    static Texture2D _cachedSoftVaporTexture;

    /// <summary> Мягкая квадратная текстура пара (непрозрачный центр, прозрачные края). </summary>
    public static Texture2D GetSoftVaporTexture()
    {
        if (_cachedSoftVaporTexture != null) return _cachedSoftVaporTexture;
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;
        Color[] pixels = new Color[size * size];
        int edge = 4;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = x >= edge && x < size - edge && y >= edge && y < size - edge;
                float a = inside ? 1f : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(true, true);
        _cachedSoftVaporTexture = tex;
        return tex;
    }

    /// <summary> Настроить рендерер частиц пара/дыма так же, как у игрока (шейдер, текстура, billboard). </summary>
    public static void SetupVaporRenderer(ParticleSystemRenderer renderer, Color tint)
    {
        if (renderer == null) return;
        Shader shader = Shader.Find("NewCore/VaporParticle")
            ?? Shader.Find("Mobile/Particles/Alpha Blended")
            ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply")
            ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit");
        if (shader == null)
        {
            Debug.LogWarning("VaporParticleSetup: не найден шейдер для пара. Добавь NewCore/VaporParticle в Always Included Shaders.");
            return;
        }
        Material mat = new Material(shader);
        mat.mainTexture = GetSoftVaporTexture();
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", tint);
        else if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", tint);
        mat.renderQueue = 3000;
        renderer.material = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.minParticleSize = 0.001f;
        renderer.maxParticleSize = 10f;
    }

    /// <summary>
    /// Fallback: выпустить пар кальяна (когда нет PlayerHQDVape в сцене). Без родителя — иначе scale стола даёт огромные партиклы.
    /// </summary>
    public static GameObject EmitHookahVaporAt(Transform mouthPoint, Transform parent, Color? vaporColor = null)
    {
        if (mouthPoint == null) return null;
        Color color = vaporColor ?? new Color(0.95f, 0.95f, 0.98f, 0.55f);

        float normalizedHold = 0.5f; // «средняя» затяжка
        float countMul = 1f + Random.Range(-0.18f, 0.18f);
        int count = Mathf.RoundToInt(Mathf.Lerp(400f, 2800f, normalizedHold) * countMul);
        if (count <= 0) count = 800;
        float exhaleDuration = 1.25f;
        float rate = count / exhaleDuration;

        float sizeBase = Mathf.Lerp(0.65f, 2.2f, normalizedHold);
        float sizeMul = 1f + Random.Range(-0.22f, 0.22f);
        float sizeMin = sizeBase * sizeMul * 0.9f;
        float sizeMax = sizeBase * sizeMul * 1.6f;

        var vaporGo = new GameObject("HookahVapor");
        vaporGo.transform.SetParent(null);
        vaporGo.transform.position = mouthPoint.position;
        vaporGo.transform.rotation = mouthPoint.rotation;
        vaporGo.transform.localScale = Vector3.one;
        vaporGo.SetActive(false);

        var ps = vaporGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.duration = exhaleDuration;
        main.loop = false;
        main.startLifetime = 3.2f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 4500;
        main.gravityModifier = 0.01f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = rate;
        emission.SetBursts(new ParticleSystem.Burst[0]);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.04f;
        shape.rotation = Vector3.zero;

        // Направление выдоха — явно из mouthPoint.forward (мировые координаты), чтобы пар шёл вперёд
        Vector3 blowDir = mouthPoint.forward;
        if (blowDir.sqrMagnitude < 0.01f)
            blowDir = Vector3.forward;
        else
            blowDir.Normalize();

        float blowSpeed = 2.8f;
        float spread = 0.25f;

        // velocityOverLifetime в мировых координатах — пар уверенно выдувается вперёд
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(blowDir.x * blowSpeed - spread, blowDir.x * blowSpeed + spread);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(blowDir.y * blowSpeed - spread, blowDir.y * blowSpeed + spread);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(blowDir.z * blowSpeed - spread, blowDir.z * blowSpeed + spread);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.15f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(color.a * 0.92f, 0f), new GradientAlphaKey(color.a * 0.65f, 0.35f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = grad;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f), new Keyframe(0.12f, 1.15f), new Keyframe(0.35f, 1.5f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));

        var renderer = vaporGo.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
            SetupVaporRenderer(renderer, color);

        ParticlesCollisionSetup.SetupCollisionAndSplash(ps, color, addSplash: false, default, enableCollision: false);

        vaporGo.SetActive(true);
        ps.Play();
        UnityEngine.Object.Destroy(vaporGo, 5.5f);
        return vaporGo;
    }
}
