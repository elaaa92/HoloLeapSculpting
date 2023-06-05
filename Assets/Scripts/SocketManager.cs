using UnityEngine;
using System.Net;
using System;
using System.Collections.Generic;


#if WINDOWS_UWP
using System.IO;
using Windows.Networking.Sockets;
#else
using System.Net.Sockets;
#endif


public class SocketManager : MonoBehaviour
{
    public static SocketManager Instance { get; private set; }

    private const string HOST = "127.0.0.1";
    private const int PORTIN = 9004;
    private const int PORTOUT = 9005;

    IConcreteSocketManager manager = null;

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

        manager.init(HOST, PORTIN, PORTOUT, 700);

#if !WINDOWS_UWP
        if(Application.isPlaying)
            InvokeRepeating("ListenForAWhile", 0.1f, 0.5f);
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

    public List<string> getDataBuffer()
    {
        return manager.getDataBuffer();
    }

    public string getData()
    {
        return manager.getData();
    }

    public long getStartDate()
    {
        return manager.getStartDate();
    }

#if !WINDOWS_UWP
    public void ListenForAWhile()
    {
        manager.ReceiveData();
    }
#endif

    public void SendPacket(string msg, long timestamp)
    {
        manager.StartSendingPacket(msg, timestamp);
    }
}

interface IConcreteSocketManager
{
    void init(string host, int inputPort, int outputPort, long respDelayTol);
    void SendUdpDatagram(string msg);
    string getData();
    List<string> getDataBuffer();
    long getStartDate();
    void StartSendingPacket(string msg, long timestamp);
#if !WINDOWS_UWP
    void ReceiveData();
#endif

}

#if !WINDOWS_UWP
public class DotNetSocketManager : IConcreteSocketManager
{
    UdpClient outputSocket;
    Socket inputSocket;
    IPEndPoint inputEndPoint;
    IPEndPoint outputEndPoint;
    private List<String> returnData;
    private long startDate;
    private long lastReceived;
    private int timer;
    private long respDelay;
    private bool synchronized;
    private bool packetreceived;
    private string packet;
    private long packettimestamp;

    public List<string> getDataBuffer()
    {
        return returnData;
    }

    void updateBuffer(string data)
    {
        lastReceived = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (data.Contains("Leap start"))
            synchronized = true;
        else if (data.Contains("Packet received"))
        {
            packetreceived = true;
            packet = null;
            timer = 0;
        }
        else
        {
            if (returnData.Count > 10000)
                returnData.Clear();
            returnData.Add(data);
        }
        
        if (!synchronized)
            SendWelcomeMessage();

        if (!packetreceived)
        {
            if(timer % 100 == 0)
                SendPacket();
            timer++;
        }
    }

    //Get last message, drop too old ones
    public string getData()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now - lastReceived < respDelay)
        {
            int size = returnData.Count;
            string data = null;
            if (size > 2)
                returnData.RemoveRange(0, size - 2);
            if(size > 0)
                data = returnData[0];
            return data;
        }
        else
        {
            returnData.Clear();
            return null;
        }
    }

    public long getStartDate()
    {
        return this.startDate;
    }

    public void init(string host, int inputPort, int outputPort, long respDelayTol)
    {
        returnData = new List<string>();
        // input socket
        inputSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPAddress serverAddr = IPAddress.Parse(host);
        inputEndPoint = new IPEndPoint(serverAddr, inputPort);

        synchronized = false;
        SendWelcomeMessage();

        // output socket
        outputSocket = new UdpClient(outputPort);
        outputEndPoint = new IPEndPoint(IPAddress.Any, 0);
        outputSocket.Client.ReceiveTimeout = 100;
        respDelay = respDelayTol;
        timer = 0;
    }

    private void SendWelcomeMessage()
    {
        SendUdpDatagram("Leap start");
        this.startDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public void SendUdpDatagram(string msg)
    {
        int bytelength = System.Text.ASCIIEncoding.ASCII.GetByteCount(msg);
        //Each block must contain max 128 bytes
        if (bytelength < 128)
        {
            byte[] byteMsg = System.Text.Encoding.ASCII.GetBytes(msg);
            inputSocket.SendTo(byteMsg, inputEndPoint);
        }
        else
        {
            string times = packettimestamp.ToString();
            int timestamplength = times.Length, strlength = msg.Length, chunksize = 127, nblocks = Mathf.CeilToInt(strlength / (float)chunksize);
            int indexlength = nblocks.ToString().Length, payloadsize;

            nblocks = Mathf.CeilToInt((strlength + (2 * indexlength + timestamplength + 4) * nblocks) / (float)chunksize);
            payloadsize = strlength / nblocks;

            for (int i = 0; i < nblocks; i++)
            {
                string block = "t" + times + " " + i + " " + nblocks + " " + msg.Substring(i * payloadsize, (i * payloadsize + payloadsize <= strlength) ? payloadsize : strlength - i * payloadsize);
                block.PadRight(128, '_');
                byte[] byteMsg = System.Text.Encoding.ASCII.GetBytes(block);
                inputSocket.SendTo(byteMsg, inputEndPoint);
                Debug.Log("sent: " + block.Length + " " + block);
            }
        }

    }

    public void StartSendingPacket(string msg, long timestamp)
    {
        packet = msg;
        packetreceived = false;
        timer = 0;
        packettimestamp = timestamp;
    }

    private void SendPacket()
    {
        SendUdpDatagram(packet);
    }

    public void ReceiveData()
    {
        try
        {
            byte[] buff = outputSocket.Receive(ref outputEndPoint);
            updateBuffer(System.Text.Encoding.ASCII.GetString(buff, 0, buff.Length));
        }
        catch
        {
        }
    }
}
#endif

