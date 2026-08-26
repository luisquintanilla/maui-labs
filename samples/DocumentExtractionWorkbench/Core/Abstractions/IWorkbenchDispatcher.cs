namespace DocumentExtractionWorkbench.Core;

public interface IWorkbenchDispatcher
{
	Task InvokeAsync(Action action, CancellationToken cancellationToken = default);
}
