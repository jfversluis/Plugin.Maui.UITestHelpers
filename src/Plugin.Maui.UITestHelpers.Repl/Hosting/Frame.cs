using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Plugin.Maui.UITestHelpers.Repl.Engine;

namespace Plugin.Maui.UITestHelpers.Repl.Hosting
{
	internal static class FrameTypes
	{
		public const string Hello = "hello";
		public const string Output = "out";
		public const string Exec = "exec";
		public const string Done = "done";
		public const string Cancel = "cancel";
		public const string Complete = "complete";
		public const string Completions = "completions";
		public const string Bye = "bye";
	}

	/// <summary>
	/// A message between the test process (which owns the app and evaluates code) and the REPL window (which
	/// only does the typing and the showing). Sent as one JSON object per line.
	/// </summary>
	internal sealed class Frame
	{
		[JsonPropertyName("t")]
		public string Type { get; set; } = string.Empty;

		public int Id { get; set; }

		public string? Text { get; set; }

		public int Caret { get; set; }

		public string? Token { get; set; }

		public int Width { get; set; }

		public int Height { get; set; }

		public int Colors { get; set; }

		public bool Unicode { get; set; }

		public bool Exit { get; set; }

		public List<CompletionDto>? Items { get; set; }
	}

	internal sealed record CompletionDto(string Name, string Kind, string? Detail)
	{
		public static CompletionDto From(ReplCompletion completion) => new(completion.Name, completion.Kind, completion.Detail);
	}

	/// <summary>
	/// Reads and writes <see cref="Frame"/>s on a (named pipe) stream.
	/// </summary>
	internal sealed class FrameChannel : IDisposable
	{
		static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		};

		readonly StreamReader _reader;
		readonly StreamWriter _writer;
		readonly SemaphoreSlim _writeLock = new(1, 1);

		public FrameChannel(Stream stream)
		{
			var utf8 = new UTF8Encoding(false);
			_reader = new StreamReader(stream, utf8, false, 16 * 1024, leaveOpen: true);
			_writer = new StreamWriter(stream, utf8, 16 * 1024, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
		}

		public async Task<Frame?> ReadAsync(CancellationToken cancellationToken = default)
		{
			try
			{
				var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
				return line is null ? null : JsonSerializer.Deserialize<Frame>(line, JsonOptions);
			}
			catch (JsonException)
			{
				// A garbled line, ignore it.
				return new Frame();
			}
			catch (IOException)
			{
				return null;
			}
			catch (ObjectDisposedException)
			{
				return null;
			}
		}

		public async Task<bool> WriteAsync(Frame frame, CancellationToken cancellationToken = default)
		{
			var json = JsonSerializer.Serialize(frame, JsonOptions);

			try
			{
				await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (ObjectDisposedException)
			{
				return false;
			}

			try
			{
				await _writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
				return true;
			}
			catch (IOException)
			{
				return false;
			}
			catch (ObjectDisposedException)
			{
				return false;
			}
			finally
			{
				_writeLock.Release();
			}
		}

		public void Dispose()
		{
			_writeLock.Dispose();
		}
	}
}