#if WINDOWS_UWP
public class UWPSocketManager : IConcreteSocketManager
{
    DatagramSocket outputSocket;
    DatagramSocket inputSocket;
    StreamWriter inputWriter;
    private List<string> returnData;
    private long startDate;
    private long lastReceived;
    private int timer;
    private long respDelay;
    private bool synchronized;
    private bool packetreceived;
    private string packet;
    private long packettimestamp;

    public event EventHandler OnDataReceived;

    public List<string> getDataBuffer()
    {
        List<string> buffer = returnData.GetRange(0, returnData.Count);
        returnData.Clear();
        return buffer;
    }

    void updateBuffer(string data)
    {
        lastReceived = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (data.Contains("Leap start"))
        {
            synchronized = true;
        }
        else if (data.Contains("Packet received"))
        {
            packetreceived = true;
            packet = null;
            timer = 0;
        }
        else
        {
            if (returnData.Count > 10000)
                returnData.Clear();
            returnData.Add(data);
        }
        
        if (!synchronized)
            SendWelcomeMessage();

        if (!packetreceived)
        {
            if (timer % 100 == 0)
                SendPacket();
            timer++;
        }
    }

    public string getData()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now - lastReceived < respDelay)
        {
            int size = returnData.Count;
            string data = null;
            if (size > 2)
                returnData.RemoveRange(0, size - 2);
            if (size > 0)
                data = returnData[0];
            return data;
        }
        else
        {
            returnData.Clear();
            return null;
        }
    }

    public long getStartDate()
    {
        return this.startDate;
    }

    public void init(string host, int inputPort, int outputPort, long respDelayTol)
    {
        returnData = new List<string>();
        this.initSockets(host, inputPort, outputPort, respDelayTol);
    }

    private async void initSockets(string host, int inputPort, int outputPort, long respDelayTol)
    {
        // input socket
        inputSocket = new DatagramSocket();
        Windows.Networking.HostName serverAddr = new Windows.Networking.HostName(host);
        Stream streamOut = (await inputSocket.GetOutputStreamAsync(serverAddr, "" + inputPort)).AsStreamForWrite();
        inputWriter = new StreamWriter(streamOut, System.Text.Encoding.ASCII, 128);

        synchronized = false;
        SendWelcomeMessage();

        // output socket
        outputSocket = new DatagramSocket();
        outputSocket.MessageReceived += Socket_MessageReceived;
        respDelay = respDelayTol;
        packetreceived = true;
        timer = 0;

        try
        {
            await outputSocket.BindServiceNameAsync("" + outputPort);
            //Debug.Log("Starting listening on port " + outputSocket.Information.LocalPort);
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            Debug.Log(Windows.Networking.Sockets.SocketError.GetStatus(e.HResult).ToString());
            return;
        }
    }

    private void SendWelcomeMessage()
    {
        SendUdpDatagram("Leap start");
        Debug.Log("Sent leap start");
        this.startDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public void quit()
    {
        throw new NotImplementedException();
    }

    public async void SendUdpDatagram(string msg)
    {
        try
        {
            int bytelength = System.Text.ASCIIEncoding.ASCII.GetByteCount(msg);
            //Each block must contain max 128 bytes
            if (bytelength < 128)
            {
                await inputWriter.WriteAsync(msg);
                await inputWriter.FlushAsync();
            }
            else
            {
                string times = packettimestamp.ToString();
                int timestamplength = times.Length, strlength = msg.Length, chunksize = 127, nblocks = Mathf.CeilToInt(strlength / (float)chunksize);
                int indexlength = nblocks.ToString().Length, payloadsize, headersize = 2 * indexlength + timestamplength + 4, extendedmessagelength;

                extendedmessagelength = strlength + headersize * nblocks;
                nblocks = Mathf.CeilToInt(extendedmessagelength / (float)chunksize);
                payloadsize = Mathf.CeilToInt(strlength / (float) nblocks);
                
                for (int i = 0; i < nblocks; i++)
                {
                    string block = "t" + times + " " + i + " " + nblocks + " " + msg.Substring(i * payloadsize, (i * payloadsize + payloadsize <= strlength) ? payloadsize : strlength - i * payloadsize);
                    block.PadRight(128, '_');
                    await inputWriter.WriteAsync(block);
                    await inputWriter.FlushAsync();
                    //Debug.Log("sent: " + block.Length + " " + block);
                }
            }
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
        }
    }

    public void StartSendingPacket(string msg, long timestamp)
    {
        packet = msg;
        packetreceived = false;
        timer = 0;
        packettimestamp = timestamp;
    }

    private void SendPacket()
    {
        SendUdpDatagram(packet);
    }

    private async void Socket_MessageReceived(Windows.Networking.Sockets.DatagramSocket sender,
       Windows.Networking.Sockets.DatagramSocketMessageReceivedEventArgs args)
    {
        //Read the message that was received from the UDP echo client.

        try
        {
            Stream streamIn = args.GetDataStream().AsStreamForRead();
            StreamReader reader = new StreamReader(streamIn);
            updateBuffer(await reader.ReadLineAsync());
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
