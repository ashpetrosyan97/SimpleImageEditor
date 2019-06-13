using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Point = System.Windows.Point;
using Image = System.Drawing.Image;
using WIA;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Microsoft.Win32;
using System.ComponentModel;
using Forms = System.Windows.Forms;

namespace SimpleImageEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            images.SelectionChanged += Images_SelectionChanged;
        }

        const string WIA_HORIZONTAL_SCAN_RESOLUTION_DPI = "6147";
        const string WIA_VERTICAL_SCAN_RESOLUTION_DPI = "6148";

        List<Bitmap> parts = new List<Bitmap>();
        DeviceManager manager = new DeviceManager();
        byte[] imageBytes;
        Point Start, End;
        Forms.FolderBrowserDialog fbd = new Forms.FolderBrowserDialog();
        private Bitmap ScaledImage;
        private Bitmap OriginalImage;
        private Bitmap CroppedImage;
        private Bitmap DisplayImage;
        private Graphics DisplayGraphics;
        float ImageScale = 0.25f;
        bool drawing = false;


        private void Scan_Click(object sender, RoutedEventArgs e)
        {
            ICommonDialog dialog = new WIA.CommonDialog();
            DeviceInfo device = null;
            for (int i = 1; i <= manager.DeviceInfos.Count; i++)
            {
                if (manager.DeviceInfos[i].Type != WiaDeviceType.ScannerDeviceType)
                {
                    continue;
                }

                device = manager.DeviceInfos[i];

                break;
            }

            if (device == null)
            {
                MessageBox.Show("Scanner doesn't attached", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var d = device.Connect();
            Item item = d.Items[1];
            SetWIAProperty(item.Properties, WIA_HORIZONTAL_SCAN_RESOLUTION_DPI, 300);
            SetWIAProperty(item.Properties, WIA_VERTICAL_SCAN_RESOLUTION_DPI, 300);
            ImageFile image = (ImageFile)dialog.ShowTransfer(item, FormatID.wiaFormatJPEG, true);
            if (image == null) return;
            imageBytes = (byte[])image.FileData.get_BinaryData();
            OriginalImage = (Bitmap)Image.FromStream(new MemoryStream(imageBytes));
            OriginalImage.RotateFlip(RotateFlipType.Rotate90FlipNone);
            CroppedImage = OriginalImage.Clone() as Bitmap;
            ImageScale = (float)Width / OriginalImage.Width;
            MakeScaledImage();
        }

        private void WorkingArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            drawing = true;
            Start = e.GetPosition(WorkingArea);
            Canvas.SetTop(selectionRectangle, Start.Y);
            Canvas.SetLeft(selectionRectangle, Start.X);
            selectionRectangle.Visibility = Visibility.Visible;
        }

        private void WorkingArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (!drawing) return;

            End = e.GetPosition(WorkingArea);
            DrawSelectionBox(End);
        }

        private void WorkingArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!drawing) return;
            drawing = false;
            int x = (int)(Math.Min(Start.X, End.X) / ImageScale);
            int y = (int)(Math.Min(Start.Y, End.Y) / ImageScale);
            int width = (int)(Math.Abs(Start.X - End.X) / ImageScale);
            int height = (int)(Math.Abs(Start.Y - End.Y) / ImageScale);

            if ((width == 0) || (height == 0))
            {
                MessageBox.Show("Width and height must be greater than 0.");
                return;
            }

            Rectangle source_rect = new Rectangle(x, y, width, height);
            Rectangle dest_rect = new Rectangle(0, 0, width, height);

            Bitmap new_image = new Bitmap(width, height);
            using (Graphics gr = Graphics.FromImage(new_image))
            {
                gr.DrawImage(OriginalImage, dest_rect, source_rect, GraphicsUnit.Pixel);
            }
            CroppedImage = new_image;
        }
        private void DrawSelectionBox(Point end_point)
        {
            int x = (int)Math.Min(Start.X, End.X);
            int y = (int)Math.Min(Start.Y, End.Y);
            selectionRectangle.Width = (int)Math.Abs(Start.X - End.X);
            selectionRectangle.Height = (int)Math.Abs(Start.Y - End.Y);
            Canvas.SetTop(selectionRectangle, y);
            Canvas.SetLeft(selectionRectangle, x);
        }


        private void Images_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((sender as ListView).SelectedItem != null)
            {
                ActionBar.IsEnabled = true;
            }
            else
            {
                ActionBar.IsEnabled = false;
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ImageScale = float.Parse(e.NewValue.ToString());
            if (ScaledImage != null)
            {
                MakeScaledImage();
                selectionRectangle.Visibility = Visibility.Collapsed;
            }
        }

        private void Image_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            WorkingArea.Height = e.NewSize.Height;
            WorkingArea.Width = e.NewSize.Width;
        }

        private void Crop_Click(object sender, RoutedEventArgs e)
        {
            if (!selectionRectangle.IsVisible)
            {
                MessageBox.Show("Nothing is Selected", "Attention", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            selectionRectangle.Visibility = Visibility.Collapsed;
            parts.Add(CroppedImage);
            ListViewItem listitem = new ListViewItem()
            {
                Height = 130,
                Content = new System.Windows.Controls.Image()
                {
                    Source = ImageActions.BitmapImageFromBitmap(CroppedImage),
                    Stretch = Stretch.Uniform,
                }
            };
            images.Items.Add(listitem);
        }

        private void RotateLeft_Click(object sender, RoutedEventArgs e)
        {
            int index = images.SelectedIndex;
            parts[index].RotateFlip(RotateFlipType.Rotate270FlipNone);
            var item = images.SelectedItem as ListViewItem;
            ((System.Windows.Controls.Image)item.Content).Source = ImageActions.BitmapImageFromBitmap(parts[index]);
        }

        private void RotateRight_Click(object sender, RoutedEventArgs e)
        {
            int index = images.SelectedIndex;
            parts[index].RotateFlip(RotateFlipType.Rotate90FlipNone);
            var item = images.SelectedItem as ListViewItem;
            ((System.Windows.Controls.Image)item.Content).Source = ImageActions.BitmapImageFromBitmap(parts[index]);

        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            int index = images.SelectedIndex;
            parts.RemoveAt(index);
            images.Items.RemoveAt(index);
        }

        private void MakeScaledImage()
        {
            int wid = (int)(ImageScale * (OriginalImage.Width));
            int hgt = (int)(ImageScale * (OriginalImage.Height));
            ScaledImage = new Bitmap(wid, hgt);
            using (Graphics gr = Graphics.FromImage(ScaledImage))
            {
                Rectangle src_rect = new Rectangle(0, 0, OriginalImage.Width, OriginalImage.Height);
                Rectangle dest_rect = new Rectangle(0, 0, wid, hgt);
                gr.PixelOffsetMode = PixelOffsetMode.Half;
                gr.InterpolationMode = InterpolationMode.NearestNeighbor;
                gr.DrawImage(OriginalImage, dest_rect, src_rect, GraphicsUnit.Pixel);
            }

            DisplayImage = ScaledImage.Clone() as Bitmap;
            if (DisplayGraphics != null) DisplayGraphics.Dispose();
            DisplayGraphics = Graphics.FromImage(DisplayImage);
            image.Source = ImageActions.BitmapImageFromBitmap(DisplayImage);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            List<byte[]> converted = new List<byte[]>();
            foreach (var item in parts)
            {
                converted.Add(ImageActions.ByteArrayFromBitmap(item));
            }
            Close();
        }


        private void Open_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Image files (*.jpg, *.jpeg, *.jpe, *.jfif, *.png) | *.jpg; *.jpeg; *.jpe; *.jfif; *.png"
            };
            if (ofd.ShowDialog() == true)
            {
                OriginalImage = new Bitmap(ofd.FileName);
                CroppedImage = OriginalImage.Clone() as Bitmap;
                ImageScale = (float)Width / OriginalImage.Width;
                MakeScaledImage();
                WindowState = WindowState.Maximized;
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (fbd.ShowDialog() == Forms.DialogResult.OK)
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    parts[i].Save(Path.Combine(fbd.SelectedPath, $"image{i}.jpg"));
                }
                MessageBox.Show("Export Success", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }


        private static void SetWIAProperty(IProperties properties, object propName, object propValue)
        {
            Property prop = properties.get_Item(ref propName);
            prop.set_Value(ref propValue);
        }

    }
}
