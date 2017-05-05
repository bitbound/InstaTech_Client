using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace InstaTech_Client
{
    public class ScreenShot
    {
        private Bitmap _currentFrame;
        private Bitmap _lastFrame;
        private Rectangle _boundingBox;

        public Bitmap CurrentFrame => _currentFrame;

        public int TotalHeight { get; private set; } = 0;
        public int TotalWidth { get; private set; } = 0;

        public void Initialize()
        {
            TotalWidth = SystemInformation.VirtualScreen.Width;
            TotalHeight = SystemInformation.VirtualScreen.Height;
            _currentFrame = new Bitmap(TotalWidth, TotalHeight);
            _lastFrame = new Bitmap(TotalWidth, TotalHeight);
        }

        public void SaveCroppedFrame(MemoryStream ms)
        {
            using (var croppedFrame = _currentFrame.Clone(_boundingBox, PixelFormat.Format32bppArgb))
            {
                croppedFrame.Save(ms, ImageFormat.Jpeg);
            }
        }

        public void CloneLastFrame()
        {
            _lastFrame = (Bitmap)_currentFrame.Clone();
        }

        public byte[] GetNewData()
        {
            if (_currentFrame.Height != _lastFrame.Height || _currentFrame.Width != _lastFrame.Width)
            {
                throw new Exception("Bitmaps are not of equal dimensions.");
            }
            if (!Bitmap.IsAlphaPixelFormat(_currentFrame.PixelFormat) || !Bitmap.IsAlphaPixelFormat(_lastFrame.PixelFormat) ||
                !Bitmap.IsCanonicalPixelFormat(_currentFrame.PixelFormat) || !Bitmap.IsCanonicalPixelFormat(_lastFrame.PixelFormat))
            {
                throw new Exception("Bitmaps must be 32 bits per pixel and contain alpha channel.");
            }
            var width = _currentFrame.Width;
            var height = _currentFrame.Height;
            byte[] newImgData;
            int left = int.MaxValue;
            int top = int.MaxValue;
            int right = int.MinValue;
            int bottom = int.MinValue;

            var bd1 = _currentFrame.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, _currentFrame.PixelFormat);
            var bd2 = _lastFrame.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, _lastFrame.PixelFormat);
            // Get the address of the first line.
            IntPtr ptr1 = bd1.Scan0;
            IntPtr ptr2 = bd2.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes = Math.Abs(bd1.Stride) * _currentFrame.Height;
            byte[] rgbValues1 = new byte[bytes];
            byte[] rgbValues2 = new byte[bytes];

            // Copy the RGBA values into the array.
            Marshal.Copy(ptr1, rgbValues1, 0, bytes);
            Marshal.Copy(ptr2, rgbValues2, 0, bytes);

            // Check RGBA value for each pixel.
            for (int counter = 0; counter < rgbValues1.Length - 4; counter += 4)
            {
                if (rgbValues1[counter] != rgbValues2[counter] ||
                    rgbValues1[counter + 1] != rgbValues2[counter + 1] ||
                    rgbValues1[counter + 2] != rgbValues2[counter + 2] ||
                    rgbValues1[counter + 3] != rgbValues2[counter + 3])
                {
                    // Change was found.
                    var pixel = counter / 4;
                    var row = (int)Math.Floor((double)pixel / bd1.Width);
                    var column = pixel % bd1.Width;
                    if (row < top)
                    {
                        top = row;
                    }
                    if (row > bottom)
                    {
                        bottom = row;
                    }
                    if (column < left)
                    {
                        left = column;
                    }
                    if (column > right)
                    {
                        right = column;
                    }
                }
            }
            if (left < right && top < bottom)
            {
                // Bounding box is valid.

                left = Math.Max(left - 20, 0);
                top = Math.Max(top - 20, 0);
                right = Math.Min(right + 20, TotalWidth);
                bottom = Math.Min(bottom + 20, TotalHeight);

                // Byte array that indicates top left coordinates of the image.
                newImgData = new byte[6];
                newImgData[0] = Byte.Parse(left.ToString().PadLeft(6, '0').Substring(0, 2));
                newImgData[1] = Byte.Parse(left.ToString().PadLeft(6, '0').Substring(2, 2));
                newImgData[2] = Byte.Parse(left.ToString().PadLeft(6, '0').Substring(4, 2));
                newImgData[3] = Byte.Parse(top.ToString().PadLeft(6, '0').Substring(0, 2));
                newImgData[4] = Byte.Parse(top.ToString().PadLeft(6, '0').Substring(2, 2));
                newImgData[5] = Byte.Parse(top.ToString().PadLeft(6, '0').Substring(4, 2));

                _boundingBox = new System.Drawing.Rectangle(left, top, right - left, bottom - top);
                _currentFrame.UnlockBits(bd1);
                _lastFrame.UnlockBits(bd2);
                return newImgData;
            }
            else
            {
                _currentFrame.UnlockBits(bd1);
                _lastFrame.UnlockBits(bd2);
                return null;
            }
        }
    }
}
