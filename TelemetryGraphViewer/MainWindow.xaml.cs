using Microsoft.WindowsAPICodePack.Dialogs;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Diagnostics;
using System.Globalization;
using OxyPlot.Legends;

namespace TelemetryViewer
{
    public partial class MainWindow : Window
    {
        private List<TelemetryFile> _loadedFiles = new List<TelemetryFile>();

        public MainWindow()
        {
            InitializeComponent(); // Критически важная строка!
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
            _loadedFiles.Clear();
            var allUniqueColumns = new HashSet<string>(); // Для сбора ВСЕХ столбцов

            try
            {
                foreach (var filePath in Directory.GetFiles(folderPath, "*.txt"))
                {
                    try
                    {
                        var file = new TelemetryFile(filePath);
                        _loadedFiles.Add(file);

                        // Добавляем все столбцы файла в общий набор (кроме Time)
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
                        Debug.WriteLine($"Ошибка в файле {filePath}: {ex.Message}");
                    }
                }

                // Обновляем ComboBox
                YColumnSelector.ItemsSource = allUniqueColumns.OrderBy(c => c).ToList();

                if (YColumnSelector.Items.Count == 0)
                {
                    MessageBox.Show("Не найдено столбцов для отображения!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
            }
        }

        private void PlotGraph_Click(object sender, RoutedEventArgs e)
        {
            if (YColumnSelector.SelectedItem == null || !_loadedFiles.Any())
            {
                MessageBox.Show("Сначала выберите столбец для оси Y и загрузите файлы!");
                return;
            }

            var selectedColumn = YColumnSelector.SelectedItem.ToString();
            var plotModel = new PlotModel
            {
                Title = $"График: {selectedColumn}",
                TitleFontSize = 14,
                TitleFontWeight = OxyPlot.FontWeights.Bold,
                SubtitleFontSize = 10,
                DefaultFont = "Segoe UI",
                PlotAreaBorderColor = OxyColors.LightGray,
                PlotAreaBorderThickness = new OxyThickness(1)
            };

            // Настройка легенды (современный способ)
            plotModel.Legends.Add(new Legend
            {
                LegendTitle = "Источники данных",
                LegendTitleFont = "Segoe UI",
                LegendTitleFontSize = 10,
                LegendFontSize = 10,
                LegendBorder = OxyColors.LightGray,
                LegendBackground = OxyColor.FromArgb(200, 255, 255, 255),
                LegendPosition = LegendPosition.RightTop,
                LegendPlacement = LegendPlacement.Outside
            });

            // Настройка оси X (Time)
            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Время, сек",
                TitleFontSize = 12,
                AxislineColor = OxyColors.Black,
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.LightGray,
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = OxyColors.LightGray,
                TicklineColor = OxyColors.Black,
                MinimumPadding = 0.05,
                MaximumPadding = 0.05
            });

            // Настройка оси Y
            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = selectedColumn,
                TitleFontSize = 12,
                AxislineColor = OxyColors.Black,
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.LightGray,
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = OxyColors.LightGray,
                TicklineColor = OxyColors.Black,
                MinimumPadding = 0.1,
                MaximumPadding = 0.1
            });

            // Палитра цветов для линий
            var colors = new[]
            {
        OxyColors.Blue,
        OxyColors.Red,
        OxyColors.Green,
        OxyColors.Orange,
        OxyColors.Purple,
        OxyColors.Brown,
        OxyColors.Teal
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
                    MarkerSize = 4,
                    MarkerFill = colors[colorIndex % colors.Length],
                    MarkerStroke = OxyColors.White,
                    MarkerStrokeThickness = 1,
                    LineStyle = LineStyle.Solid
                };

                colorIndex++;

                // Парсинг данных с обработкой разных форматов чисел
                var timeData = file.GetColumnData("Time")
                    .Select(t => double.TryParse(t.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var time)
                        ? time
                        : double.NaN)
                    .Where(t => !double.IsNaN(t))
                    .ToArray();

                var yData = file.GetColumnData(selectedColumn)
                    .Select(y => double.TryParse(y.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
                        ? value
                        : double.NaN)
                    .Where(y => !double.IsNaN(y))
                    .ToArray();

                // Добавление точек (только если есть соответствующие данные)
                int pointCount = Math.Min(timeData.Length, yData.Length);
                if (pointCount > 0)
                {
                    for (int i = 0; i < pointCount; i++)
                    {
                        series.Points.Add(new DataPoint(timeData[i], yData[i]));
                    }
                    plotModel.Series.Add(series);

                    Debug.WriteLine($"Добавлено {pointCount} точек из файла {Path.GetFileName(file.FilePath)}");
                }
            }

            if (plotModel.Series.Count > 0)
            {
                // Автоматическое масштабирование
                plotModel.ResetAllAxes();

                // Обновление графика с анимацией
                PlotView.InvalidatePlot(true);
                PlotView.Model = plotModel;
            }
            else
            {
                MessageBox.Show("Нет данных для построения графика!\nПроверьте содержимое выбранных файлов.");
            }
        }
        public class TelemetryFile
        {
            public string FilePath { get; }
            public List<string> Columns { get; private set; } = new List<string>();
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

                // Определяем разделитель автоматически
                var separator = lines[0].Contains('\t') ? '\t' :
                              lines[0].Contains(';') ? ';' : ',';

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

            public List<string> GetColumnData(string columnName)
            {
                int index = Columns.IndexOf(columnName);
                return index == -1 ? new List<string>() : Rows.Select(r => r[index]).ToList();
            }
        }
    }
}