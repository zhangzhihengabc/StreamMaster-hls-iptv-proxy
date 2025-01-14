﻿using Microsoft.Extensions.Logging;

using StreamMasterApplication.Common.Interfaces;

using StreamMasterDomain.Common;
using StreamMasterDomain.Enums;
using StreamMasterDomain.Services;

using StreamMasterInfrastructure.Common;

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StreamMasterInfrastructure.VideoStreamManager.Factories;

public class ProxyFactory(ILogger<ProxyFactory> logger, IHttpClientFactory httpClientFactory, ISettingsService settingsService) : IProxyFactory
{
    public async Task<(Stream? stream, int processId, ProxyStreamError? error)> GetProxy(string streamUrl, CancellationToken cancellationToken)
    {
        Setting setting = await settingsService.GetSettingsAsync();

        Stream? stream;
        ProxyStreamError? error;
        int processId;

        if (setting.StreamingProxyType == StreamingProxyTypes.FFMpeg)
        {
            (stream, processId, error) = await GetFFMpegStream(streamUrl);
            LogErrorIfAny(stream, error, streamUrl);
        }
        else
        {
            (stream, processId, error) = await GetProxyStream(streamUrl, cancellationToken);
            LogErrorIfAny(stream, error, streamUrl);
        }

        return (stream, processId, error);
    }

    private void LogErrorIfAny(Stream? stream, ProxyStreamError? error, string streamUrl)
    {
        if (stream == null || error != null)
        {
            logger.LogError("Error getting proxy stream for {StreamUrl}: {ErrorMessage}", streamUrl, error?.Message);
        }
    }

    private async Task<(Stream? stream, int processId, ProxyStreamError? error)> GetFFMpegStream(string streamUrl)
    {
        Setting settings = await settingsService.GetSettingsAsync().ConfigureAwait(false);

        string ffmpegExec = Path.Combine(BuildInfo.AppDataFolder, settings.FFMPegExecutable);

        if (!File.Exists(ffmpegExec) && !File.Exists(ffmpegExec + ".exe"))
        {
            if (!IsFFmpegAvailable())
            {
                ProxyStreamError error = new() { ErrorCode = ProxyStreamErrorCode.FileNotFound, Message = $"FFmpeg executable file not found: {settings.FFMPegExecutable}" };
                return (null, -1, error);
            }
            ffmpegExec = "ffmpeg";
        }

        try
        {
            return await CreateFFMpegStream(ffmpegExec, streamUrl).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            return HandleFFMpegStreamException<IOException>(ProxyStreamErrorCode.IoError, ex);
        }
        catch (Exception ex)
        {
            return HandleFFMpegStreamException<Exception>(ProxyStreamErrorCode.UnknownError, ex);
        }
    }

    private async Task<(Stream? stream, int processId, ProxyStreamError? error)> CreateFFMpegStream(string ffmpegExec, string streamUrl)
    {
        try
        {
            Setting settings = await settingsService.GetSettingsAsync().ConfigureAwait(false);

            string options = string.IsNullOrEmpty(settings.FFMpegOptions) ? BuildInfo.FFMPEGDefaultOptions : settings.FFMpegOptions;

            string formattedArgs = options.Replace("{streamUrl}", $"\"{streamUrl}\"");
            formattedArgs += $" -user_agent \"{settings.StreamingClientUserAgent}\"";

            using Process process = new();
            process.StartInfo.FileName = ffmpegExec;
            process.StartInfo.Arguments = formattedArgs;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;

            bool processStarted = process.Start();
            if (!processStarted)
            {
                // Log and return an error if the process couldn't be started
                ProxyStreamError error = new() { ErrorCode = ProxyStreamErrorCode.ProcessStartFailed, Message = "Failed to start FFmpeg process" };
                logger.LogError("CreateFFMpegStream Error: {ErrorMessage}", error.Message);
                return (null, -1, error);
            }

            // Return the standard output stream of the process
            return (await Task.FromResult(process.StandardOutput.BaseStream).ConfigureAwait(false), process.Id, null);
        }
        catch (Exception ex)
        {
            // Log and return any exceptions that occur
            ProxyStreamError error = new() { ErrorCode = ProxyStreamErrorCode.UnknownError, Message = ex.Message };
            logger.LogError(ex, "CreateFFMpegStream Error: {ErrorMessage}", error.Message);
            return (null, -1, error);
        }
    }


    private (Stream? stream, int processId, ProxyStreamError? error) HandleFFMpegStreamException<T>(ProxyStreamErrorCode errorCode, T exception) where T : Exception
    {
        ProxyStreamError error = new() { ErrorCode = errorCode, Message = exception.Message };
        logger.LogError(exception, "GetFFMpegStream Error: ", error.Message);
        return (null, -1, error);
    }

    private async Task<(Stream? stream, int processId, ProxyStreamError? error)> GetProxyStream(string sourceUrl, CancellationToken cancellationToken)
    {
        try
        {
            Setting settings = await settingsService.GetSettingsAsync().ConfigureAwait(false);
            HttpClient client = CreateHttpClient(settings.StreamingClientUserAgent);
            HttpResponseMessage? response = await client.GetWithRedirectAsync(sourceUrl, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response?.IsSuccessStatusCode != true)
            {
                ProxyStreamError error = new() { ErrorCode = ProxyStreamErrorCode.DownloadError, Message = "Could not retrieve stream url" };
                logger.LogError("GetProxyStream Error: ", error.Message);
                return (null, -1, error);
            }

            string? contentType = response.Content.Headers?.ContentType?.MediaType;

            if ((!string.IsNullOrEmpty(contentType) &&

                    contentType.Equals("application/vnd.apple.mpegurl", StringComparison.OrdinalIgnoreCase)) ||
                    contentType.Equals("audio/mpegurl", StringComparison.OrdinalIgnoreCase) ||
                    contentType.Equals("application/x-mpegURL", StringComparison.OrdinalIgnoreCase)
                )
            {
                logger.LogInformation("Stream URL has HLS content, using FFMpeg for streaming: {StreamUrl}", sourceUrl);
                return await GetFFMpegStream(sourceUrl).ConfigureAwait(false);
            }

            Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            logger.LogInformation("Successfully retrieved stream for: {StreamUrl}", sourceUrl);
            return (stream, -1, null);
        }
        catch (Exception ex)
        {
            ProxyStreamError error = new() { ErrorCode = ProxyStreamErrorCode.DownloadError, Message = ex.Message };
            logger.LogError(ex, "GetProxyStream Error: ", error.Message);
            return (null, -1, error);
        }
    }

    private HttpClient CreateHttpClient(string streamingClientUserAgent)
    {
        HttpClient client = httpClientFactory.CreateClient();

        client.DefaultRequestHeaders.UserAgent.ParseAdd(streamingClientUserAgent);
        return client;
    }

    private static bool IsFFmpegAvailable()
    {
        string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";
        ProcessStartInfo startInfo = new(command, "ffmpeg")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        Process process = new()
        {
            StartInfo = startInfo
        };
        process.Start();
        process.WaitForExit();
        return process.ExitCode == 0;
    }
}
