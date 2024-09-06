using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BatteRay
{
    public partial class MainWindow : Window
    {
        private BatteryDbContext _context;
        private string selectedFolderPath;
        private Dictionary<string, int> matches = new Dictionary<string, int>();

        public MainWindow()
        {
            InitializeComponent();
            _context = new BatteryDbContext();
            InitializeDatabase();
            LoadBatteryTypes(); // Загрузка данных при запуске приложения
        }

        public class BatteryDbContext : DbContext
        {

            public DbSet<BatteryType> BatteryTypes { get; set; }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseSqlite("Data Source=BatteRayDB.db;");
                //optionsBuilder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=BatteRayDB;Trusted_Connection=True;");
            }
        }

        private void LoadBatteryTypesButton_Click(object sender, RoutedEventArgs e)
        {
            LoadBatteryTypes(); // Вызываем метод для загрузки данных
        }

        private void InitializeDatabase()
        {
            _context.Database.EnsureCreated();
        }
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.Text = string.Empty;
                textBox.GotFocus -= TextBox_GotFocus; // Удаляем обработчик после первого вызова, чтобы не очищать текст повторно
            }
        }
        private void AddBatteryTypeButton_Click(object sender, RoutedEventArgs e)
        {
            var name = batteryTypeInput.Text.Trim();
            var massInput = batteryMassInput.Text.Trim();

            if (!string.IsNullOrEmpty(name) && int.TryParse(massInput, out int mass))
            {
                var batteryType = new BatteryType { Name = name, Mass = mass, Identifier = name }; // Identifier устанавливается автоматически
                _context.BatteryTypes.Add(batteryType);
                _context.SaveChanges();

                LoadBatteryTypes();
            }
            else
            {
                MessageBox.Show("Введите корректные данные.");
            }
        }

        private void LoadBatteryTypes()
        {
            batteryTypesListBox.ItemsSource = _context.BatteryTypes.ToList();
        }
        private void LoadFromFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt",
                Title = "Select a file with battery types"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var lines = File.ReadAllLines(openFileDialog.FileName);
                    foreach (var line in lines)
                    {
                        var parts = line.Split(',');
                        if (parts.Length == 2)
                        {
                            string name = parts[0].Trim();
                            if (int.TryParse(parts[1].Trim(), out int mass))
                            {
                                var batteryType = new BatteryType { Name = name, Mass = mass, Identifier = name };
                                _context.BatteryTypes.Add(batteryType);
                            }
                        }
                    }
                    _context.SaveChanges();
                    LoadBatteryTypes();
                    MessageBox.Show("Battery types loaded successfully.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while loading the file: {ex.Message}");
                }
            }
        }
        private void UpdateBatteryTypeButton_Click(object sender, RoutedEventArgs e)
        {
            if (batteryTypesListBox.SelectedItem is BatteryType selectedBatteryType)
            {
                var name = batteryTypeInput.Text.Trim();
                if (int.TryParse(batteryMassInput.Text.Trim(), out int massValue) && !string.IsNullOrEmpty(name))
                {
                    selectedBatteryType.Name = name;
                    selectedBatteryType.Mass = massValue;
                    selectedBatteryType.Identifier = batteryIdentifierInput.Text.Trim(); // Обновление идентификатора
                    _context.SaveChanges();

                    LoadBatteryTypes();
                }
                else
                {
                    MessageBox.Show("Please enter valid battery type and mass.");
                }
            }
            else
            {
                MessageBox.Show("Please select a battery type to update.");
            }
        }

        private void DeleteBatteryTypeButton_Click(object sender, RoutedEventArgs e)
        {
            if (batteryTypesListBox.SelectedItem is BatteryType selectedBatteryType)
            {
                _context.BatteryTypes.Remove(selectedBatteryType);
                _context.SaveChanges();

                LoadBatteryTypes();
            }
            else
            {
                MessageBox.Show("Please select a battery type to delete.");
            }
        }

        private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select a folder", // Prompt text in the file field
                Filter = "Folders|\n" // Filter for folders
            };
            if (dialog.ShowDialog() == true)
            {
                selectedFolderPath = System.IO.Path.GetDirectoryName(dialog.FileName);
                selectedFolderTextBlock.Text = $"Selected Folder: {selectedFolderPath}";
            }
        }

        private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFolderPath) || batteryTypesListBox.ItemsSource == null || !(batteryTypesListBox.ItemsSource is List<BatteryType>))
            {
                MessageBox.Show("Load battery types and select a folder.");
                return;
            }

            var files = Directory.GetFiles(selectedFolderPath, "*.*", SearchOption.AllDirectories);
            matches.Clear();

            // Сортируем по длине имени типа батареи в обратном порядке, чтобы сначала проверять более длинные имена
            var batteryTypes = ((List<BatteryType>)batteryTypesListBox.ItemsSource)
                .OrderByDescending(bt => bt.Name.Length)
                .ToList();

            foreach (var file in files)
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(file); // Получаем имя файла без расширения

                foreach (var batteryType in batteryTypes)
                {
                    // Проверяем, если имя файла начинается с имени батареи и заканчивается на "_full"
                    if (fileName.StartsWith(batteryType.Name, StringComparison.OrdinalIgnoreCase) && fileName.EndsWith("_full", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!matches.ContainsKey(batteryType.Name))
                        {
                            matches[batteryType.Name] = 0;
                        }
                        matches[batteryType.Name]++;
                        break; // Прерываем цикл, чтобы избежать двойных совпадений
                    }
                }
            }

            DisplayResults(matches);
        }


        //private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        //{
        //    if (string.IsNullOrEmpty(selectedFolderPath) || batteryTypesListBox.ItemsSource == null || !(batteryTypesListBox.ItemsSource is List<BatteryType>))
        //    {
        //        MessageBox.Show("Load battery types and select a folder.");
        //        return;
        //    }

        //    var files = Directory.GetFiles(selectedFolderPath, "*.*", SearchOption.AllDirectories);
        //    matches.Clear();

        //    // Сортируем по длине имени типа батареи в обратном порядке, чтобы сначала проверять более длинные имена
        //    var batteryTypes = ((List<BatteryType>)batteryTypesListBox.ItemsSource)
        //        .OrderByDescending(bt => bt.Name.Length)
        //        .ToList();

        //    foreach (var file in files)
        //    {
        //        var fileName = System.IO.Path.GetFileNameWithoutExtension(file); // Получаем имя файла без расширения

        //        foreach (var batteryType in batteryTypes)
        //        {
        //            // Проверяем, начинается ли имя файла с имени типа батареи и заканчивается ли на "_full"
        //            if (fileName.StartsWith(batteryType.Name, StringComparison.OrdinalIgnoreCase) &&
        //                fileName.EndsWith("_full", StringComparison.OrdinalIgnoreCase))
        //            {
        //                if (!matches.ContainsKey(batteryType.Name))
        //                {
        //                    matches[batteryType.Name] = 0;
        //                }
        //                matches[batteryType.Name]++;
        //                break; // Прерываем цикл, чтобы избежать двойных совпадений
        //            }
        //        }
        //    }

        //    DisplayResults(matches);
        //}



        private void DisplayResults(Dictionary<string, int> matches)
        {
            double totalMassInGrams = 0;
            int totalCount = 0;
            string displayText = "Результаты анализа:\n";

            // Преобразуем запрос к базе данных в коллекцию в памяти перед выполнением фильтрации
            var notFoundTypes = _context.BatteryTypes
                .AsEnumerable()  // Преобразование в коллекцию в памяти
                .Where(bt => !matches.ContainsKey(bt.Name))
                .Select(bt => bt.Name)
                .ToList();

            foreach (var match in matches)
            {
                var batteryType = _context.BatteryTypes.FirstOrDefault(bt => bt.Name == match.Key);
                if (batteryType != null)
                {
                    double massInGrams = match.Value * batteryType.Mass;
                    totalMassInGrams += massInGrams;
                    totalCount += match.Value;

                    displayText += $"{match.Key}: {match.Value} шт.\n";
                    displayText += $"    Масса: {batteryType.Mass} г\n";
                    displayText += $"    Общая масса: {massInGrams} г\n\n";
                }
            }

            displayText += $"\nИтог:\nОбщее количество: {totalCount} шт.\nОбщая масса: {totalMassInGrams} г ({totalMassInGrams / 1000} кг, {totalMassInGrams / 1_000_000} тонн)";

            if (notFoundTypes.Any())
            {
                displayText += "\n\nНе найдены:\n" + string.Join("\n", notFoundTypes);
            }

            resultTextBlock.Text = displayText;
        }

        private void ExportToExcelButton_Click(object sender, RoutedEventArgs e)
        {
            if (matches.Count == 0)
            {
                MessageBox.Show("Perform an analysis first.");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                Title = "Save Analysis Results"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Analysis Results");

                    // Adding the title and date
                    worksheet.Cell(1, 1).Value = "Battery Analysis Report";
                    worksheet.Cell(1, 1).Style.Font.Bold = true;
                    worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                    worksheet.Cell(1, 5).Value = DateTime.Now.ToString("yyyy-MM-dd");
                    worksheet.Cell(1, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    // Fill column headers
                    worksheet.Cell(3, 1).Value = "Battery Type";
                    worksheet.Cell(3, 2).Value = "Count";
                    worksheet.Cell(3, 3).Value = "Mass (grams)";
                    worksheet.Cell(3, 4).Value = "Mass (kilograms)";
                    worksheet.Cell(3, 5).Value = "Mass (tons)";
                    worksheet.Row(3).Style.Font.Bold = true;

                    int row = 4;
                    foreach (var match in matches)
                    {
                        var batteryType = _context.BatteryTypes.FirstOrDefault(bt => bt.Name == match.Key);
                        if (batteryType != null)
                        {
                            double massInKg = batteryType.MassInKg * match.Value;
                            double massInTons = massInKg / 1000.0;

                            worksheet.Cell(row, 1).Value = batteryType.Name;
                            worksheet.Cell(row, 2).Value = match.Value;
                            worksheet.Cell(row, 3).Value = batteryType.Mass * match.Value;
                            worksheet.Cell(row, 4).Value = massInKg;
                            worksheet.Cell(row, 5).Value = massInTons;

                            row++;
                        }
                    }

                    // Adding total formulas
                    worksheet.Cell(row, 1).Value = "Total";
                    worksheet.Cell(row, 1).Style.Font.Bold = true;
                    worksheet.Cell(row, 2).FormulaA1 = $"SUM(B4:B{row - 1})";
                    worksheet.Cell(row, 3).FormulaA1 = $"SUM(C4:C{row - 1})";
                    worksheet.Cell(row, 4).FormulaA1 = $"SUM(D4:D{row - 1})";
                    worksheet.Cell(row, 5).FormulaA1 = $"SUM(E4:E{row - 1})";

                    // Adjust column widths
                    worksheet.Columns().AdjustToContents();

                    // Save the workbook
                    workbook.SaveAs(saveFileDialog.FileName);
                }

                MessageBox.Show("Analysis results exported to Excel successfully.");
            }
        }

        public class BatteryType
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Mass { get; set; } // Масса в граммах
            public string Identifier { get; set; } // Уникальный идентификатор

            public double MassInKg => Mass / 1000.0; // Масса в килограммах
        }
        private void BatteryTypeInput_GotFocus(object sender, RoutedEventArgs e)
        {
            if (batteryTypeInput.Text == "Введите тип батарейки")
            {
                batteryTypeInput.Text = string.Empty;
                batteryTypeInput.Foreground = new SolidColorBrush(Colors.Black);
            }
        }

        private void BatteryTypeInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(batteryTypeInput.Text))
            {
                batteryTypeInput.Text = "Введите тип батарейки";
                batteryTypeInput.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }
        private void ClearDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            _context.BatteryTypes.RemoveRange(_context.BatteryTypes);
            _context.SaveChanges();
            LoadBatteryTypes(); // Обновить список
            MessageBox.Show("База данных очищена.");
        }

        private void BatteryTypesListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (batteryTypesListBox.SelectedItem is BatteryType selectedBatteryType)
            {
                batteryTypeInput.Text = selectedBatteryType.Name;
                batteryMassInput.Text = selectedBatteryType.Mass.ToString();
                batteryIdentifierInput.Text = selectedBatteryType.Identifier; // Установка идентификатора
            }
        }
    }
}