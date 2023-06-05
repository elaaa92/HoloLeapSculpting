// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.WSA.Input;
using Random = System.Random;

public class CalibrationManager : MonoBehaviour
{
    /// <summary>
    /// HandDetected tracks the hand detected state.
    /// Returns true if the list of tracked hands is not empty.
    /// </summary>
    /// 
    //Transform matrix contains also conversion from mm to m
    private LeapProvider leapProvider;
    private Matrix4x4 originTransform;
    private List<Vector3> holoBasePoints = new List<Vector3>();
    private List<Vector3> leapBasePoints = new List<Vector3>();
    private const int npointsmin = 200;

    private Vector3 cameraStartPosition;
    private Quaternion cameraStartRotation;
#if UNITY_EDITOR
    private GameObject fakecamera;
#endif
    public bool IsCalibrated { get; internal set; }
    public Vector3 currentPos { get; internal set; }
    public bool ManualCalibration;

#if !UNITY_EDITOR
    public Vector3 LeapShift = new Vector3(-0.00702f, 0.0849f, 0.18739f);
    //public Vector3 LeapShift = new Vector3(0, 0, 0.18739f);
    public Vector3 LeapRot = new Vector3(0,0,0);
#else
    public Vector3 LeapShift = Vector3.zero;
    public Vector3 LeapRot = Vector3.zero;
#endif

    void Start()
    {
#if !UNITY_EDITOR
        //LeapShift.y = 0.5f;
#endif
        leapProvider = GetComponent<LeapProvider>();
        //updateAllPoints();
        //UpdateOriginTransform();
        cameraStartPosition = Camera.main.transform.position;  //Vector3.zero;
        cameraStartRotation = Camera.main.transform.rotation;  //Quaternion.identity;

        string savePath = Application.persistentDataPath + "\\transformdata.txt";

        if (ManualCalibration)
        {
            originTransform = Matrix4x4.Translate(LeapShift) * Matrix4x4.Rotate(Quaternion.Euler(LeapRot));

            IsCalibrated = true;
        }
        else if (File.Exists(savePath))
        {
            using (StreamReader streamReader = File.OpenText(savePath))
            {
                string jsonString = streamReader.ReadToEnd();
                originTransform = JsonUtility.FromJson<Matrix4x4>(jsonString);
                Debug.Log(originTransform);
            }
            IsCalibrated = true;
        }
        else
        {
            StartCalibration();
        }

    }

    public void StartCalibration()
    {
        Debug.Log("Calibration start");
        IsCalibrated = false;
        InteractionManager.InteractionSourceDetected += InteractionManager_InteractionSourceDetected;
        InteractionManager.InteractionSourceUpdated += InteractionManager_InteractionSourceUpdated;
        InteractionManager.InteractionSourceLost += InteractionManager_InteractionSourceLost;
    }

    private void StopHololensHandTracking()
    {
        InteractionManager.InteractionSourceDetected -= InteractionManager_InteractionSourceDetected;
        InteractionManager.InteractionSourceUpdated -= InteractionManager_InteractionSourceUpdated;
        InteractionManager.InteractionSourceLost -= InteractionManager_InteractionSourceLost;
    }

    private void InteractionManager_InteractionSourceDetected(InteractionSourceDetectedEventArgs args)
    {
        uint id = args.state.source.id;
        // Check to see that the source is a hand.
        if (args.state.source.kind != InteractionSourceKind.Hand)
        {
            return;
        }

        Vector3 pos;

        if (args.state.sourcePose.TryGetPosition(out pos))
        {
            //Debug.Log("Pos of holopoint: " + pos);
            //Update the base.
            currentPos = pos;
            if (!IsCalibrated)
            {
                UpdateBase(pos);
            }
        }
    }

    private void InteractionManager_InteractionSourceUpdated(InteractionSourceUpdatedEventArgs args)
    {
        uint id = args.state.source.id;
        Vector3 pos;
        if (args.state.source.kind == InteractionSourceKind.Hand)
        {
            if (args.state.sourcePose.TryGetPosition(out pos))
            {
                //Debug.Log("Pos of holopoint: " + pos);
                //Update the base.
                currentPos = pos;
                if (!IsCalibrated)
                {
                    UpdateBase(pos);
                }
            }
        }
    }

    private void InteractionManager_InteractionSourceLost(InteractionSourceLostEventArgs args)
    {
        uint id = args.state.source.id;
        // Check to see that the source is a hand.
        if (args.state.source.kind != InteractionSourceKind.Hand)
        {
            return;
        }
    }

    void OnDestroy()
    {
        StopHololensHandTracking();
    }

    //Return true if base is complete
    public void UpdateBase(Vector3 holoPoint)
    {
        LeapFrame frame = leapProvider.GetCurrentFrame();
        float velocityThresh = 0.015f;

        //Debug.Log("Holo hand: " + holoPoint.x + "," + holoPoint.y + "," + holoPoint.z);
        LeapHand[] hands = frame.hands;
        //List<LeapFrame.Hand> hands = lastFrame.GetHands().FindAll(x => x.IsPointing);
        if (hands.Count() > 0)
        {
            LeapHand leapHand = hands.First();
            //If hand is pointing and the base is not complete
            //check if leapPoint is enough distant from the others leap base points 
            if (leapHand.PalmVelocityMagnitude < velocityThresh && leapBasePoints.Count < npointsmin)
            {
                //Leap point relative to camera system reference
                Vector3 leapPoint = CameraTransform(leapHand.PalmPosition);
                //Add the two correspondent point to the base
                //holoBasePoints.Add(new Vector3 (holoPoint.x, holoPoint.y+0.1f, holoPoint.z+0.1f));
                holoBasePoints.Add(holoPoint);
                leapBasePoints.Add(leapPoint);
                
                //Debug.Log("Pos of holopoint: " + holoPoint.ToString("F4") + " Pos of leappoint: " + leapPoint.ToString("F4") + " Npoints: " + leapBasePoints.Count);
                //If the base is changed and it is complete, change origin transform

                bool flag = leapBasePoints.Count == npointsmin;
                if (flag)
                    UpdateOriginTransform();
            }
        }
    }

    public void UpdateOriginTransform()
    {
        float[] dist = new float[4];
        Matrix4x4[] ms = new Matrix4x4[4];

        ms[0] = FitTraslate(false);
        dist[0] = EvalFit(ms[0], "Traslate");
        ms[1] = FitTraslate(true);
        dist[1] = EvalFit(ms[1], "Traslate with resize");
        ms[2] = FitUmeyama();
        dist[2] = EvalFit(ms[2], "Umeyama");
        ms[3] = FitLs();
        dist[3] = EvalFit(ms[3], "Generic");

        originTransform = ms[dist.ToList().IndexOf(dist.Min())];

        //Write transform matrix for future execution
        String data = JsonUtility.ToJson(originTransform);
        using (StreamWriter streamWriter = File.CreateText(Application.persistentDataPath + "\\transformdata.txt"))
            streamWriter.Write(data);

        IsCalibrated = true;

        StopHololensHandTracking();
    }

