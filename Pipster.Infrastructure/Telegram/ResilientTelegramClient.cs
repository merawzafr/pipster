using Microsoft.Extensions.Logging;
using Pipster.Shared.Contracts.Telegram;
using Polly;
using Polly.CircuitBreaker;
using TdLib;

namespace Pipster.Infrastructure.Telegram;

/// <summary>
/// Resilient Telegram client wrapper for a single tenant.
/// Handles connection lifecycle, message observation, and error recovery.
/// </summary>
public class ResilientTelegramClient : ITelegramClient
{
    private readonly TdClient _client;
    private readonly TelegramCredentials _credentials;
    private readonly string _sessionPath;
    private readonly TelegramClientOptions _options;
    private readonly ILogger<ResilientTelegramClient> _logger;
    private readonly HashSet<long> _observedChannels = new();
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private readonly AsyncCircuitBreakerPolicy _circuitBreaker;

    private bool _isConnected;
    private bool _disposed;
    private int _reconnectAttempts;

    public string TenantId { get; }
    public bool IsConnected => _isConnected && _circuitBreaker.CircuitState != CircuitState.Open;

    public ResilientTelegramClient(
        string tenantId,
        TelegramCredentials credentials,
        string sessionPath,
        TelegramClientOptions options,
        ILogger<ResilientTelegramClient> logger)
    {
        TenantId = tenantId;
        _credentials = credentials;
        _sessionPath = sessionPath;
        _options = options;
        _logger = logger;

        // Circuit breaker: open after 5 failures in 30s, stay open for 60s
        _circuitBreaker = Policy
            .Handle<TdException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(60),
                onBreak: (ex, duration) =>
                {
                    _logger.LogError(ex, "Circuit breaker opened for tenant {TenantId} for {Duration}s",
                        TenantId, duration.TotalSeconds);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset for tenant {TenantId}", TenantId);
                });

