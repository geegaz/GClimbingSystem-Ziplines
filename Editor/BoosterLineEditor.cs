using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;
using UdonSharpEditor;

[CustomEditor(typeof(BoosterLine))]
public class BoosterLineEditor : Editor
{
    enum LineType {
        Simple,
        Weighted,
        Curved,
        Swing
    }

    bool showBakingOptions = true;
    Vector3 lastTransformPosition;
    
    // Common properties
    // Additional properties are found directly in specialized inspectors
    private SerializedProperty _typeProp;
    private SerializedProperty _targetPointProp;
    private SerializedProperty _lengthProp;
    private SerializedProperty _bakedPointsPrecisionProp;
    private SerializedProperty _bakedPointsProp;

    private SerializedProperty[] _specializedProps;

    void OnEnable()
    {
        _typeProp = serializedObject.FindProperty("type");
        _targetPointProp = serializedObject.FindProperty("targetPoint");
        _lengthProp = serializedObject.FindProperty("length");
        _bakedPointsPrecisionProp = serializedObject.FindProperty("bakedPointsPrecision");
        _bakedPointsProp = serializedObject.FindProperty("bakedPoints");

        _specializedProps = new SerializedProperty[] {
            serializedObject.FindProperty("weightedPoint"),
            serializedObject.FindProperty("curvedControlPointA"),
            serializedObject.FindProperty("curvedControlPointB"),
            serializedObject.FindProperty("swingSmoothSpeed")
        };

        BoosterLine line = target as BoosterLine;
        lastTransformPosition = line.transform.position;
    }

    public override void OnInspectorGUI() {
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
        BoosterLine line = target as BoosterLine;
        
        EditorGUI.BeginChangeCheck();
        serializedObject.Update();

        // Draw the line type with a nice enum
        LineType type = (LineType)_typeProp.intValue;
        type = (LineType)EditorGUILayout.EnumPopup("Line Type", type);
        _typeProp.intValue = (int)type;

        // Draw specific inspectors
        EditorGUILayout.BeginVertical("box");
        switch (type)
        {
            case LineType.Simple: // Inspector for the Simple line type
            // No additional editor properties needed
            break;

            case LineType.Weighted: // Inspector for the Weighted line type
            EditorGUILayout.PropertyField(_specializedProps[0]);
            break;

            case LineType.Curved: // Inspector for the Curved line type
            EditorGUILayout.PropertyField(_specializedProps[1]);
            EditorGUILayout.PropertyField(_specializedProps[2]);
            break;

            case LineType.Swing: // Inspector for the Swing line type
            EditorGUILayout.PropertyField(_specializedProps[3]);
            break;
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

        // Draw common properties
        // - Target Point
        EditorGUILayout.PropertyField(_targetPointProp);
        // - Length (Read Only)
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.PropertyField(_lengthProp);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();
        // - Baking parameters
        showBakingOptions = EditorGUILayout.BeginFoldoutHeaderGroup(showBakingOptions, "Line Baking");
        if (showBakingOptions) {
            EditorGUILayout.PropertyField(_bakedPointsPrecisionProp);
            // - Baked Points
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(_bakedPointsProp);
            EditorGUI.EndDisabledGroup();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
        if (EditorGUI.EndChangeCheck()) {
            // Did the properties change ?
            // Update the baked points
            line.BakePoints();
        }
    }
    
    private void OnSceneGUI() {
        BoosterLine line = target as BoosterLine;
        
        Vector3 origin = line.transform.position;
        Vector3 point = line.targetPoint;
        Vector3 controlA = line.curvedControlPointA;
        Vector3 controlB = line.curvedControlPointB;
        Vector3 weighted = line.weightedPoint;

        // Move all points with the object's transform
        if (line.transform.hasChanged) {
            Vector3 diff = line.transform.position - lastTransformPosition;

            point += diff;
            controlA += diff;
            controlB += diff;
            weighted += diff;

            lastTransformPosition = line.transform.position;
        }
        
        EditorGUI.BeginChangeCheck();

        point = Handles.DoPositionHandle(point, Quaternion.identity);

        switch (line.type)
        {
            case 0: // Handles for the Simple line type
            // No additional handles needed
            break;
            
            case 1: // Handles for the Weighted line type
            Handles.color = Color.yellow;
            
            Vector3 center = Vector3.Lerp(origin, point, 0.5f);
            weighted.x = center.x;
            weighted.z = center.z;
            Vector3 vector = (weighted - center) * 0.5f;
            weighted = Handles.Slider(center + vector, Vector3.down, 0.5f, Handles.SphereHandleCap, 0f) + vector;
            
            Handles.DrawDottedLine(center, weighted - vector, 5f);
            break;

            case 2: // Handles for the Curved line type
            Handles.color = new Color(1f, 0.5f, 0f);

            controlA = Handles.FreeMoveHandle(controlA, Quaternion.identity, 0.5f, Vector3.zero, Handles.SphereHandleCap);
            //controlA = Handles.DoPositionHandle(controlA, Quaternion.identity);
            Handles.DrawDottedLine(origin, controlA, 5f);
            controlB = Handles.FreeMoveHandle(controlB, Quaternion.identity, 0.5f, Vector3.zero, Handles.SphereHandleCap);
            //controlB = Handles.DoPositionHandle(controlB, Quaternion.identity);
            Handles.DrawDottedLine(point, controlB, 5f);
            break;

            case 3: // Handles for the Swing line type
            Handles.color = Color.red;

            Vector3 target = line.GetPoint(1f);
            Handles.DrawDottedLine(point, target, 5f);
            break;

            default:
            break;
        }
        
        if (EditorGUI.EndChangeCheck() || line.transform.hasChanged) {
            Undo.RecordObject(line, "Change target point position");
            line.targetPoint = point;
            line.curvedControlPointA = controlA;
            line.curvedControlPointB = controlB;
            line.weightedPoint = weighted;
            line.BakePoints();
        }
    }

    [MenuItem("GameObject/Climbing System/Booster Line", false, 0)]
    public static void Create() {
        Transform target = Selection.activeTransform;

        GameObject new_object = new GameObject("Booster Line", typeof(BoosterLine));
        new_object.transform.parent = target;
        BoosterLine new_line = new_object.GetComponent<BoosterLine>();
        new_line.bakedPoints = new Vector3[2];
        new_line.bakedPoints[0] = Vector3.zero;
        new_line.bakedPoints[1] = Vector3.up;

        Selection.SetActiveObjectWithContext(new_object, null);
    }
}
