using System.Collections.Generic;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// LOD管理组件
    /// 由于视角固定，只需要根据相机高度来判断当前LOD等级
    /// 同时受画质等级影响
    /// UI武器界面不挂载该组件
    /// </summary>
    [DisallowMultipleComponent]
    public class EffectRenderQueue : MonoBehaviour
    {

        public List<Renderer> allRenderers;
        public static bool openSortRenderQueue = false;
        private void Awake()
        {
            if (openSortRenderQueue)
                AdjustRenderQueue();
            if (textureArrayPlayer == null)
            {
                textureArrayPlayer = GetComponent("TextureArrayPlayer") as MonoBehaviour;
                if (textureArrayPlayer == null)
                {
                    MonoBehaviour[] all = GetComponentsInChildren<MonoBehaviour>(true);
                    for (int i = 0; i < all.Length; i++)
                    {
                        MonoBehaviour mb = all[i];
                        if (mb != null && mb.GetType().Name == "TextureArrayPlayer")
                        {
                            textureArrayPlayer = mb;
                            break;
                        }
                    }
                }
            }
        }
        
        #region Batching

        private static readonly Dictionary<int, int> MaterialIndexMap = new Dictionary<int, int>();
        private const int BaseRenderQueue = 3100;

        public MonoBehaviour textureArrayPlayer;
   
        private void UpdateMaterial(Renderer r)
        {
            if (r.sharedMaterial.renderQueue >= 3300)
            {
                return;
            }
            
            var instId = r.sharedMaterial.GetInstanceID();
            if (!MaterialIndexMap.TryGetValue(instId, out var matIndex))
            {
                matIndex = MaterialIndexMap.Count + 1;
                MaterialIndexMap[instId] = matIndex;
#if UNITY_EDITOR
                r.sharedMaterial.renderQueue = BaseRenderQueue + matIndex;
#else
                r.sharedMaterial.renderQueue = BaseRenderQueue + matIndex;
#endif
            }
            
            // if (r.renderMode == ParticleSystemRenderMode.Mesh)
            // {
            //     if (r.mesh == null) continue;
            //     var meshId = r.mesh.GetInstanceID();
            //     if (!_materialMeshMap.TryGetValue(instId, out var set))
            //     {
            //         set = new Dictionary<int, Material>();
            //         _materialMeshMap.Add(instId, set);
            //     }
            //     if (set.TryGetValue(meshId, out var material))
            //     {
            //         r.sharedMaterial = material;
            //     }
            //     else
            //     {
            //         // renderMode为Mesh时，无法与Billboard合批, 为每个mesh创建一个新的材质防止穿插打断合批
            //         material = new Material(r.sharedMaterial);
            //         matIndex = MaterialIndexMap.Count + 1;
            //         MaterialIndexMap[material.GetInstanceID()] = matIndex;
            //         material.renderQueue = BaseRenderQueue + matIndex;
            //         set.Add(meshId, material);
            //         r.sharedMaterial = material;
            //     }
            // }

        }
        /// <summary>
        /// 基于材质调整renderQueue以及基于renderer调整sortingOrder
        /// 以便于合批
        /// </summary>
        private void AdjustRenderQueue()
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r.sharedMaterial != null)
                {
                    UpdateMaterial(r);
                }
            }
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.sharedMaterial != null)
                {
                    UpdateMaterial(r);
                }
            }
            foreach (var r in GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                // 只对特效调整RenderQueue
                if ( r.sharedMaterial != null)
                {
                    UpdateMaterial(r);
                }
            }
        }
        #endregion

        
    }
}