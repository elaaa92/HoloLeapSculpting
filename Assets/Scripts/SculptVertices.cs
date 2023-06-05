using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SculptVertices : MonoBehaviour {
    public float radius = 0.01f;
    public float pull = 0.001f;
    private MeshFilter unappliedMesh;

    public enum FallOff { Gauss, Linear, Needle }
    public FallOff fallOff = FallOff.Gauss;
    private enum PinchAction {Pinch, Leave, None};
    private PinchAction pinchAction = PinchAction.None;
    private Vector3 pinchPoint;

    static float LinearFalloff(float distance, float inRadius)
    {
        return Mathf.Clamp01(1.0f - distance / inRadius);
    }

    static float GaussFalloff(float distance, float inRadius)
    {
        return Mathf.Clamp01(Mathf.Pow(360.0f, -Mathf.Pow(distance / inRadius, 2.5f) - 0.01f));
    }

    float NeedleFalloff(float dist, float inRadius)
    {
        return -(dist * dist) / (inRadius * inRadius) + 1.0f;
    }

    public void Pinch(Vector3 pinchPoint)
    {
        this.pinchAction = SculptVertices.PinchAction.Pinch;
        this.pinchPoint = pinchPoint;
    }

    public void Release()
    {
        this.pinchAction = SculptVertices.PinchAction.Leave;
    }

    void DeformMesh(Mesh mesh, Vector3 position, float power, float inRadius)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        float sqrRadius = inRadius * inRadius;

        // Calculate averaged normal of all surrounding vertices	
        var averageNormal = Vector3.zero;
        float sqrMagnitude;
        float distance;
        float falloff;

        for (int i = 0; i < vertices.Length; i++)
        {
            sqrMagnitude = (vertices[i] - position).sqrMagnitude;
            // Early out if too far away
            if (sqrMagnitude > sqrRadius)
                continue;

            distance = Mathf.Sqrt(sqrMagnitude);
            falloff = LinearFalloff(distance, inRadius);
            averageNormal += falloff * normals[i];
        }
        averageNormal = averageNormal.normalized;

        // Deform vertices along averaged normal
        for (int i = 0; i < vertices.Length; i++)
        {
            sqrMagnitude = (vertices[i] - position).sqrMagnitude;
            // Early out if too far away
            if (sqrMagnitude > sqrRadius)
                continue;

            distance = Mathf.Sqrt(sqrMagnitude);
            switch (fallOff)
            {
                case FallOff.Gauss:
                    falloff = GaussFalloff(distance, inRadius);
                    break;
                case FallOff.Needle:
                    falloff = NeedleFalloff(distance, inRadius);
                    break;
                default:
                    falloff = LinearFalloff(distance, inRadius);
                    break;
            }

            vertices[i] += averageNormal * falloff * power;
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void Update()
    {
        // When user stop to pinch we update the mesh collider
        if (pinchAction == PinchAction.Leave)
        {
            // Apply collision mesh when we let go of button
            ApplyMeshCollider();
            pinchAction = PinchAction.None;
        }
        else if (pinchAction == PinchAction.Pinch)
        {
            MeshFilter filter = gameObject.GetComponent<MeshFilter>();
            if (filter)
            {
                if (filter != unappliedMesh)
                {
                    ApplyMeshCollider();
                    unappliedMesh = filter;
                }
                
                // Deform mesh
                var relativePoint = filter.transform.InverseTransformPoint(pinchPoint);
                DeformMesh(filter.mesh, relativePoint, pull * Time.deltaTime, radius);
            }
        }
    }

    void ApplyMeshCollider()
    {
        if (unappliedMesh && unappliedMesh.GetComponent<MeshCollider>())
        {
            unappliedMesh.GetComponent<MeshCollider>().sharedMesh = unappliedMesh.mesh;
        }
        unappliedMesh = null;
    }
}
