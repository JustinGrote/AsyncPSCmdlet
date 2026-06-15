# Async Friendly PS Cmdlet

## Module API

The module provides an abstract `AsyncPSCmdlet` base class for writing PowerShell cmdlets that use asynchronous `Task`-based lifecycle methods. It also provides a new improved, backward compatible API for writing binary cmdlets.

### Base class

`AsyncPSCmdlet : PSCmdlet`

#### Lifecycle hooks

Implement these Async methods

- `Begin()` — replaces `BeginProcessing()`
- `Process()` — replaces `ProcessRecord()`
- `End()` — replaces `EndProcessing()`

### Output and write helpers

The base class exposes internal helpers for writing to the PowerShell pipeline in a safe async-friendly way:

- `AddOutput(object item, bool raw = false)` — buffers an item for later emission to the pipeline.
- `Debug(string message, bool raw = false)`
- `Verbose(string message, bool raw = false)`
- `Warning(string message, bool raw = false)`
- `Info(string message, string[]? tags = null, bool raw = false)`
- `Console(string message, bool raw = false)`
- `Error(...)` — overloads for exception-based and message-based errors.

### Error handling

`Error(...)` supports:

- `Exception exception`
- `string message`
- optional `recommendedAction`
- optional `errorId`
- optional `targetObject`
- optional `errorDetailsMessage`
- optional `category`
- `terminating` to throw a terminating error when `true`

### Usage pattern

```csharp
public sealed class ExampleCmdlet : AsyncPSCmdlet
{
    protected override async Task Process()
    {
        await Task.Delay(100);
        AddOutput("done");
    }
}
```

### Notes

- Use the helper methods above instead of calling `WriteObject`/`WriteError` directly from async work.
- The async pipeline buffers output and emits it back to PowerShell in the cmdlet execution thread.


