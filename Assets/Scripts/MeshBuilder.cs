using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshBuilder : MonoBehaviour {
    public static Mesh BuildMesh(Vector3[] quads)
    {
        Mesh mesh = new Mesh();
        int nvertices = quads.Length, ntriangles = nvertices * 3 / 2;
        int[] triangles = new int[ntriangles];
        Vector3[] vertices = new Vector3[nvertices];
        Vector2[] uvs = new Vector2[nvertices];
        Vector2[] quaduv = { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };

        for (int i = 0; i < nvertices; i += 4)
        {
            Vector3[] verti = { quads[i], quads[i + 1], quads[i + 2], quads[i + 3] };
            int[] lvinds = { 2, 0, 1, 0, 2, 3 };

            for (int j = 0; j < 4; j++)
            {
                vertices[i + j] = verti[j];
                uvs[i + j] = quaduv[j];
            }

            for (int j = 0; j < 6; j++)
            {
                triangles[i * 3 / 2 + j] = i + lvinds[j];
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        
        return mesh;
    }
}
