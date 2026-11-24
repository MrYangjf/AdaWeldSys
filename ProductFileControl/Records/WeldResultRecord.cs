using System;
using System.Collections.Generic;
using AdaWeldSystem.Comm.SystemComm;
using AdaWeldSystem.FileOperate;
using AdaWeldSystem.SQLiteDBControl;
using AdaWeldSystem.SQLiteDBControl.Helpers;

namespace AdaWeldSystem.ProductFileControl
{
    /// <summary>焊接结果记录类</summary>
    public class WeldResultRecord : ProductRecordBase
    {
        private const string Tag = "WeldResultRecord";
        public override string Name
        {
            get { return "WeldResultRecord"; }
        }

        /// <summary>统一日志出口（固定使用本类标签）</summary>
        /// <param name="message">消息内容。</param>
        /// <param name="level">日志级别。</param>
        private void Log(string message, MessageLevel level = MessageLevel.Info)
        {
            GlobalCommData.ShowLog(Tag, message, level);
        }

        /// <summary>记录一条焊接结果</summary>
        /// <param name="item">焊接结果条目</param>
        public void RecordResult(WeldResultItem item)
        {
            Enqueue(item);
        }

        /// <summary>单条落盘（后台线程调用）：写入产品数据库当天分</summary>

        /// <param name="item">记录条目</param>
        protected override void Persist(RecordItem item)
        {
            if (!(item is WeldResultItem result))
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
                helper.AddProductInfo(result.Sn, result.StartTime, result.Status,
                    result.Result, result.Info);
            }
            catch (Exception ex)
            {
                Log(string.Format("焊接结果落库异常 {0}", ex.Message), MessageLevel.Error);
            }
        }

        /// <summary>查询产品检测结果（DB 回溯入口；转发产品数据库 Helper）</summary>
        /// <param name="date">查询日期</param>
        /// <returns>该日期数据表；数据库不可用或无数据返回 null</returns>
        public System.Data.DataTable SelectResultTable(DateTime date)
        {
            var helper = SQLiteDatabaseManager.Instance.ProductDatabaseHelper;
            if (helper == null || helper.IsDisposed)
            {
                Log("产品数据库不可用 生产记录查询已跳过", MessageLevel.Warning);
                return null;
            }
            helper.SelectTable(date);
            return helper.CurrentTable;
        }

        /// <summary>查询时间区间内的生产记录（数据追溯页区间筛</summary>

        /// <param name="start">区间起始日（含）</param>
        /// <param name="end">区间结束日（含）</param>
        /// <returns>合并后的记录表；数据库不可用返回 null，区间内无记录返回零行的表</returns>
        public System.Data.DataTable SelectResultTable(DateTime start, DateTime end)
        {
            var helper = SQLiteDatabaseManager.Instance.ProductDatabaseHelper;
            if (helper == null || helper.IsDisposed)
            {
                Log("产品数据库不可用 生产记录区间查询已跳过", MessageLevel.Warning);
                return null;
            }
            return helper.SelectTables(start, end);
        }
    }
}
