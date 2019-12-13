using System;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;
using PdfTracker;

namespace PDF_View_2
{
    internal class MatomoAnalytics
    {
        private const string SERVER_URL = "http://10.10.15.65";
        private const string APP_NAME = "PdfViewer";
        private const int SITE_ID = 7;
        //private const string SETTING_NAME = "MatomoAnalytics.setting";
        private Tracker tracker;
        private BackgroundWorker sendWorker;  // 傳送大部分的紀錄都用此背景工作
        private BackgroundWorker eventWorker;  // 僅用來傳送卡片數量的事件
        private bool doNotTrack;
        private bool completeLog = false;

        public void Init()
        {
            ReadTrackKey();
            if (doNotTrack)
                return;

            string uid = GetFingerprint();
            tracker = new Tracker(SERVER_URL, "v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(), uid, null, SITE_ID, APP_NAME);

            sendWorker = new BackgroundWorker();
            sendWorker.WorkerSupportsCancellation = false;
            sendWorker.DoWork += SendWorker_DoWork;
            sendWorker.RunWorkerCompleted += Worker_RunWorkerCompleted;

            eventWorker = new BackgroundWorker();
            eventWorker.WorkerSupportsCancellation = false;
            eventWorker.DoWork += EventWorker_DoWork;
            eventWorker.RunWorkerCompleted += Worker_RunWorkerCompleted;
        }

        #region Background works

        private bool TryDoWork<T>(T op) where T : System.Enum
        {
            // send in background
            if (!sendWorker.IsBusy)
            {
                object[] args = { op };
                sendWorker.RunWorkerAsync(args);
                return true;
            }
            return false;
        }

        private bool TrySendAdView(string adLocation)
        {
            // send in background
            if (!sendWorker.IsBusy)
            {
                object[] args = { PDF_OP.AD, adLocation };
                sendWorker.RunWorkerAsync(args);
                return true;
            }
            return false;
        }

        private bool TrySendError(string moduleName, string errorMessage, string title)
        {
            // send in background
            if (!sendWorker.IsBusy)
            {
                object[] args = { moduleName, errorMessage, title };
                sendWorker.RunWorkerAsync(args);
                return true;
            }
            return false;
        }

        private void SendWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            TrackerResult result = new TrackerResult();
            if (!Utility.IsConnectedToInternet())
            {
                result.ExcptionType = TrackerExcptionType.OtherException;
                result.Message = "No internet";
                e.Result = result;
                return;
            }

            object[] args = e.Argument as object[];
            if (args != null && args.Length == 1)
            {
                // 1 argument is standard log
                switch (args[0])
                {
                    case PDF_OP op:
                        result = tracker.SendOperation(op);
                        if (result.ExcptionType == TrackerExcptionType.Success && op == PDF_OP.LaunchDaily)
                        {
                            // send a launch after successfull launch daily
                            result = tracker.SendOperation(PDF_OP.Launch);
                        }
                        break;
                    //case WCR_Import_OP iop:
                    //    result = tracker.SendOperation(iop);
                    //    break;
                    //case WCR_Export_OP eop:
                    //    result = tracker.SendOperation(eop);
                    //    break;
                    //case WCR_SYNC_OP sop:
                    //    result = tracker.SendOperation(sop);
                    //    break;
                    default:
                        break;
                }
            }
            else if (args != null && args.Length == 2)
            {
                // 2 arguments are view ad
                if (args[0] == null || args[1] == null)
                {
                    result.ExcptionType = TrackerExcptionType.ArgumentExcption;
                    return;
                }
                if (args[0] is PDF_OP.AD)
                    result = tracker.SendAdView((string)args[1]);
            }
            else if (args != null && args.Length == 3)
            {
                // 3 arguments are error log
                if (args[0] is string moduleName && args[1] is string errorMessage && args[2] is string title)
                {
                    result = tracker.SendErrorLog(moduleName, errorMessage, title);
                }
                else
                {
                    result.ExcptionType = TrackerExcptionType.ArgumentExcption;
                    return;
                }
            }

            e.Result = result;
        }

        private void EventWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            TrackerResult result = new TrackerResult();
            if (!Utility.IsConnectedToInternet())
            {
                result.ExcptionType = TrackerExcptionType.OtherException;
                result.Message = "No internet";
                e.Result = result;
                return;
            }

