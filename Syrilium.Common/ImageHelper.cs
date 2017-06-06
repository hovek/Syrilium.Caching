using Syrilium.CommonInterface;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Web;

namespace Syrilium.Common
{
	public class ImageHelper : IImageHelper
	{
		public Image Resize(Stream imageStream, int targetWidth, int targetHeight, ImageResizeType imageResizeType = ImageResizeType.KeepRatioFill)
		{
			using (Image originalImage = Image.FromStream(imageStream))
				return Resize(originalImage, targetWidth, targetHeight, imageResizeType);
		}

		public Image Resize(Image image, int targetWidth, int targetHeight, ImageResizeType imageResizeType = ImageResizeType.KeepRatioFill)
		{
			Size correctedSize;
			if (imageResizeType == ImageResizeType.KeepRatioFill)
				correctedSize = CorrectTargetSizeKeepRatioFill(new Size(targetWidth, targetHeight), new Size(image.Width, image.Height));
			else
				correctedSize = CorrectTargetSizeKeepRatioBox(new Size(targetWidth, targetHeight), new Size(image.Width, image.Height));

			Bitmap bitmap = new Bitmap(correctedSize.Width, correctedSize.Height);
			bitmap.SetResolution(image.HorizontalResolution, image.VerticalResolution);

			using (Graphics grPhoto = Graphics.FromImage(bitmap))
			{
				grPhoto.InterpolationMode = InterpolationMode.HighQualityBicubic;
				grPhoto.DrawImage(image,
					new Rectangle(0, 0, correctedSize.Width, correctedSize.Height),
					new Rectangle(0, 0, image.Width, image.Height),
					GraphicsUnit.Pixel);
			}
			return bitmap;
		}

		public Size CorrectTargetSizeKeepRatioBox(Size target, Size source)
		{
			Size targetSize = new Size();

			decimal targetRatio = (decimal)target.Width / (decimal)target.Height;
			decimal sourceRatio = (decimal)source.Width / (decimal)source.Height;

			if (sourceRatio < targetRatio)
			{
				targetSize.Height = target.Height;
				targetSize.Width = (int)((decimal)targetSize.Height * sourceRatio);
			}
			else
			{
				decimal ratio = (decimal)source.Height / (decimal)source.Width;
				targetSize.Width = target.Width;
				targetSize.Height = (int)((decimal)targetSize.Width * ratio);
			}
			return targetSize;
		}

		public Size CorrectTargetSizeKeepRatioFill(Size target, Size source)
		{
			Size targetSize = new Size();

			decimal targetRatio = (decimal)target.Width / (decimal)target.Height;
			decimal sourceRatio = (decimal)source.Width / (decimal)source.Height;

			if (sourceRatio < targetRatio)
			{
				decimal ratio = (decimal)source.Height / (decimal)source.Width;
				targetSize.Width = target.Width;
				targetSize.Height = (int)((decimal)targetSize.Width * ratio);
			}
			else
			{
				targetSize.Height = target.Height;
				targetSize.Width = (int)((decimal)targetSize.Height * sourceRatio);
			}
			return targetSize;
		}

		public string CheckAndGetImagePath(string pathToOriginal, string pathToFormated, string imageName, out bool createImageFormat, bool onNotExistsReturnEmpty = false)
		{
			createImageFormat = false;

			string fullReturnPath = string.Concat(pathToFormated, imageName);
			string serverPathToFormated = HttpContext.Current.Server.MapPath(fullReturnPath);
			if (File.Exists(serverPathToFormated)) return fullReturnPath;

			pathToOriginal = HttpContext.Current.Server.MapPath(string.Concat(pathToOriginal, imageName));
			if (File.Exists(pathToOriginal)) createImageFormat = true;

			return onNotExistsReturnEmpty && !createImageFormat ? "" : fullReturnPath;
		}

		public void CreateImageFormat(string pathToOriginal, string pathToFormated, string imageName, int imageHeight, int imageWidth, ImageResizeType imageResizeType = ImageResizeType.KeepRatioFill)
		{
			string serverPathToFormated = HttpContext.Current.Server.MapPath(pathToFormated);
			if (!Directory.Exists(serverPathToFormated))
				Directory.CreateDirectory(serverPathToFormated);

			Image newImage;
			pathToOriginal = HttpContext.Current.Server.MapPath(string.Concat(pathToOriginal, imageName));
			using (FileStream fs = File.OpenRead(pathToOriginal))
			{
				try
				{
					newImage = new ImageHelper().Resize(fs, imageWidth, imageHeight, imageResizeType);
				}
				catch (Exception ex)
				{
					throw new System.Exception(ex.Message + " " + pathToOriginal);
				}
			}
			try
			{
				string pathToImageFormated = Path.Combine(serverPathToFormated, imageName);
				newImage.Save(pathToImageFormated, GetImageFormatByFileExtension(imageName));
			}
			finally
			{
				newImage.Dispose();
			}
		}

		public ImageFormat GetImageFormatByFileExtension(string fileName)
		{
			int lastDotIndex = fileName.LastIndexOf('.');
			if (lastDotIndex == -1) lastDotIndex = fileName.Length - 1;
			string extension = fileName.Substring(lastDotIndex + 1, fileName.Length - lastDotIndex - 1).Trim().ToLower();
			switch (extension)
			{
				case "png":
					return ImageFormat.Png;
				default:
					return ImageFormat.Jpeg;
			}
		}
	}
}
