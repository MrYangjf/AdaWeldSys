﻿using Emgu.CV;
using System;
using System.Collections.Generic;

namespace AdaWeldSystem.MonitorCam.IMonitorCam
{
    /// <summary>
    /// 监控相机抽象接口，实现层唯一契约。
    /// 实现层只按契约取帧，不承载业务语义。
    /// 业务语义由 MonitorCamManager 承担。
    /// 当前实现：HikCam（海康威视 MvCameraControl.Net 封装）。
    /// 取流模型分连续采集 StartCapture 与软触发单帧 TriggerSingleShot 两种。
    /// 连续采集经 PreviewFrame 逐帧抛出。
    /// 录像 StartRecord 须在连续取流中开启。
    /// 帧由实现层取流线程统一喂给 VideoRecorder。
    /// </summary>
    public interface IMonitorCamApi
    {
        /// <summary>当前连接状态</summary>
        MonitorCameraConnectionState ConnectionState { get; }

        /// <summary>是否正在连续取流（采集）</summary>
        bool IsGrabbing { get; }

        /// <summary>是否正在录像（须处于连续取流中）</summary>
        bool IsRecording { get; }

        /// <summary>
        /// 预览帧事件，每帧独立副本，勿长期持有。
        /// 仅连续取流抛出，软触发单帧不走此处。
        /// </summary>
        event Action<Mat> PreviewFrame;

        /// <summary>
        /// 枚举已连接相机，返回可连接设备列表。
        /// 未连接也可调用，由实现层自行完成 SDK 初始化。
        /// </summary>
        /// <returns>发现的设备列表；无设备或扫描失败返回空列表</returns>
        List<MonitorCamDeviceInfo> ScanDevices();

        /// <summary>
        /// 连接相机
        /// </summary>
        /// <param name="ip">相机 IP（GigE 按 IP 匹配；USB3 相机可留空，取枚举首台）</param>
        /// <param name="port">相机端口（海康 SDK 不使用，保留以兼容契约）</param>
        /// <returns>是否成功建立连接</returns>
        bool Connect(string ip, string port);

        /// <summary>断开连接并释放 SDK 资源（含停止录像/采集）</summary>
        void Disconnect();

        /// <summary>
        /// 开始连续采集：连续模式并开启取流。
        /// 启动取帧线程，已在采集则直接返回 true。
        /// </summary>
        /// <returns>是否成功开始采集</returns>
        bool StartCapture();

        /// <summary>
        /// 停止采集：先停录像，再停线程并关流。
        /// </summary>
        void StopCapture();

        /// <summary>
        /// 触发一次采集并同步返回帧。
        /// 与连续取流共享队列，调用方自行协调。
        /// </summary>
        /// <returns>采集到的图像帧（独立副本）；失败返回 null</returns>
        Mat CaptureSingleFrame();

        /// <summary>
        /// 软触发单帧，对齐官方 demo 触发取图。
        /// 切触发模式（TriggerMode On 与 TriggerSource Software）。
        /// 开流取一帧后关流，保持触发模式。
        /// 调用方须保证当前未在连续采集中。
        /// </summary>
        /// <returns>触发取得的图像帧（独立副本）；失败返回 null</returns>
        Mat TriggerSingleShot();

        /// <summary>
        /// 开始录像，须在连续取流中，录 AVI。
        /// 帧由实现层取流线程统一喂给 VideoRecorder。
        /// </summary>
        /// <param name="filePath">录像文件完整路径（.avi）</param>
        /// <returns>是否成功开始录像</returns>
        bool StartRecord(string filePath);

        /// <summary>停止录像</summary>
        void StopRecord();

        /// <summary>设置曝光时间（关闭自动曝光后写入）</summary>
        /// <param name="valueUs">曝光时间（微秒）</param>
        /// <returns>是否设置成功</returns>
        bool SetExposureTime(float valueUs);

        /// <summary>设置增益（关闭自动增益后写入）</summary>
        /// <param name="valueDb">增益（dB）</param>
        /// <returns>是否设置成功</returns>
        bool SetGain(float valueDb);

        /// <summary>读取当前曝光时间（微秒）；失败返回 -1</summary>
        /// <returns>曝光时间（微秒）；失败返回 -1</returns>
        float GetExposureTime();

        /// <summary>读取当前增益（dB）；失败返回 -1</summary>
        /// <returns>增益（dB）；失败返回 -1</returns>
        float GetGain();

        /// <summary>失败原因成品文本，日志与界面只读</summary>
        /// <remarks>按各自 SDK 口径映射，不得空串或编造。</remarks>
        /// <param name="context">失败语境（连接 / 采集 / 分析）</param>
        /// <returns>非空的原因描述</returns>
        string DescribeLastError(MonitorCamErrorContext context);

        /// <summary>释放 SDK 资源</summary>
        void Dispose();
    }
}
