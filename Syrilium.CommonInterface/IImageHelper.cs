using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace Syrilium.CommonInterface
{
    public interface IImageHelper
    {
        Image Resize(Stream imageStream, int targetWidth, int targetHeight);
        Image Resize(Image image, int targetWidth, int targetHeight);
        Size CorrectTargetSize(Size target, Size source);

        ImageFormat GetImageFormatByFileExtension(string fileName);
        void CreateImageFormat(string pathToOriginal, string pathToFormated, string imageName, int imageHeight, int imageWidth);
        string CheckAndGetImagePath(string pathToOriginal, string pathToFormated, string imageName, out bool createImageFormat, bool onNotExistsReturnEmpty = false);
    }
}
