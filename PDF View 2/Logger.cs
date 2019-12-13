using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PDF_View_2
{
    internal class Logger
    {
        private const string LOGPATH = @"\Penpower\PdfViewer\";
        private static object m_sLockFlag = new object();
        private static int _log_level = 0;

        public static void WriteLog(string FileName, LOG_LEVEL llLogLevel, string LogStr)
        {
            //時間、iLevel、字串，符合Level設定範圍的就寫入log

            string strExt = Path.GetExtension(FileName);
            if (string.IsNullOrEmpty(strExt))
                strExt = ".log";

            string strFile = Path.GetFileNameWithoutExtension(FileName);

            string LogPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + LOGPATH;
            string dateToday = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            string FilePath = LogPath + strFile + dateToday + strExt;

            // 依照設定顯示不同的log level
            if (IsNeedWritelog(llLogLevel))
            {
                if (!Directory.Exists(LogPath))
                {
                    Directory.CreateDirectory(LogPath);
                }

                lock (m_sLockFlag)
                {
                    try
                    {
                        using (StreamWriter sw = File.AppendText(FilePath))
                        {
                            string LevelStr = "(str)";
                            LevelStr = LevelStr.Replace("str", llLogLevel.ToString());
                            dateToday = DateTime.Now.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
                            string timeNow = "[" + dateToday + " " + DateTime.Now.ToString("HH:mm:ss:fff", CultureInfo.InvariantCulture) + "]";
                            sw.WriteLine("{0} {1} {2}",                                 //[時間]  Level  ErrMsg
                                timeNow,
                                LevelStr,
                                LogStr);
                            sw.Flush();
                            sw.Close();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }
        }

        private static bool IsNeedWritelog(LOG_LEVEL iLevel)
        {
            //return true;
            // 如果是0，就先讀一次registry決定
            if (_log_level == 0)
                _log_level = (int)GetLogLevel();

            if ((int)iLevel > _log_level)
                return false;
            else
                return true;
        }

        /// <summary>
        /// 取得註冊表中debug Log的Level，預設是LL_SUB_FUNC=2
        /// </summary>
        /// <returns></returns>
        public static LOG_LEVEL GetLogLevel()
        {
            int llValue = 0;
            string subKeyPath = @"SOFTWARE\Penpower\ScannerManager";
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(subKeyPath);
                if (key != null)
                {
                    object objValue = key.GetValue("LogLevel");
                    if (objValue != null)
                    {
                        if (key.GetValueKind("LogLevel") != RegistryValueKind.DWord)
                        {
                            //key.DeleteValue("LogLevel");
                            llValue = 0;
                        }
                        else
                        {
                            llValue = Convert.ToInt32(objValue);
                        }
                    }
                    else
                    {
                        llValue = 0;
                    }
                    key.Close();
                    key.Dispose();
                }
            }
            catch { }
            llValue = llValue == 0 ? 2 : llValue;
            if (llValue > 4 || llValue < 1)
                llValue = 2;
            return (LOG_LEVEL)llValue;
        }

        /// <summary>
        /// 設定debug level
        /// </summary>
        /// <param name="llValue">level設定值，範圍1~4</param>
        /// <returns></returns>
        public static bool WriteLevel(LOG_LEVEL level = LOG_LEVEL.LL_SUB_FUNC)
        {
            int llValue = (int)level;
            if (llValue > 4 || llValue < 1)
                return false;
            string subKeyPath = @"SOFTWARE\Penpower\ScannerManager";
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(subKeyPath, true);
                if (key != null)
                {
                    key.SetValue("LogLevel", llValue);
                    key.Close();
                    key.Dispose();
                }
                else
                {
                    key = Registry.CurrentUser.CreateSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
                    if (key != null)
                    {
                        key.SetValue("LogLevel", llValue);
                        key.Close();
                        key.Dispose();
                    }
                    else
                        return false;
                }
            }
            catch { return false; }
            _log_level = llValue;
            return true;
        }
    }

    internal enum LOG_LEVEL
    {
        LL_SERIOUS_ERROR = 1,
        LL_SUB_FUNC = 2,
        LL_NORMAL_LOG = 3,
        LL_TRACE_LOG = 4
    }
}
