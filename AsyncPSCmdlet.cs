namespace System.Management.Automation;

using System.Collections;
using System.Collections.Concurrent;


public abstract class AsyncPSCmdlet : PSCmdlet
{
	protected string name => MyInvocation.MyCommand.Name;

	// This is our API that derived classes can implement
	protected virtual Task Begin() { return Task.CompletedTask; }
	protected virtual Task Process() { return Task.CompletedTask; }
	protected virtual Task End() { return Task.CompletedTask; }

	// Our "better" API for writing output to PowerShell

	internal void AddOutput(object item, bool raw = false)
	{
		if (_output == null)
		{
			throw new InvalidOperationException("WriteObject cannot be called before the pipeline is initialized.");
		}
		_output.Add((item, raw));
	}

	internal void Debug(string message, bool raw = false) => WriteDebug(raw ? message : $"{name}: {message}");
	internal void Verbose(string message, bool raw = false) => WriteVerbose(raw ? message : $"{name}: {message}");
	internal void Warning(string message, bool raw = false) => WriteWarning(raw ? message : $"{name}: {message}");
	internal void Info(string message, string[]? tags = null, bool raw = false)
		=> WriteInformation(raw ? message : $"{name}: {message}", tags);
	internal void Console(string message, bool raw = false)
		=> WriteInformation(raw ? message : $"{name}: {message}", ["PSHOST"]);

	internal void Error(
			Exception exception,
			string? recommendedAction = null,
			string errorId = "PSCmdletError",
			object? targetObject = null,
			// Usually comes from the exception message, specify this to override
			string? errorDetailsMessage = null,
			// This is often autodetermined
			ErrorCategory? category = null,
			bool terminating = false)
	{
		ErrorRecord error = new(
				exception,
				errorId,
				category ?? exception switch
				{
					ArgumentException => ErrorCategory.InvalidArgument,
					FileNotFoundException => ErrorCategory.ObjectNotFound,
					InvalidOperationException => ErrorCategory.InvalidOperation,
					NotSupportedException => ErrorCategory.NotSpecified,
					UnauthorizedAccessException => ErrorCategory.SecurityError,
					PathTooLongException => ErrorCategory.InvalidArgument,
					DirectoryNotFoundException => ErrorCategory.ObjectNotFound,
					IOException => ErrorCategory.WriteError,
					NullReferenceException => ErrorCategory.InvalidData,
					FormatException => ErrorCategory.InvalidData,
					TimeoutException => ErrorCategory.OperationTimeout,
					OutOfMemoryException => ErrorCategory.ResourceUnavailable,
					NotImplementedException => ErrorCategory.NotImplemented,
					OperationCanceledException => ErrorCategory.OperationStopped,
					AccessViolationException => ErrorCategory.SecurityError,
					InvalidCastException => ErrorCategory.InvalidType,
					_ => ErrorCategory.NotSpecified
				},
				targetObject
		)
		{
			ErrorDetails = new ErrorDetails(errorDetailsMessage ?? exception.Message)
			{
				RecommendedAction = recommendedAction
			}
		};

		if (terminating)
		{
			ThrowTerminatingError(error);
		}
		else
		{
			WriteError(error);
		}
	}

	internal void Error(
			string message,
			string? recommendedAction = null,
			string errorId = "PSCmdletError",
			object? targetObject = null,
			ErrorCategory category = ErrorCategory.NotSpecified,
			bool terminating = false
		) => Error(
			new CmdletInvocationException(message), recommendedAction, errorId, targetObject, null, category, terminating
		);


	/// <summary>
	/// Buffers output from asynchronous pipeline steps on separate threads before writing it to the pipeline.
	/// </summary>
	/// <remarks>
	/// A fresh collection is created for each pipeline execution, but it must remain accessible to the overridden <c>WriteObject</c> method.
	/// </remarks>
	private BlockingCollection<(object Item, bool Raw)>? _output;

	// Override the pscmdlet entrypoints to execute our async methods
	protected override void BeginProcessing() => ExecuteAsyncPipelineStep(Begin);
	protected override void ProcessRecord() => ExecuteAsyncPipelineStep(Process);
	protected override void EndProcessing() => ExecuteAsyncPipelineStep(End);

	/// <summary>
	/// Executes an asynchronous cmdlet step and routes its output and errors through the PowerShell pipeline.
	/// </summary>
	private void ExecuteAsyncPipelineStep(Func<Task> cmdletMethod)
	{
		_output = [];
		Task.Run(async () =>
		{
			try
			{
				await cmdletMethod();
			}
			catch (Exception ex)
			{
				// Handle exceptions by writing them to the error stream
				AddOutput(new ErrorRecord(ex, "AsyncCmdletError", ErrorCategory.NotSpecified, null));
			}
			finally
			{
				_output.CompleteAdding();
			}
		});

		foreach (var item in _output.GetConsumingEnumerable(PipelineStopToken))
		{
			ProcessOutput(item);
		}
	}

	private void ProcessOutput((object Item, bool Raw) inputObject)
	{
		var (item, raw) = inputObject;

		if (raw)
		{
			base.WriteObject(item);
			return;
		}

		switch (item)
		{
			case ErrorRecord errorRecord:
				base.WriteError(errorRecord);
				break;
			case InformationRecord informationRecord:
				base.WriteInformation(informationRecord);
				break;
			case WarningRecord warningRecord:
				base.WriteWarning(warningRecord.Message);
				break;
			case VerboseRecord verboseRecord:
				base.WriteVerbose(verboseRecord.Message);
				break;
			case DebugRecord debugRecord:
				base.WriteDebug(debugRecord.Message);
				break;
			case ProgressRecord progressRecord:
				base.WriteProgress(progressRecord);
				break;
			default:
				base.WriteObject(item);
				break;
		}
	}


	// These are compatibility shims for the other Write* methods. You should use the other methods above instead of these, but this allows existing code to work without modification.
	// Override WriteObject to write to our blocking collection instead of the pipeline directly, to avoid marshalling issues
	protected new void WriteObject(object outputObject, bool enumerateCollection = false)
	{
		if (enumerateCollection && outputObject is IEnumerable enumerable)
		{
			foreach (var item in enumerable)
			{
				AddOutput(item);
				return;
			}
		}

		AddOutput(outputObject);
	}

	protected new void WriteWarning(string message) => WriteObject(new WarningRecord(message));
	protected new void WriteVerbose(string message) => WriteObject(new VerboseRecord(message));
	protected new void WriteDebug(string message) => WriteObject(new DebugRecord(message));
	protected new void WriteError(ErrorRecord errorRecord) => WriteObject(errorRecord);
	protected new void WriteInformation(InformationRecord informationRecord) => WriteObject(informationRecord);
	protected new void WriteProgress(ProgressRecord progressRecord) => WriteObject(progressRecord);
}