using System;
using System.IO;

namespace AdaWeldSystem.FileOperate
{
    public class Log
    {
        public Log(string strFileLogName)
        {
            LogFileName = strFileLogName;
            InitLogDir();
            CheckLogLife(LogDir);
        }
        public string LogFileName = "";
        public string LogFilePath = "";
        public string LogDir = "";
        public uint LogLiftCycle = 30;
        public int FileSize = 20;
        public string ErrorInfo = "";
        private bool IsError = false;
        private static object m_lock = new object();

        public void Debug(string strFunName, string strLogContent)
        {
            string str = string.Format("{0} [Debug][{1}] {2}", GetNowToString(), strFunName, strLogContent);
            this.WriteLog(str);
        }
        public void Error(string strFunName, string strLogContent)
        {
            string str = string.Format("{0} [Error][{1}] {2}", GetNowToString(), strFunName, strLogContent);
            this.WriteLog(str);
        }
        public void Info(string strFunName, string strLogContent)
        {
            string str = string.Format("{0} [Info][{1}] {2}", GetNowToString(), strFunName, strLogContent);
            this.WriteLog(str);
        }
        public void Warn(string strFunName, string strLogContent)
        {
            string str = string.Format("{0} [Warn][{1}] {2}", GetNowToString(), strFunName, strLogContent);
            this.WriteLog(str);
        }
        private void CheckLogLife(string strPath)
        {
            DateTime LimitTime = DateTime.Now.AddDays(-LogLiftCycle);
            if (Directory.Exists(strPath))
            {
                string[] strFiles = Directory.GetDirectories(strPath);
                foreach (string SingleFile in strFiles)
                {
                    if (Directory.GetCreationTime(SingleFile) <= LimitTime)
                    {
                        Directory.Delete(SingleFile, true);
                        continue;
                    }
                    CheckLogLife(SingleFile);
                }
            }
        }
        private string GetNowToString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:ffff");
        }
        private void InitLogDir()
        {
            string name = System.Reflection.MethodBase.GetCurrentMethod().Name;
            this.LogDir = "";
            string path = "D:";
            if (!IsError)
            {
                if (!Directory.Exists(path))
                {
                    path = "C:";
                    if (!Directory.Exists(path))
                    {
                        char ch = 'E';
                        int num = Convert.ToInt32(ch);
                        for (int i = 0; i < 0x18; i++)
                        {
                            num += i;
                            path = Convert.ToChar(num).ToString();
                            if (Directory.Exists(path))
                            {
                                break;
                            }
                        }
                    }
                }
                path = path + @"\IntelliSystemLogData";
                LogDir = path;
                if (!Directory.Exists(path))
                {
                    try
                    {
                        Directory.CreateDirectory(path);
                    }
                    catch (Exception exception)
                    {
                        IsError = true;
                        ErrorInfo = string.Format("函数 {0} 创建日志目录失败 {1} 错误信息 {2}", name, path, exception.Message);
                        path = "";
                    }
                }
            }
        }
        private void InitLogFilePath()
        {
            string name = System.Reflection.MethodBase.GetCurrentMethod().Name;
            try
            {
                string year = "";
                string month = "";
                string day = "";
                GetDate(ref year, ref month, ref day);
                string yearFile = string.Format(@"{0}\{1}", LogDir, year);
                if (!Directory.Exists(yearFile))
                {
                    Directory.CreateDirectory(yearFile);
                }
                string monthFile = string.Format(@"{0}\{1}", yearFile, month);
                if (!Directory.Exists(monthFile))
                {
                    Directory.CreateDirectory(monthFile);
                }
                string dayFile = string.Format(@"{0}\{1}", monthFile, day);
                if (!Directory.Exists(dayFile))
                {
                    Directory.CreateDirectory(dayFile);
                }
                this.LogFilePath = string.Format(@"{0}\{1}{2}{3}_{4}.log", dayFile, year, month, day, LogFileName);
            }
            catch (Exception exception)
            {
                this.LogFilePath = "";
                IsError = true;
                ErrorInfo = string.Format("函数 {0} 创建日志目录失败 错误信息 {1}", name, exception.Message);
            }
        }
        private void GetDate(ref string year, ref string month, ref string day)
        {
            DateTime now = DateTime.Now;
            year = now.ToString("yyyy");
            month = now.ToString("MM");
            day = now.ToString("dd");
        }
        private bool WriteLog(string strLogContent)
        {
            lock (m_lock)
            {
                InitLogFilePath();
                if (strLogContent.Length > 0x100000)
                {
                    strLogContent = strLogContent.Substring(0, 0x100000);
                }
                string destFileName = "";
                int num = (FileSize * 0x400) * 0x400;
                FileStream stream = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, strLogContent.Length, FileOptions.Asynchronous);

                if (stream.Length > num)
                {
                    string str2 = LogFilePath.Substring(0, LogFilePath.Length - 4);
                    destFileName = string.Format("{0}_{1}.log", str2, DateTime.Now.ToString("HHmmssfff"));
                    stream.Close();
                    if (destFileName != "")
                    {
                        File.Move(LogFilePath, destFileName);
                    }
                    using (FileStream stream2 = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, strLogContent.Length, FileOptions.Asynchronous))
                    {
                        StreamWriter sw = new StreamWriter(stream2, System.Text.Encoding.UTF8);
                        sw.WriteLine(strLogContent);
                        sw.Flush();
                        sw.Close();
                        stream2.Close();
                    }
                }
                else
                {
                    StreamWriter sw = new StreamWriter(stream, System.Text.Encoding.UTF8);
                    sw.WriteLine(strLogContent);
                    sw.Flush();
                    sw.Close();
                    stream.Close();
                }
                return true;
            }
        }

    }
}
