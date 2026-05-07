using System;
using UnityEngine;

namespace GameFrameworkExt.Animation
{
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleFlyTarget : MonoBehaviour
    {
        private ParticleSystem _particleSystem;

        private ParticleSystem.Particle[] _particlesArray;
        

        private int _particleCount;
        // [LabelText("粒子持续时间")]
        // public float liftTime = 1f;
        
        public float maxSpeed = 1f;

        public AnimationCurve speedCurve;

        public bool useFlyScaleCurve;
        public AnimationCurve scaleCurve;
        // [LabelText("飞行缩放系数")]
        // public float flyScaleRatio;
        public float delayTime = 1f;
   
        private float _waitFlyTime = 0;

        private float _effectiveTime = 0; //生效时间

        public Transform target;
        public bool dynamicCount;
        private float _durationTime;
        public Vector3 rotationSpeed;
        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
            _particlesArray = new ParticleSystem.Particle[_particleSystem.main.maxParticles];
            _waitFlyTime = delayTime;
            _effectiveTime = 0;
            _durationTime = 0;
        }

        public Action endCallBack;
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");

        private Vector3 GetTargetPos()
        {
            if (FlyZModel) //3D飞行模式
            {
                return target.position;
            }
            else
            {
                return new Vector3(target.position.x,target.position.y, transform.position.z);
            }
        }
   
        public bool FlyZModel { get; set; }
        private void Update()
        {
            var deltaTime = Time.deltaTime;
            _durationTime += deltaTime;
            // //检查粒子生命周期
            // if (_durationTime >= liftTime)
            // {
            //     OnParticleStopped();
            //     return;
            // }
            
            if (!_particleSystem || target == null)
            {
                return;
            }
            
            _waitFlyTime -= deltaTime;
            if (_waitFlyTime > 0)
            {
                return;
            }
            
            if (_effectiveTime == 0 && useFlyScaleCurve)
            {
                var sizeOver = _particleSystem.sizeOverLifetime;
                sizeOver.enabled = true;
                sizeOver.size = new ParticleSystem.MinMaxCurve(1.0f,scaleCurve); 
            }
            
            _particleCount = _particleSystem.GetParticles(_particlesArray);
            if (_particleCount < 1)
            {
                OnParticleStopped();
                return;
            }
            
            var currentSpeed  = speedCurve.Evaluate(_effectiveTime) * maxSpeed;
            _effectiveTime += deltaTime;

            var targetPos = GetTargetPos();
            for (var i = 0; i < _particleCount; i++)
            {
                float timeAlive = _particlesArray[i].startLifetime - _particlesArray[i].remainingLifetime;
                if (timeAlive >= delayTime)
                {
                    // var delayTimeRatio = Mathf.Exp((_effectiveTime - _particlesArray[i].startLifetime) * lifetimeGrowthRate);
                    // if (_particlesArray[i].startLifetime < delayTimeRatio)
                    // {
                    //     continue;
                    // }
                    
                    var particlePos = _particlesArray[i].position;
                    var direction = (targetPos - particlePos).normalized; // 计算方向
                    var moveDistance = direction * (currentSpeed) * deltaTime;

                    var rotationOffset = new Vector3(rotationSpeed.x, rotationSpeed.y, rotationSpeed.z) * deltaTime;
                    _particlesArray[i].rotation3D += rotationOffset;
                    
                    // _particlesArray[i].startSize
                    // 移动到目标,就消失
                    if (Vector3.Distance(particlePos, targetPos) < moveDistance.magnitude)
                    {
                        _particlesArray[i].remainingLifetime = 0;
                    }
                    else
                    {
                        _particlesArray[i].position += moveDistance;
                    }
                }
                // _particlesArray[i].velocity = direction * currentSpeed; //设置速度
            }

            _particleSystem.SetParticles(_particlesArray, _particleCount);
        }

        public void Play()
        {
            enabled = true;
            if (_particleSystem == null)
            {
                _particleSystem = this.GetComponent<ParticleSystem>();
            }

            if (useFlyScaleCurve)
            {
                var particleSystemSizeOverLifetime = _particleSystem.sizeOverLifetime;
                particleSystemSizeOverLifetime.enabled = false;
            }
            _waitFlyTime = delayTime;
            _effectiveTime = 0;
            _durationTime = 0;
            _particleSystem.Stop();
            _particleSystem.Clear();
            _particleSystem.Play();
        }
        
        private void OnParticleStopped()
        {
            Debug.Log($"{name},OnParticleSystemStopped");
            endCallBack?.Invoke();
            endCallBack = null;
            enabled = false;
        }

        public void SetMaxParticleCount(int max)
        {
            var particleSystemMain = _particleSystem.main;
            particleSystemMain.maxParticles = max;
        }

        public void SetMainTexture(Texture2D texture2D)
        {
            var psRenderer = GetComponent<ParticleSystemRenderer>();
            psRenderer.material.SetTexture(MainTex,texture2D);
        }
    }
}