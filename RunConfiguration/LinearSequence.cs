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
using System.Linq;
using System.Xml.Linq;
using NLog;

namespace IS4U.RunConfiguration
{
	/// <summary>
	/// Represents a linear sequence.
	/// </summary>
	public class LinearSequence : Step
	{
		/// <summary>
		/// Default constructor.
		/// </summary>
		public LinearSequence() { }

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="runConfig">Xml configuration.</param>
		/// <param name="logger">Logger.</param>
		public LinearSequence(XElement runConfig, Logger logger)
		{
			if (runConfig.Attribute("Name") != null)
			{
				Name = runConfig.Attribute("Name").Value;
			}
			else
			{
				Name = Guid.NewGuid().ToString();
			}
			if (runConfig.Attribute("Profile") != null)
			{
				DefaultRunProfile = runConfig.Attribute("Profile").Value;
			}
			else
			{
				DefaultRunProfile = Guid.NewGuid().ToString();
			}
			StepsToRun = (from step in runConfig.Elements("Step")
										select GetStep(step, logger)).ToList();
			Count = 0;
		}

		/// <summary>
		/// Executes a linear execution of the different steps.
		/// </summary>
		public override void Run()
		{
			foreach (Step step in StepsToRun)
			{
				step.Run();
			}
		}
	}
}
