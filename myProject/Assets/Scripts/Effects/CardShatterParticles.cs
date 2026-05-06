using FairyGUI;
using UnityEngine;

/// <summary>
/// 在 FairyGUI 卡牌位置生成一次性的「碎块向外飘散」粒子（Unity <see cref="ParticleSystem"/>），播完自动销毁。
/// </summary>
public static class CardShatterParticles
{
    const float DestroyAfterSeconds = 2.5f;
    const string ShardMaterialResourcePath = "VitaMJ/CardShatterWhiteShard";

    static Material _sharedShardMat;

    static Material ResolveShardMaterial()
    {
        if (_sharedShardMat != null)
            return _sharedShardMat;

        _sharedShardMat = Resources.Load<Material>(ShardMaterialResourcePath);
        if (_sharedShardMat == null)
        {
            Shader sh = Shader.Find("VitaMJ/Particles/WhiteShardUnlit");
            if (sh != null)
                _sharedShardMat = new Material(sh);
        }

        return _sharedShardMat;
    }

    public static void PlayAtCard(GComponent card)
    {
        if (card == null || card.isDisposed || card.displayObject == null)
            return;

        if (!TryGetWorldExtents(card, out Vector3 worldCenter, out float boxW, out float boxH))
        {
            worldCenter = card.displayObject.cachedTransform.position;
            boxW = boxH = 0.25f;
        }

        if (MahjongBaoshiShardParticles.TryPlay(worldCenter, boxW, boxH, ResolveShardMaterial()))
            return;

        SpawnBillboardFallback(worldCenter, boxW, boxH);
    }

    static bool TryGetWorldExtents(GComponent card, out Vector3 worldCenter, out float boxW, out float boxH)
    {
        worldCenter = default;
        boxW = boxH = 0.1f;
        Transform tr = card.displayObject.cachedTransform;
        Stage st = Stage.inst;
        if (st != null)
        {
            Vector2 g = card.LocalToGlobal(new Vector2(card.width * 0.5f, card.height * 0.5f));
            worldCenter = st.cachedTransform.TransformPoint(g.x, -g.y, 0f);
            Vector3 dx = st.cachedTransform.TransformVector(new Vector3(card.width, 0f, 0f));
            Vector3 dy = st.cachedTransform.TransformVector(new Vector3(0f, -card.height, 0f));
            boxW = Mathf.Max(0.02f, dx.magnitude);
            boxH = Mathf.Max(0.02f, dy.magnitude);
            return true;
        }

        worldCenter = tr.TransformPoint(card.width * 0.5f, -card.height * 0.5f, 0f);
        boxW = Mathf.Max(0.02f, tr.TransformVector(new Vector3(card.width, 0f, 0f)).magnitude);
        boxH = Mathf.Max(0.02f, tr.TransformVector(new Vector3(0f, -card.height, 0f)).magnitude);
        return true;
    }

    static void SpawnBillboardFallback(Vector3 worldCenter, float worldW, float worldH)
    {
        var go = new GameObject("CardShatterFx");
        go.transform.SetPositionAndRotation(worldCenter, Quaternion.identity);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.06f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.74f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6.5f);
        float refSize = (worldW + worldH) * 0.5f;
        float sz = Mathf.Lerp(0.022f, 0.1f, Mathf.Clamp01(refSize / 160f));
        main.startSize = new ParticleSystem.MinMaxCurve(sz * 0.5f, sz * 1.05f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.28f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.97f, 0.88f, 1f),
            new Color(0.94f, 0.89f, 0.76f, 1f));

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(worldW, worldH, 0.04f);
        shape.randomDirectionAmount = 0.28f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)38, (short)58),
        });

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve shrink = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0.1f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, shrink);

        var colLife = ps.colorOverLifetime;
        colLife.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colLife.color = new ParticleSystem.MinMaxGradient(gradient);

        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.separateAxes = true;
        rol.x = new ParticleSystem.MinMaxCurve(-2.8f, 2.8f);
        rol.y = new ParticleSystem.MinMaxCurve(-2.8f, 2.8f);
        rol.z = new ParticleSystem.MinMaxCurve(-2.8f, 2.8f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        vel.y = new ParticleSystem.MinMaxCurve(0.9f, 2.9f);

        var r = ps.GetComponent<ParticleSystemRenderer>();
        Material mat = ResolveShardMaterial();
        if (mat == null)
        {
            mat = ParticleSystem.defaultMaterial;
            if (mat == null)
                mat = Resources.GetBuiltinResource<Material>("Default-Particle.mat");
        }

        if (mat != null)
            r.sharedMaterial = mat;

        ps.Play();
        Object.Destroy(go, DestroyAfterSeconds);
    }
}
