#if AUTO_ROBOT

using System.Threading;
using XLua;
using UnityEngine;
using Newtonsoft.Json;
using System;

namespace AutoRobot
{
    [LuaCallCSharp]
    public class AutoRobotInterface
    {
        public static void TestFunc(string msg)
        {
            Debug.Log("static Func TestFunc has been called...");
            Debug.Log("msg: " + msg);
            AutoRobotThread.GetInstance().SendBackMessage(Cmd.TestFuncCmd, "success");
        }
    }
}
#endif
