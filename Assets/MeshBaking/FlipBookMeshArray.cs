using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class FlipBookMeshArray : MonoBehaviour
{
    [SerializeField] private Mesh[] meshes;

    [SerializeField] bool loop = true;

    private MeshFilter meshFilter;

    private int index = 0;

    [HideInInspector]
    [SerializeField] private float duration;

    public float Duration { get => duration; set => duration = value; }
    public Mesh[] Meshes { get => meshes; set => meshes = value; }
    public int Index { get => index; private set => index = value; }

    int lastUpdateIndex = -1;

    private void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    private void Update()
    {
        if (meshes.Length == 0)
        {
            return;
        }

        UpdateMeshFilter(Time.time);
    }

    private void UpdateMeshFilter(float t)
    {
        int index = 0;
        if (loop)
        {
            index = Mathf.RoundToInt((t % duration) / duration * (meshes.Length - 1));
        }
        else
        {
            index = Mathf.RoundToInt(t / duration * (meshes.Length - 1));

            if (index >= meshes.Length)
            {
                index = meshes.Length - 1;
            }
        }

        if (lastUpdateIndex != index)
        {
            meshFilter.sharedMesh = meshes[index];
            lastUpdateIndex = index;
        }
    }
}
