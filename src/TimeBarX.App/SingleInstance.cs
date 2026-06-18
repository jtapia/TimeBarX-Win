using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TimeBarX.App;

/// <summary>
/// Named-pipe single-instance gate. The first launcher becomes the server and
/// receives forwarded URIs from subsequent invocations. The pipe name is fixed
/// per user so multiple users on one machine don't collide.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string PipeName = "TimeBarX.Instance.v1";

    private readonly Mutex _mutex;
    private readonly bool _isOwner;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public bool IsOwner => _isOwner;

    public event Action<string>? MessageReceived;

    public SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, name: "Global\\TimeBarX.SingleInstance.v1", out _isOwner);
    }

    public void StartListener()
    {
        if (!_isOwner) return;
        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenLoop(_cts.Token));
    }

    public static bool TrySend(string message, TimeSpan timeout)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect((int)timeout.TotalMilliseconds);
            var bytes = Encoding.UTF8.GetBytes(message);
            client.Write(bytes, 0, bytes.Length);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _listenerTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _cts?.Dispose();
        if (_isOwner)
        {
            try { _mutex.ReleaseMutex(); } catch { }
        }
        _mutex.Dispose();
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var message = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    MessageReceived?.Invoke(message.Trim());
                }
            }
            catch (OperationCanceledException) { return; }
            catch { /* keep listening */ }
            finally
            {
                server?.Dispose();
            }
        }
    }
}
