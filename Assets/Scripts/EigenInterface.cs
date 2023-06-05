using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class EigenInterface {
    public const int MAXCOLS = 16;
    public const int MAXROWS = 800;
    public const String path = "";

    [DllImport("kernel32")]
    public static extern IntPtr LoadLibrary(string dllToLoad);

#if UNITY_EDITOR

    [DllImport("Eigen.dll", EntryPoint = "solveSystem")]
    unsafe static extern float* solveSystem(float* A, float* b, int nrows, int ncols);

    [DllImport("Eigen.dll", EntryPoint = "releaseMemory")]
    unsafe static extern void releaseMemory(float* p);

    [DllImport("Eigen.dll", EntryPoint = "umeyama")]
    unsafe static extern float* umeyama(float* L, float* H, int nrows, int ncols, bool resize);
#else
    [DllImport("Eigen.dll", EntryPoint = "solveSystem")]
    unsafe static extern float* solveSystem(float* A, float* b, int nrows, int ncols);

    [DllImport("Eigen.dll", EntryPoint = "releaseMemory")]
    unsafe static extern void releaseMemory(float* p);

    [DllImport("Eigen.dll", EntryPoint = "umeyama")]
    unsafe static extern float* umeyama(float* L, float* H, int nrows, int ncols, bool resize);
#endif

    public unsafe struct LinearSystem
    {
        public fixed float A[MAXCOLS * MAXROWS];
        public fixed float b[MAXROWS];
        public int nrows;
        public int ncols;

        public unsafe float[] Result
        {
            get
            {
                fixed (float* ptrA = A, ptrB = b)
                {
                    float* ptrX = solveSystem(ptrA, ptrB, nrows, ncols);
                    float[] Xarray = new float[ncols+1];

                    Marshal.Copy((IntPtr)ptrX, Xarray, 0, ncols+1);

                    //Deallocare memoria in c++
                    releaseMemory(ptrX);
                    return Xarray;
                }
            }
        }
    }

    public unsafe struct UmeyamaStruct
    {
        public fixed float H[MAXROWS];
        public fixed float L[MAXROWS];
        public int nrows;
        public int ncols;
        public bool resize;

        public unsafe float[] Result
        {
            get
            {
                fixed (float* ptrH = H, ptrL = L)
                {
                    float* ptrM = umeyama(ptrL, ptrH, nrows, ncols, resize);
                    float[] Marray = new float[16];

                    Marshal.Copy((IntPtr)ptrM, Marray, 0, 16);

                    //Deallocare memoria in c++
                    releaseMemory(ptrM);
                    return Marray;
                }
            }
        }
    }

    public unsafe static float[] SolveSystem(float[] A, float[] b)
    {
        int nrows = b.Length, ncols = A.Length / nrows;
        LinearSystem system;
        for (int i = 0; i < (nrows * ncols); i++)
            system.A[i] = A[i];
        for (int i = 0; i < nrows; i++)
            system.b[i] = b[i];
        system.nrows = nrows;
        system.ncols = ncols;

        return system.Result;
    }

    public unsafe static float[] Umeyama(float[] H, float[] L, bool resize)
    {
        int nrows = 3, ncols = H.Length/nrows;
        UmeyamaStruct ustruct;
        for (int i = 0; i < (nrows * ncols); i++)
        { 
            ustruct.H[i] = H[i];
            ustruct.L[i] = L[i];
        }
        ustruct.nrows = nrows;
        ustruct.ncols = ncols;
        ustruct.resize = resize;

        return ustruct.Result;
    }

    /*public static void Start()
    {
        float[] A = { 2, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 2 };
        float[] b = { 1, 1, 1, 1, 1 };
        float[] x = SolveSystem(A, b);

        string solution = "";
        foreach (float el in x)
            solution += el.ToString() + " ";
        Debug.Log("Solution: " + solution);
    }*/
}
