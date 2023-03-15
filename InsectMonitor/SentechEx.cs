using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using Sentech.GenApiDotNET;
using Sentech.StApiDotNET;

namespace InsectMonitor
{
    public class SentechEx
    {
        private CStApiAutoInit _api = null;
        private static CStSystem _system = null;
        private CStDevice _device = null;
        private CStDataStream _DataStream = null;

        private IStImageBuffer _lastBuffer = null;
        private IStImageBuffer _lastColorBuffer = null;
        public IStImageBuffer _RotateBuffer = null;
        public IStImageBuffer _ReverseBuffer = null;
        private EventWaitHandle _grabDone = null;
        private bool _isConvert = false;

        public SentechEx()
        {
            _api = new CStApiAutoInit();

            if (_system == null)
                _system = new CStSystem();

            _grabDone = new EventWaitHandle(false, EventResetMode.AutoReset);
        }

        ~SentechEx()
        {
            Close();

            if (_system != null)
            {
                _system.Dispose();
                _system = null;
            }

            if (_api != null)
            {
                _api.Dispose();
                _api = null;
            }
        }

        #region Open & Close
        public void OpenByDialog()
        {
            IStInterface iInterface = null;
            IStDeviceInfo deviceInfo = null;

            using (CStDeviceSelectionWnd wnd = new CStDeviceSelectionWnd())
            {
                //int w = System.Windows.Forms.SystemInformation.VirtualScreen.Width;
                //int h = System.Windows.Forms.SystemInformation.VirtualScreen.Height;
                //int w2 = 960;
                //int h2 = 720;

                //wnd.SetPosition(w / 2 - w2 / 2, h / 2 - h2 / 2, w2, h2);
                wnd.RegisterTargetIStSystem(_system);

                wnd.Show(eStWindowMode.Modal);
                wnd.GetSelectedDeviceInfo(out iInterface, out deviceInfo);
            }

            if (deviceInfo != null)
            {
                eDeviceAccessFlags deviceAccessFlags = eDeviceAccessFlags.CONTROL;
                if (deviceInfo.AccessStatus == eDeviceAccessStatus.READONLY)
                {
                    deviceAccessFlags = eDeviceAccessFlags.READONLY;
                }

                _device = iInterface.CreateStDevice(deviceInfo.ID, deviceAccessFlags);
                open();
            }
        }
        public void OpenBySerial(string serial)
        {
            IStInterface stInterface = null;
            IStDeviceInfo deviceInfo = null;
            for (uint j = 0; j < _system.InterfaceCount; j++)
            {
                stInterface = _system.GetIStInterface(j);
                stInterface.UpdateDeviceList();
                uint dvCount = stInterface.DeviceCount;
                for (uint i = 0; i < dvCount; i++)
                {
                    deviceInfo = stInterface.GetIStDeviceInfo(i);
                    if (serial == deviceInfo.SerialNumber)
                        goto EXIT;
                }
            }
        EXIT:
            if (deviceInfo != null)
            {
                eDeviceAccessFlags flag = eDeviceAccessFlags.CONTROL;
                switch (deviceInfo.AccessStatus)
                {
                    case (eDeviceAccessStatus.READONLY):
                        flag = eDeviceAccessFlags.READONLY;
                        break;
                    case (eDeviceAccessStatus.READWRITE):
                        flag = eDeviceAccessFlags.CONTROL;
                        break;
                }

                _device = stInterface.CreateStDevice(deviceInfo.ID, flag);
                open();
            }
        }
        public void OpenByUserID(string id)
        {
            IStInterface stInterface = null;
            IStDeviceInfo deviceInfo = null;
            for (uint j = 0; j < _system.InterfaceCount; j++)
            {
                stInterface = _system.GetIStInterface(j);
                stInterface.UpdateDeviceList();
                uint dvCount = stInterface.DeviceCount;
                for (uint i = 0; i < dvCount; i++)
                {
                    deviceInfo = stInterface.GetIStDeviceInfo(i);
                    if (id == deviceInfo.ID)
                        goto EXIT;
                }
            }
        EXIT:
            if (deviceInfo != null)
            {
                eDeviceAccessFlags flag = eDeviceAccessFlags.CONTROL;
                switch (deviceInfo.AccessStatus)
                {
                    case (eDeviceAccessStatus.READONLY):
                        flag = eDeviceAccessFlags.READONLY;
                        break;
                    case (eDeviceAccessStatus.READWRITE):
                        flag = eDeviceAccessFlags.CONTROL;
                        break;
                }

                _device = stInterface.CreateStDevice(deviceInfo.ID, flag);
                open();
            }
        }
        public void OpenByIPAdress(string ip)
        {
            for (uint j = 0; j < _system.InterfaceCount; j++)
            {
                IStInterface stInterface = _system.GetIStInterface(j);
                stInterface.UpdateDeviceList();

                if (stInterface.DeviceCount != 0)
                {
                    INodeMap nodeMap = stInterface.GetIStPort().GetINodeMap();

                    IInteger integerDeviceSelector = nodeMap.GetNode<IInteger>("DeviceSelector");

                    long nMaxIndex = integerDeviceSelector.Maximum;

                    IInteger integerGevDeviceIPAddress = nodeMap.GetNode<IInteger>("GevDeviceIPAddress");

                    for (uint i = 0; i <= nMaxIndex; ++i)
                    {
                        integerDeviceSelector.SetValue(i, true);

                        if (integerGevDeviceIPAddress.IsAvailable == true)
                        {
                            long nip = integerGevDeviceIPAddress.GetValue(true, true);

                            char[] digits = new char[4];
                            digits[0] = Convert.ToChar(nip & 0xFF);
                            digits[1] = Convert.ToChar((nip >> 8) & 0xFF);
                            digits[2] = Convert.ToChar((nip >> 16) & 0xFF);
                            digits[3] = Convert.ToChar((nip >> 24) & 0xFF);

                            string adrr = string.Format("{0}.{1}.{2}.{3}", Convert.ToInt32(digits[3]), Convert.ToInt32(digits[2]), Convert.ToInt32(digits[1]), Convert.ToInt32(digits[0]));
                            if (ip == adrr)
                            {
                                _device = stInterface.CreateStDevice(i);
                                open();
                            }
                        }
                    }
                }
            }
        }
        public void OpenDeviceControlByDialog()
        {
            using (CStNodeMapDisplayWnd wnd = new CStNodeMapDisplayWnd())
            {
                wnd.RegisterINode(_device.GetRemoteIStPort().GetINodeMap().GetNode<INode>("Root"), "Root");
                wnd.SetPosition(0, 0, 480, 640);
                wnd.Show(eStWindowMode.Modal);
            }
        }
        private void open()
        {
            if (_device == null)
                throw new Exception("The device is not created.");

            _lastBuffer = CStApiDotNet.CreateStImageBuffer();
            _lastBuffer.CreateBuffer((uint)Width, (uint)Height, (eStPixelFormatNamingConvention)Enum.Parse(typeof(eStPixelFormatNamingConvention), PixelFormat));

            if (_isConvert == true)
            {
                _lastColorBuffer = CStApiDotNet.CreateStImageBuffer();
                _lastColorBuffer.CreateBuffer((uint)Width, (uint)Height, eStPixelFormatNamingConvention.RGB8);
            }
            _RotateBuffer = CStApiDotNet.CreateStImageBuffer();
            _RotateBuffer.CreateBuffer((uint)Height, (uint)Width, (eStPixelFormatNamingConvention)Enum.Parse(typeof(eStPixelFormatNamingConvention), PixelFormat));

            _ReverseBuffer = CStApiDotNet.CreateStImageBuffer();
            _ReverseBuffer.CreateBuffer((uint)Width, (uint)Height, (eStPixelFormatNamingConvention)Enum.Parse(typeof(eStPixelFormatNamingConvention), PixelFormat));

        }

