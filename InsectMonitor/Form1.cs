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
        public delegate void PaintDelegate(Graphics g);

        private SentechEx _camera;
        private Mutex _mutexImage;
        private Bitmap _bitmap;
        private Thread _threadCamera;
        private bool _isWorkingThreadCamera;
        private bool _isBayerConvert;
        public Form1()
        {
            InitializeComponent();

            _camera = new SentechEx();
            _mutexImage = new Mutex();
        }

        private void CreateBitmap(int index, int width, int height, System.Drawing.Imaging.PixelFormat format)
        {
            if (format == PixelFormat.Format8bppIndexed)
            {
                _bitmap = new Bitmap((int)width, (int)height, PixelFormat.Format8bppIndexed);
                if (_bitmap.PixelFormat == PixelFormat.Format8bppIndexed)
                {
                    ColorPalette colorPalette = _bitmap.Palette;
                    for (int i = 0; i < 256; i++)
                    {
                        colorPalette.Entries[i] = Color.FromArgb(i, i, i);
                    }

                    _bitmap.Palette = colorPalette;
                }
            }
            else
                _bitmap = new Bitmap((int)width, (int)height, PixelFormat.Format24bppRgb);
        }

        #region DisplayThread & Redraw
        private static void DisplayThread_Cam(object aParameters)
        {
            object[] lParameters = (object[])aParameters;
            Form1 lThis = (Form1)lParameters[0];

            while (lThis._isWorkingThreadCamera)
            {
                Thread.Sleep(100);

                EventWaitHandle handle = lThis._camera.HandleGrabDone;
                if (handle.WaitOne(1000) == true)
                {
                    lThis._mutexImage.WaitOne();

                    BitmapData bmpData = lThis._bitmap.LockBits(new Rectangle(0, 0, lThis._bitmap.Width, lThis._bitmap.Height), ImageLockMode.ReadWrite, lThis._bitmap.PixelFormat);

                    IntPtr ptrBmp = bmpData.Scan0;
                    int bpp = 8;
                    if (lThis._bitmap.PixelFormat == PixelFormat.Format24bppRgb)
                    {
                        bpp = 24;
                        Marshal.Copy(lThis._camera.ColorBuffer, 0, ptrBmp, lThis._bitmap.Width * lThis._bitmap.Height * bpp / 8);
                    }
                    else
                        Marshal.Copy(lThis._camera.Buffer, 0, ptrBmp, lThis._bitmap.Width * lThis._bitmap.Height * bpp / 8);
                    lThis._bitmap.UnlockBits(bmpData);

                    lThis.BeginInvoke(new PaintDelegate(lThis.Redraw_Cam), new object[1] { lThis.pbCam1.CreateGraphics() });

                    lThis._camera.OnResetEventGrabDone();

                    lThis._mutexImage.ReleaseMutex();
                }
            }
        }

        void Redraw_Cam(Graphics g)
        {
            try
            {
                _mutexImage.WaitOne();

                if (_bitmap != null)
                {
                    g.DrawImage(_bitmap, 0, 0, pbCam1.Height, pbCam1.Width);
                }
            }
            catch (System.Exception exc)
            {
                System.Diagnostics.Trace.WriteLine(exc.Message, "System Exception");
            }
            finally
            {
                _mutexImage.ReleaseMutex();
            }
        }
        #endregion

        private void checkBox_Bayer_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox check = (CheckBox)sender;
            int index = Convert.ToInt32(((CheckBox)sender).Name.Substring(("cbBayer").Length, 1)) - 1;

            if (check.Checked)
            {
                _camera.SetColorConversion(true);
                _isBayerConvert = true;
            }
            else
            {
                _camera.SetColorConversion(false);
                _isBayerConvert = false;
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
                    _camera.Start();
                    ((Button)sender).Text = "Stop";
                }
                else
                {
                    _camera.Stop();
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
                    _camera.OpenByDialog();
                    if (_camera.IsOpened == false)
                        return;

                    long width = 0, height = 0;
                    width = _camera.Width;
                    height = _camera.Height;

                    if (_isBayerConvert == false)
                        CreateBitmap(index, (int)width, (int)height, PixelFormat.Format8bppIndexed);
                    else
                        CreateBitmap(index, (int)width, (int)height, PixelFormat.Format24bppRgb);

                    string model = "", serial = "";
                    model = _camera.DeviceModelName;
                    serial = _camera.DeviceSerialNumber;
                    object[] lParameters = new object[] { this };
                    //Callback Registered
                    _camera.SetEnableImageCallback(true);
                    _isWorkingThreadCamera = true;

                    _threadCamera = new Thread(new ParameterizedThreadStart(DisplayThread_Cam));

                    _threadCamera.Start(lParameters);

                    ((Button)sender).Text = "Close";
                    /*this.Controls.Find("lbModel" + (index + 1), true).FirstOrDefault().Text = model;
                    this.Controls.Find("lbSerial" + (index + 1), true).FirstOrDefault().Text = serial;
                    this.Controls.Find("cbBayer" + (index + 1), true).FirstOrDefault().Enabled = false;*/
                }
                else
                {
                    if (_threadCamera != null)
                    {
                        _isWorkingThreadCamera = false;
                        _threadCamera.Join();
                        _threadCamera = null;
                    }

                    if (_camera.IsActived == true)
                    {
                        _camera.Stop();
                        this.Controls.Find("btnAcquisition" + (index + 1), true).FirstOrDefault().Text = "Start";
                    }

                    _camera.Close();

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
            if (_threadCamera != null)
            {
                _isWorkingThreadCamera = false;
                _threadCamera.Join();
                _threadCamera = null;
            }
            _camera.Close();
        }
    }
}
