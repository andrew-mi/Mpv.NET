﻿using Mpv.NET.API.Interop;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mpv.NET.API
{
	public class MpvEventLoop : IMpvEventLoop, IDisposable
	{
		public bool IsRunning { get; private set; }

		public Action<MpvEvent> Callback { get; set; }

		public IntPtr MpvHandle
		{
			get => mpvHandle;
			private set
			{
				if (value == IntPtr.Zero)
					throw new ArgumentException("Mpv handle is invalid.");

				mpvHandle = value;
			}
		}

		public IMpvFunctions Functions
		{
			get => functions;
			set
			{
				Guard.AgainstNull(value);

				functions = value;
			}
		}

		private IntPtr mpvHandle;
		private IMpvFunctions functions;

		private Task eventLoopTask;

		private bool isStopping = false;

		private bool disposed = false;

		public MpvEventLoop(Action<MpvEvent> callback, IntPtr mpvHandle, IMpvFunctions functions)
		{
			Callback = callback;
			MpvHandle = mpvHandle;
			Functions = functions;
		}

		public void Start()
		{
			Guard.AgainstDisposed(disposed, nameof(MpvEventLoop));

			DisposeEventLoopTask();

			eventLoopTask = new Task(EventLoopThreadHandler);
			eventLoopTask.Start();

			IsRunning = true;
		}

		public void Stop()
		{
			Guard.AgainstDisposed(disposed, nameof(MpvEventLoop));

			isStopping = true;

			// Wake up WaitEvent in the event loop thread
			// so we can stop it.
			Functions.Wakeup(mpvHandle);

			eventLoopTask.Wait();

			isStopping = false;

			IsRunning = false;
		}

		private void EventLoopThreadHandler()
		{
			while (IsRunning && !isStopping)
			{
				var eventPtr = Functions.WaitEvent(mpvHandle, Timeout.Infinite);
				if (eventPtr == IntPtr.Zero)
					continue;

				var @event = MpvMarshal.PtrToStructure<MpvEvent>(eventPtr);

				if (@event.ID != MpvEventID.None)
					Callback?.Invoke(@event);
			}
		}

		private void DisposeEventLoopTask()
		{
			eventLoopTask?.Dispose();
		}

		public void Dispose()
		{
			Dispose(true);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (!disposed)
				{
					Stop();

					DisposeEventLoopTask();
				}

				disposed = true;
			}
		}
	}
}