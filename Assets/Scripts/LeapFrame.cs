using System;
using System.Collections.Generic;
using UnityEngine;

public class LeapFrame
{
    List<LeapHand> hands;

    public LeapFrame(List<LeapHand> hands)
    {
        this.hands = hands;
    }

    public List<LeapHand> GetHands()
    {
        return hands;
    }

    public void Transform(Vector3 originTransform)
    {
        foreach (LeapHand hand in hands)
        {
            hand.PalmPosition = hand.PalmPosition - originTransform;
            hand.PalmNormal = hand.PalmNormal - originTransform;
        }
    }

    public class LeapHand
    {
        public int Id { get; set;  }
        public Vector3 PalmPosition { get; set; }
        public Vector3 PalmNormal { get; set; }
        public bool IsPointing;

        public LeapHand(int id, float[] palmPosition, float[] palmNormal, bool isPointing)
        {
            this.Id = id;
            //Change unit from mm to m
            this.PalmPosition = Vector3.Scale(new Vector3(palmPosition[0], palmPosition[1], palmPosition[2]), new Vector3(0.001F, 0.001F, 0.001F));
            this.PalmNormal = Vector3.Scale(new Vector3(palmNormal[0], palmNormal[1], palmNormal[2]), new Vector3(0.001F, 0.001F, 0.001F));
            this.IsPointing = isPointing;
        }
    }
}
