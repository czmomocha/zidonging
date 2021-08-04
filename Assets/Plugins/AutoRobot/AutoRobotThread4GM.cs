#if AUTO_ROBOT
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;

namespace AutoRobot
{
    public class AutoRobotThread4GM
    {
        public static AutoRobotThread4GM instance = null;
        public static bool isConnectionSuccessful = false;
        public static Socket clientSocket = null;
        public static byte[] recvMessageBuffer = new byte[128 * 1024];
        public static AutoMsg msg = new AutoMsg();
        public static int receiveSize = 0;

        public string svrIpAddress = "61.241.53.11";  // Commander平台网址 http://commander.oa.com/mywork/index
        public int port = 33001;
        public string project = "TestGame";
        public string runType = "";
        public string uuid = "";
        public string data = "";
        public string backMessage = "";
        public Queue<string> commandQueue4GM = new Queue<string>();

        public static AutoRobotThread4GM GetInstance()
        {
            if (instance == null)
            {
                instance = new AutoRobotThread4GM();
            }

            return instance;
        }


        public void CreateTCPConnection()
        {
            IPAddress ip = IPAddress.Parse(svrIpAddress);
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                GernerateDeviceUid();
                Debug.Log("uuid: " + uuid);
                clientSocket.BeginConnect(new IPEndPoint(ip, port), new AsyncCallback(ConnectionCallBackMethod), clientSocket);
            }
            catch
            {
                Debug.LogError("Connect CommanderSvr failed...");

                return;
            }

            BeginReceiveMessage();

        }

        private static void ConnectionCallBackMethod(IAsyncResult asyncresult)
        {
            Socket tcpclient = (Socket) asyncresult.AsyncState;
            try
            {
                if (tcpclient != null)
                {
                    tcpclient.EndConnect(asyncresult);
                    isConnectionSuccessful = true;
                    AutoRobotThread4GM.GetInstance().SendMessage(WebCmd.CmdDeviceInfo);
                }
            }
            catch (Exception ex)
            {
                isConnectionSuccessful = false;
                Debug.Log(ex.ToString());
                AutoRobotThread4GM.GetInstance().CloseSocket();
            }
        }


        public void BeginReceiveMessage()
        {
            if (clientSocket == null)
            {
                return;
            }

            try
            {
                Thread heartbeat = new Thread(new ThreadStart(TickSender));
                heartbeat.Start();

                clientSocket.BeginReceive(recvMessageBuffer, 0, recvMessageBuffer.Length, 0, new AsyncCallback(ReceiveCallBack), clientSocket);
            }
            catch (System.Exception ex)
            {
                Debug.Log(ex.ToString());
            }
        }

