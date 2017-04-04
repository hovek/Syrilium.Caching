using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace Syrilium.CommonInterface
{
	public enum ImageResizeType
	{
		KeepRatioFill = 0,
		/// <summary>
		/// Image cannot be bigger than target size.
		/// </summary>
		KeepRatioBox = 1
	}

	public interface IImageHelper
	{
		Image Resize(Stream imageStream, int targetWidth, int targetHeight, ImageResizeType imageResizeType = ImageResizeType.KeepRatioFill);
		Image Resize(Image image, int targetWidth, int targetHeight, ImageResizeType imageResizeType = ImageResizeType.KeepRatioFill);
		Size CorrectTargetSizeKeepRatioFill(Size target, Size source);

		ImageFormat GetImageFormatByFileExtension(string fileName);
		void CreateImageFormat(string pathToOriginal, string pathToFormated, string imageName, int imageHeight, int imageWidth, ImageResizeType imageResizeType = ImageResizeType.KeepRatioFill);
		string CheckAndGetImagePath(string pathToOriginal, string pathToFormated, string imageName, out bool createImageFormat, bool onNotExistsReturnEmpty = false);
	}
}
