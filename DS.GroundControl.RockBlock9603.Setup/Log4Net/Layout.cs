using log4net.Layout;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using ConfigurationManager = System.Configuration.ConfigurationManager;

namespace DS.GroundControl.RockBlock9603.Log4Net
{
    public class Layout : PatternLayout
    {
        public override string Header { get => BuildHeader(); }

        private static string BuildHeader()
        {
            var serviceConfiguration = (NameValueCollection)ConfigurationManager.GetSection("serviceConfiguration");

            var settingsKeys = serviceConfiguration.AllKeys;

            var maxKeyLength = settingsKeys.Aggregate("", (max, cur) => max.Length > cur.Length ? max : cur).Length;
            var lineLength = (maxKeyLength + 1) * 2 + 1;

            var header = new StringBuilder();

            header.AppendLine(new string('=', lineLength));

            #region Print "Session Information" section
            var sessionInfo = new NameValueCollection
                {
                    {
                        "Product Version",
                        FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location).ProductVersion
                    },
                    {
                        ".NET Version Used",
                        $"{Environment.Version}"
                    },
                    {
                        "User Running Process",
                        GetUserLoginName()
                    },
                    {
                        "Process Start Time UTC",
                        $"{Process.GetCurrentProcess().StartTime.ToUniversalTime():yyyy-MM-dd HH:mm:ss,fff}"
                    },
                    {
                        "Local Time Information",
                        GetLocalTimeInformation()
                    }
                };

            header.AppendLine(CenterString("Session Information", lineLength));
            header.AppendLine(new string('-', lineLength));

            foreach (var key in sessionInfo.AllKeys)
            {
                header.AppendLine($"{key.PadLeft(maxKeyLength)} : {sessionInfo[key]}");
            }
            #endregion

            #region Print "Service Configuration" section
            header.AppendLine(new string('-', lineLength));
            header.AppendLine(CenterString("Service Configuration", lineLength));
            header.AppendLine(new string('-', lineLength));

            foreach (var key in serviceConfiguration.AllKeys)
            {
                header.AppendLine($"{key.PadLeft(maxKeyLength)} : {serviceConfiguration[key]}");
            }
            #endregion

            header.AppendLine(new string('=', lineLength));

            return header.ToString();
        }
        private static string GetUserLoginName()
        {
            try
            {
                return Environment.UserDomainName + "\\" + Environment.UserName;
            }
            catch
            {
                return string.Empty;
            }
        }
        private static string GetLocalTimeInformation()
        {
            var localZone = TimeZoneInfo.Local;
            var utcOffset = localZone.GetUtcOffset(DateTime.Now);
            var isDst = localZone.IsDaylightSavingTime(DateTime.Now);

            var timeZoneName = isDst
                ? localZone.DaylightName
                : localZone.StandardName;

            return $"Now: {DateTime.Now}, UTC Offset: {utcOffset}, Zone: {timeZoneName}, Is DST: {isDst}";
        }
        private static string CenterString(string str, int totalWidth)
        {
            return str.PadLeft((totalWidth - str.Length) / 2 + str.Length).PadRight(totalWidth);
        }
    }
}