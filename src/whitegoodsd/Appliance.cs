using System;
using System.Globalization;

namespace whitegoodsd
{
	internal enum ApplianceState
	{
		Stopped = 0,
		Running = 1
	}

	internal sealed class Appliance
	{
		private int m_SampleOffset;
		private bool[] m_Samples;

		public Appliance(string name, string smartplug, int threshold_running_mw, int minsamples)
		{
			Name = name;
			Smartplug = smartplug;
			ThresholdRunningMW = threshold_running_mw;
			State = ApplianceState.Stopped;
			m_Samples = new bool[minsamples];
		}

		public string Name { get; private set; }
		public string Smartplug { get; private set; }
		public int ThresholdRunningMW { get; private set; }

		public ApplianceState State { get; set; }

		public bool AllSamplesIndicateStopped
		{
			get
			{
				for (int i = 0; i < m_Samples.Length; i++)
					if (m_Samples[i]) return false;

				return true;
			}
		}

		public bool AllSamplesIndicateRunning
		{
			get
			{
				for (int i = 0; i < m_Samples.Length; i++)
					if (!m_Samples[i]) return false;

				return true;
			}
		}

		public void Sample(bool running)
		{
			m_Samples[m_SampleOffset] = running;
			m_SampleOffset = (m_SampleOffset + 1) % m_Samples.Length;
		}

		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture,
				"Appliance {0} ({1})", Name, Smartplug);
		}
	}
}
