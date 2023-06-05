using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GestureManager {
    bool ishorizontal = false;
    bool isclicking = false;
    bool isholding = false;
    bool isrelaxed = true;
    long timer;
    bool initialize = false;
    float lengthfactor;

    public int GetGesture(LeapHand hand, Vector3 forward, Vector3 upward, bool enforcedhold)
    {
        float maxdist = -1;
        float[] dists = new float[5];
        int i = 0;

        foreach (LeapFinger f in hand.fingers)
        {
            if (f.type != LeapFinger.FingerType.TYPE_PINKY && f.type != LeapFinger.FingerType.TYPE_THUMB)
            {
                //float distance = Mathf.Abs(palmposition.y - f.tipposition.y);
                Vector3 diff = hand.PalmPosition - f.tipposition;
                Vector3 upcomponent = Vector3.Dot(diff, upward) * upward;
                //Vector3 upcomponent = Vector3.Dot(diff, hand.ybasis) * hand.ybasis;
                float distance = upcomponent.magnitude;

                if (distance < 0)
                    maxdist = 100;
                maxdist = distance > maxdist ? distance : maxdist;
                dists[i] = distance;
            }
        }

        if (initialize)
            lengthfactor = maxdist / 2;
        else if(maxdist > lengthfactor)
                lengthfactor = maxdist + lengthfactor/2;

        maxdist /= lengthfactor;

        //Debug.Log(maxdist);

        /*
        bool maybeisrotating = Mathf.Abs(hand.PalmNormal.y) < 0.8;
        bool almostvertical = Mathf.Abs(hand.PalmNormal.y) > 0.8;
        bool horizontal = (ishorizontal && maybeisrotating)
            || (!ishorizontal && Mathf.Abs(hand.PalmNormal.y) < 0.2);
            */

        bool maybeisrotating = Mathf.Abs(hand.zbasis.y) < 0.5;
        bool almostvertical = Mathf.Abs(hand.zbasis.y) > 0.8;
        bool horizontal = (ishorizontal && maybeisrotating)
            || (!ishorizontal && Mathf.Abs(hand.zbasis.y) < 0.3);

        if (horizontal)
        {
            if(!ishorizontal)
               Debug.Log("horizontal");
            ishorizontal = true;
            isclicking = false;
            isholding = false;
            isrelaxed = false;
            return -1;
        }
        else
        {
            if (isrelaxed && (maxdist < 0.5f /*|| (enforcedhold && hand.PinchStrength == 1)*/) && timer == 0)// && hand.PalmVelocityMagnitude < 0.008f && !almostvertical)
            {
                if(!isclicking)
                    Debug.Log("click");
                ishorizontal = false;
                isclicking = true;
                isholding = false;
                isrelaxed = false;
                return 0;
            }
            else if ((isclicking || isholding) && (maxdist < 0.6f || (enforcedhold && hand.PinchStrength > 0.8)))
            {
                if(!isholding)
                    Debug.Log("holding");
                ishorizontal = false;
                isclicking = false;
                isholding = true;
                isrelaxed = false;
                /*if (hand.PalmVelocityMagnitude < 0.008f)
                    return 1;
                else
                    */return 2;
            }
            else
            {
                if (ishorizontal)
                {
                    timer = 30;
                    Debug.Log("reset timer");
                }
                else if (timer > 0)
                    timer--;
                if(!isrelaxed)
                   Debug.Log("relaxed");
                ishorizontal = false;
                isclicking = false;
                isholding = false;
                isrelaxed = true;

                return 3;
            }
        }
    }
}
