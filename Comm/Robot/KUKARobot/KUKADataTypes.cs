using System;

namespace AdaWeldSystem.Comm.Robot.KUKARobot
{
    /// <summary>
    /// RSI 数据类型枚举
    /// </summary>
    public enum RSIDataType
    {
        BOOL,
        DOUBLE,
        LONG,
        STRING
    }

    /// <summary>
    /// EKI 数据类型枚举
    /// </summary>
    public enum EKIDataType
    {
        STRING,
        REAL,
        INT,
        BOOL,
        FRAME,
        BYTE,
        STREAM
    }

    /// <summary>
    /// EKI 运行模式枚举
    /// </summary>
    public enum EKIMode
    {
        Client,
        Server
    }

    /// <summary>
    /// EKI 环境枚举
    /// </summary>
    public enum EKIEnvironment
    {
        Program,
        Submit,
        System
    }

    /// <summary>
    /// RSI 错误类型枚举
    /// </summary>
    public enum RSIErrorType
    {
        ConnectionFailed,
        IPOMismatch,
        XMLSerializationError,
        XMLDeserializationError,
        NetworkTimeout,
        Unknown
    }

    /// <summary>
    /// 笛卡尔位姿结构
    /// </summary>
    public struct KUKAPosition
    {
        public double X;
        public double Y;
        public double Z;
        public double A;
        public double B;
        public double C;

        public KUKAPosition(double x, double y, double z, double a, double b, double c)
        {
            X = x;
            Y = y;
            Z = z;
            A = a;
            B = b;
            C = c;
        }

        public override string ToString()
        {
            return string.Format("X={0:F3}, Y={1:F3}, Z={2:F3}, A={3:F3}, B={4:F3}, C={5:F3}", X, Y, Z, A, B, C);
        }
    }

    /// <summary>
    /// 坐标系结构（EKI FRAME 类型）
    /// </summary>
    public struct KUKAFrame
    {
        public double X;
        public double Y;
        public double Z;
        public double A;
        public double B;
        public double C;

        public KUKAFrame(double x, double y, double z, double a, double b, double c)
        {
            X = x;
            Y = y;
            Z = z;
            A = a;
            B = b;
            C = c;
        }

        public override string ToString()
        {
            return string.Format("X={0:F3}, Y={1:F3}, Z={2:F3}, A={3:F3}, B={4:F3}, C={5:F3}", X, Y, Z, A, B, C);
        }
    }

    /// <summary>
    /// 机器人状态数据（RSI 发送方向）
    /// </summary>
    public class KUKARobotData
    {
        public KUKAPosition RIst;
        public KUKAPosition RSol;
        public double[] AIPos;
        public double[] ASPos;
        public double[] EIPos;
        public double[] ESPos;
        public double[] MACur;
        public double[] MECur;
        public double[] TechC;
        public double[] TechT;
        public long Delay;
        public string EStr;
        public ulong IPOC;

        public KUKARobotData()
        {
            AIPos = new double[6];
            ASPos = new double[6];
            EIPos = new double[6];
            ESPos = new double[6];
            MACur = new double[6];
            MECur = new double[6];
            TechC = new double[6];
            TechT = new double[6];
            EStr = string.Empty;
        }
    }

    /// <summary>
    /// 传感器修正数据（RSI 接收方向）
    /// </summary>
    public class KUKACorrectionData
    {
        public KUKAPosition RKorr;
        public double[] AK;
        public double[] EK;
        public long DiO;
        public ulong IPOC;

        public KUKACorrectionData()
        {
            AK = new double[6];
            EK = new double[6];
        }
    }

    /// <summary>
    /// EKI 状态结构
    /// </summary>
    public struct EKIStatus
    {
        public int Buff;
        public int Read;
        public int MsgNo;
        public bool Con;
    }

    /// <summary>
    /// RSI 数据接收事件参数
    /// </summary>
    public class RSIDataEventArgs : EventArgs
    {
        public KUKARobotData RobotData;
        public KUKACorrectionData CorrectionData;
        public ulong IPOC;
        public DateTime Timestamp;
    }

    /// <summary>
    /// RSI 错误事件参数
    /// </summary>
    public class RSIErrorEventArgs : EventArgs
    {
        public string ErrorMessage;
        public Exception Exception;
        public RSIErrorType ErrorType;
    }

    /// <summary>
    /// EKI 数据接收事件参数
    /// </summary>
    public class EKIDataEventArgs : EventArgs
    {
        public string XmlData;
        public DateTime Timestamp;
    }

    /// <summary>
    /// EKI 连接状态变更事件参数
    /// </summary>
    public class EKIConnectionEventArgs : EventArgs
    {
        public bool IsConnected;
        public string ConnectionName;
        public DateTime Timestamp;
    }

    /// <summary>
    /// EKI 错误事件参数
    /// </summary>
    public class EKIErrorEventArgs : EventArgs
    {
        public string ErrorMessage;
        public Exception Exception;
        public string ConnectionName;
    }

    /// <summary>
    /// KUKA 数据接收事件参数（统一管理层）
    /// </summary>
    public class KUKADataReceivedEventArgs : EventArgs
    {
        public KUKARobotData RobotData;
        public KUKACorrectionData CorrectionData;
        public DateTime Timestamp;
    }

    /// <summary>
    /// KUKA 连接状态变更事件参数（统一管理层）
    /// </summary>
    public class KUKAConnectionEventArgs : EventArgs
    {
        public bool IsRSIConnected;
        public bool IsEKIConnected;
        public DateTime Timestamp;
    }
}