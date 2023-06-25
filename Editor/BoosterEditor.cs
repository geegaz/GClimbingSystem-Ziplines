using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;
using UdonSharpEditor;

[CustomEditor(typeof(Booster))]
public class BoosterEditor : Editor
{
    private float trajectoryLength = 10f;
    private float trajectoryPrecision = 2f;
    
    void OnEnable()
    {
        
    }
    
    public override void OnInspectorGUI() {
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
        DrawDefaultInspector();

        // Additional trajectory stuff
        Booster booster = (Booster)target;
        if (booster.line) {
            EditorGUILayout.Space();
            trajectoryLength = Mathf.Max(0.0001f, EditorGUILayout.FloatField("Trajectory Length", trajectoryLength)); 
            trajectoryPrecision = EditorGUILayout.FloatField("Trajectory Precision", trajectoryPrecision);

            booster.PlaceOnLine();
        }
    }

    private void OnSceneGUI() {
        // Additional trajectory stuff
        Booster booster = (Booster)target;
        if (booster.line) {
            DrawTrajectory(booster, booster.line, trajectoryLength, trajectoryPrecision);
            booster.PlaceOnLine();
        }
    }

    private void DrawTrajectory(Booster booster, BoosterLine line, float length, float precision) {
        int last_point = line.bakedPoints.Length - 1;
        Vector3 origin = line.bakedPoints[last_point];
        Vector3 previous = line.bakedPoints[last_point - 1];

        float expected_delta = line.length / last_point;
        float actual_delta = Vector3.Distance(previous, origin);
        float ratio = actual_delta / expected_delta;
        
        Vector3 velocity = Vector3.Normalize(origin - previous) * ratio * booster.boostSpeed;

        int segments = Mathf.FloorToInt(length * precision);
        float delta = (1f / segments) * precision;
        Vector3[] trajectory = new Vector3[segments * 2];

        for (int t = 0; t < (segments - 1); t++)
        {
            trajectory[t * 2] = Trajectory(origin, velocity, t * delta);
            trajectory[t * 2 + 1] = Trajectory(origin, velocity, (t + 1) * delta);
        }

        Handles.DrawDottedLines(trajectory, 2);
    }

    private Vector3 Trajectory(Vector3 origin, Vector3 velocity, float time) {
        return origin + velocity * time + (Physics.gravity * time * time / 2f);
    }

    [MenuItem("GameObject/Climbing System/Booster", false, 0)]
    public static void Create() {
        Transform target = Selection.activeTransform;

        GameObject new_object = new GameObject("Booster", typeof(Booster));
        new_object.transform.parent = target;

        Selection.SetActiveObjectWithContext(new_object, null);
    }
}
