using UnityEngine;

/// <summary>
/// 麻将消除用 3D 碎块粒子：移植自 FairyClient_BK 中 <c>fx_ui_boashi_da.prefab</c> 子节点「dian (3)」的
/// ParticleSystem（baoshi.FBX 网格 burst + 球形发射）；原工程的 URP/Nuwa 材质改为项目内置的
/// <see cref="Shader"/> 「VitaMJ/Particles/WhiteShardUnlit」材质。
/// </summary>
public static class MahjongBaoshiShardParticles
{
    /// <summary>与 Resources 目录下 FBX（无扩展名）路径一致：<c>VitaMJ/Effects/Models/baoshi.FBX</c>。</summary>
    public const string BaoshiMeshResourcePath = "VitaMJ/Effects/Models/baoshi";

    const float DestroyAfterSeconds = 3f;

    static Mesh _cachedMesh;

    public static Mesh CachedShardMeshOrNull => _cachedMesh;

    /// <summary>若已成功加载 Baoshi 网格则返回 true，并在 <paramref name="worldCenter"/> 处播放一局。</summary>
    public static bool TryPlay(Vector3 worldCenter, float footprintWorldW, float footprintWorldH,
        Material particleMaterialOrNull)
    {
        Mesh mesh = ResolveBaoshiShardMesh();
        if (mesh == null)
            return false;

        if (particleMaterialOrNull == null)
        {
            particleMaterialOrNull = Resources.Load<Material>("VitaMJ/CardShatterWhiteShard");
            if (particleMaterialOrNull == null)
            {
                Shader sh = Shader.Find("VitaMJ/Particles/WhiteShardUnlit");
                if (sh != null)
                    particleMaterialOrNull = new Material(sh);
            }
        }

        if (particleMaterialOrNull == null)
            return false;

        GameObject root = new GameObject("MahjongBaoshiShards");
        root.transform.position = worldCenter;
        root.transform.rotation = Quaternion.identity;

        ParticleSystem ps = root.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.simulationSpeed = 1f;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 100;

        float refSpan = Mathf.Max((footprintWorldW + footprintWorldH) * 0.5f, 2f);

        main.startLifetime = new ParticleSystem.MinMaxCurve(1.35f, 2.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 20f);

        float szMin = Mathf.Lerp(0.05f, 0.08f, Mathf.Clamp01(refSpan / 180f));
        float szMax = Mathf.Lerp(0.28f, 0.52f, Mathf.Clamp01(refSpan / 200f));
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(szMin, szMax);
        main.startSizeY = main.startSizeX;
        main.startSizeZ = new ParticleSystem.MinMaxCurve(szMin * 0.82f, szMax * 0.95f);

        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 0f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 0f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(Mathf.Min(-16.755161f, 6.283185f),
            Mathf.Max(-16.755161f, 6.283185f));

        main.gravityModifier = new ParticleSystem.MinMaxCurve(0f, 0.52f);

        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.94f, 0.93f, 0.91f, 1f),
            new Color(0.995f, 1f, 0.98f, 1f));

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        int burstMin = Mathf.Clamp(Mathf.RoundToInt(24f + refSpan * 0.08f), 16, 48);
        int burstMax = Mathf.Clamp(burstMin + 12, burstMin + 1, 55);
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)burstMin, (short)burstMax),
        });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Clamp(refSpan * 0.0022f, 0.038f, 0.42f);
        shape.randomDirectionAmount = 0f;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.92f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.14f),
                new GradientAlphaKey(0.94f, 0.74f),
                new GradientAlphaKey(0f, 1f),
            });
        col.color = new ParticleSystem.MinMaxGradient(g);

        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve shrink = new AnimationCurve(
            new Keyframe(0f, 1f, -1.62f, -1.62f),
            new Keyframe(1f, 0f));
        shrink.postWrapMode = WrapMode.Clamp;
        sol.size = new ParticleSystem.MinMaxCurve(1f, shrink);

        ParticleSystem.LimitVelocityOverLifetimeModule lv = ps.limitVelocityOverLifetime;
        lv.enabled = true;
        lv.space = ParticleSystemSimulationSpace.World;
        lv.dampen = 0.5f;

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.32f, 0.72f);
        noise.frequency = 0.55f;
        noise.scrollSpeed = 0.22f;
        noise.quality = ParticleSystemNoiseQuality.Low;

        ParticleSystemRenderer rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Mesh;
        rend.mesh = mesh;
        rend.sharedMaterial = particleMaterialOrNull;
        rend.sortingOrder = 520;

        ps.Play();
        Object.Destroy(root, DestroyAfterSeconds);
        return true;
    }

    static Mesh ResolveBaoshiShardMesh()
    {
        if (_cachedMesh != null)
            return _cachedMesh;

        Mesh[] meshes = Resources.LoadAll<Mesh>(BaoshiMeshResourcePath);
        if (meshes == null || meshes.Length == 0)
            return null;

        Mesh best = meshes[0];
        int bestV = best != null ? best.vertexCount : 0;
        for (int i = 1; i < meshes.Length; i++)
        {
            if (meshes[i] != null && meshes[i].vertexCount > bestV)
            {
                best = meshes[i];
                bestV = meshes[i].vertexCount;
            }
        }

        _cachedMesh = best;
        return _cachedMesh;
    }
}
