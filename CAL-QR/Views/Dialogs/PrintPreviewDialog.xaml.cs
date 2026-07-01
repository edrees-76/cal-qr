using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;
using CAL_QR.ViewModels;
using CAL_QR.Models;

namespace CAL_QR.Views.Dialogs
{
    public partial class PrintPreviewDialog : Window
    {
        private readonly int _calibrationRecordId;
        private PrintPreviewViewModel? _viewModel;

        public PrintPreviewDialog(int calibrationRecordId)
        {
            InitializeComponent();
            _calibrationRecordId = calibrationRecordId;

            if (App.ServiceProvider != null)
            {
                _viewModel = App.ServiceProvider.GetRequiredService<PrintPreviewViewModel>();
                _viewModel.CloseWindowAction = Close;
                _viewModel.RedrawGridRequested += OnRedrawGridRequested;
                DataContext = _viewModel;

                Loaded += async (s, e) =>
                {
                    await _viewModel.LoadDataAsync(_calibrationRecordId);
                    RedrawGrid();
                };
            }
        }

        private void OnRedrawGridRequested(object? sender, EventArgs e)
        {
            RedrawGrid();
        }

        private void RedrawGrid()
        {
            if (_viewModel == null || _viewModel.SelectedTemplate == null) return;

            PaperCanvas.Children.Clear();

            var template = _viewModel.SelectedTemplate;
            double canvasWidth = PaperCanvas.Width;
            double canvasHeight = canvasWidth * ((double)template.PaperHeightMm / (double)template.PaperWidthMm);
            PaperCanvas.Height = canvasHeight;

            double scale = canvasWidth / (double)template.PaperWidthMm;

            var borderRect = new Rectangle
            {
                Width = canvasWidth,
                Height = canvasHeight,
                Stroke = Brushes.LightGray,
                StrokeThickness = 1,
                Fill = Brushes.White
            };
            PaperCanvas.Children.Add(borderRect);

            for (int r = 1; r <= template.Rows; r++)
            {
                for (int c = 1; c <= template.Columns; c++)
                {
                    double xMm = (double)template.MarginLeftMm + (c - 1) * ((double)template.LabelWidthMm + (double)template.HorizontalGapMm);
                    double yMm = (double)template.MarginTopMm + (r - 1) * ((double)template.LabelHeightMm + (double)template.VerticalGapMm);

                    double xPx = xMm * scale;
                    double yPx = yMm * scale;
                    double wPx = (double)template.LabelWidthMm * scale;
                    double hPx = (double)template.LabelHeightMm * scale;

                    if (xPx + wPx > canvasWidth || yPx + hPx > canvasHeight) continue;

                    var cellRect = new Rectangle
                    {
                        Width = wPx,
                        Height = hPx,
                        Stroke = Brushes.DodgerBlue,
                        StrokeThickness = 0.5,
                        Cursor = Cursors.Hand
                    };

                    if (r == _viewModel.StartRow && c == _viewModel.StartColumn)
                    {
                        cellRect.Fill = new SolidColorBrush(Color.FromArgb(50, 30, 144, 255));
                        cellRect.Stroke = Brushes.DodgerBlue;
                        cellRect.StrokeThickness = 1.5;

                        if (_viewModel.QrImagePreview != null)
                        {
                            double miniQrSize = Math.Min(wPx, hPx) * 0.8;
                            var qrImg = new Image
                            {
                                Source = _viewModel.QrImagePreview,
                                Width = miniQrSize,
                                Height = miniQrSize
                            };
                            Canvas.SetLeft(qrImg, xPx + (wPx - miniQrSize) / 2);
                            Canvas.SetTop(qrImg, yPx + (hPx - miniQrSize) / 2);
                            PaperCanvas.Children.Add(qrImg);
                        }
                    }
                    else if (r < _viewModel.StartRow || (r == _viewModel.StartRow && c < _viewModel.StartColumn))
                    {
                        cellRect.Fill = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                        cellRect.Stroke = Brushes.Silver;
                    }
                    else
                    {
                        cellRect.Fill = Brushes.Transparent;
                    }

                    int targetRow = r;
                    int targetCol = c;
                    cellRect.MouseLeftButtonDown += (s, e) =>
                    {
                        _viewModel.StartRow = targetRow;
                        _viewModel.StartColumn = targetCol;
                    };

                    Canvas.SetLeft(cellRect, xPx);
                    Canvas.SetTop(cellRect, yPx);
                    PaperCanvas.Children.Add(cellRect);
                }
            }
        }

        private void NewTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new PaperTemplateDialog();
            dialog.Owner = this;
            dialog.ShowDialog();

            if (_viewModel != null)
            {
                Task.Run(async () =>
                {
                    await _viewModel.LoadDataAsync(_calibrationRecordId);
                    Dispatcher.Invoke(() => RedrawGrid());
                });
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