        public void Close()
        {
            try
            {
                if (_lastBuffer != null)
                {
                    _lastBuffer = null;
                }

                if (_lastColorBuffer != null)
                {
                    _lastColorBuffer = null;
                }

                if (_DataStream != null)
                {
                    _DataStream.Dispose();
                    _DataStream = null;
                }

                if (_device != null)
                {
                    _device.Dispose();
                    _device = null;
                }
                if (_RotateBuffer != null)
                {
                    _RotateBuffer = null;
                }
                if (_ReverseBuffer != null)
                {
                    _ReverseBuffer = null;
                }
            }
            catch (Exception exc)
            {
                Trace.WriteLine(exc.StackTrace);
                Trace.WriteLine(exc.Message);
            }
        }

        public void SetEnableImageCallback(bool value)
        {
            if (_DataStream != null)
            {
                _DataStream.Dispose();
                _DataStream = null;
            }
            _DataStream = _device.CreateStDataStream(0);
            if (value == true)
            {
                _DataStream.RegisterCallbackMethod(OnCallback);
            }
        }
        #endregion

        #region Acquisition
        public void Start()
        {
            _DataStream.StartAcquisition();

            _device.AcquisitionStart();
        }

        public void Stop()
        {
            _device.AcquisitionStop();

            _DataStream.StopAcquisition();
        }
        #endregion