        public static void ReceiveCallBack(IAsyncResult asyncresult)
        {
            Socket receiverSocket = (Socket) asyncresult.AsyncState;
            try
            {
                SocketError error;
                int byteRead = receiverSocket.EndReceive(asyncresult, out error);

                if (byteRead > 0)
                {
                    //获取数据长度;
                    if (msg.hd.len == 0)
                    {
                        msg.hd.cmd = BitConverter.ToInt32(recvMessageBuffer, 0);
                        msg.hd.len = BitConverter.ToInt32(recvMessageBuffer, 4);
                        Debug.Log("receive data length = " + msg.hd.len);

                        //第一次接收，除包头外全部拷贝
                        Buffer.BlockCopy(recvMessageBuffer, 8, msg.data, 0, byteRead - 8);
                        receiveSize = byteRead - 8;
                    }
                    else
                    {
                        Buffer.BlockCopy(recvMessageBuffer, 0, msg.data, receiveSize, byteRead);
                        receiveSize += byteRead;
                    }

                    if (receiveSize >= msg.hd.len)
                    {
                        if (msg.hd.cmd == 0x15)
                        {
                            AutoRobotThread4GM.GetInstance().PhaseWebCommand(msg);
                        }

                        //接收完毕，重置 msg
                        receiveSize = 0;
                        msg.ReSet();
                        Array.Clear(recvMessageBuffer, 0, recvMessageBuffer.Length);
                        receiverSocket.BeginReceive(recvMessageBuffer, 0, recvMessageBuffer.Length, 0, new AsyncCallback(ReceiveCallBack), clientSocket);
                    }
                    else
                    {
                        int needsize = msg.hd.len - receiveSize;
                        int recvsize = needsize < recvMessageBuffer.Length ? needsize : recvMessageBuffer.Length;
                        receiverSocket.BeginReceive(recvMessageBuffer, 0, recvsize, 0, new AsyncCallback(ReceiveCallBack), clientSocket);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.Log(ex.ToString());
                AutoRobotThread4GM.GetInstance().CloseSocket();
            }
        }

        public void SendData(Head hd, string jsondata)
        {
            if (clientSocket == null)
            {
                return;
            }

            if (!String.IsNullOrEmpty(jsondata))
            {
                byte[] sendByteData = new byte[hd.len + 8];
                byte[] CmdBytes = BitConverter.GetBytes(hd.cmd);
                byte[] LenBytes = BitConverter.GetBytes(hd.len);

                Buffer.BlockCopy(CmdBytes, 0, sendByteData, 0, 4);
                Buffer.BlockCopy(LenBytes, 0, sendByteData, 4, 4);
                Buffer.BlockCopy(System.Text.Encoding.Default.GetBytes(jsondata), 0, sendByteData, 8, hd.len);
                try
                {
                    clientSocket.BeginSend(sendByteData, 0, sendByteData.Length, 0, new AsyncCallback(SendCallback), clientSocket);
                    Debug.Log("send msg success...");
                }
                catch (System.Exception ex)
                {
                    Debug.Log(ex.ToString());
                }
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
                AutoRobotThread4GM.GetInstance().CloseSocket();
            }
        }

        public void SetBackMessage(string value)
        {
            backMessage = value;
        }

        public void SendMessage(WebCmd cmd)
        {
            if (uuid.Equals(""))
            {
                return;
            }

            string data = "";
            Head hd = new Head();
            if (cmd == WebCmd.CmdHeartBeat)
            {
                HeartBeat bt = new HeartBeat();
                TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
                long unixTime = Convert.ToInt64(ts.TotalMilliseconds);
                bt.timestamp = unixTime.ToString();
                data = bt.ToJson();
                hd.cmd = 0x0019;
                hd.len = data.Length;
                SendData(hd, data);
            }

            if (cmd == WebCmd.CmdDeviceInfo)
            {
                DeviceInfo dt = new DeviceInfo();
                dt.gameId = project;
                dt.name = project;
                dt.uid = uuid;
#if UNITY_IOS
                dt.os = "ios";
#elif UNITY_ANDROID
                dt.os = "android";
#elif UNITY_STANDALONE_WIN
                dt.os = "windows";
#endif
                data = dt.ToJson();
                hd.cmd = 0x0010;
                hd.len = data.Length;
                SendData(hd, data);
            }

            if (cmd == WebCmd.CmdCommandInfo)
            {
                CommandInfo cm = new CommandInfo();
                cm.devid = uuid;
                cm.project = project;
                cm.backmessage = backMessage;
                data = cm.ToJson();
                hd.cmd = 0x0018;
                hd.len = data.Length;
                SendData(hd, data);
            }
        }


        public void CloseSocket()
        {
            try
            {
                if (clientSocket != null)
                {
                    clientSocket.Close();
                    clientSocket = null;

                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError(ex.Message + " " + ex.StackTrace);
            }
        }


        public void GernerateDeviceUid()
        {
            if (clientSocket == null)
            {
                return;
            }

#if UNITY_EDITOR
            string filepath = "Assets/Scripts/AutoRobot/Resource/";
#elif UNITY_ANDROID || UNITY_IOS
            string filepath = Application.persistentDataPath + "/AutoRobot/";
#endif
            string filefullname = filepath + "uuid.txt";

            if (File.Exists(filefullname))
            {
                string[] info = File.ReadAllLines(filefullname);
                uuid = info[0];

                return;
            }

            string uid = SystemInfo.deviceUniqueIdentifier;
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] retVal = md5.ComputeHash(System.Text.Encoding.Default.GetBytes(uid));
            StringBuilder strbul = new StringBuilder();
            for (int i = 0; i < retVal.Length; i++)
            {
                strbul.Append(retVal[i].ToString("x2"));  //加密结果"x2"结果为32位

            }

            uuid = strbul.ToString();
        }

        public string PhaseWebCommand(AutoMsg msg)
        {
            byte[] data = new byte[msg.hd.len];
            string ret = "";
            Buffer.BlockCopy(msg.data, 0, data, 0, msg.hd.len);
            string cmdstr = System.Text.Encoding.UTF8.GetString(data);
            try
            {
                JObject jo = (JObject) JsonConvert.DeserializeObject(cmdstr);
                runType = jo["lang"].ToString();
                this.data = jo["data"].ToString();
                if (runType == "text")
                {
                    commandQueue4GM.Enqueue(this.data);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError(ex.Message + " " + ex.StackTrace);
            }

            return ret;
        }

        public void TickSender()
        {
            while (true)
            {
                if (clientSocket == null)
                {
                    break;
                }

                if (isConnectionSuccessful)
                {
                    SendMessage(WebCmd.CmdHeartBeat);
                    Debug.Log("Heart Beat!");
                    Thread.Sleep(5000);
                }
            }
        }
    }

    public enum WebCmd
    {
        CmdHeartBeat = 1,  // 心跳
        CmdDeviceInfo = 2,  // 设备信息
        CmdCommandInfo = 3,  // 命令
    }

    public class HeartBeat
    {
        public string msg = "ping";
        public string timestamp = "";
        public string ToJson()
        {
            string jsonstr = JsonConvert.SerializeObject(this);

            return jsonstr;
        }
    }

    public class DeviceInfo
    {
        public string gameId = "";
        public string uid = "";
        public string encoding = "utf-8";
        public string script_lang = "text";
        public string safaia_version = "1.4.7";
        public string instruction_set = "owner";
        public string name = "";
        public string engine_name = "U3D";
        public string interpreter_url = "None";
        public string os = "Windows";
        public string ToJson()
        {
            string jsonstr = JsonConvert.SerializeObject(this);

            return jsonstr;
        }

    }

    public class CommandInfo
    {
        public string type = "REQ";
        public string devid = "";
        public string project = "";
        public string backmessage = "";
        public string ToJson()
        {
            string jsonstr = JsonConvert.SerializeObject(this);

            return jsonstr;
        }
    }
}
#endif
