using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System;

public class ObjManager
{
    public static string MeshToString(Mesh m)
    {
        Vector3[] vertices = m.vertices;
        Vector2[] uvs = m.uv;
        int[] polygons = m.triangles;
        int nvert = 3;

        return Stringify(vertices, uvs, polygons, nvert);
    }

    public static string Stringify(Vector3[] vertices, Vector2[] uvs, int[] polygons, int nvert)
    {
        StringBuilder sb = new StringBuilder();
        bool hasuv = uvs != null;

        sb.Append("g ").Append("mesh").Append("\n\n");
        foreach (Vector3 v in vertices)
        {
            string row = string.Format("v {0} {1} {2}\n", v.x, v.y, v.z).Replace(",", ".");
            sb.Append(row);
        }
        sb.Append("\n");

        if (hasuv)
        {
            foreach (Vector3 v in uvs)
            {
                string row = string.Format("vt {0} {1} {2}\n", v.x, v.y, v.z).Replace(",", ".");
                sb.Append(row);
            }
            sb.Append("\n");
        }

        for (int i = 0; i < polygons.Length; i += nvert)
        {
            sb.Append("f");
            for (int j = 0; j < nvert; j++)
                sb.Append(string.Format(" {0}/{0}", (polygons[i + j] + 1)));

            sb.Append("\n");
        }
        return sb.ToString();
    }

    public static void MeshToFile(Mesh mf, string filename)
    {
        WriteFile(filename, MeshToString(mf));
    }

    public static string DcelToString(DCEL dcel)
    {
        Vector3[] vertices, normals;
        int[] polygons;
        int nvert;

        dcel.Export(out vertices, out normals, out polygons, out nvert);

        return Stringify(vertices, null, polygons, nvert);
    }

    public static void DcelToFile(DCEL dcel, string filename)
    {
        WriteFile(filename, DcelToString(dcel));
    }

    public static DCEL FileToDcel(string filename, int angleThresh)
    {
        DCEL dcel;
        int nvert = 0, npol = 0;
        List<Vector3> lvertices = new List<Vector3>(), lpolygons = new List<Vector3>(), lnormals = new List<Vector3>();
        List<Vector2> luvs = new List<Vector2>();
        Vector3[] vertices = null, polygons, normals = null;
        bool hasnormals = false, hasuv = false;

        var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
        using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
        {
            string line;
            while ((line = streamReader.ReadLine()) != null)
            {
                string[] words = line.Split(' ');
                if (words[0] == "v")
                {
                    lvertices.Add(new Vector3(float.Parse(words[1]), float.Parse(words[2]), float.Parse(words[3])));
                }
                if (words[0] == "vn")
                {
                    lnormals.Add(new Vector3(float.Parse(words[1]), float.Parse(words[2]), float.Parse(words[3])));
                }
                if (words[0] == "vt")
                {
                    luvs.Add(new Vector3(float.Parse(words[1]), float.Parse(words[2])));
                }
                if (words[0] == "f")
                {
                    Vector3 normal = Vector3.zero, expectedNormal;

                    if (vertices == null)
                    {
                        vertices = lvertices.ToArray();
                        nvert = words.Length - 1;
                    }
                    if (normals == null)
                        normals = lnormals.ToArray();

                    for (int i = 1; i < nvert + 1; i++)
                    {
                        Vector3 vertex;
                        if (words[i].Contains("/"))
                        {
                            hasnormals = words[i].Contains("/");
                            string[] tuple = words[i].Split('/');
                            hasuv = tuple[1] != "";
                            hasnormals = tuple[2] != "";
                            vertex = vertices[int.Parse(tuple[0]) - 1];
                            normal += vertex;
                            /*if (!hasuv)
                                throw new Exception("this mesh doesn't have uvs");
                            luvs.Add(uvs[int.Parse(tuple[3]) - 1]);*/
                        }
                        else
                        {
                            vertex = vertices[int.Parse(words[i]) - 1];
                        }
                        lpolygons.Add(vertex);
                        npol++;
                    }
                    if (hasnormals)
                    {
                        normal = normal.normalized;
                        expectedNormal = Vector3.Cross(lpolygons[npol - nvert + 1] - lpolygons[npol - nvert],
                            lpolygons[npol - nvert + 2] - lpolygons[npol - nvert + 1]);
                        //If dot is positive, then vertices are in counterclockwise order
                        if (Vector3.Dot(expectedNormal, normal) > 0)
                        {
                            Debug.Log("counterclockwise");
                            lpolygons.Reverse(npol - nvert, nvert);
                            if (hasuv)
                                luvs.Reverse(npol - nvert, nvert);
                        }
                        else
                            Debug.Log("clockwise");
                    }
                }
            }
        }

        polygons = lpolygons.ToArray();

        if (nvert == 3)
            dcel = new TriDCEL(polygons, angleThresh);
        else
            dcel = new QuadDCEL(polygons, angleThresh);

        return dcel;
    }

#if !WINDOWS_UWP
    public static void WriteFile(string filename, string text)
    {
        string path = Application.persistentDataPath + "/" + filename + ".obj";
        using (StreamWriter sw = new StreamWriter(path))
        {
            sw.Write(text);
        }
        Debug.Log(path);
    }
#else
    public static async void WriteFile(string filename, string text)
    {
        Windows.Storage.StorageFolder storageFolder =
        Windows.Storage.ApplicationData.Current.LocalFolder;
        Windows.Storage.StorageFile file = await storageFolder.CreateFileAsync(filename, Windows.Storage.CreationCollisionOption.ReplaceExisting);
        Windows.Storage.FileIO.WriteTextAsync(file, text);
    }
#endif
}