using System.Collections.Generic;
using System.Linq;
using SharpPcap;
using SharpPcap.LibPcap;

namespace CSArp.Model
{
    public static class NetworkAdapterManager
    {
        public static CaptureDeviceList NetworkAdapters {
            get {
                if (_networkAdapters == null)
                {
                    _networkAdapters = CaptureDeviceList.Instance;
                }

                return _networkAdapters;
            }
        }

        private static CaptureDeviceList _networkAdapters;

        public static IReadOnlyList<LibPcapLiveDevice> WinPcapDevices => NetworkAdapters
                    .Where(adapter => adapter is LibPcapLiveDevice)
                    .Select(adapter => adapter as LibPcapLiveDevice)
                    .ToList()
                    .AsReadOnly();
    }
}