        _client = new TdClient();
        ConfigureClient();
    }

    private void ConfigureClient()
    {
        _client.UpdateReceived += async (_, update) =>
        {
            try
            {
                await HandleUpdateAsync(update);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling update for tenant {TenantId}", TenantId);
            }
        };
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ResilientTelegramClient));

        try
        {
            await _circuitBreaker.ExecuteAsync(async () =>
            {
                // Set TDLib parameters
                await _client.ExecuteAsync(new TdApi.SetTdlibParameters
                {
                    ApiId = _credentials.ApiId,
                    ApiHash = _credentials.ApiHash,
                    DatabaseDirectory = Path.Combine(_sessionPath, "db"),
                    FilesDirectory = Path.Combine(_sessionPath, "files"),
                    UseFileDatabase = true,
                    UseChatInfoDatabase = true,
                    UseMessageDatabase = false, // Don't store message history
                    UseSecretChats = false,
                    SystemLanguageCode = "en",
                    DeviceModel = "Pipster",
                    ApplicationVersion = "1.0.0"
                });

                // Wait for authorization state
                var authState = await WaitForAuthorizationStateAsync(ct);

                if (authState is TdApi.AuthorizationState.AuthorizationStateReady)
                {
                    _isConnected = true;
                    _reconnectAttempts = 0;
                    _logger.LogInformation("Connected to Telegram for tenant {TenantId}", TenantId);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect for tenant {TenantId}", TenantId);
            throw;
        }
    }

    public async Task<bool> AuthenticateAsync(string phoneNumber, CancellationToken ct)
    {
        try
        {
            await _client.ExecuteAsync(new TdApi.SetAuthenticationPhoneNumber
            {
                PhoneNumber = phoneNumber
            });
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication failed for tenant {TenantId}", TenantId);
            return false;
        }
    }

    public async Task<bool> VerifyCodeAsync(string code, CancellationToken ct)
    {
        try
        {
            await _client.ExecuteAsync(new TdApi.CheckAuthenticationCode
            {
                Code = code
            });
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Code verification failed for tenant {TenantId}", TenantId);
            return false;
        }
    }

    public async Task AddChannelAsync(long channelId, CancellationToken ct)
    {
        await _channelLock.WaitAsync(ct);
        try
        {
            if (_observedChannels.Add(channelId))
            {
                // Join channel if not already a member
                await _circuitBreaker.ExecuteAsync(async () =>
                {
                    await _client.ExecuteAsync(new TdApi.JoinChat { ChatId = channelId });
                });

                _logger.LogInformation("Added channel {ChannelId} for tenant {TenantId}",
                    channelId, TenantId);
            }
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public async Task RemoveChannelAsync(long channelId, CancellationToken ct)
    {
        await _channelLock.WaitAsync(ct);
        try
        {
            if (_observedChannels.Remove(channelId))
            {
                _logger.LogInformation("Removed channel {ChannelId} for tenant {TenantId}",
                    channelId, TenantId);
            }
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public async Task<IReadOnlyList<long>> GetObservedChannelsAsync(CancellationToken ct)
    {
        await _channelLock.WaitAsync(ct);
        try
        {
            return _observedChannels.ToList();
        }
        finally
        {
            _channelLock.Release();
        }
    }

    private async Task HandleUpdateAsync(TdApi.Update update)
    {
        switch (update)
        {
            case TdApi.Update.UpdateNewMessage newMessage:
                await HandleNewMessageAsync(newMessage);
                break;

            case TdApi.Update.UpdateAuthorizationState authState:
                await HandleAuthorizationStateAsync(authState.AuthorizationState);
                break;

            case TdApi.Update.UpdateConnectionState connectionState:
                await HandleConnectionStateAsync(connectionState.State);
                break;
        }
    }

    private async Task HandleNewMessageAsync(TdApi.Update.UpdateNewMessage update)
    {
        var chatId = update.Message.ChatId;

        // Only process messages from observed channels
        if (!_observedChannels.Contains(chatId))
            return;

        // Extract message content
        var content = update.Message.Content switch
        {
            TdApi.MessageContent.MessageText text => text.Text.Text,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(content))
            return;

        // TODO: Publish to message bus (will implement in next step)
        _logger.LogDebug("Received message in channel {ChannelId} for tenant {TenantId}: {Content}",
            chatId, TenantId, content);
    }

    private Task HandleAuthorizationStateAsync(TdApi.AuthorizationState state)
    {
        switch (state)
        {
            case TdApi.AuthorizationState.AuthorizationStateReady:
                _isConnected = true;
                _logger.LogInformation("Authorization ready for tenant {TenantId}", TenantId);
                break;

            case TdApi.AuthorizationState.AuthorizationStateClosed:
                _isConnected = false;
                _logger.LogWarning("Authorization closed for tenant {TenantId}", TenantId);
                break;
        }

        return Task.CompletedTask;
    }

    private Task HandleConnectionStateAsync(TdApi.ConnectionState state)
    {
        var wasConnected = _isConnected;

        _isConnected = state is TdApi.ConnectionState.ConnectionStateReady;

        if (wasConnected && !_isConnected)
        {
            _logger.LogWarning("Connection lost for tenant {TenantId}, state: {State}",
                TenantId, state.GetType().Name);
        }
        else if (!wasConnected && _isConnected)
        {
            _logger.LogInformation("Connection restored for tenant {TenantId}", TenantId);
        }

        return Task.CompletedTask;
    }

    private async Task<TdApi.AuthorizationState> WaitForAuthorizationStateAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<TdApi.AuthorizationState>();

        void handler(object? sender, TdApi.Update update)
        {
            if (update is TdApi.Update.UpdateAuthorizationState authState)
            {
                if (authState.AuthorizationState is TdApi.AuthorizationState.AuthorizationStateReady)
                {
                    tcs.TrySetResult(authState.AuthorizationState);
                }
            }
        }

        _client.UpdateReceived += handler;

        using var _ = ct.Register(() => tcs.TrySetCanceled());

        try
        {
            return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        }
        finally
        {
            _client.UpdateReceived -= handler;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _isConnected = false;

        try
        {
            await _client.ExecuteAsync(new TdApi.Close());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing client for tenant {TenantId}", TenantId);
        }

        _client?.Dispose();
        _channelLock.Dispose();
    }
}