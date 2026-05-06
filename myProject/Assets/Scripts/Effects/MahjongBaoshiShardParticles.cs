using FairyGUI;
using UnityEngine;

/// <summary>
/// 麻将消除用 3D 碎块粒子：预制体路径 <see cref="FxPrefabResourcePath"/>（含 ParticleSystem + ShardParticleCollectDriver），
/// 运行时仍由此类写入模块参数；预制体缺失时用同名空物体 + AddComponent 兜底。
/// </summary>
public static class MahjongBaoshiShardParticles
{
    /// <summary>与 Resources 目录下 FBX（无扩展名）路径一致：<c>VitaMJ/Effects/Models/baoshi.FBX</c>。</summary>
    public const string BaoshiMeshResourcePath = "VitaMJ/Effects/Models/baoshi";

    /// <summary>Resources 路径（无扩展名）。Unity 首次打开工程后由 Editor 脚本生成预制体。</summary>
    public const string FxPrefabResourcePath = "VitaMJ/Prefabs/MahjongBaoshiShardsFx";

    const float DestroyAfterSeconds = 6.85f;

    static Mesh _cachedMesh;

    public static Mesh CachedShardMeshOrNull => _cachedMesh;

    /// <summary>若已成功加载 Baoshi 网格则返回 true，并在 <paramref name="worldCenter"/> 处播放一局。</summary>
    public static bool TryPlay(Vector3 worldCenter, float footprintWorldW, float footprintWorldH,
        Material particleMaterialOrNull)
    {
        Mesh mesh = ResolveBaoshiShardMesh();
        if (mesh == null)
            return false;

        particleMaterialOrNull = ResolveParticleMaterial(particleMaterialOrNull);
        if (particleMaterialOrNull == null)
            return false;

        float refSpan = Mathf.Max((footprintWorldW + footprintWorldH) * 0.5f, 2f);

        GameObject root = ShardFxPrefabSpawn.InstantiateUiFx(FxPrefabResourcePath, worldCenter, "MahjongBaoshiShards");

        ParticleSystem ps = root.GetComponent<ParticleSystem>();
        if (ps == null)
            ps = root.AddComponent<ParticleSystem>();

        ps.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ConfigureMahjongBurst(ps, mesh, particleMaterialOrNull, refSpan);

        ShardParticleCollectDriver collect = root.GetComponent<ShardParticleCollectDriver>();
        if (collect == null)
            collect = root.AddComponent<ShardParticleCollectDriver>();

        collect.Configure(ps,
            burstPhaseSeconds: 0.36f,
            pixelsPastBottomEdge: 240f,
            straightFlightSeconds: 0.52f,
            arriveDistance: 0.11f);

        ps.Play();
        Object.Destroy(root, DestroyAfterSeconds);
        return true;
    }

    static Material ResolveParticleMaterial(Material particleMaterialOrNull)
    {
        if (particleMaterialOrNull != null)
            return particleMaterialOrNull;

        particleMaterialOrNull = Resources.Load<Material>("VitaMJ/MahjongRockChunk");
        if (particleMaterialOrNull == null)
        {
            Shader rk = Shader.Find("VitaMJ/Particles/RockChunkUnlit");
            if (rk != null)
                particleMaterialOrNull = new Material(rk);
        }

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

        return particleMaterialOrNull;
    }

    static void ConfigureMahjongBurst(ParticleSystem ps, Mesh mesh, Material particleMaterial, float refSpan)
    {
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.simulationSpeed = 1f;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 420;

        main.startLifetime = new ParticleSystem.MinMaxCurve(4.2f, 5.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(7.5f, 15f);

        float szMin = Mathf.Lerp(0.044f, 0.068f, Mathf.Clamp01(refSpan / 180f));
        float szMax = Mathf.Lerp(0.23f, 0.42f, Mathf.Clamp01(refSpan / 200f));
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(szMin, szMax);
        main.startSizeY = main.startSizeX;
        // Z 略厚于 XY，减少「薄片」观感。
        main.startSizeZ = new ParticleSystem.MinMaxCurve(szMin * 1.02f, szMax * 1.22f);

        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(-Mathf.PI * 2f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(-Mathf.PI * 2f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(-Mathf.PI * 2f, Mathf.PI * 2f);

        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.78f, 1.28f);

        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, Color.white);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        int burstMin = Mathf.Clamp(Mathf.RoundToInt(56f + refSpan * 0.17f), 46, 118);
        int burstMax = Mathf.Clamp(burstMin + 26, burstMin + 1, 148);
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)burstMin, (short)burstMax),
        });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Clamp(refSpan * 0.00185f, 0.028f, 0.34f);
        shape.randomDirectionAmount = 0.035f;

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
                new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(1f, 0.88f),
                new GradientAlphaKey(0f, 1f),
            });
        col.color = new ParticleSystem.MinMaxGradient(g);

        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve shrink = new AnimationCurve(
            new Keyframe(0f, 1f, -0.65f, -0.65f),
            new Keyframe(1f, 0.32f));
        shrink.postWrapMode = WrapMode.Clamp;
        sol.size = new ParticleSystem.MinMaxCurve(1f, shrink);

        ParticleSystem.LimitVelocityOverLifetimeModule lv = ps.limitVelocityOverLifetime;
        lv.enabled = true;
        lv.space = ParticleSystemSimulationSpace.World;
        lv.dampen = 0.14f;

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = false;

        ParticleSystemRenderer rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Mesh;
        rend.mesh = mesh;
        rend.sharedMaterial = particleMaterial;
        rend.sortingOrder = 520;
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
