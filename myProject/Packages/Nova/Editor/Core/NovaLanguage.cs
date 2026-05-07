// --------------------------------------------------------------
// Copyright 2024 CyberAgent, Inc.
// --------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Nova.Editor.Core.Scripts
{
    public static class NovaLanguage
    {
        public static bool showChinese = true;

        private static Dictionary<string, string> _languageMap = new()
        {
            { "Render Settings","渲染设置"},
            { "Render Type","渲染类型"},
            { "Blend Mode","混合模式"},
            { "Cutoff","裁剪"},
            { "Render Face","渲染面"},
            { "Render Priority","渲染优先级"},
            { "Vertex Alpha Mode","渲染优先级"},
            { "ZWrite","深度写入"},
            { "Work Flow Mode","工作流模式"},
            { "Receive Shadows","接受阴影"},
            { "Specular Highlights","高光"},
            { "Environment Reflections","环境反射"},
            { "ZTest","深度测试"},
            
            { "Vertex Deformation","顶点形变"},
            { "Intensity","强度"},
            { "Rotation","旋转"},
            { "Offset","偏移"},
            { "Mirror Sampling","镜像采样"},
            { "Flip-Book Progress","翻书Progress"},
            { "Flow Map","流动贴图"},
            { "Transparency","透明度"},
            { "Emission","发射"},
            { "Alpha Transition","Alpha过渡"},
            { "Color Correction","颜色校正"},
            { "Parallax Map","视差贴图"},
            { "Strength","强度"},
            { "TextureMode","纹理模式"},
            { "Soft Particles","软粒子"},
            { "Luminance","亮度"},
            { "Rim","边缘"},
            { "Shadow Caster","阴影来源"},
            { "Depth Fade","深度渐变"},
            { "Base Map","基本贴图"},
            { "Mode","模式"},
            { "Tint Color","色调"},
            { "Map Mode","地图模式"},
            { "Surface Maps","表面贴图"},
            { "Offset Coords","偏移坐标"},
            { "Tiling","瓦片"},
            { "Channels","通道"},
            { "Smoothness","光滑度"},
            { "Specular","镜面"},
            { "Normal Map","法线贴图"},
            // { "Offset Coords",""},
            // { "Offset Coords",""},

            // { "Render Priority","渲染优先级"},
        };

    

        public static string Get(string key)
        {

            if (showChinese && _languageMap.TryGetValue(key, out var value))
            {
                return $"{key} [{value}]";
            }
            else
            {
                // Debug.Log("Key = " + key);
                return key;
            }
        }
    }
}