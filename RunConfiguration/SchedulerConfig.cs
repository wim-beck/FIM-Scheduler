﻿/**
 * IS4U's Forefront Identity Manager Scheduler is created to schedule automated 
 * run profiles using configuration files on the Synchronization Service.
 * 
 * Copyright (C) 2013 by IS4U (info@is4u.be)
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation version 3.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * A full copy of the GNU General Public License can be found 
 * here: http://opensource.org/licenses/gpl-3.0.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Xml.Linq;
using IS4U.Constants;
using NLog;

namespace IS4U.RunConfiguration
{
	/// <summary>
	/// Scheduler configuration.
	/// </summary>
	public class SchedulerConfig
	{
		private Logger logger = LogManager.GetLogger("");

		/// <summary>
		/// Flag indicating whether or not to clear the run history.
		/// </summary>
		public bool ClearRunHistory { get; internal set; }
		/// <summary>
		/// Number of days to keep the run history.
		/// </summary>
		public int KeepHistory { get; internal set; }
		/// <summary>
		/// Delay between start of the management agent runs in a parallel sequence.
		/// </summary>
		public static int DelayInParallelSequence { get; internal set; }
		/// <summary>
		/// Delay between start of the management agent runs in a linear sequence.
		/// </summary>
		public static int DelayInLinearSequence { get; internal set; }
		/// <summary>
		/// Key: name of the run configuration.
		/// Value: linear sequence representing a run configuration.
		/// </summary>
		public Dictionary<string, LinearSequence> RunConfigurations { get; internal set; }
		/// <summary>
		/// Key: name of the sequence.
		/// Value: list of steps in the sequence.
		/// </summary>
		public Dictionary<string, List<Step>> Sequences { get; internal set; }
		/// <summary>
		/// On demand schedule name.
		/// </summary>
		public static string OnDemandSchedule { get; private set; }

		private string configFile;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="configFile">Configuration file.</param>
		public SchedulerConfig(string configFile)
		{
			this.configFile = configFile;
			RunConfigurations = new Dictionary<string, LinearSequence>();
			Sequences = new Dictionary<string, List<Step>>();
			DelayInParallelSequence = -1;
			KeepHistory = -1;
			GenerateReport = false;
			ClearRunHistory = false;

			if (!string.IsNullOrEmpty(configFile) && File.Exists(configFile))
			{
				XElement root = XDocument.Load(configFile).Root;
				setClearRunHistory(root.Element("ClearRunHistory"));
				setKeepHistory(root.Element("KeepHistory"));
				setDelayInParallelSequence(root.Element("DelayInParallelSequence"));
				setDelayInLinearSequence(root.Element("DelayInLinearSequence"));
				setOnDemandSchedule(root.Element("OnDemandSchedule"));
				Sequences = (from sequence in root.Elements("Sequence")
								 select new Sequence(sequence)).ToDictionary(seq => seq.Name, seq => seq.Steps,
																							StringComparer.CurrentCultureIgnoreCase);
				RunConfigurations = (from runConfig in root.Elements("RunConfiguration")
											select new LinearSequence(runConfig)).ToDictionary(runConfig => runConfig.Name, runConfig => runConfig, StringComparer.CurrentCultureIgnoreCase);
			}
			else
			{
				logger.Error("Run configuration xml configuration file not found.");
			}
		}

		/// <summary>
		/// This method will run the passed run configuration, if it is present in the configuration.
		/// </summary>
		/// <param name="runConfigurationName">Desired run configuration.</param>
		public void Run(string runConfigurationName)
		{
			if (RunConfigurations.ContainsKey(runConfigurationName))
			{
				LinearSequence runConfiguration = RunConfigurations[runConfigurationName];
				foreach (Step step in runConfiguration.StepsToRun)
				{
					// we pass the third parameter to allow execution of several run profiles and
					// because different run profiles can contain the same sequences.
					step.Initialize(Sequences, runConfiguration.DefaultRunProfile, 0);
					step.Run();
					logger.Info("Running step: " + step.Name);
				}
				if (ClearRunHistory)
				{
					clearRunHistory();
				}
			}
			else
			{
				logger.Error(string.Format("Run configuration '{0}' not found.", runConfigurationName));
			}
		}

		/// <summary>
		/// Run on demand schedule.
		/// </summary>
		public void RunOnDemand()
		{
			Run(OnDemandSchedule);
		}

		/// <summary>
		/// Clear the run history.
		/// </summary>
		private void clearRunHistory()
		{
			if (KeepHistory > 0)
			{
				TimeSpan days = new TimeSpan(KeepHistory, 0, 0, 0);
				DateTime utc = DateTime.UtcNow.Subtract(days);
				DateTime local = utc.ToLocalTime();
				string date = utc.ToString("yyyy-MM-dd HH:mm:ss.fff");

				logger.Info(string.Concat("Clear run history before ", local.ToString("yyyy-MM-dd HH:mm:ss")));

				ManagementScope mgmtScope = new ManagementScope(Constant.FIM_WMI_NAMESPACE);
				SelectQuery query = new SelectQuery("Select * from MIIS_Server");
				using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(mgmtScope, query))
				{
					foreach (ManagementObject obj in searcher.Get())
					{
						using (ManagementObject wmiServerObject = obj)
						{
							string status = wmiServerObject.InvokeMethod("ClearRuns", new object[] { date }).ToString();
							logger.Info(string.Concat("Done clearing history. Status: ", status));
						}
					}
				}
			}
		}

		/// <summary>
		/// Sets the number of days to keep history.
		/// </summary>
		/// <param name="history">Xelement containing the xml configuration.</param>
		private void setClearRunHistory(XElement clearRunHistory)
		{
			if (clearRunHistory != null)
			{
				try
				{
					ClearRunHistory = Convert.ToBoolean(clearRunHistory.Value);
				}
				catch (FormatException fe)
				{
					throw new Exception("ClearRunHistory is not a valid boolean: " + fe.Message);
				}
			}
		}

		/// <summary>
		/// Sets the number of days to keep history.
		/// </summary>
		/// <param name="history">Xelement containing the xml configuration.</param>
		private void setKeepHistory(XElement history)
		{
			if (history != null && history.Attribute("Days") != null)
			{
				try
				{
					KeepHistory = Convert.ToInt32(history.Attribute("Days").Value);
				}
				catch (FormatException fe)
				{
					throw new Exception("DaysToKeepHistory is not a valid number: " + fe.Message);
				}
			}
		}

		/// <summary>
		/// Sets the number of seconds to wait between starting threads in a parallel sequence.
		/// </summary>
		/// <param name="delay">Xelement containing the xml configuration.</param>
		private void setDelayInParallelSequence(XElement delay)
		{
			if (delay != null && delay.Attribute("Seconds") != null)
			{
				try
				{
					DelayInParallelSequence = Convert.ToInt32(delay.Attribute("Seconds").Value);
				}
				catch (FormatException fe)
				{
					throw new Exception("DelayInParallelSequence is not a valid number: " + fe.Message);
				}
			}
		}

		/// <summary>
		/// Sets the number of seconds to wait between steps in a linear sequence.
		/// </summary>
		/// <param name="delay">Xelement containing the xml configuration.</param>
		private void setDelayInLinearSequence(XElement delay)
		{
			if (delay != null && delay.Attribute("Seconds") != null)
			{
				try
				{
					DelayInLinearSequence = Convert.ToInt32(delay.Attribute("Seconds").Value);
				}
				catch (FormatException fe)
				{
					throw new Exception("DelayInLinearSequence is not a valid number: " + fe.Message);
				}
			}
		}

		/// <summary>
		/// Sets the on demand schedule.
		/// </summary>
		/// <param name="delay">Xelement containing the xml configuration.</param>
		private void setOnDemandSchedule(XElement onDemandSchedule)
		{
			if (onDemandSchedule != null)
			{
				OnDemandSchedule = onDemandSchedule.Value;
			}
		}
	}
}
