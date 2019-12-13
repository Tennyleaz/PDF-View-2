using System;
using System.Globalization;
using System.Windows;
using Piwik.Tracker;

namespace PdfTracker
{
    public class Tracker
    {
        #region Private members        
        private static string PiwikBaseUrl = "http://matomo.penpower.net";
        private static int SiteId = 1;  //Piwik控制台裡面設定的site id號碼，對應不同產品
        private static readonly int TIMEOUT_SECOUND = 30;
        private static string referrerURL = null;

        private string _userAgent;  // = "Mozilla/5.0 (Windows NT 10.0; WOW64; en-US;)"; //"Mozilla/5.0 (Windows NT 10.0; WOW64; rv:55.0)";
        private string _userID;
        private string _title;
        private string _appName;
        private string _appLocale;
        private string _version;
        private int _width, _height;
        private int _dimentionIndex;
        private string _dimentionValue;
        private bool _ignoreSSLWarning;
        private TrackerExcptionType _excptionType;  // 用來儲存 constructor 裡面發生的問題
        private string _excptionMessage;

        private static readonly TrackerResult ArgumentExcptionResult = new TrackerResult()
        {
            ExcptionType = TrackerExcptionType.ArgumentExcption,
            StatusCode = 0,
            Message = "Argument is null or empty. ",
        };
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="siteID">Matomo 伺服器網頁上定義的每個網站 ID</param>
        /// <param name="serverURL">追蹤伺服器位址</param>
        /// <param name="appName">複寫預設的程式名，例如 WCR</param>
        /// <param name="version">以v開頭為佳，例如 v1.2.1</param>
        /// <param name="userID">辨識獨立的使用者代號，例如 Alice</param>
        /// <param name="customWidth">複寫系統螢幕寬度</param>
        /// <param name="customHeight">複寫系統螢幕高度</param>
        /// <param name="ignoreSSLWarning">忽略沒有有效簽章的 server 產生的 ssl 警告</param>
        /// <param name="appLocale">複寫使用者的預設 Windows 文化/地區，遵循 RFC 4646 的文化特性格式，例如 en-US</param>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="ArgumentNullException"/>
        public Tracker(string serverURL, string version, string userID,
            string appLocale = null, int siteID = 5, string appName = "PdfViewer", int? customWidth = null, int? customHeight = null, bool ignoreSSLWarning = false)
        {
            _excptionType = TrackerExcptionType.Success;
            _excptionMessage = string.Empty;

            #region 檢查用
            if (string.IsNullOrEmpty(appName))
            {
                //throw new ArgumentNullException(appName, "App name must not be null or empty. ");
                _excptionMessage = "App name must not be null or empty. ";
                _excptionType = TrackerExcptionType.ArgumentExcption;
            }
            if (string.IsNullOrEmpty(serverURL))
            {
                //throw new ArgumentNullException(version, "Server URL must not be null or empty. ");
                _excptionMessage += "Server URL must not be null or empty. ";
                _excptionType = TrackerExcptionType.ArgumentExcption;
            }
            if (string.IsNullOrEmpty(version))
            {
                //throw new ArgumentNullException(serverURL, "Version must not be null or empty.");
                _excptionMessage += "Version must not be null or empty. ";
                _excptionType = TrackerExcptionType.ArgumentExcption;
            }
            if (siteID <= 0)
            {
                //throw new ArgumentException("siteID must > 0.", "siteID");
                _excptionMessage += "siteID must > 0.";
                _excptionType = TrackerExcptionType.ArgumentExcption;
            }
            #endregion

            if (!string.IsNullOrEmpty(serverURL))
                PiwikBaseUrl = serverURL;

            // os version
            int osMajor = Environment.OSVersion.Version.Major;
            int osMinor = Environment.OSVersion.Version.Minor;
            string architecture = Environment.Is64BitOperatingSystem ? "; x64" : "; x86";
            _userAgent = "Mozilla/5.0 (Windows NT " + osMajor + "." + osMinor + architecture + ")";

            // locale of user environment            
            if (!string.IsNullOrEmpty(appLocale))
                _appLocale = appLocale;
            else
            {
                CultureInfo ci = CultureInfo.CurrentCulture;
                _appLocale = ci.Name;
            }

            _width = customWidth ?? (int)SystemParameters.PrimaryScreenWidth;
            _height = customHeight ?? (int)SystemParameters.PrimaryScreenHeight;

            _appName = appName;
            _version = version;
            _userID = userID;
            SiteId = siteID;
            _ignoreSSLWarning = ignoreSSLWarning;
        }

