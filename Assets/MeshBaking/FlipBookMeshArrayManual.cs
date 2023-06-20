using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
[DisallowMultipleComponent]
public class FlipBookMeshArrayManual : MonoBehaviour
{
    [SerializeField] private Mesh[] meshes;

    private MeshFilter meshFilter;

    private int index = 0;

    [HideInInspector]
    [SerializeField] private float duration;

    [SerializeField] float currentTime;

    public float CurrentTime
    { 
        get => currentTime;
        set
        {
            currentTime = Mathf.Clamp(value, 0.0f, duration);
            index = Mathf.RoundToInt((currentTime / duration) * (meshes.Length - 1));
        }
    }

    public float Duration { get => duration; set => duration = value; }
    public Mesh[] Meshes { get => meshes; set => meshes = value; }
    public int Index { get => index; private set => index = value; }

    float lastUpdateTime = -1f;
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

        UpdateMeshFilter(CurrentTime);
    }

    private void UpdateMeshFilter(float t)
    {
        if (lastUpdateTime == t)
        {
            return;
        }

        CurrentTime = t;

        if (lastUpdateIndex != Index)
        {
            meshFilter.sharedMesh = meshes[Index];
            lastUpdateIndex = Index;
        }

        lastUpdateTime = t;
    }
}
