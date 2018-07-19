#if UNIX
using Mono.Unix;
using System.Runtime.InteropServices;
#endif
using Mono.Unix.Native;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;

namespace whitegoodsd
{
	public static class Program
	{
		private static bool LOG_DEBUG = true;

		private static int MONITOR_INTERVAL, MONITOR_INTERVAL_NOTHING_RUNNING;

		private static ReadOnlyCollection<Appliance> APPLIANCE_LIST;

		public static int Main(string[] args)
		{
#if UNIX
			const string DAEMON_NAME = "whitegoodsd";
			IntPtr DAEMON_NAME_HANDLE = Marshal.StringToHGlobalAuto(DAEMON_NAME);

			Syscall.openlog(DAEMON_NAME_HANDLE, SyslogOptions.LOG_PERROR | SyslogOptions.LOG_PID,
				SyslogFacility.LOG_LOCAL5);
#endif
			Log(SyslogLevel.LOG_INFO, "Starting.");

			#region Load Settings
			try
			{
				// *sigh& Application.ExecutablePath is only available in System.Windows.Forms.
				string settingsPath = string.Format(CultureInfo.InvariantCulture, "{0}{1}whitegoodsd.ini",
					Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath),
					Path.DirectorySeparatorChar);

				Log(SyslogLevel.LOG_INFO, "Loading settings from: \"{0}\"", settingsPath);

				FuckINI ini = new FuckINI(settingsPath);

				Log(SyslogLevel.LOG_INFO, "Loading setting whitegoodsd.monitor_interval");
				MONITOR_INTERVAL = int.Parse(ini.Get("whitegoodsd",
					"monitor_interval"), NumberStyles.None);

				Log(SyslogLevel.LOG_INFO, "Loading setting whitegoodsd.monitor_interval_nothing_running");
				MONITOR_INTERVAL_NOTHING_RUNNING = int.Parse(ini.Get("whitegoodsd",
					"monitor_interval_nothing_running"), NumberStyles.None);

				Log(SyslogLevel.LOG_INFO, "Loading setting whitegoodsd.log_debug");
				LOG_DEBUG = (ini.Get("whitegoodsd", "log_debug") == "1");

				Log(SyslogLevel.LOG_INFO, "Loading setting whitegoodsd.appliances");
				string[] appliances = ini.Get("whitegoodsd", "appliances").Split(",".ToCharArray(),
					StringSplitOptions.RemoveEmptyEntries);

				List<Appliance> applianceList = new List<Appliance>();

				foreach (string appliance in appliances)
				{
					string smartplug;
					int threshold_running_mw, minsamples;

					Log(SyslogLevel.LOG_INFO, "Loading setting {0}.smartplug", appliance);
					smartplug = ini.Get(appliance, "smartplug");

					Log(SyslogLevel.LOG_INFO, "Loading setting {0}.threshold_running_mw", appliance);
					threshold_running_mw = int.Parse(ini.Get(appliance, "threshold_running_mw"), NumberStyles.None);

					Log(SyslogLevel.LOG_INFO, "Loading setting {0}.minsamples", appliance);
					minsamples = int.Parse(ini.Get(appliance, "minsamples"), NumberStyles.None);

					Appliance a = new Appliance(appliance, smartplug, threshold_running_mw, minsamples);

					applianceList.Add(a);
				}

				APPLIANCE_LIST = applianceList.AsReadOnly();
			}
			catch (Exception ex)
			{
				Log(SyslogLevel.LOG_ERR, "Unable to read settings: {0}", ex);
				return 1;
			}
			#endregion

			Thread thread = new Thread(ThreadMain);

			Log(SyslogLevel.LOG_INFO, "Starting worker thread.");

			thread.Start();

#if UNIX
			UnixSignal[] signals = new UnixSignal[] {
				new UnixSignal(Signum.SIGINT),
				new UnixSignal(Signum.SIGTERM),
			};

			// Wait for a unix signal
			do
			{
				int id = UnixSignal.WaitAny(signals);

				if (id >= 0 && id < signals.Length)
					if (signals[id].IsSet) break;

			} while (true);
#else
			Console.WriteLine("Press ENTER to exit.");
			Console.ReadLine();
#endif

			Log(SyslogLevel.LOG_INFO, "Exiting on signal.");

			thread.Abort();

#if UNIX
			Syscall.closelog();
			Marshal.FreeHGlobal(DAEMON_NAME_HANDLE);
#endif

			return 0;
		}

		private static void ThreadMain()
		{
			dynamic TPLINK_RESULT;

			try
			{
				again:
				bool have_running = false;

				Log(SyslogLevel.LOG_DEBUG, "Next iteration starts.");

				foreach (Appliance a in APPLIANCE_LIST)
				{
					Log(SyslogLevel.LOG_DEBUG, "{0} polling. Current state: {1}.", a.Name, a.State);

					int power_mw;

					try
					{
						TPLINK_RESULT = TPLINK.TPLINK_EMETER_GET_REALTIME(a.Smartplug);
						power_mw = TPLINK_RESULT["emeter"]["get_realtime"]["power_mw"];
					}
					catch (Exception ex)
					{
						Log(SyslogLevel.LOG_ERR, "Error getting current usage for {0}: {1}", a.Name, ex);
						have_running = true; // Force next iteration to be early
						continue;
					}

					Log(SyslogLevel.LOG_DEBUG, "{0} current usage is {1:n0} mW; threshold is {2:n0} mW",
							a.Name, power_mw, a.ThresholdRunningMW);

					a.Sample(power_mw >= a.ThresholdRunningMW);

					if (a.State == ApplianceState.Stopped && a.AllSamplesIndicateRunning)
					{
						a.State = ApplianceState.Running;
						Log(SyslogLevel.LOG_INFO, "{0} is RUNNING.", a.Name);
					}
				
					if (a.State == ApplianceState.Running && a.AllSamplesIndicateStopped)
					{
						a.State = ApplianceState.Stopped;
						Log(SyslogLevel.LOG_INFO, "{0} is FINISHED.", a.Name);
					}

					have_running =
						have_running ||
						power_mw >= a.ThresholdRunningMW ||
						a.State == ApplianceState.Running;
				}
				
				int sleep_duration;

				if (have_running)
					sleep_duration = MONITOR_INTERVAL;
				else
					sleep_duration = MONITOR_INTERVAL_NOTHING_RUNNING;

				Log(SyslogLevel.LOG_DEBUG, "Sleeping {0:n0} seconds before next iteration.",
					sleep_duration);

				Thread.Sleep(sleep_duration * 1000);

				have_running = false;
				goto again;
			}
			catch (ThreadAbortException)
			{
				Log(SyslogLevel.LOG_INFO, "Thread aborted.");
			}
		}

		private static void Log(SyslogLevel level, string format, params object[] args)
		{
			Log(level, string.Format(CultureInfo.InvariantCulture, format, args));
		}

		private static void Log(SyslogLevel level, string message)
		{
			if (level == SyslogLevel.LOG_DEBUG && !LOG_DEBUG)
				return;

#if UNIX
			Syscall.syslog(level, message);
#else
			Console.WriteLine("{0}: [{1}] {2}", DateTime.Now, level, message);
#endif
		}
	}
}