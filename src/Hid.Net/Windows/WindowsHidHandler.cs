﻿using Device.Net;
using Device.Net.Exceptions;
using Device.Net.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hid.Net.Windows
{
    public class WindowsHidHandler : IHidDeviceHandler
    {
        public string DeviceId { get; }
        public bool? IsReadOnly { get; private set; }
        public ConnectedDeviceDefinition ConnectedDeviceDefinition { get; private set; }
        public ushort? WriteBufferSize { get; private set; }
        public ushort? ReadBufferSize { get; private set; }


        private bool disposed;
        private Stream _ReadFileStream;
        private Stream _WriteFileStream;
        private SafeFileHandle _ReadSafeFileHandle;
        private SafeFileHandle _WriteSafeFileHandle;
        private readonly ILogger Logger;
        private readonly IHidApiService HidService;
        public WindowsHidHandler(
            string deviceId,
            ushort? writeBufferSize = null,
            ushort? readBufferSize = null,
            IHidApiService hidApiService = null,
            ILoggerFactory loggerFactory = null)
        {
            DeviceId = deviceId;
            Logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<WindowsHidHandler>();
            HidService = hidApiService ?? new WindowsHidApiService(loggerFactory);

            WriteBufferSize = writeBufferSize;
            ReadBufferSize = readBufferSize;
        }

        public void Initialize()
        {
            using var logScope = Logger.BeginScope("DeviceId: {deviceId} Call: {call}", DeviceId, nameof(Initialize));

            if (string.IsNullOrEmpty(DeviceId))
            {
                throw new ValidationException(
                    $"{nameof(DeviceId)} must be specified before {nameof(Initialize)} can be called.");
            }

            _ReadSafeFileHandle = HidService.CreateReadConnection(DeviceId, FileAccessRights.GenericRead);
            _WriteSafeFileHandle = HidService.CreateWriteConnection(DeviceId);

            if (_ReadSafeFileHandle.IsInvalid)
            {
                throw new ApiException(Messages.ErrorMessageCantOpenRead);
            }

            IsReadOnly = _WriteSafeFileHandle.IsInvalid;

            if (IsReadOnly.Value)
            {
                Logger.LogWarning(Messages.WarningMessageOpeningInReadonlyMode, DeviceId);
            }

            ConnectedDeviceDefinition = HidService.GetDeviceDefinition(DeviceId, _ReadSafeFileHandle);

            ReadBufferSize ??= (ushort?)ConnectedDeviceDefinition.ReadBufferSize;
            WriteBufferSize ??= (ushort?)ConnectedDeviceDefinition.WriteBufferSize;

            if (!ReadBufferSize.HasValue)
            {
                throw new ValidationException(
                    $"ReadBufferSize must be specified. HidD_GetAttributes may have failed or returned an InputReportByteLength of 0. Please specify this argument in the constructor");
            }

            _ReadFileStream = HidService.OpenRead(_ReadSafeFileHandle, ReadBufferSize.Value);

            if (_ReadFileStream.CanRead)
            {
                Logger.LogInformation(Messages.SuccessMessageReadFileStreamOpened);
            }
            else
            {
                Logger.LogWarning(Messages.WarningMessageReadFileStreamCantRead);
            }

            if (IsReadOnly.Value) return;

            if (!WriteBufferSize.HasValue)
            {
                throw new ValidationException(
                    $"WriteBufferSize must be specified. HidD_GetAttributes may have failed or returned an OutputReportByteLength of 0. Please specify this argument in the constructor");
            }

            //Don't open if this is a read only connection
            _WriteFileStream = HidService.OpenWrite(_WriteSafeFileHandle, WriteBufferSize.Value);

            if (_WriteFileStream.CanWrite)
            {
                Logger.LogInformation(Messages.SuccessMessageWriteFileStreamOpened);
            }
            else
            {
                Logger.LogWarning(Messages.WarningMessageWriteFileStreamCantWrite);
            }
        }

        public async Task<TransferResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (_ReadFileStream == null)
            {
                throw new NotInitializedException(Messages.ErrorMessageNotInitialized);
            }

            var bytes = new byte[ReadBufferSize.Value];

            var bytesRead = (uint)await _ReadFileStream.ReadAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);

            return new TransferResult(bytes, bytesRead);
        }

        public async Task<uint> WriteAsync(byte[] bytes, CancellationToken cancellationToken = default)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            if (_WriteFileStream == null)
            {
                throw new NotInitializedException("The device has not been initialized");
            }

            if (_WriteFileStream.CanWrite)
            {
                await _WriteFileStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
                return (uint)bytes.Length;
            }
            else
            {
                throw new IOException("The file stream cannot be written to");
            }
        }

        public void Dispose()
        {
            if (disposed) return;

            disposed = true;

            _ReadFileStream?.Dispose();
            _WriteFileStream?.Dispose();

            _ReadFileStream = null;
            _WriteFileStream = null;

            if (_ReadSafeFileHandle != null)
            {
                _ReadSafeFileHandle.Dispose();
                _ReadSafeFileHandle = null;
            }

            if (_WriteSafeFileHandle != null)
            {
                _WriteSafeFileHandle.Dispose();
                _WriteSafeFileHandle = null;
            }
        }
    }
}