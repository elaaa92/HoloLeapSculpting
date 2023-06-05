using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Security.Cryptography;
using System.Text;
using System;

public class ComparerVector3 : IEqualityComparer<Vector3>
{
    public int GetHash(string inputString)
    {
        MD5 md5Hash = MD5.Create();
        // Convert the input string to a byte array and compute the hash.
        byte[] bytes = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(inputString));

        /*
        HashAlgorithm algorithm = SHA256.Create();

        byte[] bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));

        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        */
            
        return BitConverter.ToInt32(bytes, 0);
    }

   public bool Equals(Vector3 x, Vector3 y)
    {
        if (x.GetHashCode() == y.GetHashCode() || (x - y).sqrMagnitude < 0.001f || x.ToString("F4") == y.ToString("F4"))
            return true;
        else
        {
            bool equalx = Stringify(x.x) == Stringify(y.x), equaly = Stringify(x.y) == Stringify(y.y), equalz = Stringify(x.z) == Stringify(y.z);

            if (equalx && equaly && equalz)
                return true;
        }
        return false;
    }

    public int GetHashCode(Vector3 obj)
    {
        return GetHash(obj.ToString("F4"));
    }

    string Stringify(float x)
    {
        return x.ToString("F4").Replace(" ","").Replace("(", "").Replace(")", "");
    }
}
