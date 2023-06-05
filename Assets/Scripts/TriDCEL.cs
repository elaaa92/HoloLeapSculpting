using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TriDCEL : DCEL {
    private Vector3[] BackupVertices;
    private int[] BackupTriangles;

    public TriDCEL(int angleThresh) : base()
    {
        this.AngleThresh = angleThresh;
    }

    public TriDCEL(Mesh mesh, int angleThresh) : base()
    {
        Vector3[] vs = mesh.vertices;
        Vector3[] triangles = mesh.triangles.Select(v => vs[v]).ToArray();
        Vector2[] uvs = mesh.uv;

        this.AngleThresh = angleThresh;

        Normalize(ref vs, 0.5f, 0.5f, 0.5f);

        int nadded;
        if (!AddNewFaces(triangles, 3, out nadded))
            throw new Exception("This mesh is not fabbricable");
    }

    public TriDCEL(Vector3[] triangles, int angleThresh) : base()
    {
        this.AngleThresh = angleThresh;

        Normalize(ref triangles, 0.5f, 0.5f, 0.5f);

        int nadded;
        if (!AddNewFaces(triangles, 3, out nadded))
            throw new Exception("This mesh is not fabbricable");
    }

    public void CheckConsistency()
    {
        bool ok = Vertices.Length == VertexTable.Count;

        if (!ok)
            throw new Exception("Vertices count fail" + Vertices.Length + " " + VertexTable.Count);

        ok = true;
        for (int i = 0; i < Faces.Length; i++)
        {
            int e = Faces[i].GetOuter();
            int next = HEdges[e].GetNextEdge(), prev = HEdges[e].GetPrevEdge();
            if (prev >= HEdges.Length)
                Debug.Log(prev);
            ok &= (HEdges[e].GetStartPoint() == HEdges[prev].GetEndPoint()
                && HEdges[next].GetStartPoint() == HEdges[e].GetEndPoint());
        }

        if (!ok)
            throw new Exception("Prev and next check fail");

        ok = true;
        for (int i = 0; i < Faces.Length; i++)
        {
            int next = HEdges[i].GetNextEdge();
            int prev = HEdges[i].GetPrevEdge();
            ok &= (HEdges[i].GetStartPoint() != HEdges[i].GetEndPoint()
                && HEdges[prev].GetStartPoint() != HEdges[prev].GetEndPoint()
                && HEdges[next].GetStartPoint() != HEdges[next].GetEndPoint());
        }

        if (!ok)
            throw new Exception("Distinctivity check fail");

        ok = true;
        for (int i = 0; i < HEdges.Length; i++)
        {
            int twin;
            if (HEdges[i].TryGetTwinEdge(out twin))
            {
                int v1 = HEdges[i].GetStartPoint(), v2 = HEdges[i].GetEndPoint();
                int v1t = HEdges[twin].GetStartPoint(), v2t = HEdges[twin].GetEndPoint();
                ok &= (v1 == v2t && v2 == v1t);
            }
        }

        if (!ok)
            throw new Exception("Twins check fail");
    }

    public Mesh GetMesh()
    {
        Vector3[] dcelvertices = Vertices.Select(v => v.GetPosition()).ToArray();
        int[] firstEdgesInd = (Faces.Select(f => f.GetOuter()).ToArray());
        HalfEdge[] firstEdges = firstEdgesInd.Select(e => HEdges[e]).ToArray();
        int[] triangles = new int[Faces.Length * 3];
        Vector3[] vertices = dcelvertices;
        Vector2[] uvs = new Vector2[Faces.Length * 3];
        int nvertices = dcelvertices.Length;
        Mesh mesh = new Mesh();
        
        for (int i = 0; i < firstEdges.Length; i++)
        {
            int[] vind = { HEdges[firstEdges[i].GetPrevEdge()].GetStartPoint(),
                            firstEdges[i].GetStartPoint(),
                            HEdges[firstEdges[i].GetNextEdge()].GetStartPoint() };

            for (int j = 0; j < 3; j++)
            {
                triangles[3 * i + j] = vind[j];
            }
        }
        
        uvs = vertices.Select(v => new Vector2(v.x / (float)nvertices, 
            v.z / (float)nvertices)).ToArray();
        System.Array.Resize(ref uvs, nvertices);

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}