        public TrackerResult SendOperation(PDF_OP recordType)
        {
            TrackerResult result = new TrackerResult();
            if (_excptionType != TrackerExcptionType.ArgumentExcption)
            {
                string url = "http://" + _appName + "/Windows/" + _version + "/" + recordType.ToString();
                result = DoTrackPage(url, _title);
            }
            else
            {
                // argument exception, don't do anything
                result.StatusCode = 0;
                result.Message = _excptionMessage;
            }
            return result;
        }

        public TrackerResult SendEvent(string eventCategory, string eventAction, int count)
        {
            TrackerResult result = new TrackerResult();
            if (_excptionType != TrackerExcptionType.ArgumentExcption)
            {
                result = SendEvent(eventCategory, eventAction, null, count.ToString());
            }
            else
            {
                // argument exception, don't do anything
                result.StatusCode = 0;
                result.Message = _excptionMessage;
            }
            return result;
        }

        public TrackerResult SendAdView(string campaignName)
        {
            TrackerResult result = new TrackerResult();
            if (_excptionType != TrackerExcptionType.ArgumentExcption)
            {
                // check empty message
                if (string.IsNullOrEmpty(campaignName))
                {
                    result.ExcptionType = TrackerExcptionType.ArgumentExcption;
                    result.Message = "campaignName or excpetionMessage is empty.";
                    result.StatusCode = 0;
                    return result;
                }

                string url = "http://" + _appName + "/Windows/" + _version + "/Ad/" + campaignName + @"?pk_campaign=" + campaignName;
                result = DoTrackPage(url);
            }
            else
            {
                // argument exception, don't do anything
                result.StatusCode = 0;
                result.Message = _excptionMessage;
            }
            return result;
        }

        /// <summary>
        /// 記錄一筆錯誤，會顯示成一筆紀錄如 http://app/ver/Error/moduleName/excpetionMessage。
        /// </summary>
        /// <param name="moduleName">模組的名稱</param>
        /// <param name="excpetionMessage">錯誤訊息</param>
        /// <param name="title">可選的標題文字</param>
        /// <returns></returns>
        public TrackerResult SendErrorLog(string moduleName, string excpetionMessage, string title = null)
        {
            TrackerResult result = new TrackerResult();
            if (_excptionType != TrackerExcptionType.ArgumentExcption)
            {
                // check empty message
                if (string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(excpetionMessage))
                {
                    result.ExcptionType = TrackerExcptionType.ArgumentExcption;
                    result.Message = "moduleName or excpetionMessage is empty.";
                    result.StatusCode = 0;
                    return result;
                }

                string url = "http://" + _appName + "/Windows/" + _version + "/Error/" + moduleName + "/" + excpetionMessage;
                result = DoTrackPage(url, title);
            }
            else
            {
                // argument exception, don't do anything
                result.StatusCode = 0;
                result.Message = _excptionMessage;
            }
            return result;
        }

        public TrackerResult CheckServerStatus()
        {
            TrackerResult result = new TrackerResult();
            if (_excptionType != TrackerExcptionType.ArgumentExcption)
            {
                try
                {
                    PiwikTracker tracker = new PiwikTracker(SiteId, PiwikBaseUrl, _ignoreSSLWarning);
                    tracker.DisableCookieSupport();

                    TrackingResponse response = tracker.DoPing();
                    result.ExcptionType = TrackerExcptionType.Success;
                    result.StatusCode = (short)response.HttpStatusCode;
                    result.Message = response.HttpStatusCode.ToString();
                }
                catch (System.Net.WebException ex)
                {
                    result = HandleWebException(ex);
                }
                catch (Exception ex)
                {
                    // If exception thrown, no status code is provided. We will make one.
                    result.ExcptionType = TrackerExcptionType.OtherException;
                    result.StatusCode = 0;
                    result.Message = ex.ToString();
                }
            }
            else
            {
                // argument exception, don't do anything
                result.StatusCode = 0;
                result.Message = _excptionMessage;
            }
            return result;
        }

