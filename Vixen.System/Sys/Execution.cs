﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vixen.Execution;
using System.Diagnostics;
using System.Threading;
using Vixen.Sys;
using Vixen.Module.Effect;
using Vixen.Commands;

namespace Vixen.Sys {
	public class Execution {
		static private Dictionary<Guid, ProgramContext> _contexts = new Dictionary<Guid, ProgramContext>();
		static internal SystemClock SystemTime = new SystemClock();
		static private Thread _channelReadThread;
		// Creating channels in here instead of VixenSystem so that the collection
		// will be locally available for EffectRenderer instances.
		static private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

		// These are system-level events.
		static public event EventHandler<ProgramContextEventArgs> ProgramContextCreated;
		static public event EventHandler<ProgramContextEventArgs> ProgramContextReleased;
		static public event EventHandler NodesChanged {
			add { NodeManager.NodesChanged += value; }
			remove { NodeManager.NodesChanged -= value; }
		}
		static public event Action<ExecutionStateValues> ValuesChanged;

		public enum ExecutionState { Starting, Started, Stopping, Stopped };
		static private volatile ExecutionState _state = ExecutionState.Stopped;
		static public event Action<ExecutionState> ExecutionStateChanged;

		/// <summary>
		/// Allows data to be executed.
		/// </summary>
		static public bool OpenExecution() {
			if(_State == ExecutionState.Stopped) {
				VixenSystem.Logging.Info("Vixen Execution Engine starting...");
				_State = ExecutionState.Starting;

				// Open the channels.
				VixenSystem.Channels.OpenChannels();

				SystemTime.Start();
				VixenSystem.Controllers.OpenControllers();
				_channelReadThread = new Thread(_ChannelReaderThread);
				_channelReadThread.Start();
				_State = ExecutionState.Started;
				VixenSystem.Logging.Info("Vixen Execution Engine started.");
				return true;
			}
			return false;
		}

		static public bool CloseExecution() {
			if(_State == ExecutionState.Started) {
				VixenSystem.Logging.Info("Vixen Execution Engine stopping...");
				// Release all contexts.
				// Releasing a context removes it from the collection, so
				// enumerate a copy of the collection.
				foreach(ProgramContext context in _contexts.Values.ToArray()) {
					ReleaseContext(context);
				}
				// Stop reading from channels.
				_State = ExecutionState.Stopping;
				while(_State != ExecutionState.Stopped) { Thread.Sleep(1); }
				_channelReadThread = null;
				// Close the channels.
				VixenSystem.Channels.CloseChannels();
				// Stop the controllers.
				VixenSystem.Controllers.CloseControllers();
				// Stop internal timing.
				SystemTime.Stop();
				VixenSystem.Logging.Info("Vixen Execution Engine stopped.");
				return true;
			}
			return false;
		}

		// Something went kaflooey with the threaded use of the _state variable,
		// so it's been wrapped in the safe and fluffy blankets of this property.
		static private ExecutionState _State {
			get {
				_lock.EnterReadLock();
				try {
					return _state;
				} finally {
					_lock.ExitReadLock();
				}
			}
			set {
				_lock.EnterWriteLock();
				try {
					_state = value;
				} finally {
					_lock.ExitWriteLock();
				}
				if (ExecutionStateChanged != null)
					ExecutionStateChanged(value);
			}
		}

		static public ExecutionState State
		{
			get { return _State; }
		}


		static public TimeSpan CurrentExecutionTime { get { return (SystemTime.IsRunning) ? SystemTime.Position : TimeSpan.Zero; } }

		static public string CurrentExecutionTimeString { get { return CurrentExecutionTime.ToString("m\\:ss\\.fff"); } }

		static private void _ChannelReaderThread() {
			// Our mission:
			// Read data from the channel enumerators and write to the channel patches.

			while(_State != ExecutionState.Stopping) {
				_UpdateChannelStates();
				Thread.Sleep(1);
			}
			_State = ExecutionState.Stopped;
		}

		static private void _UpdateChannelStates() {
			ExecutionStateValues stateBuffer = new ExecutionStateValues(SystemTime.Position);

			foreach(Channel channel in VixenSystem.Channels) {
				Command channelState = VixenSystem.Channels.UpdateChannelState(channel);
				if(channelState != null) {
					stateBuffer[channel] = channelState;
				}
			}

			if(ValuesChanged != null) {
				ValuesChanged(stateBuffer);
			}
		}


		static public ProgramContext CreateContext(Program program) {
			ProgramContext context = new ProgramContext(program);
			_contexts[context.Id] = context;
			if(ProgramContextCreated != null) {
				ProgramContextCreated(null, new ProgramContextEventArgs(context));
			}

			return context;
		}

