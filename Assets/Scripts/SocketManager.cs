using UnityEngine;
using System.Net;
using System.Threading;
using System;


#if WINDOWS_UWP
using System.IO;
using Windows.Networking.Sockets;
#else
using System.Net.Sockets;
#endif


public class SocketManager : MonoBehaviour
{
    public static SocketManager Instance { get; private set; }
    // test hololens
#if !UNITY_EDITOR
    private const string HOST = "10.132.0.133";
#else
    private const string HOST = "127.0.0.1";
#endif
    private const int PORTIN = 9000;
    private const int PORTOUT = 9001;

    IConcreteSocketManager manager = null;
    private String lastInfo = "";

    // Use this for initialization
    void Start()
    {
        Instance = this;
        //returnData = null;
#if WINDOWS_UWP
        manager = new UWPSocketManager();
        
#else
        manager = new DotNetSocketManager();
#endif

        manager.init(HOST, PORTIN, PORTOUT);

#if WINDOWS_UWP
        InvokeRepeating("UpdateParameters", 1.0f, 1.0f);
#else
        manager.OnDataReceived += Manager_OnDataReceived;
#endif
    }


    /// <summary>
    /// Questo metodo invia un messaggio msg al simulatore
    /// </summary>
    /// <param name="msg">Il messaggio da inviare che deve essere formattato come 
    /// deciso nel file .xml 
    /// </param>
    public void SendUdpDatagram(string msg)
    {
        manager.SendUdpDatagram(msg);
    }

    public string getData()
    {
        return manager.getData();
    }

    private void OnApplicationQuit()
    {
        manager.quit();
    }

    private void Manager_OnDataReceived(object sender, EventArgs e)
    {
        UpdateParameters();
    }

    void UpdateParameters()
    {
        string data = manager.getData();
        if (data != null && data != "")
        {
            lastInfo = data;
        }
            
        /*if (data != null)
        {
            if (!InputSequence.Instance.flag)
            {
                Parameters.Instance.set_new_parameters(data);
                InputSequence.Instance.checkNextSeq();
            }
        }*/
    }

    public String GetLastInfo()
    {
        return lastInfo;
    }
}

interface IConcreteSocketManager
{
    void init(string host, int inputPort, int outputPort);
    void SendUdpDatagram(string msg);
    void quit();
    string getData();
    event EventHandler OnDataReceived;

}

#if !WINDOWS_UWP
public class DotNetSocketManager : IConcreteSocketManager
{
    UdpClient outputSocket;
    Socket inputSocket;
    IPEndPoint inputEndPoint;
    IPEndPoint outputEndPoint;
    bool stop = false;
    private string returnData;
    Thread t;

    public event EventHandler OnDataReceived;

    public string getData()
    {
        return returnData;
    }

    public void init(string host, int inputPort, int outputPort)
    {
        // input socket
        inputSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPAddress serverAddr = IPAddress.Parse(host);
        inputEndPoint = new IPEndPoint(serverAddr, inputPort);
        SendUdpDatagram("Leap start");

        // output socket
        outputSocket = new UdpClient(outputPort);
        outputEndPoint = new IPEndPoint(IPAddress.Any, 0);
        //outputEndPoint = new IPEndPoint(serverAddr, outputPort);
        receiveData();
    }

    public void quit()
    {
        stop = true;
        if (t != null)
        {
            t.Abort();
        }
    }

    public void SendUdpDatagram(string msg)
    {
        byte[] byteMsg = System.Text.Encoding.ASCII.GetBytes(msg);
        inputSocket.SendTo(byteMsg, inputEndPoint);
    }

    private void receiveData()
    {

        t = new Thread(() =>
        {
            while (!stop)
            {
                byte[] buff = outputSocket.Receive(ref outputEndPoint);
                returnData = System.Text.Encoding.ASCII.GetString(buff, 0, buff.Length);
                if (this.OnDataReceived != null)
                {
                    this.OnDataReceived(this, new EventArgs());
                }
            }
        });
        t.Start();
    }

}
#endif

#if WINDOWS_UWP
public class UWPSocketManager : IConcreteSocketManager
{
    DatagramSocket outputSocket;
    DatagramSocket inputSocket;
    StreamWriter inputWriter;
    private string returnData;

    public event EventHandler OnDataReceived;

    public string getData()
    {
        return returnData;
    }

    public void init(string host, int inputPort, int outputPort)
    {
        this.initSockets(host, inputPort, outputPort);
    }

    private async void initSockets(string host, int inputPort, int outputPort)
    {
        // input socket
        inputSocket = new DatagramSocket();
        Windows.Networking.HostName serverAddr = new Windows.Networking.HostName(host);
        Stream streamOut = (await inputSocket.GetOutputStreamAsync(serverAddr, "" + inputPort)).AsStreamForWrite();
        inputWriter = new StreamWriter(streamOut);
        Debug.Log("Sending starting message to " + serverAddr + ":" + inputPort);
        SendUdpDatagram("Leap start");

        // output socket
        outputSocket = new DatagramSocket();
        outputSocket.MessageReceived += Socket_MessageReceived;

        try
        {
            await outputSocket.BindServiceNameAsync("" + outputPort);
            Debug.Log("Starting listening on port " + outputSocket.Information.LocalPort);
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            Debug.Log(Windows.Networking.Sockets.SocketError.GetStatus(e.HResult).ToString());
            return;
        }
    }

    public void quit()
    {
        throw new NotImplementedException();
    }

    public async void SendUdpDatagram(string msg)
    {
        try
        {
            await inputWriter.WriteAsync(msg);
            await inputWriter.FlushAsync();
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            Debug.Log(Windows.Networking.Sockets.SocketError.GetStatus(e.HResult).ToString());
            return;
        }
    }

    private async void Socket_MessageReceived(Windows.Networking.Sockets.DatagramSocket sender,
       Windows.Networking.Sockets.DatagramSocketMessageReceivedEventArgs args)
    {
        //Read the message that was received from the UDP echo client.

        try
        {
            Stream streamIn = args.GetDataStream().AsStreamForRead();
            StreamReader reader = new StreamReader(streamIn);
            returnData = await reader.ReadLineAsync();
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            Debug.Log(Windows.Networking.Sockets.SocketError.GetStatus(e.HResult).ToString());
            return;
        }
    }

    void RaiseUpdate()
    {
        if (this.OnDataReceived != null)
        {
            this.OnDataReceived(this, new EventArgs());
        }
    }
}
#endif
