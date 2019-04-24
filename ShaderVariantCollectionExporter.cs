using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public static class ShaderVariantCollectionExporter
{
    [MenuItem("Tools/Shader/Export ShaderVariantCollection")]
    private static void Export()
    {
        var dict = new Dictionary<string, bool>();

        foreach (var assetBundleName in AssetDatabase.GetAllAssetBundleNames())
        {
            string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName);
            foreach (var dependency in AssetDatabase.GetDependencies(assetPaths, true))
            {
                if (!dict.ContainsKey(dependency))
                {
                    dict.Add(dependency, true);
                }
            }
        }

        var di = new DirectoryInfo("Assets/Resources");
        foreach (var fi in di.GetFiles("*", SearchOption.AllDirectories))
        {
            if (fi.Extension == ".meta")
            {
                continue;
            }

            string assetPath = fi.FullName.Replace(Application.dataPath, "Assets");
            foreach (var dependency in AssetDatabase.GetDependencies(assetPath, true))
            {
                if (!dict.ContainsKey(dependency))
                {
                    dict.Add(dependency, true);
                }
            }
        }

        string[] scenes = (from scene in EditorBuildSettings.scenes
            where scene.enabled
            select scene.path).ToArray();
        foreach (var dependency in AssetDatabase.GetDependencies(scenes, true))
        {
            if (!dict.ContainsKey(dependency))
            {
                dict.Add(dependency, true);
            }
        }

        var materials = new List<Material>();
        var shaderDict = new Dictionary<Shader, List<Material>>();
        foreach (var assetPath in dict.Keys)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material != null)
            {
                if (material.shader != null)
                {
                    if (!shaderDict.ContainsKey(material.shader))
                    {
                        shaderDict.Add(material.shader, new List<Material>());
                    }

                    if (!shaderDict[material.shader].Contains(material))
                    {
                        shaderDict[material.shader].Add(material);
                    }
                }

                if (!materials.Contains(material))
                {
                    materials.Add(material);
                }
            }
        }

        ProcessMaterials(materials);

        var sb = new System.Text.StringBuilder();
        foreach (var kvp in shaderDict)
        {
            sb.AppendLine(kvp.Key + " " + kvp.Value.Count + " times");

            if (kvp.Value.Count <= 5)
            {
                Debug.LogWarning("Shader: " + kvp.Key.name, kvp.Key);

                foreach (var m in kvp.Value)
                {
                    Debug.Log(AssetDatabase.GetAssetPath(m), m);
                }
            }
        }

        Debug.Log(sb.ToString());
    }

    static ShaderVariantCollectionExporter()
    {
        EditorApplication.update += EditorUpdate;
    }

    private static void EditorUpdate()
    {
        if (_isStarted && _elapsedTime.ElapsedMilliseconds >= WaitTimeBeforeSave)
        {
            Debug.Log(InvokeInternalStaticMethod(typeof(ShaderUtil),
                "GetCurrentShaderVariantCollectionVariantCount"));
            _elapsedTime.Stop();
            _elapsedTime.Reset();
            _isStarted = false;
            EditorApplication.isPlaying = false;
            InvokeInternalStaticMethod(typeof(ShaderUtil), "SaveCurrentShaderVariantCollection",
                ShaderVariantCollectionPath);
            Debug.Log(
                InvokeInternalStaticMethod(typeof(ShaderUtil), "GetCurrentShaderVariantCollectionShaderCount"));
        }
    }

    private static void ProcessMaterials(List<Material> materials)
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
        InvokeInternalStaticMethod(typeof(ShaderUtil), "ClearCurrentShaderVariantCollection");
        Debug.Log(InvokeInternalStaticMethod(typeof(ShaderUtil), "GetCurrentShaderVariantCollectionShaderCount"));

        int totalMaterials = materials.Count;

        var camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("Main Camera didn't exist");
            return;
        }

        float aspect = camera.aspect;

        float height = Mathf.Sqrt(totalMaterials / aspect) + 1;
        float width = Mathf.Sqrt(totalMaterials / aspect) * aspect + 1;

        float halfHeight = Mathf.CeilToInt(height / 2f);
        float halfWidth = Mathf.CeilToInt(width / 2f);

        camera.orthographic = true;
        camera.orthographicSize = halfHeight;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        Selection.activeGameObject = camera.gameObject;
        EditorApplication.ExecuteMenuItem("GameObject/Align View to Selected");

        int xMax = (int) (width - 1);

        int x = 0;
        int y = 0;

        for (int i = 0; i < materials.Count; i++)
        {
            var material = materials[i];

            var position = new Vector3(x - halfWidth + 1f, y - halfHeight + 1f, 0f);
            CreateSphere(material, position, x, y, i);

            if (x == xMax)
            {
                x = 0;
                y++;
            }
            else
            {
                x++;
            }
        }

        _elapsedTime.Stop();
        _elapsedTime.Reset();
        _elapsedTime.Start();
        _isStarted = true;
    }

    private static void CreateSphere(Material material, Vector3 position, int x, int y, int index)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.GetComponent<Renderer>().material = material;
        go.transform.position = position;
        go.name = string.Format("Sphere_{0}|{1}_{2}|{3}", index, x, y, material.name);
    }

    private static object InvokeInternalStaticMethod(System.Type type, string method, params object[] parameters)
    {
        var methodInfo = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
        if (methodInfo == null)
        {
            Debug.LogError(string.Format("{0} method didn't exist", method));
            return null;
        }

        return methodInfo.Invoke(null, parameters);
    }

    private static bool _isStarted;
    private static readonly Stopwatch _elapsedTime = new Stopwatch();

    private const string ShaderVariantCollectionPath = "Assets/ShaderVariantCollection.shadervariants";
    private const int WaitTimeBeforeSave = 1000;
}