		static public ProgramContext CreateContext(ISequence sequence, string contextName = null) {
			Program program = new Program(contextName ?? sequence.Name);
			program.Add(sequence);
			return CreateContext(program);
		}

		static public void ReleaseContext(ProgramContext context) {
			// Double-check locking.
			// Do we have the context?
			// Great.  Lock.
			// Do we still have the context?
			// Okay, now we can release it.
			if(_contexts.ContainsKey(context.Id)) {
				lock(_contexts) {
					if(_contexts.ContainsKey(context.Id)) {
						_contexts.Remove(context.Id);
						if(ProgramContextReleased != null) {
							ProgramContextReleased(null, new ProgramContextEventArgs(context));
						}
						context.Dispose();
					}
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sequence"></param>
		/// <param name="contextName"></param>
		/// <returns>The resulting length of the queue.  0 if it cannot be added.</returns>
		static public int QueueSequence(ISequence sequence, string contextName = null) {
			// Look for an execution context with that name.
			ProgramContext context = _contexts.Values.FirstOrDefault(x => x.Name.Equals(contextName, StringComparison.OrdinalIgnoreCase));
			if(context != null) {
				// Context already exists.  Add sequence to it.
				// Can't just add the sequence to the program because it's executing and the
				// thing executing it has likely cached the state.  Need to go through the
				// appropriate layers.
				return context.Queue(sequence);
			} else {
				// Context does not exist.
				// The context must be created and managed since the user is not doing it.
				context = CreateContext(sequence, contextName);
				// When the program ends, release the context.
				context.ProgramEnded += (sender, e) => {
					ReleaseContext(context);
				};
				context.Play();
				// It is the sequence playing now.
				return 0;
			}
		}

		/// <summary>
		/// Write data for immediate execution.
		/// </summary>
		/// <param name="state"></param>
		static public void Write(IEnumerable<EffectNode> state) {
			// Give the renderer a separate collection instance.
			EffectRenderer renderer = new EffectRenderer(state.ToArray());
			ThreadPool.QueueUserWorkItem((o) => renderer.Start());
		}

		#region EffectRenderer
		class EffectRenderer {
			private TimeSpan _timeStarted;
			private Stack<EffectNode> _effects;
			//*** to be user data, the offset to add to make live data more live
			private TimeSpan _syncDelta = TimeSpan.Zero;

			public EffectRenderer(EffectNode[] state) {
				_timeStarted = Execution.SystemTime.Position;
				_effects = new Stack<EffectNode>(state);
			}

			public void Start() {
				try {
					// Keep going while there is data to render and the system is running.
					while (_effects.Count > 0 && SystemTime.IsRunning) {
						EffectNode effectNode = _effects.Pop();

						if (!effectNode.IsEmpty && effectNode.Effect.TargetNodes.Length > 0) {
							// Get the channels that are to be affected by this effect.
							// If they are targetting multiple nodes, the resulting channels
							// will be treated as a single collection of channels.  There will be
							// no differentiation between channels of different trees.
							Dictionary<Guid, Channel> targetChannels = effectNode.Effect.TargetNodes.SelectMany(x => x).ToDictionary(x => x.Id);

							// Render the effect for the whole span of the command's time.
							ChannelData channelData = effectNode.RenderEffectData(TimeSpan.Zero, effectNode.TimeSpan);

							if (channelData != null) {
								// Get it into the channels.
								foreach (Guid channelId in channelData.Keys) {
									Channel targetChannel = targetChannels[channelId];

									Monitor.Enter(targetChannel);

									TimeSpan systemTimeDelta = _timeStarted + _syncDelta;

									// Offset the data's time frame by the command's time offset.
									foreach (CommandNode data in channelData[channelId]) {
										// The data time needs to be translated from effect-local to
										// system-local.
										// Adding the command's start time makes it context-local.
										// Adding the system time makes it system-local.
										// Creating a new instance instead of changing the time members because
										// changing them affects the data that the effect has created, affecting
										// the relative timing of the data.  The data that the effect generates
										// should always be relative to the start of the effect.
										CommandNode targetChannelData = new CommandNode(data.Command, data.StartTime + systemTimeDelta, data.TimeSpan);
										targetChannel.AddData(targetChannelData);
									}

									Monitor.Exit(targetChannel);
								}
							}
						}
					}
				}
				catch (Exception ex) {
					VixenSystem.Logging.Error("EffectRender: Exception while trying to render an effect. (Has the effect " +
						"generated data for a channel or node it doesn't target?)", ex);
				}
			}
		}
		#endregion
	}
}
