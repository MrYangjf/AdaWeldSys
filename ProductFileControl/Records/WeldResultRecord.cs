using System;
using System.Collections.Generic;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.SQLiteDBControl;
using AdaWeldSystem.SQLiteDBControl.Helpers;

namespace AdaWeldSystem.ProductFileControl
{
    /// <summary>
    /// 焊接结果记录类（实现层）
    /// 记录内容 = 焊接异常 / 失败 / 成功 + 产品信息，连接数据库管理
    /// （经 SQLiteDatabaseManager.ProductDatabaseHelper 按日期分表落库，写入契约 string[]，ADR-026 分层约束）。
    /// </summary>
    public class WeldResultRecord : ProductRecordBase
    {
        private const string Tag = "WeldResultRecord";

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>
        /// 记录名称
        /// </summary>
        public override string Name
        {
            get { return "WeldResultRecord"; }
        }

        /// <summary>
        /// 记录一条焊接结果（异常/失败/成功 + 产品信息；入队即返回，后台落库）
        /// </summary>
        /// <param name="item">焊接结果条目</param>
        public void RecordResult(WeldResultItem item)
        {
            Enqueue(item);
        }

        /// <summary>
        /// 单条落盘（后台线程调用）：写入产品数据库当天分表
        /// </summary>
        /// <param name="item">记录条目</param>
        protected override void Persist(RecordItem item)
        {
            var result = item as WeldResultItem;
            if (result == null)
            {
                return;
            }
            try
            {
                var helper = SQLiteDatabaseManager.Instance.ProductDatabaseHelper;
                if (helper == null)
                {
                    Log("ProductDatabaseHelper 未就绪 结果记录未落库", MessageLevel.Warning);
                    return;
                }
                helper.AddProductInfo(result.Sn, result.StartTime, result.InspectionType,
                    result.Result, result.CheckInfo);
            }
            catch (Exception ex)
            {
                Log(string.Format("焊接结果落库异常 {0}", ex.Message), MessageLevel.Error);
            }
        }

        /// <summary>
        /// 查询产品检测结果（DB 回溯入口；转发产品数据库 Helper）
        /// </summary>
        /// <param name="date">查询日期</param>
        /// <returns>该日期数据表；无数据返回 null</returns>
        public System.Data.DataTable SelectResultTable(DateTime date)
        {
            var helper = SQLiteDatabaseManager.Instance.ProductDatabaseHelper;
            if (helper == null)
            {
                return null;
            }
            helper.SelectTable(date);
            return helper.CurrentTable;
        }
    }
}
