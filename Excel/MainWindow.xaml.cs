using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Vml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Formats.Tar;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Printing;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static System.Net.Mime.MediaTypeNames;
using Application = System.Windows.Application;
using Image = System.Windows.Controls.Image;

namespace Wiring
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public Data myData = new Data();
        private int _BusListIndex = 0;
        private Wire _wire = new Wire();
        private int _findedCabinetIndex = 0;
        public static MainWindow MyWindow { get; private set; }
        private static List<string> ListOfNames = new List<string>();

        private DispatcherTimer timer, timer2;

        private void Timer_Tick2(object sender, EventArgs e)
        {
            UpdateTimer();

        }
        private void UpdateTimer()
        {
            var selectedWire = listView.SelectedItem as Wire;

            if (selectedWire == null)
            {
                return;
            }
            // Do something with the selected wire

            if (selectedWire.WireStatus != (int?)Data.Status.AllConfirmed)
            {
                var timespan = DateTime.Now - selectedWire.Start;
                var seconds = timespan.TotalSeconds;

                var timespanHandling = DateTime.Now - Data.StartHandling;
                var secondsHandling = timespanHandling.TotalSeconds - seconds;


                selectedWire.HandlingTime = Math.Round(secondsHandling, 1);
                selectedWire.Seconds = Math.Round(seconds, 1);
                if (selectedWire.WireStatus == 0)
                    LabelValue = Math.Round(selectedWire.Seconds + secondsHandling, 1);
                else if (selectedWire.WireStatus == 1)
                    LabelValue = Math.Round(selectedWire.Seconds + selectedWire.SecondsSource + secondsHandling, 1);
                else if (selectedWire.WireStatus == 2)
                    LabelValue = Math.Round(selectedWire.Seconds + selectedWire.SecondsDestination + secondsHandling, 1);
            }
            else
                LabelValue = Math.Round(selectedWire.Seconds + selectedWire.SecondsSource + selectedWire.SecondsDestination + selectedWire.HandlingTime, 1);

            //if (LabelValue > selectedWire.TimeForExecuting)
            //    selectedWire.Overtime = true;
            //else
            //    selectedWire.Overtime = false;
            //Overtime = selectedWire.Overtime;
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            MyWindow = this;
            //Application.Current.MainWindow = this;
            
            LoadDataFromExcel(); //pobieranie danych z listy excel
            FileOperations.ReadMemory(ref _findedCabinetIndex, myData.ListOfImportedCabinets, @"memory.txt"); // czytanie danych na temat ostatniej robionej szafy

            listView.ItemsSource = myData.ListOfImportedCabinets[_findedCabinetIndex]; //wyświetlanie danych z listy jako listview



            Dispatcher.Invoke(new Action(() => textBlockSet.Text = $"Set:{Data.SetNumber}")); //wyświetlanie numeru seta

            if (Data.LoggedPerson != null)
            {
                Dispatcher.Invoke(new Action(() => textBlockLogged.Text = $"Zalogowany/a: {Data.LoggedPersonBT}"));
                buttonLogging.Content = "Wyloguj";
                buttonLogging.Visibility = Visibility.Visible;
            }

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1); // Set the delay time here (1 second in this example)
            timer.Tick += Timer_Tick;
            SplitItemsByBus();

            SetBusStatus();
            ContinueProductionAfterStart();
            RefreshList(listView1);
            RefreshList(listView); //odświeżenie wyświerlanych danych na aplikacji

            timer2 = new DispatcherTimer();
            timer2.Interval = TimeSpan.FromMilliseconds(100); // Set the delay time here (1 second in this example)
            timer2.Tick += Timer_Tick2;
            timer2.Start();
            CountSummaryTime(myData.ListOfImportedCabinets[_findedCabinetIndex]);
            Data.StartHandling = DateTime.Now;
            CountProgress();

        }


        private void LoadDataFromExcel() 
        {
            //   string fileName = "\\\\KWIPUBV04\\General$\\Enercon\\Shared\\wiring\\PrzewodyProgramWszystkie.xlsx"; //ścieżka pod którą jest lista excel z której są pobierane dane
            //          string fileName = "\\\\KWIPUBV04\\General$\\Enercon\\Shared\\perforex\\PrzewodyProgramWszystkie.xlsx"; //ścieżka pod którą jest lista excel z której są pobierane dane
            //string fileName = "\\\\KWIPUBV04\\General$\\Enercon\\Shared\\mounting\\Ariel Testy Programu\\PrzewodyProgramWszystkie.xlsx"; //ścieżka pod którą jest lista excel z której są pobierane dane
            //string fileName = "\\\\KWIPUBV04\\General$\\Enercon\\Shared\\mounting\\Piotr\\PrzewodyProgramWszystkie.xlsx"; //ścieżka pod którą jest lista excel z której są pobierane dane
            //string fileName = "\\\\KWIPUBV04\\General$\\Enercon\\Shared\\mounting\\Andrzej\\PrzewodyProgramWszystkie.xlsx"; //ścieżka pod którą jest lista excel z której są pobierane dane
            //string fileName = "C:\\Users\\2281209\\Downloads\\PrzewodyProgramWszystkie.xlsx";
            //    string fileName = "C:\\ener\\mounting\\PrzewodyProgramWszystkie.xlsx";
            //  string fileName = "\\\\KWIPUBV04\\General$\\Enercon\\Shared\\DIN\\SzynyDin.xlsx";

              string fileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "perforex.xlsx");
          //  string fileName = System.IO.Path.Combine(@"D:/PERFOREX_MANUAL", "perforex.xlsx");

            using (var excelWorkbook = new XLWorkbook(fileName)) //otwiera podany plik excel
            {
                //           var nonEmptyDataRows = excelWorkbook.Worksheet(2).RowsUsed();

                myData.ListOfImportedCabinets = new List<List<Wire>>(); //tworzy nową listę szaf w której są listy przewodów do zrobienia

                var counter = 0;
                foreach (var item in excelWorkbook.Worksheets)
                {
                    ListOfNames.Add(item.Name); // lista nazw szaf potrzebna do wyboru szafy poprzez combobox

                    if (!item.Name.Equals("Podsumowanie"))  // nazwa zakładki nie może być: "Podsumowanie"
                    {
                        var nonEmptyDataRows = item.RowsUsed(); //czytamy tylko wiersze które nie są puste
                        myData.ListOfImportedCabinets.Add(new List<Wire>());  //dodajemy nową listę np. szafę xxxx1

                        foreach (var dataRow in nonEmptyDataRows) //iterujemy po każdym wierszu który załadowaliśmy z aplikacji
                        {
                            if (dataRow.RowNumber() >= 3) //zaczyna od 3 wiersza
                            {
                                _wire = new Wire(); //tworzymy nowy przewód i dodajemy do niego atrybuty:

                                _wire.NameOfCabinet = item.Name; //nazwa szafy brana jest z nazwy zakładki
                                _wire.Number = dataRow.Cell(1).Value.GetText(); //czytamy pierwszą kolumnę jako numer itd
                                _wire.Nc = dataRow.Cell(2).Value.GetText();
                                _wire.Torque = dataRow.Cell(3).Value.GetText();
                                _wire.Descriptions = dataRow.Cell(4).Value.GetText();
                                _wire.Bus = dataRow.Cell(5).Value.GetText();
                                _wire.Box = dataRow.Cell(6).Value.GetText();

                                _wire.TimeForExecuting = ParseFromStringToDouble(dataRow.Cell(7).Value.GetText());

                                //  _wire.IsCoordinatesRequired = dataRow.Cell(9).Value.GetText().Trim().Equals("x", StringComparison.OrdinalIgnoreCase);
                                _wire.hostname = dataRow.Cell(9).Value.GetText();
                                _wire.program = dataRow.Cell(10).Value.GetText();
                                _wire.job = dataRow.Cell(11).Value.GetText();
                                if (_wire.hostname.Length > 0 && _wire.program.Length > 0 && _wire.job.Length > 0)
                                    _wire.IsCameraNeeded = true;
                                //_wire.DtSource = dataRow.Cell(4).Value.GetText();
                                //_wire.WireEndDimensionSource = dataRow.Cell(7).Value.GetText();
                                //_wire.WireEndTerminationSource = dataRow.Cell(6).Value.GetText();

                                //_wire.DtTarget = dataRow.Cell(8).Value.GetText();
                                //_wire.WireEndTerminationTarget = dataRow.Cell(10).Value.GetText();
                                //_wire.WireEndDimensionTarget = dataRow.Cell(11).Value.GetText();
                                //_wire.Colour = dataRow.Cell(12).Value.GetText();
                                //_wire.CrossSection = ParseFromStringToDouble(dataRow.Cell(13).Value.GetText());
                                //_wire.Type = dataRow.Cell(14).Value.GetText();
                                //_wire.Lenght = ParseFromStringToDouble(dataRow.Cell(16).Value.GetText());



                                myData.ListOfImportedCabinets[counter].Add(_wire); //finalne dodanie przewodu do listy
                            }
                        }

                        counter++;
                    }
                }
                for (int i = 0; i < myData.ListOfImportedCabinets.Count; i++)
                {
                    myData.ListOfImportedCabinets[i] = myData.ListOfImportedCabinets[i].OrderBy(x => x.Bus).ToList();
                }


            }
            comboBox.ItemsSource = ListOfNames; //kopiowanie nazw szaf do comboboxa żeby były one do wyboru
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private double _labelValue;
        public double LabelValue
        {
            get => _labelValue;
            set
            {
                _labelValue = value;
                OnPropertyChanged(nameof(LabelValue)); // Notify UI of the change
            }
        }
        private bool _overtime;
        public bool Overtime
        {
            get => _overtime;
            set
            {
                if (_overtime != value)
                {
                    _overtime = value;
                    OnPropertyChanged(nameof(Overtime)); // Notify UI of the change
                }
            }
        }
        private double? _totalTime;
        public double? TotalTime
        {
            get => _totalTime;
            set
            {
                _totalTime = value;
                OnPropertyChanged(nameof(TotalTime)); // Notify UI of the change
            }
        }
        private double? _totalExpectedTime;
        public double? TotalExpectedTime
        {
            get => _totalExpectedTime;
            set
            {
                _totalExpectedTime = value;
                OnPropertyChanged(nameof(TotalExpectedTime)); // Notify UI of the change
            }
        }

        private Brush _listViewBackground = Brushes.White;
        public Brush ListViewBackground
        {
            get => _listViewBackground;
            set
            {
                _listViewBackground = value;
                OnPropertyChanged(nameof(ListViewBackground));
            }
        }

        private void CountSummaryTime(List<Wire> list)
        {
            TotalTime = Math.Round(list
                .Where(w => w.WireStatus == 3) // Filtruj po statusie
                .Sum(w => (w.Seconds+w.HandlingTime+w.SecondsDestination+w.SecondsSource)), 1);         // Zsumuj Time

            TotalExpectedTime = Math.Round(list
               .Where(w => w.WireStatus == 3) // Filtruj po statusie
               .Sum(w => w.TimeForExecuting), 1);          // Zsumuj Time
            if (TotalTime > TotalExpectedTime)
            {
                Overtime = true;
                ListViewBackground = Brushes.DarkRed;
            }
            else
            {
                Overtime = false;
                ListViewBackground = Brushes.LawnGreen;
            }

                
            
        }


        private void Timer_Tick(object sender, EventArgs e)
        {
            //expander.IsExpanded = false; // Hide the ListView when the timer ticks
            timer.Stop(); // Stop the timer after hiding
        }
        private void Expander_MouseEnter(object sender, MouseEventArgs e)
        {
          //  listView.Width = 300;
           // expander.IsExpanded = true;
            timer.Stop();
        }

        private void Expander_MouseLeave(object sender, MouseEventArgs e)
        {
          //  listView.Width = 10;
            timer.Start();
           // expander.IsExpanded = false;
        }
        private void expander_GotMouseCapture(object sender, MouseEventArgs e)
        {
          //  listView.Width = 500;
          //  expander.IsExpanded = true;
            timer.Stop();
        }

        public double ParseFromStringToDouble(string stringToParse)
        {
            if (stringToParse.Contains("m"))// czasami potrafiło się pojawić m w listach (podawanie długości w metrach zamiast mm)
                stringToParse = stringToParse.Substring(0, stringToParse.Length - 3); // usuwanie wtedy 3 ostatnich liter -> mm2
            double result;
            if (Double.TryParse(stringToParse, out result)) //parsowanie danych na double 
                return result;
            else return 0.0;  //jeśli się nie uda to zwraca 0.0
        }

        public void ClearAllConfirms() //czyści listę i ładuje na nowo z danych z excela
        {
            myData.ListOfImportedCabinets.Clear();
            LoadDataFromExcel();
        }


        private void ChooseImage(int index, Image image, int PictureNumber) //wyświetlanie konkretnego zdjęcia w podanej kontrolce Image
        {
            var selectedWire = listView.SelectedItem as Wire;

            if (selectedWire == null)
                return;

            //var folderCabinetName = myData.ListOfImportedCabinets[_findedCabinetIndex][index].NameOfCabinet;
            //var folderWireName = myData.ListOfImportedCabinets[_findedCabinetIndex][index].Number;
            var folderCabinetName = selectedWire.NameOfCabinet;
            var folderWireName = selectedWire.Number;


            var nameOfImage = @$"\{folderCabinetName}\{folderWireName}\{PictureNumber}.png";

            if(File.Exists(AppDomain.CurrentDomain.BaseDirectory
                            + nameOfImage))
            {
                try
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => {
                            image.Source = new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory
                            + nameOfImage, UriKind.Absolute));
                        }));
                }
                catch (Exception)
                {
                    ;
                }
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => {
                    image.Source = null;
                }));
            }


        }



        //public void HideImages() nieużywane ale zostawię bo można użyć do chowania wyświetlanych zdjęć na które się kliknie
        //{
        //    foreach (Window item in App.Current.Windows)
        //    {
        //        if (item != this)
        //        {
        //            Dispatcher.Invoke(new Action(() => item.Close()));

        //        }
        //    }
        //}

        private void comboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) //odpalane przy wyborze nowej safy poprzez combobox
        {
            if (comboBox.SelectedIndex != -1)
            {
                Window1 subWindow = new Window1();
                subWindow.ShowDialog();

                if (Data.SetNumber == null)
                {
                    MessageBox.Show("Nie podano numeru seta!");
                    return;
                }


                Dispatcher.Invoke(new Action(() => textBlockSet.Text = $"Set:{Data.SetNumber}"));
                //   string inputRead = new InputBox("Insert something", "Title", "Arial", 20).ShowDialog();

                ClearAllConfirms();
                Dispatcher.Invoke(new Action(() => labelPotwierdzonoWszystkiePrzewody.Visibility = Visibility.Hidden));

                _findedCabinetIndex = comboBox.SelectedIndex;

                var name = myData.ListOfImportedCabinets[_findedCabinetIndex][0].NameOfCabinet;

                if (File.Exists($@"{name}_{Data.SetNumber}")) //sprawdzanie czy już dana szafa była robiona ->jeśli była to ładuje dane na temat potwierdzonych przewodów
                {
                    FileOperations.ReadMemory(ref _findedCabinetIndex, myData.ListOfImportedCabinets, $@"{name}_{Data.SetNumber}"); // dane są zapisywane w pliku nazwaszafy_numerseta
                }
                else
                {
                 //   myData.ListOfImportedCabinets[_findedCabinetIndex][1].IsConfirmed = true; 
                 //   listView.ItemsSource = myData.ListOfImportedCabinets[_findedCabinetIndex];
                }
                myData.ListOfImportedCabinets[_findedCabinetIndex][1].IsConfirmed = true;

                    listView.ItemsSource = myData.ListOfImportedCabinets[_findedCabinetIndex]; // ładuje nową szafę do listview

                SplitItemsByBus();
                SetBusStatus();
                ContinueProductionAfterStart();
                RefreshList(listView); //odświeżanie widoku aplikcaji
                RefreshList(listView1);
                Data.StartHandling = DateTime.Now;
            }
        }

        private void SetBusStatus()
        {
            for (int i = 0; i < BusList.Count; i++)
            {
                if (myData.ListOfImportedCabinets[_findedCabinetIndex].Where(x => BusList[i].Bus == x.Bus).All(x => x.WireStatus == 3))
                    BusList[i].WireStatus = 3;
                else if (myData.ListOfImportedCabinets[_findedCabinetIndex].Where(x => BusList[i].Bus == x.Bus).Any(x => x.WireStatus == 3))
                {
                    BusList[i].WireStatus = 1;
                }
                else
                    BusList[i].WireStatus = 0;
            }

        }
        private void ContinueProductionAfterStart()
        {
            BusList.OrderBy(x => x.Number)   // Sorting by 'Number' in ascending order
                        .ToList();                // Convert it back to a List (if needed)
            foreach (var item in BusList)
            {
                if (item.WireStatus == 1)
                {
                    listView1.SelectedItem = item; //wybieranie na który element ma być w tej chwili zaznaczony
                    break;
                }
            }
            FocusListViewOnFirstElement();
            _BusListIndex = listView1.SelectedIndex;
        }
        private void FocusListViewOnFirstElement()
        {
            listView.SelectedItem = 0;
            // Assuming you have access to the ListView's data source (for example, a List or ObservableCollection)
            var itemsSource = new List<Wire>();
            foreach (Wire item in listView.ItemsSource)
            {
                itemsSource.Add(item);
            }
            if (itemsSource != null)
            {
                // Sort the list based on 'Number' (ascending order)
              //  var sortedList = itemsSource.OrderByDescending(x => x.Number).ToList();

                // Iterate through the sorted list to find the first item with WireStatus == 0
                foreach (var item in itemsSource)
                {
                    if (item.WireStatus == 0)
                    {
                        listView.SelectedItem = item; // Set this item as selected in the ListView
                        break; // Stop the loop once we find the first match
                    }
                }
            }
        }

        private List<Wire> BusList { get; set; }
        private void SplitItemsByBus()
        {
            var bubba = myData.ListOfImportedCabinets[_findedCabinetIndex].DistinctBy(x => x.Bus);
            BusList = new List<Wire>();
            foreach (var wire in bubba)
            {
                BusList.Add(new Wire() { Bus = wire.Bus, WireStatus = 2 }); // Use copy constructor
            }
            listView1.ItemsSource = BusList;
            MoveDownSelectedItemFromList(listView1);
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            var item = listView.SelectedItem;
            if (item != null)
            {
                // MessageBox.Show(item.ToString());
            }
            else
                return;

            int index = listView.Items.IndexOf(item);

            var hux = myData.ListOfImportedCabinets[_findedCabinetIndex][index].IsConfirmed = true;

            MoveDownSelectedItemFromList(listView);
            listView.Items.Refresh();

                var allValid = myData.ListOfImportedCabinets[_findedCabinetIndex].Any() && myData.ListOfImportedCabinets[_findedCabinetIndex].All(item => item.IsConfirmed);
                
                if(allValid)
                {
                  Dispatcher.Invoke(new Action(() => labelPotwierdzonoWszystkiePrzewody.Visibility = Visibility.Visible));
                }
                else
                    Dispatcher.Invoke(new Action(() => labelPotwierdzonoWszystkiePrzewody.Visibility = Visibility.Hidden));


        }

        private void MoveDownSelectedItemFromList(ListView listView)
        {
            if (listView.SelectedIndex < listView.Items.Count - 1)
            {
                listView.SelectedIndex = listView.SelectedIndex + 1;
            }
        }

        private void RefreshList(ListView listView)
        {
            if (listView.SelectedIndex < listView.Items.Count - 1)
            {
                listView.SelectedIndex = listView.SelectedIndex + 1;
                listView.SelectedIndex = listView.SelectedIndex - 1;
            }
            else
            {
                listView.SelectedIndex = listView.SelectedIndex - 1;
                listView.SelectedIndex = listView.SelectedIndex + 1;
            }
        }

        private void listView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //var item = listView.SelectedItem; //sprawdzanie czy mamy jakieś przewody do zatwierdzenia
            //if (item != null)
            //{
            //    // MessageBox.Show(item.ToString());
            //}
            //else
            //    return;

            //int index = listView.Items.IndexOf(item);
            var selectedWire = listView.SelectedItem as Wire;

            if (selectedWire != null)
            {
                selectedWire.Start = DateTime.Now;
                // Do something with the selected wire
                // MessageBox.Show($"Selected Wire: {bus}, {number}");
            }


            //myData.ListOfImportedCabinets[_findedCabinetIndex][index].Start = DateTime.Now; //sprawdzanie statusu wykonania przewodu
            //////////var item = (sender as ListView).SelectedItem;
            //////////if (item != null)
            //////////{
            //////////   // MessageBox.Show(item.ToString());
            //////////}
            //////////else
            //////////    return;

            //////////int index = listView.Items.IndexOf(item);

            //////////var hux = myData.ListOfImportedCabinets[_findedCabinetIndex][index].IsConfirmed = true;
            //////////listView.Items.Refresh();

            //           ShowImage(_findedCabinetIndex, index);

            //foreach (var itemf in myData.ListOfImportedCabinets)
            //{

            //}

        }


        private void TextBlock_TargetUpdated(object sender, DataTransferEventArgs e) //wykorzystywane do wyświetlania obrazków
        {
            var selectedWire = listView.SelectedItem as Wire;

            if (selectedWire != null)
            {
                int wireNumber;
                bool isParsed = int.TryParse(selectedWire.Number, out wireNumber);
                if (isParsed)
                {
                    ChooseImage(wireNumber, image_All, 1);
                }


            }
                
        }

        private void image_Source_GotMouseCapture(object sender, MouseEventArgs e) //po kliknięcu na dany obrazek odpala się zdjęcie, które kliknęlismy w windowsowym edytorze zdjęć
        {
            Image image = (Image)sender;
            int PictureNumberToShow = 0;
            switch (image.Name)
            {
                case "image_Source":
                    PictureNumberToShow = 2;
                    break;
                case "image_All":
                    PictureNumberToShow = 1;
                    break;
                case "image_Target":
                    PictureNumberToShow = 3;
                    break;
                default:
                    PictureNumberToShow = 1;
                    break;
            }

            var selectedWire = listView.SelectedItem as Wire;

            if (selectedWire == null)
                return;

            //var folderCabinetName = myData.ListOfImportedCabinets[_findedCabinetIndex][index].NameOfCabinet;
            //var folderWireName = myData.ListOfImportedCabinets[_findedCabinetIndex][index].Number;
            var folderCabinetName = selectedWire.NameOfCabinet;
            var folderWireName = selectedWire.Number;

            var nameOfImage = @$"\{folderCabinetName}\{folderWireName}\{PictureNumberToShow}.png";

            var selectedNumber = listView.SelectedIndex;
            if (selectedNumber >= 0)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(@$"{AppDomain.CurrentDomain.BaseDirectory}\{folderCabinetName}\{folderWireName}\{PictureNumberToShow}.png") { UseShellExecute = true });
                }
                catch (Exception)
                {
                    ;
                }
            }
        }

        private void SourceConfirm_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;

            //if(Data.LoggedPerson == null || Data.LoggedPerson.Length < 2)
            //{
            //    MessageBox.Show("Operacja wymaga zalogowania się!");
            //    return;
            //}

            myData.TextVisibility ^= true;

            var selectedWire = listView.SelectedItem as Wire;

            if (selectedWire == null)
            {
                return;
                // Do something with the selected wire
                // MessageBox.Show($"Selected Wire: {bus}, {number}");
            }
            if(selectedWire.IsCoordinatesRequired)
            {
                if (!(selectedWire.X.Length > 0 && selectedWire.Y.Length > 0))
                {
                    MessageBox.Show("Podaj współrzędne!");
                    return;
                }
                
            }

            var statusValue = selectedWire.WireStatus; //sprawdzanie statusu wykonania przewodu

            var timespan = DateTime.Now - selectedWire.Start;
            var seconds = timespan.TotalSeconds;
            //  selectedWire.Seconds += seconds;

            //if (selectedWire.Overtime && selectedWire.WireStatus != (int?)Data.Status.AllConfirmed)
            //{
            //    ReasonOvertimeWindowKafelki subWindow = new ReasonOvertimeWindowKafelki();
            //    subWindow.ShowDialog();

            //    if (Data.ReasonDT == null)
            //    {
            //        MessageBox.Show("Nie podano powodu DT!");
            //        return;
            //    }
            //    else
            //        selectedWire.ReasonDT = Data.ReasonDT;
            //}


            switch (btn.Name) // sprawdzanie który przysk wybraliśmy i w zależności od niego dodajemy do parametru wireStatus wartość 1 = potwierdzone source,2 = potwierdzone target,3 = potwierdzone wszystko
            {
                case "btnSourceConfirm":
                    if(statusValue != (int?)Data.Status.SourceConfirmed && statusValue < (int?)Data.Status.AllConfirmed)
                    {
                        selectedWire.WireStatus += (int?)Data.Status.SourceConfirmed;
                        selectedWire.SecondsSource = seconds + selectedWire.HandlingTime;

                        Data.StartHandling = DateTime.Now;
                        //  Dispatcher.Invoke(new Action(() => btnSourceConfirm.Content = "Odznacz Source"));
                    }
                    else if(statusValue == (int?)Data.Status.SourceConfirmed || statusValue == (int?)Data.Status.AllConfirmed)
                    {
                        selectedWire.WireStatus -= (int?)Data.Status.SourceConfirmed;
                    //    selectedWire.SecondsDestination = seconds;
                        Data.StartHandling = Data.StartHandling.AddSeconds(-selectedWire.SecondsSource);
                        //  Dispatcher.Invoke(new Action(() => btnSourceConfirm.Content = "Potwierdz Source"));
                    }
                 //   listView.Items.Refresh();
                    break;
                case "btnTargetConfirm":
                    if (statusValue != (int?)Data.Status.TargetConfirmed && statusValue < (int?)Data.Status.AllConfirmed)
                    {
                        selectedWire.WireStatus += (int?)Data.Status.TargetConfirmed;
                        selectedWire.SecondsDestination = seconds + selectedWire.HandlingTime;

                        Data.StartHandling = DateTime.Now;
                        //  Dispatcher.Invoke(new Action(() => btnTargetConfirm.Content = "Odznacz Target"));
                    }
                    else if (statusValue == (int?)Data.Status.TargetConfirmed || statusValue == (int?)Data.Status.AllConfirmed)
                    {
                        selectedWire.WireStatus -= (int?)Data.Status.TargetConfirmed;
                        Data.StartHandling = Data.StartHandling.AddSeconds(-selectedWire.SecondsDestination);
                        CountSummaryTime(myData.ListOfImportedCabinets[_findedCabinetIndex]);
                        //  Dispatcher.Invoke(new Action(() => btnTargetConfirm.Content = "Potwierdź Target"));
                    }
                  //  listView.Items.Refresh();
                    break;
                case "btnConfirmBoth":
                    if (statusValue != (int?)Data.Status.AllConfirmed)
                    {
                        selectedWire.WireStatus = (int?)Data.Status.AllConfirmed;

                        var selectedWire2 = listView1.SelectedItem as Wire;
                        if (selectedWire2 is null)
                        {
                            listView1.SelectedIndex = _BusListIndex;
                            selectedWire2 = listView1.SelectedItem as Wire;
                        }

                        if (selectedWire2 != null)
                        {
                            

                            if(myData.ListOfImportedCabinets[_findedCabinetIndex].Where(x => selectedWire2.Bus == x.Bus).All(x => x.WireStatus == 3))
                                selectedWire2.WireStatus = 3;
                            else
                                selectedWire2.WireStatus = 1;
                            // Do something with the selected wire
                            // MessageBox.Show($"Selected Wire: {bus}, {number}");
                        }
                        //selectedWire.HandlingTime = secondsHandling - seconds;
                        //Data.StartHandling = DateTime.Now;
                        Data.StartHandling = DateTime.Now;
                        CountSummaryTime(myData.ListOfImportedCabinets[_findedCabinetIndex]);
                    }

                    else
                    {
                        selectedWire.WireStatus = (int?)Data.Status.Unconfirmed;
                        //   btnConfirmBoth.Content = "asdasd";
                        // Dispatcher.Invoke(new Action(() => btnConfirmBoth.Content = "Potwierdź wszystkie"));
                        var selectedWire2 = listView1.SelectedItem as Wire;
                        if (selectedWire2 is null)
                        {
                            listView1.SelectedIndex = _BusListIndex;
                            selectedWire2 = listView1.SelectedItem as Wire;
                        }
                        if (selectedWire2 != null)
                        {


                            if (!myData.ListOfImportedCabinets[_findedCabinetIndex].Where(x => selectedWire2.Bus == x.Bus).Any(x => x.WireStatus == 3))
                                BusList[_BusListIndex].WireStatus = 0;
                            else
                                BusList[_BusListIndex].WireStatus = 1;
                            // Do something with the selected wire
                            // MessageBox.Show($"Selected Wire: {bus}, {number}");
                        }
                        CountSummaryTime(myData.ListOfImportedCabinets[_findedCabinetIndex]);
                    }
                 //   listView.Items.Refresh();
                //    listView1.Items.Refresh();
                    break;

                default:
                    break;
            }
            selectedWire.MadeBy = Data.LoggedPerson;

            //double countOfProgress = 0;
            //for (int i = 0; i < myData.ListOfImportedCabinets[_findedCabinetIndex].Count; i++)
            //{
            //    if (myData.ListOfImportedCabinets[_findedCabinetIndex][i].WireStatus == (int?)Data.Status.SourceConfirmed ||
            //        myData.ListOfImportedCabinets[_findedCabinetIndex][i].WireStatus == (int?)Data.Status.TargetConfirmed)
            //        countOfProgress++;
            //    else if (myData.ListOfImportedCabinets[_findedCabinetIndex][i].WireStatus == (int?)Data.Status.AllConfirmed)
            //        countOfProgress += 2;

            //}

            //myData.ListOfImportedCabinets[_findedCabinetIndex].ForEach(x => x.Progress = Math.Round(  (countOfProgress / (myData.ListOfImportedCabinets[_findedCabinetIndex].Count * 2) * 100), 2));
            CountProgress();



            selectedWire.DateOfFinish = DateTime.Now;
           
            FileOperations.WriteListStatusToFile(_findedCabinetIndex, myData.ListOfImportedCabinets[_findedCabinetIndex]); //zapisywanie do pamięci danych o statusie potwierdzeń wszystkich przewodów w danej szafie


            var allValid = myData.ListOfImportedCabinets[_findedCabinetIndex].Any() && myData.ListOfImportedCabinets[_findedCabinetIndex].All(item => item.WireStatus == 3);



            if (allValid) //sprawdzanie czy wykonaliśmy już wszystkie przeowdy
            {
                Dispatcher.Invoke(new Action(() => labelPotwierdzonoWszystkiePrzewody.Visibility = Visibility.Visible));
                FileOperations.SaveLog(myData.ListOfImportedCabinets[_findedCabinetIndex][0].NameOfCabinet, myData.ListOfImportedCabinets[_findedCabinetIndex]);
            }
            else
            {
                Dispatcher.Invoke(new Action(() => labelPotwierdzonoWszystkiePrzewody.Visibility = Visibility.Hidden));
                FileOperations.SaveSingleLog(myData.ListOfImportedCabinets[_findedCabinetIndex][0].NameOfCabinet, selectedWire);
                if(selectedWire.WireStatus != (int?)Data.Status.AllConfirmed)
                {
                    selectedWire.Seconds = 0;
                    selectedWire.Start = DateTime.Now;
                }
                
                //if(selectedWire.WireStatus == (int?)Data.Status.AllConfirmed)
                //{
                //    if(selectedWire.Addnotations != null && selectedWire.Addnotations.Length > 0)
                //    {
                //        FileOperations.SaveComment(myData.ListOfImportedCabinets[_findedCabinetIndex][0].NameOfCabinet, selectedWire);
                //    }
                //}
            }

            if (selectedWire.WireStatus == (int?)Data.Status.AllConfirmed) //sprawdzenie czy przewód ma wszystko już potwierdzone
                MoveDownSelectedItemFromList(listView); //jeśli tak to przechodzimy do kolejnego przewodu
            else
                RefreshList(listView); // jeśli nie to odświeżamy tylko widok aplikacji

            listView.Items.Refresh();
            listView1.Items.Refresh();

        }
        private void CountProgress()
        {
            double countOfProgress = 0;
            double totalCount = 0;
            for (int i = 0; i < myData.ListOfImportedCabinets[_findedCabinetIndex].Count; i++)
            {
                if (myData.ListOfImportedCabinets[_findedCabinetIndex][i].WireStatus == (int?)Data.Status.SourceConfirmed)
                {
                    countOfProgress += myData.ListOfImportedCabinets[_findedCabinetIndex][i].TimeForExecuting / 2;
                }
                else if (myData.ListOfImportedCabinets[_findedCabinetIndex][i].WireStatus == (int?)Data.Status.TargetConfirmed)
                {
                    countOfProgress += myData.ListOfImportedCabinets[_findedCabinetIndex][i].TimeForExecuting / 2;
                }
                else if (myData.ListOfImportedCabinets[_findedCabinetIndex][i].WireStatus == (int?)Data.Status.AllConfirmed)
                {
                    countOfProgress += myData.ListOfImportedCabinets[_findedCabinetIndex][i].TimeForExecuting;
                }
                totalCount += myData.ListOfImportedCabinets[_findedCabinetIndex][i].TimeForExecuting;

            }
            myData.ListOfImportedCabinets[_findedCabinetIndex].ForEach(x => x.Progress = Math.Round((countOfProgress / (totalCount) * 100), 2));

        }

        public static List<List<string>> orginalItems;
        private void textBox_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Return)
            {
            //    var tempList = new List<Wire>();
            //    tempList = myData.ListOfImportedCabinets[_findedCabinetIndex];
            //    listView.ItemsSource = tempList.Select(item => item.DtSource.Contains(textBox.Text) || item.DtTarget.Contains(textBox.Text));


                var snToFind = textBox.Text.ToUpper();  // poczukiwany tekst to ten który został wpisany do kontorlki

              //  int curIndex = myData.ListOfImportedCabinets[_findedCabinetIndex].FindIndex(a => a.DtSource.ToUpper().Contains(snToFind));
         //       int curIndex = myData.ListOfImportedCabinets[_findedCabinetIndex].FindIndex(a => $"{a.DtSource.ToUpper()} <> {a.DtTarget.ToUpper()}".Equals(snToFind));

                int curIndex = myData.ListOfImportedCabinets[_findedCabinetIndex].FindIndex(a => $"{a.Nc.ToUpper()}".Equals(snToFind));

                if (curIndex >= 0) // jeśli index jest znaleziony
                {
                    listView.SelectedIndex = curIndex;
                    listView.Items.Refresh();
                    listView.Focus();
                    // listView.SetSelected(curIndex, true);
                }
                textBox.Text = string.Empty;


            }
        }

        private void buttonLogging_Click(object sender, RoutedEventArgs e)
        {
            ListOfNames.Clear();
            // Dispatcher.Invoke(new Action(() => comboBox.Clear()));

            Window2 subWindow = new Window2();
            subWindow.ShowDialog();
            this.Close();
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => {
                    image_All.Source = null;
                }));

            var item = listView1.SelectedItem;
            if (item == null) // jeśli żadnen elemnt nie jest wybrany to kończy metodę bez żadnej akcji
                return;

            int index = listView1.Items.IndexOf(item);
            _BusListIndex = index;

            var selectedWire = listView1.SelectedItem as Wire;

            if (selectedWire != null)
            {
                listView.ItemsSource = myData.ListOfImportedCabinets[_findedCabinetIndex].Where(x => x.Bus == selectedWire.Bus);
                // Access properties of the selected wire
                string bus = selectedWire.Bus;
                string number = selectedWire.Number;
                // Add more properties as needed

                // Do something with the selected wire
               // MessageBox.Show($"Selected Wire: {bus}, {number}");
            }
            FocusListViewOnFirstElement();
            RefreshList(listView);
        }

        private void textBox_TextChanged()
        {

        }

        private void listView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = listView.SelectedItem; //sprawdzanie czy mamy jakieś przewody do zatwierdzenia
            if (item != null)
            {
                // MessageBox.Show(item.ToString());
            }
            else
                return;

            int index = listView.Items.IndexOf(item);


            var selectedWire = listView.SelectedItem as Wire;
            if (selectedWire == null)
            {
                return;
                // Do something with the selected wire
                // MessageBox.Show($"Selected Wire: {bus}, {number}");
            }

            selectedWire.Start = DateTime.Now;
           // myData.ListOfImportedCabinets[_findedCabinetIndex][index].Start = DateTime.Now; //sprawdzanie statusu wykonania przewodu
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {

            }

        }

        private void textBox1_TextChanged(object sender, TextChangedEventArgs e)
        {
            var selectedWire = listView.SelectedItem as Wire;
            if (selectedWire != null)
            {

            }
        }

        private async void CameraTrigger_Click(object sender, RoutedEventArgs e)
        {
            var selectedWire = listView.SelectedItem as Wire;

            if (selectedWire == null)
            {
                return;
                // Do something with the selected wire
                // MessageBox.Show($"Selected Wire: {bus}, {number}");
            }

            try
            {
                bool result = await Camera.Checker(selectedWire.hostname, selectedWire.program, selectedWire.job, selectedWire.NameOfCabinet + "--" + Data.SetNumber);
                MessageBox.Show(result ? "Kamera została pomyślnie wyzwolona!" : "Błąd podczas wyzwalania kamery.",
                                "Wynik",
                                MessageBoxButton.OK,
                                result ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var selectedWire = listView.SelectedItem as Wire;
            if (selectedWire == null)
            {
                return;
                // Do something with the selected wire
                // MessageBox.Show($"Selected Wire: {bus}, {number}");
            }

            Window3 subWindow = new Window3(selectedWire);
            subWindow.ShowDialog();
            RefreshList(listView);
        }

        }
    }
