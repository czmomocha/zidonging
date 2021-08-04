#if AUTO_ROBOT
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using AutoRobot;
using XLua;
using System.IO;
using System;

namespace AutoRobot
{

    public class AutoRobotHandle : MonoBehaviour
    {
        public static LuaEnv luaenv = null;
        public static string path = "";

        public static void Init()
        {
            AutoRobotHandle.Start();
        }

        /// <summary>
        /// Start is called before the first frame update
        /// </summary>
        public static void Start()
        {
#if UNITY_EDITOR
            path = "Assets/Plugins/AutoRobot/Resources";
#elif UNITY_ANDROID || UNITY_IOS
            // Application.persistentDataPath points to /storage/emulated/0/Android/data/<packagename>/files
            path = Application.persistentDataPath + "/AutoRobot/Resources";
#endif
            DirectoryInfo dict = new DirectoryInfo(path);
            if (dict.Exists)
            {
                foreach (FileInfo file in dict.GetFiles("*.txt"))
                {
                    if (file.Name == "AutoTest.lua.txt")
                    {
                        luaenv = new LuaEnv();
                        string script = File.ReadAllText(file.FullName);
                        luaenv.DoString(script);
                        break;
                    }
                }
            }

            Debug.Log("AutoRobotHandle.Start has been called...");
            if (IsStartAutoTest())
            {
                GameObject autoTest = new GameObject("autoTest");
                GameObject.DontDestroyOnLoad(autoTest);
                autoTest.hideFlags = HideFlags.HideInHierarchy;
                autoTest.AddComponent<AutoRobotHandle>();
                StartAutoTestThread();

                TickMessage();
            }
            else
            {
                Debug.Log("Do not start AutoRobotThread...");
            }
        }

        public static void StartAutoTestThread()
        {
            AutoRobotThread.GetInstance().CreateTCPConnection();
            AutoRobotThread4GM.GetInstance().CreateTCPConnection();
        }

        /// <summary>
        /// Update is called once per frame
        /// </summary>
        public static void Update()
        {
            if (luaenv != null)
            {
                luaenv.Tick();
            }
        }

        public static void OnDestroy()
        {
            luaenv.Dispose();
        }


        public static void TickMessage()
        {
            Thread msgHandle = new Thread(new ThreadStart(DealMsg));
            msgHandle.Start();
        }

        public static void DealMsg()
        {
            while (true)
            {
                if (AutoRobotThread.GetInstance().serverSocket == null)
                {
                    break;
                }

                if (AutoRobotThread.GetInstance().commandQueue.Count > 0)
                {
                    AutoMsg msg = AutoRobotThread.GetInstance().commandQueue.Dequeue();
                    string command = AutoRobotThread.GetInstance().PhaseCommand(msg);
                    CallLuaFunction(command);
                }

                if (AutoRobotThread4GM.GetInstance().commandQueue4GM.Count > 0)
                {
                    string webcommand = AutoRobotThread4GM.GetInstance().commandQueue4GM.Dequeue();
                    CallLuaFunction(webcommand);
                }
                
                if (AutoRobotThread.GetInstance().sendBackMsgQueue.Count > 0)
                {
                    JsonData msg = null;
                    //Log.Info(LogModules.AUTOROBOT, "AutoRobotThread.GetInstance().sendBackMsgQueue.Count = " + AutoRobotThread.GetInstance().sendBackMsgQueue.Count.ToString());
                    lock (AutoRobotThread.GetInstance().sendBackMsgQueue)
                    {
                        msg = AutoRobotThread.GetInstance().sendBackMsgQueue.Dequeue();
                    }

                    if (msg != null)
                    {
                        //Log.Info(LogModules.AUTOROBOT, "call SendBackMessage cmd: {0}, Value: {1}", msg.Cmd, msg.Value);
                        AutoRobotThread.instance.SendBackMessage((AutoRobot.Cmd) msg.Cmd, msg.Value);
                    }
                }
            }
        }

        public static bool IsStartAutoTest()
        {
            try
            {
                if (luaenv?.Global == null)
                {
                    return false;
                }
                
                LuaTable d = luaenv.Global.Get<LuaTable>("AutoTest");

                if (d != null)
                {
                    return d.Get<bool>("gIsEnableAutoTest");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Exception" + ex);
            }

            return false;
        }

        public static void CallLuaFunction(string commandStr)
        {
            //Log.Info(LogModules.AUTOROBOT, "commandStr = " + commandStr);
            if (luaenv != null)
            {
                try
                {
                    luaenv.DoString(commandStr);
                }
                catch (Exception e)
                {
                    //Debug.Log("CallLuaFunction error: " + commandStr);
                    //Debug.Log("exception info: " + e.ToString());
                }
            }
        }
    }
}
#endif
