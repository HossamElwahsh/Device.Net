using Device.Net;
using Device.Net.Exceptions;
using Device.Net.UWP;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using usbControlRequestType = Windows.Devices.Usb.UsbControlRequestType;
using usbControlTransferType = Windows.Devices.Usb.UsbControlTransferType;
using usbSetupPacket = Windows.Devices.Usb.UsbSetupPacket;
using windowsUsbDevice = Windows.Devices.Usb.UsbDevice;

namespace Usb.Net.UWP
{
    public class UWPUsbInterfaceManager : UWPDeviceBase<windowsUsbDevice>, IUsbInterfaceManager
    {
        #region Fields
        private bool disposed;
        private readonly ushort? _WriteBufferSize;
        private readonly ushort? _ReadBufferSize;
        private readonly Func<windowsUsbDevice, SetupPacket, byte[], CancellationToken, Task<TransferResult>> _performControlTransferAsync;
        #endregion

        #region Public Properties
        public UsbInterfaceManager UsbInterfaceHandler { get; }
        #endregion

        #region Public Override Properties
        public override ushort WriteBufferSize => _WriteBufferSize ?? WriteUsbInterface.WriteBufferSize;
        public override ushort ReadBufferSize => _ReadBufferSize ?? ReadUsbInterface.WriteBufferSize;

        public IUsbInterface ReadUsbInterface
        {
            get => UsbInterfaceHandler.ReadUsbInterface;
            set => UsbInterfaceHandler.ReadUsbInterface = value;
        }

        public IUsbInterface WriteUsbInterface
        {
            get => UsbInterfaceHandler.WriteUsbInterface;
            set => UsbInterfaceHandler.WriteUsbInterface = value;
        }

        public IList<IUsbInterface> UsbInterfaces => UsbInterfaceHandler.UsbInterfaces;
        #endregion

        #region Constructors
        public UWPUsbInterfaceManager(
            ConnectedDeviceDefinition connectedDeviceDefinition,
            Func<windowsUsbDevice, SetupPacket, byte[], CancellationToken, Task<TransferResult>> performControlTransferAsync,
            IDataReceiver dataReceiver,
            ILoggerFactory loggerFactory = null,
            ushort? readBufferSize = null,
            ushort? writeBufferSize = null) : base(connectedDeviceDefinition?.DeviceId, dataReceiver, loggerFactory)
        {
            ConnectedDeviceDefinition = connectedDeviceDefinition ?? throw new ArgumentNullException(nameof(connectedDeviceDefinition));
            UsbInterfaceHandler = new UsbInterfaceManager(loggerFactory);
            _WriteBufferSize = writeBufferSize;
            _ReadBufferSize = readBufferSize;
            _performControlTransferAsync = performControlTransferAsync;
        }
        #endregion

        #region Public Methods
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (disposed) throw new ValidationException(Messages.DeviceDisposedErrorMessage);

            await GetDeviceAsync(DeviceId, cancellationToken);

            if (ConnectedDevice != null)
            {
                if (ConnectedDevice.Configuration.UsbInterfaces == null || ConnectedDevice.Configuration.UsbInterfaces.Count == 0)
                {
                    ConnectedDevice.Dispose();
                    throw new DeviceException(Messages.ErrorMessageNoInterfaceFound);
                }

                var interfaceIndex = 0;
                foreach (var usbInterface in ConnectedDevice.Configuration.UsbInterfaces)
                {
                    var uwpUsbInterface = new UWPUsbInterface(
                        usbInterface,
                        _performControlTransferAsync != null ?
                        new PerformControlTransferAsync((sp, data, c) => _performControlTransferAsync(ConnectedDevice, sp, data, c)) :
                        new PerformControlTransferAsync(PerformControlTransferAsync),
                        DataReceiver,
                        Logger,
                        _ReadBufferSize,
                        _WriteBufferSize);

                    UsbInterfaceHandler.UsbInterfaces.Add(uwpUsbInterface);
                    interfaceIndex++;
                }
            }
            else
            {
                var deviceException = new DeviceException(Messages.GetErrorMessageCantConnect(DeviceId));
                Logger.LogError(deviceException, "Error getting device");
                throw deviceException;
            }

            UsbInterfaceHandler.RegisterDefaultInterfaces();
        }

        public override void Dispose()
        {
            if (disposed) return;
            disposed = true;

            UsbInterfaceHandler?.Dispose();
            base.Dispose();
        }

        public Task WriteAsync(byte[] data) => WriteUsbInterface.WriteAsync(data);

        public Task<ConnectedDeviceDefinition> GetConnectedDeviceDefinitionAsync(CancellationToken cancellationToken = default) => Task.FromResult(ConnectedDeviceDefinition);

        public override Task<TransferResult> ReadAsync(CancellationToken cancellationToken = default) => ReadUsbInterface.ReadAsync(ReadBufferSize, cancellationToken);
        #endregion

        #region Private Methods
        private async Task<TransferResult> PerformControlTransferAsync(SetupPacket setupPacket, byte[] buffer, CancellationToken cancellationToken = default)
        {
            if (setupPacket.RequestType.Direction == RequestDirection.In)
            {
                var uwpSetupPacket = new usbSetupPacket
                {
                    Index = setupPacket.Index,
                    Length = setupPacket.Length,
                    Request = setupPacket.Request,
                    RequestType = new usbControlRequestType
                    {
                        ControlTransferType = setupPacket.RequestType.Type switch
                        {
                            RequestType.Standard => usbControlTransferType.Standard,
                            RequestType.Class => usbControlTransferType.Class,
                            RequestType.Vendor => usbControlTransferType.Vendor,
                            _ => throw new NotImplementedException()
                        }
                    },
                    Value = setupPacket.Value
                };

                var readBuffer = await ConnectedDevice.SendControlInTransferAsync(uwpSetupPacket);

                return new TransferResult(readBuffer.ToArray(), readBuffer.Length);
            }

            return default;
        }
        #endregion

        #region Protected Methods
        protected override IAsyncOperation<windowsUsbDevice> FromIdAsync(string id) => windowsUsbDevice.FromIdAsync(id);
        #endregion
    }
}