        #region Buffer Control
        private void OnCallback(IStCallbackParamBase paramBase, object[] param)
        {
            // Check callback type. Only NewBuffer event is handled in here.
            if (paramBase.CallbackType == eStCallbackType.TL_DataStreamNewBuffer)
            {
                // In case of receiving a NewBuffer events:
                // Convert received callback parameter into IStCallbackParamGenTLEventNewBuffer for acquiring additional information.
                IStCallbackParamGenTLEventNewBuffer callbackParam = paramBase as IStCallbackParamGenTLEventNewBuffer;

                try
                {
                    // Get the IStDataStream interface object from the received callback parameter.
                    IStDataStream dataStream = callbackParam.GetIStDataStream();

                    // Retrieve the buffer of image data for that callback indicated there is a buffer received.
                    using (CStStreamBuffer streamBuffer = dataStream.RetrieveBuffer(0))
                    {
                        // Check if the acquired data contains image data.
                        if (streamBuffer.GetIStStreamBufferInfo().IsImagePresent)
                        {
                            // If yes, we create a IStImage object for further image handling.
                            _lastBuffer.CopyImage(streamBuffer.GetIStImage());
                            _grabDone.Set();
                        }
                    }
                }
                catch (Exception exc)
                {
                    Trace.WriteLine(exc.StackTrace);
                    Trace.WriteLine(exc.Message);
                }
            }
            else
            {
                switch (paramBase.CallbackType)
                {
                    case eStCallbackType.TL_SystemError:
                        Trace.WriteLine("TL_SystemError");
                        break;
                    case eStCallbackType.TL_InterfaceError:
                        Trace.WriteLine("TL_InterfaceError");
                        break;
                    case eStCallbackType.TL_DeviceError:
                        Trace.WriteLine("TL_DeviceError");
                        break;
                    case eStCallbackType.TL_DataStreamError:
                        Trace.WriteLine("TL_DataStreamError");
                        break;
                    case eStCallbackType.TL_StreamBufferError:
                        Trace.WriteLine("TL_StreamBufferError");
                        break;
                    case eStCallbackType.IP_VideoFilerOpen:
                        Trace.WriteLine("IP_VideoFilerOpen");
                        break;
                    case eStCallbackType.IP_VideoFilerClose:
                        Trace.WriteLine("IP_VideoFilerClose");
                        break;
                    case eStCallbackType.IP_VideoFilerError:
                        Trace.WriteLine("IP_VideoFilerError");
                        break;
                    case eStCallbackType.GUI_DisplayImageWndDrawing:
                        Trace.WriteLine("GUI_DisplayImageWndDrawing");
                        break;
                    case eStCallbackType.GUI_WndCreate:
                        Trace.WriteLine("GUI_WndCreate");
                        break;
                    case eStCallbackType.GUI_WndClose:
                        Trace.WriteLine("GUI_WndClose");
                        break;
                    case eStCallbackType.GUI_WndDestroy:
                        Trace.WriteLine("GUI_WndDestroy");
                        break;
                    case eStCallbackType.GUI_WndError:
                        Trace.WriteLine("GUI_WndError");
                        break;
                    case eStCallbackType.Count:
                        Trace.WriteLine("Count");
                        break;
                }
            }
        }

        public byte[] Grab(uint timeOut)
        {
            byte[] buffer = null;

            using (CStStreamBuffer streamBuffer = _DataStream.RetrieveBuffer(timeOut))
            {
                if (streamBuffer.GetIStStreamBufferInfo().IsImagePresent)
                {
                    IStImage stImage = streamBuffer.GetIStImage();
                    if (_isConvert == false)
                    {
                        buffer = new byte[stImage.ImagePlanePitch * stImage.ImageHeight];
                        buffer = stImage.GetByteArray();
                    }
                    else
                    {
                        buffer = new byte[stImage.ImagePlanePitch * stImage.ImageHeight];
                        using (CStImageBuffer imageBuffer = CStApiDotNet.CreateStImageBuffer())
                        using (CStPixelFormatConverter pixelFormatConverter = new CStPixelFormatConverter())
                        {
                            imageBuffer.CreateBuffer((uint)Width, (uint)Height, eStPixelFormatNamingConvention.BGR8);
                            pixelFormatConverter.DestinationPixelFormat = eStPixelFormatNamingConvention.BGR8;
                            pixelFormatConverter.BayerInterpolationMethod = eStBayerInterpolationMethod.NearestNeighbor2;
                            pixelFormatConverter.Convert(stImage, imageBuffer);

                            buffer = imageBuffer.GetIStImage().GetByteArray();
                        }
                    }
                }
            }
            return buffer;
        }
        public byte[] Buffer
        {
            get
            {
                return _lastBuffer.GetIStImage().GetByteArray();
            }
        }

