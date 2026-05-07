using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameFrameworkExt.Animation
{
    public class ParticleController : MonoBehaviour
    {
        public List<ParticleSystem> particleSystemList; 
   
        private bool _init = false;

        public void Start()
        {
            CheckInit();
        }

        private void CheckInit()
        {
            if (_init)
            {
                return;
            }
            particleSystemList = GetComponentsInChildren<ParticleSystem>(true).ToList();
            _init = true;
        }
        
        public void Stop()
        {
            CheckInit();
            foreach (var ps in particleSystemList)
            {
                ps.Stop();
            }
        }
        public void Play()
        {
            CheckInit();
            foreach (var ps in particleSystemList)
            {
                ps.Stop();
                ps.Clear();
                ps.Play();
            }
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
            Debug.Log("OnDisable");
        }

        public float GetMaxTime()
        {
            CheckInit();
            float max = 0;
            foreach (var cell in particleSystemList)
            {
                max = Math.Max(max, cell.main.duration);
            }

            return max;
        }
    }
}