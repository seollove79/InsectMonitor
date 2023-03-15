using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace InsectMonitor
{
    public partial class Form1 : Form
    {
        private const int MAX_CAM = 1;
        public delegate void PaintDelegate(Graphics g);

        private SentechEx[] _camera = null;
        private Mutex[] _mutexImage = null;
        private Bitmap[] _bitmap = null;
        private Thread[] _threadCamera = null;
        private bool[] _isWorkingThreadCamera = null;
        private bool[] _isBayerConvert = null;
        public Form1()
        {
            InitializeComponent();
            _camera = new SentechEx[MAX_CAM];
            _mutexImage = new Mutex[MAX_CAM];
            _bitmap = new Bitmap[MAX_CAM];
            _threadCamera = new Thread[MAX_CAM];
            _isWorkingThreadCamera = new bool[MAX_CAM];
            _isBayerConvert = new bool[MAX_CAM];

            for (int i = 0; i < MAX_CAM; i++)
            {
                _camera[i] = new SentechEx();
                _mutexImage[i] = new Mutex();
            }
        }
        private void CreateBitmap(int index, int width, int height, System.Drawing.Imaging.PixelFormat format)
        {
            if (format == PixelFormat.Format8bppIndexed)
            {
                _bitmap[index] = new Bitmap((int)width, (int)height, PixelFormat.Format8bppIndexed);
                if (_bitmap[index].PixelFormat == PixelFormat.Format8bppIndexed)
                {
                    ColorPalette colorPalette = _bitmap[index].Palette;
                    for (int i = 0; i < 256; i++)
                    {
                        colorPalette.Entries[i] = Color.FromArgb(i, i, i);
                    }

                    _bitmap[index].Palette = colorPalette;
                }
            }
            else
                _bitmap[index] = new Bitmap((int)width, (int)height, PixelFormat.Format24bppRgb);
        }

        #region DisplayThread & Redraw
        private static void DisplayThread_Cam1(object aParameters)
        {
            object[] lParameters = (object[])aParameters;
            Form1 lThis = (Form1)lParameters[0];

            while (lThis._isWorkingThreadCamera[0])
            {
                Thread.Sleep(100);

                EventWaitHandle handle = lThis._camera[0].HandleGrabDone;
                if (handle.WaitOne(1000) == true)
                {
                    lThis._mutexImage[0].WaitOne();

                    BitmapData bmpData = lThis._bitmap[0].LockBits(new Rectangle(0, 0, lThis._bitmap[0].Width, lThis._bitmap[0].Height), ImageLockMode.ReadWrite, lThis._bitmap[0].PixelFormat);

                    IntPtr ptrBmp = bmpData.Scan0;
                    int bpp = 8;
                    if (lThis._bitmap[0].PixelFormat == PixelFormat.Format24bppRgb)
                    {
                        bpp = 24;
                        Marshal.Copy(lThis._camera[0].ColorBuffer, 0, ptrBmp, lThis._bitmap[0].Width * lThis._bitmap[0].Height * bpp / 8);
                    }
                    else
                        Marshal.Copy(lThis._camera[0].Buffer, 0, ptrBmp, lThis._bitmap[0].Width * lThis._bitmap[0].Height * bpp / 8);
                    lThis._bitmap[0].UnlockBits(bmpData);

                    lThis.BeginInvoke(new PaintDelegate(lThis.Redraw_Cam1), new object[1] { lThis.pbCam1.CreateGraphics() });

                    lThis._camera[0].OnResetEventGrabDone();

                    lThis._mutexImage[0].ReleaseMutex();
                }
            }
        }

        void Redraw_Cam1(Graphics g)
        {
            try
            {
                _mutexImage[0].WaitOne();

                if (_bitmap[0] != null)
                {
                    g.DrawImage(_bitmap[0], 0, 0, pbCam1.Height, pbCam1.Width);
                }
            }
            catch (System.Exception exc)
            {
                System.Diagnostics.Trace.WriteLine(exc.Message, "System Exception");
            }
            finally
            {
                _mutexImage[0].ReleaseMutex();
            }
        }
        #endregion

        private void checkBox_Bayer_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox check = (CheckBox)sender;
            int index = Convert.ToInt32(((CheckBox)sender).Name.Substring(("cbBayer").Length, 1)) - 1;

            if (check.Checked)
            {
                _camera[index].SetColorConversion(true);
                _isBayerConvert[index] = true;
            }
            else
            {
                _camera[index].SetColorConversion(false);
                _isBayerConvert[index] = false;
            }
        }

        private void btn_Connection_Click(object sender, EventArgs e)
        {
            try
            {
                Button btn = (Button)sender;
                int index = Convert.ToInt32(((Button)sender).Name.Substring(("btnConnection").Length, 1)) - 1;

                if (btn.Text == "Open")
                {
                    if (index == 0)
                        _camera[index].OpenByDialog();
                    else if (index == 1)
                        _camera[index].OpenByDialog();
                    else if (index == 2)
                        _camera[index].OpenByDialog();
                    else if (index == 3)
                        _camera[index].OpenByDialog();

                    if (_camera[index].IsOpened == false)
                        return;

                    long width = 0, height = 0;
                    width = _camera[index].Width;
                    height = _camera[index].Height;

                    if (_isBayerConvert[index] == false)
                        CreateBitmap(index, (int)width, (int)height, PixelFormat.Format8bppIndexed);
                    else
                        CreateBitmap(index, (int)width, (int)height, PixelFormat.Format24bppRgb);

                    string model = "", serial = "";
                    model = _camera[index].DeviceModelName;
                    serial = _camera[index].DeviceSerialNumber;
                    object[] lParameters = new object[] { this };
                    //Callback Registered
                    _camera[index].SetEnableImageCallback(true);
                    _isWorkingThreadCamera[index] = true;

                    switch (index)
                    {
                        case 0: _threadCamera[index] = new Thread(new ParameterizedThreadStart(DisplayThread_Cam1)); break;
                    }

                    _threadCamera[index].Start(lParameters);

                    ((Button)sender).Text = "Close";
                    this.Controls.Find("lbModel" + (index + 1), true).FirstOrDefault().Text = model;
                    this.Controls.Find("lbSerial" + (index + 1), true).FirstOrDefault().Text = serial;
                    this.Controls.Find("cbBayer" + (index + 1), true).FirstOrDefault().Enabled = false;
                }
                else
                {
                    if (_threadCamera[index] != null)
                    {
                        _isWorkingThreadCamera[index] = false;
                        _threadCamera[index].Join();
                        _threadCamera[index] = null;
                    }

                    if (_camera[index].IsActived == true)
                    {
                        _camera[index].Stop();
                        this.Controls.Find("btnAcquisition" + (index + 1), true).FirstOrDefault().Text = "Start";
                    }

                    _camera[index].Close();

                    ((Button)sender).Text = "Open";
                    this.Controls.Find("lbModel" + (index + 1), true).FirstOrDefault().Text = "-";
                    this.Controls.Find("lbSerial" + (index + 1), true).FirstOrDefault().Text = "-";
                    this.Controls.Find("cbBayer" + (index + 1), true).FirstOrDefault().Enabled = true;
                }
            }
            catch (Exception exc)
            {
                Debug.WriteLine(exc);
            }
        }
        
        private void btn_Acquisition_Click(object sender, EventArgs e)
        {
            try
            {
                Button btn = (Button)sender;
                int index = 0;
                if (((Button)sender).Text == "Start")
                {
                    _camera[index].Start();
                    ((Button)sender).Text = "Stop";
                }
                else
                {
                    _camera[index].Stop();
                    ((Button)sender).Text = "Start";
                }
            }

            catch (Exception exc)
            {
                Debug.WriteLine(exc);
            }
        }

        private void btn_connection_Click_1(object sender, EventArgs e)
        {
            try
            {
                Button btn = (Button)sender;
                int index = 0;

                if (btn.Text == "Open")
                {
                    if (index == 0)
                        _camera[index].OpenByDialog();
                    else if (index == 1)
                        _camera[index].OpenByDialog();
                    else if (index == 2)
                        _camera[index].OpenByDialog();
                    else if (index == 3)
                        _camera[index].OpenByDialog();

                    if (_camera[index].IsOpened == false)
                        return;

                    long width = 0, height = 0;
                    width = _camera[index].Width;
                    height = _camera[index].Height;

                    if (_isBayerConvert[index] == false)
                        CreateBitmap(index, (int)width, (int)height, PixelFormat.Format8bppIndexed);
                    else
                        CreateBitmap(index, (int)width, (int)height, PixelFormat.Format24bppRgb);

                    string model = "", serial = "";
                    model = _camera[index].DeviceModelName;
                    serial = _camera[index].DeviceSerialNumber;
                    object[] lParameters = new object[] { this };
                    //Callback Registered
                    _camera[index].SetEnableImageCallback(true);
                    _isWorkingThreadCamera[index] = true;

                    switch (index)
                    {
                        case 0: _threadCamera[index] = new Thread(new ParameterizedThreadStart(DisplayThread_Cam1)); break;
                    }

                    _threadCamera[index].Start(lParameters);

                    ((Button)sender).Text = "Close";
                    /*this.Controls.Find("lbModel" + (index + 1), true).FirstOrDefault().Text = model;
                    this.Controls.Find("lbSerial" + (index + 1), true).FirstOrDefault().Text = serial;
                    this.Controls.Find("cbBayer" + (index + 1), true).FirstOrDefault().Enabled = false;*/
                }
                else
                {
                    if (_threadCamera[index] != null)
                    {
                        _isWorkingThreadCamera[index] = false;
                        _threadCamera[index].Join();
                        _threadCamera[index] = null;
                    }

                    if (_camera[index].IsActived == true)
                    {
                        _camera[index].Stop();
                        this.Controls.Find("btnAcquisition" + (index + 1), true).FirstOrDefault().Text = "Start";
                    }

                    _camera[index].Close();

                    ((Button)sender).Text = "Open";
/*                    this.Controls.Find("lbModel" + (index + 1), true).FirstOrDefault().Text = "-";
                    this.Controls.Find("lbSerial" + (index + 1), true).FirstOrDefault().Text = "-";
                    this.Controls.Find("cbBayer" + (index + 1), true).FirstOrDefault().Enabled = true;*/
                }
            }
            catch (Exception exc)
            {
                Debug.WriteLine(exc);
            }
        }
        
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            for (int i = 0; i < MAX_CAM; i++)
            {
                if (_threadCamera[i] != null)
                {
                    _isWorkingThreadCamera[i] = false;
                    _threadCamera[i].Join();
                    _threadCamera[i] = null;
                }
                _camera[i].Close();
            }
        }
    }
}
