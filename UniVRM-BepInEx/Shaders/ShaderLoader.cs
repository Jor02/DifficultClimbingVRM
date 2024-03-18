using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UniVRM10;

namespace UniVRM.Shaders
{
    internal static class ShaderLoader
    {
        private static AssetBundle assetBundle = null;

        //Temp Solution for finding shaders
        private static readonly Dictionary<string, Shader> shaderDict = new Dictionary<string, Shader>();

        private static void Initialize()
        {
            if (assetBundle == null)
            {
                assetBundle = AssetBundle.LoadFromMemory(Properties.Resources.UniVRM);

                if (assetBundle == null)
                    throw new Exception("Couldn't load UniVRM shaders");

                foreach (Shader shader in assetBundle.LoadAllAssets<Shader>())
                    shaderDict.Add(shader.name, shader);
            }
        }

        internal static Shader Find(string name)
        {
            System.Diagnostics.Debugger.Break();

            Initialize();

            Shader shader = Shader.Find(name); //assetBundle.LoadAsset<Shader>(name);

            if (shader == null)
            {
                if (shaderDict.TryGetValue(name, out Shader bundledShader))
                    shader = bundledShader;

                if (shader == null)
                {
                    Debug.LogError($"UniVRM failed to load the shader {name}");
                    throw new NullReferenceException();
                }
            }

            return shader;
        }
    }
}
