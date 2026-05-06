using FairyGUI;
using UnityEngine;

/// <summary>
/// 在 FairyGUI 卡牌位置生成一次性的「碎块向外飘散」粒子（Unity <see cref="ParticleSystem"/>）。
/// 主路径预制体 <see cref="MahjongBaoshiShardParticles.FxPrefabResourcePath"/>；兜底 Billboard 预制体见 <see cref="BillboardFxPrefabResourcePath"/>。
/// </summary>
public static class CardShatterParticles
{
    const float DestroyAfterSeconds = 6.6f;
    const string ShardMaterialResourcePath = "VitaMJ/CardShatterWhiteShard";

    /// <summary>Resources 路径（无扩展名）。</summary>
    public const string BillboardFxPrefabResourcePath = "VitaMJ/Prefabs/CardShatterBillboardFx";

    static Material _sharedShardMat;

    static Material ResolveShardMaterial()
    {
        if (_sharedShardMat != null)
            return _sharedShardMat;

        _sharedShardMat = Resources.Load<Material>("VitaMJ/MahjongRockChunk");
        if (_sharedShardMat == null)
        {
            Shader rock = Shader.Find("VitaMJ/Particles/RockChunkUnlit");
            if (rock != null)
                _sharedShardMat = new Material(rock);
        }

        if (_sharedShardMat == null)
        {
            _sharedShardMat = Resources.Load<Material>(ShardMaterialResourcePath);
            if (_sharedShardMat == null)
            {
                Shader sh = Shader.Find("VitaMJ/Particles/WhiteShardUnlit");
                if (sh != null)
                    _sharedShardMat = new Material(sh);
            }
        }

        return _sharedShardMat;
    }

    public static void PlayAtCard(GComponent card)
    {
        if (card == null || card.isDisposed || card.displayObject == null)
            return;

        TryGetWorldExtents(card, out Vector3 worldCenter, out float boxW, out float boxH);

        if (MahjongBaoshiShardParticles.TryPlay(worldCenter, boxW, boxH, ResolveShardMaterial()))
            return;

        SpawnBillboardFallback(worldCenter, boxW, boxH);
    }

    static void TryGetWorldExtents(GComponent card, out Vector3 worldCenter, out float boxW, out float boxH)
    {
        worldCenter = default;
        boxW = boxH = 0.1f;

        // 用对角顶点求中心：与 (width/2,height/2) 不同，能正确处理 pivotAsAnchor / 轴对齐布局。
        Vector2 g0 = card.LocalToGlobal(Vector2.zero);
        Vector2 gDiag = card.LocalToGlobal(new Vector2(card.width, card.height));
        Vector2 gCenter = (g0 + gDiag) * 0.5f;

        Vector2 gWx = card.LocalToGlobal(new Vector2(card.width, 0f));
        Vector2 gHy = card.LocalToGlobal(new Vector2(0f, card.height));
        boxW = Mathf.Max(0.02f, Vector2.Distance(g0, gWx));
        boxH = Mathf.Max(0.02f, Vector2.Distance(g0, gHy));

        Stage st = Stage.inst;
        if (st != null)
        {
            worldCenter = st.cachedTransform.TransformPoint(gCenter.x, -gCenter.y, 0f);
            return;
        }

        Vector3 w0 = card.displayObject.LocalToWorld(Vector3.zero);
        Vector3 wDiag = card.displayObject.LocalToWorld(new Vector3(card.width, card.height, 0f));
        worldCenter = (w0 + wDiag) * 0.5f;
    }

    static void SpawnBillboardFallback(Vector3 worldCenter, float worldW, float worldH)
    {
        GameObject go = ShardFxPrefabSpawn.InstantiateUiFx(BillboardFxPrefabResourcePath, worldCenter, "CardShatterFx");

        ParticleSystem ps = go.GetComponent<ParticleSystem>();
        if (ps == null)
            ps = go.AddComponent<ParticleSystem>();

        ps.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ConfigureBillboardBurst(ps, worldW, worldH);

        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        Material mat = ResolveShardMaterial();
        if (mat == null)
        {
            mat = Resources.GetBuiltinResource<Material>("Default-Particle.mat");
            if (mat == null)
            {
                Shader sh = Shader.Find("Particles/Standard Unlit")
                    ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                    ?? Shader.Find("Sprites/Default");
                if (sh != null)
                    mat = new Material(sh);
            }
        }

        if (mat != null)
            r.sharedMaterial = mat;

        r.sortingOrder = 520;

        ShardParticleCollectDriver collect = go.GetComponent<ShardParticleCollectDriver>();
        if (collect == null)
            collect = go.AddComponent<ShardParticleCollectDriver>();

        collect.Configure(ps,
            burstPhaseSeconds: 0.34f,
            pixelsPastBottomEdge: 240f,
            straightFlightSeconds: 0.5f,
            arriveDistance: 0.11f);

        ps.Play();
        Object.Destroy(go, DestroyAfterSeconds);
    }

    static void ConfigureBillboardBurst(ParticleSystem ps, float worldW, float worldH)
    {
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.06f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 5.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6.5f);
        float refSize = (worldW + worldH) * 0.5f;
        float sz = Mathf.Lerp(0.018f, 0.078f, Mathf.Clamp01(refSize / 160f));
        main.startSize = new ParticleSystem.MinMaxCurve(sz * 0.48f, sz * 1.02f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.92f, 1.35f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 520;
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, Color.white);

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(worldW * 0.72f, worldH * 0.72f, 0.09f);
        shape.randomDirectionAmount = 0.16f;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)92, (short)136),
        });

        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve shrink = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0.38f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, shrink);

        ParticleSystem.ColorOverLifetimeModule colLife = ps.colorOverLifetime;
        colLife.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.84f),
                new GradientAlphaKey(0f, 1f),
            });
        colLife.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.RotationOverLifetimeModule rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.separateAxes = true;
        rol.x = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);
        rol.y = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);
        rol.z = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);

        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.65f);
    }
}
