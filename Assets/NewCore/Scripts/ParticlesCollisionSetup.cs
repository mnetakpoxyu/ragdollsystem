using UnityEngine;

/// <summary>
/// Настройка коллизии частиц со стенами и объектами: пар/дым врезается в препятствия и даёт эффект распыления.
/// Вызывай для ParticleSystem пара (вейп) и дыма кальяна.
/// </summary>
public static class ParticlesCollisionSetup
{
    /// <summary>
    /// Включить коллизию с миром (стены, пол, объекты) и опционально суб-эмиттер «распыление» при ударе.
    /// </summary>
    /// <param name="ps">Система частиц (вейп или дым кальяна).</param>
    /// <param name="splashColor">Цвет частиц распыления при ударе о стену (обычно как у пара/дыма).</param>
    /// <param name="addSplash">Добавить ли суб-эмиттер: при ударе о стену — маленькое облачко «по разным сторонам».</param>
    /// <param name="ignoreLayers">Слои, с которыми частицы НЕ сталкиваются (пар проходит сквозь них). Укажи слой игрока — пар будет проходить сквозь модель.</param>
    /// <param name="enableCollision">Включить коллизию. false = пар не сталкивается ни с чем (для NPC кальяна — иначе застревает о стол).</param>
    public static void SetupCollisionAndSplash(ParticleSystem ps, Color splashColor, bool addSplash = true, LayerMask ignoreLayers = default, bool enableCollision = true)
    {
        if (ps == null) return;

        var collision = ps.collision;
        collision.enabled = enableCollision;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.dampen = new ParticleSystem.MinMaxCurve(0.92f);   // почти вся скорость теряется при ударе — пар «останавливается» о стену
        collision.bounce = new ParticleSystem.MinMaxCurve(0.12f);   // лёгкий отскок — визуально «распылился»
        collision.lifetimeLoss = 0.15f;
        // Со всеми слоями, кроме ignoreLayers — пар проходит сквозь игрока и не застревает в нём
        collision.collidesWith = (LayerMask)(~0 & ~ignoreLayers.value);
        collision.radiusScale = 0.6f;
        collision.sendCollisionMessages = false;

        if (!addSplash) return;

        // Суб-эмиттер: при коллизии в точке удара появляется маленькое облачко — пар «разбегается» по стене
        GameObject splashGo = new GameObject("CollisionSplash");
        splashGo.transform.SetParent(ps.transform);
        splashGo.transform.localPosition = Vector3.zero;
        splashGo.transform.localRotation = Quaternion.identity;
        splashGo.transform.localScale = Vector3.one;

        ParticleSystem splashPs = splashGo.AddComponent<ParticleSystem>();
        var main = splashPs.main;
        main.playOnAwake = false;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 0.2f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = new Color(splashColor.r, splashColor.g, splashColor.b, splashColor.a * 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;
        main.gravityModifier = 0.02f;
        main.playOnAwake = false;

        var emission = splashPs.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 4, 8) });

        var shape = splashPs.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.angle = 25f;
        shape.radius = 0.02f;

        var colorOverLifetime = splashPs.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(splashColor, 0f), new GradientColorKey(splashColor, 1f) },
            new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = grad;

        var renderer = splashGo.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply") ?? Shader.Find("Particles/Standard Unlit"));
            if (renderer.material.HasProperty("_Color")) renderer.material.SetColor("_Color", splashColor);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        var sub = ps.subEmitters;
        sub.enabled = true;
        sub.AddSubEmitter(splashPs, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing);
    }
}
