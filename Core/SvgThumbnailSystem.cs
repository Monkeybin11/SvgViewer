﻿using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SvgViewer.Utility;

namespace SvgViewer.Core
{
    public class SvgUtilSetupInfo
    {
        public Brush Stroke { get; set; } = Brushes.Black;
        public Brush Fill { get; set; } = Brushes.Gray;
        public double StrokeThickness { get; set; } = 1;
        public PenLineJoin StrokeLineJoin { get; set; } = PenLineJoin.Round;
        public int Width { get; set; } = 256;
        public int Height { get; set; } = 256;
    }

    public class SvgThumbnailSystem
    {
        private readonly SvgUtilSetupInfo _setupInfo;
        private readonly StaWorkerManager _staWorkerManager;
        public bool UsingSingleThread { get; set; }
        public SvgThumbnailSystem(SvgUtilSetupInfo setupInfo , StaWorkerManager workerManager = null)
        {
            _setupInfo = setupInfo ?? new SvgUtilSetupInfo();
            _staWorkerManager = workerManager;
        }

        private object _lock = new object();

        private void CreateThumbnail(string filePath)
        {
            var thumnbnailServis = new ThumbnailService(filePath);
            var view = new Border()
            {
                Width = _setupInfo.Width,
                Height = _setupInfo.Height,
            };

            var pathGeometry = SvgThumbnailUtil.GetGeometry(filePath);

            var path = new System.Windows.Shapes.Path()
            {
                Data = pathGeometry,
                Fill = _setupInfo.Fill,
                Stroke = _setupInfo.Stroke,
                StrokeThickness = _setupInfo.StrokeThickness,
                Stretch = Stretch.Uniform,
                Opacity = 1.0,
                StrokeLineJoin = _setupInfo.StrokeLineJoin,
            };
            view.Child = path;

            view.Measure(new Size(_setupInfo.Width, _setupInfo.Height));
            view.Arrange(new Rect(new Size(_setupInfo.Width, _setupInfo.Height)));

            //! rendering
            var bmp = new RenderTargetBitmap(_setupInfo.Width, _setupInfo.Height, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(view);

            //! save and encode to file
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));

            lock (_lock)
            {
                thumnbnailServis.CreateThumbnailDirectory();
                using (var stream = File.Create(thumnbnailServis.ThumbnailPath))
                    encoder.Save(stream);
            }
        }

        public ImageSource GetImageSource(string filePath)
        {
            var thumnbnailServis = new ThumbnailService(filePath);

            if (thumnbnailServis.ExistsThumnail is false)
                CreateThumbnail(filePath);

            var image = new BitmapImage();

            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = thumnbnailServis.GetThumnailMemorySteream();
            image.EndInit();

            return image.DoFreeze();
        }

        public ImageSource GetImageSourceAsync(string filePath, ImageSource _default, Action<ImageSource> onLoaded)
        {
            if (File.Exists(filePath) is false)
                return _default;

            void DecodeAction()
            {
                var thumnbnailServis = new ThumbnailService(filePath);

                if (thumnbnailServis.ExistsThumnail is false)
                    CreateThumbnail(filePath);
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    onLoaded(GetImageSource(filePath));
                }, DispatcherPriority.ContextIdle);
            }

            if(UsingSingleThread)
                DecodeAction();
            else if (_staWorkerManager != null)
                _staWorkerManager.AddJob(DecodeAction);
            else
                TaskEx.RunOnSta(DecodeAction);
            return _default;
        }
    }
}
