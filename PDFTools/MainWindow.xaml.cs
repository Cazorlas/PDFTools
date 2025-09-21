using Microsoft.Win32;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;


namespace PDFTools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private enum AppView { Dashboard, Merge, Split, Compress }
        private enum SplitOption { Individual, Range }
        private enum CompressionLevel { High, Medium, Low }

        public ObservableCollection<string> FilesToMerge { get; set; } = new ObservableCollection<string>();

        private string _fileToSplit;
        private string _fileToCompress;
        private string _splitPageRange = "";
        public string FileToSplit
        {
            get => _fileToSplit;
            set { _fileToSplit = value; OnPropertyChanged(nameof(FileToSplit)); }
        }
        public string SplitPageRange
        {
            get => _splitPageRange;
            set { _splitPageRange = value; OnPropertyChanged(nameof(SplitPageRange)); }
        }
        public string FileToCompress
        {
            get => _fileToCompress;
            set { _fileToCompress = value; OnPropertyChanged(nameof(FileToCompress)); OnPropertyChanged(nameof(FileToCompressDisplayName)); }
        }

        // Properties to display just the filename in the UI
        public string FileToSplitDisplayName => string.IsNullOrEmpty(FileToSplit) ? "Click or drag and drop a PDF file here" : Path.GetFileName(FileToSplit);
        public string FileToCompressDisplayName => string.IsNullOrEmpty(FileToCompress) ? "Click or drag and drop a PDF file here" : Path.GetFileName(FileToCompress);

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            // Start at the dashboard view
            SwitchView(AppView.Dashboard);
        }

        private void SwitchView(AppView view)
        {
            DashboardView.Visibility = Visibility.Collapsed;
            MergeView.Visibility = Visibility.Collapsed;
            SplitView.Visibility = Visibility.Collapsed;
            CompressView.Visibility = Visibility.Collapsed;

            switch (view)
            {
                case AppView.Dashboard:
                    DashboardView.Visibility = Visibility.Visible;
                    break;
                case AppView.Merge:
                    MergeView.Visibility = Visibility.Visible;
                    break;
                case AppView.Split:
                    SplitView.Visibility = Visibility.Visible;
                    break;
                case AppView.Compress:
                    CompressView.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void GoToDashboard_Click(object sender, RoutedEventArgs e) => SwitchView(AppView.Dashboard);
        private void GoToMerge_Click(object sender, RoutedEventArgs e) => SwitchView(AppView.Merge);
        private void GoToSplit_Click(object sender, RoutedEventArgs e) => SwitchView(AppView.Split);
        private void GoToCompress_Click(object sender, RoutedEventArgs e) => SwitchView(AppView.Compress);

        private void FileDrop_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }
        private void MergeFile_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var pdfFiles = files.Where(f => Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase));
                foreach (string file in pdfFiles)
                {
                    if (!FilesToMerge.Contains(file))
                    {
                        FilesToMerge.Add(file);
                    }
                }
            }
        }
        private void SplitFile_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var firstPdf = files.FirstOrDefault(f => Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase));
                if (firstPdf != null)
                {
                    FileToSplit = firstPdf;
                }
            }
        }
        private void CompressFile_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var firstPdf = files.FirstOrDefault(f => Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase));
                if (firstPdf != null)
                {
                    FileToCompress = firstPdf;
                }
            }
        }
        private void SelectMergeFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "PDF Files (*.pdf)|*.pdf",
                Title = "Select PDF Files to Merge"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string filename in openFileDialog.FileNames)
                {
                    if (!FilesToMerge.Contains(filename))
                    {
                        FilesToMerge.Add(filename);
                    }
                }
            }
        }
        private void ClearMergeList_Click(object sender, RoutedEventArgs e)
        {
            FilesToMerge.Clear();
        }
        private void MergeFiles_Click(object sender, RoutedEventArgs e)
        {
            if (FilesToMerge.Count < 2)
            {
                MessageBox.Show("Please select at least two PDF files to merge.", "Not Enough Files", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF File (*.pdf)|*.pdf",
                Title = "Save Merged PDF",
                FileName = "merged.pdf"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (PdfDocument outputDocument = new PdfDocument())
                    {
                        foreach (string file in FilesToMerge)
                        {
                            using (PdfDocument inputDocument = PdfReader.Open(file, PdfDocumentOpenMode.Import))
                            {
                                for (int i = 0; i < inputDocument.PageCount; i++)
                                {
                                    outputDocument.AddPage(inputDocument.Pages[i]);
                                }
                            }
                        }
                        outputDocument.Save(saveFileDialog.FileName);
                    }
                    MessageBox.Show($"Successfully merged {FilesToMerge.Count} files!", "Merge Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    Process.Start(new ProcessStartInfo(saveFileDialog.FileName) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred during merging: {ex.Message}", "Merge Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        

        


        private void SelectSplitFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "PDF Files (*.pdf)|*.pdf", Title = "Select PDF File to Split" };
            if (openFileDialog.ShowDialog() == true)
            {
                FileToSplit = openFileDialog.FileName;
            }
        }

        private void ClearSplitFile_Click(object sender, RoutedEventArgs e)
        {
            FileToSplit = null;
            SplitPageRange = "";
        }
        private void SplitFile_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(FileToSplit))
            {
                MessageBox.Show("Please select a PDF file to split.", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SplitOption selectedOption = (SplitIndividualPagesRadio.IsChecked == true) ? SplitOption.Individual : SplitOption.Range;

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF File (*.pdf)|*.pdf",
                Title = "Select Output Location",
                FileName = $"{Path.GetFileNameWithoutExtension(FileToSplit)}_split.pdf"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string outputDirectory = Path.GetDirectoryName(saveFileDialog.FileName);
                string baseFileName = Path.GetFileNameWithoutExtension(saveFileDialog.FileName);

                try
                {
                    using (PdfDocument inputDocument = PdfReader.Open(FileToSplit, PdfDocumentOpenMode.Import))
                    {
                        if (selectedOption == SplitOption.Individual)
                        {
                            for (int i = 0; i < inputDocument.PageCount; i++)
                            {
                                using (PdfDocument outputDocument = new PdfDocument())
                                {
                                    outputDocument.AddPage(inputDocument.Pages[i]);
                                    string outputFileName = Path.Combine(outputDirectory, $"{baseFileName}_page_{i + 1}.pdf");
                                    outputDocument.Save(outputFileName);
                                }
                            }
                            MessageBox.Show($"Successfully split the file into {inputDocument.PageCount} pages.", "Split Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else // Split by range
                        {
                            List<int> pagesToExtract = ParsePageRange(SplitPageRange, inputDocument.PageCount);
                            if (pagesToExtract == null || pagesToExtract.Count == 0)
                            {
                                MessageBox.Show("Invalid page range specified. Please use formats like '1-3', '5', '1,3,5-7'.", "Invalid Range", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }

                            using (PdfDocument outputDocument = new PdfDocument())
                            {
                                foreach (int pageIndex in pagesToExtract)
                                {
                                    outputDocument.AddPage(inputDocument.Pages[pageIndex]);
                                }
                                outputDocument.Save(saveFileDialog.FileName);
                            }
                            MessageBox.Show($"Successfully extracted {pagesToExtract.Count} pages.", "Split Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        }

                        // Open the output directory
                        Process.Start(new ProcessStartInfo(outputDirectory) { UseShellExecute = true });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred during splitting: {ex.Message}", "Split Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        

        private List<int> ParsePageRange(string range, int maxPage)
        {
            var pageNumbers = new SortedSet<int>();
            if (string.IsNullOrWhiteSpace(range)) return null;

            try
            {
                foreach (var part in range.Split(','))
                {
                    if (part.Contains('-'))
                    {
                        var limits = part.Split('-');
                        int start = int.Parse(limits[0]);
                        int end = int.Parse(limits[1]);
                        for (int i = start; i <= end; i++)
                        {
                            if (i > 0 && i <= maxPage) pageNumbers.Add(i - 1); // PDF pages are 0-indexed
                        }
                    }
                    else
                    {
                        int page = int.Parse(part);
                        if (page > 0 && page <= maxPage) pageNumbers.Add(page - 1); // PDF pages are 0-indexed
                    }
                }
            }
            catch { return null; /* Invalid format */ }
            return pageNumbers.ToList();
        }


        private void SelectCompressFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "PDF Files (*.pdf)|*.pdf", Title = "Select PDF File to Compress" };
            if (openFileDialog.ShowDialog() == true)
            {
                FileToCompress = openFileDialog.FileName;
            }
        }

        private void ClearCompressFile_Click(object sender, RoutedEventArgs e)
        {
            FileToCompress = null;
        }

        private void CompressFile_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(FileToCompress))
            {
                MessageBox.Show("Please select a PDF file to compress.", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF File (*.pdf)|*.pdf",
                Title = "Save Compressed PDF",
                FileName = $"{Path.GetFileNameWithoutExtension(FileToCompress)}_compressed.pdf"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    long originalSize = new FileInfo(FileToCompress).Length;

                    // Open the document in Modify mode so it can be saved after changing options
                    using (PdfDocument document = PdfReader.Open(FileToCompress, PdfDocumentOpenMode.Modify))
                    {
                        // The primary way PDFsharp compresses is by optimizing the file structure.
                        // The options are limited compared to dedicated compression software.
                        // We will set the best options available.
                        var options = document.Options;
                        options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
                        options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Always;
                        options.CompressContentStreams = true;
                        options.NoCompression = false;
                        options.EnableCcittCompressionForBilevelImages = true;


                        // NOTE: Grayscale conversion is a very complex process involving image manipulation
                        // within the PDF structure, which is beyond PDFsharp's standard capabilities.
                        // This UI option is for demonstration; a more powerful library like IronPDF or Aspose.PDF would be needed.
                        if (ConvertToGrayscaleCheck.IsChecked == true)
                        {
                            // Placeholder for future implementation with a different library.
                        }

                        document.Save(saveFileDialog.FileName);
                    }

                    long newSize = new FileInfo(saveFileDialog.FileName).Length;
                    double reduction = 100 - ((double)newSize / originalSize * 100);
                    MessageBox.Show($"Compression complete! File size reduced by {reduction:F2}%.", "Compression Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    Process.Start(new ProcessStartInfo(saveFileDialog.FileName) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred during compression: {ex.Message}", "Compression Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        
    }
}
