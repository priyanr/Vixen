﻿using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Vixen.Module;
using System.Drawing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace VixenModules.App.SuperScheduler
{
	public enum StateType
	{
		Waiting,
		Startup,
		Running,
		Shutdown,
		//Stop,
		Paused,
		Changed
	}

	[DataContract]
	public class ScheduleItem: IDisposable
	{
		#region Data Members

		[DataMember]
		public Guid ShowID { get; set; }

		[DataMember]
		public bool Monday;
		[DataMember]
		public bool Tuesday;
		[DataMember]
		public bool Wednesday;
		[DataMember]
		public bool Thursday;
		[DataMember]
		public bool Friday;
		[DataMember]
		public bool Saturday;
		[DataMember]
		public bool Sunday;
		[DataMember]
		public bool Enabled = true;

		#endregion // Data Members

		#region Variables

		Shows.ShowItem _currentItem = null;
		//Shows.Action _currentAction = null;
		//private readonly Dictionary<string, IProgramContext> _cachedPrograms;
		CancellationTokenSource tokenSourcePreProcess = new CancellationTokenSource();
		CancellationToken tokenPreProcess;

		#endregion // Variables

		#region Properties

		public bool ManuallyStarted { get; set; }
		public bool ManuallyStopped { get; set; }

		private List<Shows.Action> runningActions = null;
		public List<Shows.Action> RunningActions
		{
			get
			{
				if (runningActions == null)
					runningActions = new List<Shows.Action>();
				return runningActions;
			}
		}

		private StateType _state = StateType.Waiting;
		public StateType State
		{
			get { return _state; }
			set { _state = value; }
		}

		private Shows.Show _show = null;
		public Shows.Show Show {
			get 
			{
				if (_show == null)
					_show = Shows.ShowsData.GetShow(ShowID);
				return _show;
			}
		}

		private DateTime _startDate;
		[DataMember]
		public DateTime StartDate
		{
			get
			{
				return _startDate;
			}
			set
			{
				_startDate = new DateTime(value.Year, value.Month, value.Day);
			}
		}

		private DateTime _endDate;
		[DataMember]
		public DateTime EndDate
		{
			get
			{
				return _endDate;
			}
			set
			{
				_endDate = new DateTime(value.Year, value.Month, value.Day);
			}
		}

		private DateTime _startTime;
		[DataMember]
		public DateTime StartTime
		{
			get
			{
				return _startTime;
			}
			set
			{
				_startTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
										 value.Hour, value.Minute, value.Second);
			}
		}

		private DateTime _endTime;
		[DataMember]
		public DateTime EndTime
		{
			get
			{
				return _endTime;
			}
			set
			{
				_endTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
										 value.Hour, value.Minute, value.Second);
				if (_endTime < StartTime)
					_endTime.AddDays(1);
			}
		}

		private DateTime StartDateTime {
			get 
			{
				return new DateTime(StartDate.Year, StartDate.Month, StartDate.Day, StartTime.Hour, StartTime.Minute, StartTime.Second);
			}
		}

		private DateTime EndDateTime
		{
			get
			{
				return new DateTime(EndDate.Year, EndDate.Month, EndDate.Day, EndTime.Hour, EndTime.Minute, EndTime.Second);
			}
		}

		private Queue<Shows.ShowItem> itemQueue;
		public Queue<Shows.ShowItem> ItemQueue
		{
			get
			{
				if (itemQueue == null)
					itemQueue = new Queue<Shows.ShowItem>();
				return itemQueue;
			}
			set
			{
				itemQueue = value;
			}
		}

		private List<Shows.Action> backgroundActions;
		public List<Shows.Action> BackgroundActions
		{
			get
			{
				if (backgroundActions == null)
					backgroundActions = new List<Shows.Action>();
				return backgroundActions;
			}
			set
			{
				backgroundActions = value;
			}
		}

		//private Queue<Shows.ShowItem> backgroundItemQueue;
		//public Queue<Shows.ShowItem> BackgroundItemQueue
		//{
		//    get
		//    {
		//        if (backgroundItemQueue == null)
		//            backgroundItemQueue = new Queue<Shows.ShowItem>();
		//        return backgroundItemQueue;
		//    }
		//    set
		//    {
		//        backgroundItemQueue = value;
		//    }
		//}

		#endregion //Properties

		#region Show Control

		public void ProcessSchedule()
		{
			if (Show != null)
			{
				switch (State)
				{
					case StateType.Waiting:
						CheckForStartup();
						break;
					//case StateType.Changed:
					//	Restart();
					//	break;
					case StateType.Paused:
						//Pause();
						break;
					case StateType.Startup:
						//ProcessStartup();
						break;
					case StateType.Shutdown:
						//ProcessShutdown();
						//Shutdown();
						break;
					//default:
					//	Process();
					//	break;
				}
			}
		}

		public void CheckForStartup()
		{
			//ScheduleExecutor.AddSchedulerLogEntry(Show.Name, "CheckForStartup 1: " + StartDate.ToString() + "->" + EndDate.ToString() + " : " + StartDate.CompareTo(DateTime.Now) + ":" + EndDate.CompareTo(DateTime.Now));
			//Console.WriteLine(Show.Name, "CheckForStartup 2: " + StartTime.ToString() + "->" + EndTime.ToString());
			//Console.WriteLine(Enabled.ToString() +
			//	(StartDate.CompareTo(DateTime.Now) <= 0).ToString() +
			//	(EndDate.CompareTo(DateTime.Now) >= 0).ToString() +
			//	(StartTime.CompareTo(DateTime.Now) <= 0).ToString() +
			//	(EndTime.CompareTo(DateTime.Now) >= 0).ToString());

			// If we're manually stopped, is is exactly (within 5 seconds) time to start the show again?
			if (ManuallyStopped
				&& StartTime.Hour == DateTime.Now.Hour
				&& StartTime.Minute == DateTime.Now.Minute
				&& StartTime.Second - DateTime.Now.Second >= 5)
			{
				ManuallyStopped = false;
			}
			else if (ManuallyStopped)
			{
				// If we're manually stopped, return without doing anything
				return;
			}

			if (
				Enabled
				&& StartDateTime.CompareTo(DateTime.Now) >= 0
				&& EndDateTime.CompareTo(DateTime.Now) <= 0
			   )
			{
				Start(false);
			}
		}

		//public void Process()
		//{
		//	if (CheckForShutdown())
		//	{
		//		Shutdown();
		//	}
		//	else
		//	{
		//	}
		//}

		private bool CheckForShutdown()
		{
			return (EndTime.CompareTo(DateTime.Now) <= 0 || State == StateType.Shutdown);
		}

		//private void ProcessShutdown()
		//{
		//	State = StateType.Shutdown;
		//}

		//public void Restart()
		//{
		//	ScheduleExecutor.AddSchedulerLogEntry(Show.Name, "Restarting show");
		//}

		public void Stop(bool graceful)
		{
			ManuallyStopped = true;

			if (graceful)
			{
				State = StateType.Shutdown;
			} else
			{
				State = StateType.Waiting;
				int runningCount = RunningActions.Count();
				for (int i = 0; i < runningCount; i++)
				{
					if (i < RunningActions.Count())
					{
						ScheduleExecutor.AddSchedulerLogEntry(Show.Name, "Stopping action: " + RunningActions[i].ShowItem.Name);
						RunningActions[i].Stop();
						if (tokenPreProcess.CanBeCanceled)
							tokenSourcePreProcess.Cancel();
					}
				}
			}

			ScheduleExecutor.AddSchedulerLogEntry(Show.Name, "Show stopped " + (graceful ? "gracefully" : "immediately"));
		}

		public void Pause()
		{
			//ScheduleExecutor.AddSchedulerLogEntry(Show.Name, "Paused action: " + _currentAction.ShowItem.Name);
		}

		public void Shutdown()
		{
			ScheduleExecutor.AddSchedulerLogEntry(Show.Name, "Shutdown Requested");
			State = StateType.Shutdown;
		}

		public void Start(bool manuallyStarted)
		{
			ScheduleExecutor.AddSchedulerLogEntry(Show.Name, "Show starting");
			ManuallyStarted = manuallyStarted;
			BeginStartup();				
		}

		private void ExecuteAction(Shows.Action action)
		{
			if (State != StateType.Waiting)
			{
				if (!action.PreProcessingCompleted)
				{
					ScheduleExecutor.AddSchedulerLogEntry(Show.Name, "Pre-processing action: " + action.ShowItem.Name);

					// Do this in a task so we don't stop Vixen while pre-processing!
					Task preProcessTask = new Task(() => action.PreProcess(), tokenPreProcess);
					preProcessTask.ContinueWith(task =>
						{
							ScheduleExecutor.AddSchedulerLogEntry(Show.Name, "Starting action: " + action.ShowItem.Name);
							action.Execute();
							RunningActions.Add(action);
						}
					);

					preProcessTask.Start();
				}
				else
				{
					ScheduleExecutor.AddSchedulerLogEntry(Show.Name, "Starting action: " + action.ShowItem.Name);
					action.Execute();
					RunningActions.Add(action);
				}
			}
		}

		#endregion // Show Control

		#region Startup Items

		public void BeginStartup()
		{
			if (Show != null)
			{
				State = StateType.Startup;
				foreach (Shows.ShowItem item in Show.GetItems(Shows.ShowItemType.Startup))
				{
					ItemQueue.Enqueue(item);
				}

				ExecuteNextStartupItem();
			}
		}

		public void ExecuteNextStartupItem() 
		{
			if (ItemQueue.Count() > 0)
			{
				_currentItem = ItemQueue.Dequeue();
				Shows.Action action = _currentItem.GetAction();
				action.ActionComplete += OnStartupActionComplete;
				ExecuteAction(action);
				// Otherwise, move on to the sequential items
			}
			else
			{
				//State = StateType.Running;
				BeginSequential();
				BeginBackground();
			}
		}

		public void OnStartupActionComplete(object sender, EventArgs e)
		{
			Shows.Action action = (sender as Shows.Action);
			action.ActionComplete -= OnStartupActionComplete;
			RunningActions.Remove(action);
			ScheduleExecutor.AddSchedulerLogEntry(Show.Name, "Startup action complete: " + action.ShowItem.Name);
			ExecuteNextStartupItem();
		}

		#endregion // Startup Items

		#region Sequential Items

		public void BeginSequential()
		{
			if (Show != null)
			{
				State = StateType.Running;
				foreach (Shows.ShowItem item in Show.GetItems(Shows.ShowItemType.Sequential))
				{
					ItemQueue.Enqueue(item);
				}

				if (ItemQueue.Count() > 0) 
					ExecuteNextSequentialItem();
			}
		}

		public void ExecuteNextSequentialItem()
		{
			if (State == StateType.Running)
			{
				if (ItemQueue.Count() > 0)
				{
					_currentItem = ItemQueue.Dequeue();
					Shows.Action action = _currentItem.GetAction();
					action.ActionComplete += OnSequentialActionComplete;
					ExecuteAction(action);
				}
				else
				{
					// Restart the queue 
					BeginSequential();
				}
			}
		}

		public void OnSequentialActionComplete(object sender, EventArgs e)
		{
			Shows.Action action = (sender as Shows.Action);
			action.ActionComplete -= OnSequentialActionComplete;
			RunningActions.Remove(action);
			ScheduleExecutor.AddSchedulerLogEntry(Show.Name, "Sequential action complete: " + action.ShowItem.Name);
			if (!StartShutdownIfRequested())
			{
				ExecuteNextSequentialItem();
			}
		}

		#endregion // Startup Items

		#region Background Items

		public void BeginBackground()
		{
			if (Show != null)
			{
				foreach (Shows.ShowItem item in Show.GetItems(Shows.ShowItemType.Background))
				{
					Shows.Action action = item.GetAction();
					BackgroundActions.Add(action);
					ExecuteBackgroundAction(action);
				}
			}
		}

		public void ExecuteBackgroundAction(Shows.Action action)
		{
			action.ActionComplete += OnBackgroundActionComplete;
			ExecuteAction(action);
		}

		public void OnBackgroundActionComplete(object sender, EventArgs e)
		{
			Shows.Action action = (sender as Shows.Action);
			action.ActionComplete -= OnBackgroundActionComplete;
			RunningActions.Remove(action);
			ScheduleExecutor.AddSchedulerLogEntry(Show.Name, "Background action complete: " + action.ShowItem.Name);

			// Run it again and again and again and again and again and again and again and...
			if (!CheckForShutdown())
			{
				ExecuteBackgroundAction(action);
			}
		}

		public void StopBackground()
		{
			foreach (Shows.Action action in BackgroundActions)
			{
				action.Stop();
			}
		}

		#endregion // Background Items

		#region Shutdown Items

		public bool StartShutdownIfRequested() {
			if (State == StateType.Shutdown || CheckForShutdown())
			{
				BeginShutdown();
				return true;
			}
			return false;
		}

		public void BeginShutdown()
		{
			if (Show != null)
			{
				ScheduleExecutor.AddSchedulerLogEntry(Show.Name, "Starting shutdown");

				StopBackground();

				State = StateType.Shutdown;
				foreach (Shows.ShowItem item in Show.GetItems(Shows.ShowItemType.Shutdown))
				{
					ItemQueue.Enqueue(item);
				}

				ExecuteNextShutdownItem();
			}
		}

		public void ExecuteNextShutdownItem()
		{
			if (ItemQueue.Count() > 0)
			{
				_currentItem = ItemQueue.Dequeue();
				Shows.Action action = _currentItem.GetAction();
				action.ActionComplete += OnShutdownActionComplete;
				ExecuteAction(action);
				// Otherwise, the show is done :(
			}
			else
			{
				State = StateType.Waiting;
				ScheduleExecutor.AddSchedulerLogEntry(Show.Name, "Show complete");
			}
		}

		public void OnShutdownActionComplete(object sender, EventArgs e)
		{
			Shows.Action action = (sender as Shows.Action);
			action.ActionComplete -= OnShutdownActionComplete;
			RunningActions.Remove(action);
			ScheduleExecutor.AddSchedulerLogEntry(Show.Name, "Shutdown action complete: " + action.ShowItem.Name);
			ExecuteNextShutdownItem();
		}

		#endregion // Shutdown Items

		public void Dispose()
		{
			if (tokenPreProcess.CanBeCanceled)
				tokenSourcePreProcess.Cancel();
		}
	}
}