            object[] args = e.Argument as object[];
            if (args != null && args.Length == 3)
            {
                // 3 arguments is card count
                // arts[0] is CARD_COUNT_EVENT, args[1] is source/destination, args[2] is card count
                if (args[0] == null || args[1] == null || args[2] == null)
                {
                    result.ExcptionType = TrackerExcptionType.ArgumentExcption;
                    return;
                }

                result = tracker.SendEvent((string)args[0], (string)args[1], (int)args[2]);
            }
            e.Result = result;
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is TrackerResult trackerResult)
            {
                if (completeLog && trackerResult.ExcptionType != TrackerExcptionType.Success)
                    Logger.WriteLog("System", LOG_LEVEL.LL_SUB_FUNC, "Matomo result is: " + trackerResult.ExcptionType);
            }
        }

        #endregion

        public void TrackOP(PDF_OP op)
        {
            if (doNotTrack) return;
            if (op == PDF_OP.Launch || op == PDF_OP.LaunchDaily)
                DaliyUse();
            else
                TryDoWork(op);
        }

        public void ReportError(string moduleName, LOG_LEVEL logLevel, string errorMessage, string title = null)
        {
            if (doNotTrack) return;
            if (string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(errorMessage))
                return;
            TrySendError(moduleName, logLevel + "/" + errorMessage, string.IsNullOrEmpty(title) ? string.Empty : title);
        }

        public void TrackViewAd(string adLocation)
        {
            if (doNotTrack) return;
            if (string.IsNullOrEmpty(adLocation))
                return;
            TrySendAdView(adLocation);
        }

        private void DaliyUse()
        {
            try
            {
                //const string key = "LastUseTime";
                //string strAction = "WC_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + "_" + PDF_OP.LaunchDaily.ToString();
                
                DateTime lastUserTime = Properties.Settings.Default.LastLaunchTime;
                if (lastUserTime == DateTime.MinValue)
                {
                    // no last record, track use once
                    TryDoWork(PDF_OP.LaunchDaily);
                }
                else
                {
                    DateTime lastDay = new DateTime(lastUserTime.Year, lastUserTime.Month, lastUserTime.Day);
                    DateTime dtNow = DateTime.Now;
                    TimeSpan ts = dtNow - lastDay;
                    if (ts.Days >= 1)
                    {
                        // track use once
                        TryDoWork(PDF_OP.LaunchDaily);
                    }
                    else if (ts.Days == 0)
                    {
                        // after the first daily alunch, do regular launch
                        TryDoWork(PDF_OP.Launch);
                    }
                }

                // update last use time
                Properties.Settings.Default.LastLaunchTime = DateTime.Now;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                Logger.WriteLog("System", LOG_LEVEL.LL_SERIOUS_ERROR, "GoogleAnalytics DaliyUse fail: " + ex);
            }
        }

        private void ReadTrackKey()
        {
            doNotTrack = false;
            string path = @"SOFTWARE\Penpower\" + APP_NAME;
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(path);
                object value = key?.GetValue("DoNotTrack", null);
                if (value != null)
                {
                    bool.TryParse(value.ToString(), out doNotTrack);
                }
            }
            catch (Exception) { }
        }

        #region User id related

        private string GetFingerprint()
        {
            string uuid = ReadID();
            if (string.IsNullOrEmpty(uuid))
                uuid = SetNewID();

            // if cannot creat to registry...
            if (string.IsNullOrEmpty(uuid))
                uuid = GetWindowsUUID();
            return uuid;
        }

        private string SetNewID()
        {
            Guid guid = Guid.NewGuid();
            string path = @"SOFTWARE\Penpower\WorldCard 8\ProjectList";
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(path, true);
                if (key == null)
                    key = Registry.CurrentUser.CreateSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree);

                key.SetValue("Uuid", guid.ToString());
                return guid.ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return string.Empty;
        }

        private string ReadID()
        {
            string path = @"SOFTWARE\Penpower\WorldCard 8\ProjectList";
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(path);
                object value = key?.GetValue("Uuid", string.Empty);
                return value.ToString();
            }
            catch (Exception) { }
            return string.Empty;
        }

        // from:
        // https://stackoverflow.com/a/32636967/3576052
        private string GetWindowsUUID()
        {
            var procStartInfo = new ProcessStartInfo("cmd", "/c " + "wmic csproduct get UUID")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var proc = new Process() { StartInfo = procStartInfo };
            proc.Start();

            return proc.StandardOutput.ReadToEnd().Replace("UUID", string.Empty).Trim().ToUpper();
        }

        public string GetFrieldlyIDInfo()
        {
            return "[User ID]" + Environment.NewLine + GetFingerprint();
        }
        #endregion
    }
}