        private TrackerResult DoTrackPage(string url, string title = null)
        {
            TrackerResult result = new TrackerResult();
            try
            {
                PiwikTracker tracker = new PiwikTracker(SiteId, PiwikBaseUrl, _ignoreSSLWarning);
                tracker.DisableCookieSupport();
                tracker.SetUrl(url);
                tracker.SetUrlReferrer(referrerURL);
                referrerURL = url;
                GenerateAdditionalProperties(tracker);

                TrackingResponse response = tracker.DoTrackPageView(title);
                result.ExcptionType = TrackerExcptionType.Success;
                result.StatusCode = (short)response.HttpStatusCode;
                result.Message = response.HttpStatusCode.ToString();
            }
            catch (System.Net.WebException ex)
            {
                result = HandleWebException(ex);
            }
            catch (Exception ex)
            {
                // If exception thrown, no status code is provided. We will make one.
                result.ExcptionType = TrackerExcptionType.OtherException;
                result.StatusCode = 0;
                result.Message = ex.ToString();
            }
            return result;
        }

        /// <summary>
        /// 傳送一個事件紀錄給伺服器
        /// </summary>
        /// <param name="eventCategory"></param>
        /// <param name="eventAction"></param>
        /// <param name="eventName"></param>
        /// <param name="eventValue"></param>
        /// <returns>true=開始傳送 false=忙碌中</returns>
        private TrackerResult SendEvent(string eventCategory, string eventAction, string eventName = "", string eventValue = "")
        {
            TrackerResult result = new TrackerResult();

            if (string.IsNullOrWhiteSpace(eventCategory) || string.IsNullOrWhiteSpace(eventAction))
            {
                result.ExcptionType = TrackerExcptionType.ArgumentExcption;
                result.StatusCode = 0;
                result.Message = "eventCategory or eventAction is empty";
                return result;
            }

            try
            {
                string url = "http://" + _appName + "/Windows/" + _version + "/Event/";
                PiwikTracker tracker = new PiwikTracker(SiteId, PiwikBaseUrl, _ignoreSSLWarning);
                tracker.SetUrl(url);
                tracker.DisableCookieSupport();
                GenerateAdditionalProperties(tracker);

                TrackingResponse response = tracker.DoTrackEvent(eventCategory, eventAction, eventName, eventValue);
                result.ExcptionType = TrackerExcptionType.Success;
                result.StatusCode = (short)response.HttpStatusCode;
                result.Message = response.HttpStatusCode.ToString();
            }
            catch (System.Net.WebException ex)
            {
                result = HandleWebException(ex);
            }
            catch (Exception ex)
            {
                // If exception thrown, no status code is provided. We will make one.
                result.ExcptionType = TrackerExcptionType.OtherException;
                result.StatusCode = 0;
                result.Message = ex.ToString();
            }
            return result;
        }

        /// <summary>
        /// 設定額外的 UA、地區、ID、解析度、timeout
        /// </summary>
        /// <param name="piwikTracker"></param>
        private void GenerateAdditionalProperties(PiwikTracker piwikTracker)
        {
            if (!string.IsNullOrEmpty(_userAgent))
                piwikTracker.SetUserAgent(_userAgent);
            if (!string.IsNullOrEmpty(_appLocale))
                piwikTracker.SetBrowserLanguage(_appLocale);
            if (!string.IsNullOrEmpty(_userID))
                piwikTracker.SetUserId(_userID);
            if (_width > 0 && _height > 0)
                piwikTracker.SetResolution(_width, _height);
            piwikTracker.RequestTimeout = new TimeSpan(0, 0, TIMEOUT_SECOUND);
        }

        private TrackerResult HandleWebException(System.Net.WebException ex)
        {
            TrackerResult result = new TrackerResult();
            if (ex.Response is System.Net.HttpWebResponse response)
            {
                if (ex.Status == System.Net.WebExceptionStatus.TrustFailure && !_ignoreSSLWarning)
                {
                    // tell user set ignoreSSLWarning to true
                    result.ExcptionType = TrackerExcptionType.SSLException;
                    result.StatusCode = (short)response.StatusCode;
                    result.Message = "Something wrong with the certificate, try set ignoreSSLWarning to true. ";
                }
                else
                {
                    // If exception thrown, no status code is provided. We will make one.
                    result.ExcptionType = TrackerExcptionType.WebException;
                    result.StatusCode = (short)response.StatusCode;
                    result.Message = response.ToString();
                }
            }
            else if (ex.InnerException is System.Net.Sockets.SocketException socketException)
            {
                result.ExcptionType = TrackerExcptionType.SocketException;
                result.Message = socketException.Message;
                result.StatusCode = (short)socketException.SocketErrorCode;
            }
            else
            {
                // if no HttpWebResponse, i.e. no internet or server down
                result.ExcptionType = TrackerExcptionType.WebException;
                result.Message = ex.ToString();
                result.StatusCode = 0;
            }

            return result;
        }
    }
}