    public float EvalFit(Matrix4x4 trans, string title)
    {
        int npoints = holoBasePoints.Count();
        
        float[] dists_x = new float[npoints];
        float[] dists_y = new float[npoints];
        float[] dists_z = new float[npoints];
        Vector3[] transformedPoints = new Vector3[npoints];
        for (int i = 0; i < npoints; i++)
        {
            transformedPoints[i] = trans.MultiplyPoint(leapBasePoints[i]);
            dists_x[i] = Mathf.Abs(holoBasePoints[i].x - transformedPoints[i].x);
            dists_y[i] = Mathf.Abs(holoBasePoints[i].y - transformedPoints[i].y);
            dists_z[i] = Mathf.Abs(holoBasePoints[i].z - transformedPoints[i].z);
        }
        Debug.Log(title + ": \n" + trans + 
            "\nAvg dist x: " + dists_x.Average() + ", Avg dist y: " + dists_y.Average()+
            ", Avg dist z: " + dists_z.Average());

        return (dists_x.Average() + dists_y.Average() + dists_z.Average()) / 3;
    }

    public Matrix4x4 FitTraslate(bool resize)
    {
        int npoints = holoBasePoints.Count();
        float[,] tM = { { 0f, 0f, 0f, 0f }, { 0f, 0f, 0f, 0f }, { 0f, 0f, 0f, 0f }, { 0f, 0f, 0f, 1f } };

        
        tM[0, 0] = 1;
        tM[1, 1] = 1;
        tM[2, 2] = 1;
        if(resize)
        {
            //float [] q0 = new float[npoints];
            //float[] q1 = new float[npoints];
            float[] q2 = new float[npoints];
            for (int i = 0; i < npoints; i++)
            {
                //q0[i] = holoBasePoints[i].x / leapBasePoints[i].x;
                //q1[i] = holoBasePoints[i].y / leapBasePoints[i].y;
                q2[i] = holoBasePoints[i].z / leapBasePoints[i].z;
            }
            //tM[0, 0] = q0.Min(x => Math.Abs(x));
            //tM[1, 1] = q1.Min(x => Math.Abs(x));
            tM[2, 2] = q2.Min(x => Math.Abs(x));
        }
        for (int i = 0; i < npoints; i++)
        {
            tM[0, 3] += (holoBasePoints[i].x - tM[0,0] * leapBasePoints[i].x);
            tM[1, 3] += (holoBasePoints[i].y - tM[1,1] * leapBasePoints[i].y);
            tM[2, 3] += (holoBasePoints[i].z - tM[2,2] * leapBasePoints[i].z);
        }
        tM[0, 3] /= npoints;
        tM[1, 3] /= npoints;
        tM[2, 3] /= npoints;
        tM[3, 3] = 1;

        return AffineTransformConverter.Convert(tM);
    }

    public Matrix4x4 FitUmeyama()
    {
        int ncoords = 3, npoints = holoBasePoints.Count(), msize = 4;
        float[,] tM = { { 0f, 0f, 0f, 0f }, { 0f, 0f, 0f, 0f }, { 0f, 0f, 0f, 0f }, { 0f, 0f, 0f, 1f } };
        float[] H = new float[ncoords * npoints], L = new float[ncoords * npoints], m = new float[msize * msize];

        for (int i = 0; i < npoints; i++)
        {
            H[3 * i] = holoBasePoints[i].x;
            H[3 * i + 1] = holoBasePoints[i].y;
            H[3 * i + 2] = -holoBasePoints[i].z;
        }
        m = EigenInterface.Umeyama(H, L, false);

        for (int j = 0; j < msize; j++)
            for (int i = 0; i < msize; i++)
                tM[i, j] = m[i * 4 + j];

        return AffineTransformConverter.Convert(tM);
    }

    public Matrix4x4 FitLs()
    {
        int npoints = holoBasePoints.Count();
        List<float> A = new List<float>();
        List<float> b = new List<float>();
        float[] fill = { 0f, 0f, 0f , 0f};
        List<float> fillVector = fill.ToList<float>();
        List<float> hc = new List<float>();
        List<float> lc = new List<float>();

        for (int i = 0; i < npoints; i++)
        {
            float[] l = { holoBasePoints[i].x, holoBasePoints[i].y, holoBasePoints[i].z, 1 };
            float[] h = { leapBasePoints[i].x, leapBasePoints[i].y, -leapBasePoints[i].z };
            hc = h.ToList<float>();
            lc = l.ToList<float>();
            A = A.Concat(lc).Concat(fillVector).Concat(fillVector)
            .Concat(fillVector).Concat(lc).Concat(fillVector)
            .Concat(fillVector).Concat(fillVector).Concat(lc).ToList<float>();
            b = b.Concat(hc).ToList<float>();
        }

        float[] res = EigenInterface.SolveSystem(A.ToArray(), b.ToArray());

        float[,] tM = { { 0f, 0f, 0f, 0f }, { 0f, 0f, 0f, 0f }, { 0f, 0f, 0f, 0f }, { 0f, 0f, 0f, 1f } };
        for (int i = 0; i < 3; i++)
        {
            for(int j = 0; j < 3; j++)
                tM[i, j] = res[3 * i + j];
        }
        
        /*
        Matrix4x4 m = AffineTransformConverter.Convert(tM);
        float[] dists_x = new float[npoints];
        float[] dists_y = new float[npoints];
        float[] dists_z = new float[npoints];
        Vector3[] transformedPoints = new Vector3[npoints];
        for (int i = 0; i < npoints; i++)
        {
            transformedPoints[i] = m.MultiplyPoint3x4(leapBasePoints[i]);
            dists_x[i] = Mathf.Abs(holoBasePoints[i].x - transformedPoints[i].x);
            dists_y[i] = Mathf.Abs(holoBasePoints[i].y - transformedPoints[i].y);
            dists_z[i] = Mathf.Abs(holoBasePoints[i].z - transformedPoints[i].z);
        }
        tM[0, 3] = dists_x.Average();
        tM[1, 3] = dists_y.Average();
        tM[2, 3] = dists_z.Average();
        */

        return AffineTransformConverter.Convert(tM);
    }

    public Matrix4x4 GetOriginTransform()
    {
        return originTransform;
    }

    public Matrix4x4 GetProjectiveMatrix(float targetheight)
    {
        #if UNITY_EDITOR
                float scaleMultiplierx = 1000, scaleMultipliery = 1000;
                //A adjust parameter that approximates the distance between camera (head) and hand
        #else
                float scaleMultiplierx = 4000, scaleMultipliery = 4000;
        #endif

        Vector3 leapPos = (Vector3)originTransform.GetColumn(3);
        return GetProjectiveMatrix(targetheight, scaleMultiplierx, scaleMultipliery, leapPos);
    }

