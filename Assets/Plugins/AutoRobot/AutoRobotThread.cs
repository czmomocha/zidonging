#if AUTO_ROBOT
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System;
using System.Text;
using Newtonsoft.Json;

namespace AutoRobot
{
    public class AutoRobotThread
    {
        public static AutoRobotThread instance = null;
        public Socket serverSocket = null;
        public Socket connectSocket = null;
        public int port = 8000;
        public Queue<AutoMsg> commandQueue = new Queue<AutoMsg>();
        public Queue<JsonData> sendBackMsgQueue = new Queue<JsonData>(); // 回包消息队列

        public static AutoRobotThread GetInstance()
        {
            if (instance == null)
            {
                instance = new AutoRobotThread();
            }

            return instance;
        }


        public void CreateTCPConnection()
        {
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);

            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            if (serverSocket == null)
            {
                Debug.Log("socket create failed...");

                return;
            }

            try
            {
                serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                ;
                serverSocket.Bind(ipEndPoint);
                serverSocket.Listen(1);

                Debug.Log("Server Start");
                serverSocket.BeginAccept(new AsyncCallback(Accept), serverSocket);

                return;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Exception" + ex);
            }

            return;
        }

        public static void Accept(IAsyncResult result)
        {
            Socket serverSocket = (Socket) result.AsyncState;
            Socket receiverSocket = serverSocket.EndAccept(result);

            StateObject state = new StateObject();
            state.curSocket = receiverSocket;
            AutoRobotThread.GetInstance().connectSocket = state.curSocket;

            receiverSocket.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallBack), state);
            serverSocket.BeginAccept(Accept, serverSocket);
        }

        public static void ReceiveCallBack(IAsyncResult result)
        {
            StateObject state = (StateObject) result.AsyncState;
            Socket receiverSocket = state.curSocket;
            try
            {
                SocketError error;
                int byteRead = receiverSocket.EndReceive(result, out error);

                if (byteRead > 0)
                {
                    //获取数据长度;
                    if (state.datalen == 0)
                    {
                        state.msg.hd.cmd = BitConverter.ToInt32(state.buffer, 0);
                        state.msg.hd.len = BitConverter.ToInt32(state.buffer, 4);
                        state.datalen = state.msg.hd.len;
                        //Logger.d("receive data length = " + state.datalength);

                        Buffer.BlockCopy(state.buffer, 8, state.msg.data, 0, byteRead - 8);
                        state.recvedSize = byteRead - 8;
                    }
                    else
                    {
                        //byte[] dataarr = new byte[byteRead];
                        Buffer.BlockCopy(state.buffer, 0, state.msg.data, state.recvedSize, byteRead);
                        state.recvedSize += byteRead;
                    }

                    if (state.recvedSize >= state.datalen)
                    {
                        state.msg.socket = state.curSocket;
                        AutoRobotThread.GetInstance().commandQueue.Enqueue(state.msg);

                        StateObject newState = new StateObject();
                        newState.curSocket = receiverSocket;

                        receiverSocket.BeginReceive(newState.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallBack), newState);
                    }
                    else
                    {
                        //数据没有接收完，继续接收;
                        int needsize = state.datalen - state.recvedSize;
                        int recvsize = needsize < StateObject.BufferSize ? needsize : StateObject.BufferSize;
                        receiverSocket.BeginReceive(state.buffer, 0, recvsize, 0, new AsyncCallback(ReceiveCallBack), state);
                    }
                }
                else
                {
                    Debug.LogError("Recv socket error,error code = " + error);
                    CloseSocket(receiverSocket);
                }
            }
            catch (Exception ex)
            {
                Debug.Log(ex.Message + " " + ex.StackTrace);
            }


        }

        public void SendBackMessage(Cmd cmd, string value)
        {
            JsonData jd = new JsonData();
            AutoMsg msg = new AutoMsg();
            jd.Cmd = (int) cmd;
            jd.Value = value;
            string jsonstr = JsonConvert.SerializeObject(jd);
            msg.hd.cmd = (int) cmd;
            msg.hd.len = jsonstr.Length;
            msg.socket = connectSocket;
            msg.data = System.Text.Encoding.Default.GetBytes(jsonstr);

            if (!string.IsNullOrEmpty(AutoRobotThread4GM.GetInstance().data))
            {
                AutoRobotThread4GM.GetInstance().SetBackMessage(jsonstr);
                AutoRobotThread4GM.GetInstance().SendMessage(WebCmd.CmdCommandInfo);
                AutoRobotThread4GM.GetInstance().data = "";
            }
            else
            {
                Sendcmd(msg);
            }
        }

        public void Sendcmd(AutoMsg msg)
        {
            byte[] sendByteData = new byte[msg.hd.len + 8];
            byte[] CmdBytes = BitConverter.GetBytes(msg.hd.cmd);
            byte[] LenBytes = BitConverter.GetBytes(msg.hd.len);

            Buffer.BlockCopy(CmdBytes, 0, sendByteData, 0, 4);
            Buffer.BlockCopy(LenBytes, 0, sendByteData, 4, 4);
            Buffer.BlockCopy(msg.data, 0, sendByteData, 8, msg.hd.len);
            try
            {
                if (connectSocket != null)
                {
                    msg.socket.BeginSend(sendByteData, 0, sendByteData.Length, 0, new AsyncCallback(SendCallback), msg.socket);
                }
            }
            catch (System.Exception ex)
            {
                Debug.Log(ex.ToString());
                CloseSocket(msg.socket);
            }
        }

        private static void SendCallback(IAsyncResult result)
        {
            Socket handler = (Socket) result.AsyncState;
            try
            {
                SocketError error;
                int bytesSend = handler.EndSend(result, out error);
                Debug.Log("Sent bytes to client = " + bytesSend);
            }
            catch (System.Exception ex)
            {
                Debug.Log(ex.ToString());
                CloseSocket(handler);
            }
        }

        public static void CloseSocket(Socket socket)
        {
            try
            {
                if (socket != null)
                {
                    socket.Close();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError(ex.Message + " " + ex.StackTrace);
            }
        }

        public string PhaseCommand(AutoMsg msg)
        {
            //获得Json数组：一个Json文件根目录可能有多个类。
            byte[] data = new byte[msg.hd.len];
            string ret = "";
            Buffer.BlockCopy(msg.data, 0, data, 0, msg.hd.len);
            string cmdstr = System.Text.Encoding.UTF8.GetString(data);
            try
            {
                JsonData jd = (JsonData) JsonConvert.DeserializeObject(cmdstr, typeof(JsonData));
                if (jd != null)
                {
                    string commande = jd.Value;

                    return commande;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError(ex.Message + " " + ex.StackTrace);
            }

            return ret;
        }
    }

    public class Head
    {
        public int cmd = 0;
        public int len = 0;
    }

    public class AutoMsg
    {
        public Head hd = new Head();
        public Socket socket = null;
        public byte[] data = new byte[1024 * 128]; 
        public void ReSet()
        {
            hd.cmd = 0;
            hd.len = 0;
            Array.Clear(data, 0, 1024 * 128);
        }

    }

    public class JsonData
    {
        public int Cmd { get; set; }
        public string Value { get; set; }
    }

    public class StateObject
    {
        public Socket curSocket = null;
        public AutoMsg msg = new AutoMsg();
        public const int BufferSize = 1024 * 128;
        public byte[] buffer = new byte[BufferSize];
        public StringBuilder stringBuilder = new StringBuilder();
        public int recvedSize = 0;  //已经接收的字符长度
        public int datalen = 0;  //接收的总长度
        public int offset = 0;
    }
}
#endif
