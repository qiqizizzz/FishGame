/*
 * ┌──────────────────────────────────┐
 * │  描    述: 客户端轻量日志封装，Release 构建自动抹除日志
 * │  类    名: QLog.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Utils.log
{
    public static class QLog
    {
        [Conditional("UNITY_EDITOR")]
        public static void Info(object msg) => Debug.Log(FormatMessage(msg));

        [Conditional("UNITY_EDITOR")]
        public static void Warning(object msg) => Debug.LogWarning(FormatMessage(msg));

        [Conditional("UNITY_EDITOR")]
        public static void Error(object msg) => Debug.LogError(FormatMessage(msg));

        /// <summary>
        /// 在编辑器中记录异常，并在所有构建中抛出异常
        /// </summary>
        /// <param name="exception">需要抛出的异常</param>
        public static void Throw(Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

#if UNITY_EDITOR
            Debug.LogException(exception);
#endif
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        #region 日志类名查询方法
        // 构建带调用类名前缀的日志内容
        private static string FormatMessage(object msg)
        {
            string callerTypeName = GetCallerTypeName();
            string message = msg == null ? "null" : msg.ToString();

            if (string.IsNullOrEmpty(callerTypeName))
                return message;

            return $"[{callerTypeName}] {message}";
        }

        // 从调用栈中查找第一个非 QLog 的调用类型
        private static string GetCallerTypeName()
        {
            StackTrace stackTrace = new StackTrace(false);

            for (int i = 1; i < stackTrace.FrameCount; i++)
            {
                MethodBase method = stackTrace.GetFrame(i)?.GetMethod();
                Type declaringType = method?.DeclaringType;

                if (declaringType == null || declaringType == typeof(QLog))
                    continue;

                return declaringType.Name;
            }

            return string.Empty;
        }
        #endregion
    }
}