    public Matrix4x4 GetProjectiveMatrix(float targetheight, float scaleMultiplierx, float scaleMultipliery, Vector3 originPos)
    {
        Vector3 cameraPos = GetCameraPosition(), right = GetCameraRight(), up = GetCameraUp(), forward = GetCameraForward();
        Vector3 origintocamshift, scale = Vector3.one, shift = Vector3.zero, targettocamshift;
        Quaternion cameraRotation = GetCameraRotation();
        
        scale = new Vector3(Mathf.Clamp(right.x * scaleMultiplierx + up.x * scaleMultipliery, 0.001f, scaleMultiplierx),
                            Mathf.Clamp(right.y * scaleMultiplierx + up.y * scaleMultipliery, 0.001f, scaleMultiplierx),
                            Mathf.Clamp(right.z * scaleMultiplierx + up.z * scaleMultipliery, 0.001f, scaleMultiplierx));
        origintocamshift = originPos - cameraPos;
        targettocamshift = targetheight * Vector3.up - cameraPos;
                               
        
        Matrix4x4 projectiveMatrix = Matrix4x4.Translate(origintocamshift + targettocamshift) * Matrix4x4.Rotate(cameraRotation) * Matrix4x4.Scale(scale);
        return projectiveMatrix;
    }

    public Vector3 GetAxisAlignedCameraForward()
    {
        Vector3 dir = GetCameraForward();

        dir = dir.x >= dir.y && dir.x >= dir.z ? dir.x * Vector3.right : (dir.y >= dir.x && dir.y >= dir.z ? dir.y * Vector3.up : dir.z * Vector3.forward);
        return dir.normalized;
    }
    public Vector3 GetAxisAlignedCameraUpward()
    {
        Vector3 dir = GetCameraUp();

        dir = dir.x >= dir.y && dir.x >= dir.z ? dir.x * Vector3.right : (dir.y >= dir.x && dir.y >= dir.z ? dir.y * Vector3.up : dir.z * Vector3.forward);
        return dir.normalized;
    }

    public Transform GetCameraTransform()
    {
        return Camera.main.transform;
    }

    public Vector3 GetCameraForward()
    {
        return Camera.main.transform.forward;
    }
    public Vector3 GetCameraUp()
    {
        return Camera.main.transform.up;
    }
    public Vector3 GetCameraRight()
    {
        return Camera.main.transform.right;
    }

    public Vector3 GetCameraPosition()
    {
        return GetCameraTraslate();
    }

    public Vector3 GetCameraTraslate()
    {
        return cameraStartPosition - Camera.main.transform.position;
    }

    public Quaternion GetCameraRotation()
    {
        return Quaternion.RotateTowards(cameraStartRotation, Camera.main.transform.rotation, 360);
    }

    public Vector3 CameraTransform(Vector3 vector)
    {
        return (GetCameraRotation() * (vector + GetCameraTraslate()));
    }

