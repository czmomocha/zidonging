#if AUTO_ROBOT
namespace AutoRobot
{
    /**
     * AutoRobot与python通信的Cmd
     */
    public enum Cmd
    {
        TestFuncCmd = 10000,
    } 
    
    /**
     * 跟python脚本通信的错误码，复用业务的错误码
     * 另外需要额外约定的错误码定义在此处
     */
    public enum AutoRobotErrCode
    {
        Success = 0,
        UnknownError = 1010001,
    } 
}
#endif