        public byte[] ColorBuffer
        {
            get
            {
                using (CStImageBuffer imageBuffer = CStApiDotNet.CreateStImageBuffer())
                using (CStPixelFormatConverter pixelFormatConverter = new CStPixelFormatConverter())
                {
                    imageBuffer.CreateBuffer((uint)Width, (uint)Height, eStPixelFormatNamingConvention.BGR8);
                    pixelFormatConverter.DestinationPixelFormat = eStPixelFormatNamingConvention.BGR8;
                    pixelFormatConverter.BayerInterpolationMethod = eStBayerInterpolationMethod.NearestNeighbor2;
                    pixelFormatConverter.Convert(_lastBuffer.GetIStImage(), imageBuffer);

                    _lastColorBuffer.CopyImage(imageBuffer.GetIStImage());
                    return _lastColorBuffer.GetIStImage().GetByteArray();
                }
            }
        }
        public void SetColorConversion(bool enable)
        {
            _isConvert = enable;
        }

        public byte[] RotateClockWiseBuffer
        {
            get
            {
                using (CStReverseConverter pStRotateConverter = new CStReverseConverter())
                {
                    pStRotateConverter.RotationMode = eStRotationMode.Clockwise90;
                    pStRotateConverter.Convert(_lastBuffer.GetIStImage(), _RotateBuffer);

                    return _RotateBuffer.GetIStImage().GetByteArray();
                }
            }
        }
        public byte[] RotateCounterClockWiseBuffer
        {
            get
            {
                using (CStReverseConverter pStRotateConverter = new CStReverseConverter())
                {
                    pStRotateConverter.RotationMode = eStRotationMode.Counterclockwise90;
                    pStRotateConverter.Convert(_lastBuffer.GetIStImage(), _RotateBuffer);

                    return _RotateBuffer.GetIStImage().GetByteArray();
                }
            }
        }

        public byte[] ReverseXBuffer
        {
            get
            {
                using (CStReverseConverter pStRotateConverter = new CStReverseConverter())
                {
                    pStRotateConverter.ReverseX = true;
                    pStRotateConverter.ReverseY = false;

                    pStRotateConverter.Convert(_lastBuffer.GetIStImage(), _ReverseBuffer);
                    return _ReverseBuffer.GetIStImage().GetByteArray();

                }
            }
        }

        public byte[] ReverseYBuffer
        {
            get
            {
                using (CStReverseConverter pStRotateConverter = new CStReverseConverter())
                {
                    pStRotateConverter.ReverseX = false;
                    pStRotateConverter.ReverseY = true;

                    pStRotateConverter.Convert(_lastBuffer.GetIStImage(), _ReverseBuffer);
                    return _ReverseBuffer.GetIStImage().GetByteArray();

                }
            }
        }

        public void SaveImage(string path)
        {
            using (CStStillImageFiler stillImageFiler = new CStStillImageFiler())
            {
                if (_isConvert == true)
                    stillImageFiler.Save(_lastBuffer.GetIStImage(), eStStillImageFileFormat.Bitmap, path);
                else
                    stillImageFiler.Save(_lastColorBuffer.GetIStImage(), eStStillImageFileFormat.Bitmap, path);
            }
        }
        public EventWaitHandle HandleGrabDone
        {
            get
            {
                return _grabDone;
            }
        }

        public void OnResetEventGrabDone()
        {
            _grabDone.Reset();
        }
        #endregion

        #region Status Check Functions
        public bool IsOpened
        {
            get
            {
                return (_device != null);
            }
        }

        public bool IsActived
        {
            get
            {
                if (_DataStream == null) return false;
                return _DataStream.IsGrabbing;
            }
        }
        #endregion

