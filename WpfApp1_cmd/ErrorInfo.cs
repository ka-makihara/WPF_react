using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1_cmd
{
	public enum ErrorCode
	{
		OK = 0,
		UPDATE_READ_ERROR      = 0xE000001,
		UPDATE_UNDEFINED_ERROR = 0xE000002,
		FTP_ACCOUNT_ERROR      = 0xE000010,
		FTP_SERVER_ERROR       = 0xE000011,
		FTP_TRANSFER_ERROR     = 0xE000012,
		BACKUP_DATA_MISSMATCH  = 0xE000020,
		LCU_CONNECT_ERROR      = 0xE000030,
		LCU_DISK_INFO_ERROR    = 0xE000031,
		LCU_UPDATE_DISK_FULL   = 0xE000032,
	}

	public static class ErrorMessages
	{
		private static readonly ResourceManager ResourceManager = new ResourceManager("WpfApp1_cmd.Resources.Resource", typeof(ErrorMessages).Assembly);

        public static string GetErrorMessage(ErrorCode code)
        {
			if(ResourceManager == null)
			{
				return "Resource Manager null.";
			}
			string resourceKey = $"Error_{code}";
            string? message = ResourceManager.GetString(resourceKey, CultureInfo.CurrentUICulture);
            return string.IsNullOrEmpty(message) ? "Unknown error." : message;
        }
		/*
		private static readonly Dictionary<ErrorCode, string> errorMessages = new Dictionary<ErrorCode, string>
		{
			{ ErrorCode.OK,                    "Operation completed successfully." },
			{ ErrorCode.UPDATE_READ_ERROR,     "UpdateData not found." },
			{ ErrorCode.FTP_SERVER_ERROR,      "FTP server error." },
			{ ErrorCode.FTP_TRANSFER_ERROR,    "FTP transfer error." },
			{ ErrorCode.FTP_ACCOUNT_ERROR,     "FTP account error." },
			{ ErrorCode.BACKUP_DATA_MISSMATCH, "Backup data missmatch." },
			{ ErrorCode.LCU_CONNECT_ERROR,     "LCU connection error." },
			{ ErrorCode.LCU_DISK_INFO_ERROR,   "LCU disk Info error." },
			{ ErrorCode.LCU_UPDATE_DISK_FULL,  "LCU update disk full." },
		};

		public static string GetErrorMessage(ErrorCode code)
		{
			return errorMessages.TryGetValue(code, out var message) ? message : "Unknown error.";
		}
		*/
    }
	public static class ErrorCodeExtensions
	{
		public static string GetMessage(this ErrorCode code)
		{
			return ErrorMessages.GetErrorMessage(code);
		}
	}

	public class ErrorInfo
	{
		public static ErrorCode ErrCode { get; set; }
		public static ErrorCode SubErrCode { get; set; }

		public static string GetErrMessage()
		{
			return ErrCode.GetMessage();
		}

		public static string GetErrMessage(ErrorCode code)
		{
			return code.GetMessage();
		}
	}
}
