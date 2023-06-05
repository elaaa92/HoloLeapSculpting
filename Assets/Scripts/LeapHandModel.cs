using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LeapHandModel: MonoBehaviour {
    private Transform wrist;
    private Transform palm;
    public Transform[,] fingerBones = new Transform[5,4];
    public Vector3[,] fingerForward = new Vector3[5,4];
    public Vector3[,] fingerUp = new Vector3[5,4];
    public bool isLeft;

	// Use this for initialization
	void Start ()
    {
        string[] fingername = { "thumb_meta", "index_meta", "middle_meta", "ring_meta", "pinky_meta" };
        wrist = transform.Find("Wrist");
        palm = wrist.transform.Find("Palm");
        for (int i = 0; i < 5; i++)
        {
            fingerBones[i, 0] = palm.transform.Find(fingername[i]);
            fingerForward[i, 0] = fingerBones[i, 0].forward;
            fingerUp[i, 0] = fingerBones[i, 0].up;

            for (int j = 1; j < 4; j++)
            {
                if(i!=0 || j<3)
                {
                    fingerBones[i, j] = fingerBones[i, j - 1].GetChild(0);
                    fingerForward[i, j] = fingerBones[i, j].forward;
                    fingerUp[i, j] = fingerBones[i, j].up;
                }
            }
        }
    }

    public void UpdateHand(LeapHand leaphand)
    {
        //Quaternion handReorientation = (leaphand.isLeft ? (Quaternion.Euler(new Vector3(0, 90, 180))) : (Quaternion.Euler(new Vector3(0, 90, 0))));
        Quaternion handReorientation = (leaphand.isLeft ? (Quaternion.Euler(new Vector3(0, 90, 0))) : (Quaternion.Euler(new Vector3(0, 90, 0))));

        //Quaternion leaphand_rotation = Quaternion.LookRotation(leaphand.zbasis, leaphand.ybasis);
        Quaternion leaphand_rotation = (leaphand.isLeft ? (Quaternion.LookRotation(-leaphand.zbasis, -leaphand.ybasis)) : (Quaternion.LookRotation(leaphand.zbasis, leaphand.ybasis)));

        palm.position = leaphand.arm.wristPosition;
        palm.rotation = leaphand_rotation;
        
        int i = 0;
        foreach (LeapFinger leapfinger in leaphand.fingers)
        {
            if (i==0)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (isLeft)
                        fingerBones[i, j].rotation = Quaternion.LookRotation(-leapfinger.bones[j].zbasis, -leapfinger.bones[j].xbasis);
                    else
                        fingerBones[i, j].rotation = Quaternion.LookRotation(leapfinger.bones[j].zbasis, leapfinger.bones[j].xbasis);
                }
                fingerBones[i, 0].rotation *= Quaternion.Euler(new Vector3(0, 0, -45));
                
            }
            else
            {
                for (int j = 0; j < 4; j++)
                {
                    if (isLeft)
                        fingerBones[i, j].rotation = Quaternion.LookRotation(-leapfinger.bones[j].ybasis, -leapfinger.bones[j].xbasis);
                    else
                        fingerBones[i, j].rotation = Quaternion.LookRotation(leapfinger.bones[j].ybasis, leapfinger.bones[j].xbasis);
                }
                fingerBones[i, 0].rotation *= Quaternion.Euler(new Vector3(0, 0, -90));
            }
            i++;
        }
        
        palm.rotation *= handReorientation;
    }
}
