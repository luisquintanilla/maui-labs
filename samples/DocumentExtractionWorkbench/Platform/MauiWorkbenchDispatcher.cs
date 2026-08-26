using DocumentExtractionWorkbench.Core;
using Microsoft.Maui.Dispatching;

namespace DocumentExtractionWorkbench;

public sealed class MauiWorkbenchDispatcher : IWorkbenchDispatcher
{
	private readonly IDispatcher _dispatcher;

	public MauiWorkbenchDispatcher(IDispatcher dispatcher)
	{
		_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
	}

	public Task InvokeAsync(
		Action action,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(action);
		cancellationToken.ThrowIfCancellationRequested();

		if (!_dispatcher.IsDispatchRequired)
		{
			action();
			return Task.CompletedTask;
		}

		var completion = new TaskCompletionSource<bool>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		var accepted = _dispatcher.Dispatch(() =>
		{
			if (cancellationToken.IsCancellationRequested)
			{
				completion.TrySetCanceled(cancellationToken);
				return;
			}

			try
			{
				action();
				completion.TrySetResult(true);
			}
			catch (Exception exception)
			{
				completion.TrySetException(exception);
			}
		});

		if (!accepted)
		{
			throw new InvalidOperationException(
				"The MAUI dispatcher rejected a workbench state update.");
		}

		return completion.Task;
	}
}
