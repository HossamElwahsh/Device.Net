﻿using Android.Content;
using Android.Hardware.Usb;
using Device.Net;
using System;

namespace Usb.Net.Android
{
    public class UsbDeviceDetachedReceiver : BroadcastReceiver
    {
        #region Fields
        private readonly AndroidUsbDevice _AndroidHidDevice;
        #endregion

        #region Constructor
        public UsbDeviceDetachedReceiver(AndroidUsbDevice androidHidDevice)
        {
            _AndroidHidDevice = androidHidDevice;
        }
        #endregion

        #region Overrides
        public override async void OnReceive(Context context, Intent intent)
        {

            throw new NotImplementedException();
            //var device = intent.GetParcelableExtra(UsbManager.ExtraDevice) as UsbDevice;

            //if (_AndroidHidDevice == null || device == null || device.VendorId != _AndroidHidDevice.DeviceDefinition.VendorId || device.ProductId != _AndroidHidDevice.DeviceDefinition.ProductId) return;

            //await _AndroidHidDevice.UsbDeviceDetached();
            //Logger.Log("Device detached", null, AndroidUsbDevice.LogSection);
        }
        #endregion
    }
}