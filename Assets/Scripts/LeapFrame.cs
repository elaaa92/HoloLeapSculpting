using System;
using System.Collections.Generic;
using UnityEngine;

public class LeapFrame
{
    public LeapHand[] hands;
    //Microseconds timediff
    public long timediff;

    public LeapFrame(LeapHand[] hands, long timediff)
    {
        this.hands = hands;
        this.timediff = timediff;
    }

    public LeapHand[] GetHands(Matrix4x4 pt)
    {
        foreach(LeapHand hand in hands)
        {
            hand.Transform(pt);
        }

        return hands;
    }

    public bool IsEmpty
    {
        get
        {
            return hands.Length == 0;
        }
    }
}

public class LeapArm
{
    public Vector3 wristPosition;

    public LeapArm(Vector3 wristPosition)
    {
        this.wristPosition = wristPosition;
    }
}

public class LeapHand
{
    public int Id { get; set; }
    public Vector3 xbasis;
    public Vector3 ybasis;
    public Vector3 zbasis;
    public bool isLeft;
    public float PinchStrength;
    public Vector3 PalmPosition;
    public Vector3 PalmNormal;
    public float PalmVelocityMagnitude;
    public LeapFinger[] fingers = new LeapFinger[5];
    public LeapArm arm;

    public LeapHand(int id, Vector3 xbasis, Vector3 ybasis, Vector3 zbasis, bool isLeft,
            Vector3 palmPosition, Vector3 palmNormal, float palmVelocitymagnitude, float pinchStrength,
            LeapFinger[] fingers, LeapArm arm)
    {
        this.Id = id;
        //Units are mm
        this.xbasis = xbasis;
        this.ybasis = ybasis;
        this.zbasis = zbasis;
        this.isLeft = isLeft;
        this.PinchStrength = pinchStrength;
        this.PalmPosition = palmPosition;
        this.PalmNormal = palmNormal;
        this.PalmVelocityMagnitude = palmVelocitymagnitude;
        this.arm = arm;

        //Order by type
        foreach (LeapFinger finger in fingers)
        {
            this.fingers[(int)finger.type] = finger;
        }
    }

    public Vector3 GetAdaptedWristPosition()
    {
        return PalmPosition + 0.025f * zbasis;
    }

    public void ApplyTransform(Matrix4x4 ptransform, ref Vector3 vector, bool isDirection)
    {
        if (isDirection)
        {
            if (ptransform.ValidTRS())
                vector = ptransform.rotation * vector;
        }
        else
        {
            vector = ptransform.MultiplyPoint3x4(vector);
        }
    }

    public void Transform(Matrix4x4 projectiveTransform)
    {
        //Arm
        ApplyTransform(projectiveTransform, ref this.arm.wristPosition, false);

        //Hand
        ApplyTransform(projectiveTransform, ref this.xbasis, true);
        ApplyTransform(projectiveTransform, ref this.ybasis, true);
        ApplyTransform(projectiveTransform, ref this.zbasis, true);
        ApplyTransform(projectiveTransform, ref this.PalmPosition, false);
        ApplyTransform(projectiveTransform, ref this.PalmNormal, true);

        //Fingers
        for (int j = 0; j < this.fingers.Length; j++)
        {
            ApplyTransform(projectiveTransform, ref fingers[j].tipposition, false);
            for (int k = 0; k < 4; k++)
            {
                ApplyTransform(projectiveTransform, ref fingers[j].bones[k].xbasis, true);
                ApplyTransform(projectiveTransform, ref fingers[j].bones[k].ybasis, true);
                ApplyTransform(projectiveTransform, ref fingers[j].bones[k].zbasis, true);
            }
        }
    }
}

public class LeapFinger
{
    public enum FingerType { TYPE_THUMB = 0, TYPE_INDEX = 1, TYPE_MIDDLE = 2, TYPE_RING = 3, TYPE_PINKY = 4 };
    public int Id { get; set; }
    public Vector3 tipposition;
    public FingerType type;
    public LeapBone[] bones;

    public LeapFinger(int id, Vector3 tipposition, LeapBone[] bones, int type)
    {
        Id = id;
        this.tipposition = tipposition;
        this.bones = bones;
        this.type = (FingerType)type;
    }

    public LeapBone GetBone(int ind)
    {
        return bones[ind];
    }
}

public class LeapBone
{
    public Vector3 xbasis;
    public Vector3 ybasis;
    public Vector3 zbasis;

    public LeapBone(Vector3 xbasis, Vector3 ybasis, Vector3 zbasis)
    {
        this.xbasis = xbasis;
        this.ybasis = ybasis;
        this.zbasis = zbasis;
    }
}
