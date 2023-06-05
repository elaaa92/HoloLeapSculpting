using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SculptureManager : MonoBehaviour {
    private GameObject Sculpture;
    private MeshFilter filter;
    private Renderer screnderer;
    
    private MessageSender messageSender;

    private MeshManager meshManager;
    private int lastTouchedFace;
    private Vector3 lastTouchedPoint;
    private Vector3 lastTouchedHandleDir;
    private HashSet<int> section = new HashSet<int>();
    private List<int> sectionorder = new List<int>();
    private Vector3 anchor;
    private Quaternion currRotation;
    private Vector3 currScale;
    private Vector3[] cumulativepoints;

    private LineRenderer[] lines;
    private Vector3[][] linepoints;
    private GameObject feedbackmesh;
    private MeshFilter feedbackMeshFilter;
    private MeshRenderer feedbackMeshRenderer;
    private GameObject[] handles;
    private Vector3[] handleadditionalpoints;
    private Vector3[] handlepos;
    private Quaternion[] handlerot;

    private Material bluematerial;
    private Material fabbricablematerial;
    private Material unfabbricablematerial;

#if UNITY_EDITOR
    private Vector3 scaling = 0.20f * Vector3.one;
    private Vector3 handlescaling = Vector3.one;
    private Vector3 editorPos = new Vector3(0, 0, 2f);
#else
    private Vector3 scaling = Vector3.one;
    private Vector3 handlescaling = 0.05f * Vector3.one;
    private Vector3 editorPos = new Vector3(0, 0.5f, 5f);
#endif
    private Vector3 inverseScaling;
    
    private bool[] selectedLines = new bool[2];

    private enum Mode { select_e, select_d, split, extrude, deform, scale, rotate, none };
    private Mode mode;
    public int selectedHandle;
    private bool fabbricable;
    public bool onhandle;

    private EditorAction[] editorHistory;
    private int currAction;
    private int bufferlength;
    private int nrecordedactions;
    private int currActionShift;
    private int cumulativedelta;

    void Start ()
    {
        messageSender = GameObject.FindWithTag("Controller").GetComponent<MessageSender>();
        if (messageSender == null)
            throw new System.Exception("Cannot find controller");
        Material meshmaterial = (Material)Resources.Load("Materials/square");

        bluematerial = (Material)Resources.Load("Materials/Blue");

        //transform.position = cameraTransform.TransformPoint(editorPos);
        transform.position = editorPos;

        inverseScaling = new Vector3(1 / scaling.x, 1 / scaling.y, 1 / scaling.z);
        
        Sculpture = new GameObject("Sculpture");
        Sculpture.layer = LayerMask.NameToLayer("Target");
        filter = Sculpture.AddComponent<MeshFilter>();
        screnderer = Sculpture.AddComponent<MeshRenderer>();
        Sculpture.AddComponent<MeshCollider>();

        meshManager = new MeshManager();
        Mesh mesh = meshManager.BuildGeometryBlock(1, 1, 0.25f);
        filter.mesh = mesh;
        filter.sharedMesh = mesh;
        Sculpture.GetComponent<MeshCollider>().sharedMesh = mesh;

        Sculpture.transform.parent = transform;
        Sculpture.transform.localScale = scaling;
        Sculpture.transform.localPosition = Vector3.zero;
        screnderer.material = meshmaterial;

        lastTouchedFace = -1;

        lines = new LineRenderer[2];
        linepoints = new Vector3[2][];
        
        for (int i = 0; i < 2; i++)
        {
            GameObject splitlineobject = new GameObject("line" + i);
            splitlineobject.transform.parent = Sculpture.transform;
            splitlineobject.transform.localPosition = Vector3.zero;
            splitlineobject.transform.localScale = Vector3.one;
            lines[i] = splitlineobject.AddComponent<LineRenderer>();
            lines[i].useWorldSpace = false;
            lines[i].material = bluematerial;
            lines[i].startColor = Color.blue;
            lines[i].endColor = Color.blue;
            lines[i].enabled = false;
            lines[i].widthMultiplier = 0.005f;
        }

        feedbackmesh = new GameObject("FeedbackMesh");
        feedbackmesh.layer = LayerMask.NameToLayer("Target");
        feedbackMeshFilter = feedbackmesh.AddComponent<MeshFilter>();
        feedbackMeshRenderer = feedbackmesh.AddComponent<MeshRenderer>();
        feedbackmesh.transform.parent = Sculpture.transform;
        feedbackmesh.transform.localPosition = Vector3.zero;
        feedbackmesh.transform.localScale = Vector3.one;
        feedbackmesh.SetActive(false);
        

        bufferlength = 20;
        editorHistory = new EditorAction[bufferlength];
        currAction = 0;
        nrecordedactions = 0;
        currActionShift = 0;
        fabbricablematerial = (Material)Resources.Load("Materials/TransparentGreen");
        unfabbricablematerial = (Material)Resources.Load("Materials/TransparentRed");
        
        mode = Mode.none;
    }

    public Vector3 GetPosition()
    {
        return editorPos;
    }

    public float GetHeight()
    {
        return editorPos.y;
    }

    public Quaternion GetRotation()
    {
        return Sculpture.transform.rotation;
    }

    public float GetRadius()
    {
        Vector3 extents = filter.sharedMesh.bounds.extents;
        float scRadius;

        if (extents.x >= extents.y && extents.x >= extents.z)
            scRadius = extents.x * scaling.x;
        else if (extents.y >= extents.x && extents.y >= extents.z)
            scRadius = extents.y * scaling.y;
        else
            scRadius = extents.z * scaling.z;

        return 0.05f + scRadius;
    }

    public Vector3 GetCenter()
    {
        return filter.sharedMesh.bounds.center;
    }

    public bool GetInteractionPoint(Vector3 handpos, Vector3 dir, out Vector3 interactionPoint)
    {
        return GetInteractionPoint(handpos, dir, Mathf.Infinity, out interactionPoint);
    }
    
    public bool GetInteractionPoint(Vector3 handpos, Vector3 dir, float maxdist, out Vector3 interactionPoint)
    {
        RaycastHit hit;
        int prevTouchedFace = lastTouchedFace;
        Vector3 prevtouchedpoint = lastTouchedPoint;
        bool result;
        
        onhandle = false;
        if ((mode == Mode.select_e || mode == Mode.select_d) && Physics.Raycast(handpos, dir, out hit, maxdist, LayerMask.GetMask("Handle")))
        {
            interactionPoint = hit.point - 0.03f * dir;
            lastTouchedPoint = hit.point;

            for(int i=0; i<handles.Length; i++)
            {
                if (handles[i].name == hit.transform.parent.name)
                {
                    selectedHandle = i;
                    lastTouchedHandleDir = handles[selectedHandle].transform.forward;
                }
            }
            onhandle = true;
            result = true;
        }
        else if (Physics.Raycast(handpos, dir, out hit, maxdist, LayerMask.GetMask("Target")))
        {
            int triangleind = hit.triangleIndex;
            interactionPoint = hit.point - 0.03f * dir;

            lastTouchedFace = meshManager.FindFace(triangleind, filter);
            lastTouchedPoint = hit.point;
            result = true;
        }
        //Else return a position at fixed distance from the camera
        else
        {
            interactionPoint = handpos + (Vector3.Distance(handpos, GetPosition()) - GetRadius()) * dir;
            lastTouchedFace = -1;
            result = false;
        }
        
        if (mode == Mode.split && section.Count > 0 && (prevTouchedFace != lastTouchedFace || Vector3.SqrMagnitude(prevtouchedpoint - lastTouchedPoint) > 0.000001 * GetRadius()))
            UpdateSplitPreview();
        else if (mode == Mode.extrude && section.Count > 0)
            UpdateExtrusionPreview(handpos, lastTouchedHandleDir);
        else if (mode == Mode.deform && section.Count > 0)
            UpdateMoveEdgePreview(handpos, lastTouchedHandleDir);
        else if (mode == Mode.scale)
            UpdateScale(handpos, dir);
        else if (mode == Mode.rotate)
            UpdateRotation(handpos);

        return result;
    }

    public bool SelectFace()
    {
        if (lastTouchedFace != -1)
        {
            Vector3[] contour = null, contourmiddlepoints = null, contournorm = null, contourmiddlenorm = null;
            Vector3 sectioncenter = Vector3.zero, sectionnormal = Vector3.zero;
            float sectionMinRadius = 0, sectionMinContourEdgeLength = 0;

            if (section.Count > 0 && section.Contains(lastTouchedFace))
            {
                if (meshManager.GetConnectedSectionRemove(section, sectionorder, lastTouchedFace, out contour, out contournorm, out contourmiddlepoints, out contourmiddlenorm, out sectioncenter, out sectionnormal,
                    out sectionMinRadius, out sectionMinContourEdgeLength))
                {
                    section.Remove(lastTouchedFace);
                    sectionorder.Remove(lastTouchedFace);
                }
            }
            else if (meshManager.GetConnectedSectionAdd(section, lastTouchedFace, out contour, out contournorm, out contourmiddlepoints, out contourmiddlenorm, out sectioncenter, out sectionnormal,
                out sectionMinRadius, out sectionMinContourEdgeLength))
            {
                section.Add(lastTouchedFace);
                sectionorder.Add(lastTouchedFace);
            }
            if (mode == Mode.select_d)
                UpdateSelectionPreview(contour, contourmiddlepoints, contourmiddlenorm, sectionMinContourEdgeLength);
            else if (mode == Mode.select_e)
                UpdateSelectionPreview(contour, contournorm, sectioncenter, sectionnormal, sectionMinRadius);
        }
        return section.Count > 0;
    }

    public bool SelectHandle()
    {
        return onhandle;
    }

    public void ResetSelection()
    {
        section.Clear();
        sectionorder.Clear();
        ResetHandles();
        onhandle = false;
        if (mode == Mode.split)
            StopSplitPreview();
        else if (mode == Mode.select_e || mode == Mode.select_d)
            StopSelectionPreview();
        else if (mode == Mode.extrude)
            StopExtrusionPreview();
        else if (mode == Mode.deform)
            StopMoveEdgePreview();
    }

    public void ResetHandles()
    {
        if (handles != null)
        {
            foreach (GameObject handle in handles)
                Destroy(handle);
            handles = null;
        }
    }

    void Inflate(ref Vector3 point, float inflateamount)
    {
        Vector3 shift = inflateamount * point.normalized;
        point = point + shift;
    }

    void Inflate(ref Vector3[] points, float inflateamount)
    {
        for (int i = 0; i < points.Length; i++)
        {
            Inflate(ref points[i], inflateamount);
        }
    }
    void ScaleRotateAndTraslate(ref Vector3 point, Vector3 originshift, Quaternion originrotation, Vector3 scale)
    {
        Vector3 p = Vector3.Scale(point, scale);
        point = originrotation * (originshift + p);
    }

    void ScaleRotateAndTraslate(ref Vector3[] points, Vector3 originshift, Quaternion originrotation, Vector3 scale)
    {
        for (int i = 0; i < points.Length; i++)
        {
            ScaleRotateAndTraslate(ref points[i], originshift, originrotation, scale);
        }
    }

    public void StartSplitPreview()
    {
        if (section.Count > 0 && lastTouchedFace != -1)
        {
            mode = Mode.split;
            meshManager.GetRings(section.First(), out linepoints[0], out linepoints[1]);

            for (int i = 0; i < 2; i++)
            {
                int npoints = linepoints[i].Length;
                Vector3[] linepointsextended = new Vector3[2 * npoints - 2];
                Vector3[] inflatedpoints = new Vector3[npoints];
                for (int j = 0; j < npoints - 1; j++)
                {
                    linepointsextended[2 * j] = linepoints[i][j];
                    linepointsextended[2 * j + 1] = (linepoints[i][j] + linepoints[i][(j + 1) % npoints]) / 2;
                    inflatedpoints[j] = linepoints[i][j];
                }
                inflatedpoints[npoints-1] = linepoints[i][npoints-1];
                linepointsextended[2 * npoints - 3] = (linepoints[i][npoints - 2] + linepoints[i][0]) / 2;
                ScaleRotateAndTraslate(ref linepointsextended, GetPosition(), GetRotation(), Vector3.one);
                Inflate(ref inflatedpoints, 0.005f);
                linepoints[i] = linepointsextended;

                lines[i].positionCount = inflatedpoints.Length;
                lines[i].SetPositions(inflatedpoints);
                lines[i].enabled = false;
            }
        }
        else
            StopSplitPreview();
    }

    public void StopSplitPreview()
    {
        mode = Mode.none;
        for (int i = 0; i < 2; i++)
            lines[i].positionCount = 0;
    }

    void UpdateSplitPreview()
    {
        Vector3 A = lastTouchedPoint;
        float dist, mindist = 100;
        
        for (int i = 0; i < 2; i++)
        {
            int npoints;
            
            npoints = linepoints[i].Length;

            for (int j = 0; j < npoints; j++)
            {
                //Triangle ABC (BC is the segment of the line), height AH - B => linepoints[j], C => linepoints[(j + 1) % npoints]; find BH length with dot product, find AH 
                Vector3 B = linepoints[i][j], C = linepoints[i][(j + 1) % npoints], BCDIR = (B - C).normalized;
                float BHLength = Mathf.Abs(Vector3.Dot(B - A, (B - C))), delta = 0.025f * Vector3.SqrMagnitude(B - C);
                Vector3 H = B + BHLength * BCDIR;

                dist = Vector3.SqrMagnitude(A - H);

                if (dist == mindist || (dist > mindist - delta && dist < mindist + delta))
                {
                    selectedLines[0] = true;
                    selectedLines[1] = true;
                }
                else if (dist < mindist)
                {
                    selectedLines[i] = true;
                    selectedLines[(i + 1) % 2] = false;
                    mindist = dist;
                }
            }
        }

        lines[0].enabled = selectedLines[0];
        lines[1].enabled = selectedLines[1];
    }
    
    public void Split()
    {
        EditorAction[] editorActions;
        Mesh mesh = meshManager.Split(selectedLines, out editorActions);

        if (mesh != null)
        {
            UpdateBuffer(editorActions);
            filter.mesh = mesh;
            Sculpture.GetComponent<MeshFilter>().sharedMesh = mesh;
            Sculpture.GetComponent<MeshCollider>().sharedMesh = mesh;
        }
    }

    public void StartSelectionPreview(int selecttype)
    {
        if (selecttype == 0)
            mode = Mode.select_e;
        else
            mode = Mode.select_d;
    }

    void UpdateHandles(Vector3[] refpositions, Vector3[] handlepositions, Vector3[] handlenormals, float handleradius)
    {
        int nhandles = handlepositions.Count();
        handlepos = new Vector3[nhandles];
        handlerot = new Quaternion[nhandles];
        if (handles == null || handles.Length != nhandles)
        {
            ResetHandles();
            handles = new GameObject[nhandles];

            GameObject prefab = (GameObject)Resources.Load("Prefab/Handle");

            handles = new GameObject[nhandles];
            for (int i = 0; i < handles.Length; i++)
            {
                handles[i] = (GameObject)Instantiate(prefab);
                handles[i].transform.parent = Sculpture.transform;
                handles[i].transform.name = "handle" + i;
                handles[i].transform.localPosition = handlepositions[i];
                handles[i].transform.localRotation = Quaternion.LookRotation(handlenormals[i]);
                handles[i].transform.localScale = handlescaling * 10 * handleradius;
                handles[i].layer = LayerMask.NameToLayer("Handle");
                handles[i].SetActive(true);
                handlepos[i] = handles[i].transform.position;
                handlerot[i] = handles[i].transform.localRotation;
            }
        }
        else
        {
            for (int i = 0; i < handles.Length; i++)
            {
                handles[i].transform.localPosition = handlepositions[i];
                handles[i].transform.localRotation = Quaternion.LookRotation(handlenormals[i]);
                handles[i].transform.localScale = handlescaling * 10 * handleradius;
                handlepos[i] = handles[i].transform.position;
                handlerot[i] = handles[i].transform.localRotation;
            }
        }
        handleadditionalpoints = refpositions;
    }

    void UpdateSelectionPreview(Vector3[] contour, Vector3[] contourmiddlepoints, Vector3[] contourmiddlenorm, float sectionMinContourEdgeLength)
    {
        if (section.Count > 0)
        {
            if (contour != null)
            {
                int npoints = contour.Length;
                Vector3[] inflatedpoints = new Vector3[npoints + 1];
                for (int j = 0; j < npoints; j++)
                {
                    inflatedpoints[j] = contour[j];
                }
                inflatedpoints[npoints] = contour[0];
                Inflate(ref inflatedpoints, 0.005f);//transform.position, scaling);

                lines[0].positionCount = inflatedpoints.Length;
                lines[0].SetPositions(inflatedpoints);
                lines[0].enabled = true;

                UpdateHandles(contour, contourmiddlepoints, contourmiddlenorm, sectionMinContourEdgeLength);
            }
        }
        else
        {
            ResetSelection();
        }
    }

    void UpdateSelectionPreview(Vector3[] contour, Vector3[] contournorm, Vector3 sectionCenter, Vector3 sectionNormal, float sectionMinRadius)
    {
        if (section.Count > 0)
        {
            if (contour != null)
            {
                int npoints = contour.Length;
                Vector3[] inflatedpoints = new Vector3[npoints + 1];
                for (int j = 0; j < npoints; j++)
                {
                    inflatedpoints[j] = contour[j];
                }
                inflatedpoints[npoints] = contour[0];
                Inflate(ref inflatedpoints, 0.005f);//transform.position, scaling);

                lines[0].positionCount = inflatedpoints.Length;
                lines[0].SetPositions(inflatedpoints);
                lines[0].enabled = true;

                Vector3[] refpositions = { sectionCenter };
                Vector3[] handlerotations = { sectionNormal };
                UpdateHandles(refpositions, refpositions, handlerotations, sectionMinRadius);
            }
        }
        else
        {
            ResetSelection();
        }
    }

    public void StopSelectionPreview()
    {
        for (int i = 0; i < 2; i++)
            lines[i].positionCount = 0;
        ResetHandles();
        mode = Mode.none;
    }

    public void StartExtrusionPreview()
    {
        anchor = lastTouchedPoint;
        if (section.Count > 0)
        {
            mode = Mode.extrude;
            feedbackmesh.SetActive(true);
            fabbricable = false;
            cumulativedelta = 0;
        }
        else
            StopExtrusionPreview();
    }

    public void StopExtrusionPreview()
    {
        feedbackmesh.SetActive(false);
        mode = Mode.none;
    }

    void UpdateExtrusionPreview(Vector3 pointerpos, Vector3 pointerdir)
    {
        Vector3 extrusionCenter, dir = (pointerpos - anchor);//handlepos[0]);
        dir.Normalize();
        Vector3 forwardcomponent = Mathf.Abs(Vector3.Dot(dir,pointerdir)) * pointerdir;
        
        cumulativedelta++;
        dir = dir - forwardcomponent;
        dir = dir + 0.01f * cumulativedelta * pointerdir;
        dir = Quaternion.Inverse(Sculpture.transform.localRotation) * dir;
        Debug.Log(dir);
        Matrix4x4 extrusion = Matrix4x4.Translate(dir);
        //Matrix4x4 extrusion = Matrix4x4.Translate(cumulativedelta * 0.1f * (1 / scaling.x) * Time.deltaTime * dir);
        Mesh extrusionMesh;
        int extrusionResult;

        extrusionResult = meshManager.GetExtrusion(section.ToArray(), extrusion, out extrusionMesh, out extrusionCenter);
        feedbackMeshFilter.sharedMesh = extrusionMesh;

        if (extrusionResult != 2)
        {
            if (extrusionResult == 0)
            {
                feedbackMeshRenderer.material = fabbricablematerial;
                fabbricable = true;
            }
            else
            {
                feedbackMeshRenderer.material = unfabbricablematerial;
                fabbricable = false;
            }

            ScaleRotateAndTraslate(ref extrusionCenter, GetPosition(), GetRotation(), scaling);
            handlepos[0] = extrusionCenter;
        }
    }

    public void Extrude()
    {
        if (fabbricable)
        {
            EditorAction editorAction;
            Mesh mesh = meshManager.Extrude(out editorAction);

            if (mesh != null)
            {
                UpdateBuffer(editorAction);
                
                Sculpture.GetComponent<MeshFilter>().sharedMesh = mesh;
                Sculpture.GetComponent<MeshCollider>().sharedMesh = mesh;
            }
        }
    }

    public void StartMoveEdgePreview()
    {
        anchor = Vector3.zero;
        if (section.Count > 0)
        {
            mode = Mode.deform;
            feedbackmesh.SetActive(true);
            fabbricable = false;
            cumulativedelta = 0;
        }
        else
            StopMoveEdgePreview();
    }

    public void StopMoveEdgePreview()
    {
        feedbackmesh.SetActive(false);
        mode = Mode.none;
    }

    public void UpdateMoveEdgePreview(Vector3 pointerpos, Vector3 pointerdir)
    {
        if (Mathf.Abs(anchor.magnitude) < 0.001f)
            anchor = pointerpos;
        else
        {
            int nhandles = handlepos.Length;
            Vector3 deformationcenter, dir = (pointerpos - anchor), deformedv1, deformedv2;
            Vector3 parallelcomponent = Vector3.Dot(dir, pointerdir) * pointerdir;
            Vector3 forwardcomponent = Mathf.Abs(Vector3.Dot(dir, pointerdir)) * pointerdir;
            Mesh deformedMesh;

            cumulativedelta++;
            //dir = dir - parallelcomponent - forwardcomponent;
            //dir = 0.0001f * cumulativedelta * pointerdir;
            dir = dir.normalized * 0.0001f * cumulativedelta;
            dir = Quaternion.Inverse(Sculpture.transform.localRotation) * dir;
            
            int deformResult = meshManager.GetDeformation(handleadditionalpoints[selectedHandle], handleadditionalpoints[(selectedHandle + 1) % nhandles], dir, out deformedMesh, out deformationcenter, out deformedv1, out deformedv2);

            feedbackMeshFilter.sharedMesh = deformedMesh;

            if (deformResult != 2)
            {
                if (deformResult == 0)
                {
                    feedbackMeshRenderer.material = fabbricablematerial;
                    fabbricable = true;
                }
                else
                {
                    feedbackMeshRenderer.material = unfabbricablematerial;
                    fabbricable = false;
                }

                ScaleRotateAndTraslate(ref deformationcenter, GetPosition(), GetRotation(), scaling);
                handlepos[selectedHandle] = deformationcenter;
                handleadditionalpoints[selectedHandle] = deformedv1;
                handleadditionalpoints[(selectedHandle + 1) % nhandles] = deformedv2;
            }
        }
    }

    public void MoveEdge()
    {
        if (fabbricable)
        {
            EditorAction editorAction;
            Mesh mesh = meshManager.Deform(out editorAction);

            if (mesh != null)
            {
                UpdateBuffer(editorAction);
                
                Sculpture.GetComponent<MeshFilter>().sharedMesh = mesh;
                Sculpture.GetComponent<MeshCollider>().sharedMesh = mesh;
            }
        }
    }

    public void StartScaling()
    {
        anchor = lastTouchedPoint;
        currScale = Sculpture.transform.localScale;
        mode = Mode.scale;
    }

    public void StopScaling()
    {
        mode = Mode.none;
    }
    
    void UpdateScale(Vector3 handpos, Vector3 dir)
    {
        Vector3 refpos = anchor - Vector3.Dot(anchor,dir) * dir;
        float motion = (lastTouchedPoint - refpos).x;
        Vector3 newscale = 0.1f * motion * Vector3.one + currScale;
        
        if (newscale.x <= 2 * scaling.x && newscale.x >= 0.5f * scaling.x)
        {
            Sculpture.transform.localScale = newscale;
            currScale = newscale;
        }
    }

    public void StartRotation()
    {
        anchor = Vector3.zero;
        currRotation = Sculpture.transform.localRotation;
        mode = Mode.rotate;
        cumulativepoints = new Vector3[10];
    }

    public void StopRotation()
    {
        mode = Mode.none;
    }

    void UpdateRotation(Vector3 refpos)
    {
        if (Mathf.Abs(anchor.z) < 0.001)
            anchor = refpos;
        else
        {
            //If user touch close to the perimeter, rotate by 180 degrees from the start rotation
            Vector3 anchordir = GetPosition() - anchor, dir = GetPosition() - refpos, diff = anchordir.normalized - dir.normalized;

            if (Mathf.Abs(diff.x) < Mathf.Abs(diff.y))
            {
                float angle = Mathf.Abs(diff.y * 1000) > 180 ? Mathf.Sign(diff.y) * 180 : 1000 * diff.y;
                Sculpture.transform.localRotation = currRotation;
                Sculpture.transform.Rotate(Vector3.right, angle, Space.World);
            }
            else if (Mathf.Abs(diff.x) > Mathf.Abs(diff.y))
            {
                float angle = Mathf.Abs(diff.x * 1000) > 180 ? Mathf.Sign(diff.x) * 180 : -1000 * diff.x;
                Sculpture.transform.localRotation = currRotation;
                Sculpture.transform.Rotate(Vector3.up, angle, Space.World);
            }
        }
    }

    void UpdateBuffer(EditorAction editorAction)
    {
        if (currActionShift != 0)
        {
            currActionShift = 0;
            currAction = 0;
            nrecordedactions = 0;
        }
        editorHistory[currAction] = editorAction;
        if (nrecordedactions < bufferlength)
            nrecordedactions++;
        Debug.Log("new action " + currAction + " curr action shift " + currActionShift + " n recorded actions are " + nrecordedactions);
        currAction = (currAction + 1) % bufferlength;
    }

    void UpdateBuffer(EditorAction[] editorActions)
    {
        int nactions = editorActions.Length;

        for (int i = 0; i < nactions; i++)
        {
            UpdateBuffer(editorActions[i]);
        }
    }

    public void Undo()
    {
        int previousinbuffer = (currAction + bufferlength - 1) % bufferlength;
        if ((nrecordedactions == bufferlength && (currActionShift <= 0 && Mathf.Abs(currActionShift) < bufferlength - 1)) || (nrecordedactions < bufferlength && previousinbuffer < nrecordedactions))
        {
            EditorAction lastAction;

            Debug.Log("Undo " + currAction + " current is now " + previousinbuffer + " curr action shift " + currActionShift + " n recorded actions are " + nrecordedactions);
            currAction = previousinbuffer;
            lastAction = editorHistory[currAction];

            meshManager.Undo(lastAction);

            Mesh mesh = meshManager.GetMesh();
            
            filter.sharedMesh = mesh;
            Sculpture.GetComponent<MeshCollider>().sharedMesh = mesh;
            currActionShift--;
        }
        else
            Debug.Log("There are not other actions to undo: curraction is " + currAction + " curr action shift " + currActionShift + " n recorded actions are " + nrecordedactions);
    }

    public void Redo()
    {
        int nextinbuffer = (currAction + 1) % bufferlength;
        if ((nrecordedactions == bufferlength && (currActionShift < 0)) || (nrecordedactions < bufferlength && currAction < nrecordedactions))
        {
            EditorAction lastAction;

            Debug.Log("Redo " + currAction + " current is now " + nextinbuffer + " curr action shift " + currActionShift + " n recorded actions are " + nrecordedactions);

            lastAction = editorHistory[currAction];
            currAction = nextinbuffer;

            meshManager.Redo(lastAction);
            
            Mesh mesh = meshManager.GetMesh();
            
            filter.sharedMesh = mesh;
            Sculpture.GetComponent<MeshCollider>().sharedMesh = mesh;
            currActionShift++;
        }
        else
            Debug.Log("There are not other actions to redo: curraction is " + currAction + " curr action shift " + currActionShift + " n recorded actions are " + nrecordedactions);
    }

    public void SaveMesh()
    {
        string export = meshManager.ExportMesh();
        messageSender.SendPacket(export);
    }
}
