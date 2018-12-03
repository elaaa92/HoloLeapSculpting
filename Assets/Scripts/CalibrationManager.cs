// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.WSA.Input;

public class CalibrationManager : MonoBehaviour
{
    /// <summary>
    /// HandDetected tracks the hand detected state.
    /// Returns true if the list of tracked hands is not empty.
    /// </summary>
    /// 
    private LeapFrame lastFrame;
    private Matrix4x4 originTransform {get; set;}
    private List<Vector3> holoBasePoints = new List<Vector3>();
    private List<Vector3> leapBasePoints = new List<Vector3>();

    public bool HandDetected
    {
        get { return trackedHands.Count > 0; }
    }

    public GameObject TrackingObject;
    //public TextMesh StatusText;
    public Color DefaultColor = Color.green;
    public Color TapColor = Color.blue;
    public Color HoldColor = Color.red;

    private HashSet<uint> trackedHands = new HashSet<uint>();
    private Dictionary<uint, GameObject> trackingObject = new Dictionary<uint, GameObject>();
    //private GestureRecognizer gestureRecognizer;
    private uint activeId;

    void Awake()
    {
        Debug.Log("Awake");
        InteractionManager.InteractionSourceDetected += InteractionManager_InteractionSourceDetected;
        InteractionManager.InteractionSourceUpdated += InteractionManager_InteractionSourceUpdated;
        InteractionManager.InteractionSourceLost += InteractionManager_InteractionSourceLost;

        /*gestureRecognizer = new GestureRecognizer();
        gestureRecognizer.SetRecognizableGestures(GestureSettings.Tap | GestureSettings.Hold);
        gestureRecognizer.Tapped += GestureRecognizerTapped;
        gestureRecognizer.HoldStarted += GestureRecognizer_HoldStarted;
        gestureRecognizer.HoldCompleted += GestureRecognizer_HoldCompleted;
        gestureRecognizer.HoldCanceled += GestureRecognizer_HoldCanceled;
        gestureRecognizer.StartCapturingGestures();
        StatusText.text = "READY";*/
    }

    void ChangeObjectColor(GameObject obj, Color color)
    {
        var rend = obj.GetComponentInChildren<Renderer>();
        if (rend)
        {
            rend.material.color = color;
            //Debug.LogFormat("Color Change: {0}", color.ToString());
        }
    }

    /*
    private void GestureRecognizer_HoldStarted(HoldStartedEventArgs args)
    {
        uint id = args.source.id;
        StatusText.text = $"HoldStarted - Kind:{args.source.kind.ToString()} - Id:{id}";
        if (trackingObject.ContainsKey(activeId))
        {
            ChangeObjectColor(trackingObject[activeId], HoldColor);
            StatusText.text += "-TRACKED";
        }
    }

    private void GestureRecognizer_HoldCompleted(HoldCompletedEventArgs args)
    {
        uint id = args.source.id;
        StatusText.text = $"HoldCompleted - Kind:{args.source.kind.ToString()} - Id:{id}";
        if (trackingObject.ContainsKey(activeId))
        {
            ChangeObjectColor(trackingObject[activeId], DefaultColor);
            StatusText.text += "-TRACKED";
        }
    }

    private void GestureRecognizer_HoldCanceled(HoldCanceledEventArgs args)
    {
        uint id = args.source.id;
        StatusText.text = $"HoldCanceled - Kind:{args.source.kind.ToString()} - Id:{id}";
        if (trackingObject.ContainsKey(activeId))
        {
            ChangeObjectColor(trackingObject[activeId], DefaultColor);
            StatusText.text += "-TRACKED";
        }
    }

    private void GestureRecognizerTapped(TappedEventArgs args)
    {
        uint id = args.source.id;
        StatusText.text = $"Tapped - Kind:{args.source.kind.ToString()} - Id:{id}";
        if (trackingObject.ContainsKey(activeId))
        {
            ChangeObjectColor(trackingObject[activeId], TapColor);
            StatusText.text += "-TRACKED";
        }
    }
    */

    private void InteractionManager_InteractionSourceDetected(InteractionSourceDetectedEventArgs args)
    {
        uint id = args.state.source.id;
        // Check to see that the source is a hand.
        if (args.state.source.kind != InteractionSourceKind.Hand)
        {
            return;
        }

        trackedHands.Add(id);
        activeId = id;

        GameObject obj = null;
        Vector3 pos;

        if (args.state.sourcePose.TryGetPosition(out pos))
        {
            //Debug.Log(pos.ToString());
            obj = Instantiate(TrackingObject, pos, Quaternion.identity);
            ChangeObjectColor(obj, DefaultColor);
        }

        trackingObject.Add(id, obj);

        //Update the base.
        UpdateBase(pos);
    }

