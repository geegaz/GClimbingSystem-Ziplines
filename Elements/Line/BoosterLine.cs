
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BoosterLine : UdonSharpBehaviour
{
    // Fake enum since Udon doesn't allow custom enums
    // 0 -> Simple (line from origin to point)
    // 1 -> Weighted (cubic bezier affected by gravity)
    // 2 -> Curved (quadratic bezier with 2 additional control points)
    // 3 -> Swing (arc from origin rotating around point)
    public int type = 0;

    // Common parameters (Simple type)
    public Vector3 targetPoint = Vector3.up;
    public float length = 1f;

    // Weighted type parameters
    public Vector3 weightedPoint;

    // Curved type parameters
    public Vector3 curvedControlPointA = Vector3.right;
    public Vector3 curvedControlPointB = Vector3.up + Vector3.right;

    // Swing type parameters
    public bool swingSmoothSpeed = false;

    // Baking the points
    public int bakedPointsPrecision = 20;
    public Vector3[] bakedPoints;

    private LineRenderer _lineRenderer;
    
    public void Place(Transform tf, float time) {
        time = Mathf.Clamp01(time); // Ensure time can't be invalid
        int last_point = (bakedPoints.Length - 1);

        // Always assume there's always going to be at least 2 baked points
        if (time <= 0f) {
            tf.position = bakedPoints[0];
            tf.forward = Vector3.Normalize(bakedPoints[1] - bakedPoints[0]);
        }
        else if (time >= 1f) {
            tf.position = bakedPoints[last_point];
            tf.forward = Vector3.Normalize(bakedPoints[last_point] - bakedPoints[last_point - 1]);
        }
        else {
            float local_time = time * (float)last_point;
            int point = Mathf.FloorToInt(local_time);
            local_time -= Mathf.Floor(local_time);
            // Actually place the object !
            tf.position = Vector3.Lerp(bakedPoints[point], bakedPoints[point + 1], local_time);
            tf.forward = Vector3.Normalize(bakedPoints[point + 1] - bakedPoints[point]);
        }
    }

    // EDITOR ONLY
    #if !COMPILER_UDONSHARP && UNITY_EDITOR

    #region Curve formulas
    // Shouldn't be used in Udon (and not compiled anyway) ! 
    // These can be heavy to compute, bake them to points instead

    public Vector3 QuadraticBezier(Vector3 p1, Vector3 p2, Vector3 p3, float t) {
        float t1 = (1f - t) * (1f - t);
        float t2 = 2f * (1f - t) * t;
        float t3 = t * t;
        return t1 * p1 + t2 * p2 + t3 * p3;
    }

    public Vector3 CubicBezier(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, float t) {
        float t1 = (1f - t) * (1f - t) * (1f - t);
        float t2 = 3f * (1f - t) * (1f - t) * t;
        float t3 = 3f * (1f - t) * t * t;
        float t4 = t * t * t;
        return t1 * p1 + t2 * p2 + t3 * p3 + t4 * p4;
    }

    public Vector3 SwingCurve(Vector3 p1, Vector3 p2, float t) {
        Vector3 vector = p1 - p2; // From point to origin
        Vector3 axis = Vector3.Cross(Vector3.up, vector).normalized;

        float angle = Vector3.SignedAngle(vector, Physics.gravity, axis) * 2f;
        if (swingSmoothSpeed) angle *= (Mathf.Cos((1f + t) * Mathf.PI) + 1f) * 0.5f;
        else angle *= t;
        
        Quaternion rotation = Quaternion.AngleAxis(angle, axis);

        return p2 + rotation * vector;
    }
    #endregion

    #region Points baking
    // Baking the points for the different curve types
    // should hopefully make it lighter to compute for everyone !

    public void BakePoints() {
        float base_length = Vector3.Distance(transform.position, targetPoint);
        _lineRenderer = GetComponent<LineRenderer>();

        if (type == 0) {
            bakedPoints = new Vector3[2];
            bakedPoints[0] = transform.position;
            bakedPoints[1] = targetPoint;
            length = base_length;

            if (_lineRenderer) {
                _lineRenderer.positionCount = 3;
                _lineRenderer.SetPosition(0, bakedPoints[0]);
                _lineRenderer.SetPosition(1, Vector3.Lerp(bakedPoints[0], bakedPoints[1], 0.5f));
                _lineRenderer.SetPosition(2, bakedPoints[1]);
            } 
        }
        else {
            bakedPoints = new Vector3[bakedPointsPrecision];
            // Baking (basically just sampling along the curves)
            int points = bakedPoints.Length;
            float time = 0f;
            for (int i = 0; i < points; i++)
            {
                time = (float)i / (points - 1);
                bakedPoints[i] = GetPoint(time);
            }
            // Recalculate length along curve
            length = 0f;
            for (int i = 0; i < points - 1; i++)
                length += Vector3.Distance(bakedPoints[i], bakedPoints[i + 1]);
            
            if (_lineRenderer) {
                _lineRenderer.positionCount = bakedPoints.Length;
                _lineRenderer.SetPositions(bakedPoints);
            } 
        }
    }

    public Vector3 GetPoint(float time) {
        switch (type)
        {
            case 0:
            return Vector3.Lerp(transform.position, targetPoint, time);
            
            case 1:
            return QuadraticBezier(transform.position, weightedPoint, targetPoint, time);

            case 2:
            return CubicBezier(transform.position, curvedControlPointA, curvedControlPointB, targetPoint, time);

            case 3:
            return SwingCurve(transform.position, targetPoint, time);
        }
        return Vector3.zero;
    }
    #endregion

    private void OnDrawGizmos() {
        Gizmos.color = Color.white;

        Gizmos.DrawLine(transform.position, targetPoint);
        Gizmos.DrawSphere(targetPoint, 0.5f);

        int last_point = bakedPoints.Length - 1;
        
        if (type > 0) {
            switch (type)
            {
                case 1:
                Gizmos.color = Color.yellow;
                break;

                case 2:
                Gizmos.color = new Color(1f, 0.5f, 0f);
                break;

                case 3:
                Gizmos.color = Color.red;
                break;

                default:
                break;
            }
            for (int i = 0; i < bakedPoints.Length - 1; i++)
                Gizmos.DrawLine(bakedPoints[i], bakedPoints[i + 1]);
        }
    }
    #endif
}
