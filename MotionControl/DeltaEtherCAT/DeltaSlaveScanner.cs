using System.Collections.Generic;
using AdaWeldSystem.MotionControl.DeltaEtherCAT.Native;

namespace AdaWeldSystem.MotionControl.DeltaEtherCAT
{
    /// <summary>从站设备类型</summary>
    public enum SlaveDeviceType
    {
        /// <summary>未识别</summary>
        Unknown,
        /// <summary>台达 A2E 伺服</summary>
        Servo_A2E,
        /// <summary>台达单轴模块</summary>
        Servo_EcAxis,
        /// <summary>台达四轴模块</summary>
        Servo_Ec4Axis,
        /// <summary>16 路数字输入</summary>
        DI_16Channel,
        /// <summary>16 路数字输出</summary>
        DO_16Channel,
        /// <summary>模拟量输入</summary>
        AD_Module,
        /// <summary>模拟量输出</summary>
        DA_Module,
        /// <summary>安川伺服</summary>
        Yaskawa_Servo
    }

    /// <summary>从站扫描器</summary>
    /// <remarks>按厂商号与产品码识别设备类型，须在主站初始化完成后调用。</remarks>
    public class DeltaSlaveScanner
    {
        #region 私有变量

        private const uint VendorDelta = 0x01DD;
        private const uint VendorDeltaAlt = 0x01A05;
        private const uint VendorYaskawa = 0x00539;

        #endregion

        #region 公共变量

        /// <summary>扫描结果</summary>
        public List<SlaveInfo> SlaveInfos { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>创建从站扫描器</summary>
        public DeltaSlaveScanner()
        {
            SlaveInfos = new List<SlaveInfo>();
        }

        #endregion

        #region 私有函数

        /// <summary>按厂商号与产品码识别设备类型</summary>
        /// <param name="info">待识别的从站</param>
        private static void IdentifyDevice(SlaveInfo info)
        {
            if (info.VendorId == VendorYaskawa)
            {
                if (info.ProductCode != 0x2200001) return;
                info.DeviceType = SlaveDeviceType.Yaskawa_Servo;
                info.DeviceName = "安川伺服";
                info.AxisCount = 1;
                return;
            }

            if (info.VendorId != VendorDelta && info.VendorId != VendorDeltaAlt) return;

            switch (info.ProductCode)
            {
                case 0x10305070:
                    info.DeviceType = SlaveDeviceType.Servo_A2E;
                    info.DeviceName = "台达 A2E 伺服";
                    info.AxisCount = 1;
                    break;
                case 0x00624:
                    info.DeviceType = SlaveDeviceType.Servo_Ec4Axis;
                    info.DeviceName = "台达四轴模块";
                    info.AxisCount = 4;
                    break;
                case 0x05621:
                    info.DeviceType = SlaveDeviceType.Servo_EcAxis;
                    info.DeviceName = "台达单轴模块";
                    info.AxisCount = 1;
                    break;
                case 0x06002:
                case 0x6022:
                case 0x6032:
                case 0x6142:
                    info.DeviceType = SlaveDeviceType.DI_16Channel;
                    info.DeviceName = "16 路数字输入";
                    break;
                case 0x07062:
                case 0x70A2:
                case 0x71A2:
                    info.DeviceType = SlaveDeviceType.DO_16Channel;
                    info.DeviceName = "16 路数字输出";
                    break;
            }
        }

        #endregion

        #region 公共函数

        /// <summary>扫描总线上所有从站</summary>
        /// <param name="cardNo">卡号</param>
        /// <returns>扫描成功返回 true</returns>
        public bool Scan(ushort cardNo)
        {
            SlaveInfos.Clear();

            ushort slaveNum = 0;
            ushort ret = EtherCATMasterNative.CS_ECAT_Master_Get_SlaveNum(cardNo, ref slaveNum);
            if (ret != DeltaErrorCode.NO_ERROR) return false;

            for (ushort seq = 0; seq < slaveNum; seq++)
            {
                ushort nodeId = 0;
                uint vendorId = 0;
                uint productCode = 0;
                uint revisionNo = 0;
                uint dcTime = 0;
                ret = EtherCATMasterNative.CS_ECAT_Master_Get_Slave_Info(cardNo, seq, ref nodeId,
                    ref vendorId, ref productCode, ref revisionNo, ref dcTime);
                if (ret != DeltaErrorCode.NO_ERROR) continue;

                SlaveInfo info = new SlaveInfo
                {
                    SeqId = seq,
                    NodeId = nodeId,
                    VendorId = vendorId,
                    ProductCode = productCode,
                    RevisionNo = revisionNo,
                    DeviceName = "未识别设备",
                    DeviceType = SlaveDeviceType.Unknown
                };
                IdentifyDevice(info);
                SlaveInfos.Add(info);
            }
            return true;
        }

        /// <summary>按设备类型筛选从站</summary>
        /// <param name="deviceType">设备类型</param>
        /// <returns>匹配的从站列表</returns>
        public List<SlaveInfo> GetByType(SlaveDeviceType deviceType)
        {
            return SlaveInfos.FindAll(s => s.DeviceType == deviceType);
        }

        /// <summary>按从站号查找</summary>
        /// <param name="nodeId">从站号</param>
        /// <returns>匹配的从站，未找到返回 null</returns>
        public SlaveInfo FindByNodeId(ushort nodeId)
        {
            return SlaveInfos.Find(s => s.NodeId == nodeId);
        }

        #endregion
    }

    /// <summary>从站信息</summary>
    public class SlaveInfo
    {
        /// <summary>扫描序号</summary>
        public ushort SeqId { get; set; }

        /// <summary>从站号</summary>
        public ushort NodeId { get; set; }

        /// <summary>厂商号</summary>
        public uint VendorId { get; set; }

        /// <summary>产品码</summary>
        public uint ProductCode { get; set; }

        /// <summary>版本号</summary>
        public uint RevisionNo { get; set; }

        /// <summary>设备名称</summary>
        public string DeviceName { get; set; }

        /// <summary>设备类型</summary>
        public SlaveDeviceType DeviceType { get; set; }

        /// <summary>轴数，非伺服为 0</summary>
        public ushort AxisCount { get; set; }
    }
}