    private void InteractionManager_InteractionSourceUpdated(InteractionSourceUpdatedEventArgs args)
    {
        uint id = args.state.source.id;
        Vector3 pos;
        Quaternion rot;

        if (args.state.source.kind == InteractionSourceKind.Hand)
        {
            if (trackingObject.ContainsKey(id))
            {
                if (args.state.sourcePose.TryGetPosition(out pos))
                {
                    trackingObject[id].transform.position = pos;
                }

                if (args.state.sourcePose.TryGetRotation(out rot))
                {
                    trackingObject[id].transform.rotation = rot;
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

        if (trackedHands.Contains(id))
        {
            trackedHands.Remove(id);
        }

        if (trackingObject.ContainsKey(id))
        {
            var obj = trackingObject[id];
            trackingObject.Remove(id);
            Destroy(obj);
        }
        if (trackedHands.Count > 0)
        {
            activeId = trackedHands.First();
        }
    }

    void OnDestroy()
    {
        InteractionManager.InteractionSourceDetected -= InteractionManager_InteractionSourceDetected;
        InteractionManager.InteractionSourceUpdated -= InteractionManager_InteractionSourceUpdated;
        InteractionManager.InteractionSourceLost -= InteractionManager_InteractionSourceLost;

        /*
        gestureRecognizer.Tapped -= GestureRecognizerTapped;
        gestureRecognizer.HoldStarted -= GestureRecognizer_HoldStarted;
        gestureRecognizer.HoldCompleted -= GestureRecognizer_HoldCompleted;
        gestureRecognizer.HoldCanceled -= GestureRecognizer_HoldCanceled;
        gestureRecognizer.StopCapturingGestures();*/
    }

    public Vector3 GetHandPosition()
    {
        return trackingObject.First().Value.transform.position;
    }

    //Return true if base is complete
    public void UpdateBase(Vector3 holoPoint)
    {
        this.lastFrame = JsonConvert.DeserializeObject<LeapFrame>(gameObject.GetComponent<SocketManager>().GetLastInfo());
        if (lastFrame != null)
        {
            List<LeapFrame.LeapHand> hands = lastFrame.GetHands().FindAll(x => x.IsPointing);
            if (hands.Count() > 0)
            {
                LeapFrame.LeapHand leapHand = hands.First();
                Debug.Log("hand detected by leap: " + lastFrame.GetHands().Count().ToString());
                //If hand is pointing and the base is not complete
                //check if holoPoint vector is orthogonal to the other vector of holobase
                if (leapHand.IsPointing && holoBasePoints.Count < 4)
                {
                    Vector3 leapPoint = leapHand.PalmPosition;
                    int i = 0;
                    while (i < leapBasePoints.Count)
                    {
                        float orthoprod = System.Math.Abs(Vector3.Dot(leapPoint, leapBasePoints[i]) / (leapPoint.magnitude * leapBasePoints[i].magnitude));
                        Debug.Log("Orthogonal product: " + System.Math.Abs(Vector3.Dot(leapPoint, leapBasePoints[i]) / (leapPoint.magnitude * leapBasePoints[i].magnitude)).ToString());
                        if (orthoprod > 0.001F)
                            break;
                        i++;
                    }
                    if (i == leapBasePoints.Count)
                    {
                        Debug.Log("Hand added");
                        //Add the two correspondent point to the base
                        holoBasePoints.Add(holoPoint);
                        leapBasePoints.Add(leapPoint);
                    }
                    Debug.Log("Current base has " + holoBasePoints.Count + " elements");
                    //If the base is changed and it is complete, change origin transform
                    if (holoBasePoints.Count == 4)
                        UpdateOriginTransform();
                }
            }
            else
            {
                Debug.Log("No available hands");
            }
        }
        else
        {
            Debug.Log("No available frames");
        }
    }

    public void UpdateOriginTransform()
    {
        Matrix4x4 holoMatrix = new Matrix4x4(
            new Vector4(holoBasePoints[0].x, holoBasePoints[0].y, holoBasePoints[0].z, 1),
            new Vector4(holoBasePoints[1].x, holoBasePoints[1].y, holoBasePoints[1].z, 1),
            new Vector4(holoBasePoints[2].x, holoBasePoints[2].y, holoBasePoints[2].z, 1),
            new Vector4(holoBasePoints[3].x, holoBasePoints[3].y, holoBasePoints[3].z, 1)
            );
        Matrix4x4 leapMatrix = new Matrix4x4(
            new Vector4(leapBasePoints[0].x, leapBasePoints[0].y, leapBasePoints[0].z, 1),
            new Vector4(leapBasePoints[1].x, leapBasePoints[1].y, leapBasePoints[1].z, 1),
            new Vector4(leapBasePoints[2].x, leapBasePoints[2].y, leapBasePoints[2].z, 1),
            new Vector4(leapBasePoints[3].x, leapBasePoints[3].y, leapBasePoints[3].z, 1)
            );
        originTransform = leapMatrix.inverse * holoMatrix;
        gameObject.GetComponent<GestureManager>().calibrated = true;
    }

    public Matrix4x4 GetOriginTransform()
    {
        return originTransform;
    }
}