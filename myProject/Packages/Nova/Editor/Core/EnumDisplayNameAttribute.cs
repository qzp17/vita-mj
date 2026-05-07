// --------------------------------------------------------------
// Copyright 2024 CyberAgent, Inc.
// --------------------------------------------------------------
using UnityEngine;

namespace Nova.Editor.Core.Scripts
{

    public class EnumDisplayNameAttribute : PropertyAttribute
    {
        public string[] DisplayNames;

        public EnumDisplayNameAttribute(params string[] displayNames)
        {
            DisplayNames = displayNames;
        }
    }

}