        #region Setter
        public void SetValueBoolean(string node, bool value)
        {
            _device.GetRemoteIStPort().GetINodeMap().GetNode<IBool>(node).Value = value;
        }
        public void SetValueFloat(string node, float value)
        {
            _device.GetRemoteIStPort().GetINodeMap().GetNode<IFloat>(node).Value = value;
        }
        public void SetValueInteger(string node, int value)
        {
            _device.GetRemoteIStPort().GetINodeMap().GetNode<IInteger>(node).Value = value;
        }
        public void SetValueString(string node, string value)
        {
            _device.GetRemoteIStPort().GetINodeMap().GetNode<IString>(node).Value = value;
        }
        public void SetValueEnumString(string node, string value)
        {
            _device.GetRemoteIStPort().GetINodeMap().GetNode<IEnum>(node).StringValue = value;
        }
        public void SetValueEnumInteger(string node, int value)
        {
            _device.GetRemoteIStPort().GetINodeMap().GetNode<IEnum>(node).IntValue = value;
        }
        #endregion

        #region Getter
        public bool GetValueBoolean(string node)
        {
            return _device.GetRemoteIStPort().GetINodeMap().GetNode<IBool>(node).Value;
        }
        public double GetValueFloat(string node)
        {
            return _device.GetRemoteIStPort().GetINodeMap().GetNode<IFloat>(node).Value;
        }
        public long GetValueInteger(string node)
        {
            return _device.GetRemoteIStPort().GetINodeMap().GetNode<IInteger>(node).Value;
        }
        public string GetValueString(string node)
        {
            return _device.GetRemoteIStPort().GetINodeMap().GetNode<IString>(node).Value;
        }
        public string GetValueEnumString(string node)
        {
            return _device.GetRemoteIStPort().GetINodeMap().GetNode<IEnum>(node).StringValue;
        }
        public long GetValueEnumInteger(string node)
        {
            return _device.GetRemoteIStPort().GetINodeMap().GetNode<IEnum>(node).IntValue;
        }
        #endregion

        #region Executable
        public void ExecuteCommand(string node)
        {
            _device.GetRemoteIStPort().GetINodeMap().GetNode<ICommand>(node).Execute();
        }
        #endregion

        #region Properties 
        public string ExposureMode
        {
            get { return _device.GetRemoteIStPort().GetINodeMap().GetNode<IEnum>("ExposureMode").StringValue; }
            set { _device.GetRemoteIStPort().GetINodeMap().GetNode<IEnum>("ExposureMode").StringValue = value; }
        }
        public string TriggerMode
        {
            get { return _device.GetRemoteIStPort().GetINodeMap().GetNode<IEnum>("TriggerMode").StringValue; }
            set { _device.GetRemoteIStPort().GetINodeMap().GetNode<IEnum>("TriggerMode").StringValue = value; }
        }
        public string TriggerSource
        {
            get { return _device.GetRemoteIStPort().GetINodeMap().GetNode<IEnum>("TriggerSource").StringValue; }
            set { _device.GetRemoteIStPort().GetINodeMap().GetNode<IEnum>("TriggerSource").StringValue = value; }
        }
        public string TriggerSelector
        {
            get { return _device.GetRemoteIStPort().GetINodeMap().GetNode<IEnum>("TriggerSelector").StringValue; }
            set { _device.GetRemoteIStPort().GetINodeMap().GetNode<IEnum>("TriggerSelector").StringValue = value; }
        }
        public long Width
        {
            get { return _device.GetRemoteIStPort().GetINodeMap().GetNode<IInteger>("Width").Value; }
            set { _device.GetRemoteIStPort().GetINodeMap().GetNode<IInteger>("Width").Value = value; }
        }
        public long Height
        {
            get { return _device.GetRemoteIStPort().GetINodeMap().GetNode<IInteger>("Height").Value; }
            set { _device.GetRemoteIStPort().GetINodeMap().GetNode<IInteger>("Height").Value = value; }
        }

        public string PixelFormat
        {
            get { return _device.GetRemoteIStPort().GetINodeMap().GetNode<IEnum>("PixelFormat").StringValue; }
            set { _device.GetRemoteIStPort().GetINodeMap().GetNode<IEnum>("PixelFormat").StringValue = value; }
        }
        public string DeviceModelName
        {
            get { return _device.GetRemoteIStPort().GetINodeMap().GetNode<IString>("DeviceModelName").Value; }
        }
        public string DeviceSerialNumber
        {
            get { return _device.GetLocalIStPort().GetINodeMap().GetNode<IString>("DeviceSerialNumber").Value; }
        }

        #endregion
    }
}
