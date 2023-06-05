using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class QuadDCEL : TriDCEL
{
    //Precomputed data for split
    int[] ringfaces1;
    int[] ringfaces2;
    int[] ringedges1;
    int[] ringedges2;
    Vector3[] ringmiddlepoints1;
    Vector3[] ringmiddlepoints2;

    //Precomputed data for extrusion
    int[] extrusionSelection;
    Vector3[] extrusionFaces;

    int[] deformSelection;
    Vector3[] deformedFaces;

    public QuadDCEL(Vector3[] quads, int angleThresh) : base(angleThresh)
    {
        int nadded;

        Normalize(ref quads, 0.5f, 0.5f, 0.5f);

        if (!AddNewFaces(quads, 4, out nadded))
            throw new Exception("This mesh is not fabbricable");
    }

    public QuadDCEL(Vector3[] quads, int angleThresh, bool withscale) : base(angleThresh)
    {
        int nadded;

        if (!AddNewFaces(quads, 4, out nadded))
            throw new Exception("This mesh is not fabbricable");
    }

    //Build quad dcel from trimesh
    public QuadDCEL(Mesh trimesh, int angleThresh) : base(trimesh, angleThresh)
    {
        int nfaces = Faces.Length;
        int nmerged = 0;
        int[] f1 = new int[nfaces / 2];
        int[] f2 = new int[nfaces / 2];
        HashSet<int> facesToRemove = new HashSet<int>();
        HashSet<int> updatedFaces = new HashSet<int>();
        int[] faces = Enumerable.Range(0, nfaces).ToArray();

        //Check if there are an even number of triangle faces
        if (nfaces % 2 != 0)
            throw new Exception("Cannot quadify this mesh");
        //Quadify
        Array.Sort(faces.Select(f => GetAdjacentFaces(f).Length).ToArray(), faces);
        foreach (int i in faces)
        {
            if (!updatedFaces.Contains(i) && !facesToRemove.Contains(i))
            {
                //For each face search for the adjacent faces
                int[] adjacentFaces = GetAdjacentFaces(i), nadjofadj;
                int nadjacent;
                List<int>[] adjofadj;

                adjacentFaces = Array.FindAll(adjacentFaces, f => !facesToRemove.Contains(f) && !updatedFaces.Contains(f)
                                                && IsALegalCompanion(i, f));
                nadjacent = adjacentFaces.Length;
                nadjofadj = new int[nadjacent];
                adjofadj = new List<int>[nadjacent];

                for (int j = 0; j < nadjacent; j++)
                {
                    adjofadj[j] = Array.FindAll(GetAdjacentFaces(adjacentFaces[j]), f => !facesToRemove.Contains(f) && !updatedFaces.Contains(f)
                                                && IsALegalCompanion(adjacentFaces[j], f)).ToList();
                    nadjofadj[j] = adjofadj[j].Count;
                }

                if (nadjacent > 0 && Array.FindAll(nadjofadj, n => n == 1).Length < 2)
                {
                    int adjacentFace;
                    Array.Sort(nadjofadj, adjacentFaces);

                    adjacentFace = adjacentFaces[0];
                    f1[nmerged] = i;
                    f2[nmerged] = adjacentFace;
                    updatedFaces.Add(i);
                    facesToRemove.Add(adjacentFace);
                    nmerged++;
                }
                else
                {
                    int[] verti = GetFaceVertices(i), edgi = GetFaceEdges(i);
                    Debug.Log(updatedFaces.Count + " " + facesToRemove.Count);
                    throw new Exception("Cannot find adjacent triangle for face " + i + " - " + verti[0] + " " + verti[1] + " " + verti[2]
                        + " " + edgi[0] + " " + edgi[1] + " " + edgi[2] + " " + (nadjacent > 0));
                }
            }
        }

        if (nmerged != nfaces / 2 || !MergeFaces(f1, f2))
            throw new Exception("Cannot find a triangle to form a quad " + nmerged + " " + nfaces / 2);

        for (int i = 0; i < HEdges.Length; i++)
            if (!HEdges[i].TryGetTwinEdge(out nfaces))
                throw new Exception("this mesh is not watertight " + i + " " + HEdges[i].GetIncidentFace());
        CheckConsistency();

        Debug.Log("Create quad dcel");
        //Debug.Log("quaddcel " + Vertices.Length + " " + HEdges.Length + " " + Faces.Length);
    }

    //Check if the two triangles have the same normal and can form a quad with angles smaller than 180°
    //This will work only if the mesh is formed by convex quad, need to be adapted to 
    //give priority to smaller angles and eventually forming ottuse quads when there is no other choice
    public bool IsALegalCompanion(int f1, int f2)
    {
        int twin;
        int[] edges1, edges2;
        Vector3[] edgeDirections1, edgeDirections2;
        Vector3 normal1 = Faces[f1].GetNormal();

        GetFaceEdgeAndDirections(f1, out edgeDirections1, out edges1);
        GetFaceEdgeAndDirections(f2, out edgeDirections2, out edges2);

        int e12ind = Array.FindIndex(edges1, e => HEdges[e].TryGetTwinEdge(out twin) && HEdges[twin].GetIncidentFace() == f2);
        int e21ind = Array.FindIndex(edges2, e => HEdges[edges1[e12ind]].TryGetTwinEdge(out twin) && twin == e);

        if (normal1 != Faces[f2].GetNormal())
            return false;
        else
        {
            return (Mathf.Abs(Vector3.SignedAngle(edgeDirections1[(e12ind + 1) % 3], -edgeDirections1[e12ind], normal1))
                + Mathf.Abs(Vector3.SignedAngle(edgeDirections2[(e21ind + 2) % 3], -edgeDirections2[e21ind], normal1)) < 180
                &&
                Mathf.Abs(Vector3.SignedAngle(edgeDirections1[(e12ind + 2) % 3], -edgeDirections1[e12ind], normal1))
                + Mathf.Abs(Vector3.SignedAngle(edgeDirections2[(e21ind + 1) % 3], -edgeDirections2[e21ind], normal1)) < 180);
        }
    }

    public bool MergeFaces(int[] firstfaces, int[] companionfaces)
    {
        int nfaces = firstfaces.Length;
        int[] deletededges = new int[2 * nfaces];
        for (int i = 0; i < nfaces; i++)
        {
            int f1 = firstfaces[i], f2 = companionfaces[i];
            int[] e1 = GetFaceEdges(f1), e2 = GetFaceEdges(f2), quadedges, removededges;

            //Debug.Log(v1[0] + " " + v1[1] + " " + v1[2] + " " + v2[0] + " " + v2[1] + " " + v2[2]);
            removededges = GetOrderedEdges(e1, e2, out quadedges);
            //Find edges
            if (removededges != null)
            {
                Vector3 center = (Faces[f1].GetCenter() + Faces[f2].GetCenter()) / 2;
                Faces[f1] = new Face(quadedges[0], center, (Faces[f1].GetNormal() + Faces[f2].GetNormal()) / 2);
                for (int k = 0; k < 4; k++)
                {
                    int startpoint = HEdges[quadedges[k]].GetStartPoint();

                    Vertices[startpoint].SetEdge(quadedges[k]);
                    HEdges[quadedges[k]].SetNextEdge(quadedges[(k + 1) % 4]);
                    HEdges[quadedges[k]].SetPrevEdge(quadedges[(k + 3) % 4]);
                    HEdges[quadedges[k]].SetIncidentFace(f1);
                }

                Faces[f2] = new Face(removededges[0], Vector3.zero, Vector3.zero);
                for (int k = 0; k < 2; k++)
                {
                    HEdges[removededges[k]].SetNextEdge(removededges[(k + 1) % 2]);
                    HEdges[removededges[k]].SetPrevEdge(removededges[(k + 1) % 2]);
                    HEdges[removededges[k]].SetIncidentFace(f2);
                }

                deletededges[2 * i] = removededges[0];
                deletededges[2 * i + 1] = removededges[1];

                //int[] quad = GetFaceVertices(f1);
                //Debug.Log(quad[0] + " " + quad[1] + " " + quad[2] + " " + quad[3]);
            }
            else
            {
                return false;
            }
        }
        DeleteElements(null, deletededges, companionfaces);
        CheckConsistency();
        return true;
    }

    //Return couple of common vertices
    public int[] GetOrderedEdges(int[] e1, int[] e2, out int[] me)
    {
        int twin;
        int[] common = { -1, -1 };
        int[] twins = e1.Select(e => HEdges[e].TryGetTwinEdge(out twin) ? twin : -1).ToArray();

        me = new int[4];

        //Debug.Log(HEdges[e1[0]].ToString + " " + HEdges[e1[1]].ToString + " " + 
        //    HEdges[e1[2]].ToString + " " + HEdges[e2[0]].ToString + " " + HEdges[e2[1]].ToString + " " + HEdges[e2[2]].ToString);

        for (int i = 0; i < 3; i++)
        {
            int j = 0, it = 0;
            while (it < 6 && twins[i] != e2[j])
            {
                j = (j + 1) % 3;
                it++;
            }
            if (it < 6)
            {
                me[0] = e2[(j + 1) % 3];
                me[1] = e2[(j + 2) % 3];
                me[2] = e1[(i + 1) % 3];
                me[3] = e1[(i + 2) % 3];
                common[0] = e1[i];
                common[1] = e2[j];

                //Debug.Log(me[0] + " " + me[1] + " " + me[2] + " " + me[3] + " " + common[0] + " " + common[1]);
                return common;
            }
        }

        return null;
    }

    public new void CheckConsistency()
    {
        bool ok = Vertices.Length == VertexTable.Count;

        if (!ok)
            throw new Exception("Vertices count fail");

        ok = true;
        for (int i = 0; i < Faces.Length; i++)
        {
            int[] edges = GetFaceEdges(i);
            ok &= (HEdges[edges[0]].GetEndPoint() == HEdges[edges[1]].GetStartPoint()
                && HEdges[edges[1]].GetEndPoint() == HEdges[edges[2]].GetStartPoint()
                && HEdges[edges[2]].GetEndPoint() == HEdges[edges[3]].GetStartPoint()
                && HEdges[edges[3]].GetEndPoint() == HEdges[edges[0]].GetStartPoint());
        }

        if (!ok)
            throw new Exception("Prev and next check fail");

        ok = true;
        for (int i = 0; i < Faces.Length; i++)
        {
            int[] edges = GetFaceEdges(i);
            ok &= (HEdges[edges[0]].GetStartPoint() != HEdges[edges[0]].GetEndPoint()
                && HEdges[edges[1]].GetStartPoint() != HEdges[edges[1]].GetEndPoint()
                && HEdges[edges[2]].GetStartPoint() != HEdges[edges[2]].GetEndPoint()
                && HEdges[edges[3]].GetStartPoint() != HEdges[edges[3]].GetEndPoint());
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
            else
                throw new Exception("this mesh is not watertight");
        }

        if (!ok)
            throw new Exception("Twins check fail");
    }

    public void CheckNavigability()
    {
        //Check ring adjacency conisstency
        int[] ringfaces, ringedges;
        Vector3[] middlepoints;
        GetRing(0, 0, out ringfaces, out ringedges, out middlepoints);

        HashSet<int> ring = new HashSet<int>(ringfaces);
        HashSet<int> visited = new HashSet<int>();
        int ntovisit = ring.Count();
        ringfaces = ring.ToArray();
        string deb = "";
        for (int j = 0; j < ntovisit; j++)
        {
            int twin, f = ringfaces[j], prevf = ringfaces[(j + ntovisit - 1) % ntovisit];
            int[] intersection = GetFaceEdges(f).Intersect(
                GetFaceEdges(prevf).Select(e => HEdges[e].TryGetTwinEdge(out twin) ? twin : -1)).ToArray();
            int edge = HEdges[intersection[0]].GetNextEdge();
            int[] ring2, ringe2;
            GetRing(f, edge, out ring2, out ringe2, out middlepoints);

            deb += "face " + f + " ring: ";
            foreach (int f1 in ring2)
            {
                deb += " " + f1;
                if (!visited.Contains(f1))
                    visited.Add(f1);
            }
            deb += "\n";
        }
        //Debug.Log(deb);
        if (visited.Count < Faces.Length)
            throw new Exception("navigability check fail");
    }

    public new Mesh GetMesh()
    {
        int nfaces = Faces.Length;
        return GetMesh(Enumerable.Range(0, nfaces).ToArray());
    }

    //Selected faces should be ordinate by adjacence to get correct uvs
    public Mesh GetMesh(int[] selectedFaces)
    {
        Vector3[] quads = selectedFaces.SelectMany(f => GetFaceVertices(f).Select(v => Vertices[v].GetPosition())).ToArray();

        return MeshBuilder.BuildMesh(quads);
    }

    public void ComputeRings(int face, out Vector3[] middlePoints1, out Vector3[] middlePoints2)
    {
        int[] edges = GetFaceEdges(face);

        ComputeRing(face, 0, edges[0], out middlePoints1);
        ComputeRing(face, 1, edges[1], out middlePoints2);
    }

    public void ComputeRing(int face, int nring, int edge, out Vector3[] middlePoints)
    {
        int npoints;

        if (nring == 0)
        {
            GetRing(face, edge, out ringfaces1, out ringedges1, out ringmiddlepoints1);
            npoints = ringmiddlepoints1.Length;
            middlePoints = new Vector3[npoints + 1];

            for (int i = 0; i < npoints + 1; i++)
                middlePoints[i] = ringmiddlepoints1[i % npoints];
        }
        else
        {
            GetRing(face, edge, out ringfaces2, out ringedges2, out ringmiddlepoints2);
            npoints = ringmiddlepoints2.Length;
            middlePoints = new Vector3[npoints + 1];

            for (int i = 0; i < npoints + 1; i++)
                middlePoints[i] = ringmiddlepoints2[i % npoints];
        }

    }

    public void GetRing(int startQuad, int startEdge, out int[] ringfaces, out int[] ringedges, out Vector3[] middlePoints)
    {
        int currEdge = startEdge, currQuad = startQuad;
        int[] edges = GetFaceEdges(startQuad);
        List<int> ringL = new List<int>(), ringeL = new List<int>();
        List<Vector3> middleL = new List<Vector3>();
        bool start = true;
        
        while (start || currQuad != startQuad)
        {
            start = false;
            int localEdgePos = Array.FindIndex(edges, e => e == currEdge);
            int parallelEdge = edges[(localEdgePos + 2) % 4];


            //Next next twin to navigate to the successive quad
            if (HEdges[parallelEdge].TryGetTwinEdge(out currEdge))
            {
                currQuad = HEdges[currEdge].GetIncidentFace();
                edges = GetFaceEdges(currQuad);
                
                ringL.Add(currQuad);
                ringeL.Add(currEdge);
                middleL.Add(HEdgeMiddlePoint(currEdge));
            }
            else
                throw new Exception("twin not found!");
        }

        ringfaces = ringL.ToArray();
        ringedges = ringeL.ToArray();
        middlePoints = middleL.ToArray();
    }

    public void SplitRing(int selectedRing, out int[] deletequads, out Vector3[] subquads)
    {
        int[] ring, ringe;
        int ringLength;
        List<int> deletequadsL = new List<int>();
        List<Vector3> subquadsL = new List<Vector3>();
        Vector3[] middlePoints;

        if (selectedRing == 0)
        {
            ring = ringfaces1;
            ringe = ringedges1;
            middlePoints = ringmiddlepoints1;
        }
        else
        {
            ring = ringfaces2;
            ringe = ringedges2;
            middlePoints = ringmiddlepoints2;
        }

        ringLength = ring.Length;
        for (int i = 0; i < ringLength ; i++)
        {
            //The parallel edge is the next edge in chain and it becomes to the next quad
            int currQuad = ring[i];
            int[] edges = GetFaceEdges(currQuad);
            Vector3 m1 = middlePoints[i], m2 = middlePoints[(i + 1) % ringLength];
            Vector3[] verti = edges.Select(e => Vertices[HEdges[e].GetStartPoint()].GetPosition()).ToArray();
            int currVert = Array.FindIndex(edges, e => e == ringe[i]);

            //Delete quad ABCD (curredge is AB, paralleledge is CD)
            deletequadsL.Add(currQuad);

            //Create and add 2 subquads (F is middle point of AB, E of CD)

            //First subquad AFED
            subquadsL.Add(verti[currVert]);
            subquadsL.Add(m1);
            subquadsL.Add(m2);
            subquadsL.Add(verti[(currVert + 3) % 4]);
            
            //Second subquad FBCE
            subquadsL.Add(m1);
            subquadsL.Add(verti[(currVert + 1) % 4]);
            subquadsL.Add(verti[(currVert + 2) % 4]);
            subquadsL.Add(m2);


            /*Debug.Log(Vertices[HEdges[currEdge].GetStartPoint()].GetPosition() + " " + Vertices[HEdges[currEdge].GetEndPoint()].GetPosition()
                + " " + Vertices[HEdges[parallelEdge].GetStartPoint()].GetPosition() + " " + Vertices[HEdges[parallelEdge].GetEndPoint()].GetPosition()
                + " \n" + Vertices[HEdges[currEdge].GetStartPoint()].GetPosition() + " " + HEdgeMiddlePoint(currEdge)
                + " " + HEdgeMiddlePoint(parallelEdge) + " " + Vertices[HEdges[parallelEdge].GetEndPoint()].GetPosition()
                + " \n" + HEdgeMiddlePoint(currEdge) + " " + Vertices[HEdges[currEdge].GetEndPoint()].GetPosition()
                + " " + Vertices[HEdges[parallelEdge].GetStartPoint()].GetPosition() + " " + HEdgeMiddlePoint(parallelEdge));*/
        }

        deletequads = deletequadsL.ToArray();
        subquads = subquadsL.ToArray();
    }

    //Select a ring of faces given a start position and a direction; it returns the array of couples of quad of the ring
    public void Split(int selectedRing, out Vector3[] originalquads, out Vector3[] subquads)
    {
        int[] deletefaces;
        int nadded;

        SplitRing(selectedRing, out deletefaces, out subquads);
        originalquads = deletefaces.SelectMany(f => GetFaceVerticesCoords(f)).ToArray();
        
        if (!ReplaceFaces(deletefaces, subquads, 4, out nadded))
            throw new Exception("Error in split");


        int nquads = subquads.Length;
        Vector3[] coords = { subquads[nquads-4], subquads[nquads-3], subquads[nquads-2], subquads[nquads-1] }, middlepoints;
        int newfirstface = FindFaceByVertices(coords), newfirstedge;

        TryFindEdge(VertexTable[subquads[nquads - 1]], VertexTable[subquads[nquads - 4]], out newfirstedge);

        ComputeRing(newfirstface, (selectedRing + 1) % 2, newfirstedge, out middlepoints); 
        //CheckNavigability();
    }

    /*
    public int[] GetSection(Vector3[] startPoss, Vector3[] directions, Vector3 refPoint)
    {
        int nrings = startPoss.Count(), nfaces = Faces.Length;
        List<int>[] sections = new List<int>[2 ^ nrings];
        bool[,] facesBinSections = new bool[nfaces, nrings];
        int[] facesSections = new int[nfaces];
        int selectedSection = FindLandingFace(refPoint);

        for (int i = 0; i < nrings; i++)
        {
            int[] ringfaces, ringedges;
            HashSet<int> ring, visited = new HashSet<int>();
            int nvisited = 0, ntovisit;
            
            GetRing(startPoss[i], directions[i], out ringfaces, out ringedges);
            ring = new HashSet<int>(ringfaces);
            ringfaces = ring.ToArray();
            ntovisit = ring.Count;

            for (int j=0; j<ntovisit; j++)
            {
                int[] ringf, ringe;
                int twin, f = ringfaces[j], prevf = ringfaces[(j + ntovisit - 1) % ntovisit];
                int[] intersection = GetFaceEdges(f).Intersect(
                    GetFaceEdges(prevf).Select(e => HEdges[e].TryGetTwinEdge(out twin) ? twin : -1)).ToArray();
                int edge = HEdges[intersection[0]].GetNextEdge();
                bool isInSection1 = true, inring = true;

                GetRing(f, edge, out ringf, out ringe);
                foreach (int r in ringf)
                {
                    inring = ring.Contains(r);
                    if (inring)
                        isInSection1 = !isInSection1;
                    if (!visited.Contains(r))
                    {
                        if (inring)
                            facesBinSections[r, i] = true;
                        else
                            facesBinSections[r, i] = isInSection1;
                        visited.Add(r);
                        nvisited++;
                    }
                }
            }
            
            if (nvisited < nfaces)
            {
                foreach (int f in visited.Except(Enumerable.Range(0, nfaces)))
                {
                    int[] adj = GetAdjacentFaces(f);
                    Debug.Log("missing " + f + " vertices " + adj[0]
                        + " " + adj[1]
                        + " " + adj[2]
                        + " " + adj[3]);
                }
                throw new Exception(i + " Error visiting quad mesh: mesh is not correct.\n" +
                   "Visited " + (nvisited) + " of " + nfaces);
            }
        }

        for (int i = 0; i < nfaces; i++)
        {
            string section = "";
            for (int r = 0; r < nrings; r++)
                section += facesBinSections[i, r] ? 1 : 0;
            facesSections[i] = Convert.ToInt32(section, 2);
        }

        //Debug.Log("section 1 " + Array.FindAll(facesSections, f => f == 0).Length);
        //Debug.Log("section 2 " + Array.FindAll(facesSections, f => f == 1).Length);
        selectedSection = facesSections[FindLandingFace(refPoint)];
        return Array.FindAll(Enumerable.Range(0, nfaces).ToArray(), f => facesSections[f] == 2);
    }*/

    public int ComputeExtrusion(int[] selectedFaces, Matrix4x4 extrusion, out Vector3[] extrusionAppendix)
    {
        Vector3[] inputVertices, inputVerticesNorm;
        int[] contour, inputQuads;
        Vector3 extrusionDirection = extrusion.MultiplyPoint(Vector3.zero).normalized;
        bool ismanifold = true;

        extrusionSelection = null;
        extrusionFaces = null;
        extrusionAppendix = null;

        if (BuildSection(ref selectedFaces, extrusion, out contour, out inputVertices, out inputVerticesNorm, out inputQuads))
        {
            int quadCount = 0, extrudedVertexCount = 0, extrudedQuadIndexCount = 0;
            int nvertices, nextCapVertex, nextCapQuadIndex, contourCount;
            ComparerVector3 comp = new ComparerVector3();

            quadCount = inputQuads.Length;
            contourCount = contour.Length;

            Vector3[] quads = new Vector3[4 * contour.Length + quadCount];
            Vector3[] vertices = new Vector3[inputQuads.Length + 2 * contour.Length];
            
            // Build extruded vertices
            foreach (int v in contour)
            {
                vertices[extrudedVertexCount] = inputVertices[v];
                vertices[extrudedVertexCount + contourCount] = extrusion.MultiplyPoint(inputVertices[v]);
                extrudedVertexCount++;
            }

            // Build top vertices
            nvertices = 2 * extrudedVertexCount;
            for (int i = 0; i < quadCount; i++)
            {
                vertices[nvertices++] = extrusion.MultiplyPoint(inputVertices[inputQuads[i]]);
            }
            Array.Resize(ref vertices, nvertices);

            // Build extruded quads
            for (int vind = 0; vind < extrudedVertexCount; vind++)
            {
                int[] indices = { vind, (vind + 1), (vind + contourCount + 1), (vind + contourCount) };
                if (vind == extrudedVertexCount - 1)
                {
                    indices[1] = 0;
                    indices[2] = extrudedVertexCount;
                }
                Vector3[] coords = { vertices[indices[0]], vertices[indices[1]], vertices[indices[2]], vertices[indices[3]] };

                bool[] b = { !comp.Equals(coords[0], coords[1]),
                            !comp.Equals(coords[0], coords[2]),
                            !comp.Equals(coords[0], coords[3]),
                            !comp.Equals(coords[1], coords[2]),
                            !comp.Equals(coords[1], coords[3]),
                            !comp.Equals(coords[2], coords[3]) };

                if (b.Aggregate(true, (ag, bb) => ag && bb, ag => ag))
                {
                    //Check if extruded vertices (coords 2 and coords 3) already exists
                    if (VertexTable.ContainsKey(coords[2]) || VertexTable.ContainsKey(coords[3]))
                    {
                        Debug.Log("Illegal extrusion: non manifold vertex " + coords[2] + " " + coords[3]);
                        ismanifold = false;
                    }
                    else
                    {
                        //Coords 0 and coords 1 are original vertices
                        int extruded_edge, twin = -1, v1 = VertexTable[coords[0]], v2 = VertexTable[coords[1]];
                        if (TryFindEdge(v1, v2, out extruded_edge) && HEdges[extruded_edge].TryGetTwinEdge(out twin))
                        {
                            //P0 is the central point of extruded edge, P1 is the middle point of the other edge of the adjacent,
                            //P2 is the middle point of the other edge of the extruded face
                            Vector3 p0 = HEdgeMiddlePoint(twin), p1 = HEdgeMiddlePoint(HEdges[HEdges[twin].GetNextEdge()].GetNextEdge());
                            Vector3 p2 = MiddlePoint(coords[2], coords[3]), p01 = (p0 - p1).normalized, p02 = (p0 - p2).normalized;
                            Vector3 refdir = (coords[2] - coords[3]).normalized;
                            
                            //The extruded faces must not intersect the faces adjacent to the contour (sin of theta must be positive)
                            quads[extrudedQuadIndexCount] = coords[0];
                            quads[extrudedQuadIndexCount + 1] = coords[1];
                            quads[extrudedQuadIndexCount + 2] = coords[2];
                            quads[extrudedQuadIndexCount + 3] = coords[3];

                            extrudedQuadIndexCount += 4;

                            //The angle is between -90 + 90 and the angle between 0 and -90
                            if (Vector3.Dot(p01, p02) > 0 && Vector3.Dot(Vector3.Cross(p01, p02), -refdir) <= 0) //Vector3.Cross(p01, p02).magnitude <= 0.01f
                            {
                                int f1 = HEdges[extruded_edge].GetIncidentFace(), f2 = HEdges[twin].GetIncidentFace();
                                Debug.Log("Illegal extrusion: face intersection " + f1 + " " + GetFaceNormal(f1)
                                    + " " + f2 + " " + GetFaceNormal(f2));
                                ismanifold = false;
                            }
                        }
                    }
                }
                else
                {
                    ismanifold = false;
                }
            }
            
            // Top
            nextCapVertex = 2 * extrudedVertexCount;
            nextCapQuadIndex = extrudedQuadIndexCount;
            while (nextCapVertex < nvertices)
            {
                bool[] b = { !comp.Equals(vertices[nextCapVertex], vertices[nextCapVertex + 1]),
                        !comp.Equals(vertices[nextCapVertex], vertices[nextCapVertex + 2]),
                        !comp.Equals(vertices[nextCapVertex], vertices[nextCapVertex + 3]),
                        !comp.Equals(vertices[nextCapVertex + 1], vertices[nextCapVertex + 2]),
                        !comp.Equals(vertices[nextCapVertex + 1], vertices[nextCapVertex + 3]),
                        !comp.Equals(vertices[nextCapVertex + 2], vertices[nextCapVertex + 3]) };
                if (b.Aggregate(true, (ag, bb) => ag && bb, ag => ag))
                {
                    quads[nextCapQuadIndex + 0] = vertices[nextCapVertex];
                    quads[nextCapQuadIndex + 1] = vertices[nextCapVertex + 1];
                    quads[nextCapQuadIndex + 2] = vertices[nextCapVertex + 2];
                    quads[nextCapQuadIndex + 3] = vertices[nextCapVertex + 3];

                    nextCapQuadIndex += 4;
                    nextCapVertex += 4;
                }
                else
                {
                    ismanifold = false;
                }
            }
            Array.Resize(ref quads, nextCapQuadIndex);

            extrusionSelection = selectedFaces;
            extrusionFaces = quads;
            //Build base for extrusion appendix and append to the extruded faces
            extrusionAppendix = selectedFaces.SelectMany(face => GetFaceVertices(face).Reverse())
                .Select(vertex => Vertices[vertex].GetPosition()).Concat(quads).ToArray();
            
            return ismanifold && CheckFabricability(quads, 4, extrusionSelection) ? 0 : 1;
        }
        return 2;
    }

    //Build an array of section vertices, a contour and an array of quads referring to the first
    public bool BuildSection(ref int[] selectedFaces, Matrix4x4 extrusion, out int[] contour, out Vector3[] vertices, out Vector3[] verticesnorm, out int[] quads)
    {
        if (selectedFaces.Any() && ( extrusion.isIdentity || CleanSection(ref selectedFaces, extrusion)))
        {
            int vertexCount = 0, quadCount = 0, contourCount = 0;
            Dictionary<Vector3, int> verticesTable = new Dictionary<Vector3, int>(new ComparerVector3());
            HashSet<int> contourEdges = new HashSet<int>(selectedFaces.SelectMany(f => GetFaceEdges(f)));
            //Map each contour vertex to the correspondent endpoint position in contourVertices array
            Dictionary<Vector3, Vector3> startToEndpoint = new Dictionary<Vector3, Vector3>(new ComparerVector3());

            vertices = new Vector3[Vertices.Length];
            verticesnorm = new Vector3[Vertices.Length];
            quads = new int[4 * selectedFaces.Length];

            foreach (int f in selectedFaces)
            {
                int twin;
                int[] edges = GetFaceEdges(f);
                int[] vind = { HEdges[edges[0]].GetStartPoint(), HEdges[edges[1]].GetStartPoint(),
                        HEdges[edges[2]].GetStartPoint(), HEdges[edges[3]].GetStartPoint() };
                Vector3[] v = vind.Select(ind => Vertices[ind].GetPosition()).ToArray();
                Vector3 facenormal = GetFaceNormal(f);
                for (int i = 0; i < 4; i++)
                {
                    bool iscontour = contourEdges.Contains(edges[i]);

                    if (HEdges[edges[i]].TryGetTwinEdge(out twin) && contourEdges.Contains(twin))
                    {
                        contourEdges.Remove(edges[i]);
                        contourEdges.Remove(twin);
                        iscontour = false;
                    }

                    if (!verticesTable.ContainsKey(v[i]))
                    {
                        vertices[vertexCount] = v[i];
                        verticesTable.Add(v[i], vertexCount);
                        vertexCount++;
                    }

                    verticesnorm[verticesTable[v[i]]] += facenormal;

                    if (iscontour)
                    {
                        //Debug.Log(i + " " + v[i] + " " + ((i+1)%4) + " " + v[(i + 1) % 4]);
                        if(!startToEndpoint.ContainsKey(v[i]))
                            startToEndpoint.Add(v[i], v[(i + 1) % 4]);

                        contourCount++;
                    }
                    //Add only the first vertex of edge to the quad
                    quads[quadCount++] = verticesTable[v[i]];
                }
            }
            Array.Resize(ref vertices, vertexCount);
            Array.Resize(ref quads, quadCount);

            contour = new int[contourCount];
            contourCount = 0;
            contour[contourCount] = verticesTable[startToEndpoint.First().Key];
            while (contourCount < contour.Length - 1)
            {
                Vector3 curr = vertices[contour[contourCount]];
                //If the contour is interrupted due to a discontinuity in the selected faces
                if (!startToEndpoint.ContainsKey(curr))
                    throw new Exception("Selected faces are not adjacent");
                Vector3 next = startToEndpoint[curr];

                contour[contourCount + 1] = verticesTable[next];
                contourCount++;
            }
            return true;
        }
        else
        {
            contour = null;
            vertices = null;
            verticesnorm = null;
            quads = null;
            return false;
        }
    }

    public bool BuildSection(ref int[] selectedFaces, Matrix4x4 extrusion, out int[] contourind, out Vector3[] vertices, out Vector3[] verticesnorm, out Vector3 sectioncenter, out Vector3 sectionnormal, out int[] quads)
    {
        if (BuildSection(ref selectedFaces, extrusion, out contourind, out vertices, out verticesnorm, out quads))
        {
            int nvertices = vertices.Length, centralface;
            Vector3[] sortedvertices = vertices.Select(v => v).ToArray();
            Vector3 center = Vector3.zero;
            
            for (int i = 0; i < nvertices; i++)
                center += vertices[i];
            center /= nvertices;

            sectioncenter = center;
            float[] dists = vertices.Select(v => Vector3.Distance(center, v)).ToArray();
            Array.Sort(dists, sortedvertices);

            centralface = FindFaceByVertices(sortedvertices.Take(4).ToArray());
            if (centralface == -1)
                sectionnormal = sortedvertices.Take(4).Aggregate(Vector3.zero, (acc, v) => acc + v, res => res / 4).normalized;
            else
                sectionnormal = GetFaceNormal(centralface);

            return true;
        }
        else
        {
            sectioncenter = Vector3.zero;
            sectionnormal = Vector3.zero;
            return false;
        }
    }

    //Delete the faces that cannot be extruded in the chosen direction
    public bool CleanSection(ref int[] selectedFaces, Matrix4x4 extrusion)
    {
        IEnumerable<int> remaining;
        //Vector3 extrusionDirection = extrusion.MultiplyPoint(Vector3.zero).normalized;
        Vector3 extrusionDirection = extrusion.GetColumn(3).normalized;

        extrusionDirection.Normalize();

        remaining = selectedFaces.Where(f => Vector3.Dot(extrusionDirection, Faces[f].GetNormal()) > 0);
        if (remaining.Any())
        {
            selectedFaces = remaining.ToArray();
            return true;
        }
        return false;
    }

    public bool Extrude(out Vector3[] originalQuads, out Vector3[] extrudedQuads)
    {
        extrudedQuads = null;
        originalQuads = null;
        if (extrusionSelection != null && extrusionFaces != null)
        {
            int nadded;
            originalQuads = extrusionSelection.SelectMany(f => GetFaceVerticesCoords(f)).ToArray();
            if (!ReplaceFaces(extrusionSelection, extrusionFaces, 4, out nadded))
            {
                Debug.LogWarning("There was an error in extrusion");
                return false;
            }
            extrudedQuads = extrusionFaces;
            return true;
        }
        else
            return false;
    }

    public int ComputeDeformation(Vector3 v1, Vector3 v2, Vector3 dir, out Vector3 deformededgecenter, out Vector3[] deformedQuads, out Vector3 movedv1, out Vector3 movedv2)
    {
        Vector3[] vertices = { v1, v2 };
        int[] verticesinds = GetVerticesIndices(vertices);
        int edge, twin, v1i = verticesinds[0], v2i = verticesinds[1];
        bool ismanifold = true;

        if (TryFindEdge(v1i, v2i, out edge) && HEdges[edge].TryGetTwinEdge(out twin))
        {
            int f1 = HEdges[edge].GetIncidentFace(), f2 = HEdges[twin].GetIncidentFace();

            int[] modifiedFaces, incEdges;
            int nmodifiedFaces;

            TryFindIncidentEdges(v1i, out incEdges);
            modifiedFaces = incEdges.Select(e => HEdges[e].GetIncidentFace()).ToArray();
            TryFindIncidentEdges(v2i, out incEdges);
            modifiedFaces = modifiedFaces.Concat(incEdges.Select(e => HEdges[e].GetIncidentFace())).Distinct().ToArray();
            nmodifiedFaces = modifiedFaces.Length;

            //Take the component that is perpendicular to the normal of the lateral faces
            /*for (int i = 0; i < nmodifiedFaces; i++)
            {
                if (modifiedFaces[i] != f1 && modifiedFaces[i] != f2)
                {
                    Vector3 normal = GetFaceNormal(modifiedFaces[i]);
                    float dot = Vector3.Dot(normal, dir);
                    dir = dir - dot * normal;
                }
            }*/

            deformSelection = modifiedFaces;
            deformedFaces = new Vector3[nmodifiedFaces * 4];


            Vector3 n1 = GetFaceNormal(f1), n2 = GetFaceNormal(f2);
            float dot1 = Vector3.Dot(dir, n1), dot2 = Vector3.Dot(dir, n2);
            dir = Mathf.Abs(dot1) > Mathf.Abs(dot2) ? dot1 * n1 : dot2 * n2;
            movedv1 = v1 + dir;
            movedv2 = v2 + dir;
            Debug.Log(v1.ToString("F4") + " " + v2.ToString("F4") + " " + movedv1.ToString("F4") + " " + movedv2.ToString("F4"));

            for (int i = 0; i < nmodifiedFaces; i++)
            {
                int[] facevertices = GetFaceVertices(modifiedFaces[i]);
                for (int j = 0; j < 4; j++)
                {
                    if (facevertices[j] == v1i)
                        deformedFaces[4 * i + j] = movedv1;
                    else if (facevertices[j] == v2i)
                        deformedFaces[4 * i + j] = movedv2;
                    else
                        deformedFaces[4 * i + j] = Vertices[facevertices[j]].GetPosition();
                }
            }

            if (VertexTable.ContainsKey(movedv1) || VertexTable.ContainsKey(movedv2))
            {
                Debug.Log("Illegal deformation: non manifold vertex " + movedv1 + " " + movedv2);
                ismanifold = false;
            }

            //Check for face collisions
            int nextofnext = HEdges[HEdges[edge].GetNextEdge()].GetNextEdge();
            Vector3 p0 = MiddlePoint(movedv1, movedv2), p1 = HEdgeMiddlePoint(HEdges[HEdges[twin].GetNextEdge()].GetNextEdge());
            Vector3 p2 = HEdgeMiddlePoint(nextofnext), p01 = (p0 - p1).normalized, p02 = (p0 - p2).normalized;
            Vector3 refdir = (movedv1 - movedv2).normalized;
            //The extruded faces must not intersect the faces adjacent to the contour (sin of theta must be positive)
            //The angle is between -90 + 90 and the angle between 0 and -90
            if (Vector3.Dot(p01, p02) > 0 && Vector3.Dot(Vector3.Cross(p01, p02), -refdir) <= 0) //Vector3.Cross(p01, p02).magnitude <= 0.01f&& Vector3.Cross(p01, p02).magnitude <= 0)
            {
                Debug.Log("Illegal extrusion: face intersection " + f1 + " " + GetFaceNormal(f1)
                    + " " + f2 + " " + GetFaceNormal(f2));
                ismanifold = false;
            }

            deformedQuads = deformedFaces;
            deformededgecenter = p0;

            return ismanifold && CheckFabricability(deformedFaces, 4, deformSelection) ? 0 : 1;
        }
        else
            throw new Exception("Edge not found");
    }

    public bool Deform(out Vector3[] originalQuads, out Vector3[] deformedQuads)
    {
        deformedQuads = null;
        originalQuads = null;
        if (deformSelection != null && deformedFaces != null)
        {
            int nadded;
            originalQuads = deformSelection.SelectMany(f => GetFaceVerticesCoords(f)).ToArray();
            if (!ReplaceFaces(deformSelection, deformedFaces, 4, out nadded))
                throw new Exception("There was an error in extrusion");
            deformedQuads = deformedFaces;
            return true;
        }
        else
            return false;
    }

    public bool GetConnectedSectionAdd(HashSet<int> faces, int newface, out Vector3[] contour, out Vector3[] contournorm, out Vector3[] contourmiddlepoints, out Vector3[] contourmiddlenorm, 
        out Vector3 sectioncenter, out Vector3 sectionnormal, out float sectionMinRadius, out float sectionMinContourEdgeLength)
    {
        int[] adjs = GetAdjacentFaces(newface), quads, contourind;
        Vector3[] vertices, verticesnorm;
        
        contour = null;
        contournorm = null;
        contourmiddlepoints = null;
        contourmiddlenorm = null;
        sectioncenter = Vector3.zero;
        sectionnormal = Vector3.zero;
        sectionMinRadius = 100;
        sectionMinContourEdgeLength = 100;

        //Check if the new face is adjacent to some of the other faces
        if (faces.Add(newface) && (faces.Count == 1 || adjs.Where(f => faces.Contains(f)).Count() > 0))
        {
            int[] section = faces.ToArray();
            if (BuildSection(ref section, Matrix4x4.identity, out contourind, out vertices, out verticesnorm, out sectioncenter, out sectionnormal, out quads))
            {
                int ncontour = contourind.Length;
                contour = new Vector3[ncontour];
                contournorm = new Vector3[ncontour];
                contourmiddlepoints = new Vector3[ncontour];
                contourmiddlenorm = new Vector3[ncontour];

                for (int i = 0; i < ncontour; i++)
                {
                    float dist;
                    contour[i] = vertices[contourind[i]];
                    contournorm[i] = verticesnorm[contourind[i]];
                    contourmiddlepoints[i] = MiddlePoint(vertices[contourind[i]], vertices[(contourind[(i + 1) % ncontour])]);
                    contourmiddlenorm[i] = MiddlePoint(verticesnorm[contourind[i]], verticesnorm[(contourind[(i + 1) % ncontour])]);
                    dist = Vector3.Distance(sectioncenter, contour[i]);
                    if (dist < sectionMinRadius)
                        sectionMinRadius = dist; dist = Vector3.Distance(sectioncenter, contour[i]);
                    dist = Vector3.Distance(contourmiddlepoints[(i + ncontour - 1) % ncontour], contourmiddlepoints[i]);
                    if (i > 0 && dist < sectionMinContourEdgeLength)
                        sectionMinContourEdgeLength = dist;
                }
                return true;
            }
            Debug.Log("fail build section");
        }
        return false;
    }
    public bool GetConnectedSectionRemove(HashSet<int> faces, List<int> facepath, int delface, out Vector3[] contour, out Vector3[] contournorm, out Vector3[] contourmiddlepoints, out Vector3[] contourmiddlenorm, 
        out Vector3 sectioncenter, out Vector3 sectionnormal, out float sectionMinRadius, out float sectionMinContourEdgeLength)
    {
        int[] quads, contourind;
        Vector3[] vertices, verticesnorm;
        int nfaces = faces.Count(), facepos = facepath.FindIndex(f => f == delface);
        bool connected = true;

        contour = null;
        contournorm = null;
        contourmiddlepoints = null;
        contourmiddlenorm = null;
        sectioncenter = Vector3.zero;
        sectionnormal = Vector3.zero;
        sectionMinRadius = 100;
        sectionMinContourEdgeLength = 100;

        if (faces.Count == 2)
            connected = true;
        else
        {
            int[] closefaces = faces.Where(f => GetAdjacentFaces(f).Contains(delface)).ToArray();
            int nclose = closefaces.Length;
            //Check if the face to delete doesn't leave some faces without adjacent
            for (int i = 0; i < nclose; i++)
            {
                int[] adjs = GetAdjacentFaces(closefaces[i]);
                connected &= adjs.Where(f => f != delface && faces.Contains(f)).Count() > 0;
            }
        }

        if (connected && faces.Remove(delface))
        {
            int[] facearr = faces.ToArray();
            if (BuildSection(ref facearr, Matrix4x4.identity, out contourind, out vertices, out verticesnorm, out sectioncenter, out sectionnormal, out quads))
            {
                int ncontour = contourind.Length;

                contour = new Vector3[ncontour];
                contournorm = new Vector3[ncontour];
                contourmiddlepoints = new Vector3[ncontour];
                contourmiddlenorm = new Vector3[ncontour];

                for (int i = 0; i < ncontour; i++)
                {
                    float dist;
                    contour[i] = vertices[contourind[i % ncontour]];
                    contournorm[i] = verticesnorm[contourind[i % ncontour]];
                    contourmiddlepoints[i] = MiddlePoint(vertices[contourind[i % ncontour]], vertices[(contourind[(i + 1) % ncontour])]);
                    contourmiddlenorm[i] = MiddlePoint(verticesnorm[contourind[i % ncontour]], verticesnorm[(contourind[(i + 1) % ncontour])]);
                    dist = Vector3.Distance(sectioncenter, contour[i]);
                    if (dist < sectionMinRadius)
                        sectionMinRadius = dist; dist = Vector3.Distance(sectioncenter, contour[i]);
                    dist = Vector3.Distance(contourmiddlepoints[(i + ncontour - 1) % ncontour], contourmiddlepoints[i]);
                    if (i > 0 && dist < sectionMinRadius)
                        sectionMinContourEdgeLength = dist;
                }
                return true;
            }
        }
        return false;
    }
}