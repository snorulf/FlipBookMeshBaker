using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;

public class BakeFlipBookMeshEditor : EditorWindow
{
    string filepath = "Assets\\BakeFolder\\FlipBookMesh";
    float fps = 30f;
    float alembicStepSize = 0.1f;

    [MenuItem("Mesh Baking/Bake Flip Book Mesh")]
    static void Init()
    {
        BakeFlipBookMeshEditor window = (BakeFlipBookMeshEditor)EditorWindow.GetWindow(typeof(BakeFlipBookMeshEditor));
        window.Show();
    }

    void OnGUI()
    {
        string path = Path.GetDirectoryName(filepath);
        string meshName = Path.GetFileName(filepath);

        GUILayout.Label("Select a gameobject to bake a Flip Book Mesh from", EditorStyles.boldLabel);
        filepath = EditorGUILayout.TextField("Output", filepath);

#if false
        if (GUILayout.Button("Clear"))
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
                Debug.Log("<color=cyan>Deleted bake folder at " + path + "</color>");
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.Log("<color=cyan>Bake folder does not exist at " + path + "</color>");
            }
        }
#endif

        fps = EditorGUILayout.Slider("FPS", fps, 1f, 120f);
        GUILayout.Label("FPS should be what the source animation was made in", EditorStyles.miniBoldLabel);
        alembicStepSize = EditorGUILayout.Slider("Alembic Step Size", alembicStepSize, 0.01f, 10f);
        GUILayout.Label("The step size in seconds of each mesh frame generated", EditorStyles.miniBoldLabel);

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Bake Skinned Mesh"))
        {
            BakeSkinnedMesh(meshName, path, fps);
        }

        if (GUILayout.Button("Bake Alembic Stream"))
        {
            BakeAlembic(meshName, path, alembicStepSize);
        }
    }

    void BakeAlembic(string meshName, string filepath, float alembicStepSize)
    {
        if (!TryValidateFilePath(out string path, filepath, meshName))
        {
            return;
        }

        GameObject gameObject = Selection.activeGameObject;
        if (gameObject == null)
        {
            Debug.LogError("No gameobject selected");
            EditorUtility.DisplayDialog("Error", "No gameobject selected", "Ok");
            return;
        }

        AlembicStreamPlayer player = gameObject.GetComponent<AlembicStreamPlayer>();
        if (player == null)
        {
            Debug.LogError("No AlembicStreamPlayer found on gameobject", gameObject);
            EditorUtility.DisplayDialog("Error", "No AlembicStreamPlayer found on " + gameObject.name, "Ok");
            return;
        }

        MeshFilter[] meshFilters = gameObject.GetComponentsInChildren<MeshFilter>();
        if (meshFilters == null)
        {
            Debug.LogError("No MeshFilter found on gameobject", gameObject);
            EditorUtility.DisplayDialog("Error", "No MeshFilter found on " + gameObject.name, "Ok");
            return;
        }

        // estimate the number of baked meshes that will be created
        int numMeshes = Mathf.RoundToInt((player.Duration / alembicStepSize) + 1);
        if (!EditorUtility.DisplayDialog("Bake Skinned Mesh", "Baking " + gameObject.name + " will generate " + numMeshes + " meshes", "Continue", "Cancel"))
        {
            return;
        }

        List<Mesh> bakedMeshes = new List<Mesh>();
        float playerTime = player.CurrentTime;
        {
            float time = 0;
            int index = 0;
            while (time <= player.Duration)
            {
                player.UpdateImmediately(time);

                List<Mesh> meshes = new List<Mesh>();

                foreach (var meshFilter in meshFilters)
                {
                    Mesh mesh = new Mesh();
                    meshes.Add(meshFilter.sharedMesh);
                }

                // Combine meshes into one mesh
                CombineInstance[] combine = new CombineInstance[meshes.Count];
                for (int i = 0; i < meshes.Count; i++)
                {
                    combine[i].mesh = meshes[i];
                    combine[i].transform = gameObject.transform.localToWorldMatrix.inverse * meshFilters[i].transform.localToWorldMatrix;
                }

                Mesh combinedMesh = new Mesh();
                combinedMesh.CombineMeshes(combine);

                // Save mesh to asset
                Mesh meshFrame = (Mesh)Instantiate(combinedMesh);
                AssetDatabase.CreateAsset(meshFrame, path + meshName + "_" + index + ".asset");
                bakedMeshes.Add(meshFrame);
                time += alembicStepSize;
                index++;
            }
        }

        // reset
        player.UpdateImmediately(playerTime);

        if (bakedMeshes.Count == 0)
        {
            Debug.LogError("No meshes baked");
            return;
        }
        
        {
            var objToSpawn = new GameObject(gameObject.name + "_BakedMeshArray");
            objToSpawn.transform.SetPositionAndRotation(gameObject.transform.position, gameObject.transform.rotation);

            objToSpawn.tag = gameObject.tag;
            objToSpawn.layer = gameObject.layer;

            objToSpawn.AddComponent<MeshFilter>();

            objToSpawn.AddComponent<MeshRenderer>();
            objToSpawn.GetComponent<MeshRenderer>().material = gameObject.GetComponentInChildren<Renderer>().sharedMaterial;

            objToSpawn.AddComponent<FlipBookMeshArrayManual>();
            objToSpawn.GetComponent<FlipBookMeshArrayManual>().Meshes = bakedMeshes.ToArray();
            objToSpawn.GetComponent<FlipBookMeshArrayManual>().Duration = player.Duration;
            objToSpawn.GetComponent<FlipBookMeshArrayManual>().CurrentTime = player.CurrentTime;

            int index = objToSpawn.GetComponent<FlipBookMeshArrayManual>().Index;
            objToSpawn.GetComponent<MeshFilter>().sharedMesh = bakedMeshes[index];

            AssetDatabase.SaveAssets();

            Debug.Log("<color=cyan>Bake Flip Book Mesh Array complete: Baked " + bakedMeshes.Count + " meshes to " + path + meshName + "_*.asset" + " and spawned " + objToSpawn.name + "</color>", objToSpawn);
        }

    }

    void BakeSkinnedMesh(string meshName, string filepath, float fps)
    {
        if (!TryValidateFilePath(out string path, filepath, meshName))
        {
            return;
        }

        GameObject gameObject = Selection.activeGameObject;
        if (gameObject == null)
        {
            Debug.LogError("No gameobject selected");
            EditorUtility.DisplayDialog("Error", "No gameobject selected", "Ok");
            return;
        }

        Animator animator = gameObject.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("No animator found on gameobject", gameObject);
            EditorUtility.DisplayDialog("Error", "No Animator found on " + gameObject.name, "Ok");
            return;
        }

        SkinnedMeshRenderer[] skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
        if (skinnedMeshRenderers == null || skinnedMeshRenderers.Length == 0)
        {
            Debug.LogError("No skinned mesh renderer found on gameobject", gameObject);
            EditorUtility.DisplayDialog("Error", "No SkinnedMeshRenderer found on " + gameObject.name, "Ok");
            return;
        }

        var clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0)
        {
            Debug.LogError("No animation clips found on gameobject", animator.transform.gameObject);
            EditorUtility.DisplayDialog("Error", "No animation clips found on " + animator.transform.gameObject.name, "Ok");
            return;
        }

        // estimate the number of baked meshes that will be created
        int bakedMeshCount = 0;
        foreach (var clip in clips)
        {
            bakedMeshCount += Mathf.CeilToInt(clip.length * fps);
        }
        if (!EditorUtility.DisplayDialog("Bake Skinned Mesh", "Baking " + gameObject.name + " will generate " + bakedMeshCount + " meshes", "Continue", "Cancel"))
        {
            return;
        }

        List<Mesh> bakedMeshes = new List<Mesh>();
        foreach (var clip in clips)
        {
            float time = 0.0f;
            int frame = 0;

            while (time <= clip.length)
            {
                Debug.Log("Sample animation clip " + clip.name + " at " + time);

                clip.SampleAnimation(gameObject, time);

                CombineInstance[] combine = new CombineInstance[skinnedMeshRenderers.Length];
                Mesh combinedMesh = new Mesh();

                for (int i = 0; i < skinnedMeshRenderers.Length; i++)
                {
                    var mesh = new Mesh();
                    skinnedMeshRenderers[i].BakeMesh(mesh);
                    combine[i].mesh = mesh;
                    combine[i].transform = skinnedMeshRenderers[i].transform.localToWorldMatrix;
                }

                combinedMesh.CombineMeshes(combine);
                AssetDatabase.CreateAsset(combinedMesh, path + meshName + "_" + frame + ".asset");

                bakedMeshes.Add(combinedMesh);

                // move to the next frame
                time = (float)(frame) / fps;
                frame++;
            }
        }

        if (bakedMeshes.Count == 0)
        {
            Debug.LogError("No meshes baked");
            return;
        }

        float totalDuration = 0;
        foreach (var clip in clips)
        {
            totalDuration += clip.length;
        }

        {
            var objToSpawn = new GameObject(gameObject.name + "_BakedMeshArray");

            objToSpawn.tag = gameObject.tag;
            objToSpawn.layer = gameObject.layer;

            objToSpawn.transform.SetPositionAndRotation(gameObject.transform.position, gameObject.transform.rotation);

            objToSpawn.AddComponent<MeshFilter>();
            objToSpawn.GetComponent<MeshFilter>().sharedMesh = bakedMeshes[0];

            objToSpawn.AddComponent<MeshRenderer>();
            objToSpawn.GetComponent<MeshRenderer>().material = gameObject.GetComponentInChildren<Renderer>().sharedMaterial;

            objToSpawn.AddComponent<FlipBookMeshArray>();
            objToSpawn.GetComponent<FlipBookMeshArray>().Meshes = bakedMeshes.ToArray();
            objToSpawn.GetComponent<FlipBookMeshArray>().Duration = totalDuration;

            AssetDatabase.SaveAssets();

            Debug.Log("<color=cyan>Bake Flip Book Mesh Array complete: Baked " + bakedMeshes.Count + " meshes to " + path + meshName + "_*.asset" + " and spawned " + objToSpawn.name + "</color>", objToSpawn);
        }
    }

    bool TryValidateFilePath(out string path, string filepath, string meshName)
    {
        path = string.Empty;

        if (!filepath.EndsWith("\\"))
        {
            filepath += "\\";
        }

        {
            string assetPath = Path.Combine(filepath, meshName + "_0.asset");
            if (File.Exists(assetPath))
            {
                Debug.LogError("Asset already exists at " + assetPath);
                EditorUtility.DisplayDialog("Error", "Asset already exists at " + assetPath, "Ok");
                return false;
            }
        }

        Directory.CreateDirectory(filepath);

        path = filepath;

        return true;
    }
}