    void updateAllPoints()
    {
        holoBasePoints.Add(new Vector3(0.0046f, 0.0709f, 0.3806f));
        leapBasePoints.Add(new Vector3(-0.0846f, 0.1191f, 0.1823f));
        holoBasePoints.Add(new Vector3(0.0094f, 0.0990f, 0.3916f));
        leapBasePoints.Add(new Vector3(-0.1103f, 0.1192f, 0.1602f));
        holoBasePoints.Add(new Vector3(0.0129f, 0.0989f, 0.3937f));
        leapBasePoints.Add(new Vector3(-0.1104f, 0.1190f, 0.1605f));
        holoBasePoints.Add(new Vector3(0.0140f, 0.0991f, 0.3942f));
        leapBasePoints.Add(new Vector3(-0.1105f, 0.1188f, 0.1606f));
        holoBasePoints.Add(new Vector3(0.0170f, 0.1002f, 0.3963f));
        leapBasePoints.Add(new Vector3(-0.1130f, 0.1182f, 0.1594f));
        holoBasePoints.Add(new Vector3(0.0989f, 0.1032f, 0.4310f));
        leapBasePoints.Add(new Vector3(-0.1517f, 0.1190f, 0.1567f));
        holoBasePoints.Add(new Vector3(-0.0771f, 0.0476f, 0.3584f));
        leapBasePoints.Add(new Vector3(-0.0596f, 0.1495f, 0.1786f));
        holoBasePoints.Add(new Vector3(-0.0758f, 0.0475f, 0.3597f));
        leapBasePoints.Add(new Vector3(-0.0598f, 0.1499f, 0.1789f));
        holoBasePoints.Add(new Vector3(-0.0751f, 0.0474f, 0.3603f));
        leapBasePoints.Add(new Vector3(-0.0599f, 0.1499f, 0.1790f));
        holoBasePoints.Add(new Vector3(-0.0700f, 0.0457f, 0.3646f));
        leapBasePoints.Add(new Vector3(-0.0646f, 0.1529f, 0.1807f));
        holoBasePoints.Add(new Vector3(0.0759f, 0.0612f, 0.4199f));
        leapBasePoints.Add(new Vector3(-0.1473f, 0.1280f, 0.1581f));
        holoBasePoints.Add(new Vector3(0.1180f, 0.0736f, 0.4352f));
        leapBasePoints.Add(new Vector3(-0.1674f, 0.1265f, 0.1520f));
        holoBasePoints.Add(new Vector3(0.0794f, 0.0431f, 0.4326f));
        leapBasePoints.Add(new Vector3(-0.1479f, 0.1323f, 0.1674f));
        holoBasePoints.Add(new Vector3(0.0775f, 0.0428f, 0.4325f));
        leapBasePoints.Add(new Vector3(-0.1478f, 0.1323f, 0.1674f));
        holoBasePoints.Add(new Vector3(0.0767f, 0.0423f, 0.4324f));
        leapBasePoints.Add(new Vector3(-0.1480f, 0.1327f, 0.1674f));
        holoBasePoints.Add(new Vector3(0.0579f, 0.0459f, 0.4288f));
        leapBasePoints.Add(new Vector3(-0.1506f, 0.1453f, 0.1623f));
        holoBasePoints.Add(new Vector3(-0.0843f, 0.0325f, 0.3589f));
        leapBasePoints.Add(new Vector3(-0.0657f, 0.1614f, 0.1781f));
        holoBasePoints.Add(new Vector3(-0.0869f, 0.0331f, 0.3577f));
        leapBasePoints.Add(new Vector3(-0.0657f, 0.1618f, 0.1782f));
        holoBasePoints.Add(new Vector3(-0.0871f, 0.0332f, 0.3576f));
        leapBasePoints.Add(new Vector3(-0.0657f, 0.1620f, 0.1782f));
        holoBasePoints.Add(new Vector3(-0.0872f, 0.0332f, 0.3576f));
        leapBasePoints.Add(new Vector3(-0.0657f, 0.1621f, 0.1782f));
        holoBasePoints.Add(new Vector3(-0.0933f, 0.0321f, 0.3517f));
        leapBasePoints.Add(new Vector3(-0.0647f, 0.1651f, 0.1796f));
        holoBasePoints.Add(new Vector3(-0.0931f, 0.0318f, 0.3510f));
        leapBasePoints.Add(new Vector3(-0.0648f, 0.1650f, 0.1797f));
        holoBasePoints.Add(new Vector3(-0.0929f, 0.0316f, 0.3507f));
        leapBasePoints.Add(new Vector3(-0.0648f, 0.1650f, 0.1798f));
        holoBasePoints.Add(new Vector3(0.0406f, 0.1004f, 0.4077f));
        leapBasePoints.Add(new Vector3(-0.1274f, 0.1171f, 0.1658f));
        holoBasePoints.Add(new Vector3(-0.1092f, 0.0477f, 0.3375f));
        leapBasePoints.Add(new Vector3(-0.0468f, 0.1595f, 0.1711f));
        holoBasePoints.Add(new Vector3(-0.1014f, 0.0370f, 0.3417f));
        leapBasePoints.Add(new Vector3(-0.0580f, 0.1601f, 0.1783f));
        holoBasePoints.Add(new Vector3(-0.0905f, 0.0203f, 0.3532f));
        leapBasePoints.Add(new Vector3(-0.0650f, 0.1643f, 0.1818f));
        holoBasePoints.Add(new Vector3(-0.0450f, -0.0115f, 0.3809f));
        leapBasePoints.Add(new Vector3(-0.0915f, 0.1751f, 0.1946f));
        holoBasePoints.Add(new Vector3(-0.0124f, -0.0230f, 0.3917f));
        leapBasePoints.Add(new Vector3(-0.1040f, 0.1734f, 0.1887f));
        holoBasePoints.Add(new Vector3(-0.0115f, -0.0237f, 0.3922f));
        leapBasePoints.Add(new Vector3(-0.1043f, 0.1736f, 0.1887f));
        holoBasePoints.Add(new Vector3(0.0011f, -0.0080f, 0.4062f));
        leapBasePoints.Add(new Vector3(-0.1283f, 0.1784f, 0.1860f));
        holoBasePoints.Add(new Vector3(0.0021f, -0.0086f, 0.4065f));
        leapBasePoints.Add(new Vector3(-0.1284f, 0.1784f, 0.1861f));
        holoBasePoints.Add(new Vector3(0.1066f, 0.0120f, 0.4319f));
        leapBasePoints.Add(new Vector3(-0.1910f, 0.1539f, 0.1711f));
        holoBasePoints.Add(new Vector3(-0.0948f, 0.0024f, 0.4856f));
        leapBasePoints.Add(new Vector3(-0.0576f, 0.1599f, 0.1964f));
        holoBasePoints.Add(new Vector3(-0.1069f, 0.0074f, 0.3894f));
        leapBasePoints.Add(new Vector3(-0.0688f, 0.1750f, 0.1938f));
        holoBasePoints.Add(new Vector3(-0.1059f, 0.0052f, 0.3806f));
        leapBasePoints.Add(new Vector3(-0.0634f, 0.1736f, 0.1985f));
        holoBasePoints.Add(new Vector3(-0.1034f, 0.0036f, 0.3653f));
        leapBasePoints.Add(new Vector3(-0.0710f, 0.1799f, 0.1909f));
        holoBasePoints.Add(new Vector3(-0.1036f, 0.0035f, 0.3643f));
        leapBasePoints.Add(new Vector3(-0.0710f, 0.1798f, 0.1909f));
        holoBasePoints.Add(new Vector3(-0.1013f, 0.0050f, 0.3534f));
        leapBasePoints.Add(new Vector3(-0.0707f, 0.1791f, 0.1910f));
        holoBasePoints.Add(new Vector3(-0.0975f, 0.0026f, 0.3424f));
        leapBasePoints.Add(new Vector3(-0.0704f, 0.1831f, 0.1911f));
        holoBasePoints.Add(new Vector3(-0.0956f, 0.0009f, 0.3394f));
        leapBasePoints.Add(new Vector3(-0.0701f, 0.1829f, 0.1915f));
        holoBasePoints.Add(new Vector3(-0.0856f, -0.0045f, 0.3205f));
        leapBasePoints.Add(new Vector3(-0.0759f, 0.1858f, 0.1903f));
        holoBasePoints.Add(new Vector3(-0.0840f, -0.0043f, 0.3186f));
        leapBasePoints.Add(new Vector3(-0.0763f, 0.1866f, 0.1908f));
        holoBasePoints.Add(new Vector3(-0.0745f, -0.0045f, 0.3092f));
        leapBasePoints.Add(new Vector3(-0.0765f, 0.1856f, 0.1896f));
        holoBasePoints.Add(new Vector3(-0.0696f, -0.0049f, 0.3062f));
        leapBasePoints.Add(new Vector3(-0.0767f, 0.1859f, 0.1897f));
        holoBasePoints.Add(new Vector3(-0.0542f, -0.0048f, 0.3025f));
        leapBasePoints.Add(new Vector3(-0.0767f, 0.1787f, 0.1830f));
        holoBasePoints.Add(new Vector3(-0.0078f, -0.0333f, 0.2958f));
        leapBasePoints.Add(new Vector3(-0.0948f, 0.1904f, 0.1803f));
        holoBasePoints.Add(new Vector3(-0.0068f, -0.0336f, 0.2964f));
        leapBasePoints.Add(new Vector3(-0.0948f, 0.1904f, 0.1803f));
        holoBasePoints.Add(new Vector3(0.0880f, 0.0988f, 0.4480f));
        leapBasePoints.Add(new Vector3(-0.1667f, 0.1206f, 0.1577f));
        holoBasePoints.Add(new Vector3(-0.0920f, 0.0164f, 0.3917f));
        leapBasePoints.Add(new Vector3(-0.0552f, 0.1562f, 0.1793f));
        holoBasePoints.Add(new Vector3(-0.0919f, 0.0149f, 0.3922f));
        leapBasePoints.Add(new Vector3(-0.0552f, 0.1563f, 0.1795f));
        holoBasePoints.Add(new Vector3(-0.0908f, 0.0009f, 0.3941f));
        leapBasePoints.Add(new Vector3(-0.0595f, 0.1646f, 0.1842f));
        holoBasePoints.Add(new Vector3(-0.0906f, 0.0007f, 0.3942f));
        leapBasePoints.Add(new Vector3(-0.0595f, 0.1648f, 0.1842f));
        holoBasePoints.Add(new Vector3(0.0835f, 0.0439f, 0.4615f));
        leapBasePoints.Add(new Vector3(-0.1648f, 0.1311f, 0.1662f));
        holoBasePoints.Add(new Vector3(0.0797f, 0.0557f, 0.4612f));
        leapBasePoints.Add(new Vector3(-0.1659f, 0.1274f, 0.1647f));
        holoBasePoints.Add(new Vector3(0.0631f, 0.0794f, 0.4551f));
        leapBasePoints.Add(new Vector3(-0.1516f, 0.0975f, 0.1520f));
        holoBasePoints.Add(new Vector3(0.0573f, 0.0844f, 0.4552f));
        leapBasePoints.Add(new Vector3(-0.1487f, 0.0962f, 0.1496f));
        holoBasePoints.Add(new Vector3(0.0449f, 0.0268f, 0.5494f));
        leapBasePoints.Add(new Vector3(-0.1077f, 0.1107f, 0.1828f));
        holoBasePoints.Add(new Vector3(0.0449f, 0.0268f, 0.5493f));
        leapBasePoints.Add(new Vector3(-0.1078f, 0.1106f, 0.1827f));
        holoBasePoints.Add(new Vector3(0.0511f, 0.0391f, 0.4921f));
        leapBasePoints.Add(new Vector3(-0.1364f, 0.1169f, 0.1709f));
        holoBasePoints.Add(new Vector3(0.0511f, 0.0391f, 0.4920f));
        leapBasePoints.Add(new Vector3(-0.1363f, 0.1168f, 0.1710f));
        holoBasePoints.Add(new Vector3(0.0573f, 0.0410f, 0.4766f));
        leapBasePoints.Add(new Vector3(-0.1428f, 0.1171f, 0.1708f));
        holoBasePoints.Add(new Vector3(0.0673f, 0.0363f, 0.3882f));
        leapBasePoints.Add(new Vector3(-0.1728f, 0.1385f, 0.1381f));
        holoBasePoints.Add(new Vector3(0.0664f, 0.0356f, 0.3883f));
        leapBasePoints.Add(new Vector3(-0.1749f, 0.1390f, 0.1396f));
        holoBasePoints.Add(new Vector3(0.0773f, 0.0230f, 0.3902f));
        leapBasePoints.Add(new Vector3(-0.1782f, 0.1330f, 0.1574f));
        holoBasePoints.Add(new Vector3(0.1218f, -0.0134f, 0.3876f));
        leapBasePoints.Add(new Vector3(-0.2079f, 0.1813f, 0.1887f));
        holoBasePoints.Add(new Vector3(-0.0419f, 0.0097f, 0.3515f));
        leapBasePoints.Add(new Vector3(-0.0912f, 0.1775f, 0.1701f));
        holoBasePoints.Add(new Vector3(0.1098f, 0.0434f, 0.4819f));
        leapBasePoints.Add(new Vector3(-0.1778f, 0.1353f, 0.1730f));
        holoBasePoints.Add(new Vector3(0.1132f, 0.0438f, 0.4799f));
        leapBasePoints.Add(new Vector3(-0.1795f, 0.1348f, 0.1739f));
        holoBasePoints.Add(new Vector3(0.1154f, 0.0438f, 0.4789f));
        leapBasePoints.Add(new Vector3(-0.1803f, 0.1350f, 0.1733f));
        holoBasePoints.Add(new Vector3(0.1166f, 0.0436f, 0.4787f));
        leapBasePoints.Add(new Vector3(-0.1811f, 0.1351f, 0.1734f));
        holoBasePoints.Add(new Vector3(-0.0764f, 0.0128f, 0.4051f));
        leapBasePoints.Add(new Vector3(-0.0858f, 0.1659f, 0.1886f));
        holoBasePoints.Add(new Vector3(-0.0773f, 0.0125f, 0.4036f));
        leapBasePoints.Add(new Vector3(-0.0858f, 0.1658f, 0.1886f));
        holoBasePoints.Add(new Vector3(-0.0778f, 0.0124f, 0.4021f));
        leapBasePoints.Add(new Vector3(-0.0854f, 0.1658f, 0.1883f));
        holoBasePoints.Add(new Vector3(-0.0900f, 0.0111f, 0.3472f));
        leapBasePoints.Add(new Vector3(-0.0764f, 0.1646f, 0.1947f));
        holoBasePoints.Add(new Vector3(-0.0209f, 0.0072f, 0.3490f));
        leapBasePoints.Add(new Vector3(-0.0955f, 0.1574f, 0.1485f));
        holoBasePoints.Add(new Vector3(-0.0174f, 0.0083f, 0.3489f));
        leapBasePoints.Add(new Vector3(-0.0958f, 0.1580f, 0.1490f));
        holoBasePoints.Add(new Vector3(-0.0166f, 0.0090f, 0.3485f));
        leapBasePoints.Add(new Vector3(-0.0958f, 0.1581f, 0.1490f));
        holoBasePoints.Add(new Vector3(0.0294f, 0.0173f, 0.3701f));
        leapBasePoints.Add(new Vector3(-0.1259f, 0.1538f, 0.1471f));
        holoBasePoints.Add(new Vector3(0.0358f, 0.0074f, 0.3669f));
        leapBasePoints.Add(new Vector3(-0.1259f, 0.1537f, 0.1471f));
        holoBasePoints.Add(new Vector3(0.0363f, 0.0161f, 0.3687f));
        leapBasePoints.Add(new Vector3(-0.1260f, 0.1534f, 0.1471f));
        holoBasePoints.Add(new Vector3(0.0530f, 0.0419f, 0.3210f));
        leapBasePoints.Add(new Vector3(-0.1345f, 0.1486f, 0.1468f));
        holoBasePoints.Add(new Vector3(0.0639f, -0.0145f, 0.2670f));
        leapBasePoints.Add(new Vector3(-0.1211f, 0.1604f, 0.1458f));
        holoBasePoints.Add(new Vector3(0.0634f, -0.0160f, 0.2656f));
        leapBasePoints.Add(new Vector3(-0.1209f, 0.1605f, 0.1459f));
        holoBasePoints.Add(new Vector3(0.0628f, -0.0166f, 0.2649f));
        leapBasePoints.Add(new Vector3(-0.1208f, 0.1605f, 0.1460f));
        holoBasePoints.Add(new Vector3(0.0383f, -0.0311f, 0.2581f));
        leapBasePoints.Add(new Vector3(-0.0973f, 0.1754f, 0.1456f));
        holoBasePoints.Add(new Vector3(0.0344f, -0.0315f, 0.2558f));
        leapBasePoints.Add(new Vector3(-0.0952f, 0.1767f, 0.1462f));
        holoBasePoints.Add(new Vector3(0.0255f, -0.0317f, 0.2598f));
        leapBasePoints.Add(new Vector3(-0.0933f, 0.1765f, 0.1500f));
        holoBasePoints.Add(new Vector3(0.0221f, -0.0314f, 0.2620f));
        leapBasePoints.Add(new Vector3(-0.0932f, 0.1765f, 0.1501f));
        holoBasePoints.Add(new Vector3(0.0064f, -0.0298f, 0.2653f));
        leapBasePoints.Add(new Vector3(-0.0946f, 0.1739f, 0.1584f));
        holoBasePoints.Add(new Vector3(0.0045f, -0.0301f, 0.2658f));
        leapBasePoints.Add(new Vector3(-0.0946f, 0.1739f, 0.1585f));
        holoBasePoints.Add(new Vector3(0.0012f, -0.0301f, 0.2666f));
        leapBasePoints.Add(new Vector3(-0.0946f, 0.1740f, 0.1586f));
        holoBasePoints.Add(new Vector3(-0.0027f, -0.0291f, 0.2673f));
        leapBasePoints.Add(new Vector3(-0.0946f, 0.1740f, 0.1587f));
        holoBasePoints.Add(new Vector3(-0.0091f, -0.0258f, 0.2706f));
        leapBasePoints.Add(new Vector3(-0.0946f, 0.1740f, 0.1587f));
        holoBasePoints.Add(new Vector3(-0.0149f, -0.0212f, 0.2749f));
        leapBasePoints.Add(new Vector3(-0.0946f, 0.1740f, 0.1588f));
        holoBasePoints.Add(new Vector3(-0.0337f, -0.0030f, 0.2859f));
        leapBasePoints.Add(new Vector3(-0.0946f, 0.1740f, 0.1589f));
        holoBasePoints.Add(new Vector3(-0.0393f, 0.0018f, 0.2864f));
        leapBasePoints.Add(new Vector3(-0.0946f, 0.1740f, 0.1589f));
        holoBasePoints.Add(new Vector3(-0.0529f, 0.0115f, 0.2878f));
        leapBasePoints.Add(new Vector3(-0.0902f, 0.1770f, 0.1623f));
        holoBasePoints.Add(new Vector3(-0.0563f, 0.0136f, 0.2885f));
        leapBasePoints.Add(new Vector3(-0.0902f, 0.1770f, 0.1624f));
        holoBasePoints.Add(new Vector3(-0.0594f, 0.0151f, 0.2894f));
        leapBasePoints.Add(new Vector3(-0.0901f, 0.1770f, 0.1624f));
        holoBasePoints.Add(new Vector3(-0.0618f, 0.0165f, 0.2900f));
        leapBasePoints.Add(new Vector3(-0.0901f, 0.1771f, 0.1625f));
        holoBasePoints.Add(new Vector3(-0.0834f, 0.0151f, 0.3118f));
        leapBasePoints.Add(new Vector3(-0.0625f, 0.1640f, 0.1717f));
        holoBasePoints.Add(new Vector3(-0.0931f, 0.0145f, 0.3227f));
        leapBasePoints.Add(new Vector3(-0.0576f, 0.1618f, 0.1790f));
        holoBasePoints.Add(new Vector3(-0.0940f, 0.0140f, 0.3241f));
        leapBasePoints.Add(new Vector3(-0.0576f, 0.1618f, 0.1790f));
        holoBasePoints.Add(new Vector3(-0.0942f, 0.0139f, 0.3244f));
        leapBasePoints.Add(new Vector3(-0.0574f, 0.1618f, 0.1791f));
        holoBasePoints.Add(new Vector3(-0.0947f, 0.0136f, 0.3273f));
        leapBasePoints.Add(new Vector3(-0.0574f, 0.1618f, 0.1791f));
        holoBasePoints.Add(new Vector3(-0.0948f, 0.0136f, 0.3279f));
        leapBasePoints.Add(new Vector3(-0.0571f, 0.1616f, 0.1793f));
        holoBasePoints.Add(new Vector3(-0.0949f, 0.0137f, 0.3285f));
        leapBasePoints.Add(new Vector3(-0.0571f, 0.1615f, 0.1794f));
        holoBasePoints.Add(new Vector3(-0.0924f, 0.0100f, 0.3538f));
        leapBasePoints.Add(new Vector3(-0.0816f, 0.1647f, 0.1851f));
        holoBasePoints.Add(new Vector3(-0.0922f, 0.0096f, 0.3550f));
        leapBasePoints.Add(new Vector3(-0.0825f, 0.1660f, 0.1863f));
        holoBasePoints.Add(new Vector3(-0.0898f, 0.0082f, 0.3606f));
        leapBasePoints.Add(new Vector3(-0.0825f, 0.1646f, 0.1854f));
        holoBasePoints.Add(new Vector3(-0.0788f, 0.0087f, 0.3774f));
        leapBasePoints.Add(new Vector3(-0.0886f, 0.1663f, 0.1853f));
        holoBasePoints.Add(new Vector3(-0.0785f, 0.0085f, 0.3784f));
        leapBasePoints.Add(new Vector3(-0.0887f, 0.1664f, 0.1854f));
        holoBasePoints.Add(new Vector3(-0.0778f, 0.0081f, 0.3807f));
        leapBasePoints.Add(new Vector3(-0.0891f, 0.1665f, 0.1855f));
        holoBasePoints.Add(new Vector3(-0.0693f, 0.0078f, 0.3958f));
        leapBasePoints.Add(new Vector3(-0.0963f, 0.1705f, 0.1847f));
        holoBasePoints.Add(new Vector3(-0.0692f, 0.0077f, 0.3961f));
        leapBasePoints.Add(new Vector3(-0.0964f, 0.1705f, 0.1847f));
        holoBasePoints.Add(new Vector3(0.0772f, 0.0159f, 0.3860f));
        leapBasePoints.Add(new Vector3(-0.2034f, 0.1736f, 0.1679f));
        holoBasePoints.Add(new Vector3(0.0859f, 0.0227f, 0.3833f));
        leapBasePoints.Add(new Vector3(-0.1960f, 0.1623f, 0.1606f));
        holoBasePoints.Add(new Vector3(0.0945f, 0.0468f, 0.3801f));
        leapBasePoints.Add(new Vector3(-0.2104f, 0.1535f, 0.1567f));
        holoBasePoints.Add(new Vector3(0.0198f, 0.0381f, 0.3132f));
        leapBasePoints.Add(new Vector3(-0.0091f, 0.1453f, 0.1663f));
        holoBasePoints.Add(new Vector3(-0.0332f, 0.0402f, 0.3385f));
        leapBasePoints.Add(new Vector3(0.0177f, 0.1484f, 0.1545f));
        holoBasePoints.Add(new Vector3(-0.0807f, 0.0459f, 0.3683f));
        leapBasePoints.Add(new Vector3(0.0473f, 0.1389f, 0.1676f));
        holoBasePoints.Add(new Vector3(-0.0855f, 0.0470f, 0.3954f));
        leapBasePoints.Add(new Vector3(0.0461f, 0.1316f, 0.1786f));
        holoBasePoints.Add(new Vector3(-0.0868f, 0.0420f, 0.4094f));
        leapBasePoints.Add(new Vector3(0.0510f, 0.1394f, 0.1831f));
        holoBasePoints.Add(new Vector3(-0.0867f, 0.0418f, 0.4108f));
        leapBasePoints.Add(new Vector3(0.0511f, 0.1393f, 0.1832f));
        holoBasePoints.Add(new Vector3(-0.0852f, 0.0423f, 0.4304f));
        leapBasePoints.Add(new Vector3(0.0593f, 0.1325f, 0.2014f));
        holoBasePoints.Add(new Vector3(-0.0874f, 0.0342f, 0.4504f));
        leapBasePoints.Add(new Vector3(0.0605f, 0.1416f, 0.1950f));
        holoBasePoints.Add(new Vector3(0.0350f, 0.0168f, 0.4199f));
        leapBasePoints.Add(new Vector3(-0.0031f, 0.1469f, 0.1820f));
        holoBasePoints.Add(new Vector3(0.0347f, 0.0155f, 0.4197f));
        leapBasePoints.Add(new Vector3(-0.0032f, 0.1471f, 0.1823f));
        holoBasePoints.Add(new Vector3(0.0343f, 0.0094f, 0.4177f));
        leapBasePoints.Add(new Vector3(-0.0054f, 0.1458f, 0.1862f));
        holoBasePoints.Add(new Vector3(0.0319f, -0.0016f, 0.4169f));
        leapBasePoints.Add(new Vector3(-0.0025f, 0.1529f, 0.1893f));
        holoBasePoints.Add(new Vector3(0.0276f, -0.0149f, 0.4189f));
        leapBasePoints.Add(new Vector3(-0.0033f, 0.1564f, 0.1943f));
        holoBasePoints.Add(new Vector3(-0.0705f, -0.0677f, 0.4369f));
        leapBasePoints.Add(new Vector3(0.0520f, 0.2045f, 0.2103f));
        holoBasePoints.Add(new Vector3(-0.0727f, -0.0672f, 0.4363f));
        leapBasePoints.Add(new Vector3(0.0521f, 0.2046f, 0.2105f));
        holoBasePoints.Add(new Vector3(-0.0890f, -0.0677f, 0.4330f));
        leapBasePoints.Add(new Vector3(0.0639f, 0.2139f, 0.2110f));
        holoBasePoints.Add(new Vector3(-0.1083f, -0.0670f, 0.4358f));
        leapBasePoints.Add(new Vector3(0.0741f, 0.2156f, 0.2122f));
        holoBasePoints.Add(new Vector3(-0.1283f, -0.0665f, 0.4335f));
        leapBasePoints.Add(new Vector3(0.0868f, 0.2228f, 0.2081f));
        holoBasePoints.Add(new Vector3(-0.1287f, -0.0667f, 0.4336f));
        leapBasePoints.Add(new Vector3(0.0869f, 0.2229f, 0.2081f));
        holoBasePoints.Add(new Vector3(-0.1367f, -0.0695f, 0.4335f));
        leapBasePoints.Add(new Vector3(0.0911f, 0.2295f, 0.2082f));
        holoBasePoints.Add(new Vector3(-0.1376f, -0.0694f, 0.4328f));
        leapBasePoints.Add(new Vector3(0.0912f, 0.2296f, 0.2082f));
        holoBasePoints.Add(new Vector3(-0.1410f, -0.0691f, 0.4327f));
        leapBasePoints.Add(new Vector3(0.0913f, 0.2297f, 0.2081f));
        holoBasePoints.Add(new Vector3(-0.1528f, -0.0713f, 0.4315f));
        leapBasePoints.Add(new Vector3(0.1030f, 0.2335f, 0.2065f));
        holoBasePoints.Add(new Vector3(-0.1541f, -0.0720f, 0.4313f));
        leapBasePoints.Add(new Vector3(0.1029f, 0.2339f, 0.2063f));
        holoBasePoints.Add(new Vector3(-0.1547f, -0.0721f, 0.4315f));
        leapBasePoints.Add(new Vector3(0.1029f, 0.2340f, 0.2063f));
        holoBasePoints.Add(new Vector3(-0.1663f, -0.0632f, 0.4317f));
        leapBasePoints.Add(new Vector3(0.1117f, 0.2462f, 0.2038f));
        holoBasePoints.Add(new Vector3(-0.1665f, -0.0620f, 0.4317f));
        leapBasePoints.Add(new Vector3(0.1117f, 0.2463f, 0.2039f));
        holoBasePoints.Add(new Vector3(-0.1667f, -0.0604f, 0.4312f));
        leapBasePoints.Add(new Vector3(0.1117f, 0.2463f, 0.2039f));
        holoBasePoints.Add(new Vector3(-0.1669f, -0.0582f, 0.4306f));
        leapBasePoints.Add(new Vector3(0.1117f, 0.2464f, 0.2039f));
        holoBasePoints.Add(new Vector3(-0.0770f, 0.0304f, 0.4284f));
        leapBasePoints.Add(new Vector3(0.0532f, 0.1340f, 0.1998f));
        holoBasePoints.Add(new Vector3(-0.0752f, 0.0458f, 0.4243f));
        leapBasePoints.Add(new Vector3(0.0568f, 0.1330f, 0.1959f));
        holoBasePoints.Add(new Vector3(-0.0755f, 0.0500f, 0.4241f));
        leapBasePoints.Add(new Vector3(0.0535f, 0.1254f, 0.1978f));
        holoBasePoints.Add(new Vector3(-0.0725f, 0.0603f, 0.4203f));
        leapBasePoints.Add(new Vector3(0.0511f, 0.1205f, 0.1946f));
        holoBasePoints.Add(new Vector3(-0.0682f, 0.0732f, 0.4145f));
        leapBasePoints.Add(new Vector3(0.0496f, 0.1167f, 0.1920f));
        holoBasePoints.Add(new Vector3(0.0314f, 0.0689f, 0.3811f));
        leapBasePoints.Add(new Vector3(-0.0119f, 0.1162f, 0.1744f));
        holoBasePoints.Add(new Vector3(0.0322f, 0.0683f, 0.3811f));
        leapBasePoints.Add(new Vector3(-0.0120f, 0.1163f, 0.1744f));
        holoBasePoints.Add(new Vector3(0.0331f, 0.0675f, 0.3811f));
        leapBasePoints.Add(new Vector3(-0.0121f, 0.1164f, 0.1745f));
        holoBasePoints.Add(new Vector3(0.0509f, 0.0472f, 0.3711f));
        leapBasePoints.Add(new Vector3(-0.0192f, 0.1308f, 0.1727f));
        holoBasePoints.Add(new Vector3(0.0505f, 0.0456f, 0.3715f));
        leapBasePoints.Add(new Vector3(-0.0192f, 0.1308f, 0.1728f));
        holoBasePoints.Add(new Vector3(-0.0483f, -0.0447f, 0.3945f));
        leapBasePoints.Add(new Vector3(0.0126f, 0.1743f, 0.2066f));
        holoBasePoints.Add(new Vector3(-0.0766f, -0.0422f, 0.3986f));
        leapBasePoints.Add(new Vector3(0.0377f, 0.1707f, 0.2088f));
        holoBasePoints.Add(new Vector3(-0.0785f, -0.0420f, 0.3990f));
        leapBasePoints.Add(new Vector3(0.0377f, 0.1707f, 0.2091f));
        holoBasePoints.Add(new Vector3(-0.0352f, -0.0111f, 0.4466f));
        leapBasePoints.Add(new Vector3(0.0238f, 0.1611f, 0.2005f));
        holoBasePoints.Add(new Vector3(-0.0351f, -0.0108f, 0.4471f));
        leapBasePoints.Add(new Vector3(0.0237f, 0.1612f, 0.2005f));
        holoBasePoints.Add(new Vector3(-0.0349f, -0.0103f, 0.4483f));
        leapBasePoints.Add(new Vector3(0.0236f, 0.1613f, 0.2004f));
        holoBasePoints.Add(new Vector3(-0.0346f, -0.0110f, 0.4491f));
        leapBasePoints.Add(new Vector3(0.0236f, 0.1613f, 0.2005f));
        holoBasePoints.Add(new Vector3(-0.0344f, -0.0119f, 0.4510f));
        leapBasePoints.Add(new Vector3(0.0237f, 0.1614f, 0.2005f));
        holoBasePoints.Add(new Vector3(-0.0345f, -0.0124f, 0.4527f));
        leapBasePoints.Add(new Vector3(0.0237f, 0.1614f, 0.2006f));
        holoBasePoints.Add(new Vector3(-0.0404f, -0.0187f, 0.4678f));
        leapBasePoints.Add(new Vector3(0.0310f, 0.1641f, 0.2017f));
        holoBasePoints.Add(new Vector3(-0.0410f, -0.0142f, 0.4768f));
        leapBasePoints.Add(new Vector3(0.0315f, 0.1620f, 0.2018f));
        holoBasePoints.Add(new Vector3(-0.0410f, -0.0141f, 0.4779f));
        leapBasePoints.Add(new Vector3(0.0317f, 0.1619f, 0.2019f));
        holoBasePoints.Add(new Vector3(-0.0454f, -0.0125f, 0.4852f));
        leapBasePoints.Add(new Vector3(0.0354f, 0.1628f, 0.2025f));
        holoBasePoints.Add(new Vector3(-0.0857f, 0.0125f, 0.4786f));
        leapBasePoints.Add(new Vector3(0.0643f, 0.1543f, 0.2016f));
        holoBasePoints.Add(new Vector3(-0.0869f, 0.0363f, 0.3260f));
        leapBasePoints.Add(new Vector3(0.0462f, 0.1363f, 0.1850f));
        holoBasePoints.Add(new Vector3(-0.0875f, 0.0362f, 0.3260f));
        leapBasePoints.Add(new Vector3(0.0464f, 0.1364f, 0.1850f));
        holoBasePoints.Add(new Vector3(-0.1052f, 0.0287f, 0.3484f));
        leapBasePoints.Add(new Vector3(0.0710f, 0.1508f, 0.1845f));
        holoBasePoints.Add(new Vector3(-0.0297f, 0.0262f, 0.3565f));
        leapBasePoints.Add(new Vector3(0.0254f, 0.1497f, 0.1805f));
        holoBasePoints.Add(new Vector3(-0.1003f, 0.0157f, 0.4316f));
        leapBasePoints.Add(new Vector3(0.0648f, 0.1581f, 0.1990f));
        holoBasePoints.Add(new Vector3(-0.1114f, 0.0099f, 0.4342f));
        leapBasePoints.Add(new Vector3(0.0626f, 0.1538f, 0.1936f));
        holoBasePoints.Add(new Vector3(-0.1133f, 0.0090f, 0.4358f));
        leapBasePoints.Add(new Vector3(0.0626f, 0.1538f, 0.1934f));
        holoBasePoints.Add(new Vector3(-0.1153f, 0.0066f, 0.4384f));
        leapBasePoints.Add(new Vector3(0.0627f, 0.1538f, 0.1933f));
        holoBasePoints.Add(new Vector3(-0.1157f, 0.0059f, 0.4390f));
        leapBasePoints.Add(new Vector3(0.0628f, 0.1539f, 0.1933f));
        holoBasePoints.Add(new Vector3(-0.1164f, 0.0042f, 0.4436f));
        leapBasePoints.Add(new Vector3(0.0632f, 0.1555f, 0.1926f));
        holoBasePoints.Add(new Vector3(-0.1152f, 0.0047f, 0.4500f));
        leapBasePoints.Add(new Vector3(0.0631f, 0.1555f, 0.1928f));
        holoBasePoints.Add(new Vector3(-0.0770f, 0.0178f, 0.4751f));
        leapBasePoints.Add(new Vector3(0.0516f, 0.1519f, 0.2028f));
        holoBasePoints.Add(new Vector3(-0.1424f, -0.0079f, 0.4798f));
        leapBasePoints.Add(new Vector3(0.0926f, 0.1678f, 0.2126f));
        holoBasePoints.Add(new Vector3(-0.0847f, 0.0035f, 0.4593f));
        leapBasePoints.Add(new Vector3(0.0551f, 0.1599f, 0.2049f));
        holoBasePoints.Add(new Vector3(-0.0313f, 0.0053f, 0.4175f));
        leapBasePoints.Add(new Vector3(0.0381f, 0.1622f, 0.1974f));
        holoBasePoints.Add(new Vector3(-0.0304f, 0.0052f, 0.4184f));
        leapBasePoints.Add(new Vector3(0.0380f, 0.1622f, 0.1973f));
        holoBasePoints.Add(new Vector3(-0.0285f, 0.0056f, 0.4207f));
        leapBasePoints.Add(new Vector3(0.0368f, 0.1608f, 0.1969f));
        holoBasePoints.Add(new Vector3(-0.0225f, 0.0101f, 0.4292f));
        leapBasePoints.Add(new Vector3(0.0350f, 0.1559f, 0.1913f));
        holoBasePoints.Add(new Vector3(-0.0119f, 0.0072f, 0.4437f));
        leapBasePoints.Add(new Vector3(0.0305f, 0.1650f, 0.1969f));
        holoBasePoints.Add(new Vector3(0.0350f, -0.0211f, 0.2970f));
        leapBasePoints.Add(new Vector3(-0.0160f, 0.1659f, 0.1518f));
        holoBasePoints.Add(new Vector3(-0.0066f, -0.0549f, 0.4041f));
        leapBasePoints.Add(new Vector3(0.0174f, 0.1873f, 0.2010f));
        holoBasePoints.Add(new Vector3(-0.0034f, -0.0548f, 0.4022f));
        leapBasePoints.Add(new Vector3(0.0174f, 0.1870f, 0.2009f));
        holoBasePoints.Add(new Vector3(-0.0007f, -0.0548f, 0.4003f));
        leapBasePoints.Add(new Vector3(0.0171f, 0.1870f, 0.2017f));
        holoBasePoints.Add(new Vector3(0.0054f, -0.0290f, 0.4544f));
        leapBasePoints.Add(new Vector3(-0.0173f, 0.1620f, 0.2283f));
        holoBasePoints.Add(new Vector3(0.0847f, -0.0058f, 0.3378f));
        leapBasePoints.Add(new Vector3(-0.0333f, 0.1702f, 0.1654f));
        holoBasePoints.Add(new Vector3(0.0859f, -0.0046f, 0.3358f));
        leapBasePoints.Add(new Vector3(-0.0333f, 0.1702f, 0.1653f));
        holoBasePoints.Add(new Vector3(0.0861f, -0.0040f, 0.3346f));
        leapBasePoints.Add(new Vector3(-0.0332f, 0.1703f, 0.1653f));
        holoBasePoints.Add(new Vector3(0.0587f, 0.0041f, 0.3384f));
        leapBasePoints.Add(new Vector3(-0.0175f, 0.1601f, 0.1592f));
    }
}