using FairyGUI;
using UnityEngine;

/// <summary>
/// 粒子先自由炸开一小段时间，再沿直线匀速飞向舞台坐标系「屏幕底边以下、水平居中」的世界坐标点；
/// 抵达阈值距离后将粒子立刻清空寿命；全部抵达或消亡后销毁本物体。
/// </summary>
[DisallowMultipleComponent]
public sealed class ShardParticleCollectDriver : MonoBehaviour
{
    ParticleSystem _ps;
    float _burstPhase = 0.34f;
    float _pixelsPastBottom = 220f;
    float _straightFlightSeconds = 0.55f;
    float _arriveDistSq = 0.0144f; // 默认 ~0.12 world units
    float _startTime;
    bool _configured;
    bool _phase2Prepared;

    Vector3 _targetWorld;

    ParticleSystem.Particle[] _buf;

    /// <summary>
    /// 须在 <see cref="ParticleSystem.Play"/> 之前调用。
    /// </summary>
    /// <param name="straightFlightSeconds">第二阶段直线匀速飞行时长（按当前距离 / 该秒数 设定速度）。</param>
    /// <param name="arriveDistance">距目标小于该值视为抵达并销毁粒子。</param>
    public void Configure(
        ParticleSystem ps,
        float burstPhaseSeconds,
        float pixelsPastBottomEdge,
        float straightFlightSeconds,
        float arriveDistance = 0.12f)
    {
        _ps = ps;
        _burstPhase = Mathf.Max(0.05f, burstPhaseSeconds);
        _pixelsPastBottom = pixelsPastBottomEdge;
        _straightFlightSeconds = Mathf.Max(0.08f, straightFlightSeconds);
        float ad = Mathf.Max(0.02f, arriveDistance);
        _arriveDistSq = ad * ad;
        _startTime = Time.time;
        _configured = true;
        CacheTargetWorld();
    }

    void CacheTargetWorld()
    {
        Stage st = Stage.inst;
        if (st != null)
        {
            float sx = st.width * 0.5f;
            float sy = st.height + _pixelsPastBottom;
            _targetWorld = st.cachedTransform.TransformPoint(sx, -sy, 0f);
            return;
        }

        Camera cam = StageCamera.main;
        if (cam != null)
        {
            float depth = Mathf.Abs(cam.transform.position.z);
            _targetWorld = cam.ViewportToWorldPoint(new Vector3(0.5f, -0.14f, depth));
            return;
        }

        _targetWorld = transform.position + Vector3.down * 80f;
    }

    void LateUpdate()
    {
        if (!_configured || _ps == null)
            return;

        float elapsed = Time.time - _startTime;

        if (!_phase2Prepared && elapsed >= _burstPhase)
        {
            EnterStraightCollectPhase();
            _phase2Prepared = true;
        }

        if (!_phase2Prepared)
            return;

        int cap = _ps.main.maxParticles;
        if (_buf == null || _buf.Length < cap)
            _buf = new ParticleSystem.Particle[cap];

        int n = _ps.GetParticles(_buf);
        if (n <= 0)
        {
            Destroy(gameObject);
            return;
        }

        int alive = 0;
        for (int i = 0; i < n; i++)
        {
            ParticleSystem.Particle p = _buf[i];
            if (p.remainingLifetime <= 0.0001f)
                continue;

            Vector3 to = _targetWorld - p.position;
            if (to.sqrMagnitude <= _arriveDistSq)
            {
                p.remainingLifetime = 0f;
                _buf[i] = p;
                continue;
            }

            alive++;
            _buf[i] = p;
        }

        _ps.SetParticles(_buf, n);

        if (alive == 0)
            Destroy(gameObject);
    }

    void EnterStraightCollectPhase()
    {
        ParticleSystem.MainModule main = _ps.main;
        main.gravityModifier = 0f;

        ParticleSystem.LimitVelocityOverLifetimeModule lv = _ps.limitVelocityOverLifetime;
        lv.enabled = false;

        ParticleSystem.VelocityOverLifetimeModule vel = _ps.velocityOverLifetime;
        vel.enabled = false;

        int cap = _ps.main.maxParticles;
        if (_buf == null || _buf.Length < cap)
            _buf = new ParticleSystem.Particle[cap];

        int n = _ps.GetParticles(_buf);
        float flight = _straightFlightSeconds;

        for (int i = 0; i < n; i++)
        {
            ParticleSystem.Particle p = _buf[i];
            if (p.remainingLifetime <= 0.0001f)
                continue;

            Vector3 pos = p.position;
            Vector3 to = _targetWorld - pos;
            float distSq = to.sqrMagnitude;
            if (distSq <= _arriveDistSq)
            {
                p.remainingLifetime = 0f;
                _buf[i] = p;
                continue;
            }

            float dist = Mathf.Sqrt(distSq);
            Vector3 dir = to / dist;
            float speed = dist / flight;
            p.velocity = dir * speed;
            _buf[i] = p;
        }

        _ps.SetParticles(_buf, n);
    }
}
