using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DCEL {
    protected Dictionary<Vector3, int> VertexTable;
    protected Vertex[] Vertices;
    protected Face[] Faces;
    protected HalfEdge[] HEdges;
    protected Vector3 maxvert = Vector3.zero;
    protected Vector3 minvert = Vector3.zero;
    
    protected int AngleThresh;
    protected Vector3[] PrintingDirections = { new Vector3(1,0,0), new Vector3(0,1,0), new Vector3(0,0,1),
            new Vector3(-1,0,0), new Vector3(0,-1,0), new Vector3(0,0,-1) };
    protected float[] Bounds = { 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f };
    protected bool[] LegalPrintingDirections = { true, true, true, true, true, true };
    protected HashSet<int>[] Basis = new HashSet<int>[6];
    
    public DCEL()
    {
    }

    public void Export(out Vector3[] vertices, out Vector3[] normals, out int[] faces, out int nvert)
    {
        int nfaces = Faces.Length, vert = 0;

        vertices = Vertices.Select(v => v.GetPosition()).ToArray();
        normals = new Vector3[vertices.Length];
        faces = new int[1];
        nvert = 0;

        for (int i = 0; i < Faces.Length; i++)
        {
            int[] verti = GetFaceVertices(i);
            if (i == 0)
            {
                nvert = verti.Length;
                faces = new int[nfaces * nvert];
            }
            foreach (int v in verti)
            {
                normals[v] += vertices[v];
                faces[vert++] = v;
            }
        }
        normals = normals.Select(n => n.normalized).ToArray();
    }

    public void Normalize(ref Vector3[] vertices, float scalex, float scaley, float scalez)
    {
        Vector3 A = 100 * Vector3.one, B = -100 * Vector3.one, C = -new Vector3(scalex, scaley, scalez), D = new Vector3(scalex, scaley, scalez);
        int nvertices = vertices.Length;

        for (int i = 0; i < nvertices; i++)
        {
            A.x = A.x > vertices[i].x ? vertices[i].x : A.x;
            A.y = A.y > vertices[i].y ? vertices[i].y : A.y;
            A.z = A.z > vertices[i].z ? vertices[i].z : A.z;

            B.x = B.x < vertices[i].x ? vertices[i].x : B.x;
            B.y = B.y < vertices[i].y ? vertices[i].y : B.y;
            B.z = B.z < vertices[i].z ? vertices[i].z : B.z;
        }

        for (int i = 0; i < nvertices; i++)
        {
            vertices[i].x = C.x + (D.x - C.x) * (vertices[i].x - A.x) / (B.x - A.x);
            vertices[i].y = C.y + (D.y - C.y) * (vertices[i].y - A.y) / (B.y - A.y);
            vertices[i].z = C.z + (D.z - C.z) * (vertices[i].z - A.z) / (B.z - A.z);
        }
    }

    float[]  GetNewBounds(Vector3[] vertices, out bool[] boundschanged)
    {
        Vector3 maxv = maxvert, minv = minvert;

        float[] bounds = new float[6];

        boundschanged = new bool[6];

        foreach (Vector3 vertex in vertices)
        {
            maxv.x = vertex.x > maxv.x ? vertex.x : maxv.x;
            maxv.y = vertex.y > maxv.y ? vertex.y : maxv.y;
            maxv.z = vertex.z > maxv.z ? vertex.z : maxv.z;

            minv.x = vertex.x < minv.x ? vertex.x : minv.x;
            minv.y = vertex.y < minv.y ? vertex.y : minv.y;
            minv.z = vertex.z < minv.z ? vertex.z : minv.z;

            bounds[0] = minv.x;
            bounds[1] = minv.y;
            bounds[2] = minv.z;
            bounds[3] = maxv.x;
            bounds[4] = maxv.y;
            bounds[5] = maxv.z;

        }
        boundschanged[0] = Mathf.Abs(bounds[0] - Bounds[0]) > 0.001f;
        boundschanged[1] = Mathf.Abs(bounds[1] - Bounds[1]) > 0.001f;
        boundschanged[2] = Mathf.Abs(bounds[2] - Bounds[2]) > 0.001f;
        boundschanged[3] = Mathf.Abs(bounds[3] - Bounds[3]) > 0.001f;
        boundschanged[4] = Mathf.Abs(bounds[4] - Bounds[4]) > 0.001f;
        boundschanged[5] = Mathf.Abs(bounds[5] - Bounds[5]) > 0.001f;

        /*Debug.Log(bounds[0] + " " + Bounds[0] + " " + boundschanged[0] + "\t"
            + bounds[1] + " " + Bounds[1] + " " + boundschanged[1] + "\t"
            + bounds[2] + " " + Bounds[2] + " " + boundschanged[2] + "\t"
            + bounds[3] + " " + Bounds[3] + " " + boundschanged[3] + "\t"
            + bounds[4] + " " + Bounds[4] + " " + boundschanged[4] + "\t"
            + bounds[5] + " " + Bounds[5] + " " + boundschanged[5]);*/

        return bounds;
    }

    void UpdateVertexBounds(Vector3 vertex)
    {
        maxvert.x = vertex.x > maxvert.x ? vertex.x : maxvert.x;
        maxvert.y = vertex.y > maxvert.y ? vertex.y : maxvert.y;
        maxvert.z = vertex.z > maxvert.z ? vertex.z : maxvert.z;
        
        minvert.x = vertex.x < minvert.x ? vertex.x : minvert.x;
        minvert.y = vertex.y < minvert.y ? vertex.y : minvert.y;
        minvert.z = vertex.z < minvert.z ? vertex.z : minvert.z;

        Bounds[0] = minvert.x;
        Bounds[1] = minvert.y;
        Bounds[2] = minvert.z;
        Bounds[3] = maxvert.x;
        Bounds[4] = maxvert.y;
        Bounds[5] = maxvert.z;
    }

    public Vector3 GetMaxVertex()
    {
        return maxvert;
    }
    public Vector3 GetMinVertex()
    {
        return minvert;
    }

    public Vector3 GetFaceCenter(int face)
    {
        return Faces[face].GetCenter();
    }

    public Vector3 GetFaceNormal(int face)
    {
        return Faces[face].GetNormal();
    }
    
    public int[] GetFaceEdges(int face)
    {
        bool start = true;
        int[] edges = new int[10];
        
        int startEdge = Faces[face].GetOuter(), currEdge = startEdge, i = 0;
        if (startEdge != -1)
        {
            while (start || currEdge != startEdge)
            {
                start = false;
                edges[i++] = currEdge;
                if (HEdges[currEdge] == null)
                    Debug.Log("edge " + currEdge + " of face " + face + " is null");
                currEdge = HEdges[currEdge].GetNextEdge();
            }
            return edges.Take(i).ToArray();
        }
        else
            return null;
    }

    public void GetFaceEdgeAndDirections(int f, out Vector3[] edgedir, out int[] edges)
    {
        int nvert;

        edges = GetFaceEdges(f);
        nvert = edges.Length;
        edgedir = new Vector3[nvert];

        for (int j = 0; j < nvert; j++)
            edgedir[j] = (Vertices[HEdges[edges[j]].GetStartPoint()].GetPosition() - Vertices[HEdges[edges[j]].GetEndPoint()].GetPosition()).normalized;
    }

    public int[] GetFaceVertices(int face)
    {
        int[] edges = GetFaceEdges(face);
        int[] vertices = edges.Select(e => HEdges[e].GetStartPoint()).ToArray();
        return vertices;
    }

    public Vector3[] GetFaceVerticesCoords(int face)
    {
        return GetFaceVertices(face).Select(v => Vertices[v].GetPosition()).ToArray();
    }

    public int[] GetAdjacentFaces(int face)
    {
        int[] edges = GetFaceEdges(face);
        int[] incidentFaces = edges.Select(e => HEdges[e].TryGetTwinEdge(out e) ? e : -1)
            .Select(e => e != -1 ? HEdges[e].GetIncidentFace() : -1).ToArray();

        return incidentFaces;
    }

    public int[] GetTouchingFaces(int face)
    {
        int[] vertices = GetFaceVertices(face), edges;
        List<int> touchfaces = new List<int>();

        foreach (int v in vertices)
        {
            TryFindIncidentEdges(v, out edges);
            foreach (int e in edges)
                touchfaces.Add(HEdges[e].GetIncidentFace());
        }

        return touchfaces.ToArray();
    }

    public bool CheckFabricability(Vector3[] polygons, int nvert, int[] deletefaces)
    {
        HashSet<int>[] originalbasis = new HashSet<int>[6], basis;
        bool[] legalprintingdirections;

        for (int i = 0; i < 6; i++)
        {
            originalbasis[i] = new HashSet<int>(Basis[i]);
            foreach (int face in deletefaces)
                if (originalbasis[i].Contains(face))
                    originalbasis[i].Remove(face);
        }

        return CheckFabricability(polygons, nvert, originalbasis, out basis, out legalprintingdirections);
    }

    //Check if dcel is also fabbricable
    public bool CheckFabricability(Vector3[] polygons, int nvert, HashSet<int>[] originalbasis, out HashSet<int>[] basis, out bool[] legalprintingdirections)
    {
        int ndirections = LegalPrintingDirections.Length;
        bool[] boundschanged;
        float[] bounds = GetNewBounds(polygons, out boundschanged);
        int[] addedbasis = new int[6];

        legalprintingdirections = new bool[6];

        for (int i = 0; i < 6; i++)
            legalprintingdirections[i] = LegalPrintingDirections[i];

        basis = new HashSet<int>[6];
        
        for (int d = 0; d < 6; d++)
        {
            addedbasis[d] = 0;
            if (originalbasis[d] == null)
                basis[d] = new HashSet<int>();
            else
                basis[d] = new HashSet<int>(originalbasis[d]);
        }

        for (int i = 0; i < polygons.Length; i += nvert)
        {
            Vector3[] vs = new Vector3[nvert];

            for (int j = 0; j < nvert; j++)
                vs[j] = polygons[i + j];

            Vector3 N = CalculateNormal(vs);
            
            for (int d = 0; d < ndirections; d++)
            {
                if (legalprintingdirections[d])
                {
                    float dot;
                    bool layonthisbound = true;
                    for (int j = 0; j < nvert; j++)
                    {
                        float parallelcomponent;
                        dot = Vector3.Dot(vs[j], PrintingDirections[d]);
                        //The first 3 direction are positive, so they doesn't change the direction of the dot respect of the coords direction
                        if (d < 3)
                            parallelcomponent = dot;
                        else
                            parallelcomponent = -dot;
                        layonthisbound &= dot < 0 && Mathf.Abs(parallelcomponent - bounds[d]) < 0.001f;
                    }

                    if (!layonthisbound)
                    {
                        float acuteAngle = Vector3.Angle(N, PrintingDirections[d]);

                        legalprintingdirections[d] &= (acuteAngle - 90 < AngleThresh);

                        if (!legalprintingdirections[d])
                            ;
                    }
                    else
                    {
                        if (basis[d].Count() == addedbasis[d] || !boundschanged[d])
                        {
                            addedbasis[d]++;
                            basis[d].Add(i / nvert);
                        }
                        else
                        {
                            Debug.Log("Illegal basis");
                            legalprintingdirections[d] = false;
                        }
                    }
                }
            }
            if (!legalprintingdirections.Any(b => b))
            {
                Debug.Log("Violated 3: there is not a legal print direction " + N);
                return false;
            }
        }
        return true;
    }

    //Delete existent faces and add new faces, if addition fails restore deleted faces
    public bool ReplaceFaces(int[] deleteFaces, Vector3[] newpolygons, int nvert, out int nadded)
    {
        int ntodelete = deleteFaces.Length, next = 0;
        //Debug.Log("Before replace " + Vertices.Length + " " + HEdges.Length + " " + Faces.Length);
        //Make a backup of faces to delete
        Vector3[] backupFaces = new Vector3[ntodelete * nvert];

        next = 0;

        foreach (int f in deleteFaces)
        {
            int[] vert = GetFaceVertices(f);
            foreach (int v in vert)
            {
                backupFaces[next] = Vertices[v].GetPosition();
            }
        }

        for (int e = 0; e < HEdges.Length; e++)
        {
            int twin;
            if (!HEdges[e].TryGetTwinEdge(out twin))
            {
                int v1 = HEdges[e].GetStartPoint(), v2 = HEdges[e].GetEndPoint();
                if (v1 == v2)
                    throw new Exception("invalid vertex " + Vertices[v1].GetPosition().ToString("F4")
                        + " " + Vertices[v2].GetPosition().ToString("F4"));
            }
        }

        DeleteFaces(deleteFaces);

        for (int e = 0; e < HEdges.Length; e++)
        {
            int twin;
            if (!HEdges[e].TryGetTwinEdge(out twin))
            {
                int v1 = HEdges[e].GetStartPoint(), v2 = HEdges[e].GetEndPoint();
                if (v1 == v2)
                    throw new Exception("invalid vertex " + Vertices[v1].GetPosition().ToString("F4")
                        + " " + Vertices[v2].GetPosition().ToString("F4") + " " + e);
            }
        }
        
        if (AddNewFaces(newpolygons, nvert, out nadded))
        {
            //Debug.Log("After replace " + Vertices.Length + " " + HEdges.Length + " " + Faces.Length);
            return true;
        }
        else
        {
            Debug.Log("Replace: rollback " + backupFaces.Length / nvert);
            //Restore old faces if the addition fails
            AddNewFaces(backupFaces, nvert, out nadded);
            Debug.Log("After replace " + Vertices.Length + " " + HEdges.Length + " " + Faces.Length);
            return false;
        }
    }

    //The winding order is clockwise
    public bool AddNewFaces(Vector3[] newpolygons, int nvert, out int naddedfaces)
    {
        HashSet<int>[] newbasis;
        bool[] legalprintingdirections;

        if (!CheckFabricability(newpolygons, nvert, Basis, out newbasis, out legalprintingdirections))
        {
            naddedfaces = 0;
            return false;
        }
        //List of edges without twin
        Dictionary<Vector3, int> singleEdges = new Dictionary<Vector3, int>(new ComparerVector3());

        if (HEdges != null)
        {
            for (int e = 0; e < HEdges.Length; e++)
            {
                int twin;
                if (!HEdges[e].TryGetTwinEdge(out twin))
                {
                    int v1 = HEdges[e].GetStartPoint(), v2 = HEdges[e].GetEndPoint();
                    Vector3 edge = new Vector3(v1, v2);
                    singleEdges.Add(edge, e);
                    if (v1 == v2)
                        throw new Exception("invalid vertex " + Vertices[v1].GetPosition().ToString("F4")
                            + " " + Vertices[v2].GetPosition().ToString("F4"));
                }
            }
        }
        
        int norigvert = Vertices != null ? Vertices.Length : 0, norigedg = HEdges != null ? HEdges.Length : 0;
        int norigfaces = Faces != null ? Faces.Length : 0;
        int ndcelf = norigfaces, ndceledg = norigedg, ndcelvert = norigvert;
        int nfaces = newpolygons.Length / nvert;
        if (Faces == null)
        {
            Faces = new Face[nfaces];
            HEdges = new HalfEdge[nfaces * nvert];
            Vertices = new Vertex[nfaces * nvert];
            VertexTable = new Dictionary<Vector3, int>(new ComparerVector3());
        }
        else
        {
            Array.Resize(ref Vertices, ndcelvert + nfaces * nvert);
            Array.Resize(ref HEdges, ndceledg + nfaces * nvert);
            Array.Resize(ref Faces, ndcelf + nfaces);
        }

        bool legalupdate = true;
        for (int i = 0; i < newpolygons.Length && legalupdate; i += nvert)
        {
            Vector3[] vertices = new Vector3[nvert];
            
            for (int j = 0; j < nvert; j++)
            {
                vertices[j] = newpolygons[i + j];
            }

            if (vertices.Distinct().Count() == nvert)
            {
                //Add new face
                int[] vind = new int[nvert];
                Vector3 center = vertices.Aggregate(Vector3.zero, (average, v) => average + v, v => v / nvert);
                Vector3 normal = CalculateNormal(vertices);
                int nvertadded = 0;
                for (int j = 0; j < nvert; j++)
                {
                    //If vertex exists use it
                    if (VertexTable.ContainsKey(vertices[j]))
                    {
                        vind[j] = VertexTable[vertices[j]];
                        if (Vertices[vind[j]].GetPosition() != newpolygons[i + j])
                            throw new Exception("wrong vertex");
                    }
                    //Else create new vertex
                    else
                    {
                        vind[j] = ndcelvert + nvertadded;
                        Vertices[vind[j]] = new Vertex(vertices[j]);
                        VertexTable.Add(vertices[j], vind[j]);
                        UpdateVertexBounds(vertices[j]);
                        nvertadded++;
                        if (Vertices[vind[j]].GetPosition() != newpolygons[i + j])
                            throw new Exception("wrong vertex");
                    }
                }

                //If all vertices are distinct add the face
                if (vind.Distinct().Count() == nvert)
                {
                    int noverlap = 0, older = -1;
                    for (int j = 0; j < nvert && (noverlap == 0 || noverlap == j); j++)
                    {
                        if (Vertices[vind[j]].GetPosition() != newpolygons[i + j])
                            throw new Exception("wrong vertex " + Vertices[vind[j]].GetPosition() + " " +
                               newpolygons[i + j]);
                        int nextVert = vind[(j + 1) % nvert];
                        int currVert = vind[j], nextEdge = ndceledg + ((j + 1) % nvert);
                        int prevEdge = ndceledg + ((j + nvert - 1) % nvert);
                        Vector3 edge = new Vector3(currVert, nextVert);
                        if (currVert == nextVert)
                            throw new Exception("invalid vertex");

                        if (singleEdges.ContainsKey(edge))
                            Debug.Log("found in sigle " + edge + " " + Vertices[vind[j]].GetPosition().ToString("F4")
                                + " " + Vertices[vind[(j + 1) % nvert]].GetPosition().ToString("F4"));
                        else if (TryFindEdge(currVert, nextVert, out older))
                            Debug.Log("found in edges " + Vertices[currVert].GetPosition().ToString("F4")
                                + " " + Vertices[nextVert].GetPosition().ToString("F4") + " " + HEdges[older].ToString);
                        //If the edge doesn't exists
                        if (!singleEdges.ContainsKey(edge) && !TryFindEdge(currVert, nextVert, out older))
                        {
                            //Add the new edge
                            HEdges[ndceledg + j] = new HalfEdge(currVert, nextVert,
                                ndcelf, prevEdge, nextEdge);
                            if (!Vertices[currVert].HasHEdge())
                                Vertices[currVert].SetEdge(ndceledg + j);
                        }
                        else
                            noverlap++;
                    }

                    //If not a duplicate (complete overlap) add the new face and update all indices
                    if (noverlap == 0)
                    {
                        Faces[ndcelf++] = new Face(ndceledg, center, normal);
                        ndceledg += nvert;
                        ndcelvert += nvertadded;
                    }
                    //If there is partial overlapping of faces delete the new faces and stop
                    else if (noverlap != nvert)
                    {
                        Debug.Log("partial overlap noverlap " + noverlap + " face " + ndcelf);
                        legalupdate = false;
                    }
                }
            }
        }
        Array.Resize(ref Vertices, ndcelvert);
        Array.Resize(ref HEdges, ndceledg);
        Array.Resize(ref Faces, ndcelf);

        for (int f = norigfaces; f < ndcelf; f++)
        {
            int[] eind = GetFaceEdges(f);
            int[] vind = GetFaceVertices(f);
            for (int j = 0; j < nvert; j++)
            {
                Vector3 edge = new Vector3(vind[j], vind[(j+1)%nvert]);
                if (!singleEdges.ContainsKey(edge))
                    singleEdges.Add(edge, eind[j]);
                else
                    throw new Exception("Duplicate edge");
            }
        }
        
        if (legalupdate && UpdateAdjacences(singleEdges))
        {
            naddedfaces = ndcelf - norigfaces;
            Basis = newbasis;
            LegalPrintingDirections = legalprintingdirections;
            return true;
        }
        else
        {
            if (!legalupdate)
                Debug.Log("overlapping quads");
            else
                Debug.Log("error in adjacences");
            DeleteFaces(Enumerable.Range(norigfaces, ndcelf - norigfaces).ToArray());
            naddedfaces = 0;
            return false;
        }
    }

    public Vector3 CalculateNormal(Vector3[] coords)
    {
        Vector3 U = (coords[1] - coords[0]).normalized, V = (coords[2] - coords[0]).normalized;
        return Vector3.Cross(U, V).normalized;
    }

    public bool UpdateAdjacences(Dictionary<Vector3, int> singleEdges)
    {
        int ndcelfaces = Faces.Length;
        //Count edges with a twin
        int adj = 0;
        int nedges = singleEdges.Count;

        //Update adjacences
        foreach (int e in singleEdges.Values)
        {
            int v1 = HEdges[e].GetStartPoint(), v2 = HEdges[e].GetEndPoint();
            int twin, twintwin, currface = HEdges[e].GetIncidentFace();
            //If it doesn't have a twin reference 
            if (!HEdges[e].TryGetTwinEdge(out twin))
            {
                //But there is a candidate twin in the dcel or in the new edges
                if (TryFindEdge(v2, v1, out twin) || singleEdges.TryGetValue(new Vector3(v2, v1), out twin))
                {
                    //The candidate has already a twin
                    if (HEdges[twin].TryGetTwinEdge(out twintwin) && HEdges[twintwin].GetIncidentFace() != currface)
                    {
                        Debug.Log("Violated 1.a: Candidate twin "
                        + twin + " for " + e + " has already a twin: " + twintwin);
                        Debug.Log(HEdges[e].ToString + "\n" + HEdges[twin].ToString + "\n" + HEdges[twintwin].ToString);
                        return false;
                    }

                    //Add twin reference to half edge
                    HEdges[e].SetTwinEdge(twin);
                    HEdges[twin].SetTwinEdge(e);
                    adj++;
                }
                else
                    Debug.Log("cannot find twin for " + v1 + " " + v2
                        + " " + Vertices[v1].GetPosition().ToString("F4")
                        + " " + Vertices[v2].GetPosition().ToString("F4"));
                /*else
                    Debug.Log("cannot find twin for " + );*/
            }
        }
        
        if (adj != nedges / 2)
        {
            Debug.Log("Violated 2: This mesh is not watertight " + (nedges - adj) + " edges without twin");
            return false;
        }

        return true;
    }

    protected void DeleteFaces(int[] facestodelete)
    {
        DeleteElements(null, null, facestodelete);
    }

    //Delete elements in array from the dcel, pass null to ignore array of some element
    protected void DeleteElements(int[] verticestodelete, int[] edgestodelete, int[] facestodelete)
    {
        SortedDictionary<int, int> vertexmap = new SortedDictionary<int, int>();
        SortedDictionary<int, int> edgemap = new SortedDictionary<int, int>();
        SortedDictionary<int, int> facemap = new SortedDictionary<int, int>();
        int nvertices = Vertices.Length, nedges = HEdges.Length, nfaces = Faces.Length;
        HashSet<int> verticesToDelete = verticestodelete != null ?
            new HashSet<int>(verticestodelete)
            : new HashSet<int>();
        HashSet<int> edgesToDelete = edgestodelete != null ? 
            new HashSet<int>(edgestodelete)
            : new HashSet<int>();
        HashSet<int> facesToDelete = facestodelete != null ? 
            new HashSet<int>(facestodelete) 
            : new HashSet<int>();
        int lastvertex = nvertices - 1, lastedge = nedges - 1, lastface = nfaces - 1;
        int nvdelete = verticesToDelete.Count, nedelete = edgesToDelete.Count, nfdelete = facesToDelete.Count;

        //Debug.Log("Before deletion " + nvertices + " " + nedges + " " + nfaces);
        if (nfdelete > 0)
        {
            int delind = 0;
            facestodelete = facesToDelete.ToArray();
            Array.Sort(facestodelete);
            lastface -= facestodelete.Length;
            
            foreach(int f in facesToDelete)
            {
                int[] edges = GetFaceEdges(f);
                foreach (int e in edges)
                {
                    if (!edgesToDelete.Contains(e))
                    {
                        edgesToDelete.Add(e);
                        nedelete++;
                    }
                }
            }

            for (int i = nfaces - 1; (i > 0 && delind < nfdelete && facestodelete[delind] <= lastface); i--)
            {
                if (!facesToDelete.Contains(i))
                {
                    int del = facestodelete[delind++];
                    facemap.Add(i, del);
                }
            }

            //Remove deleted faces from basis
            foreach (HashSet<int> basis in Basis)
            {
                foreach (int face in facesToDelete)
                    if(basis.Contains(face))
                        basis.Remove(face);
            }
        }
        
        if (nedelete > 0)
        {
            int delind = 0;
            edgestodelete = edgesToDelete.ToArray();
            Array.Sort(edgestodelete);
            lastedge -= edgestodelete.Length;

            //Search for not connected vertices due to edge deletion
            foreach (int e in edgesToDelete)
            {
                int v = HEdges[e].GetStartPoint();
                int[] edges;
                
                if (TryFindIncidentEdges(v, out edges))
                {
                    edges = Array.FindAll(edges, ed => !edgesToDelete.Contains(ed));
                    if (edges.Length > 0)
                        Vertices[v].SetEdge(edges[0]);
                    else if (!verticesToDelete.Contains(v))
                    {
                        verticesToDelete.Add(v);
                        nvdelete++;
                    }
                }
                else
                    throw new Exception("Vertex " + v + " doesn't have incident edges");
            }

            for (int i = nedges - 1; (i > 0 && delind < nedelete && edgestodelete[delind] <= lastedge); i--)
            {
                if (!edgesToDelete.Contains(i))
                {
                    int del = edgestodelete[delind++];
                    edgemap.Add(i, del);
                }
            }
        }

        if (nvdelete > 0)
        {
            int delind = 0;
            verticestodelete = verticesToDelete.ToArray();
            Array.Sort(verticestodelete);
            lastvertex -= verticestodelete.Length;
            for (int i = nvertices - 1; (i > 0 && delind < nvdelete && verticestodelete[delind] <= lastvertex); i--)
            {
                if (!verticesToDelete.Contains(i))
                {
                    int del = verticestodelete[delind++];
                    vertexmap.Add(i, del);
                }
            }
        }

        //Debug.Log("n faces deleted: " + nfdelete + ", n edges deleted: " + nedelete + ", n vertices deleted: " + nvdelete);

        //Delete edges
        foreach (int i in edgemap.Keys)
        {
            int twin;
            //Overwrite edge
            if (HEdges[edgemap[i]].TryGetTwinEdge(out twin) && !edgesToDelete.Contains(twin))
                HEdges[twin].UnSetTwinEdge();
            HEdges[edgemap[i]] = HEdges[i];
        }
        
        //Remap edge references
        for (int i = 0; i <= lastedge; i++)
        {
            int twin, prev = HEdges[i].GetPrevEdge(), next = HEdges[i].GetNextEdge(), inc = HEdges[i].GetIncidentFace();
            int v1 = HEdges[i].GetStartPoint(), v2 = HEdges[i].GetEndPoint();

            if (HEdges[i].TryGetTwinEdge(out twin))
            {
                if (edgemap.ContainsKey(twin))
                    HEdges[i].SetTwinEdge(edgemap[twin]);
                //Delete reference from deleted twin
                else if (twin > lastedge)
                    HEdges[i].UnSetTwinEdge();
            }
            if (edgemap.ContainsKey(prev))
                HEdges[i].SetPrevEdge(edgemap[prev]);
            if (edgemap.ContainsKey(next))
                HEdges[i].SetNextEdge(edgemap[next]);
            if (vertexmap.ContainsKey(v1))
                HEdges[i].SetStartPoint(vertexmap[v1]);
            if (vertexmap.ContainsKey(v2))
                HEdges[i].SetEndPoint(vertexmap[v2]);
            if (facemap.ContainsKey(inc))
                HEdges[i].SetIncidentFace(facemap[inc]);
        }

        //Delete vertices
        foreach (int i in vertexmap.Keys)
        {
            VertexTable.Remove(Vertices[vertexmap[i]].GetPosition());
            Vertices[vertexmap[i]] = Vertices[i];
            VertexTable[Vertices[vertexmap[i]].GetPosition()] = vertexmap[i];
        }
        
        Vector3[] tablekeys = VertexTable.Keys.ToArray();
        foreach (Vector3 pos in tablekeys)
        {
            if (VertexTable[pos] > lastvertex)
                VertexTable.Remove(pos);
        }

        for (int i = 0; i <= lastvertex; i++)
        {
            int origEdge;
            if (Vertices[i].TryGetHEdge(out origEdge))
            {
                if (edgemap.ContainsKey(origEdge))
                    Vertices[i].SetEdge(edgemap[origEdge]);
            }
            else
                throw new Exception("Vertex " + i + " doesn't have an incident edge");
        }
        
        //Delete faces
        foreach (int i in facemap.Keys)
        {
            Faces[facemap[i]] = Faces[i];
            //Remap basis faces references
            foreach (HashSet<int> basis in Basis)
            {
                if (basis.Contains(i))
                {
                    basis.Remove(i);
                    basis.Add(facemap[i]);
                }
            }
        }

        for (int i = 0; i <= lastface; i++)
        {
            int outer = Faces[i].GetOuter();

            if (edgemap.ContainsKey(outer))
            {
                Faces[i].SetOuter(edgemap[outer]);
            }
        }

        
        System.Array.Resize(ref Vertices, lastvertex + 1);
        System.Array.Resize(ref HEdges, lastedge + 1);
        System.Array.Resize(ref Faces, lastface + 1);
        
        //Debug.Log("After deletion " + (lastvertex+1) + " " + (lastedge+1) + " " + (lastface+1));
    }

    public bool TryFindEdge(int startpoint, int endpoint, out int edge)
    {
        int startedge;
        edge = -1;
       
        //Search using twin
        if (Vertices[startpoint].TryGetHEdge(out startedge))
        {
            if (HEdges[startedge].GetNextEdge() != -1 && HEdges[HEdges[startedge].GetNextEdge()] != null)
            {
                bool start = true;
                int currEdge = startedge, twinEdge = -1;
                int currStartPoint = HEdges[currEdge].GetStartPoint();
                int currEndPoint = HEdges[startedge].GetEndPoint();
                while ((HEdges[currEdge].TryGetTwinEdge(out twinEdge)
                    && (start || currEdge != startedge) && (currEndPoint != endpoint || currStartPoint != startpoint)))
                {
                    start = false;
                    if (HEdges[twinEdge] == null)
                        Debug.Log("twin of " + currEdge + " is null");
                    currEdge = HEdges[twinEdge].GetNextEdge();
                    if (HEdges[currEdge] == null)
                        Debug.Log(currEdge + " is null");
                    currStartPoint = HEdges[currEdge].GetStartPoint();
                    currEndPoint = HEdges[currEdge].GetEndPoint();
                }
                if (currEndPoint == endpoint && currStartPoint == startpoint)
                    edge = currEdge;
            }
        }

        if (edge == -1 && Vertices[endpoint].TryGetHEdge(out startedge))
        {
            if (HEdges[startedge].GetPrevEdge() != -1 && HEdges[startedge].GetPrevEdge() >= HEdges.Length)
                Debug.Log(HEdges[startedge].GetPrevEdge() + " " + startedge + " face " + HEdges[startedge].GetIncidentFace());
            if (HEdges[startedge].GetPrevEdge() != -1 && HEdges[HEdges[startedge].GetPrevEdge()] != null)
            {
                bool start = true;
                int currEdge = startedge, twinEdge = -1;
                int currStartPoint = HEdges[currEdge].GetStartPoint();
                int currEndPoint = HEdges[startedge].GetEndPoint();
                while ((HEdges[currEdge].TryGetTwinEdge(out twinEdge)
                    && (start || currEdge != startedge) && (currEndPoint != endpoint || currStartPoint != startpoint)))
                {
                    start = false;
                    currEdge = HEdges[twinEdge].GetPrevEdge();
                    currStartPoint = HEdges[currEdge].GetStartPoint();
                    currEndPoint = HEdges[currEdge].GetEndPoint();
                }
                if (currEndPoint == endpoint && currStartPoint == startpoint)
                    edge = currEdge;
            }
        }

        return edge != -1;
    }

    public bool TryFindIncidentEdges(int point, out int[] edges)
    {
        int startEdge, twin;
        List<int> es = new List<int>();
        //Search using twin
        if (Vertices[point].TryGetHEdge(out startEdge))
        {
            es.Add(startEdge);

            bool start = true;
            int currEdge = HEdges[startEdge].GetPrevEdge();
            while (HEdges[currEdge].TryGetTwinEdge(out twin) && (start || twin != startEdge))
            {
                start = false;
                currEdge = HEdges[twin].GetPrevEdge();
                es.Add(twin);
                es.Add(currEdge);
            }
        }

        if (es.Count > 0)
            edges = es.Distinct().ToArray();
        else
            edges = null;
        return edges != null;
    }

    public bool TryFindStartingEdges(int startpoint, out int[] edges)
    {
        edges = null;

        if (TryFindIncidentEdges(startpoint, out edges))
        {
            edges = Array.FindAll(edges, e => HEdges[e].GetStartPoint() == startpoint);
        }

        return edges != null;
    }

    public int FindLandingFace(Vector3 refPoint)
    {
        for (int face = 0; face < Faces.Length; face++)
        {
            Vector3[] coords = GetFaceVertices(face).Select(v => Vertices[v].GetPosition()).ToArray();

            if(IsInside(refPoint, coords))
                return face;
        }
        return -1;
    }

    public int[] GetVerticesIndices(Vector3[] coords)
    {
        int ncoords = coords.Length;
        int[] vertices = new int[ncoords];
        List<int> notfound = new List<int>();

        for (int i = 0; i < ncoords; i++)
        {
            if (VertexTable.ContainsKey(coords[i]))
                vertices[i] = VertexTable[coords[i]];
            else
            {
                vertices[i] = -1;
                notfound.Add(i);
            }
        }

        if (notfound.Count > 0)
        {
            float[] dists = new float[ncoords];
            for (int i = 0; i < Vertices.Length; i++)
            {
                foreach(int j in notfound)
                {
                    float dist = Vector3.Distance(coords[j], Vertices[i].GetPosition());
                    if (i==0 || dist < dists[j])
                    {
                        dists[j] = dist;
                        vertices[j] = i;
                    }
                }
            }
        }

        /*
        if (coords.Length > 4)
        {
            string confronto = "";
            for (int i = 0; i < ncoords; i++)
            {
                confronto += "\n" + Vertices[vertices[i]].GetPosition().ToString("F4") + " " + coords[i].ToString("F4");
            }
            Debug.Log(confronto);
        }*/

        return vertices;
    }
    
    public int FindFaceByVertices(Vector3[] coords)
    {
        return FindFaceByVertices(GetVerticesIndices(coords));
    }

    public int FindFaceByVertices(int[] vertices)
    {
        int edge, nvert = vertices.Length;
        HalfEdge e0 = null, e1, e2;

        for (int i = 0; i < nvert; i++)
        {
            int v0 = vertices[i], v1 = vertices[(i + 1) % nvert], v2 = vertices[(i + 2) % nvert];
            if (TryFindEdge(v0, v1, out edge))
            {
                e0 = HEdges[edge];
                e1 = HEdges[e0.GetNextEdge()];
                e2 = HEdges[e0.GetPrevEdge()];
                if (v2 == e1.GetEndPoint() || v2 == e2.GetStartPoint())
                    return e0.GetIncidentFace();
            }
        }

        return -1;
    }

    public int[] FindFacesByVertices(Vector3[] polygons, int nvert)
    {
        int nvertices = polygons.Length;
        int[] polygonsv = GetVerticesIndices(polygons);
        Dictionary<string, int> foundedges = new Dictionary<string, int>();
        int[] faces = new int[nvertices / nvert];
        int[] range = Enumerable.Range(0, nvert).ToArray();

        for (int i = 0; i < nvertices; i += nvert)
        {
            int[] polygon = range.Select(ind => polygonsv[i + ind]).ToArray();
            string[] polygone = range.Select(ind => "" + polygonsv[i + (ind + 1) % nvert] + "," + polygonsv[i + ind]).ToArray();
            int edgeind = Array.FindIndex(polygone, e => foundedges.ContainsKey(e));

            if (edgeind != -1)
            {
                int edgefound = foundedges[polygone[edgeind]], twin;
                HEdges[edgefound].TryGetTwinEdge(out twin);
                faces[i / nvert] = HEdges[twin].GetIncidentFace();
            }
            else
            {
                int[] edges;

                faces[i / nvert] = FindFaceByVertices(polygon);

                edges = GetFaceEdges(faces[i / nvert]);

                for (int j = 0; j < nvert; j++)
                {
                    string edge = HEdges[edges[j]].GetStartPoint() + "," + HEdges[edges[j]].GetEndPoint();
                    foundedges[edge] = edges[j];
                }
            }
        }

        return faces;
    }

    bool IsInside(Vector3 q, Vector3[] polygon)
    {
        int i, n = polygon.Length;
        double m1, m2, eps = 0.0000001f;
        float anglesum = 0;
        Vector3 p1, p2;

        for (i = 0; i < n; i++)
        {
            p1.x = polygon[i].x - q.x;
            p1.y = polygon[i].y - q.y;
            p1.z = polygon[i].z - q.z;
            p2.x = polygon[(i + 1) % n].x - q.x;
            p2.y = polygon[(i + 1) % n].y - q.y;
            p2.z = polygon[(i + 1) % n].z - q.z;

            m1 = p1.magnitude;
            m2 = p2.magnitude;
            if (m1 * m2 <= eps)
                return true; /* We are on a node, consider this inside */
            else
                anglesum += Vector3.Angle(p1, p2);
        }
        return anglesum > 360 - eps && anglesum < 360 + eps;
    }

    public Vector3 HEdgeMiddlePoint(int hedge)
    {
        return MiddlePoint(Vertices[HEdges[hedge].GetStartPoint()].GetPosition(), Vertices[HEdges[hedge].GetEndPoint()].GetPosition());
    }

    public Vector3 MiddlePoint(Vector3 p0, Vector3 p1)
    {
        return (p0 + p1) / 2;
    }

    public class Vertex
    {
        private Vector3 Position;
        private int HEdge;

        public Vertex(Vector3 coord)
        {
            this.Position = coord;
            this.HEdge = -1;
        }

        public bool TryGetHEdge(out int hedge)
        {
            hedge = this.HEdge;
            return hedge != -1;
        }

        public void SetEdge(int hedge)
        {
            this.HEdge = hedge;
        }
        
        public Vector3 GetPosition()
        {
            return this.Position;
        }

        public bool HasHEdge()
        {
            return this.HEdge != -1;
        }

        public string ToString
        {
            get
            {
                return "Point" + Position.ToString("F4") + "," + this.HEdge;
            }
        }
    }

    public class Face
    {
        private int OuterComponent;
        private Vector3 Normal;
        private Vector3 Center;

        public Face(int outer, Vector3 center, Vector3 normal)
        {
            this.OuterComponent = outer;
            this.Center = center;
            this.Normal = normal;
        }

        public void SetOuter(int o)
        {
            this.OuterComponent = o;
        }

        public int GetOuter()
        {
            return this.OuterComponent;
        }

        public Vector3 GetCenter()
        {
            return this.Center;
        }
        
        public string ToString
        {
            get
            {
                return OuterComponent.ToString();
            }
        }

        public Vector3 GetNormal()
        {
            return Normal;
        }
    }

    public class HalfEdge
    {
        private int Startpoint;
        private int Endpoint;
        private int TwinHEdge;
        private int IncidentFace;
        private int PrevEdge;
        private int NextEdge;

        public HalfEdge(int startpoint, int endpoint, int incidentFace, int prevEdge, int nextEdge)
        {
            this.Startpoint = startpoint;
            this.Endpoint = endpoint;
            this.TwinHEdge = -1;
            this.IncidentFace = incidentFace;
            this.PrevEdge = prevEdge;
            this.NextEdge = nextEdge;
        }

        public HalfEdge(int startpoint, int endpoint, int twinHEdge, int incidentFace, int prevEdge, int nextEdge)
        {
            this.Startpoint = startpoint;
            this.Endpoint = endpoint;
            this.TwinHEdge = twinHEdge;
            this.IncidentFace = incidentFace;
            this.PrevEdge = prevEdge;
            this.NextEdge = nextEdge;
        }

        public int GetPrevEdge()
        {
            return this.PrevEdge;
        }

        public void SetPrevEdge(int e)
        {
            this.PrevEdge = e;
        }

        public int GetNextEdge()
        {
            return this.NextEdge;
        }

        public void SetNextEdge(int e)
        {
            this.NextEdge = e;
        }

        public int GetStartPoint()
        {
            return this.Startpoint;
        }

        public void SetStartPoint(int v)
        {
            this.Startpoint = v;
        }

        public int GetEndPoint()
        {
            return this.Endpoint;
        }

        public void SetEndPoint(int v)
        {
            this.Endpoint = v;
        }

        public bool TryGetTwinEdge(out int hedge)
        {
            hedge = this.TwinHEdge;
            return hedge != -1;
        }

        public void SetTwinEdge(int hedge)
        {
            this.TwinHEdge = hedge;
        }

        public void UnSetTwinEdge()
        {
            this.TwinHEdge = -1;
        }

        public int GetIncidentFace()
        {
            return this.IncidentFace;
        }

        public void SetIncidentFace(int f)
        {
            this.IncidentFace = f;
        }

        public bool HasTwin()
        {
            return this.TwinHEdge != -1;
        }

        public string ToString
        {
            get
            {
                return " vertex: " + this.Startpoint
                    + ", " + this.Endpoint
                    + " incident face: " + this.IncidentFace
                    + " prev edge: " + this.PrevEdge
                    + " next edge: " + this.NextEdge
                    + " twin edge: " + this.TwinHEdge;
            }
        }
    }
}
