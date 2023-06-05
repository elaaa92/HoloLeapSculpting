using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AffineTransformConverter {
    public static Matrix4x4 Convert(float[,] tM)
    {
        Vector4 trasl = new Vector3(tM[0, 3], tM[1, 3], tM[2, 3]);
        Quaternion rot = new Quaternion();
        Vector3 scale = Vector3.zero;
        scale.x = (new Vector3(tM[0, 0], tM[1, 0], tM[2, 0])).magnitude;
        scale.y = (new Vector3(tM[0, 1], tM[1, 1], tM[2, 1])).magnitude;
        scale.z = (new Vector3(tM[0, 2], tM[1, 2], tM[2, 2])).magnitude;
        for (int i = 0; i < 3; i++)
        {
            tM[i, 0] /= scale.x;
            tM[i, 1] /= scale.y;
            tM[i, 2] /= scale.z;
        }
        /*
        float trace = tM[0, 0] + tM[1, 1] + tM[2, 2];
        float s;
        if (trace > 0)
        {
            s = 0.5f / Mathf.Sqrt(trace);
            rot.w = 0.25f / s;
            rot.x = (tM[2, 1] - tM[1, 2]) * s;
            rot.y = (tM[0, 2] - tM[2, 0]) * s;
            rot.z = (tM[1, 0] - tM[0, 1]) * s;
        }
        else
        {
            if (tM[0, 0] > tM[1, 1] && tM[0, 0] > tM[2, 2])
            {
                s = Mathf.Sqrt(1.0f + tM[0, 0] - tM[1, 1] - tM[2, 2]) * 2; // S=4*qx 
                rot.w = (tM[2, 1] - tM[1, 2]) / s;
                rot.x = 0.25f * s;
                rot.y = (tM[0, 1] + tM[1, 0]) / s;
                rot.z = (tM[0, 2] + tM[2, 0]) / s;
            }
            else if (tM[1, 1] > tM[2, 2])
            {
                s = Mathf.Sqrt(1.0f + tM[1, 1] - tM[0, 0] - tM[2, 2]) * 2; // S=4*qy
                rot.w = (tM[0, 2] - tM[2, 0]) / s;
                rot.x = (tM[0, 1] + tM[1, 0]) / s;
                rot.y = 0.25f * s;
                rot.z = (tM[1, 2] + tM[2, 1]) / s;
            }
            else
            {
                s = Mathf.Sqrt(1.0f + tM[2, 2] - tM[0, 0] - tM[1, 1]) * 2; // S=4*qz
                rot.w = (tM[1, 0] - tM[0, 1]) / s;
                rot.x = (tM[0, 2] + tM[2, 0]) / s;
                rot.y = (tM[1, 2] + tM[2, 1]) / s;
                rot.z = 0.25f * s;
            }
        }*/

        rot.w = Mathf.Sqrt(Mathf.Max(0, 1 + tM[0, 0] + tM[1, 1] + tM[2, 2])) / 2;
        rot.x = Mathf.Sqrt(Mathf.Max(0, 1 + tM[0, 0] - tM[1, 1] - tM[2, 2])) / 2;
        rot.y = Mathf.Sqrt(Mathf.Max(0, 1 - tM[0, 0] + tM[1, 1] - tM[2, 2])) / 2;
        rot.z = Mathf.Sqrt(Mathf.Max(0, 1 - tM[0, 0] - tM[1, 1] + tM[2, 2])) / 2;
        rot.x *= Mathf.Sign(rot.x * (tM[2, 1] - tM[1, 2]));
        rot.y *= Mathf.Sign(rot.y * (tM[0, 2] - tM[2, 0]));
        rot.z *= Mathf.Sign(rot.z * (tM[1, 0] - tM[0, 1]));

        //Normalize
        float f = 1f / Mathf.Sqrt(rot.x * rot.x + rot.y * rot.y + rot.z * rot.z + rot.w * rot.w);
        rot = new Quaternion(rot.x * f, rot.y * f, rot.z * f, f*rot.w);
        
        Debug.Log("trasl: " + trasl.ToString("4F") + " rot: " + rot.ToString("4F") + " size: " + scale.ToString("4F"));
        return Matrix4x4.TRS(trasl, rot, scale);
    }
}
