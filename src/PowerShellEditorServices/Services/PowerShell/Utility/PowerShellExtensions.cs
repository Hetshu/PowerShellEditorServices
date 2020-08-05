﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility
{
    internal static class PowerShellExtensions
    {
        private static readonly Action<SMA.PowerShell> s_waitForServicingComplete;

        private static readonly Action<SMA.PowerShell> s_suspendIncomingData;

        private static readonly Action<SMA.PowerShell> s_resumeIncomingData;

        static PowerShellExtensions()
        {
            s_waitForServicingComplete = (Action<SMA.PowerShell>)Delegate.CreateDelegate(
                typeof(Action<SMA.PowerShell>),
                typeof(SMA.PowerShell).GetMethod("WaitForServicingComplete", BindingFlags.Instance | BindingFlags.NonPublic));

            s_suspendIncomingData = (Action<SMA.PowerShell>)Delegate.CreateDelegate(
                typeof(Action<SMA.PowerShell>),
                typeof(SMA.PowerShell).GetMethod("SuspendIncomingData", BindingFlags.Instance | BindingFlags.NonPublic));

            s_resumeIncomingData = (Action<SMA.PowerShell>)Delegate.CreateDelegate(
                typeof(Action<SMA.PowerShell>),
                typeof(SMA.PowerShell).GetMethod("ResumeIncomingData", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        public static Collection<TResult> InvokeAndClear<TResult>(this SMA.PowerShell pwsh)
        {
            try
            {
                return pwsh.Invoke<TResult>();
            }
            finally
            {
                pwsh.Commands.Clear();
            }
        }

        public static void InvokeAndClear(this SMA.PowerShell pwsh)
        {
            try
            {
                pwsh.Invoke();
            }
            finally
            {
                pwsh.Commands.Clear();
            }
        }

        public static Collection<TResult> InvokeCommand<TResult>(this SMA.PowerShell pwsh, PSCommand psCommand)
        {
            pwsh.Commands = psCommand;
            return pwsh.InvokeAndClear<TResult>();
        }

        public static void InvokeCommand(this SMA.PowerShell pwsh, PSCommand psCommand)
        {
            pwsh.Commands = psCommand;
            pwsh.InvokeAndClear();
        }

        public static void WaitForRemoteOutputIfNeeded(this SMA.PowerShell pwsh)
        {
            if (!pwsh.Runspace.RunspaceIsRemote)
            {
                return;
            }

            s_waitForServicingComplete(pwsh);
            s_suspendIncomingData(pwsh);
        }

        public static void ResumeRemoteOutputIfNeeded(this SMA.PowerShell pwsh)
        {
            if (!pwsh.Runspace.RunspaceIsRemote)
            {
                return;
            }

            s_resumeIncomingData(pwsh);
        }

        public static string GetErrorString(this SMA.PowerShell pwsh)
        {
            var sb = new StringBuilder(capacity: 1024)
                .Append("Execution of the following command(s) completed with errors:")
                .AppendLine()
                .AppendLine()
                .Append(pwsh.Commands.GetInvocationText());

            sb.AddErrorString(pwsh.Streams.Error[0], errorIndex: 1);
            for (int i = 1; i < pwsh.Streams.Error.Count; i++)
            {
                sb.AppendLine().AppendLine();
                sb.AddErrorString(pwsh.Streams.Error[i], errorIndex: i + 1);
            }

            return sb.ToString();
        }

        private static StringBuilder AddErrorString(this StringBuilder sb, ErrorRecord error, int errorIndex)
        {
            sb.Append("Error #").Append(errorIndex).Append(':').AppendLine()
                .Append(error).AppendLine()
                .Append("ScriptStackTrace:").AppendLine()
                .Append(error.ScriptStackTrace ?? "<null>").AppendLine()
                .Append("Exception:").AppendLine()
                .Append("    ").Append(error.Exception.ToString() ?? "<null>");

            Exception innerException = error.Exception?.InnerException;
            while (innerException != null)
            {
                sb.Append("InnerException:").AppendLine()
                    .Append("    ").Append(innerException);
                innerException = innerException.InnerException;
            }

            return sb;
        }
    }
}