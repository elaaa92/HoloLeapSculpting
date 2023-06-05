using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MeshManager {
    QuadDCEL dcel;

    public Mesh BuildGeometryBlock(float sizex, float sizey, float sizez)
    {
        Mesh mesh;
        Dictionary<Vector3, int> table = new Dictionary<Vector3, int>(new ComparerVector3());
        Vector3[] quads = new Vector3[6*4], normals = { Vector3.down, Vector3.back, Vector3.up, Vector3.forward, Vector3.right, Vector3.left };

        for (int i = 0; i < 6; i++)
        {
            Quaternion rotation = Quaternion.AngleAxis(90, normals[i]);
            int sign = (int)(normals[i].x + normals[i].y + normals[i].z);
            Vector3 startvert = 0.5f * (Vector3.one - sign * normals[i]) + 0.5f * normals[i];

            for (int j = 0; j < 4; j++)
            {
                Vector3 scaledvert = startvert;

                scaledvert.x *= sizex;
                scaledvert.y *= sizey;
                scaledvert.z *= sizez;
                quads[4 * i + j] = scaledvert;
                if(!table.ContainsKey(scaledvert))
                    table.Add(scaledvert, (4*i+j));
                startvert = rotation * startvert;
            }
        }

        dcel = new QuadDCEL(quads, 60, true);
        mesh = dcel.GetMesh();
        return mesh;
    }

    public Mesh BuildGeometry()
    {
        dcel = (QuadDCEL)ObjManager.FileToDcel("Assets/Resources/Prefab/cube.obj", 60); //hololens
        //dcel = (QuadDCEL)ObjManager.FileToDcel("C:/Users/elado/AppData/LocalLow/DefaultCompany/Sculpting/data/cube.obj", 60); portatile

        Mesh mesh = dcel.GetMesh();
        return mesh;
    }
    
    public int FindFace(int triangleIndex, MeshFilter filter)
    {
        Mesh mesh = filter.sharedMesh;
        Vector3[] vertices = { mesh.vertices[mesh.triangles[3 * triangleIndex]], mesh.vertices[mesh.triangles[3 * triangleIndex + 1]], mesh.vertices[mesh.triangles[3 * triangleIndex + 2]] };
        Vector3 A = mesh.bounds.min, B = mesh.bounds.max, C = dcel.GetMinVertex(), D = dcel.GetMaxVertex();

        for (int i = 0; i < 3; i++)
        {
            vertices[i].x = C.x + (D.x - C.x) * (vertices[i].x - A.x) / (B.x - A.x);
            vertices[i].y = C.y + (D.y - C.y) * (vertices[i].y - A.y) / (B.y - A.y);
            vertices[i].z = C.z + (D.z - C.z) * (vertices[i].z - A.z) / (B.z - A.z);
        }

        int result = dcel.FindFaceByVertices(vertices);
        
        if (result == -1)
            result = dcel.FindFaceByVertices(vertices.Reverse().ToArray());

        if (result == -1)
            Debug.Log(triangleIndex + " " + vertices[0] + " " + vertices[1] + " " + vertices[2]);
        return result;
    }

    public bool GetConnectedSectionAdd(HashSet<int> faces, int newface, out Vector3[] contour, out Vector3[] contournorm, out Vector3[] contourmiddlepoints, 
        out Vector3[] contourmiddlenorm, out Vector3 sectioncenter, out Vector3 sectionnormal, out float sectionMinRadius, out float sectionMinContourEdgeLength)
    {
        return dcel.GetConnectedSectionAdd(faces, newface, out contour, out contournorm, out contourmiddlepoints, out contourmiddlenorm, out sectioncenter, out sectionnormal, out sectionMinRadius, out sectionMinContourEdgeLength);
    }

    public bool GetConnectedSectionRemove(HashSet<int> faces, List<int> facepath, int newface, out Vector3[] contour, out Vector3[] contournorm, out Vector3[] contourmiddlepoints,
        out Vector3[] contourmiddlenorm, out Vector3 sectioncenter, out Vector3 sectionnormal, out float sectionMinRadius, out float sectionMinContourEdgeLength)
    {
        return dcel.GetConnectedSectionRemove(faces, facepath, newface, out contour, out contournorm, out contourmiddlepoints, out contourmiddlenorm, out sectioncenter, out sectionnormal,
            out sectionMinRadius, out sectionMinContourEdgeLength);
    }

    public void GetRings(int f1, out Vector3[] middlepoints1, out Vector3[] middlepoints2)
    {
        dcel.ComputeRings(f1, out middlepoints1, out middlepoints2);
    }
    
    public Mesh Split(bool[] selectedLines, out EditorAction[] editorActions)
    {
        int nsplit = 0;
        editorActions = new EditorAction[2];
        for (int i = 0; i < 2; i++)
        {
            if (selectedLines[i])
            {
                Vector3[] splittedQuads, originalQuads;
                dcel.Split(i, out originalQuads, out splittedQuads);
                editorActions[nsplit++] = new EditorAction(originalQuads, splittedQuads);
            }
        }
        Array.Resize(ref editorActions, nsplit);
        return dcel.GetMesh();
    }

    public Mesh GetSelection(int[] selectedFaces, out Vector3 sectioncenter, out Vector3[] contour, out Vector3[] contourmiddlepoints)
    {
        int nfaces = selectedFaces.Length;//, centralpoint;
        Mesh submesh = dcel.GetMesh(selectedFaces);
        Vector3[] vertices, verticesnorm;
        int[] contourinds, quads;
        int contourlength;

        dcel.BuildSection(ref selectedFaces, Matrix4x4.identity, out contourinds, out vertices, out verticesnorm, out quads);
        sectioncenter = Vector3.zero;
        contourlength = contourinds.Length;
        contour = new Vector3[contourlength];
        contourmiddlepoints = new Vector3[contourlength];

        for (int i = 0; i < contourlength; i++)
        {
            contour[i] = vertices[contourinds[i]];
            contourmiddlepoints[i] = (vertices[contourinds[i]] + vertices[contourinds[(i + 1) % contourlength]]) / 2;
            sectioncenter += vertices[contourinds[i]];
        }

        sectioncenter /= contourlength;

        return submesh;
    }

    public int GetExtrusion(int[] selectedFaces, Matrix4x4 extrusion, out Mesh extrusionMesh, out Vector3 extrusionCenter)
    {
        int nextrusionfaces;
        Vector3[] extrusionAppendix, relevantpoints;

        extrusionMesh = null;
        extrusionCenter = Vector3.zero;

        int extrusionResult = dcel.ComputeExtrusion(selectedFaces, extrusion, out extrusionAppendix);

        if (extrusionAppendix != null)
        {
            extrusionMesh = MeshBuilder.BuildMesh(extrusionAppendix);
            nextrusionfaces = extrusionAppendix.Length / 4;

            relevantpoints = new Vector3[nextrusionfaces * 5];
            for (int i = 0; i < nextrusionfaces; i++)
            {
                relevantpoints[5 * i + 4] = Vector3.zero;
                for (int j = 0; j < 4; j++)
                {
                    relevantpoints[5 * i + j] = extrusionAppendix[4 * i + j];
                    relevantpoints[5 * i + 4] += relevantpoints[5 * i + j];
                }
                relevantpoints[5 * i + 4] /= 4;
            }
            extrusionCenter = relevantpoints[GetCentralPoint(relevantpoints)];
        }
        return extrusionResult;
    }

    public int GetCentralPoint(Vector3[] facecenters)
    {
        float dist, mindist = 100;
        int centerofsection = -1;

        for (int i = 0; i < facecenters.Length; i++)
        {
            dist = facecenters.Select(c => Vector3.Distance(c, facecenters[i])).Max();
            if (mindist > dist)
            {
                mindist = dist;
                centerofsection = i;
            }
        }

        return centerofsection;
    }


    public Mesh Extrude(out EditorAction editorAction)
    {
        Mesh mesh = null;
        Vector3[] extrudedQuads, originalQuads;

        editorAction = null;
        if (dcel.Extrude(out originalQuads, out extrudedQuads))
        {
            mesh = dcel.GetMesh();

            editorAction = new EditorAction(originalQuads, extrudedQuads);
        }

        return mesh;
    }
    
    public int GetDeformation(Vector3 v1, Vector3 v2, Vector3 dir, out Mesh deformedMesh, out Vector3 deformationCenter, out Vector3 deformedv1, out Vector3 deformedv2)
    {
        Vector3[] deformationAppendix;
        int deformationresult = dcel.ComputeDeformation(v1, v2, dir, out deformationCenter, out deformationAppendix, out deformedv1, out deformedv2);

        deformedMesh = null;
        if (deformationAppendix != null)
        {
            deformedMesh = MeshBuilder.BuildMesh(deformationAppendix);
        }
        return deformationresult;
    }

    public Mesh Deform(out EditorAction editorAction)
    {
        Mesh mesh = null;
        Vector3[] deformedQuads, originalQuads;

        editorAction = null;
        if (dcel.Deform(out originalQuads, out deformedQuads))
        {
            mesh = dcel.GetMesh();

            editorAction = new EditorAction(originalQuads, deformedQuads);
        }

        return mesh;
    }

    public void Undo(EditorAction editorAction)
    {
        int[] newfaces = dcel.FindFacesByVertices(editorAction.newquads, 4);
        int nadded; 

        dcel.ReplaceFaces(newfaces,editorAction.oldquads, 4, out nadded);
    }

    public void Redo(EditorAction editorAction)
    {
        int[] oldfaces = dcel.FindFacesByVertices(editorAction.oldquads, 4);
        int nadded;

        dcel.ReplaceFaces(oldfaces, editorAction.newquads, 4, out nadded);
    }

    public string ExportMesh()
    {
        return ObjManager.DcelToString(dcel);
    }

    public Mesh GetMesh()
    {
        return dcel.GetMesh();
    }
}
