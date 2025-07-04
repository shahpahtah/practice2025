using Microsoft.WindowsAPICodePack.Dialogs;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Globalization;
using OxyPlot.Legends;

namespace TelemetryViewer
{
    public partial class MainWindow : Window
    {
        private List<TelemetryFile> _loadedFiles = new List<TelemetryFile>();

        public MainWindow()
        {
            InitializeComponent();
            StatusText.Text = "Выберите папку с данными для анализа";
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Выберите папку с телеметрией"
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                LoadFiles(dialog.FileName);
            }
        }

        private void LoadFiles(string folderPath)
        {
            try
            {
                _loadedFiles.Clear();
                var allUniqueColumns = new HashSet<string>();

                StatusText.Text = "Загрузка данных...";

                foreach (var filePath in Directory.GetFiles(folderPath, "*.txt"))
                {
                    try
                    {
                        var file = new TelemetryFile(filePath);
                        _loadedFiles.Add(file);

                        foreach (var col in file.Columns)
                        {
                            if (!col.Equals("Time", StringComparison.OrdinalIgnoreCase))
                            {
                                allUniqueColumns.Add(col);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusText.Text = $"Ошибка в файле {Path.GetFileName(filePath)}: {ex.Message}";
                    }
                }

                YColumnSelector.ItemsSource = allUniqueColumns.OrderBy(c => c).ToList();
                StatusText.Text = $"Загружено {_loadedFiles.Count} файлов, {allUniqueColumns.Count} столбцов";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка загрузки: {ex.Message}";
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PlotGraph_Click(object sender, RoutedEventArgs e)
        {
            if (YColumnSelector.SelectedItem == null || !_loadedFiles.Any())
            {
                StatusText.Text = "Ошибка: не выбран столбец или нет загруженных файлов";
                MessageBox.Show("Сначала выберите столбец для оси Y и загрузите файлы!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var selectedColumn = YColumnSelector.SelectedItem.ToString();
                var plotModel = new PlotModel
                {
                    Title = $"{selectedColumn} vs Time",
                    TitleFontSize = 14,
                    Subtitle = $"Всего файлов: {_loadedFiles.Count}",
                    SubtitleFontSize = 10,
                    PlotAreaBorderColor = OxyColors.LightGray
                };

                // Настройка легенды
                plotModel.Legends.Add(new Legend
                {
                    LegendPosition = LegendPosition.RightTop,
                    LegendPlacement = LegendPlacement.Outside
                });

                // Оси
                plotModel.Axes.Add(new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    Title = "Время (сек)",
                    MajorGridlineStyle = LineStyle.Dot,
                    MajorGridlineColor = OxyColors.LightGray
                });

                plotModel.Axes.Add(new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Title = selectedColumn,
                    MajorGridlineStyle = LineStyle.Dot,
                    MajorGridlineColor = OxyColors.LightGray
                });

                // Цвета для линий
                var colors = new[]
                {
                    OxyColors.Blue, OxyColors.Red, OxyColors.Green,
                    OxyColors.Orange, OxyColors.Purple, OxyColors.Teal
                };

                int colorIndex = 0;
                foreach (var file in _loadedFiles)
                {
                    if (!file.Columns.Contains("Time") || !file.Columns.Contains(selectedColumn))
                        continue;

                    var series = new LineSeries
                    {
                        Title = Path.GetFileNameWithoutExtension(file.FilePath),
                        Color = colors[colorIndex % colors.Length],
                        StrokeThickness = 2,
                        MarkerType = MarkerType.Circle,
                        MarkerSize = 3
                    };

                    var timeData = file.GetColumnData("Time")
                        .Select(t => ParseDouble(t))
                        .Where(t => !double.IsNaN(t))
                        .ToArray();

                    var yData = file.GetColumnData(selectedColumn)
                        .Select(y => ParseDouble(y))
                        .Where(y => !double.IsNaN(y))
                        .ToArray();

                    int pointCount = Math.Min(timeData.Length, yData.Length);
                    for (int i = 0; i < pointCount; i++)
                    {
                        series.Points.Add(new DataPoint(timeData[i], yData[i]));
                    }

                    if (pointCount > 0)
                    {
                        plotModel.Series.Add(series);
                        colorIndex++;
                    }
                }

                PlotView.Model = plotModel;
                StatusText.Text = $"Построен график {selectedColumn} ({plotModel.Series.Count} файлов)";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка построения графика";
                MessageBox.Show($"Ошибка при построении графика: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private double ParseDouble(string value)
        {
            return double.TryParse(value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
                ? result
                : double.NaN;
        }

        public class TelemetryFile
        {
            public string FilePath { get; }
            public List<string> Columns { get; set; } = new List<string>();
            public List<string[]> Rows { get; } = new List<string[]>();

            public TelemetryFile(string filePath)
            {
                FilePath = filePath;
                ParseFile();
            }

            private void ParseFile()
            {
                var lines = File.ReadAllLines(FilePath);
                if (lines.Length == 0) return;

                // Автоопределение разделителя
                var separator = DetectSeparator(lines[0]);
                Columns = lines[0].Split(separator).Select(c => c.Trim()).ToList();

                for (int i = 1; i < lines.Length; i++)
                {
                    var values = lines[i].Split(separator).Select(v => v.Trim()).ToArray();
                    if (values.Length == Columns.Count)
                    {
                        Rows.Add(values);
                    }
                }
            }

            private char DetectSeparator(string headerLine)
            {
                if (headerLine.Contains('\t')) return '\t';
                if (headerLine.Contains(';')) return ';';
                return ',';
            }

            public List<string> GetColumnData(string columnName)
            {
                int index = Columns.IndexOf(columnName);
                return index == -1 ? new List<string>() : Rows.Select(r => r[index]).ToList();
            }
        }
    }
}