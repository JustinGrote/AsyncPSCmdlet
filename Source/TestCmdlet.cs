namespace Test;

using System.Management.Automation;
using System.Net.Http.Json;

record Post(int UserId, int Id, string Title, string Body);

[Cmdlet(VerbsDiagnostic.Test, "AsyncPSCmdlet", SupportsShouldProcess = true)]
public class TestCmdlet : AsyncPSCmdlet
{
	readonly HttpClient client = new();
	protected override async Task Begin()
	{
		// Warning("Beginning TestCmdlet execution...");
	}
	protected override async Task Process()
	{
		// if (!await ShouldProcessCustomAsync("Fetch 30 Items")) return;

		// Fetch 30 posts simultaneously from https://jsonplaceholder.typicode.com/posts/1 to /posts/30. Use async HTTP requests and write each post to the output as soon as it's received.
		var tasks = Enumerable.Range(1, 30).Select(async i =>
		{
			try
			{
				Debug($"Fetching post {i}...");
				var post = await client.GetFromJsonAsync<Post>($"https://jsonplaceholder.typicode.com/posts/{i}", PipelineStopToken);
				Debug($"Received post {i}: {post?.Title}");
				AddOutput(post!);
			}
			catch (Exception ex)
			{
				AddOutput(new ErrorRecord(ex, "FetchPostError", ErrorCategory.NotSpecified, i));
			}
		});

		await Task.WhenAll(tasks);
	}
}